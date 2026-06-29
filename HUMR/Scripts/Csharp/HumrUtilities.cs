using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HUMR
{
    public class HumrUtilities : MonoBehaviour
    {

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
            const string matchTarget = "[HUMR] ";

            foreach (var line in lines)
            {
                if (!line.Contains(matchTarget)) continue;

                var tagIndex = line.IndexOf(matchTarget);
                var payload = line.Substring(tagIndex + matchTarget.Length);

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
        
        public static MotionFrame ParseMotionFrame(string[] parts)
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

            for (var i = 4; i < parts.Length; i++)
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
    }
}
