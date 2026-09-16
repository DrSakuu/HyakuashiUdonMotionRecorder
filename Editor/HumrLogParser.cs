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
        public RecordingTake[] takes;
        public DateTime LastWriteTime;
        public (TargetType targetType, string name)[] Targets;
    }

    [Serializable]
    public class RecordingTake
    {
        public TargetType targetType;
        public string targetName;
        public long takeTimestamp;
        public string takeName;
        public bool includeInFbx = true;

        public virtual bool IsEmpty => true;
    }

    [Serializable]
    public class BoneRotationsTake : RecordingTake
    {
        public PropertyCurve[] HipCurves { get; set; }
        public PropertyCurve[][] BoneCurves { get; set; }

        public BoneRotationsTake()
        {
            HipCurves = new[]
            {
                new PropertyCurve("localPosition.x"),
                new PropertyCurve("localPosition.y"),
                new PropertyCurve("localPosition.z")
            };

            var boneCount = HumanTrait.BoneCount;
            BoneCurves = new PropertyCurve[boneCount][];
            for (var i = 0; i < boneCount; i++)
            {
                BoneCurves[i] = new[]
                {
                    new PropertyCurve("localRotation.x"),
                    new PropertyCurve("localRotation.y"),
                    new PropertyCurve("localRotation.z"),
                    new PropertyCurve("localRotation.w")
                };
            }
        }

        public override bool IsEmpty => HipCurves == null || HipCurves[0].curve.length == 0;
    }

    [Serializable]
    public class ObjectTake : RecordingTake
    {
        public PropertyCurve[] ObjectCurves { get; set; }

        public ObjectTake()
        {
            ObjectCurves = new[]
            {
                new PropertyCurve("localPosition.x"),
                new PropertyCurve("localPosition.y"),
                new PropertyCurve("localPosition.z"),
                new PropertyCurve("localRotation.x"),
                new PropertyCurve("localRotation.y"),
                new PropertyCurve("localRotation.z"),
                new PropertyCurve("localRotation.w"),
                new PropertyCurve("localScale.x"),
                new PropertyCurve("localScale.y"),
                new PropertyCurve("localScale.z")
            };
        }

        public override bool IsEmpty => ObjectCurves == null || ObjectCurves[0].curve.length == 0;
    }

    [Serializable]
    public class PropertyCurve
    {
        public string propertyName;
        public AnimationCurve curve;

        public PropertyCurve(string propertyName)
        {
            this.propertyName = propertyName;
            this.curve = new AnimationCurve();
        }
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

        public static RecordingTake[] ParseTakes(
            string[] lines, (TargetType targetType, string targetName) targetTuple)
        {
            var takesList = new List<RecordingTake>();
            var currentTake = CreateRecordingTake(targetTuple);
            var previousTime = -1f;

            foreach (var line in lines)
            {
                var frameStartIndex = HumrLogger.TargetFrameStartIndex(
                    line, targetTuple.targetType, targetTuple.targetName);
                if (frameStartIndex < 0) continue;

                var frameText = line.Substring(frameStartIndex);
                if (!TryParseFrame(frameText, out var parsedFrame)) continue;

                if (currentTake.takeTimestamp == 0 && currentTake.IsEmpty)
                {
                    currentTake.takeTimestamp = parsedFrame.timestamp;
                }
                else if (IsNewTake(currentTake, parsedFrame.timestamp, parsedFrame.recordTime, previousTime))
                {
                    takesList.Add(currentTake);
                    currentTake = CreateRecordingTake(targetTuple, parsedFrame.timestamp);
                    previousTime = -1f;
                }

                switch (currentTake)
                {
                    case ObjectTake objectTake:
                    {
                        if (TryParseObjectValues(parsedFrame.parts, out var recordTime, out var pos, out var rot, out var scale))
                        {
                            AddObjectCurveKeys(objectTake, recordTime, pos, rot, scale);
                            previousTime = recordTime;
                        }

                        break;
                    }
                    case BoneRotationsTake boneTake:
                    {
                        if (TryParseBoneValues(parsedFrame.parts, out var recordTime, out var hipPos, out var rotations))
                        {
                            AddBoneCurveKeys(boneTake, recordTime, hipPos, rotations);
                            previousTime = recordTime;
                        }
                        break;
                    }
                }
            }

            if (!currentTake.IsEmpty) takesList.Add(currentTake);

            var takes = takesList.ToArray();
            for (var i = 0; i < takes.Length; i++)
            {
                if (string.IsNullOrEmpty(takes[i].takeName)) takes[i].takeName = $"Take{i + 1}";
            }
            
            return takes;
        }

        private static RecordingTake CreateRecordingTake(
            (TargetType targetType, string targetName) targetTuple, long takeTimestamp = 0)
        {
            RecordingTake take = targetTuple.targetType == TargetType.Object
                ? new ObjectTake()
                : new BoneRotationsTake();
        
            take.targetType = targetTuple.targetType;
            take.targetName = targetTuple.targetName;
            take.takeTimestamp = takeTimestamp;
            return take;
        }

        private static bool IsNewTake(
            RecordingTake currentTake,
            long newTimestamp,
            float currentTime,
            float previousTime)
        {
            if (currentTake.IsEmpty) return false;
            return newTimestamp != currentTake.takeTimestamp || currentTime < previousTime;
        }

        private static bool TryParseBoneValues(
            string[] parts,
            out float recordTime,
            out Vector3 hipPosition,
            out Quaternion[] rotations)
        {
            recordTime = 0f;
            hipPosition = Vector3.zero;
            rotations = null;

            if (parts.Length < 3) return false;
    
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out recordTime)) 
                return false;
        
            if (!HumrLogger.TryParseVector3(parts[2], out hipPosition)) 
                return false;

            var rotationList = new List<Quaternion>();
            for (var i = 3; i < parts.Length; i++)
            {
                if (HumrLogger.TryParseQuaternion(parts[i], out var rotation))
                    rotationList.Add(rotation);
            }

            if (rotationList.Count == 0) return false;

            rotations = rotationList.ToArray();
            return true;
        }

        private static bool TryParseObjectValues(
            string[] parts, out float recordTime, out Vector3 position, out Quaternion rotation, out Vector3 localScale)
        {
            recordTime = 0f;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            localScale = Vector3.one;

            if (parts.Length < 5) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out recordTime)) return false;
            if (!HumrLogger.TryParseVector3(parts[2], out position)) return false;
            if (!HumrLogger.TryParseQuaternion(parts[3], out rotation)) return false;
            if (!HumrLogger.TryParseVector3(parts[4], out localScale)) return false;

            return true;
        }

        private static void AddObjectCurveKeys(ObjectTake take, float time, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var curves = take.ObjectCurves;
            curves[0].curve.AddKey(time, pos.x);
            curves[1].curve.AddKey(time, pos.y);
            curves[2].curve.AddKey(time, pos.z);
            curves[3].curve.AddKey(time, rot.x);
            curves[4].curve.AddKey(time, rot.y);
            curves[5].curve.AddKey(time, rot.z);
            curves[6].curve.AddKey(time, rot.w);
            curves[7].curve.AddKey(time, scale.x);
            curves[8].curve.AddKey(time, scale.y);
            curves[9].curve.AddKey(time, scale.z);
        }
        
        private static void AddBoneCurveKeys(BoneRotationsTake take, float time, Vector3 hipPos, Quaternion[] rotations)
        {
            take.HipCurves[0].curve.AddKey(time, hipPos.x);
            take.HipCurves[1].curve.AddKey(time, hipPos.y);
            take.HipCurves[2].curve.AddKey(time, hipPos.z);

            var rotationCount = Mathf.Min(rotations.Length, take.BoneCurves.Length);
            for (var i = 0; i < rotationCount; i++)
            {
                take.BoneCurves[i][0].curve.AddKey(time, rotations[i].x);
                take.BoneCurves[i][1].curve.AddKey(time, rotations[i].y);
                take.BoneCurves[i][2].curve.AddKey(time, rotations[i].z);
                take.BoneCurves[i][3].curve.AddKey(time, rotations[i].w);
            }
        }

        public static RecordingFile CreateRecordingFile(string filePath)
        {
            var type = DetectHumrMarkers(filePath) ? LogType.Humr : LogType.NoData;
            return new RecordingFile
            {
                path = filePath,
                type = type,
                LastWriteTime = File.GetLastWriteTime(filePath),
                fileName = BuildRecordingDisplayName(filePath, type)
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