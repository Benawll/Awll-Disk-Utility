using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;


namespace Awll
{
    public static partial class CleanerBackend
    {
        #region PARAMÈTRES DE L'APPLICATION (MODE IA & CONFIG)

        private static bool _legacyMigrated = false;
        private static readonly object _migrationLock = new();

        internal static string GetAppDataDir()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var awllDir = Path.Combine(localAppData, "Awll");
            Directory.CreateDirectory(awllDir);

            if (!_legacyMigrated)
            {
                lock (_migrationLock)
                {
                    if (!_legacyMigrated)
                    {
                        _legacyMigrated = true;
                        try
                        {
                            var legacyDir = Path.Combine(localAppData, "CleanerApp");
                            if (Directory.Exists(legacyDir))
                            {
                                foreach (var src in Directory.GetFiles(legacyDir, "*.json"))
                                {
                                    var dest = Path.Combine(awllDir, Path.GetFileName(src));
                                    if (!File.Exists(dest))
                                    {
                                        File.Copy(src, dest, false);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            return awllDir;
        }

        private static string GetSettingsFilePath()
        {
            var folder = GetAppDataDir();
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        public static AppSettings GetSettings()
        {
            try
            {
                var file = GetSettingsFilePath();
                if (!File.Exists(file))
                {
                    var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                    return new AppSettings { GeminiApiKey = envKey, EnableAI = !string.IsNullOrEmpty(envKey) };
                }
                var json = File.ReadAllText(file);
                var s = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();
                if (string.IsNullOrEmpty(s.GeminiApiKey))
                {
                    s.GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                }
                return s;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var file = GetSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch { }
        }

        private static string GetAiCacheFilePath()
        {
            var folder = GetAppDataDir();
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "ai_cache.json");
        }

        private static Dictionary<string, ResearchReport> LoadAiCache()
        {
            try
            {
                var file = GetAiCacheFilePath();
                if (!File.Exists(file)) return new Dictionary<string, ResearchReport>(StringComparer.OrdinalIgnoreCase);
                var json = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, ResearchReport>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dict != null ? new Dictionary<string, ResearchReport>(dict, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, ResearchReport>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, ResearchReport>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveAiCacheEntry(ResearchReport report)
        {
            if (report == null || string.IsNullOrWhiteSpace(report.ItemPath)) return;
            try
            {
                var cache = LoadAiCache();
                cache[report.ItemPath.Trim()] = report;
                var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetAiCacheFilePath(), json);
            }
            catch { }
        }

        #endregion

        #region GESTION DE LA LISTE PROTÉGÉE (PERSISTANTE SUR DISQUE)

        public static List<string> GetAiCachedPaths()
        {
            var list = new List<string>();
            try
            {
                var cache = LoadAiCache();
                long maxAgeMs = 30L * 24 * 60 * 60 * 1000;
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                foreach (var kv in cache)
                {
                    if (kv.Value == null) continue;
                    bool expired = kv.Value.CachedAtUnixMs > 0 && (nowMs - kv.Value.CachedAtUnixMs) > maxAgeMs;
                    if (!expired) list.Add(kv.Key);
                }
            }
            catch { }
            return list;
        }

        private static string GetProtectedFilePath()
        {
            var folder = GetAppDataDir();
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "protected_items.json");
        }

        public static List<ProtectedItem> GetProtectedItems()
        {
            try
            {
                var file = GetProtectedFilePath();
                if (!File.Exists(file)) return new List<ProtectedItem>();
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<List<ProtectedItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProtectedItem>();
            }
            catch
            {
                return new List<ProtectedItem>();
            }
        }

        public static void SaveProtectedItems(List<ProtectedItem> list)
        {
            try
            {
                var file = GetProtectedFilePath();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch { }
        }

        public static void AddProtectedItem(ProtectedItem item)
        {
            var list = GetProtectedItems();
            if (!list.Any(x => (!string.IsNullOrEmpty(item.Path) && x.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(item.Name) && x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))))
            {
                list.Add(item);
                SaveProtectedItems(list);
            }
        }

        public static bool RemoveProtectedItem(string idOrPath)
        {
            var list = GetProtectedItems();
            var countBefore = list.Count;
            list.RemoveAll(x => x.Id.Equals(idOrPath, StringComparison.OrdinalIgnoreCase) ||
                                x.Path.Equals(idOrPath, StringComparison.OrdinalIgnoreCase) ||
                                x.Name.Equals(idOrPath, StringComparison.OrdinalIgnoreCase));
            if (list.Count != countBefore)
            {
                SaveProtectedItems(list);
                return true;
            }
            return false;
        }

        public static bool IsProtected(string path, string name)
        {
            var list = GetProtectedItems();
            return list.Any(x => (!string.IsNullOrEmpty(path) && x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(name) && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }

        #endregion


        #region HISTORIQUE DE NETTOYAGE

        private static string GetHistoryFilePath()
        {
            var folder = GetAppDataDir();
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "clean_history.json");
        }

        public static List<CleanHistoryEntry> GetCleanHistory()
        {
            try
            {
                var file = GetHistoryFilePath();
                if (!File.Exists(file)) return new List<CleanHistoryEntry>();
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<List<CleanHistoryEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CleanHistoryEntry>();
            }
            catch { return new List<CleanHistoryEntry>(); }
        }

        public static void AddCleanHistoryEntry(int count, double freedGB, List<string> names)
        {
            if (count <= 0) return;
            try
            {
                var list = GetCleanHistory();
                list.Insert(0, new CleanHistoryEntry
                {
                    Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    ItemsCount = count,
                    FreedGB = Math.Round(freedGB, 2),
                    ItemNames = names.Take(30).ToList()
                });
                if (list.Count > 50) list = list.Take(50).ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetHistoryFilePath(), json);
            }
            catch { }
        }

        public static void ClearCleanHistory()
        {
            try
            {
                var file = GetHistoryFilePath();
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }
        }

        #endregion
    }
}