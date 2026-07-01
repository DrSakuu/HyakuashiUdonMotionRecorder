using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HUMR
{
    public enum LogFileType
    {
        Standard,
        Legacy
    }
    
    public enum RecordingType
    {
        Object,
        Player
    }
    
    public class MotionFrame
    {
        public float RecordTime { get; set; }
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new List<Quaternion>();
    }

    public class MotionSegment
    {
        public List<MotionFrame> Frames { get; set; } = new List<MotionFrame>();
    }
    
    public class HumrUtils
    {
        public const string HumrTag = "[HUMR]";
        public const string LogMatchTarget = "-  [HUMR] ";
        public const string LegacyLogMatchTarget = "-  HUMR:";
        public const string RecordingStarted = "Recording started";
        public const string RecordingStopped = "Recording stopped";
        public const string HumrPath = "Assets/HUMR";

        public static string GetHierarchyPath(Transform self)
        {
            var path = self.gameObject.name;
            var parent = self.parent;
            while (parent.parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
        
        public static string[] GetLogFiles(string logFilePathString)
        {
            if (string.IsNullOrEmpty(logFilePathString) || !Directory.Exists(logFilePathString)) return null;
            return Directory.GetFiles(logFilePathString, "*.txt");
        }
        
        public static List<MotionSegment> PartitionLogLinesIntoSegments(string[] lines, string targetDisplayName)
        {
            var segments = new List<MotionSegment>();
            var currentSegment = new MotionSegment();
    
            var beforeTime = -1f;

            foreach (var line in lines)
            {
                if (!line.Contains(LogMatchTarget)) continue;

                var tagIndex = line.IndexOf(LogMatchTarget, StringComparison.Ordinal);
                var payload = line.Substring(tagIndex + LogMatchTarget.Length);

                var parts = payload.Split(';');
                if (parts.Length < 4) continue; 

                var rowDisplayName = parts[0];
                if (rowDisplayName != targetDisplayName) continue;

                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var currentTime)) continue;

                // Detect a timestamp rewind -> close old recording block context, start a new segment
                if (currentTime < beforeTime && currentSegment.Frames.Count > 0)
                {
                    segments.Add(currentSegment);
                    currentSegment = new MotionSegment();
                }

                var frame = ParseMotionFrame(parts);
                if (frame == null) continue;
                
                currentSegment.Frames.Add(frame);
                beforeTime = currentTime;
            }

            if (currentSegment.Frames.Count > 0)
            {
                segments.Add(currentSegment);
            }

            return segments;
        }

        private static MotionFrame ParseMotionFrame(string[] parts)
        {
            var frame = new MotionFrame
            {
                RecordTime = float.Parse(parts[1], CultureInfo.InvariantCulture)
            };

            var posValues = parts[2].Split(',');
            if (posValues.Length == 3)
            {
                frame.HipPosition = new Vector3(
                    float.Parse(posValues[0], CultureInfo.InvariantCulture),
                    float.Parse(posValues[1], CultureInfo.InvariantCulture),
                    float.Parse(posValues[2], CultureInfo.InvariantCulture)
                );
            }

            for (var i = 3; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;

                var rotValues = parts[i].Split(',');
                if (rotValues.Length != 4) continue;
                var rotation = new Quaternion(
                    float.Parse(rotValues[0], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[1], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[2], CultureInfo.InvariantCulture),
                    float.Parse(rotValues[3], CultureInfo.InvariantCulture)
                );
                frame.BoneRotations.Add(rotation);
            }

            return frame;
        }
        
        public static string GetBaseAnimationName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            // Looks for (YYYY-MM-DD_HH-MM-SS)
            var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}");
    
            return match.Success ? match.Value : fileName;
        }

        public static string SanitizeFileName(string input)
        {
            var sanitized = input;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }

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

        internal static bool TryParseLegacyLine(string line, out string displayName, out MotionFrame frame)
        {
            displayName = string.Empty;
            frame = null;

            const string targetMatch = "-  HUMR:";
            var prefixIdx = line.IndexOf(targetMatch, StringComparison.Ordinal);
            if (prefixIdx == -1) return false;

            var dataSegment = line.Substring(prefixIdx + targetMatch.Length).Trim();

            // Separate DisplayName using "0." as the separator
            var separatorIdx = dataSegment.IndexOf("0.", StringComparison.Ordinal);
            if (separatorIdx == -1) return false;

            displayName = dataSegment.Substring(0, separatorIdx);
            var numericDataRaw = dataSegment.Substring(separatorIdx);

            var tokens = numericDataRaw.Split(',');
            if (tokens.Length < 4) return false; // Must have at least Time + HipPosition

            try
            {
                var parsedFrame = new MotionFrame
                {
                    RecordTime = float.Parse(tokens[0], CultureInfo.InvariantCulture),
                    HipPosition = new Vector3(
                        float.Parse(tokens[1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[3], CultureInfo.InvariantCulture)
                    ),
                    BoneRotations = new List<Quaternion>()
                };

                // Every 4 elements after index 4 represents a sequential bone quaternion (x, y, z, w)
                for (var i = 4; i + 3 < tokens.Length; i += 4)
                {
                    var boneRot = new Quaternion(
                        float.Parse(tokens[i], CultureInfo.InvariantCulture),
                        float.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[i + 2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[i + 3], CultureInfo.InvariantCulture)
                    );
                    parsedFrame.BoneRotations.Add(boneRot);
                }

                frame = parsedFrame;
                return true;
            }
            catch (Exception ex)
            {
                HumrError($"Failed to parse legacy numeric data block: {ex.Message}");
                return false;
            }
        }

        
        
        internal static List<MotionSegment> ParseLegacySegments(List<string> logLines, string targetName)
        {
            var segments = new List<MotionSegment>();
            var currentFrames = new List<MotionFrame>();
            float lastTime = -1f;

            foreach (var line in logLines)
            {
                int prefixIdx = line.IndexOf("HUMR:", StringComparison.Ordinal);
                if (prefixIdx == -1) continue;

                string dataSegment = line.Substring(prefixIdx + 5).Trim();
                int separatorIdx = dataSegment.IndexOf("0.", StringComparison.Ordinal);
                if (separatorIdx == -1) continue;

                string displayName = dataSegment.Substring(0, separatorIdx);
                if (displayName != targetName) continue;

                // Extract and isolate flat token parameters
                string numericDataRaw = dataSegment.Substring(separatorIdx);
                string[] tokens = numericDataRaw.Split(',');
                if (tokens.Length < 4) continue;

                try
                {
                    var frame = new MotionFrame
                    {
                        RecordTime = float.Parse(tokens[0]),
                        HipPosition = new Vector3(
                            float.Parse(tokens[1]),
                            float.Parse(tokens[2]),
                            float.Parse(tokens[3])
                        ),
                        BoneRotations = new List<Quaternion>()
                    };

                    for (int i = 4; i + 3 < tokens.Length; i += 4)
                    {
                        frame.BoneRotations.Add(new Quaternion(
                            float.Parse(tokens[i]),
                            float.Parse(tokens[i + 1]),
                            float.Parse(tokens[i + 2]),
                            float.Parse(tokens[i + 3])
                        ));
                    }

                    if (lastTime >= 0 && (frame.RecordTime < lastTime || frame.RecordTime - lastTime > 1.0f))
                    {
                        if (currentFrames.Count > 0)
                        {
                            segments.Add(new MotionSegment { Frames = new List<MotionFrame>(currentFrames) });
                            currentFrames.Clear();
                        }
                    }

                    currentFrames.Add(frame);
                    lastTime = frame.RecordTime;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to interpret legacy numerical string block data: {ex.Message}");
                }
            }

            if (currentFrames.Count > 0)
            {
                segments.Add(new MotionSegment { Frames = currentFrames });
            }

            return segments;
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
