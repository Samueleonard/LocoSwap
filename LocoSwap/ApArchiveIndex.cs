#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Caches the entry list of DTG <c>.ap</c> archives. A full vehicle scan otherwise re-opens
    /// the same archives hundreds of times just to test whether a file is present; this serves
    /// those checks from memory and re-reads an archive only when its last-write time changes.
    /// </summary>
    internal static class ApArchiveIndex
    {
        private sealed class CacheEntry
        {
            public DateTime LastWriteUtc;
            public IReadOnlyList<string> Names = Array.Empty<string>();
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> _cache =
            new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>All entry paths inside <paramref name="apPath"/>, or an empty list if it cannot be read.</summary>
        public static IReadOnlyList<string> GetEntryNames(string apPath)
        {
            try
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(apPath);
                if (_cache.TryGetValue(apPath, out CacheEntry? existing) && existing.LastWriteUtc == lastWrite)
                {
                    return existing.Names;
                }

                var names = new List<string>();
                using (var archive = ZipHelper.OpenRead(apPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        names.Add(entry.FullName);
                    }
                }

                _cache[apPath] = new CacheEntry { LastWriteUtc = lastWrite, Names = names };
                return names;
            }
            catch (Exception e)
            {
                Log.Debug("Could not index .ap archive {Ap}: {Message}", apPath, e.Message);
                return Array.Empty<string>();
            }
        }

        public static void Clear() => _cache.Clear();
    }
}
