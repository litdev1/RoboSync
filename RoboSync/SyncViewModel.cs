using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
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
            var version = Assembly.GetExecutingAssembly().GetName().Version;
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
            //Clipboard.Clear();
            //Clipboard.SetText(text);
            text += "pause\n";
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RoboSync.bat";
            File.WriteAllText(path, text);
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
}
