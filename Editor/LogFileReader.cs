using System;
using System.IO;
using System.Text;
#if UNITY_EDITOR_WIN
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEngine;
#endif

namespace DrSakuu.Humr.Editor
{
    internal static class LogFileReader
    {
        private const int BufferSize = 64 * 1024;

        internal static StreamReader Open(string path, bool preserveLastAccessTime = false)
        {
#if UNITY_EDITOR_WIN
            if (preserveLastAccessTime)
            {
                try { return OpenSuppressed(path); }
                catch (Exception exception) when (exception is Win32Exception or IOException or
                    UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
                {
                    var warningKey = "DrSakuu.Humr.AccessTimeWarning." + Path.GetFullPath(path).ToUpperInvariant();
                    if (!SessionState.GetBool(warningKey, false))
                    {
                        SessionState.SetBool(warningKey, true);
                        Debug.LogWarning($"HUMR could not preserve last-access time for '{path}': {exception.Message} " +
                            "Falling back to ordinary reading; detection may update last-access time.");
                    }
                }
            }
#endif
            return CreateReader(new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, BufferSize, FileOptions.SequentialScan));
        }

        private static StreamReader CreateReader(FileStream stream)
        {
            try { return new StreamReader(stream, Encoding.UTF8, true, BufferSize); }
            catch { stream.Dispose(); throw; }
        }

#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileTime(SafeFileHandle handle, IntPtr creation,
            ref FileTime access, IntPtr write);

        private static StreamReader OpenSuppressed(string path)
        {
            const uint genericRead = 0x80000000;
            const uint writeAttributes = 0x100;
            const uint openExisting = 3;
            const uint normalAttributes = 0x80;
            const uint sequentialScan = 0x08000000;
            var handle = CreateFileW(path, genericRead | writeAttributes, (uint)FileShare.ReadWrite,
                IntPtr.Zero, openExisting, normalAttributes | sequentialScan, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "CreateFileW failed");
            }

            try
            {
                var sentinel = new FileTime { Low = uint.MaxValue, High = uint.MaxValue };
                if (!SetFileTime(handle, IntPtr.Zero, ref sentinel, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetFileTime suppression failed");

                // The FileStream owns the handle after construction; the reader owns the stream.
                return CreateReader(new FileStream(handle, FileAccess.Read, BufferSize, false));
            }
            catch { handle.Dispose(); throw; }
        }
#endif
    }
}
