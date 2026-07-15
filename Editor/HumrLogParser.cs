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
        Legacy,
        Corrupt,
        NoData
    }

    [Serializable]
    public class RecordingFile
    {
        public string path;
        public LogType type;
        public string fileName;
        public string[] targetNames;
        public string foundTakesStr;
        public List<RecordingTake> recordingTakes = new List<RecordingTake>();
        public DateTime LastWriteTime;
    }

    [Serializable]
    public class RecordingTake
    {
        public string targetName;

        public RecordingTake()
        {
            Frames = new List<RecordingFrame>();
        }

        public List<RecordingFrame> Frames { get; set; }
    }

    public class RecordingFrame
    {
        public FrameType FrameType;
        public float RecordTime { get; set; }
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new List<Quaternion>();
    }

    public static class HumrLogParser
    {
        private const int MinimumComponentCount = 4;

        private const string LogMatchTarget = "-  [HUMR] RECORDING;";
        private const string LegacyLogMatchTarget = "-  HUMR:";

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

        private static string[] CollectTargetNames(RecordingFile recordingFile)
        {
            if (!File.Exists(recordingFile.path)) return null;

            var foundTargets = new HashSet<string>();

            using (var reader = OpenReadOnlyTextFile(recordingFile.path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var targetName = ExtractTargetNameForType(line, recordingFile.type);
                    if (targetName == null) continue;
                    foundTargets.Add(targetName);
                }

                return foundTargets.ToArray();
            }
        }

        private static string ExtractTargetNameForType(string line, LogType type)
        {
            switch (type)
            {
                case LogType.Humr:
                    return ExtractTargetName(line, LogMatchTarget);
                case LogType.Legacy:
                    return ExtractLegacyTargetName(line, LegacyLogMatchTarget);
                case LogType.Corrupt:
                case LogType.NoData:
                default:
                    return null;
            }
        }

        private static string ExtractTargetName(string line, string matchTarget)
        {
            var lineSplit = line.Split(new[] { matchTarget }, StringSplitOptions.None);
            if (lineSplit.Length < 2) return null;

            var recordingFrame = lineSplit[1];
            var delimiterIndex = recordingFrame.IndexOf(HumrLogger.VariableDelimiter, StringComparison.Ordinal);
            if (delimiterIndex == -1) return null;

            return recordingFrame.Substring(0, delimiterIndex);
        }

        private static string ExtractLegacyTargetName(string line, string matchTarget)
        {
            var prefixIdx = line.IndexOf(matchTarget, StringComparison.Ordinal);
            if (prefixIdx == -1) return null;

            var dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();

            var digitIdx = FindFirstDigitIndex(dataSegment);
            return digitIdx == -1 ? null : dataSegment.Substring(0, digitIdx);
        }

        private static int FindFirstDigitIndex(string text)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsDigit(text[i]))
                    return i;

            return -1;
        }

        public static List<RecordingTake> PartitionLogLinesIntoTakes(string[] lines, string targetName)
        {
            var takes = new List<RecordingTake>();
            var currentTake = new RecordingTake { targetName = targetName };
            var targetMatchStr = LogMatchTarget + targetName + HumrLogger.VariableDelimiter;
            var foundTakes = 0;
            var beforeTime = -1f;

            foreach (var line in lines)
            {
                if (!line.Contains(targetMatchStr)) continue;

                if (!TryParseTakeLine(
                        line, targetMatchStr, out var takeSplit, out var currentTime)) continue;

                if (ShouldStartNewTake(takeSplit, foundTakes, currentTime, beforeTime, currentTake))
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake();
                    foundTakes++;
                }

                var frame = ParseMotionFrame(takeSplit);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                beforeTime = currentTime;
            }

            if (currentTake.Frames.Count > 0) takes.Add(currentTake);

            return takes;
        }

        private static bool TryParseTakeLine(string line, string targetMatchStr, out string[] takeSplit,
            out float currentTime)
        {
            takeSplit = null;
            currentTime = -1f;

            var takeStr = line.Split(targetMatchStr)[1];
            var split = takeStr.Split(HumrLogger.VariableDelimiter);
            if (split.Length < MinimumComponentCount) return false;

            if (!float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                return false;

            takeSplit = split;
            currentTime = time;
            return true;
        }

        private static bool ShouldStartNewTake(string[] takeSplit, int foundTakes, float currentTime,
            float beforeTime, RecordingTake currentTake)
        {
            var takeIndex = int.Parse(takeSplit[0]);
            var isNewTakeIndex = takeIndex > foundTakes;
            var isRewind = currentTime < beforeTime;

            return (isNewTakeIndex || isRewind) && currentTake.Frames.Count > 0;
        }

        private static RecordingFrame ParseMotionFrame(string[] parts)
        {
            var frame = new RecordingFrame
            {
                RecordTime = float.Parse(parts[2], CultureInfo.InvariantCulture),
                HipPosition = ParseHipPosition(parts[3])
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
            for (var i = 4; i < parts.Length; i++)
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
                Debug.LogError($"Failed to interpret legacy sequential data array line: {ex.Message}");
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

        private static LogType DetermineRecordingType(string filePath)
        {
            var (isHumr, isLegacy) = DetectLogMarkers(filePath);

            if (isHumr) return LogType.Humr;
            return isLegacy ? LogType.Legacy : LogType.NoData;
        }

        private static (bool isHumr, bool isLegacy) DetectLogMarkers(string filePath)
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
                    if (isHumr || isLegacy) break;
                }

                return (isHumr, isLegacy);
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
                case LogType.Legacy:
                    return "HUMR (Legacy)";
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
                var fileType = DetermineRecordingType(filePath);
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

        public static string[] ResolveTargetNames(RecordingFile file)
        {
            switch (file.type)
            {
                case LogType.Humr:
                case LogType.Legacy:
                    return CollectTargetNames(file);
                case LogType.Corrupt:
                    return new[] { "HUMR data is corrupted" };
                case LogType.NoData:
                default:
                    return new[] { "No HUMR data" };
            }
        }
    }
}