#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Remembers the result of parsing a rolling-stock <c>.bin</c> during a "Look up vehicles"
    /// scan so the edit window does not have to re-run <c>serz.exe</c> for every loose
    /// <c>.bin</c> and re-parse every railvehicle entry inside every <c>.ap</c> each time.
    /// An entry is reused only while the stamp (last-write time + size) of its source file -
    /// the <c>.bin</c> itself, or the containing <c>.ap</c> - is unchanged, so the cache
    /// heals itself whenever stock is installed, updated or removed. Negative results
    /// (a <c>.bin</c> that is not rolling stock, which is most entries in an <c>.ap</c>) are
    /// cached too. Persisted to %AppData%\LocoSwap\vehicle-cache.json.
    /// </summary>
    internal static class AvailableVehicleCache
    {
        internal sealed class Entry
        {
            public long SourceTicks { get; set; }
            public long SourceSize { get; set; }

            /// <summary>When true the source is not an engine/wagon/tender blueprint; no other field is meaningful.</summary>
            public bool NotAVehicle { get; set; }

            public string Provider { get; set; } = "";
            public string Product { get; set; } = "";
            public string BlueprintId { get; set; } = "";
            public string Name { get; set; } = "";
            public float Length { get; set; }
            public string Type { get; set; } = nameof(VehicleType.Unknown);
            public int EntityCount { get; set; }
            public string[] CargoCapacities { get; set; } = Array.Empty<string>();
            public string[] CargoAltEncodings { get; set; } = Array.Empty<string>();
            public string[] NumberingList { get; set; } = Array.Empty<string>();
            public string NameLocalisedStringXml { get; set; } = "";
        }

        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocoSwap", "vehicle-cache.json");

        private static readonly ConcurrentDictionary<string, Entry> _cache = Load();
        private static int _dirty;

        public static Entry? TryGet(string binPath, long ticks, long size)
        {
            if (ticks == 0) return null;
            if (!_cache.TryGetValue(binPath, out Entry? entry)) return null;
            if (entry.SourceTicks != ticks || entry.SourceSize != size) return null;
            return entry;
        }

        public static void Store(string binPath, long ticks, long size, Entry entry)
        {
            if (ticks == 0) return;
            entry.SourceTicks = ticks;
            entry.SourceSize = size;
            _cache[binPath] = entry;
            _dirty = 1;
        }

        public static void StoreNegative(string binPath, long ticks, long size)
        {
            if (ticks == 0) return;
            _cache[binPath] = new Entry { SourceTicks = ticks, SourceSize = size, NotAVehicle = true };
            _dirty = 1;
        }

        /// <summary>Write pending changes to disk. Cheap no-op when nothing changed.</summary>
        public static void Flush()
        {
            if (System.Threading.Interlocked.Exchange(ref _dirty, 0) == 0) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                File.WriteAllText(CachePath, JsonSerializer.Serialize(
                    new Dictionary<string, Entry>(_cache), VehicleCacheJsonContext.Default.DictionaryStringEntry));
            }
            catch (Exception ex)
            {
                Log.Debug("Could not write vehicle cache: {Message}", ex.Message);
            }
        }

        private static ConcurrentDictionary<string, Entry> Load()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var loaded = JsonSerializer.Deserialize(
                        File.ReadAllText(CachePath), VehicleCacheJsonContext.Default.DictionaryStringEntry);
                    if (loaded != null) return new ConcurrentDictionary<string, Entry>(loaded);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Could not read vehicle cache: {Message}", ex.Message);
            }
            return new ConcurrentDictionary<string, Entry>();
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Dictionary<string, AvailableVehicleCache.Entry>))]
    internal partial class VehicleCacheJsonContext : JsonSerializerContext
    {
    }
}
