using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace AwllInstaller
{
    public partial class MainWindow : Window
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private enum UiStage { Ready, Installing, Complete }
        private UiStage _stage = UiStage.Ready;
        private string? _existingVersion;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ApplyInstallerTheme(IsWindowsDarkMode());
            Loaded += (_, _) => InitializeInstallerUi();
        }

        private static bool IsWindowsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                    return val == 0;
            }
            catch { }
            return false;
        }

        private void ApplyInstallerTheme(bool isDark)
        {
            try
            {
                if (isDark)
                {
                    Resources["InstBgBrush"] = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x12));
                    Resources["InstSurfaceBrush"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x1D));
                    Resources["InstSurface2Brush"] = new SolidColorBrush(Color.FromRgb(0x29, 0x29, 0x25));
                    Resources["InstSurface2HoverBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x2E));
                    Resources["InstTrackBrush"] = new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0xEC, 0xE5));
                    Resources["InstBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x14, 0xEF, 0xEC, 0xE5));
                    Resources["InstBorderStrongBrush"] = new SolidColorBrush(Color.FromArgb(0x2E, 0xEF, 0xEC, 0xE5));
                    Resources["InstTextBrush"] = new SolidColorBrush(Color.FromRgb(0xEF, 0xEC, 0xE5));
                    Resources["InstMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x9A, 0x96, 0x8C));
                    Resources["InstAccentBrush"] = new SolidColorBrush(Color.FromRgb(0xDF, 0x6E, 0x46));
                    Resources["InstAccentHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0x7A, 0x52));
                    Resources["InstAccentSoftBrush"] = new SolidColorBrush(Color.FromArgb(0x26, 0xDF, 0x6E, 0x46));
                    Resources["InstSafeBrush"] = new SolidColorBrush(Color.FromRgb(0x5B, 0x9A, 0x7D));
                    Resources["InstSafeSoftBrush"] = new SolidColorBrush(Color.FromArgb(0x24, 0x5B, 0x9A, 0x7D));
                    Resources["InstDangerBrush"] = new SolidColorBrush(Color.FromRgb(0xD1, 0x58, 0x50));
                    Resources["InstDangerSoftBrush"] = new SolidColorBrush(Color.FromArgb(0x24, 0xD1, 0x58, 0x50));
                    Resources["InstDangerHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x36, 0xD1, 0x58, 0x50));
                }

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int darkVal = isDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkVal, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref darkVal, sizeof(int));
                }

                // Windows COLORREF (0x00BBGGRR): #1B1B18 dark / #E7E4DC light
                int captionColor = isDark ? 0x00181B1B : 0x00DCE4E7;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
            catch { }
        }

        private void InitializeInstallerUi()
        {
            string targetVer = InstallerEngine.InstallerVersion;
            TxtSubtitle.Text = $"Disk Utility — v{targetVer}";
            TxtInstallPath.Text = InstallerEngine.InstallDir;

            _existingVersion = InstallerEngine.DetectExistingVersion();
            if (!string.IsNullOrWhiteSpace(_existingVersion))
            {
                TxtStatusBadge.Text = $"Mise à jour v{_existingVersion} → v{targetVer}";
                TxtStatusBadge.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "InstAccentBrush");
                BadgeBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "InstAccentSoftBrush");
                BadgeBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "InstAccentBrush");

                TxtReadyDesc.Text = $"Une installation existante (v{_existingVersion}) a été détectée. Vous pouvez la mettre à jour tout en conservant vos réglages locaux, ou la désinstaller complètement.";
                BtnAction.Content = $"Mettre à jour vers v{targetVer}";
                TxtCompleteTitle.Text = $"Awll mis à jour vers v{targetVer}";
                BtnUninstall.Visibility = Visibility.Visible;
                PanelUninstallOption.Visibility = Visibility.Visible;
            }
            else
            {
                TxtStatusBadge.Text = "Nouvelle installation";
                BtnAction.Content = "Installer Awll";
                TxtCompleteTitle.Text = $"Awll v{targetVer} est installé";
                BtnUninstall.Visibility = Visibility.Collapsed;
                PanelUninstallOption.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == UiStage.Installing) return;

            bool removeUserData = ChkRemoveUserData.IsChecked == true;
            string extraNote = removeUserData
                ? "Vos réglages et données locales (%LocalAppData%\\Awll) seront également supprimés."
                : "Vos réglages locaux (clé IA, liste protégée, historique) seront conservés.";

            var confirm = MessageBox.Show(
                $"Voulez-vous vraiment désinstaller Awll de ce PC ?\n\n{extraNote}",
                "Désinstaller Awll",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _stage = UiStage.Installing;
            ErrorBox.Visibility = Visibility.Collapsed;
            PanelReady.Visibility = Visibility.Collapsed;
            PanelProgress.Visibility = Visibility.Visible;
            TxtProgressNote.Text = "Fermeture d'Awll, suppression des raccourcis, du dossier programme et de l'entrée Windows Applications...";
            BtnUninstall.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnAction.IsEnabled = false;
            BtnAction.Content = "Désinstallation...";

            try
            {
                await InstallerEngine.UninstallAsync(removeUserData, (msg, pct) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtProgressStep.Text = msg;
                        TxtProgressPct.Text = $"{pct}%";
                        double trackWidth = 430.0;
                        if (ProgressFill.Parent is FrameworkElement track && track.ActualWidth > 0)
                        {
                            trackWidth = track.ActualWidth;
                        }
                        ProgressFill.Width = Math.Max(0, (pct / 100.0) * trackWidth);
                    });
                });

                _stage = UiStage.Complete;
                PanelProgress.Visibility = Visibility.Collapsed;
                PanelComplete.Visibility = Visibility.Visible;
                TxtCompleteTitle.Text = "Awll a été désinstallé";
                TxtCompleteDesc.Text = removeUserData
                    ? "L'application, ses raccourcis, son entrée Windows et toutes ses données locales ont été supprimés."
                    : "L'application, ses raccourcis et son entrée Windows ont été supprimés. Vos réglages locaux ont été conservés.";
                ChkLaunchNow.IsChecked = false;
                ChkLaunchNow.Visibility = Visibility.Collapsed;
                BtnUninstall.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Collapsed;
                BtnAction.IsEnabled = true;
                BtnAction.Content = "Terminer";
            }
            catch (Exception ex)
            {
                _stage = UiStage.Ready;
                PanelProgress.Visibility = Visibility.Collapsed;
                PanelReady.Visibility = Visibility.Visible;
                ErrorBox.Visibility = Visibility.Visible;
                TxtError.Text = $"Échec de la désinstallation : {ex.Message}";
                BtnUninstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                BtnAction.IsEnabled = true;
                BtnAction.Content = $"Mettre à jour vers v{InstallerEngine.InstallerVersion}";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == UiStage.Installing) return;
            Close();
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == UiStage.Installing) return;

            if (_stage == UiStage.Complete)
            {
                if (ChkLaunchNow.IsChecked == true && System.IO.File.Exists(InstallerEngine.InstalledExePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = InstallerEngine.InstalledExePath,
                            WorkingDirectory = InstallerEngine.InstallDir,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
                Close();
                return;
            }

            bool startMenu = ChkStartMenu.IsChecked == true;
            bool desktop = ChkDesktop.IsChecked == true;

            _stage = UiStage.Installing;
            ErrorBox.Visibility = Visibility.Collapsed;
            PanelReady.Visibility = Visibility.Collapsed;
            PanelProgress.Visibility = Visibility.Visible;
            TxtProgressNote.Text = "Si une ancienne version était ouverte, elle est fermée et remplacée proprement. Vos paramètres IA et votre liste protégée restent intacts.";
            BtnUninstall.Visibility = Visibility.Collapsed;
            BtnCancel.IsEnabled = false;
            BtnAction.IsEnabled = false;
            BtnAction.Content = "Installation...";

            try
            {
                await InstallerEngine.InstallAsync(startMenu, desktop, (msg, pct) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtProgressStep.Text = msg;
                        TxtProgressPct.Text = $"{pct}%";
                        double trackWidth = 430.0;
                        if (ProgressFill.Parent is FrameworkElement track && track.ActualWidth > 0)
                        {
                            trackWidth = track.ActualWidth;
                        }
                        ProgressFill.Width = Math.Max(0, (pct / 100.0) * trackWidth);
                    });
                });

                _stage = UiStage.Complete;
                PanelProgress.Visibility = Visibility.Collapsed;
                PanelComplete.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Collapsed;
                BtnAction.IsEnabled = true;
                BtnAction.Content = "Terminer";
            }
            catch (Exception ex)
            {
                _stage = UiStage.Ready;
                PanelProgress.Visibility = Visibility.Collapsed;
                PanelReady.Visibility = Visibility.Visible;
                ErrorBox.Visibility = Visibility.Visible;
                TxtError.Text = $"Échec de l'installation : {ex.Message}";
                BtnCancel.IsEnabled = true;
                BtnAction.IsEnabled = true;
                BtnAction.Content = string.IsNullOrWhiteSpace(_existingVersion)
                    ? "Réessayer l'installation"
                    : $"Réessayer la mise à jour v{InstallerEngine.InstallerVersion}";
                if (!string.IsNullOrWhiteSpace(_existingVersion))
                {
                    BtnUninstall.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
