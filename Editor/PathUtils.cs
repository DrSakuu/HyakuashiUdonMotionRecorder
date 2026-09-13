using System.IO;
using System.Text.RegularExpressions;

namespace DrSakuu.Humr.Editor
{
    public static class PathUtils
    {
        public static void CreateDirectoryIfNotExist(string path)
        {
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
        }

        public static string SanitizeFileName(string input)
        {
            var sanitized = input;
            foreach (var c in Path.GetInvalidFileNameChars()) sanitized = sanitized.Replace(c, '_');
            return sanitized;
        }

        public static string GetDateTimeFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}");

            return match.Success ? match.Value : fileName;
        }

        public static int FindFirstDigitIndex(string text)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsDigit(text[i]))
                    return i;

            return -1;
        }

        public static string BuildAnimationName(RecordingTake take, string filePath)
        {
            var logTimestamp = PathUtils.GetDateTimeFromFileName(filePath);
            var targetName = take.targetName;
            var takeStamp = take.takeTimestamp;
            var takeName = take.takeName;
            var animationName = string.Join('_', targetName, logTimestamp, takeStamp, takeName);
            return animationName;
        }
    }
}