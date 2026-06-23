using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timer = System.Timers.Timer;
using System.Runtime.InteropServices;

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

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Definition> Definitions = new ObservableCollection<Definition>();

        private BackgroundWorker? worker;
        private Definition? selectedDefinition = null;
        private Process? process = null;
        private List<Tuple<string, string, string>> commands = new List<Tuple<string, string, string>>();
        private Tuple<string, string, string>? command = null;
        private long size = 0;
        private long inSize = 0;
        private long outSize = 0;
        private long numError = 0;

        public MainWindow()
        {
            InitializeComponent();

            LogTextBox.Text = "Welcome to RoboSync - A simple backup file sync program\n\n" +
                "A sync is a copy that keeps an exact copy, adding new or modified files\n" +
                "It also deletes synced copies when they are no longer present in the source\n" +
                "We use ROBOCOPY that is a very efficient Windows file copy method\n\n" +
                "Step 1:\nBrowse to set an Output Folder location\n" +
                "This will usually be a folder on an attached USB drive\n" +
                "Ensure this location has sufficient space\n" +
                "Ensure this location does not have other files present\n\n" +
                "Step 2:\nUse table Browse buttons to add source files to the table\n" +
                "Size calculation is performed when a locaton is entered with Browse\n" +
                "Locations may be deselected using Include tickbox\n" +
                "Delete a location by selecting the table row and pressing delete key\n\n" +
                "Step 3:\nStart the sync - the first sync will take the longest\n" +
                "Subsequent syncs will only modify changed files\n" +
                "Check the progress report in this window for errors\n" +
                "Also check the Output Folder files after the first run to be certain\n\n" +
                "Multiple definitions may be used to sync different sets of folders\n" +
                "Progress calculations are approximate to keep performance optimal\n" +
                "The ROBOCOPY commands may be exported to clipboard for use directly\n" +
                "Recommend closing other applications first - locked files are not copied";

            Properties.Settings.Default.Reload();
            if (Properties.Settings.Default.WinState > 0) WindowState = (WindowState)Properties.Settings.Default.WinState;
            if (Properties.Settings.Default.WinTop > 0) Top = Properties.Settings.Default.WinTop;
            if (Properties.Settings.Default.WinLeft > 0) Left = Properties.Settings.Default.WinLeft;
            if (Properties.Settings.Default.WinWidth > 0) Width = Properties.Settings.Default.WinWidth;
            if (Properties.Settings.Default.WinHeight > 0) Height = Properties.Settings.Default.WinHeight;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
            string definitions = Properties.Settings.Default.Definitions;
            string[] definitionArray = definitions.Split('#', StringSplitOptions.RemoveEmptyEntries);

            Definitions.Clear();
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
            DefinitionsListBox.ItemsSource = Definitions;
            selectedDefinition = Definitions[0];
            DefinitionsListBox.SelectedItem = selectedDefinition;
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
            UpdateStatus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
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
            Properties.Settings.Default.WinState = WindowState == WindowState.Minimized ? (int)WindowState.Normal : (int)WindowState;
            Properties.Settings.Default.WinTop = Top;
            Properties.Settings.Default.WinLeft = Left;
            Properties.Settings.Default.WinWidth = Width;
            Properties.Settings.Default.WinHeight = Height;

            Properties.Settings.Default.Save();
            EndSync();
        }

        private void OnOutputBrowse(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new();

            dialog.Multiselect = false;
            dialog.Title = "Select a folder";
            dialog.InitialDirectory = OutputTextBox.Text;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                OutputTextBox.Text = dialog.FolderName;
            }
        }

        private void OnFolderBrowse(object sender, RoutedEventArgs e)
        {
            if (null == selectedDefinition) return;
            Button btn = (Button)sender;
            Folder folder;
            if (btn.DataContext.GetType() == typeof(Folder))
            {
                folder = (Folder)btn.DataContext;
            }
            else
            {
                folder = new Folder{ Include = true };
            }
            Microsoft.Win32.OpenFolderDialog dialog = new();

            dialog.Multiselect = false;
            dialog.Title = "Select a folder";
            dialog.InitialDirectory = folder.Path;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                if (folder.Path == string.Empty)
                {
                    selectedDefinition.Folders.Add(folder);
                }
                folder.Path = dialog.FolderName;
                folder.Size = GetDirectorySize(folder.Path);
            }
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
        }

        private void DefinitionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Cursor = Cursors.Wait;
            ListBox listBox = (ListBox)sender;
            selectedDefinition = (Definition)listBox.SelectedItem;
            if (null == selectedDefinition) return;
            foreach (var folder in selectedDefinition.Folders)
            {
                size = 0;
                folder.Size = GetDirectorySize(folder.Path);
            }
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
            DefinitionLabel.Content = selectedDefinition.Label;
            OutputTextBox.Text = selectedDefinition.Output;
            Cursor = null;
        }

        private void Button_AddClick(object sender, RoutedEventArgs e)
        {
            Definitions.Add(new Definition() { Label = "Definition " + (Definitions.Count + 1) });
            DefinitionsListBox.ItemsSource = Definitions;
            selectedDefinition = Definitions.Last();
            DefinitionsListBox.SelectedItem = selectedDefinition;
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
        }

        private void Button_DeleteClick(object sender, RoutedEventArgs e)
        {
            if (null == selectedDefinition) return;
            var index = Definitions.IndexOf(selectedDefinition) - 1;
            Definitions.Remove(selectedDefinition);
            if (index < 0)
            {
                index = 0;
                Definitions.Add(new Definition() { Label = "Default Definition" });
            }
            DefinitionsListBox.ItemsSource = Definitions;
            selectedDefinition = Definitions[index];
            DefinitionsListBox.SelectedItem = selectedDefinition;
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == selectedDefinition) return;
            selectedDefinition.Output = OutputTextBox.Text;
        }

        private void Button_FullSyncClick(object sender, RoutedEventArgs e)
        {
            DoSync();
        }

        private void Button_AllSyncClick(object sender, RoutedEventArgs e)
        {
            DoSync(true);
        }

        private void DoSync(bool bAll = false)
        {
            if (null == selectedDefinition) return;
            GetCommands(bAll);

            worker = new BackgroundWorker();
            worker.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCompleted);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();
        }

        private void GetCommands(bool bAll = false)
        {
            if (null == selectedDefinition) return;
            string flags = ""; // "/M "
            flags += "/MIR /J /XJ /MT:" + Environment.ProcessorCount + " /R:0 /W:0 /NDL /NFL /NS /NC /NP";
            Progress.Value = 0;
            Progress2.Value = 0;
            commands.Clear();
            LogTextBox.Text = "";
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
                foreach (var folder in selectedDefinition.Folders)
                {
                    var output = selectedDefinition.Output + folder.Path.Split(':').Last();
                    if (folder.Include)
                    {
                        commands.Add(Tuple.Create(folder.Path, output, "*.* " + flags));
                    }
                }
            }
        }

        private void DoWork(object? sender, DoWorkEventArgs e)
        {
            if (null == worker) return;
            if (null == sender) return;
            int i = 0;
            numError = 0;
            foreach (var _command in commands)
            {
                command = _command;
                process = new Process();
                Dispatcher.Invoke(() =>
                {
                    Progress.Value = 0;
                    Progress2.Value = 100 * (i++ / (double)commands.Count);
                    TimeProgressTextBox.Text = command.Item1;
                    UpdateStatus();
                });
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "ROBOCOPY";
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (e.Data.StartsWith("ERROR")) numError++;
                            LogTextBox.AppendText(e.Data + Environment.NewLine);
                            LogTextBox.ScrollToEnd();
                        });
                    }
                };

                size = 0;
                inSize = GetSize(command.Item1);

                Timer timer1 = new Timer();
                timer1.Elapsed += new ElapsedEventHandler(DoTimer1);
                timer1.Interval = 1000 * Math.Min(60, Math.Max(5, inSize / 1024.0 / 1024.0 / 1024.0));
                timer1.Enabled = true;
                Timer timer2 = new Timer();
                timer2.Elapsed += new ElapsedEventHandler(DoTimer2);
                timer2.Interval = 5000;
                timer2.Enabled = true;

                if (worker.CancellationPending) return;
                process.StartInfo.Arguments = "\"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3;
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                Dispatcher.Invoke(() =>
                {
                    Progress.Value = 100;
                    Progress2.Value = 100 * (i / (double)commands.Count);
                });
                timer1.Enabled = false;
                timer2.Enabled = false;
            }
            command = null;
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(Environment.NewLine + "Completed with a total of " + numError + " errors detected" + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }

        private void DoTimer1(object? sender, ElapsedEventArgs e)
        {
            if (null == command) return;
            if (null == worker) return;
            if (process == null) return; // guard against null Process

            size = 0;
            outSize = GetSize(command.Item2);

            // avoid divide-by-zero if inSize is 0
            int progress = inSize == 0 ? 0 : (int)(100 * (double)outSize / (double)inSize);
            worker.ReportProgress(progress);
        }

        private void DoTimer2(object? sender, ElapsedEventArgs e)
        {
            if (null == command) return;
            if (null == worker) return;
            if (process == null) return; // guard against null Process

            try
            {
                var runTime = (DateTime.Now - process.StartTime);
                var rates = IoSampler.SampleBytesAsync(process);
                Dispatcher.Invoke(() =>
                {
                    var readB = rates.Item1;
                    string readUnit = " kByte/s";
                    if (readB > 1024)
                    {
                        readB /= 1024;
                        readUnit = " MByte/s";
                    }
                    var writeB = rates.Item2;
                    string writeUnit = " kByte/s";
                    if (writeB > 1024)
                    {
                        writeB /= 1024;
                        writeUnit = " MByte/s";
                    }
                    ReadProgressTextBox.Text = "\nRead = " + readB.ToString("0.#") + readUnit;
                    WriteProgressTextBox.Text = "\nWrite = " + writeB.ToString("0.#") + writeUnit;
                    long sec = (long)runTime.TotalSeconds;
                    long min = sec / 60;
                    long hour = min / 60;
                    TimeProgressTextBox.Text = command.Item1 + "\n"+ hour.ToString("00") + ":" + (min % 60).ToString("00") + 
                    ":" + (sec % 60).ToString("00") + " (H:M:S)";
                });
            }
            catch
            {
            }
        }

        private void ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            Progress.Value = e.ProgressPercentage;
        }

        private void WorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            EndSync();
        }

        private void Button_AbortSyncClick(object sender, RoutedEventArgs e)
        {
            if (null == worker) return;
            worker.CancelAsync();
            EndSync();
        }

        private void EndSync()
        {
            try
            {
                if (null != process && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
            }
            process = null;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            bool bReady = null == process;
            StatusTextBlock.Text = bReady ? "Ready" : "Sync in progress";
            FullSyncButton.IsEnabled = bReady;
            AllSyncButton.IsEnabled = bReady;
            ClipboardButton.IsEnabled = bReady;
            AbortSyncButton.IsEnabled = !bReady;
        }

        private long GetDirectorySize(string directory)
        {
            size = 0;
            size = GetSize(directory);
            return size / 1024 / 1024;
        }

        private long GetSize(string directory)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(directory))
                {
                    GetSize(dir);
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

        private void Button_BatchCommandsClick(object sender, RoutedEventArgs e)
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

    public class IoSampler
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS ioCounters);

        public static Tuple<double, double> SampleBytesAsync(Process proc, int intervalMs = 1000)
        {
            if (!GetProcessIoCounters(proc.Handle, out var start))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            Thread.Sleep(intervalMs);

            if (!GetProcessIoCounters(proc.Handle, out var end))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            ulong readDelta = end.ReadTransferCount - start.ReadTransferCount;
            ulong writeDelta = end.WriteTransferCount - start.WriteTransferCount;
            double seconds = intervalMs / 1000.0;

            return Tuple.Create(readDelta / seconds / 1024.0, writeDelta / seconds / 1024.0);
        }
    }
}

