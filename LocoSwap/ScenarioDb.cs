using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Serilog;

namespace LocoSwap
{
    public static class ScenarioDb
    {
        public enum DBState
        {
            Loading,
            Loaded,
            Error,
            Init
        }
        public enum ScenarioCompletion
        {
            CompletedSuccessfully,
            CompletedFailed,
            NotCompleted,
            NotInDB,
            Unknown
        }

        // Represents the scenario completion infos as found in the SDBCache.bin TS file
        // The first key is the route UUID, the second is the scenario UUID.
        // The reference is swapped atomically by ParseScenarioDb once it is fully built, so
        // readers only ever observe a complete map (never a half-populated one).
        private static volatile Dictionary<string, Dictionary<string, ScenarioCompletion>> scenarioDb =
            new Dictionary<string, Dictionary<string, ScenarioCompletion>>();

        public static DBState dbState = DBState.Init;

        public static ScenarioCompletion getScenarioDbInfos(string routeId, string scenarioId)
        {
            var db = scenarioDb;
            if (db.TryGetValue(routeId, out var route) && route.TryGetValue(scenarioId, out var completion))
            {
                return completion;
            }
            else if (dbState == DBState.Loaded)
            {
                return ScenarioCompletion.NotInDB;
            }
            return ScenarioCompletion.Unknown;
        }

        // Get all scenario completion status for one route, for the archiving feature
        public static Dictionary<string, ScenarioCompletion> getScenarioDbRouteInfos(string routeId)
        {
            return scenarioDb.TryGetValue(routeId, out var route) ? route : new Dictionary<string, ScenarioCompletion>();
        }

        public static void ParseScenarioDb()
        {
            Log.Debug("ParseScenarioDb invoked");
            // Protect against concurrent runs
            if (dbState == DBState.Loading)
            {
                Log.Debug("SDB Already loading : abort");
                return;
            }

            dbState = DBState.Loading;
            var newDb = new Dictionary<string, Dictionary<string, ScenarioCompletion>>();

            string dbPath = Path.Combine(Properties.Settings.Default.TsPath, "Content", "SDBCache.bin");
            if (File.Exists(dbPath))
            {
                try
                {
                    string xmlScenarioDbPath = TsSerializer.BinToXml(dbPath);
                    FileStream origStream = File.OpenRead(xmlScenarioDbPath);
                    CleanTextReader streamReader = new CleanTextReader(origStream);

                    XmlReader XReaderSDB = XmlReader.Create(streamReader);

                    // Browse scenarios
                    while (XReaderSDB.ReadToFollowing("sSDScenario"))
                    {
                        // Read route Id
                        XReaderSDB.ReadToFollowing("DevString");
                        XReaderSDB.Read();
                        string routeId = XReaderSDB.Value;

                        // Read scenario Id
                        XReaderSDB.ReadToFollowing("DevString");
                        XReaderSDB.Read();
                        string scenarioId = XReaderSDB.Value;

                        // Read completion status
                        XReaderSDB.ReadToFollowing("Completion");
                        XReaderSDB.Read();

                        // Add completion status to our internal DB
                        if (!newDb.ContainsKey(routeId))
                        {
                            newDb[routeId] = new Dictionary<string, ScenarioCompletion>();
                        }
                        newDb[routeId][scenarioId] = parseCompletion(XReaderSDB.Value);
                    }

                    // Publish the fully-built map in one atomic reference swap
                    scenarioDb = newDb;
                    dbState = DBState.Loaded;

                    // Uncompressed DB can be quite large, we delete it now instead of waiting for the next LocoSwap launch
                    origStream.Close();
                    File.Delete(xmlScenarioDbPath);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to parse SDBCache.bin");
                    dbState = DBState.Error;
                }

            }
            else
            {
                Log.Debug("SDBCache.bin not found");
                dbState = DBState.Error;
            }
            Log.Debug("SDB has been read");
        }

        public static ScenarioCompletion parseCompletion(string input)
        {
            ScenarioCompletion parsedReturn = ScenarioCompletion.Unknown;

            switch (input)
            {
                case "NotCompleted":
                    parsedReturn = ScenarioCompletion.NotCompleted;
                    break;
                case "CompletedSuccessfully":
                    parsedReturn = ScenarioCompletion.CompletedSuccessfully;
                    break;
                case "CompletedFailed":
                    parsedReturn = ScenarioCompletion.CompletedFailed;
                    break;
            }
            return parsedReturn;
        }
    }



    /**
     * Thanks to https://stackoverflow.com/a/38155562, we have a nice XML cleaner
     */
    internal class CleanTextReader : StreamReader
    {
        public CleanTextReader(Stream stream) : base(stream)
        {
        }

        public override int Read()
        {
            var val = base.Read();
            return XmlConvert.IsXmlChar((char)val) ? val : (char)' ';
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var ret = base.Read(buffer, index, count);

            for (int i = 0; i < ret; i++)
            {
                int idx = index + i;
                if (!XmlConvert.IsXmlChar(buffer[idx]))
                    buffer[idx] = ' ';
            }

            return ret;
        }

        public override int ReadBlock(char[] buffer, int index, int count)
        {
            var ret = base.ReadBlock(buffer, index, count);

            for (int i = 0; i < ret; i++)
            {
                int idx = index + i;
                if (!XmlConvert.IsXmlChar(buffer[idx]))
                    buffer[idx] = ' ';
            }

            return ret;
        }
    }
}
