#nullable enable
using System.Text.Json;
using LocoSwap;
using LocoSwap.Properties;
using Xunit;

namespace LocoSwap.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void Settings_JsonRoundTrips()
        {
            var settings = new Settings
            {
                TsPath = @"C:\Games\RailWorks",
                Language = "de",
                ScenarioNameSuffix = "[X]",
                CheckScenarioConsists = true,
                DoNotAutoArchiveWorkshopRoutes = false,
                MainWindowPlacement = "1;2;3;4;Normal",
                LastRouteId = "route-42",
            };
            settings.FavoriteRoutes.Add("fav-1");
            settings.FavoriteRoutes.Add("fav-2");
            settings.Preset.List.Add(new SwapPresetItem
            {
                TargetName = "Old",
                TargetXmlPath = @"a\b.xml",
                NewName = "New",
                NewXmlPath = @"c\d.xml",
                NewLength = 19.5f,
            });

            string json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings);
            var restored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.Settings);

            Assert.NotNull(restored);
            Assert.Equal(settings.TsPath, restored!.TsPath);
            Assert.Equal("de", restored.Language);
            Assert.Equal("[X]", restored.ScenarioNameSuffix);
            Assert.True(restored.CheckScenarioConsists);
            Assert.False(restored.DoNotAutoArchiveWorkshopRoutes);
            Assert.Equal("route-42", restored.LastRouteId);
            Assert.Equal(new[] { "fav-1", "fav-2" }, restored.FavoriteRoutes);
            Assert.Single(restored.Preset.List);
            Assert.Equal(@"a\b.xml", restored.Preset.List[0].TargetXmlPath);
            Assert.Equal(19.5f, restored.Preset.List[0].NewLength);
        }

        [Fact]
        public void Settings_DefaultsMatchLegacyValues()
        {
            var settings = new Settings();

            Assert.Equal("", settings.TsPath);
            Assert.Equal("", settings.Language);
            Assert.Equal("[LoSw]", settings.ScenarioNameSuffix);
            Assert.False(settings.CheckScenarioConsists);
            Assert.True(settings.DoNotAutoArchiveWorkshopRoutes);
            Assert.Empty(settings.FavoriteRoutes);
            Assert.NotNull(settings.Preset);
            Assert.Empty(settings.Preset.List);
        }

        [Fact]
        public void Settings_RaisesPropertyChanged()
        {
            var settings = new Settings();
            string? changed = null;
            settings.PropertyChanged += (_, e) => changed = e.PropertyName;

            settings.Language = "fr";

            Assert.Equal(nameof(Settings.Language), changed);
        }
    }
}
