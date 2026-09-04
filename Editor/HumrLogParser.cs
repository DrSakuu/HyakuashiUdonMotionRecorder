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
        private static readonly Regex LogFileNameCleanupRegex = new(@"^output_log_|\.txt$", RegexOptions.Compiled);

        public static string[] LoadHumrLogLines(string path)
        {
            var lines = new List<string>();
            using var reader = OpenReadOnlyTextFile(path);
            while (reader.ReadLine() is { } line)
                if (HumrLogger.AnyHumrFrameStartIndex(line) >= 0)
                    lines.Add(line);
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
            return file.type switch
            {
                LogType.Humr => ScanHumrTargets(file),
                LogType.Corrupt => new[] { CorruptTargetTuple },
                _ => new[] { (TargetType.Unknown, "No HUMR data") }
            };
        }

        public static string[] ConvertLegacyLines(string[] logLines, string targetName)
        {
            const TargetType targetType = TargetType.Legacy;

            var convertedLines = new List<string>();
            var previousTime = 0f;
            var takeTimestamp = 1L;

            foreach (var line in logLines)
            {
                var legacyLineParts = HumrLogger.SplitLegacyLine(line, targetName);
                if (legacyLineParts.Length < 2) continue;

                var legacyFrameParts = HumrLogger.SplitLegacyFrame(legacyLineParts[1]);
                if (legacyFrameParts.Length < 4) continue;

                if (!float.TryParse(
                        legacyFrameParts[0],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var time))
                    continue;

                if (previousTime > time) takeTimestamp++;

                var frame = HumrLogger.InitializeFrame(targetType, targetName, takeTimestamp, time);
                previousTime = time;
                
                var hipsPosition = HumrLogger.JoinComponents(
                    legacyFrameParts[1],
                    legacyFrameParts[2],
                    legacyFrameParts[3]);
                
                frame = HumrLogger.AppendObject(frame, hipsPosition);
                
                for (var i = 4; i + 3 < legacyFrameParts.Length; i += 4)
                {
                    var quaternion = HumrLogger.JoinComponents(
                        legacyFrameParts[i],
                        legacyFrameParts[i + 1],
                        legacyFrameParts[i + 2],
                        legacyFrameParts[i + 3]);
                
                    frame = HumrLogger.AppendObject(frame, quaternion);
                }
                
                convertedLines.Add(HumrLogger.JoinLogPrefixToFrame(legacyLineParts[0], frame));
            }

            return convertedLines.ToArray();
        }

        public static List<RecordingTake> ParseTakes(
            string[] lines,
            (TargetType targetType, string targetName) target)
        {
            var takes = new List<RecordingTake>();
            var currentTake = CreateRecordingTake(target);
            var previousTime = -1f;

            foreach (var line in lines)
            {
                var frameStartIndex = HumrLogger.TargetFrameStartIndex(line, target.targetType, target.targetName);
                if (frameStartIndex < 0) continue;

                var frameText = line.Substring(frameStartIndex);
                if (!TryParseFrame(frameText, out var parsedFrame)) continue;

                if (currentTake.takeTimestamp == 0 && currentTake.Frames.Count == 0)
                {
                    currentTake.takeTimestamp = parsedFrame.timestamp;
                }
                else if (IsNewTake(currentTake, parsedFrame.timestamp, parsedFrame.recordTime, previousTime))
                {
                    takes.Add(currentTake);
                    currentTake = CreateRecordingTake(target, parsedFrame.timestamp);
                    previousTime = -1f;
                }

                var frame = ParseFrame(target.targetType, parsedFrame.parts);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                previousTime = parsedFrame.recordTime;
            }

            if (currentTake.Frames.Count > 0)
                takes.Add(currentTake);

            return takes;
        }

        public static List<RecordingFile> CollectRecordingFiles(string[] filePaths)
        {
            return filePaths
                .Select(CreateRecordingFile)
                .OrderByDescending(file => file.LastWriteTime)
                .ToList();
        }

        private static RecordingFile CreateRecordingFile(string filePath)
        {
            var fileType = DetectHumrMarkers(filePath) ? LogType.Humr : LogType.NoData;

            return new RecordingFile
            {
                path = filePath,
                type = fileType,
                LastWriteTime = File.GetLastWriteTime(filePath),
                fileName = BuildRecordingDisplayName(filePath, fileType)
            };
        }

        private static (TargetType, string)[] ScanHumrTargets(RecordingFile recordingFile)
        {
            if (!File.Exists(recordingFile.path))
                return new[] { CorruptTargetTuple };

            var targets = new HashSet<(TargetType, string)>();

            using var reader = OpenReadOnlyTextFile(recordingFile.path);
            while (reader.ReadLine() is { } line)
            {
                var target = ExtractHumrOrLegacyTarget(line);
                if (target.Item1 == TargetType.Unknown) continue;

                targets.Add(target);
            }

            if (targets.Count > 0)
                return targets.ToArray();

            recordingFile.type = LogType.Corrupt;
            return new[] { CorruptTargetTuple };
        }

        private static (TargetType, string) ExtractHumrOrLegacyTarget(string line)
        {
            if (HumrLogger.HumrFrameStartIndex(line) >= 0)
                return ExtractTarget(line);

            return HumrLogger.LegacyHumrFrameStartIndex(line) >= 0
                ? ExtractLegacyTarget(line)
                : CorruptTargetTuple;
        }

        private static (TargetType, string) ExtractTarget(string line)
        {
            var frameStartIndex = HumrLogger.HumrFrameStartIndex(line);
            if (frameStartIndex < 0) return CorruptTargetTuple;

            var frame = line.Substring(frameStartIndex);
            var targetTypeText = HumrLogger.SplitNextVariable(frame, out var remaining);

            if (!Enum.TryParse(targetTypeText, out TargetType targetType))
                return CorruptTargetTuple;

            var targetName = HumrLogger.SplitNextVariable(remaining, out _);
            return string.IsNullOrEmpty(targetName)
                ? CorruptTargetTuple
                : (targetType, targetName);
        }

        private static (TargetType, string) ExtractLegacyTarget(string line)
        {
            var frameStartIndex = HumrLogger.LegacyHumrFrameStartIndex(line);
            if (frameStartIndex < 0) return CorruptTargetTuple;

            var dataSegment = line.Substring(frameStartIndex).Trim();
            var digitIndex = PathUtils.FindFirstDigitIndex(dataSegment);

            return digitIndex == -1
                ? CorruptTargetTuple
                : (TargetType.Legacy, dataSegment.Substring(0, digitIndex));
        }

        private static RecordingTake CreateRecordingTake(
            (TargetType targetType, string targetName) target,
            long takeTimestamp = 0)
        {
            return new RecordingTake
            {
                targetType = target.targetType,
                targetName = target.targetName,
                takeTimestamp = takeTimestamp
            };
        }

        private static bool TryParseFrame(
            string frameText,
            out (long timestamp, float recordTime, string[] parts) frame)
        {
            frame = default;

            var parts = HumrLogger.SplitFrame(frameText);
            if (parts.Length < 2) return false;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
                return false;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var recordTime))
                return false;

            frame = (timestamp, recordTime, parts);
            return true;
        }

        private static bool IsNewTake(
            RecordingTake currentTake,
            long newTimestamp,
            float currentTime,
            float previousTime)
        {
            if (currentTake.Frames.Count == 0) return false;

            return newTimestamp != currentTake.takeTimestamp || currentTime < previousTime;
        }

        private static Frame ParseFrame(TargetType targetType, string[] parts)
        {
            return targetType switch
            {
                TargetType.BoneRotations => ParseBoneRotationsFrame(parts),
                TargetType.Legacy => ParseBoneRotationsFrame(parts),
                TargetType.Object => ParseObjectFrame(parts),
                _ => null
            };
        }

        private static BoneRotationsFrame ParseBoneRotationsFrame(string[] parts)
        {
            if (parts.Length < 3) return null;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var recordTime))
                return null;

            if (!HumrLogger.TryParseVector3(parts[2], out var hipPosition))
                return null;

            if (!TryParseBoneRotations(parts, out var rotations))
                return null;

            return new BoneRotationsFrame
            {
                RecordTime = recordTime,
                HipPosition = hipPosition,
                BoneRotations = rotations
            };
        }

        private static bool TryParseBoneRotations(string[] parts, out Quaternion[] rotations)
        {
            var rotationList = new List<Quaternion>();

            for (var i = 3; i < parts.Length; i++)
                if (HumrLogger.TryParseQuaternion(parts[i], out var rotation))
                    rotationList.Add(rotation);

            rotations = rotationList.ToArray();
            return rotations.Length > 0;
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
            return type switch
            {
                LogType.Humr => "HUMR",
                LogType.Corrupt => "HUMR (Corrupted)",
                LogType.NoData => "----",
                _ => type.ToString()
            };
        }

        private static bool DetectHumrMarkers(string filePath)
        {
            using var reader = OpenReadOnlyTextFile(filePath);
            while (reader.ReadLine() is { } line)
            {
                if (HumrLogger.HumrFrameStartIndex(line) >= 0) return true;
                if (HumrLogger.LegacyHumrFrameStartIndex(line) >= 0) return true;
            }

            return false;
        }

        private static string BuildRecordingDisplayName(string filePath, LogType type)
        {
            var rawFileName = Path.GetFileName(filePath);
            var cleanedFileName = LogFileNameCleanupRegex.Replace(rawFileName, "");
            var typeName = LogTypeToDisplayString(type);

            return $"{cleanedFileName} {typeName}";
        }
    }
}