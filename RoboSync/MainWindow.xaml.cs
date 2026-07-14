using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timer = System.Timers.Timer;

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Win;
        public ObservableCollection<Definition> Definitions = new ObservableCollection<Definition>();
        public bool UpdateFolder = false;

        private SyncViewModel syncViewModel;
        private Timer timer;
        private Definition? SelectedDefinition = null;
        private System.Windows.Shapes.Rectangle animationRect;
        private System.Windows.Shapes.Rectangle animationRect2;

        public MainWindow()
        {
            Win = this;
            PreInitialise();
            InitializeComponent();
        }

        public SyncViewModel SyncViewModel
        {
            get { return syncViewModel; }
        }

        private void PreInitialise()
        {
            if (!App.IsFastStart)
            {
                SplashScreen splash = new SplashScreen("RoboSync.png");
                splash.Show(true, true);
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
                        AbortSyncButton.IsEnabled = !bReady;
                        SetAnimation(!bReady);
                        break;
                    case "SelectedDefinition":
                        if (SelectedDefinition == syncViewModel.SelectedDefinition) return;
                        SelectedDefinition = syncViewModel.SelectedDefinition;
                        DefinitionsDataGrid.SelectedItem = SelectedDefinition;
                        DefinitionLabel.Text = SelectedDefinition?.Label;
                        OutputTextBox.Text = SelectedDefinition?.Output;
                        FolderExclusionsTextBox.Text = SelectedDefinition?.FolderExclusions;
                        FileExclusionsTextBox.Text = SelectedDefinition?.FileExclusions;
                        FoldersDataGrid.ItemsSource = null;
                        FoldersDataGrid.ItemsSource = SelectedDefinition?.Folders;
                        App.IsFastStart = false;
                        if (null == SelectedDefinition) return;
                        //Force calculation and binding size calc
                        long totalSize = 0;
                        foreach (var folder in SelectedDefinition.Folders)
                        {
                            folder.Path = folder.Path;
                            folder.Size = folder.Size;
                            totalSize += folder.Size;
                        }
                        DefinitionLabel.Text += " (" + totalSize + " MB)";
                        break;
                    case "LogLine":
                        LogTextBox.AppendText(syncViewModel.LogLine + Environment.NewLine);
                        LogTextBox.ScrollToEnd();
                        break;
                    case "LogText":
                        LogTextBox.Text = syncViewModel.LogText;
                        LogTextBox.ScrollToEnd();
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
            if (Width < 200) Width = 1200;
            if (Height < 100) Height = 800;

            Definitions.Clear();
            DefinitionsDataGrid.ItemsSource = Definitions;

            syncViewModel = new SyncViewModel(Definitions);
            DataContext = syncViewModel;
            syncViewModel.PropertyChanged += ViewModelPropertyChanged;
            syncViewModel.Initialise();
            colDetails.Visibility = Properties.Settings.Default.ShowDetails ? Visibility.Visible : Visibility.Collapsed;
            colThreads.Visibility = Properties.Settings.Default.ShowThreads ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Progress.ApplyTemplate();
            animationRect = (System.Windows.Shapes.Rectangle)Progress.Template.FindName("Animation", Progress);
            Progress2.ApplyTemplate();
            animationRect2 = (System.Windows.Shapes.Rectangle)Progress.Template.FindName("Animation", Progress2);

            timer = new Timer();
            timer.Elapsed += new ElapsedEventHandler(DoTimer);
            timer.Interval = 5000;
            timer.Enabled = true;

            try
            {
                Utilities.AppSettings();

                if (App.IsSysTray)
                {
                    var wih = new System.Windows.Interop.WindowInteropHelper(this);
                    var hWnd = wih.Handle;
                    NotifyIcon notifyIcon = new NotifyIcon(this);
                    notifyIcon.Create(wih.Handle);
                    ShowInTaskbar = false;
                    //WindowState = WindowState.Minimized;
                }

                if (App.IsMinimised)
                {
                    WindowState = WindowState.Minimized;
                }

                if (App.IsRun)
                {
                    syncViewModel.DoSync(true);
                }
            }
            catch (Exception)
            {
            }
        }

        private void DoTimer(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CheckOutputLocation();
                if (UpdateFolder)
                {
                    Cursor = Cursors.Wait;
                    FoldersDataGrid.ItemsSource = null;
                    FoldersDataGrid.ItemsSource = SelectedDefinition?.Folders;
                    Cursor = null;
                    UpdateFolder = false;
                }
            });
        }

        private void SetAnimation(bool bVisible)
        {
            if (null == SelectedDefinition) return;
            if (null != animationRect && null != animationRect2)
            {
                animationRect.Visibility = bVisible ? Visibility.Visible : Visibility.Collapsed;
                animationRect2.Visibility = bVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (App.IsStartup && !App.CanClose && !Keyboard.IsKeyDown(Key.Escape))
            {
                e.Cancel = true;
                App.CanClose = false;
                WindowState = WindowState.Minimized;
                return;
            }

            OutputTextBox.Focus();

            Properties.Settings.Default.WinState = WindowState == WindowState.Minimized ? (int)WindowState.Normal : (int)WindowState;
            Properties.Settings.Default.WinTop = Top;
            Properties.Settings.Default.WinLeft = Left;
            Properties.Settings.Default.WinWidth = Width;
            Properties.Settings.Default.WinHeight = Height;
            Properties.Settings.Default.Version = syncViewModel.Version?.ToString();
            if (!Keyboard.IsKeyDown(Key.Space)) syncViewModel.SaveDefinitions();
            Properties.Settings.Default.Save();

            syncViewModel.EndSync();
        }

        private void OnOutputBrowse(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select an output folder";
            if (Directory.Exists(OutputTextBox.Text) && null != Directory.GetParent(OutputTextBox.Text)) dialog.InitialDirectory = Directory.GetParent(OutputTextBox.Text)?.ToString();
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                OutputTextBox.Text = dialog.FolderName;
            }
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckOutputLocation();
        }

        private void CheckOutputLocation()
        {
            if (null == SelectedDefinition || OutputTextBox.Text.Length < 1) return;
            SelectedDefinition.Output = OutputTextBox.Text;
            var drive = SelectedDefinition.Output.ToUpper().Substring(0, 1);
            var driveInfo = IsReadyDrive(drive);
            if (drive == "C" || !driveInfo.Item1)
            {
                OutputTextBox.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                OutputTextBox.Foreground = Directory.Exists(OutputTextBox.Text) ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.Blue);
            }
            OutputInfoTextBox.Text = driveInfo.Item2;
        }

        private Tuple<bool, string> IsReadyDrive(string driveLetter)
        {
            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (driveInfo.IsReady)
                {
                    var space = driveInfo.AvailableFreeSpace / 1024 / 1024;
                    return Tuple.Create(driveInfo.IsReady, "(" + space.ToString("0.#") + " MB Free)");
                }
                else
                {
                    return Tuple.Create(false, "(Not Available)");
                }
            }
            catch (ArgumentException)
            {
                return Tuple.Create(false, "(Not Available)");
            }
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
                folder = new Folder { Include = true };
            }

            OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select an input folder";
            if (Directory.Exists(folder.Path) && null != Directory.GetParent(folder.Path)) dialog.InitialDirectory = Directory.GetParent(folder.Path)?.ToString();
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                if (folder.Path == string.Empty)
                {
                    SelectedDefinition.Folders.Add(folder);
                }
                folder.Path = dialog.FolderName;
            }
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = SelectedDefinition.Folders;

            DefinitionLabel.Text = SelectedDefinition.Label;
            long totalSize = 0;
            foreach (var _folder in SelectedDefinition.Folders)
            {
                totalSize += _folder.Size;
            }
            DefinitionLabel.Text += " (" + totalSize + " MB)";
        }

        private void DefinitionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            syncViewModel.SelectedDefinition = (Definition)dataGrid.SelectedItem;
        }

        private void DefinitionsDataGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            DefinitionLabel.Text = ((Definition)dataGrid.SelectedItem).Label;
            long totalSize = 0;
            foreach (var folder in ((Definition)dataGrid.SelectedItem).Folders)
            {
                totalSize += folder.Size;
            }
            DefinitionLabel.Text += " (" + totalSize + " MB)";
        }

        private void Button_AddClick(object sender, RoutedEventArgs e)
        {
            Definitions.Add(new Definition() { Label = "Definition" + (Definitions.Count + 1) });
            syncViewModel.SelectedDefinition = Definitions.Last();
        }

        private void Button_CopyClick(object sender, RoutedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            Definition copy = new Definition() { Label = SelectedDefinition.Label + "_Copy" };
            copy.Output = SelectedDefinition.Output;
            //copy.Schedule = SelectedDefinition.Schedule;
            copy.Day = SelectedDefinition.Day;
            copy.Time = SelectedDefinition.Time;
            copy.FolderExclusions = SelectedDefinition.FolderExclusions;
            copy.FileExclusions = SelectedDefinition.FileExclusions;
            foreach (var folder in SelectedDefinition.Folders)
            {
                copy.Folders.Add(new Folder() { Include = folder.Include, Details = folder.Details, Threads = folder.Threads, Path = folder.Path, Size = folder.Size});
            }
            Definitions.Add(copy);
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

        private void Button_UpdatesClick(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/litdev1/RoboSync";
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (null == syncViewModel.SelectedDefinition) return;
            var checkbox = (CheckBox)sender;
            if (null == checkbox.IsChecked) return;
            syncViewModel.SelectedDefinition.Schedule = (bool)checkbox.IsChecked;
            syncViewModel.UpdateSchedule();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null == syncViewModel.SelectedDefinition) return;
            syncViewModel.SelectedDefinition.Day = (Days)((ComboBox)sender).SelectedValue;
            syncViewModel.UpdateSchedule();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (null == syncViewModel.SelectedDefinition) return;
            TimeOnly time = syncViewModel.SelectedDefinition.Time;
            TimeOnly.TryParse(((TextBox)sender).Text, out time);
            syncViewModel.SelectedDefinition.Time = time;
            syncViewModel.UpdateSchedule();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (App.IsSysTray && WindowState == WindowState.Minimized)
            {
                Hide();
            }
            timer.Enabled = WindowState != WindowState.Minimized;
        }

        private void Button_SettingsClick(object sender, RoutedEventArgs e)
        {
            Settings settings = new Settings();
            settings.ShowDialog();
        }

        private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = null != syncViewModel.SelectedDefinition && syncViewModel.SelectedDefinition.Folders.Count > 0;
        }

        private void Copy(object sender, ExecutedRoutedEventArgs e)
        {
            if (null == syncViewModel.SelectedDefinition) return;
            string text = "";
            foreach (var folder in syncViewModel.SelectedDefinition.Folders)
            {
                text += folder.Include.ToString() + "\t" + folder.Details.ToString() + "\t" + folder.Threads.ToString() + "\t" + 
                    folder.Path + "\t" + folder.Size.ToString() + "\n";
            }
            Clipboard.SetText(text);
        }

        private void CanPaste(object sender, CanExecuteRoutedEventArgs e)
        {
            string clipboardText = Clipboard.GetText();
            e.CanExecute = !string.IsNullOrEmpty(clipboardText);
        }

        private void Paste(object sender, ExecutedRoutedEventArgs e)
        {
            if (null == syncViewModel.SelectedDefinition) return;
            try
            {
                string clipboardText = Clipboard.GetText();
                var folders = clipboardText.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var folderData in folders)
                {
                    var data = folderData.Split([ ',', '\t' ], StringSplitOptions.RemoveEmptyEntries);
                    bool include;
                    bool details;
                    int threads;
                    long size;
                    switch (data.Length)
                    {
                        case 1:
                            syncViewModel.SelectedDefinition.Folders.Add(new Folder() { Path = data[0] });
                            break;
                        case 2:
                            if (bool.TryParse(data[0], out include))
                            {
                                syncViewModel.SelectedDefinition.Folders.Add(new Folder() { Include = include, Path = data[1]});
                            }
                            break;
                        case 3:
                            if (bool.TryParse(data[0], out include) && long.TryParse(data[2], out size))
                            {
                                syncViewModel.SelectedDefinition.Folders.Add(new Folder() { Include = include, Path = data[1], Size = size });
                            }
                            break;
                        case 4:
                            if (bool.TryParse(data[0], out include) && int.TryParse(data[1], out threads) && long.TryParse(data[3], out size))
                            {
                                syncViewModel.SelectedDefinition.Folders.Add(new Folder() { Include = include, Threads = threads, Path = data[2], Size = size });
                            }
                            break;
                        case 5:
                            if (bool.TryParse(data[0], out include) && bool.TryParse(data[1], out details) && int.TryParse(data[2], out threads) && long.TryParse(data[4], out size))
                            {
                                syncViewModel.SelectedDefinition.Folders.Add(new Folder() { Include = include, Details = details, Threads = threads, Path = data[3], Size = size });
                            }
                            break;
                    }
                }
            }
            catch
            {
            }
        }

        private void FolderXTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            SelectedDefinition.FolderExclusions = FolderExclusionsTextBox.Text;
        }

        private void FileXTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            SelectedDefinition.FileExclusions = FileExclusionsTextBox.Text;
        }

        private void OnOutputOpen(object sender, RoutedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            var path = OutputTextBox.Text;
            if (Directory.Exists(path))
            {
                var runExplorer = new ProcessStartInfo();
                runExplorer.FileName = "explorer.exe";
                runExplorer.Arguments = path;
                Process.Start(runExplorer);
            }
        }

        private void OnOutputRepair(object sender, RoutedEventArgs e)
        {
            if (null == SelectedDefinition) return;
            syncViewModel.RepairDrive(SelectedDefinition.Output.First());
        }
    }
}

