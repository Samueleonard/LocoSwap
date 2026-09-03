#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using LocoSwap;
using Xunit;

namespace LocoSwap.Tests
{
    public class ZipHelperTests : IDisposable
    {
        private readonly string _workDir;

        public ZipHelperTests()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "LocoSwap.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_workDir, recursive: true); } catch { /* best effort */ }
        }

        private string CreateZip(params (string Name, string Content)[] entries)
        {
            string path = Path.Combine(_workDir, Guid.NewGuid().ToString("N") + ".zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
            return path;
        }

        [Fact]
        public void ExtractEntry_PreservesFolderStructure()
        {
            string zipPath = CreateZip(("Scenarios/abc/Scenario.bin", "payload"));
            string dest = Path.Combine(_workDir, "out");

            using var archive = ZipFile.OpenRead(zipPath);
            string written = archive.GetEntry("Scenarios/abc/Scenario.bin")!.ExtractEntry(dest);

            Assert.Equal(Path.Combine(dest, "Scenarios", "abc", "Scenario.bin"), written);
            Assert.Equal("payload", File.ReadAllText(written));
        }

        [Fact]
        public void ExtractEntry_Flatten_DropsFolders()
        {
            string zipPath = CreateZip(("RailVehicles/deep/loco.bin", "x"));
            string dest = Path.Combine(_workDir, "flat");

            using var archive = ZipFile.OpenRead(zipPath);
            string written = archive.GetEntry("RailVehicles/deep/loco.bin")!.ExtractEntry(dest, flatten: true);

            Assert.Equal(Path.Combine(dest, "loco.bin"), written);
        }

        [Theory]
        [InlineData("../escape.txt")]
        [InlineData("Scenarios/../../escape.txt")]
        public void ExtractEntry_RefusesPathTraversal(string maliciousName)
        {
            string zipPath = CreateZip((maliciousName, "evil"));
            string dest = Path.Combine(_workDir, "guarded");
            Directory.CreateDirectory(dest);

            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(maliciousName)!;

            Assert.Throws<IOException>(() => entry.ExtractEntry(dest));
            Assert.False(File.Exists(Path.Combine(_workDir, "escape.txt")));
        }

        [Fact]
        public void ExtractEntriesUnder_OnlyTakesMatchingPrefix()
        {
            string zipPath = CreateZip(
                ("Scenarios/id1/Scenario.bin", "a"),
                ("Scenarios/id1/ScenarioProperties.xml", "b"),
                ("Scenarios/id2/Scenario.bin", "c"));
            string dest = Path.Combine(_workDir, "under");

            using var archive = ZipFile.OpenRead(zipPath);
            archive.ExtractEntriesUnder("Scenarios/id1/", dest);

            Assert.True(File.Exists(Path.Combine(dest, "Scenarios", "id1", "Scenario.bin")));
            Assert.True(File.Exists(Path.Combine(dest, "Scenarios", "id1", "ScenarioProperties.xml")));
            Assert.False(File.Exists(Path.Combine(dest, "Scenarios", "id2", "Scenario.bin")));
        }
    }
}
