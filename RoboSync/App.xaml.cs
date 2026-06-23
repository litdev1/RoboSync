using System.Configuration;
using System.Data;
using System.Windows;

namespace RoboSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            for (int i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] == "/RunAll")
                {
                }
            }

            SplashScreen splash = new SplashScreen("RoboSync.png");
            splash.Show(true, true);
        }
    }

}
