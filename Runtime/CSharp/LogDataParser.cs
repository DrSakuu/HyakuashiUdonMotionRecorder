using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HUMR
{
    [Serializable]
    public class Recording
    {
        public string target;
        public RecordingType type;
    }

    public class RecordingTake
    {
        public RecordingTake()
        {
            Frames = new List<RecordingFrame>();
        }

        public List<RecordingFrame> Frames { get; set; }
    }

    public class RecordingFrame
    {
        public float RecordTime { get; set; }
        public Vector3 HipPosition { get; set; }
        public List<Quaternion> BoneRotations { get; set; } = new List<Quaternion>();
    }

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

        public static Recording ParseRecordingEntry(string content)
        {
            var parts = content.Split(';');
            if (parts.Length < 3) return null;

            var typeStr = parts[1];
            if (!Enum.TryParse<RecordingType>(typeStr, true, out var type)) type = RecordingType.Unknown;

            return new Recording { type = type, target = parts[2] };
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

        public static List<RecordingTake> PartitionLogLinesIntoTakes(string[] lines, string targetDisplayName)
        {
            var takes = new List<RecordingTake>();
            var currentTake = new RecordingTake();

            var beforeTime = -1f;

            foreach (var line in lines)
            {
                if (!line.Contains(HumrRecordingLoader.LogMatchTarget)) continue;

                var tagIndex = line.IndexOf(HumrRecordingLoader.LogMatchTarget, StringComparison.Ordinal);
                var payload = line.Substring(tagIndex + HumrRecordingLoader.LogMatchTarget.Length);

                var parts = payload.Split(';');
                if (parts.Length < 4) continue;

                var rowDisplayName = parts[0];
                if (rowDisplayName != targetDisplayName) continue;

                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var currentTime)) continue;

                if (currentTime < beforeTime && currentTake.Frames.Count > 0)
                {
                    takes.Add(currentTake);
                    currentTake = new RecordingTake();
                }

                var frame = ParseMotionFrame(parts);
                if (frame == null) continue;

                currentTake.Frames.Add(frame);
                beforeTime = currentTime;
            }

            if (currentTake.Frames.Count > 0) takes.Add(currentTake);

            return takes;
        }

        private static RecordingFrame ParseMotionFrame(string[] parts)
        {
            var frame = new RecordingFrame
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

        public static List<RecordingTake> ParseLegacyTakes(List<string> logLines, string targetName)
        {
            var take = new List<RecordingTake>();
            var currentFrames = new List<RecordingFrame>();
            var lastTime = -1f;

            foreach (var line in logLines)
            {
                if (!TryParseLegacyFrame(line, HumrRecordingLoader.LegacyLogMatchTarget, targetName,
                        out var frame)) continue;

                HandleTakeBreak(frame, currentFrames, take, ref lastTime);

                currentFrames.Add(frame);
                lastTime = frame.RecordTime;
            }

            if (currentFrames.Count > 0) take.Add(new RecordingTake { Frames = currentFrames });

            return take;
        }

        private static bool TryParseLegacyFrame(string line, string matchTarget, string targetName,
            out RecordingFrame frame)
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
                frame = new RecordingFrame
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

        private static void ParseBoneRotations(string[] tokens, RecordingFrame frame)
        {
            for (var i = 4; i + 3 < tokens.Length; i += 4)
                frame.BoneRotations.Add(new Quaternion(
                    float.Parse(tokens[i], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 2], CultureInfo.InvariantCulture),
                    float.Parse(tokens[i + 3], CultureInfo.InvariantCulture)
                ));
        }

        private static void HandleTakeBreak(RecordingFrame frame, List<RecordingFrame> currentFrames,
            List<RecordingTake> takes, ref float lastTime)
        {
            if (lastTime < 0) return;

            var isRewind = frame.RecordTime < lastTime;
            var isGap = frame.RecordTime - lastTime > 1.0f;

            if (!isRewind && !isGap) return;
            if (currentFrames.Count <= 0) return;

            takes.Add(new RecordingTake { Frames = new List<RecordingFrame>(currentFrames) });
            currentFrames.Clear();
        }
    }
}