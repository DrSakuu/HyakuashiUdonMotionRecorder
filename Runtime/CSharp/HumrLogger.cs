using System;
using UnityEngine;

namespace HUMR
{
    public class HumrLogger
    {
        private const string HumrTag = "[HUMR]";
        public const string RecordingStarted = "Recording started";
        public const string RecordingStopped = "Recording stopped";

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
    }
}