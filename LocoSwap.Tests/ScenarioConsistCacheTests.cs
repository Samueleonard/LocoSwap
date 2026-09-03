#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class ScenarioConsistCacheTests
    {
        [Fact]
        public void CacheFile_JsonRoundTrips()
        {
            var map = new Dictionary<string, ScenarioConsistCache.Entry>
            {
                ["route-a|scen-1|"] = new() { SourceTicks = 123, SourceSize = 456, Result = "AllFound" },
                ["route-a|scen-2|C:\\pack.ap"] = new() { SourceTicks = 789, SourceSize = 10, Result = "Missing" },
            };

            string json = JsonSerializer.Serialize(map, ConsistCacheJsonContext.Default.DictionaryStringEntry);
            var restored = JsonSerializer.Deserialize(json, ConsistCacheJsonContext.Default.DictionaryStringEntry);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);
            Assert.Equal(123, restored["route-a|scen-1|"].SourceTicks);
            Assert.Equal("Missing", restored["route-a|scen-2|C:\\pack.ap"].Result);
        }
    }
}
