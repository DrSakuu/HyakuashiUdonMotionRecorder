using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HUMR
{
    public static class LogFileScanner
    {
        public static List<RecordFileEntry> BuildRecordFileEntries(string[] filePaths)
        {
            var discoveredEntries = new List<RecordFileEntry>();

            foreach (var file in filePaths)
            {
                var fileType = DetermineLogFileType(file);
                if (fileType == LogFileType.Unknown) continue;

                discoveredEntries.Add(new RecordFileEntry { path = file, type = fileType });
            }

            return discoveredEntries
                .OrderByDescending(e => File.GetLastWriteTime(e.path))
                .ToList();
        }

        private static LogFileType DetermineLogFileType(string filePath)
        {
            var isStandard = false;
            var isLegacy = false;

            foreach (var line in File.ReadLines(filePath))
            {
                if (line.Contains(PlayerRecordingLoader.LogMatchTarget)) isStandard = true;
                if (line.Contains(PlayerRecordingLoader.LegacyLogMatchTarget)) isLegacy = true;
                if (isStandard || isLegacy) break;
            }

            if (isStandard) return LogFileType.Standard;
            return isLegacy ? LogFileType.Legacy : LogFileType.Unknown;
        }

        public static string[] BuildDisplayNames(List<RecordFileEntry> entries)
        {
            var logFileRegex = new Regex(@"^output_log_|\.txt$");
            return entries
                .Select(e => logFileRegex.Replace(Path.GetFileName(e.path), ""))
                .ToArray();
        }
    }
}