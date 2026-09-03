#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace LocoSwap
{
    /// <summary>One installed-content-missing vehicle inside a scenario consist.</summary>
    public class MissingConsistVehicle
    {
        public string ConsistName { get; set; } = "";
        public bool IsPlayerConsist { get; set; }
        public string VehicleName { get; set; } = "";
        public string Number { get; set; } = "";
        public string BlueprintPath { get; set; } = "";
        public bool HasRule { get; set; }
    }

    /// <summary>One scenario that has at least one broken/missing consist vehicle.</summary>
    public class BrokenScenario
    {
        public Scenario Scenario { get; set; } = null!;
        public string RouteName { get; set; } = "";
        public string RouteId { get; set; } = "";
        public ScenarioVehicleExistance Status { get; set; }
        public string Location { get; set; } = "";
        public List<MissingConsistVehicle> MissingVehicles { get; set; } = new List<MissingConsistVehicle>();

        public string ScenarioName => Scenario.Name;
        public string ScenarioId => Scenario.Id;
        public string PlayerTrainName => Scenario.PlayerTrainName;
        public bool InAp => Scenario.ApFileName != "";
        public int MissingCount => MissingVehicles.Count;

        public string DetailText => string.Join(Environment.NewLine, MissingVehicles.Select(v =>
            string.Format("[{0}{1}]  {2}{3}  ->  \\Assets\\{4}{5}",
                v.ConsistName,
                v.IsPlayerConsist ? " (player)" : "",
                string.IsNullOrEmpty(v.VehicleName) ? "?" : v.VehicleName,
                string.IsNullOrEmpty(v.Number) ? "" : " #" + v.Number,
                v.BlueprintPath,
                v.HasRule ? "  (a swap rule matches)" : "")));
    }

    /// <summary>
    /// Walks every installed route and every scenario in it, runs a full consist check on each,
    /// and collects the ones that reference vehicles which are not installed. Results already in
    /// <see cref="ScenarioConsistCache"/> as <see cref="ScenarioVehicleExistance.AllFound"/> are
    /// skipped without re-parsing, so a second run is fast.
    /// </summary>
    public static class ConsistScanner
    {
        public readonly record struct Progress(int Done, int Total, string CurrentRoute);

        public static async Task<List<BrokenScenario>> ScanAsync(
            IProgress<Progress>? progress, CancellationToken token)
        {
            Route[] routes = await Task.Run(Route.ListAllRoutes, token);

            // Enumerate scenarios per route (file/zip listing only - relatively cheap).
            var work = new List<(Route Route, Scenario Scenario)>();
            foreach (Route route in routes)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    foreach (Scenario scenario in MainWindow.BuildScenarioList(route))
                    {
                        work.Add((route, scenario));
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Consist scan: could not list scenarios for route {0}: {1}", route.Id, e.Message);
                }
            }

            var results = new ConcurrentBag<BrokenScenario>();
            int total = work.Count;
            int done = 0;
            int degree = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

            try
            {
                await Task.Run(() => Parallel.ForEach(
                    work,
                    new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = token },
                    item =>
                    {
                        try
                        {
                            BrokenScenario? broken = Inspect(item.Route, item.Scenario);
                            if (broken != null) results.Add(broken);
                        }
                        catch (Exception e)
                        {
                            Log.Debug("Consist scan failed for scenario {0}: {1}", item.Scenario.Id, e.Message);
                        }
                        finally
                        {
                            int d = Interlocked.Increment(ref done);
                            progress?.Report(new Progress(d, total, item.Route.Name));
                        }
                    }), token);
            }
            finally
            {
                ScenarioConsistCache.Flush();
            }

            return results
                .OrderByDescending(r => r.Status == ScenarioVehicleExistance.Missing)
                .ThenBy(r => r.RouteName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.ScenarioName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static BrokenScenario? Inspect(Route route, Scenario scenario)
        {
            // A cached clean result means there is nothing to report and nothing to re-parse.
            ScenarioVehicleExistance? cached =
                ScenarioConsistCache.TryGet(route.Id, scenario.Id, scenario.ApFileName);
            if (cached == ScenarioVehicleExistance.AllFound) return null;

            List<MissingConsistVehicle> missing = scenario.FindMissingVehicles();
            if (missing.Count == 0) return null;

            string location = scenario.ApFileName != ""
                ? scenario.ApFileName
                : Path.Combine(scenario.ScenarioDirectory, "Scenario.bin");

            return new BrokenScenario
            {
                Scenario = scenario,
                RouteName = route.Name,
                RouteId = route.Id,
                Status = scenario.ScenarioVehiclesExist,
                Location = location,
                MissingVehicles = missing,
            };
        }
    }
}
