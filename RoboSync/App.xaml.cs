using System.Diagnostics;
using System.Reflection;
using System.Windows;
using ProjectSettings = global::RoboSync.Properties.Settings;

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsStartup = false;
        public static bool IsSysTray = false;
        public static bool IsFastStart = false;
        public static bool IsMinimised = false;
        public static bool IsRun = false;
        public static bool IsMultipleInstances = false;

        public static bool CanClose = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ProjectSettings.Default.Reload();
            IsStartup = ProjectSettings.Default.IsStartup;
            IsSysTray = ProjectSettings.Default.IsSysTray;
            IsFastStart = ProjectSettings.Default.IsFastStart;
            IsMinimised = ProjectSettings.Default.IsMinimised;
            IsRun = ProjectSettings.Default.IsRun;
            IsMultipleInstances = ProjectSettings.Default.IsMultipleInstances;

            for (int i = 0; i != e.Args.Length; ++i)
            {
                IsStartup = ArgCheck(e.Args[i], "S", IsStartup);
                IsSysTray = ArgCheck(e.Args[i], "T", IsSysTray);
                IsFastStart = ArgCheck(e.Args[i], "F", IsFastStart);
                IsMinimised = ArgCheck(e.Args[i], "M", IsMinimised);
                IsRun = ArgCheck(e.Args[i], "R", IsRun);
                IsMultipleInstances = ArgCheck(e.Args[i], "I", IsMultipleInstances);
            }

            ProjectSettings.Default.IsStartup = IsStartup;
            ProjectSettings.Default.IsSysTray = IsSysTray;
            ProjectSettings.Default.IsFastStart = IsFastStart;
            ProjectSettings.Default.IsMinimised = IsMinimised;
            ProjectSettings.Default.IsRun = IsRun;
            ProjectSettings.Default.IsMultipleInstances = IsMultipleInstances;
            ProjectSettings.Default.Save();

            if (!IsMultipleInstances)
            {
                var thisProc = Process.GetCurrentProcess();
                var otherProc = Process.GetProcessesByName(thisProc.ProcessName);
                if (otherProc.Length > 1)
                {
                    IsFastStart = true;
                    MessageBox.Show("RoboSync is already running", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Current.Shutdown();
                }
            }
        }

        private bool ArgCheck(string arg, string s, bool bValue)
        {
            bool ret = bValue;
            if (arg.ToUpper().StartsWith("/" + s))
            {
                ret = !arg.ToUpper().EndsWith("-");
            }
            return ret;
        }
    }

}
