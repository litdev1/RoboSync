using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            PreInitialise();
            InitializeComponent();
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
                        //ClipboardButton.IsEnabled = bReady;
                        AbortSyncButton.IsEnabled = !bReady;
                        //SettingsButton.IsEnabled = bReady;
                        break;
                    case "SelectedDefinition":
                        if (SelectedDefinition == syncViewModel.SelectedDefinition) return;
                        SelectedDefinition = syncViewModel.SelectedDefinition;
                        DefinitionsDataGrid.SelectedItem = SelectedDefinition;
                        DefinitionLabel.Text = SelectedDefinition?.Label;
                        OutputTextBox.Text = SelectedDefinition?.Output;
                        FoldersDataGrid.ItemsSource = null;
                        FoldersDataGrid.ItemsSource = SelectedDefinition?.Folders;
                        if (null == SelectedDefinition || App.IsFastStart)
                        {
                            App.IsFastStart = false;
                            return;
                        }
                        Cursor = Cursors.Wait;
                        foreach (var folder in SelectedDefinition.Folders)
                        {
                            folder.Size = Dir.GetSize(folder.Path) / 1024 / 1024;
                        }
                        Cursor = null;
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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                syncViewModel.Version = new Version(1, 0, 0, 0);

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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (App.IsStartup && !App.CanClose)
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

            syncViewModel.SaveDefinitions();
            syncViewModel.EndSync();
        }

        private void OnOutputBrowse(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select an output folder";
            if (Directory.Exists(OutputTextBox.Text)) dialog.InitialDirectory = OutputTextBox.Text;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                OutputTextBox.Text = dialog.FolderName;
            }
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == SelectedDefinition || OutputTextBox.Text.Length < 1) return;
            SelectedDefinition.Output = OutputTextBox.Text;
            var drive = SelectedDefinition.Output.ToUpper().Substring(0, 1);
            if (drive == "C" || !IsReadyDrive(drive))
            {
                OutputTextBox.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                OutputTextBox.Foreground = Directory.Exists(OutputTextBox.Text) ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.Blue);
            }
        }

        private bool IsReadyDrive(string driveLetter)
        {
            try
            {
                return new DriveInfo(driveLetter).IsReady;
            }
            catch (ArgumentException)
            {
                return false;
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
                folder.Size = Dir.GetSize(folder.Path) / 1024 / 1024;
            }
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = SelectedDefinition.Folders;
        }

        private void DefinitionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Cursor = Cursors.Wait;
            DataGrid dataGrid = (DataGrid)sender;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) App.IsFastStart = true;
            syncViewModel.SelectedDefinition = (Definition)dataGrid.SelectedItem;
            Cursor = null;
        }

        private void DefinitionsDataGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            DefinitionLabel.Text = ((Definition)dataGrid.SelectedItem).Label;
        }

        private void Button_AddClick(object sender, RoutedEventArgs e)
        {
            Definitions.Add(new Definition() { Label = "Definition" + (Definitions.Count + 1) });
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) App.IsFastStart = true;
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
            foreach (var folder in SelectedDefinition.Folders)
            {
                copy.Folders.Add(new Folder() { Include = folder.Include, Path = folder.Path, Size = folder.Size});
            }
            Definitions.Add(copy);
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) App.IsFastStart = true;
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
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) App.IsFastStart = true;
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
            syncViewModel.SelectedDefinition.Schedule = (bool)((CheckBox)sender).IsChecked;
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
        }

        private void Button_SettingsClick(object sender, RoutedEventArgs e)
        {
            Settings settings = new Settings();
            settings.ShowDialog();
        }
    }
}

