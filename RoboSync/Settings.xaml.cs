using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public ObservableCollection<SettingData> StartupData = new ObservableCollection<SettingData>();
        public ObservableCollection<SettingData> FolderData = new ObservableCollection<SettingData>();

        public Settings()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reload();
            App.IsStartup = Properties.Settings.Default.IsStartup;
            App.IsSysTray = Properties.Settings.Default.IsSysTray;
            App.IsFastStart = Properties.Settings.Default.IsFastStart;
            App.IsMinimised = Properties.Settings.Default.IsMinimised;
            App.IsRun = Properties.Settings.Default.IsRun;
            App.IsMultipleInstances = Properties.Settings.Default.IsMultipleInstances;

            StartupData.Clear();
            StartupData.Add(new SettingData() { Label = "IsFastStart", Switch = "/F[-]", Description = "Fast startup - omit initial folder size calculations\nHold Escape key down to omit folder size calculations for other operations that load a definition", Flag = App.IsStartup });
            StartupData.Add(new SettingData() { Label = "IsRun", Switch = "/R[-]", Description = "Run a full sync when started", Flag = App.IsRun });
            StartupData.Add(new SettingData() { Label = "IsStartup", Switch = "/S[-]", Description = "Start RoboSync when system starts", Flag = App.IsStartup });
            StartupData.Add(new SettingData() { Label = "IsMinimised", Switch = "/M[-]", Description = "Start the window minimised", Flag = App.IsMinimised });
            StartupData.Add(new SettingData() { Label = "IsSysTray", Switch = "/T[-]", Description = "Add icon to the system tray and hide when minimised, right click system tray icon to exit", Flag = App.IsSysTray });
            StartupData.Add(new SettingData() { Label = "IsMultipleInstances", Switch = "/I[-]", Description = "Allow multiple instances of application", Flag = App.IsMultipleInstances });
            StartupDataGrid.ItemsSource = StartupData;

            FolderData.Clear();
            FolderData.Add(new SettingData() { Label = "ShowDetails", Description = "Show all file and folder operations in log window, this can be a very large number of files", Flag = Properties.Settings.Default.ShowDetails });
            FolderData.Add(new SettingData() { Label = "ShowThreads", Description = "Large files prefer fewer parallel threads (1-128), the default (32) is good in most cases", Flag = Properties.Settings.Default.ShowThreads });
            FolderDataGrid.ItemsSource = FolderData;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.IsFastStart = StartupData[0].Flag;
            App.IsRun = StartupData[1].Flag;
            App.IsStartup = StartupData[2].Flag;
            App.IsMinimised = StartupData[3].Flag;
            App.IsSysTray = StartupData[4].Flag;
            App.IsMultipleInstances = StartupData[5].Flag;

            Utilities.AppSettings();

            Properties.Settings.Default.IsStartup = App.IsStartup;
            Properties.Settings.Default.IsSysTray = App.IsSysTray;
            Properties.Settings.Default.IsFastStart = App.IsFastStart;
            Properties.Settings.Default.IsMinimised = App.IsMinimised;
            Properties.Settings.Default.IsRun = App.IsRun;
            Properties.Settings.Default.IsMultipleInstances = App.IsMultipleInstances;

            Properties.Settings.Default.ShowDetails = FolderData[0].Flag;
            Properties.Settings.Default.ShowThreads = FolderData[1].Flag;
            MainWindow.Win.colDetails.Visibility = Properties.Settings.Default.ShowDetails ? Visibility.Visible : Visibility.Collapsed;
            MainWindow.Win.colThreads.Visibility = Properties.Settings.Default.ShowThreads ? Visibility.Visible : Visibility.Collapsed;

            Properties.Settings.Default.Save();
        }
    }

    public class SettingData
    {
        public string Label { get; set; }
        public string Switch { get; set; }
        public string Description { get; set; }
        public bool Flag { get; set; }
        public int IntValue { get; set; }
        public double DoubleValue { get; set; }

        public SettingData()
        {
            Label = "";
            Switch = "";
            Description = "";
            Flag = false;
            IntValue = 0;
            DoubleValue = 0;
        }
    }
}
