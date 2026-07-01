using UnityEngine;

namespace HUMR
{
    public enum RecordingType
    {
        Object,
        Player
    }

    public class RecorderUtils
    {
        private const string HumrTag = "[HUMR]";
        public const string RecordingStarted = "Recording started";
        public const string RecordingStopped = "Recording stopped";

        public static string RecTypeToString(RecordingType recordingType)
        {
            switch (recordingType)
            {
                case RecordingType.Object:
                    return "Object";
                case RecordingType.Player:
                    return "Player";
                default:
                    return "UNKNOWN";
            }
        }

        public static void HumrLog(object message)
        {
            Debug.Log($"{HumrTag} {message}");
        }

        public static void HumrWarning(object message)
        {
            Debug.LogWarning($"{HumrTag} {message}");
        }

        public static void HumrError(object message)
        {
            Debug.LogError($"{HumrTag} {message}");
        }

        public static void HumrAssertion(object message)
        {
            Debug.LogAssertion($"{HumrTag} {message}");
        }
    }
}