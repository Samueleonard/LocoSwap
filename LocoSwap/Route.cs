using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using Serilog;

namespace LocoSwap
{
    public class Route : ModelBase
    {
        private XDocument RouteProperties;
        private string _id;
        private string _name;
        private bool _isFavorite;
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }
        public string RouteDirectory
        {
            get
            {
                return GetRouteDirectory(Id);
            }
        }
        private bool _isArchived = false;
        public bool IsWorkshop { set; get; } = false;
        public bool IsArchived
        {
            get => _isArchived; set
            {
                _isArchived = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsArchived"));
            }
        }


        public Dictionary<string, ScenarioDb.ScenarioCompletion> LocalScenarioDb { get; } = new Dictionary<string, ScenarioDb.ScenarioCompletion>();

        public Route()
        {
            Id = "";
            Name = "Name not available";
        }

        public Route(string id)
        {
            Load(id);
        }

        public void Load(string id)
        {
            Id = id;

            string xmlPath = Path.Combine(RouteDirectory, "RouteProperties.xml");
            string xmlArchivedPath = Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff");
            string xmlToLoad = "";

            if (File.Exists(xmlPath))
            {
                xmlToLoad = xmlPath;
            }
            else if (File.Exists(xmlArchivedPath))
            {
                xmlToLoad = xmlArchivedPath;
                IsArchived = true;
            }
            else
            {
                // Look in .ap files (or archived .ap.LSoff files)
                string apFileContainingRouteProperties = "";
                xmlToLoad = Path.Combine(Utilities.GetTempDir(), "RouteProperties.xml");
                Utilities.RemoveFile(xmlToLoad);

                string[] allowedExtensions = new[] { ".ap", ".ap.LSoff" };
                string[] apFiles = Directory.GetFiles(RouteDirectory, "*", SearchOption.TopDirectoryOnly).Where(file => allowedExtensions.Any(file.EndsWith)).ToArray();

                foreach (string apPath in apFiles)
                {
                    try
                    {
                        using (var zipFile = ZipHelper.OpenRead(apPath))
                        {
                            var apEntry = zipFile.Entries.FirstOrDefault(entry => entry.FullName == "RouteProperties.xml");
                            if (apEntry == null) continue;
                            apEntry.ExtractEntry(Utilities.GetTempDir());
                            apFileContainingRouteProperties = apPath;

                            IsArchived = apPath.EndsWith(".LSoff");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Could not unzip " + apPath + ", " + e.Message);
                    }
                }
                if (apFileContainingRouteProperties == "") throw new Exception("RouteProperties.xml not found for this route ID");
            }
            RouteProperties = XmlDocumentLoader.Load(xmlToLoad);

            XElement displayName = RouteProperties.XPathSelectElement("/cRouteProperties/DisplayName/Localisation-cUserLocalisedString");
            Name = Utilities.DetermineDisplayName(displayName);

            XElement workshopId = RouteProperties.XPathSelectElement("/cRouteProperties/WorkshopId");
            if (workshopId != null)
            {
                IsWorkshop = workshopId.Value != "0";
            }

            IsFavorite = Properties.Settings.Default.FavoriteRoutes?.IndexOf(Id) >= 0;

            // Read local scenario completion DB (JSON, with one-time fallback to the legacy XML format)
            try
            {
                List<SerializableScenarioDb> localDb = null;
                if (File.Exists(LocalScenarioDbPath))
                {
                    localDb = JsonSerializer.Deserialize(File.ReadAllText(LocalScenarioDbPath),
                        ScenarioDbJsonContext.Default.ListSerializableScenarioDb);
                }
                else if (File.Exists(LegacyLocalScenarioDbPath))
                {
                    using FileStream fs = File.OpenRead(LegacyLocalScenarioDbPath);
                    localDb = (List<SerializableScenarioDb>)new XmlSerializer(typeof(List<SerializableScenarioDb>)).Deserialize(fs);
                }

                if (localDb != null)
                {
                    foreach (SerializableScenarioDb entry in localDb)
                    {
                        LocalScenarioDb[entry.Key] = ScenarioDb.parseCompletion(entry.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Couldn't read local scenario database, " + e.Message);
            }
        }

        private string LocalScenarioDbPath => Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.json");
        private string LegacyLocalScenarioDbPath => Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.xml");

        public static string GetRoutesDirectory()
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "Content", "Routes");
        }

        public static string GetRouteDirectory(string routeId)
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "Content\\Routes", routeId);
        }

