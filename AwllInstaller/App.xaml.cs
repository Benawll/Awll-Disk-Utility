using System;
using System.Linq;
using System.Windows;

namespace AwllInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool silent = e.Args.Any(a =>
                a.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

            if (silent)
            {
                try
                {
                    InstallerEngine.InstallAsync(true, true, (_, _) => { }).GetAwaiter().GetResult();
                    Shutdown(0);
                }
                catch
                {
                    Shutdown(1);
                }
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
