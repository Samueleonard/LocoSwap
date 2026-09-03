#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LocoSwap
{
    /// <summary>
    /// Helpers for reading DTG .ap archives (which are plain zip files) using the
    /// framework's System.IO.Compression, with path-traversal-safe extraction.
    /// Replaces the abandoned/vulnerable DotNetZip dependency.
    /// </summary>
    public static class ZipHelper
    {
        public static ZipArchive OpenRead(string path)
        {
            return ZipFile.OpenRead(path);
        }

        public static void ExtractToStream(this ZipArchiveEntry entry, Stream target)
        {
            using (var source = entry.Open())
            {
                source.CopyTo(target);
            }
        }

        /// <summary>
        /// Extract a single entry beneath <paramref name="destinationDirectory"/>. When
        /// <paramref name="flatten"/> is false the entry's folder structure is preserved.
        /// Refuses to write outside the destination directory.
        /// </summary>
        public static string ExtractEntry(this ZipArchiveEntry entry, string destinationDirectory, bool flatten = false)
        {
            var relative = flatten ? entry.Name : entry.FullName;
            var destinationPath = ResolveSafePath(destinationDirectory, relative);
            CreateParentDirectory(destinationPath);
            entry.ExtractToFile(destinationPath, true);
            return destinationPath;
        }

        /// <summary>
        /// Extract every file entry whose path starts with <paramref name="prefix"/>,
        /// preserving structure beneath <paramref name="destinationDirectory"/>.
        /// </summary>
        public static void ExtractEntriesUnder(this ZipArchive archive, string prefix, string destinationDirectory)
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory marker
                var destinationPath = ResolveSafePath(destinationDirectory, entry.FullName);
                CreateParentDirectory(destinationPath);
                entry.ExtractToFile(destinationPath, true);
            }
        }

        private static void CreateParentDirectory(string filePath)
        {
            var parent = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        }

        private static string ResolveSafePath(string baseDirectory, string relativePath)
        {
            var root = Path.GetFullPath(baseDirectory);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;
            var cleaned = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(root, cleaned));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException(string.Format("Zip entry '{0}' would extract outside of '{1}'.", relativePath, baseDirectory));
            return full;
        }
    }
}
