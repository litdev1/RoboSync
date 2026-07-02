using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

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

        public static Tuple<double, double, double, double> SampleBytesAsync(Process proc, int intervalMs = 1000)
        {
            if (!GetProcessIoCounters(proc.Handle, out var start))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            Thread.Sleep(intervalMs);

            if (!GetProcessIoCounters(proc.Handle, out var end))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            ulong readDelta = end.ReadTransferCount - start.ReadTransferCount;
            ulong writeDelta = end.WriteTransferCount - start.WriteTransferCount;
            double seconds = intervalMs / 1000.0;

            return Tuple.Create(readDelta / seconds / 1024.0, writeDelta / seconds / 1024.0,
                end.ReadTransferCount / 1024.0 / 1024.0, end.WriteTransferCount / 1024.0 / 1024.0);
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

    public class NotifyIcon
    {
        private Window window;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool Shell_NotifyIcon(int dwMessage, NOTIFYICONDATA lpData);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        const int NIM_ADD = 0x00000000;
        const int NIM_DELETE = 0x00000002;
        const int NIF_MESSAGE = 0x00000001;
        const int NIF_ICON = 0x00000002;
        const int NIF_TIP = 0x00000004;

        const int WM_USER = 0x0400;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONUP = 0x0205;

        const int uCallbackMessage = WM_USER + 1;

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

        public NotifyIcon(Window _window)
        {
            window = _window;
        }

        public void Create(IntPtr hWnd)
        {
            Icon icon = new Icon(new MemoryStream(Properties.Resources.RoboSync));

            NOTIFYICONDATA nid = new NOTIFYICONDATA();
            nid.cbSize = Marshal.SizeOf(nid);
            nid.hWnd = hWnd;
            nid.uID = 1;
            nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            nid.uCallbackMessage = uCallbackMessage;
            nid.hIcon = icon.Handle;
            nid.szTip = "RoboSync backup tool";

            Shell_NotifyIcon(NIM_ADD, nid);

            HwndSource source = HwndSource.FromHwnd(hWnd);
            source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case uCallbackMessage:
                    switch (lParam.ToInt32())
                    {
                        case WM_LBUTTONUP:
                            if (window.Visibility == Visibility.Hidden)
                            {
                                window.Show();
                                window.WindowState = WindowState.Normal;
                            }
                            else
                            {
                                window.WindowState = WindowState.Minimized;
                            }
                            break;
                        case WM_RBUTTONUP:
                            Icon icon = new Icon(new MemoryStream(Properties.Resources.RoboSync));
                            if (MessageBox.Show(window, "Exit RoboSync", "RoboSync", MessageBoxButton.YesNo, MessageBoxImage.Stop) == MessageBoxResult.Yes)
                            {
                                App.CanClose = true;
                                window.Close();
                            }
                            break;
                    }
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }
    }

    public static class Utilities
    {
        public static void AppSettings()
        {
            var module = Process.GetCurrentProcess().MainModule;
            if (null != module)
            {
                string key = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
                RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(key, true);
                string? assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                string? executableName = "\"" + module.FileName + "\"";
                //var keyValue = registryKey.GetValue(assemblyName);
                if (null != assemblyName)
                {
                    if (App.IsStartup)
                    {
                        registryKey?.SetValue(assemblyName, executableName);
                    }
                    else
                    {
                        registryKey?.DeleteValue(assemblyName, false);
                    }
                }
            }
        }
    }
}
