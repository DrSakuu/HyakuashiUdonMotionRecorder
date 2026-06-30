using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HUMR
{
    [Serializable]
    public class LogEntry
    {
        public RecordingType type;
        public string name;
    }
    
    
    public class RecordLogLoader : MonoBehaviour
    {
        public string logFileDirectory;
        public string[] logFilePaths;
        public string[] recordFilePaths;
        public string[] recordFileNames;
        public int logFileIndex;
        
        private static readonly Regex FilenameRegex = new Regex(@"^output_log_|\.txt$");
        public List<LogEntry> uniqueRecords = new List<LogEntry>();
        public int recordIndex;
        
        public void CollectLogFiles()
        {
            if (!Directory.Exists(logFileDirectory)) return;
            
            logFilePaths = Directory.GetFiles(logFileDirectory, "*.txt");
            recordFilePaths = logFilePaths
                .Where(file => File.ReadLines(file).Any(line => line.Contains("-  [HUMR] ")))
                .OrderBy(file => File.GetLastWriteTime(file))
                .Reverse()
                .ToArray();
            recordFileNames = recordFilePaths
                .Select(p => FilenameRegex.Replace(Path.GetFileName(p), ""))
                .ToArray();

            var foundEntries = new HashSet<string>();
            uniqueRecords.Clear();
            foreach (var recordFile in recordFilePaths)
            {
                foreach (var line in File.ReadLines(recordFile))
                {
                    if (!line.Contains("-  [HUMR] START RECORDING")) continue;
                    
                    var content = line.Split(new[] { "-  [HUMR] START RECORDING" }, StringSplitOptions.None)[1];
                    if (!foundEntries.Add(content)) continue;
                    
                    var parts = content.Split(';');
                    if (parts.Length < 3) continue;
                    
                    var typeStr = parts[1];
                    if (!Enum.TryParse<RecordingType>(typeStr, true, out var type))
                    {
                        type = RecordingType.Object;
                    }
                    uniqueRecords.Add(new LogEntry { type = type, name = parts[2] });
                }
            }
        }
    }
}
