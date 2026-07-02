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
        public ObservableCollection<SettingData> SettingsData = new ObservableCollection<SettingData>();

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

            SettingsData.Clear();
            SettingsData.Add(new SettingData() { Label = "IsFastStart", Switch = "/F[-]", Description = "Fast startup - omit initial folder size calculations\nUse Shift key to omit folder size calculations for other operations that load a definition", Value = App.IsStartup });
            SettingsData.Add(new SettingData() { Label = "IsRun", Switch = "/R[-]", Description = "Run a full sync when started", Value = App.IsRun });
            SettingsData.Add(new SettingData() { Label = "IsStartup", Switch = "/S[-]", Description = "Start RoboSync when system starts", Value = App.IsStartup });
            SettingsData.Add(new SettingData() { Label = "IsMinimised", Switch = "/M[-]", Description = "Start the window minimised", Value = App.IsMinimised });
            SettingsData.Add(new SettingData() { Label = "IsSysTray", Switch = "/T[-]", Description = "Add icon to the system tray and hide when minimised, right click system tray icon to exit", Value = App.IsSysTray });
            SettingsData.Add(new SettingData() { Label = "IsMultipleInstances", Switch = "/I[-]", Description = "Allow multiple instances of application", Value = App.IsMultipleInstances });
            SettingsDataGrid.ItemsSource = SettingsData;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.IsFastStart = SettingsData[0].Value;
            App.IsRun = SettingsData[1].Value;
            App.IsStartup = SettingsData[2].Value;
            App.IsMinimised = SettingsData[3].Value;
            App.IsSysTray = SettingsData[4].Value;
            App.IsMultipleInstances = SettingsData[5].Value;

            Utilities.AppSettings();

            Properties.Settings.Default.IsStartup = App.IsStartup;
            Properties.Settings.Default.IsSysTray = App.IsSysTray;
            Properties.Settings.Default.IsFastStart = App.IsFastStart;
            Properties.Settings.Default.IsMinimised = App.IsMinimised;
            Properties.Settings.Default.IsRun = App.IsRun;
            Properties.Settings.Default.IsMultipleInstances = App.IsMultipleInstances;
            Properties.Settings.Default.Save();
        }
    }

    public class SettingData
    {
        public string Label { get; set; }
        public string Switch { get; set; }
        public string Description { get; set; }
        public bool Value { get; set; }

        public SettingData()
        {
            Label = "";
            Switch = "";
            Description = "";
            Value = false;
        }
    }
}
