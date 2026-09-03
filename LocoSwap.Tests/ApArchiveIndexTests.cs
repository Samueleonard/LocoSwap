#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class ApArchiveIndexTests : IDisposable
    {
        private readonly string _workDir;

        public ApArchiveIndexTests()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "LocoSwap.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
            ApArchiveIndex.Clear();
        }

        public void Dispose()
        {
            try { Directory.Delete(_workDir, recursive: true); } catch { }
        }

        private string CreateZip(string name, params string[] entries)
        {
            string path = Path.Combine(_workDir, name);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var entry in entries) zip.CreateEntry(entry);
            return path;
        }

        [Fact]
        public void GetEntryNames_ReturnsAllEntries()
        {
            string zip = CreateZip("a.ap", "RailVehicles/x/loco.bin", "Scenarios/y/Scenario.bin");

            var names = ApArchiveIndex.GetEntryNames(zip);

            Assert.Equal(2, names.Count);
            Assert.Contains("RailVehicles/x/loco.bin", names);
        }

        [Fact]
        public void GetEntryNames_MissingArchive_ReturnsEmpty()
        {
            var names = ApArchiveIndex.GetEntryNames(Path.Combine(_workDir, "nope.ap"));
            Assert.Empty(names);
        }

        [Fact]
        public void GetEntryNames_RefreshesWhenArchiveChanges()
        {
            string zip = CreateZip("b.ap", "one.bin");
            Assert.Single(ApArchiveIndex.GetEntryNames(zip));

            // Rewrite with different content and a newer timestamp
            File.Delete(zip);
            CreateZip("b.ap", "one.bin", "two.bin");
            File.SetLastWriteTimeUtc(zip, DateTime.UtcNow.AddMinutes(1));

            Assert.Equal(2, ApArchiveIndex.GetEntryNames(zip).Count);
        }
    }
}
