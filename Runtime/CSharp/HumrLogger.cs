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

        private static string FormatVector3Components(Vector3 vector3, string format = FloatFormat)
        {
            var vector3XStr = vector3.x.ToString(format);
            var vector3YStr = vector3.y.ToString(format);
            var vector3ZStr = vector3.z.ToString(format);
            return string.Join(ComponentDelimiter, vector3XStr, vector3YStr, vector3ZStr);
        }

        private static string FormatQuaternionComponents(Quaternion quaternion, string format = FloatFormat)
        {
            var quaternionXStr = quaternion.x.ToString(format);
            var quaternionYStr = quaternion.y.ToString(format);
            var quaternionZStr = quaternion.z.ToString(format);
            var quaternionWStr = quaternion.w.ToString(format);
            return string.Join(ComponentDelimiter, quaternionXStr, quaternionYStr, quaternionZStr, quaternionWStr);
        }

        private static string TargetTypeToString(TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.Legacy:
                    return "Legacy";
                case TargetType.BoneRotations:
                    return "BoneRotations";
                case TargetType.Object:
                    return "Object";
                case TargetType.BoneRotationsWithIK:
                case TargetType.HumanMuscles:
                case TargetType.Unknown:
                default:
                    return "Unsupported";
            }
        }

        public static string InitializeFrame(TargetType targetType, string targetName, long takeTimestamp, float time)
        {
            var typeStr = TargetTypeToString(targetType);
            var timeStr = time.ToString(FloatFormat, CultureInfo.InvariantCulture);
            return string.Join(VariableDelimiter, RecordingTag, typeStr, targetName, takeTimestamp, timeStr);
        }

        public static string AppendObject(string outputString, object recObj)
        {
            if (recObj == null) return string.Join(VariableDelimiter, outputString, "MISSING");
            
            switch (recObj.GetType().Name)
            {
                case "Vector3":
                {
                    var vector3Str = FormatVector3Components((Vector3)recObj);
                    return string.Join(VariableDelimiter, outputString, vector3Str);
                }
                case "Quaternion":
                {
                    var quaternionStr = FormatQuaternionComponents((Quaternion)recObj);
                    return string.Join(VariableDelimiter, outputString, quaternionStr);
                }
                default:
                    return string.Join(VariableDelimiter, outputString, recObj.ToString());
            }
        }
        
        public static string JoinComponents(params object[] components)
        {
            return string.Join(ComponentDelimiter, components);
        }

        public static int AnyHumrFrameStartIndex(string line)
        {
            return HumrFrameStartIndex(line) != -1 ? HumrFrameStartIndex(line) : LegacyHumrFrameStartIndex(line);
        }

        public static int HumrFrameStartIndex(string line)
        {
            var matchStr = $"{LogTagPrefix}{HumrTag} {RecordingTag}";
            var targetMatchIndex = line.IndexOf(matchStr, StringComparison.Ordinal);
            if (targetMatchIndex < 0) return -1;
            
            return targetMatchIndex + matchStr.Length + 1;
        }

        public static int LegacyHumrFrameStartIndex(string line)
        {
            var legacyMatchStr = string.Join("", LogTagPrefix, LegacyHumrTag);
            var targetMatchIndex = line.IndexOf(legacyMatchStr, StringComparison.Ordinal);
            if (targetMatchIndex < 0) return -1;

            return targetMatchIndex + legacyMatchStr.Length;
        }

        public static string SplitNextVariable(string line, out string remaining)
        {
            remaining = line;
            var delimiterIndex = line.IndexOf(VariableDelimiter, StringComparison.Ordinal);
            if (delimiterIndex == -1) return null;

            remaining = line.Substring(delimiterIndex + 1);
            return line.Substring(0, delimiterIndex);
        }

        public static string[] SplitLegacyLine(string line, string targetName)
        {
            var legacyMatchStr = string.Join("", LogTagPrefix, LegacyHumrTag);
            var legacyMatchTarget = string.Join("", legacyMatchStr, targetName);
            return line.Split(legacyMatchTarget);
        }

        public static string[] SplitLegacyFrame(string frame)
        {
            return frame.Split(ComponentDelimiter);
        }
        
        public static string JoinLogPrefixToFrame(string prefix, string frame)
        {
            return $"{prefix}-  {HumrTag} {frame}";
        }

        public static int TargetFrameStartIndex(string line, (TargetType targetType, string targetName) target)
        {
            var matchStr = $"{LogTagPrefix}{HumrTag} {RecordingTag}";
            var targetMatchStr = string.Join(VariableDelimiter, 
                matchStr, target.targetType, target.targetName, "");
            var targetMatchIndex = line.IndexOf(targetMatchStr, StringComparison.Ordinal);
            if (targetMatchIndex < 0) return -1;
            
            return targetMatchIndex + targetMatchStr.Length;
        }
        
        public static string[] SplitFrame(string frame)
        {
            return frame.Split(VariableDelimiter);
        }

        public static bool TryParseVector3(string vector3String, out Vector3 vector)
        {
            vector = default;
            var vector3Split = vector3String.Split(ComponentDelimiter);
            if (vector3Split.Length != 3) return false;

            if (!TryParseFloat(vector3Split[0], out var x)) return false;
            if (!TryParseFloat(vector3Split[1], out var y)) return false;
            if (!TryParseFloat(vector3Split[2], out var z)) return false;

            vector = new Vector3(x, y, z);
            return true;
        }

        public static bool TryParseQuaternion(string quaternionString, out Quaternion quaternion)
        {
            quaternion = default;
            var quaternionSplit = quaternionString.Split(ComponentDelimiter);
            if (quaternionSplit.Length != 4) return false;

            if (!TryParseFloat(quaternionSplit[0], out var x)) return false;
            if (!TryParseFloat(quaternionSplit[1], out var y)) return false;
            if (!TryParseFloat(quaternionSplit[2], out var z)) return false;
            if (!TryParseFloat(quaternionSplit[3], out var w)) return false;

            quaternion = new Quaternion(x, y, z, w);
            return true;
        }
        
        private static bool TryParseFloat(string floatString, out float floatValue)
        {
            floatValue = default;
            return float.TryParse(floatString, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue);
        }
    }
}