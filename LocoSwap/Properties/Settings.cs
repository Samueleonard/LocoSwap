#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Serilog;

namespace LocoSwap.Properties
{
    /// <summary>
    /// Application settings, persisted as JSON under %AppData%\LocoSwap\settings.json.
    /// Replaces the old .NET Framework <c>ApplicationSettingsBase</c> / <c>Settings.settings</c>
    /// stack. The <see cref="Default"/> singleton is bound directly from XAML, so it stays
    /// <see cref="INotifyPropertyChanged"/> and keeps a parameterless <see cref="Save"/>.
    /// </summary>
    public sealed class Settings : INotifyPropertyChanged
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocoSwap");
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private static Settings? _default;
        public static Settings Default => _default ??= Load();

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _tsPath = "";
        public string TsPath { get => _tsPath; set => Set(ref _tsPath, value); }

        public SwapPreset Preset { get; set; } = new SwapPreset();

        private string _language = "";
        public string Language { get => _language; set => Set(ref _language, value); }

        public List<string> FavoriteRoutes { get; set; } = new List<string>();

        private string _scenarioNameSuffix = "[LoSw]";
        public string ScenarioNameSuffix { get => _scenarioNameSuffix; set => Set(ref _scenarioNameSuffix, value); }

        private bool _checkScenarioConsists;
        public bool CheckScenarioConsists { get => _checkScenarioConsists; set => Set(ref _checkScenarioConsists, value); }

        private bool _doNotAutoArchiveWorkshopRoutes = true;
        public bool DoNotAutoArchiveWorkshopRoutes { get => _doNotAutoArchiveWorkshopRoutes; set => Set(ref _doNotAutoArchiveWorkshopRoutes, value); }

        private string _mainWindowPlacement = "";
        public string MainWindowPlacement { get => _mainWindowPlacement; set => Set(ref _mainWindowPlacement, value); }

        private string _lastRouteId = "";
        public string LastRouteId { get => _lastRouteId; set => Set(ref _lastRouteId, value); }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, SettingsJsonContext.Default.Settings));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save settings to {Path}", SettingsPath);
            }
        }

        private static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    Settings? loaded = JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), SettingsJsonContext.Default.Settings);
                    if (loaded != null) return loaded;
                }
                else
                {
                    Settings? migrated = TryMigrateLegacy();
                    if (migrated != null)
                    {
                        migrated.Save();
                        return migrated;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load settings from {Path}; using defaults", SettingsPath);
            }
            return new Settings();
        }

        /// <summary>
        /// Best-effort one-time import of the pre-.NET 10 user-scoped settings, which lived in
        /// %LOCALAPPDATA%\&lt;AssemblyName&gt;_&lt;hash&gt;\&lt;version&gt;\user.config.
        /// </summary>
        private static Settings? TryMigrateLegacy()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!Directory.Exists(localAppData)) return null;

                FileInfo? newest = Directory
                    .EnumerateDirectories(localAppData, "LocoSwap*")
                    .SelectMany(dir => Directory.EnumerateFiles(dir, "user.config", SearchOption.AllDirectories))
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (newest == null) return null;

                var settings = new Settings();
                foreach (XElement setting in XDocument.Load(newest.FullName).Descendants("setting"))
                {
                    string? name = (string?)setting.Attribute("name");
                    if (name == null) continue;
                    XElement? valueEl = setting.Element("value");
                    string value = valueEl?.Value ?? "";
                    switch (name)
                    {
                        case "TsPath": settings.TsPath = value; break;
                        case "Language": settings.Language = value; break;
                        case "ScenarioNameSuffix": settings.ScenarioNameSuffix = value; break;
                        case "MainWindowPlacement": settings.MainWindowPlacement = value; break;
                        case "LastRouteId": settings.LastRouteId = value; break;
                        case "CheckScenarioConsists": settings.CheckScenarioConsists = value == "True"; break;
                        case "DoNotAutoArchiveWorkshopRoutes": settings.DoNotAutoArchiveWorkshopRoutes = value == "True"; break;
                        case "FavoriteRoutes":
                            foreach (XElement s in valueEl?.Descendants("string") ?? Enumerable.Empty<XElement>())
                            {
                                settings.FavoriteRoutes.Add(s.Value);
                            }
                            break;
                        case "Preset":
                            foreach (XElement item in valueEl?.Descendants("SwapPresetItem") ?? Enumerable.Empty<XElement>())
                            {
                                settings.Preset.List.Add(new SwapPresetItem
                                {
                                    TargetName = (string?)item.Element("TargetName") ?? "",
                                    TargetXmlPath = (string?)item.Element("TargetXmlPath") ?? "",
                                    NewName = (string?)item.Element("NewName") ?? "",
                                    NewXmlPath = (string?)item.Element("NewXmlPath") ?? "",
                                    NewLength = float.TryParse((string?)item.Element("NewLength"), out float len) ? len : 0f,
                                });
                            }
                            break;
                    }
                }
                Log.Information("Migrated legacy settings from {Path}", newest.FullName);
                return settings;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Legacy settings migration failed; using defaults");
                return null;
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Settings))]
    internal partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
