using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HUMR
{
public static class LogDataParser
    {
        public static string ExtractLegacyDisplayName(string line, string matchTarget)
        {
            var prefixIdx = line.IndexOf(matchTarget, StringComparison.Ordinal);
            if (prefixIdx == -1) return null;

            var dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();

            var digitIdx = FindFirstDigitIndex(dataSegment);
            return digitIdx == -1 ? null : dataSegment.Substring(0, digitIdx);
        }

        private static int FindFirstDigitIndex(string text)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsDigit(text[i]))
                    return i;

            return -1;
        }

        public static LogEntry ParseLogEntry(string content)
        {
            var parts = content.Split(';');
            if (parts.Length < 3) return null;

            var typeStr = parts[1];
            if (!Enum.TryParse<RecordingType>(typeStr, true, out var type)) type = RecordingType.Object;

            return new LogEntry { type = type, name = parts[2] };
        }

        public static List<string> LoadLogFileLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                while (0 <= sr.Peek()) lines.Add(sr.ReadLine());
            }

            return lines;
        }

        public static List<MotionSegment> PartitionLogLinesIntoSegments(string[] lines, string targetDisplayName)
        {
            var segments = new List<MotionSegment>();
            var currentSegment = new MotionSegment();

            var beforeTime = -1f;

            foreach (var line in lines)
            {
                if (!line.Contains(PlayerRecordingLoader.LogMatchTarget)) continue;

                var tagIndex = line.IndexOf(PlayerRecordingLoader.LogMatchTarget, StringComparison.Ordinal);
                var payload = line.Substring(tagIndex + PlayerRecordingLoader.LogMatchTarget.Length);

                var parts = payload.Split(';');
                if (parts.Length < 4) continue;

                var rowDisplayName = parts[0];
                if (rowDisplayName != targetDisplayName) continue;

                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var currentTime)) continue;

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

            if (currentSegment.Frames.Count > 0) segments.Add(currentSegment);

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
                frame.HipPosition = new Vector3(
                    float.Parse(posValues[0], CultureInfo.InvariantCulture),
                    float.Parse(posValues[1], CultureInfo.InvariantCulture),
                    float.Parse(posValues[2], CultureInfo.InvariantCulture)
                );

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

        public static List<MotionSegment> ParseLegacySegments(List<string> logLines, string targetName)
        {
            var segments = new List<MotionSegment>();
            var currentFrames = new List<MotionFrame>();
            var lastTime = -1f;

            foreach (var line in logLines)
            {
                if (!TryParseLegacyFrame(line, PlayerRecordingLoader.LegacyLogMatchTarget, targetName, out var frame)) continue;

                HandleSegmentBreak(frame, currentFrames, segments, ref lastTime);

                currentFrames.Add(frame);
                lastTime = frame.RecordTime;
            }

            if (currentFrames.Count > 0) segments.Add(new MotionSegment { Frames = currentFrames });

            return segments;
        }

        private static bool TryParseLegacyFrame(string line, string matchTarget, string targetName, out MotionFrame frame)
        {
            frame = null;
            var prefixIdx = line.IndexOf(matchTarget, StringComparison.Ordinal);
            if (prefixIdx == -1) return false;

            var dataSegment = line.Substring(prefixIdx + matchTarget.Length).Trim();
            if (!dataSegment.StartsWith(targetName)) return false;

            var numericDataRaw = dataSegment.Substring(targetName.Length);
            var tokens = numericDataRaw.Split(',');
            if (tokens.Length < 4) return false;

            try
            {
                frame = new MotionFrame
                {
                    RecordTime = float.Parse(tokens[0], CultureInfo.InvariantCulture),
                    HipPosition = new Vector3(
                        float.Parse(tokens[1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[3], CultureInfo.InvariantCulture)
                    ),
                    BoneRotations = new List<Quaternion>()
                };

                ParseBoneRotations(tokens, frame);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to interpret legacy sequential data array line: {ex.Message}");
                return false;
            }
        }

        private static void ParseBoneRotations(string[] tokens, MotionFrame frame)
        {
            for (var i = 4; i + 3 < tokens.Length; i += 4)
                frame.BoneRotations.Add(new Quaternion(
                    float.Parse(tokens[i], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 2], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 3], CultureInfo.InvariantCulture)
                ));
        }

        private static void HandleSegmentBreak(MotionFrame frame, List<MotionFrame> currentFrames, List<MotionSegment> segments, ref float lastTime)
        {
            if (lastTime < 0) return;
            
            var isRewind = frame.RecordTime < lastTime;
            var isGap = frame.RecordTime - lastTime > 1.0f;
            
            if (!isRewind && !isGap) return;
            if (currentFrames.Count <= 0) return;

            segments.Add(new MotionSegment { Frames = new List<MotionFrame>(currentFrames) });
            currentFrames.Clear();
        }
    }
}