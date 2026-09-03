#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class AvailableVehicleCacheTests
    {
        [Fact]
        public void CacheFile_JsonRoundTrips()
        {
            var map = new Dictionary<string, AvailableVehicleCache.Entry>
            {
                ["DTG\\Class66\\RailVehicles\\loco.bin"] = new()
                {
                    SourceTicks = 100,
                    SourceSize = 2048,
                    Provider = "DTG",
                    Product = "Class66",
                    BlueprintId = "RailVehicles\\loco.xml",
                    Name = "Class 66",
                    Length = 21.34f,
                    Type = "Engine",
                    EntityCount = 3,
                    CargoCapacities = new[] { "0", "100" },
                    CargoAltEncodings = new[] { "0000000000000000", "0000000000000000" },
                    NumberingList = new[] { "66001", "66002" },
                    NameLocalisedStringXml = "<Localisation-cUserLocalisedString><English>Class 66</English></Localisation-cUserLocalisedString>",
                },
                ["DTG\\Class66\\scenery\\prop.bin"] = new()
                {
                    SourceTicks = 200,
                    SourceSize = 64,
                    NotAVehicle = true,
                },
            };

            string json = JsonSerializer.Serialize(map, VehicleCacheJsonContext.Default.DictionaryStringEntry);
            var restored = JsonSerializer.Deserialize(json, VehicleCacheJsonContext.Default.DictionaryStringEntry);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);

            var loco = restored["DTG\\Class66\\RailVehicles\\loco.bin"];
            Assert.False(loco.NotAVehicle);
            Assert.Equal("Engine", loco.Type);
            Assert.Equal(21.34f, loco.Length);
            Assert.Equal(new[] { "66001", "66002" }, loco.NumberingList);
            Assert.Equal(2, loco.CargoCapacities.Length);
            Assert.Contains("Class 66", loco.NameLocalisedStringXml);

            Assert.True(restored["DTG\\Class66\\scenery\\prop.bin"].NotAVehicle);
        }
    }
}
