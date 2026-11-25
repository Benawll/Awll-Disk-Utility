using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AwllInstaller
{
    public static class InstallerEngine
    {
        public const string ResourceName = "AwllInstaller.Payload.Awll.exe.gz";
        private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Awll";

        public static string InstallDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Awll");

        public static string InstalledExePath => Path.Combine(InstallDir, "Awll.exe");

        public static string InstallerVersion
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoAttr != null && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion))
                {
                    var v = infoAttr.InformationalVersion.Split('+')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
                var ver = asm.GetName().Version;
                return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "5.0.0";
            }
        }

        public static string? DetectExistingVersion()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallRegPath);
                var regVer = key?.GetValue("DisplayVersion") as string;
                if (!string.IsNullOrWhiteSpace(regVer))
                {
                    if (File.Exists(InstalledExePath))
                    {
                        return regVer.Trim();
                    }
                    // Si la clé existe mais l'exe est absent ailleurs
                    var loc = key?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(loc) && File.Exists(Path.Combine(loc, "Awll.exe")))
                    {
                        return regVer.Trim();
                    }
                }
            }
            catch { }

            if (File.Exists(InstalledExePath))
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(InstalledExePath);
                    if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                    {
                        return fvi.ProductVersion.Split('+')[0].Trim();
                    }
                }
                catch { }
                return "existante";
            }

            return null;
        }

        public static Task InstallAsync(
            bool createStartMenu,
            bool createDesktop,
            Action<string, int> reportProgress)
        {
            return Task.Run(() =>
            {
                reportProgress("Fermeture des instances en cours...", 10);
                CloseRunningProcesses();

                reportProgress("Préparation du dossier d'installation...", 25);
                Directory.CreateDirectory(InstallDir);

                // Nettoyer l'ancien cache UI extrait tout en conservant les préférences JSON
                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    foreach (var oldAssetsDir in new[]
                    {
                        Path.Combine(localAppData, "Awll", "Assets"),
                        Path.Combine(localAppData, "CleanerApp", "Assets")
                    })
                    {
                        if (Directory.Exists(oldAssetsDir))
                        {
                            Directory.Delete(oldAssetsDir, true);
                        }
                    }
                }
                catch { }

                reportProgress("Extraction d'Awll...", 35);
                ExtractPayload(pct => reportProgress("Extraction d'Awll...", pct));

                reportProgress("Création des raccourcis...", 88);
                UpdateShortcuts(createStartMenu, createDesktop);

                reportProgress("Enregistrement dans Windows...", 95);
                WriteUninstallRegistry();

                reportProgress("Installation terminée.", 100);
            });
        }

        public static Task UninstallAsync(bool removeUserData, Action<string, int> reportProgress)
        {
            return Task.Run(() =>
            {
                reportProgress("Fermeture des instances d'Awll en cours...", 15);
                CloseRunningProcesses();

                reportProgress("Suppression des raccourcis...", 35);
                try
                {
                    var startMenuDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "Windows", "Start Menu", "Programs");
                    var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                    foreach (var lnk in new[]
                    {
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

                reportProgress("Nettoyage du Registre Windows...", 55);
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, false);
                }
                catch { }

                reportProgress("Suppression du programme installé...", 75);
                if (Directory.Exists(InstallDir))
                {
                    try
                    {
                        Directory.Delete(InstallDir, true);
                    }
                    catch
                    {
                        Thread.Sleep(400);
                        Directory.Delete(InstallDir, true);
                    }
                }

                reportProgress("Suppression du cache UI...", 90);
                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    foreach (var dir in new[]
                    {
                        Path.Combine(localAppData, "Awll", "Assets"),
                        Path.Combine(localAppData, "CleanerApp", "Assets")
                    })
                    {
                        if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    }

                    if (removeUserData)
                    {
                        foreach (var dataDir in new[]
                        {
                            Path.Combine(localAppData, "Awll"),
                            Path.Combine(localAppData, "CleanerApp")
                        })
                        {
                            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                        }
                    }
                }
                catch { }

                reportProgress("Désinstallation terminée.", 100);
            });
        }

        private static void CloseRunningProcesses()
        {
            int selfPid = Process.GetCurrentProcess().Id;
            bool closedAny = false;

            foreach (var procName in new[] { "Awll", "WorkspaceCleanerPro" })
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        try
                        {
                            if (p.Id == selfPid) continue;
                            closedAny = true;
                            if (!p.HasExited)
                            {
                                p.CloseMainWindow();
                                if (!p.WaitForExit(1500))
                                {
                                    p.Kill();
                                    p.WaitForExit(2000);
                                }
                            }
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }
                catch { }
            }

            if (closedAny)
            {
                Thread.Sleep(300);
            }
        }

        private static void ExtractPayload(Action<int> onPct)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var resStream = asm.GetManifestResourceStream(ResourceName);
            if (resStream == null)
            {
                throw new InvalidOperationException($"Ressource embarquée introuvable : {ResourceName}. Recompilez via scripts/build-release.ps1.");
            }

            string tempExePath = Path.Combine(InstallDir, "Awll.exe.new");
            if (File.Exists(tempExePath))
            {
                File.Delete(tempExePath);
            }

            long totalCompressed = resStream.CanSeek ? Math.Max(1L, resStream.Length) : 1L;
            int lastReported = 35;

            using (var gzip = new GZipStream(resStream, CompressionMode.Decompress, leaveOpen: true))
            using (var outFs = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outFs.Write(buffer, 0, read);
                    if (resStream.CanSeek)
                    {
                        int pct = 35 + (int)Math.Min(45, (resStream.Position * 45L) / totalCompressed);
                        if (pct > lastReported)
                        {
                            lastReported = pct;
                            onPct(pct);
                        }
                    }
                }
                outFs.Flush(true);
            }

            onPct(80);
            if (File.Exists(InstalledExePath))
            {
                File.Delete(InstalledExePath);
            }
            File.Move(tempExePath, InstalledExePath);
        }

        private static void UpdateShortcuts(bool createStartMenu, bool createDesktop)
        {
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs");
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var awllStartLnk = Path.Combine(startMenuDir, "Awll.lnk");
            var awllDeskLnk = Path.Combine(desktopDir, "Awll.lnk");

            // Nettoyer d'éventuels anciens raccourcis Workspace Cleaner Pro
            foreach (var legacy in new[]
            {
                Path.Combine(startMenuDir, "Workspace Cleaner Pro.lnk"),
                Path.Combine(desktopDir, "Workspace Cleaner Pro.lnk")
            })
            {
                try { if (File.Exists(legacy)) File.Delete(legacy); } catch { }
            }

            if (createStartMenu)
            {
                CreateShortcut(awllStartLnk, InstalledExePath, InstallDir, "Awll — Disk Utility");
            }
            if (createDesktop)
            {
                CreateShortcut(awllDeskLnk, InstalledExePath, InstallDir, "Awll — Disk Utility");
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workDir, string description)
        {
            var dir = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            object? shell = null;
            object? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                if (shortcut == null) return;

                var scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { $"{targetExe},0" });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }

        private static void WriteUninstallRegistry()
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegPath);
            if (key == null) return;

            int estSizeKb = 0;
            try
            {
                estSizeKb = (int)Math.Max(1L, new FileInfo(InstalledExePath).Length / 1024L);
            }
            catch { }

            key.SetValue("DisplayName", "Awll — Disk Utility", RegistryValueKind.String);
            key.SetValue("DisplayVersion", InstallerVersion, RegistryValueKind.String);
            key.SetValue("Publisher", "Benawll", RegistryValueKind.String);
            key.SetValue("InstallLocation", InstallDir, RegistryValueKind.String);
            key.SetValue("DisplayIcon", $"{InstalledExePath},0", RegistryValueKind.String);
            key.SetValue("UninstallString", $"\"{InstalledExePath}\" --uninstall", RegistryValueKind.String);
            key.SetValue("QuietUninstallString", $"\"{InstalledExePath}\" --uninstall /S", RegistryValueKind.String);
            key.SetValue("EstimatedSize", estSizeKb, RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
        }
    }
}
