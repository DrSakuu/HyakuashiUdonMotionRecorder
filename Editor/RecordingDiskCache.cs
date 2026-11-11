using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DrSakuu.Humr.Editor
{
    internal static class RecordingDiskCache
    {
        private const int FormatVersion = 1;

        [Serializable]
        private sealed class Entry
        {
            public int version;
            public string sourcePath;
            public long lastWriteTimeUtcTicks;
            public long fileSize = -1;
            public bool containsHumrData;
        }

        private static string CacheDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HUMR", "RecordingCache"));

        private static string NormalizePath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return Path.DirectorySeparatorChar == '\\' ? fullPath.ToUpperInvariant() : fullPath;
        }

        private static string CachePath(string normalizedPath)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath));
            return Path.Combine(CacheDirectory, BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() + ".json");
        }

        internal static bool TryLoad(string path, DateTime lastWriteTimeUtc, long fileSize, out bool containsHumrData)
        {
            containsHumrData = false;
            try
            {
                var normalizedPath = NormalizePath(path);
                var json = File.ReadAllText(CachePath(normalizedPath));
                var entry = new Entry();
                JsonUtility.FromJsonOverwrite(json, entry);
                if (entry.version != FormatVersion || entry.sourcePath != normalizedPath ||
                    entry.lastWriteTimeUtcTicks != lastWriteTimeUtc.Ticks || entry.fileSize != fileSize)
                    return false;

                // JsonUtility defaults missing fields. Require an explicit detection result,
                // so incomplete JSON cannot silently classify a recording as containing no data.
                var presenceCheck = new Entry { containsHumrData = !entry.containsHumrData };
                JsonUtility.FromJsonOverwrite(json, presenceCheck);
                if (presenceCheck.containsHumrData != entry.containsHumrData) return false;

                containsHumrData = entry.containsHumrData;
                return true;
            }
            catch (Exception exception) when (IsCacheFailure(exception))
            {
                return false;
            }
        }

        internal static void Save(string path, DateTime lastWriteTimeUtc, long fileSize, bool containsHumrData)
        {
            string temporaryPath = null;
            try
            {
                var source = new FileInfo(path);
                if (!source.Exists || source.LastWriteTimeUtc != lastWriteTimeUtc || source.Length != fileSize) return;

                var normalizedPath = NormalizePath(path);
                var destination = CachePath(normalizedPath);
                var entry = new Entry
                {
                    version = FormatVersion,
                    sourcePath = normalizedPath,
                    lastWriteTimeUtcTicks = lastWriteTimeUtc.Ticks,
                    fileSize = fileSize,
                    containsHumrData = containsHumrData
                };
                Directory.CreateDirectory(CacheDirectory);
                temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(entry, true));

                source.Refresh();
                if (!source.Exists || source.LastWriteTimeUtc != lastWriteTimeUtc || source.Length != fileSize) return;
                if (File.Exists(destination))
                    File.Replace(temporaryPath, destination, null);
                else
                    File.Move(temporaryPath, destination);
            }
            catch (Exception exception) when (IsCacheFailure(exception))
            {
                // Disk caching is optional; the parsed result remains usable in memory.
            }
            finally
            {
                if (temporaryPath != null) TryDelete(temporaryPath);
            }
        }

        internal static void Clear()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory)) return;
                foreach (var path in Directory.GetFiles(CacheDirectory, "*.json"))
                    TryDelete(path);
            }
            catch (Exception exception) when (IsCacheFailure(exception))
            {
                // An unavailable cache must not prevent refreshing the source logs.
            }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception exception) when (IsCacheFailure(exception)) { }
        }

        private static bool IsCacheFailure(Exception exception) =>
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;
    }
}
