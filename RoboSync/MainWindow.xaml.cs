using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SyncViewModel syncViewModel;

        public ObservableCollection<Definition> Definitions = new ObservableCollection<Definition>();
        private Definition? SelectedDefinition = null;

        public MainWindow()
        {
            InitializeComponent();

            //RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            //string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            //string executableName = "\"" + Process.GetCurrentProcess().MainModule.FileName + "\" /hide";
            //registryKey.SetValue(assemblyName, executableName);

            if (App.bHidden)
            {
                Visibility = Visibility.Hidden;
            }
        }

        private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case "Status":
                        bool bReady = syncViewModel.Status == 0;
                        StatusTextBlock.Text = bReady ? "Ready" : "Sync in progress";
                        FullSyncButton.IsEnabled = bReady;
                        AllSyncButton.IsEnabled = bReady;
                        ClipboardButton.IsEnabled = bReady;
                        AbortSyncButton.IsEnabled = !bReady;
                        break;
                    case "SelectedDefinition":
                        if (SelectedDefinition == syncViewModel.SelectedDefinition) return;
                        SelectedDefinition = syncViewModel.SelectedDefinition;
                        DefinitionsListBox.SelectedItem = SelectedDefinition;
                        DefinitionLabel.Content = SelectedDefinition?.Label;
                        OutputTextBox.Text = SelectedDefinition?.Output;
                        FoldersDataGrid.ItemsSource = null;
                        FoldersDataGrid.ItemsSource = SelectedDefinition?.Folders;
                        if (null == SelectedDefinition) return;
                        foreach (var folder in SelectedDefinition.Folders)
                        {
                            folder.Size = Dir.GetSize(folder.Path) / 1024 / 1024;
                        }
                        break;
                    case "Progress1":
                        Progress.Value = syncViewModel.Progress1;
                        break;
                    case "Progress2":
                        Progress2.Value = syncViewModel.Progress2;
                        break;
                    case "LogLine":
                        LogTextBox.AppendText(syncViewModel.LogLine + Environment.NewLine);
                        LogTextBox.ScrollToEnd();
                        break;
                    case "LogText":
                        LogTextBox.Text = syncViewModel.LogText;
                        LogTextBox.ScrollToEnd();
                        break;
                    case "ReadBytes":
                        ReadProgressTextBox.Text = syncViewModel.ReadBytes;
                        break;
                    case "WriteBytes":
                        WriteProgressTextBox.Text = syncViewModel.WriteBytes;
                        break;
                    case "ProgressTime":
                        TimeProgressTextBox.Text = syncViewModel.ProgressTime;
                        break;
                }
            });
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
            if (Properties.Settings.Default.WinState > 0) WindowState = (WindowState)Properties.Settings.Default.WinState;
            if (Properties.Settings.Default.WinTop > 0) Top = Properties.Settings.Default.WinTop;
            if (Properties.Settings.Default.WinLeft > 0) Left = Properties.Settings.Default.WinLeft;
            if (Properties.Settings.Default.WinWidth > 0) Width = Properties.Settings.Default.WinWidth;
            if (Properties.Settings.Default.WinHeight > 0) Height = Properties.Settings.Default.WinHeight;

            Definitions.Clear();
            DefinitionsListBox.ItemsSource = Definitions;

            syncViewModel = new SyncViewModel(Definitions);
            syncViewModel.PropertyChanged += ViewModelPropertyChanged;
            syncViewModel.Initialise();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.WinState = WindowState == WindowState.Minimized ? (int)WindowState.Normal : (int)WindowState;
            Properties.Settings.Default.WinTop = Top;
            Properties.Settings.Default.WinLeft = Left;
            Properties.Settings.Default.WinWidth = Width;
            Properties.Settings.Default.WinHeight = Height;

            syncViewModel.SaveDefinitions();
            syncViewModel.EndSync();
        }

        private void OnOutputBrowse(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select an output folder";
            dialog.InitialDirectory = OutputTextBox.Text;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                OutputTextBox.Text = dialog.FolderName;
            }
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            SelectedDefinition.Output = OutputTextBox.Text;
        }

        private void OnFolderBrowse(object sender, RoutedEventArgs e)
        {
            if (null == SelectedDefinition) return;
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
            dialog.Title = "Select an input folder";
            dialog.InitialDirectory = folder.Path;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                if (folder.Path == string.Empty)
                {
                    SelectedDefinition.Folders.Add(folder);
                }
                folder.Path = dialog.FolderName;
                folder.Size = Dir.GetSize(folder.Path) / 1024 / 1024;
            }
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = SelectedDefinition.Folders;
        }

        private void DefinitionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Cursor = Cursors.Wait;
            ListBox listBox = (ListBox)sender;
            syncViewModel.SelectedDefinition = (Definition)listBox.SelectedItem;
            Cursor = null;
        }

        private void DefinitionsListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            DefinitionLabel.Content = ((Definition)listBox.SelectedItem).Label;
        }

        private void Button_AddClick(object sender, RoutedEventArgs e)
        {
            Definitions.Add(new Definition() { Label = "Definition " + (Definitions.Count + 1) });
            syncViewModel.SelectedDefinition = Definitions.Last();
        }

        private void Button_DeleteClick(object sender, RoutedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            var index = Definitions.IndexOf(SelectedDefinition) - 1;
            Definitions.Remove(SelectedDefinition);
            if (index < 0)
            {
                index = 0;
                Definitions.Add(new Definition() { Label = "Default Definition" });
            }
            syncViewModel.SelectedDefinition = Definitions[index];
        }

        private void Button_FullSyncClick(object sender, RoutedEventArgs e)
        {
            syncViewModel.DoSync();
        }

        private void Button_AllSyncClick(object sender, RoutedEventArgs e)
        {
            syncViewModel.DoSync(true);
        }

        private void Button_AbortSyncClick(object sender, RoutedEventArgs e)
        {
            syncViewModel.AbortSync();
        }

        private void Button_BatchCommandsClick(object sender, RoutedEventArgs e)
        {
            syncViewModel.BatchCommands();
        }
    }
}

