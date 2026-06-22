using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Timer = System.Timers.Timer;

namespace RoboBackup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Definition> Definitions = new ObservableCollection<Definition>();

        private BackgroundWorker worker;
        private Definition? selectedDefinition = null;
        private Process? process = null;
        private List<Tuple<string, string, string>> commands = new List<Tuple<string, string, string>>();
        private Tuple<string, string, string> command;
        private long size = 0;
        private long inSize = 0;
        private long outSize = 0;

        public MainWindow()
        {
            InitializeComponent();

            //LogTextBox.Text = "RoboBackup Log\nRoboBackup; ";
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
                            //size = 0;
                            //folderParts[2] = GetDirectorySize(folderParts[1]);
                            definition.Folders.Add(new Folder()
                            {
                                Path = folderParts[0],
                                Include = bool.Parse(folderParts[1]),
                                Size = folderParts[2]
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
                    definitions += folder.Path + "," + folder.Include.ToString() + "," + folder.Size + ";";
                }
                definitions += "#";
            }
            Properties.Settings.Default.Definitions = definitions;
            Properties.Settings.Default.Save();
            EndBackup();
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
            ListBox listBox = (ListBox)sender;
            selectedDefinition = (Definition)listBox.SelectedItem;
            if (null == selectedDefinition) return;
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = selectedDefinition.Folders;
            DefinitionLabel.Content = selectedDefinition.Label;
            OutputTextBox.Text = selectedDefinition.Output;
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
            selectedDefinition.Output = OutputTextBox.Text;
        }

        private void Button_FullBackupClick(object sender, RoutedEventArgs e)
        {
            DoBackup(true);
        }

        private void Button_DifferentialBackupClick(object sender, RoutedEventArgs e)
        {
            DoBackup(false);
        }

        private void DoBackup(bool bFull)
        {
            string flags = bFull ? "" : "/M ";
            flags += "/MIR /J /XJ /MT /R:1 /W:10";
            /*if (bFull)*/ flags += " /NDL /NFL /NS /NC /NP";
            Progress.Value = 0;
            Progress2.Value = 0;
            commands.Clear();
            LogTextBox.Text = "";
            foreach (var folder in selectedDefinition.Folders)
            {
                var output = selectedDefinition.Output + folder.Path.Split(':').Last();
                if (folder.Include)
                {
                    commands.Add(Tuple.Create(folder.Path, output, "*.* " + flags));
                }
            }

            worker = new BackgroundWorker();
            worker.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCompleted);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();
        }

        private void DoWork(object? sender, DoWorkEventArgs e)
        {
            if (null == sender) return;
            Timer timer = new Timer();
            timer.Elapsed += new ElapsedEventHandler(DoTimer);
            int i = 0;
            foreach (var _command in commands)
            {
                command = _command;
                bool bFull = command.Item3.EndsWith("NP");
                process = new Process();
                Dispatcher.Invoke(() =>
                {
                    Progress.Value = 0;
                    Progress2.Value = 100 * (i++ / (double)commands.Count);
                    UpdateStatus();
                });
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "ROBOCOPY";
                process.OutputDataReceived += (s2, e2) =>
                {
                    if (!string.IsNullOrEmpty(e2.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogTextBox.AppendText(e2.Data + Environment.NewLine);
                            LogTextBox.ScrollToEnd();
                        });
                    }
                };

                size = 0;
                inSize = GetSize(command.Item1);

                timer.Interval = 1000 * Math.Min(10, Math.Max(1, inSize / 1024 / 1024 / 1024));
                timer.Enabled = true;

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
                timer.Enabled = false;
            }
        }

        private void DoTimer(object? sender, ElapsedEventArgs e)
        {
            size = 0;
            outSize = GetSize(command.Item2);
            int progress = (int)(100 * (double)outSize / (double)inSize);
            worker.ReportProgress(progress);
        }

        private void ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            Progress.Value = e.ProgressPercentage;
        }

        private void WorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            EndBackup();
        }

        private void Button_AbortBackupClick(object sender, RoutedEventArgs e)
        {
            worker.CancelAsync();
            EndBackup();
        }

        private void EndBackup()
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
            StatusTextBlock.Text = bReady ? "Ready" : "Backup in progress";
            FullBackupButton.IsEnabled = bReady;
            DifferentialBackupButton.IsEnabled = bReady;
            AbortBackupButton.IsEnabled = !bReady;
            //Cursor = bReady ? null : Cursors.Wait;
        }

        private string GetDirectorySize(string directory)
        {
            size = 0;
            size = GetSize(directory);
            if (size < 1024)
            {
                return size.ToString() + " B";
            }
            else if (size < 1024 * 1024)
            {
                return (size / 1024).ToString() + " kB";
            }
            else if (size < 1024 * 1024 * 1024)
            {
                return (size / 1024 / 1024).ToString() + " MB";
            }
            else
            {
                return (size / 1024 / 1024 / 1024).ToString() + " GB";
            }
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
    }

    public class Folder
    {
        public string Path { get; set; }
        public bool Include { get; set; }
        public string Size { get; set; }

        public Folder()
        {
            Path = string.Empty;
            Include = true;
            Size = "0";
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
}

