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
        public Quaternion[] BoneRotations { get; set; }
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
        //TODO: move to HumrLogger
        private static readonly string RecordMatchStr = $"-  {HumrLogger.HumrTag} {HumrLogger.RecordingTag}";
        private const string LegacyMatchStr = "-  HUMR:";

        private static readonly (TargetType, string) CorruptTargetTuple = (TargetType.Unknown, "HUMR data is corrupt");

        public static string[] LoadHumrLogLines(string path)
        {
            var lines = new List<string>();
            using var reader = OpenReadOnlyTextFile(path);
            while (reader.ReadLine() is { } line)
            {
                if (line.IndexOf(RecordMatchStr, StringComparison.Ordinal) >=0 
                    || line.IndexOf(LegacyMatchStr, StringComparison.Ordinal) >=0) 
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

        public static (TargetType, string)[] ScanTargets(RecordingFile file)
        {
            switch (file.type)
            {
                case LogType.Humr:
                    return ScanHumrTargets(file);
                case LogType.Corrupt:
                    return new[] { CorruptTargetTuple };
                case LogType.NoData:
                default:
                    return new[] { (TargetType.Unknown, "No HUMR data") };
            }
        }

        private static (TargetType, string)[] ScanHumrTargets(RecordingFile recordingFile)
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
            if (line.IndexOf(RecordMatchStr, StringComparison.Ordinal) >= 0) return ExtractTarget(line);
            return line.IndexOf(LegacyMatchStr, StringComparison.Ordinal) >= 0 
                ? ExtractLegacyTarget(line) : CorruptTargetTuple;
        }

        private static (TargetType, string) ExtractTarget(string line)
        {
            if (line.IndexOf(RecordMatchStr, StringComparison.Ordinal) < 0) return CorruptTargetTuple;

            var recordingFrame = line.Substring(
                line.IndexOf(RecordMatchStr, StringComparison.Ordinal) + RecordMatchStr.Length + 1);
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
            var prefixIdx = line.IndexOf(LegacyMatchStr, StringComparison.Ordinal);
            if (prefixIdx == -1) return CorruptTargetTuple;

            var dataSegment = line.Substring(prefixIdx + LegacyMatchStr.Length).Trim();

            var digitIdx = PathUtils.FindFirstDigitIndex(dataSegment);
            return digitIdx == -1 ? CorruptTargetTuple : (TargetType.Legacy, dataSegment.Substring(0, digitIdx));
        }

        public static string[] ConvertLegacyLines(string[] logLines, string targetName)
        {
            var legacyMatchTarget = string.Join("", LegacyMatchStr, targetName);
            const TargetType targetType = TargetType.Legacy;
            var newlines = new List<string>();
            var previousTime = 0f;
            var takeTimestamp = 1;
            foreach (var line in logLines)
            {
                var legacyTargetSplit = line.Split(legacyMatchTarget);
                var legacyFrameSplit = legacyTargetSplit[1].Split(HumrLogger.ComponentDelimiter);
                
                var time = float.Parse(legacyFrameSplit[0], CultureInfo.InvariantCulture); //TODO: TryParse
                if (previousTime > time)
                {
                    takeTimestamp++;
                }
                var newFrame = HumrLogger.InitializeFrame(targetType, targetName, takeTimestamp, time);
                previousTime = time;
                
                var hipsPositionStr = HumrLogger.JoinComponents(
                    legacyFrameSplit[1], legacyFrameSplit[2], legacyFrameSplit[3]);
                newFrame = HumrLogger.AppendObject(newFrame, hipsPositionStr);
                for (var i = 4; i < legacyFrameSplit.Length; i += 4)
                {
                    var quaternionStr = HumrLogger.JoinComponents( 
                        legacyFrameSplit[i], legacyFrameSplit[i + 1], legacyFrameSplit[i + 2], legacyFrameSplit[i + 3]);
                    newFrame = HumrLogger.AppendObject(newFrame, quaternionStr);
                }

                var newline = $"{legacyTargetSplit[0]}-  {HumrLogger.HumrTag} {newFrame}";
                newlines.Add(newline);
            }
            return newlines.ToArray();
        }

        public static List<RecordingTake> ParseTakes(string[] lines, (TargetType targetType, string targetName) target)
        {
            var takes = new List<RecordingTake>();
            var currentTake = new RecordingTake { targetType = target.targetType, targetName = target.targetName };
            var targetMatchStr = string.Join(
                HumrLogger.VariableDelimiter, RecordMatchStr, target.targetType, target.targetName, "");
            var previousTime = -1f;

            foreach (var line in lines)
            {
                if (line.IndexOf(targetMatchStr, StringComparison.Ordinal) < 0) continue;

                var takeStr = line.Split(targetMatchStr)[1];
                if (!TryParseTake(takeStr, out var takeSplit, out var currentTime)) continue;

                var timestamp = long.Parse(takeSplit[0]);
                if (currentTake.takeTimestamp == 0 && currentTake.Frames.Count == 0)
                {
                    currentTake.takeTimestamp = timestamp;
                }
                else if (HandleTakeBreak(currentTake, timestamp, currentTime, previousTime))
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake
                    {
                        targetType = target.targetType, targetName = target.targetName, takeTimestamp = timestamp
                    };
                    previousTime = -1;
                }

                var frame = ParseFrame(target.targetType, takeSplit);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                previousTime = currentTime;
            }

            if (currentTake.Frames.Count > 0) takes.Add(currentTake);

            return takes;
        }

        private static bool TryParseTake(string takeStr, out string[] takeSplit, out float currentTime)
        {
            takeSplit = null;
            currentTime = -1f;

            var split = takeStr.Split(HumrLogger.VariableDelimiter);
            if (!float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                return false;

            takeSplit = split;
            currentTime = time;
            return true;
        }

        private static bool HandleTakeBreak(
            RecordingTake currentTake, long newTimestamp, float currentTime, float previousTime)
        {
            if (currentTake.Frames.Count == 0) return false; //TODO: remove so only currentTimestamp is needed

            var timestampChanged = newTimestamp != currentTake.takeTimestamp;
            var timeRewound = currentTime < previousTime;
            return timestampChanged || timeRewound;
        }

        private static Frame ParseFrame(TargetType targetType, string[] takeSplit)
        {
            switch (targetType)
            {
                case TargetType.BoneRotations:
                case TargetType.Legacy:
                    return ParseBoneRotationsFrame(takeSplit);
                case TargetType.Object:
                    return ParseObjectFrame(takeSplit);
                case TargetType.Unknown:
                case TargetType.BoneRotationsWithIK:
                case TargetType.HumanMuscles:
                default:
                    return null;
            }
        }

        private static BoneRotationsFrame ParseBoneRotationsFrame(string[] parts)
        {
            if (parts.Length < 3) return null;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var recordTime))
                return null;

            if (!TryParseVector3(parts[2], out var position))
                return null;

            var frame = new BoneRotationsFrame
            {
                RecordTime = recordTime,
                HipPosition = position
            };

            if (!TryParseBoneRotations(parts, out var rotations))
                return null;

            frame.BoneRotations = rotations;
            return frame;
        }

        private static bool TryParseBoneRotations(string[] parts, out Quaternion[] rotations)
        {
            rotations = null;
            var rotationsList = new List<Quaternion>();
            for (var i = 3; i < parts.Length; i++)
            {
                if (!TryParseQuaternion(parts[i], out var rotation)) continue;
                rotationsList.Add(rotation);
            }
            rotations = rotationsList.ToArray();
            return rotations != null;
        }

        private static ObjectFrame ParseObjectFrame(string[] parts)
        {
            if (parts.Length < 5) return null;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var recordTime))
                return null;

            if (!TryParseVector3(parts[2], out var position))
                return null;

            if (!TryParseQuaternion(parts[3], out var rotation))
                return null;

            if (!TryParseVector3(parts[4], out var localScale))
                return null;

            return new ObjectFrame
            {
                RecordTime = recordTime,
                Position = position,
                Rotation = rotation,
                LocalScale = localScale
            };
        }

        private static bool TryParseVector3(string vector3String, out Vector3 vector)
        {
            vector = default;

            var parts = vector3String.Split(HumrLogger.ComponentDelimiter);
            if (parts.Length != 3) return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                return false;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return false;

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;

            vector = new Vector3(x, y, z);
            return true;
        }

        private static bool TryParseQuaternion(string quaternionString, out Quaternion quaternion)
        {
            quaternion = default;

            var parts = quaternionString.Split(HumrLogger.ComponentDelimiter);
            if (parts.Length != 4) return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                return false;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return false;

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;

            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
                return false;

            quaternion = new Quaternion(x, y, z, w);
            return true;
        }

        private static void HandleLegacyTakeBreak(Frame frame, List<Frame> frames,
            List<RecordingTake> takes, ref float lastTime)
        {
            if (lastTime < 0) return;

            var isRewind = frame.RecordTime < lastTime;
            var isGap = frame.RecordTime - lastTime > 1.0f;

            if (!isRewind && !isGap) return;
            if (frames.Count <= 0) return;

            takes.Add(new RecordingTake { Frames = new List<Frame>(frames) });
            frames.Clear();
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
                var fileType = DetectHumrMarkers(filePath) ? LogType.Humr : LogType.NoData;
                var writeTime = File.GetLastWriteTime(filePath);
                var fileName = BuildRecordingDisplayName(filePath, fileType);
                discoveredFiles.Add(new RecordingFile
                {
                    path = filePath, type = fileType, LastWriteTime = writeTime, fileName = fileName
                });
            }

            return discoveredFiles
                .OrderByDescending(entry => entry.LastWriteTime)
                .ToList();
        }

        private static bool DetectHumrMarkers(string filePath)
        {
            using var reader = OpenReadOnlyTextFile(filePath);
            var isHumr = false;
            var isLegacy = false;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf(RecordMatchStr, StringComparison.Ordinal) >= 0) isHumr = true;
                if (line.IndexOf(LegacyMatchStr, StringComparison.Ordinal) >= 0) isLegacy = true;
                if (isHumr || isLegacy) return true;
            }

            return false;
        }

        private static string BuildRecordingDisplayName(string filePath, LogType type)
        {
            var logFileRegex = new Regex(@"^output_log_|\.txt$");
            var rawFileName = Path.GetFileName(filePath);
            var cleanedFileName = logFileRegex.Replace(rawFileName, "");
            var typeName = LogTypeToDisplayString(type);
            return $"{cleanedFileName} {typeName}";
        }
    }
}