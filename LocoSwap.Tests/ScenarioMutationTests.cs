#nullable enable
using System.Linq;
using System.Xml.Linq;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    /// <summary>
    /// XML-level tests for the vehicle-number bookkeeping in <see cref="Scenario"/>. These
    /// bypass serz.exe by feeding a hand-built Scenario.bin document through the internal
    /// <see cref="Scenario.ScenarioXmlForTest"/> seam.
    /// </summary>
    public class ScenarioMutationTests
    {
        private static Scenario ScenarioWith(XDocument doc)
        {
            return new Scenario { ScenarioXmlForTest = doc };
        }

        private static XDocument SampleConsist() => XDocument.Parse(@"
<cRecordSet>
  <Record>
    <cConsist>
      <RailVehicles>
        <cOwnedEntity><Name>V1</Name><UniqueNumber>111</UniqueNumber></cOwnedEntity>
        <cOwnedEntity><Name>V2</Name><UniqueNumber>222</UniqueNumber></cOwnedEntity>
      </RailVehicles>
      <Driver>
        <cDriver>
          <InitialRV>
            <e>111</e>
            <e>222</e>
          </InitialRV>
        </cDriver>
      </Driver>
    </cConsist>
    <cConsistOperations>
      <DeltaTarget>
        <cDriverInstructionTarget>
          <RailVehicleNumber>
            <e>111</e>
            <e>222</e>
          </RailVehicleNumber>
        </cDriverInstructionTarget>
      </DeltaTarget>
    </cConsistOperations>
  </Record>
</cRecordSet>");

        [Fact]
        public void ChangeVehicleNumber_UpdatesUniqueNumberInitialRvAndMatchingInstructions()
        {
            XDocument doc = SampleConsist();
            Scenario scenario = ScenarioWith(doc);

            scenario.ChangeVehicleNumber(0, 0, "999");

            string[] unique = doc.Descendants("UniqueNumber").Select(e => e.Value).ToArray();
            Assert.Equal(new[] { "999", "222" }, unique);

            string[] initialRv = doc.Descendants("InitialRV").Elements("e").Select(e => e.Value).ToArray();
            Assert.Equal(new[] { "999", "222" }, initialRv);

            string[] instr = doc.Descendants("RailVehicleNumber").Elements("e").Select(e => e.Value).ToArray();
            Assert.Equal(new[] { "999", "222" }, instr);
        }

        [Fact]
        public void RemoveVehicle_DropsEntityInitialRvEntryAndInstructionTarget()
        {
            XDocument doc = SampleConsist();
            Scenario scenario = ScenarioWith(doc);

            scenario.RemoveVehicle(0, 1);

            Assert.Single(doc.Descendants("cOwnedEntity"));
            Assert.Equal(new[] { "111" }, doc.Descendants("InitialRV").Elements("e").Select(e => e.Value).ToArray());
            Assert.Equal(new[] { "111" }, doc.Descendants("RailVehicleNumber").Elements("e").Select(e => e.Value).ToArray());
        }
    }
}
