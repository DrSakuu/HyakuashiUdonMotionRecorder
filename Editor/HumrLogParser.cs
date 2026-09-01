using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DrSakuu.Humr.Editor
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
        public string foundTakesStr;
        public List<RecordingTake> takes = new();
        public DateTime LastWriteTime;
        public (TargetType targetType, string name)[] Targets;
    }

    [Serializable]
    public class RecordingTake
    {
        public TargetType targetType;
        public string targetName;
        public long takeTimestamp;

        public List<Frame> Frames { get; set; } = new();
    }

    [Serializable]
    public abstract class Frame
    {
        public float RecordTime { get; set; }
    }

    [Serializable]
    public class BoneRotationsFrame : Frame
    {
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new();
    }

    [Serializable]
    public class ObjectFrame : Frame
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 LocalScale { get; set; }
    }

    public static class HumrLogParser
    {
        private const int MinimumComponentCount = 4;
        private const string LogMatchTarget = "-  [HUMR] RECORDING";
        private const string LegacyLogMatchTarget = "-  HUMR:";
        private static readonly (TargetType, string) CorruptTargetTuple = (TargetType.Unknown, "HUMR data is corrupt");

        public static string[] LoadHumrLogLines(string path)
        {
            var lines = new List<string>();
            using var reader = OpenReadOnlyTextFile(path);
            while (reader.ReadLine() is { } line)
            {
                if (line.IndexOf(LogMatchTarget, StringComparison.Ordinal) >=0 
                    || line.IndexOf(LegacyLogMatchTarget, StringComparison.Ordinal) >=0) 
                    lines.Add(line);
            }
            return lines.ToArray();
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
            if (!File.Exists(recordingFile.path)) return new[] { CorruptTargetTuple };

            var foundTargets = new HashSet<(TargetType, string)>();

            using var reader = OpenReadOnlyTextFile(recordingFile.path);
            while (reader.ReadLine() is { } line)
            {
                var (targetType, targetName) = ExtractHumrOrLegacyTarget(line);
                if (targetType == TargetType.Unknown) continue;

                foundTargets.Add((targetType, targetName));
            }

            if (foundTargets.Count > 0) return foundTargets.ToArray();

            recordingFile.type = LogType.Corrupt;
            return new[] { CorruptTargetTuple };
        }

        private static (TargetType, string) ExtractHumrOrLegacyTarget(string line)
        {
            if (line.IndexOf(LogMatchTarget, StringComparison.Ordinal) >= 0) return ExtractTarget(line);
            return line.IndexOf(LegacyLogMatchTarget, StringComparison.Ordinal) >= 0 
                ? ExtractLegacyTarget(line) : CorruptTargetTuple;
        }

        private static (TargetType, string) ExtractTarget(string line)
        {
            if (line.IndexOf(LogMatchTarget, StringComparison.Ordinal) < 0) return CorruptTargetTuple;

            var recordingFrame = line.Substring(
                line.IndexOf(LogMatchTarget, StringComparison.Ordinal) + LogMatchTarget.Length + 1);
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
            return digitIdx == -1 ? CorruptTargetTuple : (TargetType.Legacy, dataSegment.Substring(0, digitIdx));
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
                if (line.IndexOf(targetMatchStr, StringComparison.Ordinal) < 0) continue;

                var takeStr = line.Split(targetMatchStr)[1];
                if (!TryParseTakeLine(takeStr, out var takeSplit, out var currentTime)) continue;

                var lineTimestamp = long.Parse(takeSplit[0]);
                if (currentTake.takeTimestamp == 0 && currentTake.Frames.Count == 0)
                {
                    currentTake.takeTimestamp = lineTimestamp;
                }
                else if (ShouldStartNewTake(currentTake, lineTimestamp, currentTime, beforeTime))
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake
                    {
                        targetType = target.targetType, targetName = target.targetName, takeTimestamp = lineTimestamp
                    };
                    beforeTime = -1;
                }

                var frame = ParseFrame(target.targetType, takeSplit);
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
            RecordingTake currentTake, long newTimestamp, float currentTime, float previousTime)
        {
            if (currentTake.Frames.Count == 0) return false;

            var timestampChanged = newTimestamp != currentTake.takeTimestamp;
            var timeRewound = currentTime < previousTime;
            return timestampChanged || timeRewound;
        }

        private static Frame ParseFrame(TargetType targetType, string[] takeSplit)
        {
            switch (targetType)
            {
                case TargetType.BoneRotations:
                    return ParseBoneRotationsFrame(takeSplit);
                case TargetType.Object:
                    return ParseObjectFrame(takeSplit);
                case TargetType.Unknown:
                case TargetType.Legacy:
                case TargetType.BoneRotationsWithIK:
                case TargetType.HumanMuscles:
                default:
                    return null;
            }
        }

        private static BoneRotationsFrame ParseBoneRotationsFrame(string[] parts)
        {
            var frame = new BoneRotationsFrame
            {
                RecordTime = float.Parse(parts[1], CultureInfo.InvariantCulture),
                HipPosition = ParseVector3(parts[2])
            };

            AppendBoneRotations(parts, frame);

            return frame;
        }

        private static Vector3 ParseVector3(string vector3String)
        {
            var posValues = vector3String.Split(HumrLogger.ComponentDelimiter);
            if (posValues.Length != 3) return default;

            return new Vector3(
                float.Parse(posValues[0], CultureInfo.InvariantCulture),
                float.Parse(posValues[1], CultureInfo.InvariantCulture),
                float.Parse(posValues[2], CultureInfo.InvariantCulture)
            );
        }

        private static Quaternion ParseQuaternion(string quaternionString)
        {
            var quaternionParts = quaternionString.Split(HumrLogger.ComponentDelimiter);
            return ParseQuaternion(quaternionParts);
        }

        private static Quaternion ParseQuaternion(params string[] quaternionParts)
        {
            if (quaternionParts.Length != 4) return default;

            return new Quaternion(
                float.Parse(quaternionParts[0], CultureInfo.InvariantCulture),
                float.Parse(quaternionParts[1], CultureInfo.InvariantCulture),
                float.Parse(quaternionParts[2], CultureInfo.InvariantCulture),
                float.Parse(quaternionParts[3], CultureInfo.InvariantCulture)
            );
        }

        private static void AppendBoneRotations(string[] parts, BoneRotationsFrame frame)
        {
            for (var i = 3; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;

                var rotation = ParseQuaternion(parts[i]);
                frame.BoneRotations.Add(rotation);
            }
        }

        private static ObjectFrame ParseObjectFrame(string[] parts)
        {
            if (parts.Length < 5) return null;
            
            var frame = new ObjectFrame
            {
                RecordTime = float.Parse(parts[1], CultureInfo.InvariantCulture),
                Position = ParseVector3(parts[2]),
                Rotation = ParseQuaternion(parts[3]),
                LocalScale = ParseVector3(parts[4])
            };
            return frame;
        }

        public static List<RecordingTake> ParseLegacyTakes(string[] logLines, string targetName)
        {
            var take = new List<RecordingTake>();
            var currentFrames = new List<Frame>();
            var lastTime = -1f;

            foreach (var line in logLines)
            {
                var dataSegment = ExtractLegacyDataSegment(line, LegacyLogMatchTarget, targetName);
                if (!TryParseLegacyFrame(dataSegment, out var frame)) continue;

                HandleTakeBreak(frame, currentFrames, take, ref lastTime);
                currentFrames.Add(frame);
                lastTime = frame.RecordTime;
            }

            if (currentFrames.Count > 0)
                take.Add(new RecordingTake
                {
                    targetType = TargetType.Legacy, targetName = targetName, Frames = currentFrames
                });

            return take;
        }

        private static bool TryParseLegacyFrame(string dataSegment, out BoneRotationsFrame frame)
        {
            frame = null;
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
            return !dataSegment.StartsWith(targetName) ? null : dataSegment.Substring(targetName.Length);
        }

        private static BoneRotationsFrame BuildLegacyFrame(string[] parts)
        {
            var frame = new BoneRotationsFrame
            {
                RecordTime = float.Parse(parts[0], CultureInfo.InvariantCulture),
                HipPosition = new Vector3(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture)
                ),
                BoneRotations = ParseBoneRotations(parts, 4)
            };
            return frame;
        }

        private static List<Quaternion> ParseBoneRotations(string[] allQuaternionParts, int startIndex)
        {
            var rotations = new List<Quaternion>();
            for (var i = startIndex; i + 3 < allQuaternionParts.Length; i += 4)
            {
                var rotation = ParseQuaternion(
                    allQuaternionParts[i],
                    allQuaternionParts[i + 1],
                    allQuaternionParts[i + 2],
                    allQuaternionParts[i + 3]);
                rotations.Add(rotation);
            }
            return rotations;
        }

        private static void HandleTakeBreak(Frame frame, List<Frame> currentFrames,
            List<RecordingTake> takes, ref float lastTime)
        {
            if (lastTime < 0) return;

            var isRewind = frame.RecordTime < lastTime;
            var isGap = frame.RecordTime - lastTime > 1.0f;

            if (!isRewind && !isGap) return;
            if (currentFrames.Count <= 0) return;

            takes.Add(new RecordingTake { Frames = new List<Frame>(currentFrames) });
            currentFrames.Clear();
        }

        private static bool DetectLogMarkers(string filePath)
        {
            using var reader = OpenReadOnlyTextFile(filePath);
            var isHumr = false;
            var isLegacy = false;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf(LogMatchTarget, StringComparison.Ordinal) >= 0) isHumr = true;
                if (line.IndexOf(LegacyLogMatchTarget, StringComparison.Ordinal) >= 0) isLegacy = true;
                if (isHumr || isLegacy) return true;
            }

            return false;
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
                    return "HUMR (Corrupted)"; // TODO: Never displayed
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
                var fileType = DetectLogMarkers(filePath) ? LogType.Humr : LogType.NoData;
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
                    return new[] { CorruptTargetTuple };
                case LogType.NoData:
                default:
                    return new[] { (TargetType.Unknown, "No HUMR data") };
            }
        }
    }
}