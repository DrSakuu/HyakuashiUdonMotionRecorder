using System;
using System.Globalization;
using UnityEngine;

namespace DrSakuu.Humr
{
    public enum TargetType
    {
        Unknown,
        Legacy,
        BoneRotations,
        Object,
        BoneRotationsWithIK,
        HumanMuscles
    }

    public static class HumrLogger
    {
        private const string HumrTag = "[HUMR]";
        private const string LegacyHumrTag = "HUMR:";
        private const string LogTagPrefix = "-  ";
        private const string RecordingTag = "RECORDING";
        private const char VariableDelimiter = ';';
        private const char ComponentDelimiter = ',';
        private const string FloatFormat = "F6";

        private const string MissingValue = "MISSING";
        private const string UnsupportedTargetType = "Unsupported";

        private const string HumrRecordingPrefix = LogTagPrefix + HumrTag + " " + RecordingTag;
        private const string LegacyHumrPrefix = LogTagPrefix + LegacyHumrTag;

        public static void Log(object message)
        {
            Debug.Log($"{HumrTag} {message}");
        }

        public static void Warning(object message)
        {
            Debug.LogWarning($"{HumrTag} {message}");
        }

        public static void Error(object message)
        {
            Debug.LogError($"{HumrTag} {message}");
        }

        public static void Assertion(object message)
        {
            Debug.LogAssertion($"{HumrTag} {message}");
        }

        public static string InitializeFrame(TargetType targetType, string targetName, long takeTimestamp, float time)
        {
            var typeText = TargetTypeToString(targetType);
            var timeText = FormatFloat(time);
        
            return string.Join(VariableDelimiter, RecordingTag, typeText, targetName, takeTimestamp, timeText);
        }

        public static string AppendObject(string outputString, object recordedObject)
        {
            string valueText;
            switch (recordedObject.GetType().Name)
            {
                case "Vector3":
                    valueText = FormatVector3Components((Vector3)recordedObject);
                    break;
                case "Quaternion":
                    valueText = FormatQuaternionComponents((Quaternion)recordedObject);
                    break;
                default:
                    valueText = recordedObject.ToString();
                    break;
            }
            
            return string.Join(VariableDelimiter, outputString, valueText);
        }

        public static string JoinComponents(params object[] components)
        {
            return string.Join(ComponentDelimiter, components);
        }

        public static int AnyHumrFrameStartIndex(string line)
        {
            var frameStartIndex = HumrFrameStartIndex(line);
            return frameStartIndex >= 0 ? frameStartIndex : LegacyHumrFrameStartIndex(line);
        }

        public static int HumrFrameStartIndex(string line)
        {
            return FindDataStartIndex(line, HumrRecordingPrefix, 1);
        }

        public static int LegacyHumrFrameStartIndex(string line)
        {
            return FindDataStartIndex(line, LegacyHumrPrefix);
        }

        public static string SplitNextVariable(string line, out string remaining)
        {
            remaining = line;

            var delimiterIndex = line.IndexOf(VariableDelimiter);
            if (delimiterIndex < 0) return null;

            remaining = line.Substring(delimiterIndex + 1);
            return line.Substring(0, delimiterIndex);
        }

        public static string[] SplitLegacyLine(string line, string targetName)
        {
            var targetPrefix = $"{LegacyHumrPrefix}{targetName}";
            return line.Split(new[] { targetPrefix }, StringSplitOptions.None);
        }

        public static string[] SplitLegacyFrame(string frame)
        {
            return frame.Split(ComponentDelimiter);
        }

        public static string JoinLogPrefixToFrame(string prefix, string frame)
        {
            return $"{prefix}{LogTagPrefix}{HumrTag} {frame}";
        }

        public static int TargetFrameStartIndex(string line, TargetType targetType, string targetName)
        {
            var targetPrefix = string.Join(
                VariableDelimiter,
                HumrRecordingPrefix,
                targetType,
                targetName,
                string.Empty);

            return FindDataStartIndex(line, targetPrefix);
        }

        public static string[] SplitFrame(string frame)
        {
            return frame.Split(VariableDelimiter);
        }

        public static bool TryParseVector3(string vector3String, out Vector3 vector)
        {
            vector = default;

            var components = vector3String.Split(ComponentDelimiter);
            if (components.Length != 3) return false;

            if (!TryParseFloat(components[0], out var x)) return false;
            if (!TryParseFloat(components[1], out var y)) return false;
            if (!TryParseFloat(components[2], out var z)) return false;

            vector = new Vector3(x, y, z);
            return true;
        }

        public static bool TryParseQuaternion(string quaternionString, out Quaternion quaternion)
        {
            quaternion = default;

            var components = quaternionString.Split(ComponentDelimiter);
            if (components.Length != 4) return false;

            if (!TryParseFloat(components[0], out var x)) return false;
            if (!TryParseFloat(components[1], out var y)) return false;
            if (!TryParseFloat(components[2], out var z)) return false;
            if (!TryParseFloat(components[3], out var w)) return false;

            quaternion = new Quaternion(x, y, z, w);
            return true;
        }

        private static int FindDataStartIndex(string line, string prefix, int extraOffset = 0)
        {
            var prefixIndex = line.IndexOf(prefix, StringComparison.Ordinal);
            return prefixIndex < 0 ? -1 : prefixIndex + prefix.Length + extraOffset;
        }

        private static string TargetTypeToString(TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.Legacy:
                    return nameof(TargetType.Legacy);
                case TargetType.BoneRotations:
                    return nameof(TargetType.BoneRotations);
                case TargetType.Object:
                    return nameof(TargetType.Object);
                default:
                    return UnsupportedTargetType;
            }
        }

        private static string FormatVector3Components(Vector3 vector)
        {
            return JoinComponents(
                FormatFloat(vector.x),
                FormatFloat(vector.y),
                FormatFloat(vector.z));
        }

        private static string FormatQuaternionComponents(Quaternion quaternion)
        {
            return JoinComponents(
                FormatFloat(quaternion.x),
                FormatFloat(quaternion.y),
                FormatFloat(quaternion.z),
                FormatFloat(quaternion.w));
        }

        private static string FormatFloat(float value)
        {
            return value.ToString(FloatFormat, CultureInfo.InvariantCulture);
        }

        private static bool TryParseFloat(string floatString, out float floatValue)
        {
            return float.TryParse(floatString, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue);
        }
    }
}