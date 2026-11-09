using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        public (TargetType type, string name)[] Targets;
    }

    [Serializable]
    public class RecordingTake
    {
        public TargetType targetType;
        public string targetName;
        public long takeTimestamp;
        public string takeName;
        public bool includeInFbx = true;
        public float length = float.MinValue;
        public float[] deltaTimes;
        public int FrameCount => deltaTimes.Length;

        public virtual bool IsEmpty => true;
    }

    [Serializable]
    public class BoneRotationsTake : RecordingTake
    {
        public BoneRotationsTake()
        {
            HipCurves = new[]
            {
                new PropertyCurve("position.x"),
                new PropertyCurve("position.y"),
                new PropertyCurve("position.z")
            };

            var boneCount = HumanTrait.BoneCount;
            BoneCurves = new PropertyCurve[boneCount][];
            for (var i = 0; i < boneCount; i++)
                BoneCurves[i] = new[]
                {
                    new PropertyCurve("rotation.x"),
                    new PropertyCurve("rotation.y"),
                    new PropertyCurve("rotation.z"),
                    new PropertyCurve("rotation.w")
                };
        }

        public PropertyCurve[] HipCurves { get; set; }
        public PropertyCurve[][] BoneCurves { get; set; }

        public override bool IsEmpty => HipCurves == null || HipCurves[0].curve.length == 0;
    }

    [Serializable]
    public class ObjectTake : RecordingTake
    {
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

        public PropertyCurve[] ObjectCurves { get; set; }

        public override bool IsEmpty => ObjectCurves == null || ObjectCurves[0].curve.length == 0;
    }

    [Serializable]
    public class PropertyCurve
    {
        public string propertyName;
        public AnimationCurve curve;

        public PropertyCurve(string newPropertyName)
        {
            propertyName = newPropertyName;
            curve = new AnimationCurve();
        }
    }

    public static class HumrLogParser
    {

        private static readonly (TargetType, string) CorruptTargetTuple = (TargetType.Unknown, "HUMR data is corrupt");
        private static readonly Regex LogFileNameCleanupRegex = new(@"^output_log_|\.txt$", RegexOptions.Compiled);

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

        public static (TargetType, string)[] ScanTargets(RecordingFile file)
        {
            return file.type switch
            {
                LogType.Humr => ScanHumrTargets(file),
                LogType.Corrupt => new[] { CorruptTargetTuple },
                _ => new[] { (TargetType.Unknown, "No HUMR data") }
            };
        }

        public static string[] LoadHumrLogLines(string path)
        {
            var lines = new List<string>();
            using var reader = OpenReadOnlyTextFile(path);
            while (reader.ReadLine() is { } line)
                if (HumrLogger.AnyHumrFrameStartIndex(line) >= 0)
                    lines.Add(line);
            return lines.ToArray();
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

        public static RecordingTake[] ParseTakes(string[] lines, (TargetType type, string name) targetTuple)
        {
            var takesList = new List<RecordingTake>();
            var currentTake = CreateRecordingTake(targetTuple);
            var previousTime = 0f;
            var deltaTimesList = new List<float>();
            var targetPrefix = $"{HumrLogger.HumrRecordingPrefix};{targetTuple.type};{targetTuple.name};";
            var rotations = new Quaternion[HumanTrait.BoneCount];

            foreach (var line in lines)
            {
                var frameStartIndex = line.IndexOf(targetPrefix, StringComparison.Ordinal);
                if (frameStartIndex < 0) continue;

                var frameText = line.AsSpan(frameStartIndex + targetPrefix.Length);
                if (!TryParseFrame(frameText, out var parsedFrame)) continue;

                if (currentTake.takeTimestamp == 0 && currentTake.IsEmpty)
                {
                    currentTake.takeTimestamp = parsedFrame.timestamp;
                }
                else if (IsNewTake(currentTake, parsedFrame.timestamp, parsedFrame.recordTime, previousTime))
                {
                    currentTake.deltaTimes = deltaTimesList.ToArray();
                    takesList.Add(currentTake);
                    currentTake = CreateRecordingTake(targetTuple, parsedFrame.timestamp);
                    previousTime = 0f;
                    deltaTimesList.Clear();
                }

                var recordTime = parsedFrame.recordTime;
                switch (currentTake)
                {
                    case ObjectTake objectTake:
                    {
                        if (TryParseObjectValues(
                                parsedFrame.parts,
                                out var pos,
                                out var rot,
                                out var scale))
                        {
                            AddObjectCurveKeys(objectTake, recordTime, pos, rot, scale);
                            if (deltaTimesList.Count != 0 || !Mathf.Approximately(recordTime - previousTime, 0f))
                                deltaTimesList.Add(recordTime - previousTime);

                            previousTime = recordTime;
                            if (recordTime > currentTake.length) currentTake.length = recordTime;
                        }

                        break;
                    }
                    case BoneRotationsTake boneTake:
                    {
                        if (TryParseBoneValues(
                                parsedFrame.parts,
                                out var hipPos,
                                rotations, out var rotationCount))
                        {
                            AddBoneCurveKeys(boneTake, recordTime, hipPos, rotations, rotationCount);
                            if (deltaTimesList.Count != 0 || !Mathf.Approximately(recordTime - previousTime, 0f))
                                deltaTimesList.Add(recordTime - previousTime);

                            previousTime = recordTime;
                            if (recordTime > currentTake.length) currentTake.length = recordTime;
                        }

                        break;
                    }
                }
            }

            currentTake.deltaTimes = deltaTimesList.ToArray();
            if (!currentTake.IsEmpty) takesList.Add(currentTake);

            var takes = takesList.ToArray();
            for (var i = 0; i < takes.Length; i++)
                if (string.IsNullOrEmpty(takes[i].takeName))
                    takes[i].takeName = $"Take{i + 1}";

            return takes;
        }

        private static bool DetectHumrMarkers(string filePath)
        {
            using var reader = OpenReadOnlyTextFile(filePath);
            var buffer = new char[16 * 1024];
            var overlapLength = Math.Max(HumrLogger.HumrRecordingPrefix.Length, HumrLogger.LegacyHumrPrefix.Length) - 1;
            var retained = 0;
            int read;
            while ((read = reader.Read(buffer, retained, buffer.Length - retained)) > 0)
            {
                var count = retained + read;
                var text = buffer.AsSpan(0, count);
                if (text.IndexOf(HumrLogger.HumrRecordingPrefix.AsSpan()) >= 0 ||
                    text.IndexOf(HumrLogger.LegacyHumrPrefix.AsSpan()) >= 0)
                    return true;

                // Preserve partial markers, including when a read returns fewer characters than requested.
                retained = Math.Min(overlapLength, count);
                Array.Copy(buffer, count - retained, buffer, 0, retained);
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

        private static StreamReader OpenReadOnlyTextFile(string filePath)
        {
            const int bufferSize = 64 * 1024;
            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.SequentialScan);

            return new StreamReader(fileStream, Encoding.UTF8, true, bufferSize);
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
            (TargetType type, string name) targetTuple, long takeTimestamp = 0)
        {
            RecordingTake take = targetTuple.type == TargetType.Object ? new ObjectTake() : new BoneRotationsTake();

            take.targetType = targetTuple.type;
            take.targetName = targetTuple.name;
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

        private static bool TryParseObjectValues(
            ReadOnlySpan<char> parts, out Vector3 position, out Quaternion rotation, out Vector3 localScale)
        {
            position = default;
            rotation = Quaternion.identity;
            localScale = Vector3.one;
            return TryParseVector3(TakeField(ref parts), out position) &&
                   TryParseQuaternion(TakeField(ref parts), out rotation) &&
                   TryParseVector3(TakeField(ref parts), out localScale);
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

        private static bool TryParseBoneValues(
            ReadOnlySpan<char> parts, out Vector3 hipPosition, Quaternion[] rotations, out int rotationCount)
        {
            rotationCount = 0;
            if (!TryParseVector3(TakeField(ref parts), out hipPosition)) return false;
            // Preserve the existing compaction of valid rotations; extra bones are ignored by the writer.
            while (!parts.IsEmpty && rotationCount < rotations.Length)
                if (TryParseQuaternion(TakeField(ref parts), out var rotation))
                    rotations[rotationCount++] = rotation;
            return rotationCount > 0;
        }

        private static void AddBoneCurveKeys(BoneRotationsTake take, float time, Vector3 hipPos, Quaternion[] rotations, int rotationCount)
        {
            take.HipCurves[0].curve.AddKey(time, hipPos.x);
            take.HipCurves[1].curve.AddKey(time, hipPos.y);
            take.HipCurves[2].curve.AddKey(time, hipPos.z);

            rotationCount = Mathf.Min(rotationCount, take.BoneCurves.Length);
            for (var i = 0; i < rotationCount; i++)
            {
                take.BoneCurves[i][0].curve.AddKey(time, rotations[i].x);
                take.BoneCurves[i][1].curve.AddKey(time, rotations[i].y);
                take.BoneCurves[i][2].curve.AddKey(time, rotations[i].z);
                take.BoneCurves[i][3].curve.AddKey(time, rotations[i].w);
            }
        }

        private ref struct ParsedFrame
        {
            public long timestamp;
            public float recordTime;
            public ReadOnlySpan<char> parts;
        }

        private static bool TryParseFrame(ReadOnlySpan<char> frameText, out ParsedFrame frame)
        {
            frame = default;
            if (frameText.IndexOf(';') < 0) return false;
            if (!long.TryParse(TakeField(ref frameText), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out frame.timestamp)) return false;
            if (!float.TryParse(TakeField(ref frameText), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out frame.recordTime)) return false;
            frame.parts = frameText;
            return true;
        }

        private static ReadOnlySpan<char> TakeField(ref ReadOnlySpan<char> remaining)
        {
            var separator = remaining.IndexOf(';');
            var field = separator < 0 ? remaining : remaining.Slice(0, separator);
            remaining = separator < 0 ? ReadOnlySpan<char>.Empty : remaining.Slice(separator + 1);
            return field;
        }

        private static bool TryParseComponents(ReadOnlySpan<char> text, Span<float> values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var separator = text.IndexOf(',');
                if ((separator < 0) != (i == values.Length - 1)) return false;
                var component = separator < 0 ? text : text.Slice(0, separator);
                if (!float.TryParse(component, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;
                if (separator >= 0) text = text.Slice(separator + 1);
            }
            return true;
        }

        private static bool TryParseVector3(ReadOnlySpan<char> text, out Vector3 value)
        {
            value = default;
            Span<float> components = stackalloc float[3];
            if (!TryParseComponents(text, components)) return false;
            value = new Vector3(components[0], components[1], components[2]);
            return true;
        }

        private static bool TryParseQuaternion(ReadOnlySpan<char> text, out Quaternion value)
        {
            value = default;
            Span<float> components = stackalloc float[4];
            if (!TryParseComponents(text, components)) return false;
            value = new Quaternion(components[0], components[1], components[2], components[3]);
            return true;
        }
    }
}
