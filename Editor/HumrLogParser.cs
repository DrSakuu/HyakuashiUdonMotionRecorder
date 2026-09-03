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
        private static readonly (TargetType, string) CorruptTargetTuple = (TargetType.Unknown, "HUMR data is corrupt");

        public static string[] LoadHumrLogLines(string path)
        {
            var lines = new List<string>();
            using var reader = OpenReadOnlyTextFile(path);
            while (reader.ReadLine() is { } line)
            {
                if (HumrLogger.AnyHumrFrameStartIndex(line) >= 0) lines.Add(line);
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
            if (HumrLogger.HumrFrameStartIndex(line) >= 0) return ExtractTarget(line);

            return HumrLogger.LegacyHumrFrameStartIndex(line) >= 0 ? ExtractLegacyTarget(line) : CorruptTargetTuple;
        }

        private static (TargetType, string) ExtractTarget(string line)
        {
            var frame = line.Substring(HumrLogger.HumrFrameStartIndex(line));
            var typeStr = HumrLogger.SplitNextVariable(frame, out var remaining);
            if (!Enum.TryParse<TargetType>(typeStr, out var targetType)) return CorruptTargetTuple;

            var targetName = HumrLogger.SplitNextVariable(remaining, out _);
            return (targetType, targetName);
        }

        private static (TargetType, string) ExtractLegacyTarget(string line)
        {
            var dataSegment = line.Substring(HumrLogger.LegacyHumrFrameStartIndex(line)).Trim();

            var digitIdx = PathUtils.FindFirstDigitIndex(dataSegment);
            return digitIdx == -1 ? CorruptTargetTuple : (TargetType.Legacy, dataSegment.Substring(0, digitIdx));
        }

        public static string[] ConvertLegacyLines(string[] logLines, string targetName)
        {
            const TargetType targetType = TargetType.Legacy;
            var newlines = new List<string>();
            var previousTime = 0f;
            var takeTimestamp = 1;
            foreach (var line in logLines)
            {
                var legacyLineSplit = HumrLogger.SplitLegacyLine(line, targetName);
                var legacyFrameSplit = HumrLogger.SplitLegacyFrame(legacyLineSplit[1]);
                
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

                var newline = HumrLogger.JoinLogPrefixToFrame(legacyLineSplit[0], newFrame);
                newlines.Add(newline);
            }
            return newlines.ToArray();
        }

        public static List<RecordingTake> ParseTakes(string[] lines, (TargetType targetType, string targetName) target)
        {
            var takes = new List<RecordingTake>();
            var currentTake = new RecordingTake { targetType = target.targetType, targetName = target.targetName };
            var previousTime = -1f;

            foreach (var line in lines)
            {
                var frameStartIndex = HumrLogger.TargetFrameStartIndex(line, target);
                if (frameStartIndex < 0) continue;

                var frameStr = line.Substring(frameStartIndex);
                if (!TryParseFrame(frameStr, out (long timestamp, float currentTime, string[] objectsSplit) frameTuple)) continue;

                if (currentTake.takeTimestamp == 0 && currentTake.Frames.Count == 0)
                {
                    currentTake.takeTimestamp = frameTuple.timestamp;
                }
                else if (HandleTakeBreak(currentTake, frameTuple.timestamp, frameTuple.currentTime, previousTime))
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake
                    {
                        targetType = target.targetType, targetName = target.targetName, takeTimestamp = frameTuple.timestamp
                    };
                    previousTime = -1;
                }

                var frame = ParseFrame(target.targetType, frameTuple.objectsSplit);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                previousTime = frameTuple.currentTime;
            }

            if (currentTake.Frames.Count > 0) takes.Add(currentTake);

            return takes;
        }

        private static bool TryParseFrame(string frameStr, out (long, float, string[]) frameTuple)
        {
            frameTuple = default;

            var frameSplit = HumrLogger.SplitFrame(frameStr);
            if (!float.TryParse(frameSplit[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                return false;

            var timestamp = long.Parse(frameSplit[0]);
            frameTuple = (timestamp, time, frameSplit);
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

            if (!HumrLogger.TryParseVector3(parts[2], out var position))
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
                if (!HumrLogger.TryParseQuaternion(parts[i], out var rotation)) continue;
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

            if (!HumrLogger.TryParseVector3(parts[2], out var position))
                return null;

            if (!HumrLogger.TryParseQuaternion(parts[3], out var rotation))
                return null;

            if (!HumrLogger.TryParseVector3(parts[4], out var localScale))
                return null;

            return new ObjectFrame
            {
                RecordTime = recordTime,
                Position = position,
                Rotation = rotation,
                LocalScale = localScale
            };
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
                if (HumrLogger.HumrFrameStartIndex(line) > 0) isHumr = true;
                if (HumrLogger.LegacyHumrFrameStartIndex(line) > 0) isLegacy = true;
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