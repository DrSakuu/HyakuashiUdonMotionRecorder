using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VRC.Dynamics;

namespace HUMR
{
    public enum LogType
    {
        Humr,
        Legacy,
        Corrupt,
        NoData
    }
    
    [Serializable]
    public class RecordingFile
    {
        public string path;
        public LogType type;
        public DateTime LastWriteTime;
        public string fileName;
        public string[] targetNames;
        public List<RecordingTake> recordingTakes = new List<RecordingTake>();
    }

    public static class RecordingScanner
    {
        public static List<RecordingFile> BuildRecordingFiles(string[] filePaths)
        {
            var discoveredEntries = new List<RecordingFile>();

            foreach (var filePath in filePaths)
            {
                var fileType = DetermineRecordingType(filePath);
                var writeTime = File.GetLastWriteTime(filePath);
                var fileName = BuildRecordingFileName(filePath, fileType);
                discoveredEntries.Add(new RecordingFile
                {
                    path = filePath, type = fileType , LastWriteTime = writeTime, fileName =  fileName
                });
            }

            return discoveredEntries
                .OrderByDescending(entry => entry.LastWriteTime)
                .ToList();
        }

        private static LogType DetermineRecordingType(string filePath)
        {
            var isHumr = false;
            var isLegacy = false;

            foreach (var line in File.ReadLines(filePath))
            {
                if (line.Contains(HumrRecordingLoader.LogMatchTarget)) isHumr = true;
                if (line.Contains(HumrRecordingLoader.LegacyLogMatchTarget)) isLegacy = true;
                if (isHumr || isLegacy) break;
            }

            if (isHumr) return LogType.Humr;
            return isLegacy ? LogType.Legacy : LogType.NoData;
        }

        public static string BuildRecordingFileName(string filePath, LogType type)
        {
            var logFileRegex = new Regex(@"^output_log_|\.txt$");
            var rawFileName = Path.GetFileName(filePath);
            var cleanedFileName = logFileRegex.Replace(rawFileName, "");
            var typeName = LogTypeToDisplayString(type);
            return $"{cleanedFileName} {typeName}";
        }
        
        private static string LogTypeToDisplayString(LogType type)
        {
            switch (type)
            {
                case LogType.NoData:
                    return "(No Humr Data)";
                
                default:
                    return type.ToString();
            }
        }
    }
}