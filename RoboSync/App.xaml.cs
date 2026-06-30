using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

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

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            for (int i = 0; i != e.Args.Length; ++i)
            {
                IsStartup |= e.Args[i].ToUpper().StartsWith("/S");
                IsSysTray |= e.Args[i].ToUpper().StartsWith("/T");
                IsFastStart |= e.Args[i].ToUpper().StartsWith("/F");
                IsMinimised |= e.Args[i].ToUpper().StartsWith("/M");
                IsRun |= e.Args[i].ToUpper().StartsWith("/R");
            }

            //Process thisProc = Process.GetCurrentProcess();
            //if (Process.GetProcessesByName(thisProc.ProcessName).Length > 1)
            //{
            //    MessageBox.Show("Application is already running");
            //    IsFastStart = true;
            //    Current.Shutdown();
            //}
        }
    }

}
