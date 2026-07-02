using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HUMR
{
    [Serializable]
    public class RecordingFile
    {
        public string path;
        public RecordingType type;
    }

    public static class RecordingScanner
    {
        public static List<RecordingFile> BuildRecordingFiles(string[] filePaths)
        {
            var discoveredEntries = new List<RecordingFile>();

            foreach (var file in filePaths)
            {
                var fileType = DetermineRecordingType(file);
                if (fileType == RecordingType.Unknown) continue;

                discoveredEntries.Add(new RecordingFile { path = file, type = fileType });
            }

            return discoveredEntries
                .OrderByDescending(e => File.GetLastWriteTime(e.path))
                .ToList();
        }

        private static RecordingType DetermineRecordingType(string filePath)
        {
            var isBoneRotations = false;
            var isLegacy = false;

            foreach (var line in File.ReadLines(filePath))
            {
                if (line.Contains(HumrRecordingLoader.LogMatchTarget)) isBoneRotations = true;
                if (line.Contains(HumrRecordingLoader.LegacyLogMatchTarget)) isLegacy = true;
                if (isBoneRotations || isLegacy) break;
            }

            if (isBoneRotations) return RecordingType.BoneRotations;
            return isLegacy ? RecordingType.Legacy : RecordingType.Unknown;
        }

        public static string[] BuildRecordingFileNames(List<RecordingFile> entries)
        {
            var logFileRegex = new Regex(@"^output_log_|\.txt$");
            return entries
                .Select(e => logFileRegex.Replace(Path.GetFileName(e.path), ""))
                .ToArray();
        }
    }
}