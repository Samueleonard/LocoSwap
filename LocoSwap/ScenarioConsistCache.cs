#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Remembers the result of the "check all scenarios' consists when selecting route"
    /// scan so it does not have to re-run <c>serz.exe</c> and re-parse every <c>Scenario.bin</c>
    /// each time a route is selected. An entry is reused only while its source file's
    /// last-write time and size are unchanged; <see cref="Clear"/> (wired into
    /// <see cref="VehicleAvailibility.ClearTable"/>) drops everything when vehicle availability
    /// may have shifted. Persisted to %AppData%\LocoSwap\consist-cache.json.
    /// </summary>
    internal static class ScenarioConsistCache
    {
        internal sealed class Entry
        {
            public long SourceTicks { get; set; }
            public long SourceSize { get; set; }
            public string Result { get; set; } = nameof(ScenarioVehicleExistance.NotChecked);
        }

        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocoSwap", "consist-cache.json");

        private static readonly ConcurrentDictionary<string, Entry> _cache = Load();
        private static int _dirty;

        private static string Key(string routeId, string scenarioId, string apFileName)
            => routeId + "|" + scenarioId + "|" + apFileName;

        private static (long Ticks, long Size) SourceStamp(string routeId, string scenarioId, string apFileName)
        {
            string path = apFileName != ""
                ? apFileName
                : Path.Combine(Scenario.GetScenarioDirectory(routeId, scenarioId), "Scenario.bin");
            try
            {
                var info = new FileInfo(path);
                return info.Exists ? (info.LastWriteTimeUtc.Ticks, info.Length) : (0, 0);
            }
            catch
            {
                return (0, 0);
            }
        }

        public static ScenarioVehicleExistance? TryGet(string routeId, string scenarioId, string apFileName)
        {
            if (!_cache.TryGetValue(Key(routeId, scenarioId, apFileName), out Entry? entry)) return null;

            (long ticks, long size) = SourceStamp(routeId, scenarioId, apFileName);
            if (ticks == 0 || entry.SourceTicks != ticks || entry.SourceSize != size) return null;

            return Enum.TryParse(entry.Result, out ScenarioVehicleExistance parsed) ? parsed : null;
        }

        public static void Store(string routeId, string scenarioId, string apFileName, ScenarioVehicleExistance result)
        {
            if (result == ScenarioVehicleExistance.NotChecked) return;

            (long ticks, long size) = SourceStamp(routeId, scenarioId, apFileName);
            if (ticks == 0) return;

            _cache[Key(routeId, scenarioId, apFileName)] = new Entry
            {
                SourceTicks = ticks,
                SourceSize = size,
                Result = result.ToString(),
            };
            _dirty = 1;
        }

        public static void Clear()
        {
            if (_cache.IsEmpty) return;
            _cache.Clear();
            _dirty = 1;
        }

        /// <summary>Write pending changes to disk. Cheap no-op when nothing changed.</summary>
        public static void Flush()
        {
            if (System.Threading.Interlocked.Exchange(ref _dirty, 0) == 0) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                File.WriteAllText(CachePath, JsonSerializer.Serialize(
                    new Dictionary<string, Entry>(_cache), ConsistCacheJsonContext.Default.DictionaryStringEntry));
            }
            catch (Exception ex)
            {
                Log.Debug("Could not write consist cache: {Message}", ex.Message);
            }
        }

        private static ConcurrentDictionary<string, Entry> Load()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var loaded = JsonSerializer.Deserialize(
                        File.ReadAllText(CachePath), ConsistCacheJsonContext.Default.DictionaryStringEntry);
                    if (loaded != null) return new ConcurrentDictionary<string, Entry>(loaded);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Could not read consist cache: {Message}", ex.Message);
            }
            return new ConcurrentDictionary<string, Entry>();
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Dictionary<string, ScenarioConsistCache.Entry>))]
    internal partial class ConsistCacheJsonContext : JsonSerializerContext
    {
    }
}
