using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace RoboSync
{
    public enum Days { Daily, Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday };

    public class SyncViewModel : INotifyPropertyChanged
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

        public Version? Version
        {
            get { return new Version(1,4,0,0); }
        }

        public int Status
        {
            get { return syncModel.Status; }
        }

        public int Progress1
        {
            get { return syncModel.Progress1; }
            set { syncModel.Progress1 = value; OnPropertyChanged(); }
        }

        public int Progress2
        {
            get { return syncModel.Progress2; }
            set { syncModel.Progress2 = value; OnPropertyChanged(); }
        }

        public string LogLine
        {
            get { return syncModel.LogLine; }
            set { syncModel.LogLine = value; OnPropertyChanged(); }
        }

        public string ReadBytes
        {
            get { return syncModel.ReadBytes; }
            set { syncModel.ReadBytes = value; OnPropertyChanged(); }
        }

        public string WriteBytes
        {
            get { return syncModel.WriteBytes; }
            set { syncModel.WriteBytes = value; OnPropertyChanged(); }
        }

        public string ProgressTime
        {
            get { return syncModel.ProgressTime; }
            set { syncModel.ProgressTime = value; OnPropertyChanged(); }
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

        public void Initialise()
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
                "If folder doesn't exist it will be displayed in blue, but will be created\n" +
                "If the drive is not present (or C:) it will be displayed in red\n" +
                "Ensure this location has sufficient space and doesn't contain other files\n" +
                "(that could be modified or deleted)\n\n" +
                "Step 2:\nUse the table Browse buttons to add source folders to the table\n" +
                "A size calculation is performed when a locaton is entered with Browse\n" +
                "Locations may be de-selected using the Include tickbox\n" +
                "Delete a location by selecting the table row and pressing the Delete key\n\n" +
                "Step 3:\nStart the sync - the first sync will take the longest\n" +
                "Subsequent syncs will only modify updated files\n" +
                "Check the progress report in this window for errors\n" +
                "Also check the Output Folder files after the first run to be certain\n\n" +
                "Multiple definitions may be used to sync different sets of folders\n" +
                "Progress calculations are approximate to keep performance optimal\n" +
                "ROBOCOPY commands may be exported to Desktop/RoboSync.bat for use directly\n" +
                "Recommend closing other applications first - locked files are not copied\n" +
                "Definitions may be scheduled (Task Scheduler) to be synced daily or weekly\n" +
                "RoboSync does not need to be running to perform scheduled sync backups\n";

            var saveVersion = Version;
            if (!Version.TryParse(Properties.Settings.Default.Version, out saveVersion))
            {
                saveVersion = new Version(1, 0, 0, 0);
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ObservableCollection<Definition>));
                ObservableCollection<Definition>? tempDefinitions = null;

                using (TextReader reader = new StringReader(Properties.Settings.Default.Definitions))
                {
                    var result = serializer.Deserialize(reader);
                    if (null != result)
                    {
                        tempDefinitions = (ObservableCollection<Definition>)result;
                    }
                }
                if (null != tempDefinitions)
                {
                    foreach (var tempDefinition in tempDefinitions)
                    {
                        Definitions.Add(tempDefinition);
                    }
                }
            }
            catch
            {
            }

            if (Definitions.Count == 0)
            {
                Definitions.Add(new Definition() { Label = "Default" });
            }
            SelectedDefinition = Definitions[0];
        }

        public void SaveDefinitions()
        {
            StringBuilder definitions = new StringBuilder();
            using (var writer = new StringWriter(definitions))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Definition>));
                serializer.Serialize(writer, Definitions);
                Properties.Settings.Default.Definitions = definitions.ToString();
            }
        }

        public void DoSync(bool bAll = false)
        {
            if (null == SelectedDefinition) return;
            GetCommands(bAll);
            LogText = "";
            syncModel.DoSync(commands);
        }

        public void GetCommands(bool bAll = false)
        {
            if (null == SelectedDefinition) return;
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
                            commands.Add(Tuple.Create(folder.Path, output, "*.* " + Flags(folder)));
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
                        commands.Add(Tuple.Create(folder.Path, output, "*.* " + Flags(folder)));
                    }
                }
            }
        }

        private string Flags(Folder folder)
        {
            string flags = "/MIR /J /XJ /MT:" + Math.Min(128, Math.Max(1, folder.Threads)) + " /R:0 /W:0 /NP" + (folder.Details? "" : " /NDL /NFL /NS /NC"); // "/M
            if (null != SelectedDefinition)
            {
                var Xfolder = SelectedDefinition.FolderExclusions.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);
                if (Xfolder.Length > 0)
                {
                    flags += " /XD";
                    foreach (var x in Xfolder)
                    {
                        flags += " \"" + x + "\"";
                    }
                }
                var Xfile = SelectedDefinition.FileExclusions.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);
                if (Xfile.Length > 0)
                {
                    flags += " /XF";
                    foreach (var x in Xfile)
                    {
                        flags += " \"" + x + "\"";
                    }
                }
            }
            return flags;
        }

        public void BatchCommands()
        {
            GetCommands(true);
            string text = "";
            foreach (var command in commands)
            {
                text += "ROBOCOPY \"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3 + "\n";
            }
            Clipboard.Clear();
            Clipboard.SetText(text);
            text += "pause\n";
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RoboSync.bat";
            File.WriteAllText(path, text);
        }

        public void UpdateSchedule()
        {
            //Delete all RoboSync tasks first
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "SchTasks",
                    Arguments = "/Query /FO List",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            while (!process.StandardOutput.EndOfStream)
            {
                string? line = process.StandardOutput.ReadLine();
                if (null != line && line.Contains("\\RoboSync\\"))
                {
                    var task = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                    var process1 = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "SchTasks",
                            Arguments = "/Delete /TN \"" + task + "\" /F",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process1.Start();
                    process1.WaitForExit();
                }
            }

            //Create the bat file and set the task
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\RoboSync\\";
            foreach (var definition in Definitions)
            {
                if (definition.Schedule)
                {
                    Regex regex = new Regex("[^a-zA-Z0-9 -]");
                    string label = regex.Replace(definition.Label, "");
                    string time = definition.Time.ToString("HH:mm");
                    string schedule = "";
                    switch (definition.Day)
                    {
                        case Days.Daily:
                            schedule = "DAILY";
                            break;
                        case Days.Sunday:
                            schedule = "WEEKLY /D SUN";
                            break;
                        case Days.Monday:
                            schedule = "WEEKLY /D MON";
                            break;
                        case Days.Tuesday:
                            schedule = "WEEKLY /D TUE";
                            break;
                        case Days.Wednesday:
                            schedule = "WEEKLY /D WED";
                            break;
                        case Days.Thursday:
                            schedule = "WEEKLY /D THU";
                            break;
                        case Days.Friday:
                            schedule = "WEEKLY /D FRI";
                            break;
                        case Days.Saturday:
                            schedule = "WEEKLY /D SAT";
                            break;
                    }

                    string text = "";
                    foreach (var folder in definition.Folders)
                    {
                        var output = definition.Output + folder.Path.Split(':').Last();
                        if (folder.Include)
                        {
                            bool tempDetails = folder.Details;
                            folder.Details = false;
                            text += "ROBOCOPY \"" + folder.Path + "\" \"" + output + "\" " + "*.* " + Flags(folder) + "\n";
                            folder.Details = tempDetails;
                        }
                    }
                    text += "pause" + "\n";
                    File.WriteAllText(appData + label + ".bat", text);

                    string createTaskCmd = "/CREATE /F /SC " + schedule + " /TN \"RoboSync\\" + label + "\" /TR \"" + appData + label + ".bat\" /ST " + time;
                    Process.Start(new ProcessStartInfo("SCHTASKS", createTaskCmd) { CreateNoWindow = true, UseShellExecute = false });
                }
            }
        }

        public void AbortSync()
        {
            syncModel.AbortSync();
        }

        public void EndSync()
        {
            syncModel.EndSync();
        }

        public void RepairDrive(char drive)
        {
            if (drive == 'C') return;
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = "chkdsk";
            process.StartInfo.Arguments = drive + ": /X /F";
            process.Start();
            process.WaitForExit();
        }
    }

    public class Folder
    {
        private string _Path;
        public string Path {
            get { return _Path; }
            set
            {
                _Path = value;
                if (!(App.IsFastStart || Keyboard.IsKeyDown(Key.Escape)))
                {
                    var lastSize = Size;
                    MainWindow.Win.Cursor = Cursors.Wait;
                    Size = Dir.GetSize(_Path) / 1024 / 1024;
                    MainWindow.Win.Cursor = null;
                    if (lastSize != Size)
                    {
                        MainWindow.Win.UpdateFolder = true;
                    }
                }
            } 
        }
        public bool Include { get; set; }
        public bool Details { get; set; }
        public long Size { get; set; }
        public long Threads { get; set; }

        public Folder()
        {
            Path = string.Empty;
            Include = true;
            Details = false;
            Size = 0;
            Threads = 32;
        }
    }

    public class Definition
    {
        public ObservableCollection<Folder> Folders { get; set; }
        public string Label { get; set; }
        public string Output { get; set; }
        public bool Schedule { get; set; }
        public TimeOnly Time { get; set; }
        public Days Day { get; set; }
        public string FolderExclusions { get; set; }
        public string FileExclusions { get; set; }

        public Definition()
        {
            Folders = new ObservableCollection<Folder>();
            Label = string.Empty;
            Output = string.Empty;
            Schedule = false;
            Time = new TimeOnly(2, 0);
            Day = Days.Daily;
            FolderExclusions = string.Empty;
            FileExclusions = string.Empty;
        }
    }
}
