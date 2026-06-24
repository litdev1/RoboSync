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
        public static bool bHidden = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            for (int i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] == "/hide")
                {
                    bHidden = true;
                }
            }

            if (!bHidden)
            {
                SplashScreen splash = new SplashScreen("RoboSync.png");
                splash.Show(true, true);
            }
        }
    }

}
