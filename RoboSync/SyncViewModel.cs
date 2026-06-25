using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;

namespace RoboSync
{
    internal class SyncViewModel : INotifyPropertyChanged
    {
        private SyncModel syncModel;
        private ObservableCollection<Definition> Definitions;

        private List<Tuple<string, string, string>> commands = new List<Tuple<string, string, string>>();

        private Definition? selectedDefinition;
        public Definition? SelectedDefinition
        {
            get { return selectedDefinition; }
            set { selectedDefinition = value; OnPropertyChanged(); }
        }

        public int Status
        {
            get { return syncModel.Status; }
        }

        public int Progress1
        {
            get { return syncModel.Progress1; }
        }

        public int Progress2
        {
            get { return syncModel.Progress2; }
        }

        public string LogLine
        {
            get { return syncModel.LogLine; }
        }

        public string ReadBytes
        {
            get { return syncModel.ReadBytes; }
        }

        public string WriteBytes
        {
            get { return syncModel.WriteBytes; }
        }

        public string ProgressTime
        {
            get { return syncModel.ProgressTime; }
        }

        private string _logText;
        public string LogText
        {
            get { return _logText; }
            set { _logText = value; OnPropertyChanged(); }
        }

        public SyncViewModel(ObservableCollection<Definition> _Definitions)
        {
            Definitions = _Definitions;
        }

        private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        // Default INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal void Initialise()
        {
            syncModel = new SyncModel();
            syncModel.PropertyChanged += ModelPropertyChanged;
            syncModel.Initialise();

            LogText = "Welcome to RoboSync - A simple backup file sync program\n\n" +
                "A sync is a copy that keeps an exact copy, adding new or modified files\n" +
                "It also deletes synced copies when they are no longer in the source\n" +
                "We use ROBOCOPY that is a very efficient Windows file copy method\n\n" +
                "Step 1:\nBrowse to set an Output Folder location\n" +
                "This will usually be a folder on an attached USB drive\n" +
                "Ensure this location has sufficient space and doesn't contain other files\n" +
                "(that could be modified or deleted)\n\n" +
                "Step 2:\nUse the table Browse buttons to add source files to the table\n" +
                "A size calculation is performed when a locaton is entered with Browse\n" +
                "Locations may be de-selected using the Include tickbox\n" +
                "Delete a location by selecting the table row and pressing the Delete key\n\n" +
                "Step 3:\nStart the sync - the first sync will take the longest\n" +
                "Subsequent syncs will only modify updated files\n" +
                "Check the progress report in this window for errors\n" +
                "Also check the Output Folder files after the first run to be certain\n\n" +
                "Multiple definitions may be used to sync different sets of folders\n" +
                "Progress calculations are approximate to keep performance optimal\n" +
                "The ROBOCOPY commands may be exported to clipboard for use directly\n" +
                "Recommend closing other applications first - locked files are not copied";

            string definitions = Properties.Settings.Default.Definitions;
            string[] definitionArray = definitions.Split('#', StringSplitOptions.RemoveEmptyEntries);

            foreach (string definitionString in definitionArray)
            {
                string[] parts = definitionString.Split('@');
                if (parts.Length == 3)
                {
                    Definition definition = new Definition() { Label = parts[0], Output = parts[1] };
                    string[] folderStrings = parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string folderString in folderStrings)
                    {
                        string[] folderParts = folderString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (folderParts.Length == 3)
                        {
                            long size = 0;
                            long.TryParse(folderParts[2], out size);
                            definition.Folders.Add(new Folder()
                            {
                                Path = folderParts[0],
                                Include = bool.Parse(folderParts[1]),
                                Size = size
                            });
                        }
                    }
                    Definitions.Add(definition);
                }
            }
            if (Definitions.Count == 0)
            {
                Definitions.Add(new Definition() { Label = "Default" });
            }

            SelectedDefinition = Definitions[0];
        }

        internal void SaveDefinitions()
        {
            string definitions = "";
            foreach (Definition definition in Definitions)
            {
                if (definition.Folders.Count == 0) continue;
                if (definition.Label == string.Empty)
                {
                    definition.Label = "Default Definition";
                }
                definitions += definition.Label + "@";
                definitions += definition.Output + "@";
                foreach (Folder folder in definition.Folders)
                {
                    definitions += folder.Path + "," + folder.Include.ToString() + "," + folder.Size.ToString() + ";";
                }
                definitions += "#";
            }
            Properties.Settings.Default.Definitions = definitions;
            Properties.Settings.Default.Save();
        }

        internal void DoSync(bool bAll = false)
        {
            if (null == SelectedDefinition) return;
            GetCommands(bAll);
            LogText = "";
            syncModel.DoSync(commands);
        }

        internal void GetCommands(bool bAll = false)
        {
            if (null == SelectedDefinition) return;
            string flags = ""; // "/M "
            flags += "/MIR /J /XJ /MT:" + Environment.ProcessorCount + " /R:0 /W:0 /NDL /NFL /NS /NC /NP";
            commands.Clear();
            if (bAll)
            {
                foreach (var definition in Definitions)
                {
                    foreach (var folder in definition.Folders)
                    {
                        var output = definition.Output + folder.Path.Split(':').Last();
                        if (folder.Include)
                        {
                            commands.Add(Tuple.Create(folder.Path, output, "*.* " + flags));
                        }
                    }
                }
            }
            else
            {
                foreach (var folder in SelectedDefinition.Folders)
                {
                    var output = SelectedDefinition.Output + folder.Path.Split(':').Last();
                    if (folder.Include)
                    {
                        commands.Add(Tuple.Create(folder.Path, output, "*.* " + flags));
                    }
                }
            }
        }

        internal void BatchCommands()
        {
            GetCommands(true);
            string text = "";
            foreach (var command in commands)
            {
                text += "ROBOCOPY \"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3 + "\n";
            }
            Clipboard.Clear();
            Clipboard.SetText(text);
        }

        internal void AbortSync()
        {
            syncModel.AbortSync();
        }

        internal void EndSync()
        {
            syncModel.EndSync();
        }
    }

    public class Folder
    {
        public string Path { get; set; }
        public bool Include { get; set; }
        public long Size { get; set; }

        public Folder()
        {
            Path = string.Empty;
            Include = true;
            Size = 0;
        }
    }

    public class Definition
    {
        public ObservableCollection<Folder> Folders { get; set; }
        public string Label { get; set; }
        public string Output { get; set; }

        public Definition()
        {
            Folders = new ObservableCollection<Folder>();
            Label = string.Empty;
            Output = string.Empty;
        }
    }

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

    public static class IoSampler
    {
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
            catch (Exception ex)
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
