using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HUMR
{public class LogEntry
    {
        public RecordingType Type;
        public string Name;
    }
    
    
    public class RecordLogLoader : MonoBehaviour
    {
        public string path;
        public string[] logFilePaths;
        public string[] logFileNames;
        public int logFileIndex;
        
        private static readonly Regex FilenameRegex = new Regex(@"^output_log_|\.txt$");
        public List<LogEntry> UniqueRecords = new List<LogEntry>();
        public int recordIndex;
        
        public void CollectLogFiles()
        {
            if (!Directory.Exists(path)) return;
            var foundEntries = new HashSet<string>();
            UniqueRecords.Clear();

            logFilePaths = Directory.GetFiles(path, "*.txt")
                .OrderByDescending(File.GetLastWriteTime)
                .ToArray();

            foreach (var file in logFilePaths)
            {
                foreach (var line in File.ReadLines(file))
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
                    UniqueRecords.Add(new LogEntry { Type = type, Name = parts[2] });
                }
            }

            logFileNames = logFilePaths
                .Select(p => FilenameRegex.Replace(Path.GetFileName(p), ""))
                .ToArray();
            logFilePaths = Directory.GetFiles(path, "*.txt")
                .Where(file => File.ReadLines(file).Any(line => line.Contains("-  [HUMR] ")))
                .OrderBy(file => File.GetLastWriteTime(file))
                .Reverse()
                .ToArray();

            logFileNames = logFilePaths
                .Select(p => FilenameRegex.Replace(Path.GetFileName(p), ""))
                .ToArray();
        }
    }
}
