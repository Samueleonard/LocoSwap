#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class ScenarioDbTests
    {
        [Theory]
        [InlineData("NotCompleted", ScenarioDb.ScenarioCompletion.NotCompleted)]
        [InlineData("CompletedSuccessfully", ScenarioDb.ScenarioCompletion.CompletedSuccessfully)]
        [InlineData("CompletedFailed", ScenarioDb.ScenarioCompletion.CompletedFailed)]
        [InlineData("something else", ScenarioDb.ScenarioCompletion.Unknown)]
        [InlineData("", ScenarioDb.ScenarioCompletion.Unknown)]
        public void ParseCompletion_MapsKnownValues(string input, ScenarioDb.ScenarioCompletion expected)
        {
            Assert.Equal(expected, ScenarioDb.parseCompletion(input));
        }

        [Fact]
        public void LocalScenarioDb_JsonRoundTrips()
        {
            var entries = new List<SerializableScenarioDb>
            {
                new("route-1", ScenarioDb.ScenarioCompletion.CompletedSuccessfully),
                new("route-2", ScenarioDb.ScenarioCompletion.CompletedFailed),
            };

            string json = JsonSerializer.Serialize(entries, ScenarioDbJsonContext.Default.ListSerializableScenarioDb);
            var restored = JsonSerializer.Deserialize(json, ScenarioDbJsonContext.Default.ListSerializableScenarioDb);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);
            Assert.Equal("route-1", restored[0].Key);
            Assert.Equal("CompletedSuccessfully", restored[0].Value);
            Assert.Equal(ScenarioDb.ScenarioCompletion.CompletedFailed, ScenarioDb.parseCompletion(restored[1].Value));
        }
    }
}
