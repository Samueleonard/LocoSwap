#nullable enable
using System;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class UtilitiesTests
    {
        [Fact]
        public void GetUUIDLongs_RoundTripsThroughGuidBytes()
        {
            var guid = Guid.NewGuid();
            var (low, high) = Utilities.GetUUIDLongs(guid);

            var rebuilt = new byte[16];
            BitConverter.GetBytes(low).CopyTo(rebuilt, 0);
            BitConverter.GetBytes(high).CopyTo(rebuilt, 8);

            Assert.Equal(guid.ToByteArray(), rebuilt);
        }

        [Fact]
        public void GenerateCGUID_EmitsUuidAndDevString()
        {
            var element = Utilities.GenerateCGUID();

            Assert.Equal("cGUID", element.Name.LocalName);
            Assert.Equal(2, System.Linq.Enumerable.Count(element.Element("UUID")!.Elements("e")));
            Assert.False(string.IsNullOrEmpty(element.Element("DevString")!.Value));
        }
    }
}
