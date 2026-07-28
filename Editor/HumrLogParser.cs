using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Humr.Editor
{
    public enum LogType
    {
        Humr,
        Corrupt,
        NoData
    }

    [Serializable]
    public class RecordingFile
    {
        public string path;
        public LogType type;
        public string fileName;
        public (TargetType targetType, string name)[] Targets;
        public string foundTakesStr;
        public List<RecordingTake> recordingTakes = new List<RecordingTake>();
        public DateTime LastWriteTime;
    }

    [Serializable]
    public class RecordingTake
    {
        public TargetType targetType;
        public string targetName;
        public long takeTimestamp;

        public RecordingTake()
        {
            Frames = new List<RecordingFrame>();
        }

        public List<RecordingFrame> Frames { get; set; }
    }

    public class RecordingFrame
    {
        public float RecordTime { get; set; }
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new List<Quaternion>();
    }

    public static class HumrLogParser
    {
        private const int MinimumComponentCount = 4;

        private const string LogMatchTarget = "-  [HUMR] RECORDING";
        private const string LegacyLogMatchTarget = "-  HUMR:";
        private static readonly (TargetType, string) CorruptTargetTuple = (TargetType.Unknown, "HUMR data is corrupt");

        public static List<string> LoadLogFileLines(string path)
        {
            using (var reader = OpenReadOnlyTextFile(path))
            {
                return ReadAllLines(reader);
            }
        }

        private static List<string> ReadAllLines(StreamReader reader)
        {
            var lines = new List<string>();
            string line;
            while ((line = reader.ReadLine()) != null) lines.Add(line);
            return lines;
        }

        private static StreamReader OpenReadOnlyTextFile(string filePath)
        {
            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            return new StreamReader(fileStream);
        }

        private static (TargetType, string)[] CollectTargetTypesAndNames(RecordingFile recordingFile)
        {
            if (!File.Exists(recordingFile.path)) return new []{ CorruptTargetTuple };

            var foundTargets = new HashSet<(TargetType, string)>();

            using (var reader = OpenReadOnlyTextFile(recordingFile.path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var (targetType, targetName) = ExtractHumrOrLegacyTarget(line);
                    if (targetType == TargetType.Unknown) continue;
                    
                    foundTargets.Add((targetType, targetName));
                }

                if (foundTargets.Count > 0) return foundTargets.ToArray();
                
                recordingFile.type = LogType.Corrupt;
                return new []{ CorruptTargetTuple };
            }
        }

        private static (TargetType, string) ExtractHumrOrLegacyTarget(string line)
        {
            if (line.Contains(LogMatchTarget)) return ExtractTarget(line);
            return line.Contains(LegacyLogMatchTarget) ? ExtractLegacyTarget(line) : CorruptTargetTuple;
        }

        private static (TargetType, string) ExtractTarget(string line)
        {
            if (!line.Contains(LogMatchTarget)) return CorruptTargetTuple;

            var recordingFrame = line.Substring(line.IndexOf(LogMatchTarget, StringComparison.Ordinal) + LogMatchTarget.Length + 1);
            var typeVariableStr = SplitNextVariable(recordingFrame, out var remaining);
            if (!Enum.TryParse<TargetType>(typeVariableStr, out var targetType)) return CorruptTargetTuple;
            
            var targetName = SplitNextVariable(remaining, out _);
            return (targetType, targetName);
        }

        private static string SplitNextVariable(string line, out string remaining)
        {
            remaining = line;
            var delimiterIndex = line.IndexOf(HumrLogger.VariableDelimiter, StringComparison.Ordinal);
            if (delimiterIndex == -1) return null;
            
            remaining = line.Substring(delimiterIndex + 1);
            return line.Substring(0, delimiterIndex);
        }

        private static (TargetType, string) ExtractLegacyTarget(string line)
        {
            var prefixIdx = line.IndexOf(LegacyLogMatchTarget, StringComparison.Ordinal);
            if (prefixIdx == -1) return CorruptTargetTuple;

            var dataSegment = line.Substring(prefixIdx + LegacyLogMatchTarget.Length).Trim();

            var digitIdx = FindFirstDigitIndex(dataSegment);
            return digitIdx == -1 ? CorruptTargetTuple : 
                (TargetType.Legacy, dataSegment.Substring(0, digitIdx));
        }

        private static int FindFirstDigitIndex(string text)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsDigit(text[i]))
                    return i;

            return -1;
        }

        public static List<RecordingTake> PartitionLogLinesIntoTakes(
            string[] lines, (TargetType targetType, string targetName) target)
        {
            var takes = new List<RecordingTake>();
            var currentTake = new RecordingTake { targetType = target.targetType, targetName = target.targetName };
            var targetMatchStr = string.Join(
                HumrLogger.VariableDelimiter, LogMatchTarget, target.targetType, target.targetName, "");
            var beforeTime = -1f;

            foreach (var line in lines)
            {
                if (!line.Contains(targetMatchStr)) continue;

                var takeStr = line.Split(targetMatchStr)[1];
                if (!TryParseTakeLine(takeStr, out var takeSplit, out var currentTime)) continue;

                var lineTimestamp = long.Parse(takeSplit[0]);
                if (currentTake.takeTimestamp == 0 && currentTake.Frames.Count == 0)
                {
                    currentTake.takeTimestamp = lineTimestamp;
                }
                else if (ShouldStartNewTake(lineTimestamp, currentTime, beforeTime, currentTake))
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake { targetType = target.targetType, targetName = target.targetName, takeTimestamp = lineTimestamp};
                    beforeTime = -1;
                }

                var frame = ParseMotionFrame(takeSplit);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                beforeTime = currentTime;
            }

            if (currentTake.Frames.Count > 0) takes.Add(currentTake);

            return takes;
        }

        private static bool TryParseTakeLine(string takeStr, out string[] takeSplit, out float currentTime)
        {
            takeSplit = null;
            currentTime = -1f;

            var split = takeStr.Split(HumrLogger.VariableDelimiter);
            if (split.Length < MinimumComponentCount) return false;

            if (!float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                return false;

            takeSplit = split;
            currentTime = time;
            return true;
        }

        // TODO: reduce number of parameters
        private static bool ShouldStartNewTake(
            long newTimestamp, float currentTime, float beforeTime, RecordingTake currentTake)
        {
            var isNewTimestamp = newTimestamp != currentTake.takeTimestamp;
            var isRewind = currentTime < beforeTime;

            return (isNewTimestamp || isRewind) && currentTake.Frames.Count > 0;
        }

        private static RecordingFrame ParseMotionFrame(string[] parts)
        {
            var frame = new RecordingFrame
            {
                RecordTime = float.Parse(parts[1], CultureInfo.InvariantCulture),
                HipPosition = ParseHipPosition(parts[2])
            };

            AppendBoneRotations(parts, frame);

            return frame;
        }

        private static Vector3 ParseHipPosition(string rawPosition)
        {
            var posValues = rawPosition.Split(HumrLogger.ComponentDelimiter);
            if (posValues.Length != 3) return default;

            return new Vector3(
                float.Parse(posValues[0], CultureInfo.InvariantCulture),
                float.Parse(posValues[1], CultureInfo.InvariantCulture),
                float.Parse(posValues[2], CultureInfo.InvariantCulture)
            );
        }

        private static void AppendBoneRotations(string[] parts, RecordingFrame frame)
        {
            for (var i = 3; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;

                var rotValues = parts[i].Split(HumrLogger.ComponentDelimiter);
                if (rotValues.Length != 4) continue;

                frame.BoneRotations.Add(new Quaternion(
                    float.Parse(rotValues[0], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[1], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[2], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[3], CultureInfo.InvariantCulture)
                ));
            }
        }

        public static List<RecordingTake> ParseLegacyTakes(List<string> logLines, string targetName)
        {
            var take = new List<RecordingTake>();
            var currentFrames = new List<RecordingFrame>();
            var lastTime = -1f;

            foreach (var line in logLines)
            {
                if (!TryParseLegacyFrame(line, LegacyLogMatchTarget, targetName,
                        out var frame)) continue;

                HandleTakeBreak(frame, currentFrames, take, ref lastTime);

                currentFrames.Add(frame);
                lastTime = frame.RecordTime;
            }

            if (currentFrames.Count > 0) take.Add(new RecordingTake { Frames = currentFrames });

            return take;
        }

        private static bool TryParseLegacyFrame(string line, string matchTarget, string targetName,
            out RecordingFrame frame)
        {
            frame = null;

            // TODO: do ExtractLegacyDataSegment before TryParseLegacyFrame
            var dataSegment = ExtractLegacyDataSegment(line, matchTarget, targetName);
            if (dataSegment == null) return false;

            var tokens = dataSegment.Split(HumrLogger.ComponentDelimiter);
            if (tokens.Length < MinimumComponentCount) return false;

            try
            {
                frame = BuildLegacyFrame(tokens);
                return true;
            }
            catch (Exception ex)
            {
                HumrLogger.Error($"Failed to interpret legacy sequential data array line: {ex.Message}");
                return false;
            }
        }

        private static string ExtractLegacyDataSegment(string line, string matchTarget, string targetName)
        {
            var prefixIdx = line.IndexOf(matchTarget, StringComparison.Ordinal);
            if (prefixIdx == -1) return null;

            var dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();
            if (!dataSegment.StartsWith(targetName)) return null;

            return dataSegment.Substring(targetName.Length);
        }

        private static RecordingFrame BuildLegacyFrame(string[] tokens)
        {
            var frame = new RecordingFrame
            {
                RecordTime = float.Parse(tokens[0], CultureInfo.InvariantCulture),
                HipPosition = new Vector3(
                    float.Parse(tokens[1], CultureInfo.InvariantCulture),
                    float.Parse(tokens[2], CultureInfo.InvariantCulture),
                    float.Parse(tokens[3], CultureInfo.InvariantCulture)
                ),
                BoneRotations = new List<Quaternion>()
            };

            ParseBoneRotations(tokens, frame);
            return frame;
        }

        private static void ParseBoneRotations(string[] tokens, RecordingFrame frame)
        {
            for (var i = 4; i + 3 < tokens.Length; i += 4)
                frame.BoneRotations.Add(new Quaternion(
                    float.Parse(tokens[i], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 2], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 3], CultureInfo.InvariantCulture)
                ));
        }

        private static void HandleTakeBreak(RecordingFrame frame, List<RecordingFrame> currentFrames,
            List<RecordingTake> takes, ref float lastTime)
        {
            if (lastTime < 0) return;

            var isRewind = frame.RecordTime < lastTime;
            var isGap = frame.RecordTime - lastTime > 1.0f;

            if (!isRewind && !isGap) return;
            if (currentFrames.Count <= 0) return;

            takes.Add(new RecordingTake { Frames = new List<RecordingFrame>(currentFrames) });
            currentFrames.Clear();
        }

        private static bool DetectLogMarkers(string filePath)
        {
            using (var reader = OpenReadOnlyTextFile(filePath))
            {
                var isHumr = false;
                var isLegacy = false;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(LogMatchTarget)) isHumr = true;
                    if (line.Contains(LegacyLogMatchTarget)) isLegacy = true;
                    if (isHumr || isLegacy) return true;
                }

                return false;
            }
        }

        private static string BuildRecordingFileName(string filePath, LogType type)
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
                case LogType.Humr:
                    return "HUMR";
                case LogType.Corrupt:
                    return "HUMR (Corrupted)";
                case LogType.NoData:
                    return "----";
                default:
                    return type.ToString();
            }
        }

        public static List<RecordingFile> CollectRecordingFiles(string[] filePaths)
        {
            var discoveredFiles = new List<RecordingFile>();

            foreach (var filePath in filePaths)
            {
                var fileType = DetectLogMarkers(filePath) ? LogType.Humr : LogType.NoData;;
                var writeTime = File.GetLastWriteTime(filePath);
                var fileName = BuildRecordingFileName(filePath, fileType);
                discoveredFiles.Add(new RecordingFile
                {
                    path = filePath, type = fileType, LastWriteTime = writeTime, fileName = fileName
                });
            }

            return discoveredFiles
                .OrderByDescending(entry => entry.LastWriteTime)
                .ToList();
        }

        public static (TargetType, string)[] ResolveTargets(RecordingFile file)
        {
            switch (file.type)
            {
                case LogType.Humr:
                    return CollectTargetTypesAndNames(file);
                case LogType.Corrupt:
                    return new []{ CorruptTargetTuple };
                case LogType.NoData:
                default:
                    return new[] { (TargetType.Unknown, "No HUMR data") };
            }
        }
    }
}