        public static Route[] ListAllRoutes()
        {
            List<Route> ret = new List<Route>();
            string[] routeDirectories = Directory.GetDirectories(GetRoutesDirectory());
            foreach (string directory in routeDirectories)
            {
                string id = new DirectoryInfo(directory).Name;
                try
                {
                    ret.Add(new Route(id));
                }
                catch (Exception e)
                {
                    Log.Error("Route in directory {0} is not a valid route: {1}", id, e.Message);
                }
            }
            return ret.ToArray();
        }

        public void ToggleArchive()
        {
            if (IsArchived)
            {
                string[] allArchivedFiles = Directory.GetFiles(RouteDirectory, "*.LSoff", SearchOption.TopDirectoryOnly);

                foreach (string file in allArchivedFiles)
                {
                    try
                    {
                        File.Move(file, file.Substring(0, file.Length - 6));
                    }
                    catch (IOException) // Target file already exists
                    { }
                }

                IsArchived = false;
            }
            else
            {
                Dictionary<string, ScenarioDb.ScenarioCompletion> filteredSDBCache = ScenarioDb.getScenarioDbRouteInfos(Id).Where(i =>
                    i.Value == ScenarioDb.ScenarioCompletion.CompletedSuccessfully ||
                    i.Value == ScenarioDb.ScenarioCompletion.CompletedFailed)
                    .ToDictionary(i => i.Key, i => i.Value);

                // Merge the scenario completion statuses that we found in SDBCache.bin and in LocoSwap_ScenarioDb.xml
                Dictionary<string, ScenarioDb.ScenarioCompletion> mergedScenarioCompletionDb =
                    filteredSDBCache.Concat(LocalScenarioDb.Where(x => !filteredSDBCache.Keys.Contains(x.Key))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); ;

                List<SerializableScenarioDb> entries = new List<SerializableScenarioDb>(mergedScenarioCompletionDb.Count);
                foreach (string key in mergedScenarioCompletionDb.Keys)
                {
                    entries.Add(new SerializableScenarioDb(key, mergedScenarioCompletionDb[key]));
                }

                if (entries.Count != 0)
                {
                    File.WriteAllText(LocalScenarioDbPath, JsonSerializer.Serialize(entries,
                        ScenarioDbJsonContext.Default.ListSerializableScenarioDb));

                    // The JSON file now supersedes any legacy XML copy
                    if (File.Exists(LegacyLocalScenarioDbPath))
                    {
                        try { File.Delete(LegacyLocalScenarioDbPath); } catch { /* best effort */ }
                    }
                }

                // Do the actual route archiving (renaming)
                // Note : even if we found a RouteProperties.xml to rename, we still have to scan the .ap's
                if (File.Exists(Path.Combine(RouteDirectory, "RouteProperties.xml")))
                {
                    if (File.Exists(Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff")))
                    {
                        File.Delete(Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff"));
                    }
                    File.Move(Path.Combine(RouteDirectory, "RouteProperties.xml"), Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff"));
                }

                string[] apFiles = Directory.GetFiles(RouteDirectory, "*.ap", SearchOption.TopDirectoryOnly);
                foreach (string apPath in apFiles)
                {
                    bool apEntryExists;
                    using (var zipFile = ZipHelper.OpenRead(apPath))
                    {
                        apEntryExists = zipFile.Entries.Any(entry => entry.FullName == "RouteProperties.xml");
                    }

                    if (apEntryExists)
                    {
                        if (File.Exists(apPath + ".LSoff"))
                        {
                            File.Delete(apPath + ".LSoff");
                        }
                        File.Move(apPath, apPath + ".LSoff");
                        break;
                    }
                }
                IsArchived = true;
            }
        }
    }

    public class SerializableScenarioDb
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public SerializableScenarioDb()
        {
        }

        public SerializableScenarioDb(string key, ScenarioDb.ScenarioCompletion value)
        {
            Key = key;
            Value = value.ToString();
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<SerializableScenarioDb>))]
    internal partial class ScenarioDbJsonContext : JsonSerializerContext
    {
    }
}
