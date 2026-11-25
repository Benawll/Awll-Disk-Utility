using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace Awll
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool isUninstall = e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
            if (isUninstall)
            {
                bool silent = e.Args.Any(a =>
                    a.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

                if (!silent)
                {
                    var res = System.Windows.MessageBox.Show(
                        "Voulez-vous désinstaller Awll Disk Utility ?\n\nVos préférences locales (clé IA, liste protégée, historique) dans AppData\\Local\\Awll sont conservées.",
                        "Désinstaller Awll Disk Utility",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        Shutdown(0);
                        return;
                    }
                }

                PerformUninstall();

                if (!silent)
                {
                    System.Windows.MessageBox.Show(
                        "Awll Disk Utility a été désinstallé avec succès.",
                        "Désinstaller Awll Disk Utility",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                Shutdown(0);
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private static void PerformUninstall()
        {
            int currentPid = Environment.ProcessId;
            foreach (var procName in new[] { "Awll-Disk-Utility", "Awll", "WorkspaceCleanerPro" })
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        try
                        {
                            if (p.Id == currentPid) continue;
                            if (!p.HasExited)
                            {
                                p.CloseMainWindow();
                                if (!p.WaitForExit(1500))
                                {
                                    p.Kill(true);
                                    p.WaitForExit(1500);
                                }
                            }
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }
                catch { }
            }

            try
            {
                var startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Start Menu", "Programs");
                var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                foreach (var lnk in new[]
                {
                    Path.Combine(startMenuDir, "Awll Disk Utility.lnk"),
                    Path.Combine(desktopDir, "Awll Disk Utility.lnk"),
                    Path.Combine(startMenuDir, "Awll.lnk"),
                    Path.Combine(desktopDir, "Awll.lnk"),
                    Path.Combine(startMenuDir, "Workspace Cleaner Pro.lnk"),
                    Path.Combine(desktopDir, "Workspace Cleaner Pro.lnk")
                })
                {
                    if (File.Exists(lnk)) File.Delete(lnk);
                }
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AwllDiskUtility", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Awll", false);
            }
            catch { }

            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                foreach (var assetsDir in new[]
                {
                    Path.Combine(localAppData, "Awll", "Assets"),
                    Path.Combine(localAppData, "CleanerApp", "Assets")
                })
                {
                    if (Directory.Exists(assetsDir))
                    {
                        Directory.Delete(assetsDir, true);
                    }
                }
            }
            catch { }

            try
            {
                var localPrograms = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs");
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var currentDir = (Path.GetDirectoryName(exePath) ?? "").TrimEnd('\\');

                foreach (var dirName in new[] { "Awll Disk Utility", "Awll" })
                {
                    var expectedInstallDir = Path.Combine(localPrograms, dirName).TrimEnd('\\');
                    if (!string.IsNullOrEmpty(currentDir) &&
                        currentDir.Equals(expectedInstallDir, StringComparison.OrdinalIgnoreCase))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{expectedInstallDir}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(psi);
                    }
                }
            }
            catch { }
        }
    }
}
