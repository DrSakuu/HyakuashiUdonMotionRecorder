# Run on Windows: pwsh -File './Tools~/Test-LogAccessTime.ps1'
# Uses temporary fixtures only. Does not modify filesystem timestamp policy.
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This test requires Windows.'
}

if (-not ('Humr.Tools.LogAccessTimeTest' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Humr.Tools
{
    public static class LogAccessTimeTest
    {
        const int BufferSize = 64 * 1024;
        const string ModernMarker = "-  [HUMR] RECORDING";
        const string LegacyMarker = "-  HUMR:";

        [StructLayout(LayoutKind.Sequential)]
        struct FileTime { public uint Low; public uint High; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetFileTime(SafeFileHandle handle, IntPtr creation,
            ref FileTime access, IntPtr write);

        static StreamReader OpenSuppressed(string path)
        {
            // GENERIC_READ | FILE_WRITE_ATTRIBUTES; FILE_SHARE_READ | FILE_SHARE_WRITE;
            // OPEN_EXISTING; FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN.
            var handle = CreateFileW(path, 0x80000100, 3, IntPtr.Zero, 3, 0x08000080, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "CreateFileW failed for suppressed read: " + path);
            }
            try
            {
                var sentinel = new FileTime { Low = uint.MaxValue, High = uint.MaxValue };
                if (!SetFileTime(handle, IntPtr.Zero, ref sentinel, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetFileTime suppression failed: " + path);
                var stream = new FileStream(handle, FileAccess.Read, BufferSize, false);
                try { return new StreamReader(stream, Encoding.UTF8, true, BufferSize); }
                catch { stream.Dispose(); throw; }
            }
            catch { handle.Dispose(); throw; }
        }

        static bool Detect(StreamReader reader)
        {
            var buffer = new char[16 * 1024];
            int retained = 0;
            int read;
            int overlap = Math.Max(ModernMarker.Length, LegacyMarker.Length) - 1;
            while ((read = reader.Read(buffer, retained, buffer.Length - retained)) > 0)
            {
                int count = retained + read;
                // This harness favors compatibility over allocation tuning.
                string text = new string(buffer, 0, count);
                if (text.IndexOf(ModernMarker, StringComparison.Ordinal) >= 0 ||
                    text.IndexOf(LegacyMarker, StringComparison.Ordinal) >= 0) return true;
                retained = Math.Min(overlap, count);
                Array.Copy(buffer, count - retained, buffer, 0, retained);
            }
            return false;
        }

        static string Fixture(string root, string name, string marker)
        {
            string path = Path.Combine(root, name + ".txt");
            string text = "Unicode: ä漢字\r\n" + new string('x', 200000) + marker + "\r\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow.AddDays(-14));
            return path;
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void CheckDetection(string label, string path, bool expected, string contents)
        {
            DateTime access = File.GetLastAccessTimeUtc(path);
            DateTime creation = File.GetCreationTimeUtc(path);
            DateTime write = File.GetLastWriteTimeUtc(path);
            bool found;
            using (var reader = OpenSuppressed(path)) found = Detect(reader);
            DateTime afterDetection = File.GetLastAccessTimeUtc(path);
            Assert(found == expected, label + ": incorrect detection result");
            Assert(access == afterDetection, label + ": detection changed access time");
            // Content verification also uses a suppressed handle, avoiding contamination.
            using (var reader = OpenSuppressed(path))
                Assert(reader.ReadToEnd() == contents, label + ": contents changed");
            Assert(File.GetLastAccessTimeUtc(path) == access, label + ": verification changed access time");
            Assert(File.GetCreationTimeUtc(path) == creation, label + ": creation time changed");
            Assert(File.GetLastWriteTimeUtc(path) == write, label + ": last-write time changed");
            Console.WriteLine("PASS {0}: access before={1:O}, after={2:O}; detection={3}; metadata/content unchanged",
                label, access, afterDetection, found);
        }

        public static void Run(string root)
        {
            foreach (string marker in new[] { ModernMarker, LegacyMarker, "" })
            {
                string label = marker == "" ? "negative" : marker == ModernMarker ? "modern" : "legacy";
                string path = Fixture(root, label, marker);
                string expected = "Unicode: ä漢字\r\n" + new string('x', 200000) + marker + "\r\n";
                CheckDetection(label, path, marker != "", expected);
            }

            string shared = Fixture(root, "shared-writer", ModernMarker);
            using (var writer = new FileStream(shared, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                // Keep writing while a suppressed detection handle is open.
                using (var reader = OpenSuppressed(shared))
                {
                    byte[] appended = Encoding.UTF8.GetBytes("Writer append\r\n");
                    writer.Write(appended, 0, appended.Length);
                    writer.Flush();
                    Assert(Detect(reader), "Detection with active writer failed");
                }
                // A writer can legitimately change timestamps. Establish a baseline after its write,
                // then verify another detection pass while that writer handle remains open.
                File.SetLastAccessTimeUtc(shared, DateTime.UtcNow.AddDays(-14));
                string expected = "Unicode: ä漢字\r\n" + new string('x', 200000) + ModernMarker + "\r\nWriter append\r\n";
                CheckDetection("shared writer", shared, true, expected);
            }

            string control = Fixture(root, "normal-read", ModernMarker);
            DateTime before = File.GetLastAccessTimeUtc(control);
            using (var stream = new FileStream(control, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, BufferSize, FileOptions.SequentialScan))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize))
                reader.ReadToEnd();
            DateTime after = File.GetLastAccessTimeUtc(control);
            Console.WriteLine("{0} normal-read control: access before={1:O}, after={2:O}",
                before == after ? "INCONCLUSIVE" : "OBSERVED UPDATE", before, after);
            Console.WriteLine("Unchanged normal-read timestamps can reflect disabled or delayed filesystem updates.");
            Console.WriteLine("Suppression checks do not establish VRChat's cleanup policy or prevent access-time changes by other processes.");
        }
    }
}
'@
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('HUMR-LogAccessTime-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
try {
    [Humr.Tools.LogAccessTimeTest]::Run($testRoot)
}
finally {
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolvedRoot) -ne $tempParent -or
        -not [IO.Path]::GetFileName($resolvedRoot).StartsWith('HUMR-LogAccessTime-')) {
        throw "Unexpected fixture directory; refusing cleanup: $resolvedRoot"
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
