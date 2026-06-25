using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RoboSync
{
    public static class IoSampler
    {
        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;   // bytes read
            public ulong WriteTransferCount;  // bytes written
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS ioCounters);

        public static Tuple<double, double> SampleBytesAsync(Process proc, int intervalMs = 1000)
        {
            if (!GetProcessIoCounters(proc.Handle, out var start))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            Thread.Sleep(intervalMs);

            if (!GetProcessIoCounters(proc.Handle, out var end))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            ulong readDelta = end.ReadTransferCount - start.ReadTransferCount;
            ulong writeDelta = end.WriteTransferCount - start.WriteTransferCount;
            double seconds = intervalMs / 1000.0;

            return Tuple.Create(readDelta / seconds / 1024.0, writeDelta / seconds / 1024.0);
        }
    }

    public static class Dir
    {
        private static long size;

        public static long GetSize(string directory)
        {
            size = 0;
            return Bytes(directory);
        }

        private static long Bytes(string directory)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(directory))
                {
                    Bytes(dir);
                }

                foreach (FileInfo file in new DirectoryInfo(directory).GetFiles())
                {
                    size += file.Length;
                }
            }
            catch (Exception)
            {
            }
            return size;
        }
    }

    public static class NotifyIcon
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool Shell_NotifyIcon(int dwMessage, NOTIFYICONDATA lpData);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int NIM_ADD = 0x00000000;
        const int NIM_DELETE = 0x00000002;
        const int NIF_MESSAGE = 0x00000001;
        const int NIF_ICON = 0x00000002;
        const int NIF_TIP = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        public static void Create(IntPtr hWnd)
        {
            Icon icon = new Icon(new MemoryStream(Properties.Resources.RoboSync));

            NOTIFYICONDATA nid = new NOTIFYICONDATA();
            nid.cbSize = Marshal.SizeOf(nid);
            nid.hWnd = hWnd;
            nid.uID = 1;
            nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            nid.uCallbackMessage = 0x8000;
            nid.hIcon = icon.Handle;
            nid.szTip = "";

            Shell_NotifyIcon(NIM_ADD, nid);
        }

        public static void Delete()
        {
            Icon icon = new Icon(new MemoryStream(Properties.Resources.RoboSync));

            NOTIFYICONDATA nid = new NOTIFYICONDATA();
            nid.cbSize = Marshal.SizeOf(nid);
            nid.hWnd = GetConsoleWindow();
            nid.uID = 1;
            nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            nid.uCallbackMessage = 0x8000;
            nid.hIcon = icon.Handle;
            nid.szTip = "";

            Shell_NotifyIcon(NIM_DELETE, nid);
        }
    }
}
