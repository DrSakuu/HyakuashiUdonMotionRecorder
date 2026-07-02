using System;
using UnityEngine;

namespace HUMR
{
    public class HumrLogger
    {
        private const string HumrTag = "[HUMR]";
        public const string RecordingTag = "RECORDING";
        public const char VariableDelimiter = ';';
        public const char ComponentDelimiter = ',';

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

        public static string FormatVector3Components(Vector3 vector3)
        {
            var trimmedVector3 = vector3.ToString().Trim('(', ')');
            return trimmedVector3.Replace(" ", "");
        }

        public static string FormatQuaternionComponents(Quaternion quaternion)
        {
            var trimmedQuaternion = quaternion.ToString().Trim('(', ')');
            return trimmedQuaternion.Replace(" ", "");
        }

        public static string RecordingTypeToString(FrameType frameType)
        {
            switch (frameType)
            {
                case FrameType.Legacy:
                    return "Legacy";
                case FrameType.BoneRotations:
                    return "BoneRotations";
                case FrameType.Object:
                    return "Object";
                case FrameType.Unknown:
                default:
                    return "Unknown";
            }
        }
    }
}