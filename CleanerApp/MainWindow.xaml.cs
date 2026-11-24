using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace CleanerApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp", "WebView2Data");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                // Mapping pour charger index.html et tailwindcss.min.js localement
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "cleaner.app",
                    appDir,
                    CoreWebView2HostResourceAccessKind.Allow
                );

                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Navigation vers la page d'accueil de l'application
                webView.CoreWebView2.Navigate("https://cleaner.app/index.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur d'initialisation de WebView2 : {ex.Message}", "Erreur Cleaner Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var jsonStr = e.WebMessageAsJson;
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                var action = root.GetProperty("action").GetString();

                switch (action)
                {
                    case "getDrives":
                    {
                        var drives = CleanerBackend.GetDrivesStatus();
                        SendJson(new { type = "drivesUpdate", drives });
                        break;
                    }

                    case "runAudit":
                    {
                        var items = await CleanerBackend.RunAuditAsync((msg, pct) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                SendJson(new { type = "auditProgress", text = msg, pct });
                            });
                        });
                        SendJson(new { type = "auditResult", items });
                        break;
                    }

                    case "researchItem":
                    {
                        var name = root.GetProperty("name").GetString() ?? "";
                        var path = root.GetProperty("path").GetString() ?? "";
                        var report = CleanerBackend.ResearchItem(name, path);
                        SendJson(new { type = "researchResult", report });
                        break;
                    }

                    case "executeClean":
                    {
                        var itemsJson = root.GetProperty("items").GetRawText();
                        var items = JsonSerializer.Deserialize<List<CleanerItem>>(itemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CleanerItem>();

                        double totalFreed = 0;
                        var deletedIds = new List<string>();

                        await Task.Run(() =>
                        {
                            foreach (var it in items)
                            {
                                var res = CleanerBackend.DeleteItem(it);
                                if (res.Success)
                                {
                                    totalFreed += res.FreedGB;
                                    deletedIds.Add(it.Id);
                                }
                            }
                        });

                        SendJson(new
                        {
                            type = "cleanResult",
                            freedGB = Math.Round(totalFreed, 2),
                            deletedIds
                        });
                        break;
                    }

                    case "openFolder":
                    {
                        var folderPath = root.GetProperty("path").GetString() ?? "";
                        if (File.Exists(folderPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{folderPath}\"",
                                UseShellExecute = true
                            });
                        }
                        else if (Directory.Exists(folderPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"\"{folderPath}\"",
                                UseShellExecute = true
                            });
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cleaner Bridge Error] {ex.Message}");
            }
        }

        private void SendJson(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                webView.CoreWebView2?.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SendJson Error] {ex.Message}");
            }
        }
    }
}
