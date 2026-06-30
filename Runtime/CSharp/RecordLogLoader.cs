using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HUMR
{
    public class RecordLogLoader : MonoBehaviour
    {
        public string path;
        public string[] logFilePaths;
        public string[] logFileNames;
        public int logFileIndex;
        private static readonly Regex FilenameRegex = new Regex(@"^output_log_|\.txt$");
        
        public void CollectLogFiles()
        {
            if (!Directory.Exists(path)) return;
    
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
