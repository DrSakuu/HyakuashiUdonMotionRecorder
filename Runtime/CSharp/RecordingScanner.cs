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
        public string foundTakesStr;
        public List<RecordingTake> recordingTakes = new List<RecordingTake>();
    }

    public static class RecordingScanner
    {
        public static LogType DetermineRecordingType(string filePath)
        {
            var isHumr = false;
            var isLegacy = false;

            using (var reader = LogDataParser.OpenReadOnlyTextFile(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(HumrRecordingLoader.LogMatchTarget)) isHumr = true;
                    if (line.Contains(HumrRecordingLoader.LegacyLogMatchTarget)) isLegacy = true;
                    if (isHumr || isLegacy) break;
                }
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
                case LogType.Humr:
                case LogType.Legacy:
                case LogType.Corrupt:
                default:
                    return type.ToString();
            }
        }
    }
}