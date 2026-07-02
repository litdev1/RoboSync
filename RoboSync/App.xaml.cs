using System.Diagnostics;
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

        public static bool CanClose = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ProjectSettings.Default.Reload();
            IsStartup = ProjectSettings.Default.IsStartup;
            IsSysTray = ProjectSettings.Default.IsSysTray;
            IsFastStart = ProjectSettings.Default.IsFastStart;
            IsMinimised = ProjectSettings.Default.IsMinimised;
            IsRun = ProjectSettings.Default.IsRun;

            for (int i = 0; i != e.Args.Length; ++i)
            {
                IsStartup = ArgCheck(e.Args[i], "S");
                IsSysTray = ArgCheck(e.Args[i], "T");
                IsFastStart = ArgCheck(e.Args[i], "F");
                IsMinimised = ArgCheck(e.Args[i], "M");
                IsRun = ArgCheck(e.Args[i], "R");
            }

            ProjectSettings.Default.IsStartup = IsStartup;
            ProjectSettings.Default.IsSysTray = IsSysTray;
            ProjectSettings.Default.IsFastStart = IsFastStart;
            ProjectSettings.Default.IsMinimised = IsMinimised;
            ProjectSettings.Default.IsRun = IsRun;
            ProjectSettings.Default.Save();

            var thisProc = Process.GetCurrentProcess();
            var otherProc = Process.GetProcessesByName(thisProc.ProcessName);
            if (otherProc.Length > 1)
            {
                IsFastStart = true;
                MessageBox.Show("RoboSync is already running", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Current.Shutdown();
            }
        }

        private bool ArgCheck(string arg, string s)
        {
            bool ret = false;
            ret |= arg.ToUpper().StartsWith("/" + s);
            ret &= !(arg.ToUpper().StartsWith("/" + s) && arg.ToUpper().EndsWith("-"));
            return ret;
        }
    }

}
