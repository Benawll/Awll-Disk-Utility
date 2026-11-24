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

                // Emplacement des assets UI (support standalone 100% autonome sur n'importe quel PC)
                var assetsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp", "Assets");
                Directory.CreateDirectory(assetsFolder);

                // Extraction des ressources intégrées si absentes ou si l'exe est déplacé seul
                ExtractEmbeddedAsset("CleanerApp.index.html", Path.Combine(assetsFolder, "index.html"));
                ExtractEmbeddedAsset("CleanerApp.tailwindcss.min.js", Path.Combine(assetsFolder, "tailwindcss.min.js"));

                var effectiveAssetsDir = File.Exists(Path.Combine(appDir, "index.html")) ? appDir : assetsFolder;

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                // Mapping pour charger index.html et tailwindcss.min.js localement
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "cleaner.app",
                    effectiveAssetsDir,
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

        private static void ExtractEmbeddedAsset(string resourceName, string targetPath)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                }
            }
            catch { }
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
                        SendJson(new { type = "auditResult", items, scanType = "smart" });
                        break;
                    }

                    case "runExhaustiveScan":
                    {
                        var items = await CleanerBackend.RunExhaustiveScanAsync((msg, pct) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                SendJson(new { type = "auditProgress", text = msg, pct });
                            });
                        });
                        SendJson(new { type = "auditResult", items, scanType = "exhaustive" });
                        break;
                    }

                    case "getProtected":
                    {
                        var items = CleanerBackend.GetProtectedItems();
                        SendJson(new { type = "protectedList", items });
                        break;
                    }

                    case "addProtected":
                    {
                        if (root.TryGetProperty("item", out var itemProp))
                        {
                            var it = JsonSerializer.Deserialize<ProtectedItem>(itemProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (it != null)
                            {
                                CleanerBackend.AddProtectedItem(it);
                            }
                        }
                        var items = CleanerBackend.GetProtectedItems();
                        SendJson(new { type = "protectedList", items });
                        break;
                    }

                    case "removeProtected":
                    {
                        var id = root.GetProperty("id").GetString() ?? "";
                        CleanerBackend.RemoveProtectedItem(id);
                        var items = CleanerBackend.GetProtectedItems();
                        SendJson(new { type = "protectedList", items });
                        break;
                    }

                    case "researchItem":
                    {
                        var name = root.GetProperty("name").GetString() ?? "";
                        var path = root.GetProperty("path").GetString() ?? "";
                        int risk = 5;
                        if (root.TryGetProperty("riskPct", out var rProp))
                        {
                            risk = rProp.GetInt32();
                        }
                        var report = CleanerBackend.ResearchItem(name, path, risk);
                        SendJson(new { type = "researchResult", report });
                        break;
                    }

                    case "researchItemAI":
                    {
                        var name = root.GetProperty("name").GetString() ?? "";
                        var path = root.GetProperty("path").GetString() ?? "";
                        int risk = 5;
                        if (root.TryGetProperty("riskPct", out var rProp))
                        {
                            risk = rProp.GetInt32();
                        }
                        string? customKey = null;
                        if (root.TryGetProperty("apiKey", out var kProp) && !string.IsNullOrWhiteSpace(kProp.GetString()))
                        {
                            customKey = kProp.GetString();
                        }
                        var report = await CleanerBackend.ResearchItemWithAIAsync(name, path, risk, customKey);
                        SendJson(new { type = "researchResult", report });
                        break;
                    }

                    case "getSettings":
                    {
                        var settings = CleanerBackend.GetSettings();
                        SendJson(new { type = "appSettings", settings });
                        break;
                    }

                    case "saveSettings":
                    {
                        if (root.TryGetProperty("settings", out var sProp))
                        {
                            var s = JsonSerializer.Deserialize<AppSettings>(sProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (s != null)
                            {
                                CleanerBackend.SaveSettings(s);
                            }
                        }
                        var current = CleanerBackend.GetSettings();
                        SendJson(new { type = "appSettings", settings = current });
                        break;
                    }

                    case "testGeminiApiKey":
                    {
                        var key = root.TryGetProperty("apiKey", out var kProp) ? (kProp.GetString() ?? "") : "";
                        var ok = await CleanerBackend.TestGeminiApiKeyAsync(key);
                        SendJson(new { type = "apiKeyTestResult", success = ok });
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

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private void SendJson(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, JsonOpts);
                webView.CoreWebView2?.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SendJson Error] {ex.Message}");
            }
        }
    }
}
