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

namespace CleanerApp
{
    public class DriveStatus
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
        public double PctUsed { get; set; }
    }

    public class AppSettings
    {
        public string GeminiApiKey { get; set; } = "";
        public bool EnableAI { get; set; } = true;
    }

    public class CleanerItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Category { get; set; } = ""; // games, system, downloads, apps
        public string Drive { get; set; } = "";
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public int RiskPct { get; set; }
        public string RiskLevel { get; set; } = "Quasi Nul";
        public string Description { get; set; } = "";
        public string LastModified { get; set; } = "";
        public long LastModifiedTimestamp { get; set; }
        public int AgeDays { get; set; }
        public string UninstallString { get; set; } = "";
        public bool IsFolder { get; set; }
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
        public bool IsExtra { get; set; }
    }

    public class CleanHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        public int ItemsCount { get; set; }
        public double FreedGB { get; set; }
        public List<string> ItemNames { get; set; } = new();
    }

    public class ProtectedItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Category { get; set; } = "";
        public string Drive { get; set; } = "";
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public string ProtectedDate { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        public string Description { get; set; } = "";
    }

    public class ResearchReport
    {
        public string ItemName { get; set; } = "";
        public string ItemPath { get; set; } = "";
        public string Definition { get; set; } = "";
        public string Origin { get; set; } = "";
        public string UsageActivity { get; set; } = "";
        public string SafetyImpact { get; set; } = "";
        public int RiskPct { get; set; }
        public bool IsRunning { get; set; }
        public string LastAccessDate { get; set; } = "";
        public bool IsAiEnriched { get; set; }
        public string AiModelUsed { get; set; } = "";
    }

    public class CleanResult
    {
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public double FreedGB { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class FolderNode
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public double PctOfParent { get; set; }
        public bool IsFolder { get; set; }
        public int FileCount { get; set; }
        public string LastModified { get; set; } = "";
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
        public List<FolderNode> Children { get; set; } = new();
    }

    public static class CleanerBackend
    {
        // Protection dynamique et adaptative des dossiers personnels et composants critiques (100% portable sur tout PC)
        private static List<string> GetSanctuarizedRoots()
        {
            var list = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NVIDIA", "NGX"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java")
            };
            return list.Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        public static List<DriveStatus> GetDrivesStatus()
        {
            var list = new List<DriveStatus>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    double total = Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 2);
                    double free = Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2);
                    double used = Math.Round(total - free, 2);
                    double pct = total > 0 ? Math.Round((used / total) * 100.0, 1) : 0;

                    list.Add(new DriveStatus
                    {
                        Name = drive.Name.TrimEnd('\\'),
                        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Disque Local" : drive.VolumeLabel,
                        TotalGB = total,
                        FreeGB = free,
                        UsedGB = used,
                        PctUsed = pct
                    });
                }
            }
            return list;
        }

        private static bool IsDriveSelected(string path, HashSet<string>? targetDrives)
        {
            if (targetDrives == null || targetDrives.Count == 0) return true;
            try
            {
                var root = Path.GetPathRoot(path)?.TrimEnd('\\') ?? "";
                if (string.IsNullOrEmpty(root)) return true;
                return targetDrives.Contains(root);
            }
            catch { return true; }
        }

        private static List<DriveInfo> GetTargetDrives(HashSet<string>? targetDrives)
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Where(d => targetDrives == null || targetDrives.Count == 0 || targetDrives.Contains(d.Name.TrimEnd('\\')))
                .ToList();
        }

        public static bool IsCoreSystemOrRuntime(string name, string path)
        {
            var n = (name + " " + path).ToLowerInvariant();
            string[] coreKeywords =
            {
                "microsoft visual c++", "visual c++", "vcredist", ".net runtime", ".net sdk", "asp.net core", "windows desktop runtime",
                "webview2", "directx", "vulkan", "nvidia graphics", "nvidia physx", "nvidia driver", "geforce experience", "nvidia app",
                "amd software", "amd chipset", "realtek", "intel graphics", "intel(r)", "bluetooth", "wi-fi", "wifi", "ethernet",
                "java 17", "java 24", "jdk-17", "jdk-24", "temurin-17", "temurin-24", "openjdk",
                "steam", "epic games launcher", "battle.net", "ubisoft connect", "ea app", "riot client",
                "discord", "visual studio code", "git", "windows terminal", "powershell", "7-zip", "winrar"
            };
            // Attention: "steam" ne doit pas protéger les démos ou dossiers downloading/shadercache/workshop
            if (n.Contains("downloading") || n.Contains("shadercache") || n.Contains("workshop") || n.Contains("demo") || n.Contains("playtest") || n.Contains("pts"))
                return false;

            return coreKeywords.Any(k => n.Contains(k));
        }

        public static string FormatAgeText(int daysAgo)
        {
            if (daysAgo <= 0) return "aujourd'hui";
            if (daysAgo == 1) return "hier";
            if (daysAgo < 30) return $"il y a {daysAgo} jours";
            if (daysAgo < 365)
            {
                int months = Math.Max(1, daysAgo / 30);
                return $"il y a {months} mois";
            }
            int years = daysAgo / 365;
            return $"il y a {years} an{(years > 1 ? "s" : "")}";
        }

        public static int ComputeRiskScore(string kind, int daysAgo, bool isRunning, bool isCore, bool isSanctuarized)
        {
            if (isSanctuarized) return 95;

            int score = kind switch
            {
                "pure_cache" => 0,          // DXCache, NuGet, Gradle, Maven, Playwright, Temp, CrashDumps, downloading, *-updater
                "installer" => daysAgo >= 7 ? 0 : 5,
                "archive" => daysAgo >= 90 ? 0 : (daysAgo >= 30 ? 5 : 15),
                "demo_pts" => daysAgo >= 120 ? 5 : 20,
                "duplicate_game" => 5,
                "emulator" => daysAgo >= 365 ? 5 : (daysAgo >= 120 ? 15 : 35),
                "portable_game" => daysAgo >= 730 ? 5 : (daysAgo >= 365 ? 10 : (daysAgo >= 120 ? 20 : 40)),
                "steam_epic_game" => daysAgo >= 1095 ? 10 : (daysAgo >= 365 ? 20 : (daysAgo >= 120 ? 35 : 60)),
                "appdata_cache" => 0,
                "appdata_inactive" => daysAgo >= 730 ? 5 : (daysAgo >= 365 ? 10 : 20),
                "installed_app" => isCore ? 90 : (daysAgo >= 1095 ? 5 : (daysAgo >= 365 ? 15 : (daysAgo >= 180 ? 30 : 55))),
                _ => daysAgo >= 730 ? 10 : (daysAgo >= 180 ? 25 : 45)
            };

            if (isCore && kind != "pure_cache" && kind != "appdata_cache")
            {
                score = Math.Max(score, 85);
            }

            if (isRunning)
            {
                score = Math.Max(score + 25, 55);
            }

            return Math.Clamp(score, 0, 98);
        }

        public static async Task<List<CleanerItem>> RunAuditAsync(Action<string, int>? progressCallback = null, List<string>? selectedDrives = null)
        {
            return await Task.Run(() =>
            {
                HashSet<string>? targetDrives = null;
                if (selectedDrives != null && selectedDrives.Count > 0)
                {
                    targetDrives = new HashSet<string>(selectedDrives.Select(d => d.TrimEnd('\\')), StringComparer.OrdinalIgnoreCase);
                }

                var runningProcs = GetRunningProcessNamesSafe();
                var recommendedItems = new List<CleanerItem>();
                var extraItems = new List<CleanerItem>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Découverte dynamique de Steam & Epic Games
                progressCallback?.Invoke("Détection des bibliothèques de jeux...", 10);
                var steamLibs = DiscoverSteamLibraries().Where(p => IsDriveSelected(p, targetDrives)).ToList();
                var epicGames = DiscoverEpicGames().Where(kv => IsDriveSelected(kv.Value, targetDrives)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

                // 2. Audit Recommandé : Caches de téléchargement Steam, workshop, shadercache, démos abandonnées, doublons
                progressCallback?.Invoke("Scan des caches de jeux et reliquats...", 25);
                AuditGamesUniversal(recommendedItems, steamLibs, epicGames, targetDrives, runningProcs);
                foreach (var it in recommendedItems) seenPaths.Add(it.Path);

                // 3. Audit Recommandé : Caches système, GPU Shaders et outils de build / IA
                progressCallback?.Invoke("Scan des caches système, GPU, IA et builds dev...", 42);
                AuditSystemUniversal(recommendedItems, targetDrives, runningProcs);
                foreach (var it in recommendedItems) seenPaths.Add(it.Path);

                // 4. Audit Recommandé : Téléchargements et disques secondaires (archives, installeurs)
                progressCallback?.Invoke("Analyse des fichiers téléchargés et archives...", 58);
                AuditDownloadsUniversal(recommendedItems, targetDrives);
                foreach (var it in recommendedItems) seenPaths.Add(it.Path);

                // 5. Inventaire Complet : Autres jeux et logiciels installés
                //    Les éléments inutilisés depuis >= 365 jours (et non critiques) sont promus dans l'Audit Recommandé !
                progressCallback?.Invoke("Inventaire des jeux et logiciels installés (avec analyse d'inactivité)...", 72);
                DiscoverInstalledGamesAndApps(recommendedItems, extraItems, steamLibs, epicGames, seenPaths, seenNames, targetDrives, runningProcs);

                // 6. Découverte Dynamique des Dossiers Racines atypiques sur tous les disques (jeux portables, émulateurs, caches...)
                progressCallback?.Invoke("Analyse dynamique des dossiers racines multi-disques...", 86);
                AuditDriveRootsDynamic(recommendedItems, extraItems, seenPaths, targetDrives, runningProcs);

                // 7. Tri par défaut : Recommandés par Risque croissant puis Taille décroissante ; Extra par Taille décroissante
                var orderedRecommended = recommendedItems
                    .Where(x => IsDriveSelected(x.Path, targetDrives))
                    .OrderBy(x => x.RiskPct)
                    .ThenByDescending(x => x.SizeGB)
                    .ToList();

                var orderedExtra = extraItems
                    .Where(x => IsDriveSelected(x.Path, targetDrives))
                    .OrderBy(x => x.RiskPct)
                    .ThenByDescending(x => x.SizeGB)
                    .ToList();

                var allCombined = orderedRecommended.Concat(orderedExtra).ToList();

                // 8. Application de la liste protégée permanente
                progressCallback?.Invoke("Application de la liste protégée...", 95);
                var protectedList = GetProtectedItems();
                foreach (var it in allCombined)
                {
                    it.IsProtected = protectedList.Any(p => p.Path.Equals(it.Path, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(it.Name, StringComparison.OrdinalIgnoreCase));
                }

                progressCallback?.Invoke("Audit et inventaire terminés !", 100);
                return allCombined;
            });
        }

        private static HashSet<string> GetRunningProcessNamesSafe()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { set.Add(p.ProcessName); } catch { }
                }
            }
            catch { }
            return set;
        }

        private static bool IsLikelyRunning(string name, string path, HashSet<string> runningProcs)
        {
            if (runningProcs.Count == 0) return false;
            var baseName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(baseName) && baseName.Length >= 3 && runningProcs.Any(p => p.Contains(baseName, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (name.Contains("Steam", StringComparison.OrdinalIgnoreCase) && !name.Contains("downloading", StringComparison.OrdinalIgnoreCase) && !name.Contains("Démo", StringComparison.OrdinalIgnoreCase) && runningProcs.Contains("steam"))
                return true;
            if (name.Contains("Discord", StringComparison.OrdinalIgnoreCase) && runningProcs.Contains("Discord"))
                return true;
            if ((name.Contains("SignalRGB", StringComparison.OrdinalIgnoreCase) || name.Contains("Whirlwind", StringComparison.OrdinalIgnoreCase) || name.Contains("Vortx", StringComparison.OrdinalIgnoreCase)) &&
                runningProcs.Any(p => p.Contains("SignalRgb", StringComparison.OrdinalIgnoreCase) || p.Contains("Vortx", StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        // =========================================================================
        // DÉCOUVERTE DE TOUS LES JEUX & LOGICIELS INSTALLÉS
        // =========================================================================
        public static void DiscoverInstalledGamesAndApps(
            List<CleanerItem> recommendedItems,
            List<CleanerItem> extraItems,
            List<string> steamLibs,
            Dictionary<string, string> epicGames,
            HashSet<string> seenPaths,
            HashSet<string> seenNames,
            HashSet<string>? targetDrives,
            HashSet<string> runningProcs)
        {
            // A. Jeux et démos Steam installés
            foreach (var lib in steamLibs)
            {
                var common = Path.Combine(lib, "steamapps", "common");
                if (Directory.Exists(common))
                {
                    foreach (var gameDir in Directory.GetDirectories(common))
                    {
                        var di = new DirectoryInfo(gameDir);
                        if (seenPaths.Add(gameDir))
                        {
                            double size = GetDirectorySizeSafe(gameDir);
                            var steamInfo = GetSteamAppInfo(gameDir);
                            var last = steamInfo?.LastPlayed ?? steamInfo?.LastUpdated ?? GetLastWriteTimeSafe(di);
                            int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                            var drive = Path.GetPathRoot(gameDir)?.TrimEnd('\\') ?? "C:";
                            string appTitle = steamInfo?.Name ?? di.Name;
                            bool isDemo = appTitle.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
                                          appTitle.Contains("playtest", StringComparison.OrdinalIgnoreCase);

                            bool isRunning = IsLikelyRunning(appTitle, gameDir, runningProcs);
                            bool promoteToRecommended = daysAgo >= 365 && !isRunning;
                            int risk = ComputeRiskScore(isDemo ? "demo_pts" : "steam_epic_game", daysAgo, isRunning, false, false);

                            string activityDetail = steamInfo?.LastPlayed != null
                                ? $"Dernière partie : {steamInfo.LastPlayed.Value:dd/MM/yyyy} ({FormatAgeText(daysAgo)})"
                                : $"Dernière mise à jour : {last:dd/MM/yyyy} ({FormatAgeText(daysAgo)})";

                            string desc = promoteToRecommended
                                ? $"Jeu Steam inutilisé depuis {FormatAgeText(daysAgo)} sur {drive} ({activityDetail})"
                                : (isDemo ? $"Démo Steam récente sur {drive} ({activityDetail})" : $"Jeu Steam sur {drive} ({activityDetail})");

                            var item = CreateItem(appTitle, gameDir, "games", size, risk, desc, lastModified: last, isExtra: !promoteToRecommended);
                            if (promoteToRecommended) recommendedItems.Add(item);
                            else extraItems.Add(item);

                            seenNames.Add(di.Name);
                            if (!string.IsNullOrEmpty(steamInfo?.Name)) seenNames.Add(steamInfo.Name);
                        }
                    }
                }
            }

            // B. Jeux Epic Games
            foreach (var eg in epicGames)
            {
                if (seenPaths.Add(eg.Value))
                {
                    double size = GetDirectorySizeSafe(eg.Value);
                    var di = new DirectoryInfo(eg.Value);
                    var last = GetLastWriteTimeSafe(di);
                    int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                    var drive = Path.GetPathRoot(eg.Value)?.TrimEnd('\\') ?? "C:";
                    bool isRunning = IsLikelyRunning(eg.Key, eg.Value, runningProcs);
                    bool promoteToRecommended = daysAgo >= 365 && !isRunning;
                    int risk = ComputeRiskScore("steam_epic_game", daysAgo, isRunning, false, false);

                    string desc = promoteToRecommended
                        ? $"Jeu Epic Games inutilisé depuis {FormatAgeText(daysAgo)} sur {drive} ({last:dd/MM/yyyy})"
                        : $"Jeu Epic Games sur {drive} (Activité : {last:dd/MM/yyyy})";

                    var item = CreateItem(eg.Key, eg.Value, "games", size, risk, desc, lastModified: last, isExtra: !promoteToRecommended);
                    if (promoteToRecommended) recommendedItems.Add(item);
                    else extraItems.Add(item);

                    seenNames.Add(eg.Key);
                }
            }

            // C. Launchers, XboxGames et répertoires de jeux portables sur tous les disques sélectionnés
            foreach (var drive in GetTargetDrives(targetDrives))
            {
                var gameFolders = new[] { "Autres Jeux", "Jeux", "Games", "Old Games", "Riot Games", "GOG Games", "Ubisoft", "Battle.net", "XboxGames" };
                foreach (var gf in gameFolders)
                {
                    var full = Path.Combine(drive.Name, gf);
                    if (Directory.Exists(full))
                    {
                        foreach (var sub in Directory.GetDirectories(full))
                        {
                            if (seenPaths.Add(sub))
                            {
                                var di = new DirectoryInfo(sub);
                                double size = GetDirectorySizeSafe(sub);
                                if (size < 20 * 1024 * 1024) continue;
                                var last = GetLastWriteTimeSafe(di);
                                int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                                bool isRunning = IsLikelyRunning(di.Name, sub, runningProcs);
                                bool promoteToRecommended = daysAgo >= 365 && !isRunning;
                                int risk = ComputeRiskScore("portable_game", daysAgo, isRunning, false, false);

                                string desc = promoteToRecommended
                                    ? $"Jeu ({gf}) inutilisé depuis {FormatAgeText(daysAgo)} sur {drive.Name.TrimEnd('\\')} ({last:dd/MM/yyyy})."
                                    : $"Jeu ou composant ({gf}) sur {drive.Name.TrimEnd('\\')} (Activité : {last:dd/MM/yyyy}).";

                                var item = CreateItem($"{di.Name} ({gf} {drive.Name.TrimEnd('\\')})", sub, "games", size, risk, desc, lastModified: last, isExtra: !promoteToRecommended);
                                if (promoteToRecommended) recommendedItems.Add(item);
                                else extraItems.Add(item);

                                seenNames.Add(di.Name);
                            }
                        }
                    }
                }
            }

            // D. Applications enregistrées dans le Registre Windows
            var regApps = GetRegistryInstalledApps(seenPaths, seenNames, targetDrives, runningProcs);
            foreach (var app in regApps)
            {
                if (!app.IsExtra) recommendedItems.Add(app);
                else extraItems.Add(app);
            }

            // E. Scan complémentaire des répertoires Program Files multi-disques
            foreach (var drive in GetTargetDrives(targetDrives))
            {
                foreach (var pf in new[] { "Program Files", "Program Files (x86)" })
                {
                    var pfp = Path.Combine(drive.Name, pf);
                    if (Directory.Exists(pfp))
                    {
                        foreach (var sub in Directory.GetDirectories(pfp))
                        {
                            var di = new DirectoryInfo(sub);
                            if (di.Name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("Common Files", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("Windows NT", StringComparison.OrdinalIgnoreCase)) continue;

                            if (seenPaths.Add(sub) && !seenNames.Contains(di.Name))
                            {
                                double size = GetDirectorySizeSafe(sub);
                                if (size > 30 * 1024 * 1024) // > 30 Mo
                                {
                                    var last = GetLastWriteTimeSafe(di);
                                    int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                                    bool isCore = IsCoreSystemOrRuntime(di.Name, sub);
                                    bool isRunning = IsLikelyRunning(di.Name, sub, runningProcs);
                                    bool isSanct = IsPathSanctuarized(sub);
                                    bool promoteToRecommended = daysAgo >= 365 && !isCore && !isRunning && !isSanct;
                                    int risk = ComputeRiskScore("installed_app", daysAgo, isRunning, isCore, isSanct);

                                    string desc = promoteToRecommended
                                        ? $"Logiciel dans {pf} inutilisé depuis {FormatAgeText(daysAgo)} ({last:dd/MM/yyyy})."
                                        : $"Logiciel installé dans {pfp} (Activité : {last:dd/MM/yyyy}).";

                                    var item = CreateItem(di.Name, sub, "apps", size, risk, desc, lastModified: last, isSanctuarized: isSanct, isExtra: !promoteToRecommended);
                                    if (promoteToRecommended) recommendedItems.Add(item);
                                    else extraItems.Add(item);

                                    seenNames.Add(di.Name);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IsPathSanctuarized(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return GetSanctuarizedRoots().Any(sp => path.StartsWith(sp, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<List<CleanerItem>> RunExhaustiveScanAsync(Action<string, int>? progressCallback = null, List<string>? selectedDrives = null)
        {
            return await RunAuditAsync(progressCallback, selectedDrives);
        }

        private static string ExtractExeFromCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return "";
            var trimmed = cmd.Trim();
            if (trimmed.StartsWith("\""))
            {
                int endQuote = trimmed.IndexOf('"', 1);
                if (endQuote > 1) return trimmed.Substring(1, endQuote - 1);
            }
            var exeIdx = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) return trimmed.Substring(0, exeIdx + 4);
            return "";
        }

        private static List<CleanerItem> GetRegistryInstalledApps(HashSet<string> seenPaths, HashSet<string> seenNames, HashSet<string>? targetDrives, HashSet<string> runningProcs)
        {
            var list = new List<CleanerItem>();
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');

            var keys = new[]
            {
                (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, subKeyPath) in keys)
            {
                try
                {
                    using var baseKey = hive.OpenSubKey(subKeyPath);
                    if (baseKey == null) continue;

                    foreach (var subName in baseKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var appKey = baseKey.OpenSubKey(subName);
                            if (appKey == null) continue;

                            var sysComp = appKey.GetValue("SystemComponent");
                            if (sysComp is int sc && sc == 1) continue;

                            var parentKey = appKey.GetValue("ParentKeyName");
                            if (parentKey != null) continue;

                            var displayName = appKey.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName)) continue;

                            if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                                displayName.Contains("Security Update", StringComparison.OrdinalIgnoreCase)) continue;

                            if (!seenNames.Add(displayName)) continue;

                            var installLoc = appKey.GetValue("InstallLocation") as string;
                            var uninstallStr = (appKey.GetValue("QuietUninstallString") as string) ?? (appKey.GetValue("UninstallString") as string) ?? "";
                            var displayIcon = appKey.GetValue("DisplayIcon") as string;
                            var publisher = appKey.GetValue("Publisher") as string ?? "Éditeur inconnu";
                            var version = appKey.GetValue("DisplayVersion") as string ?? "";
                            var installDateRaw = appKey.GetValue("InstallDate") as string ?? "";

                            string effectivePath = "";
                            string iconFile = "";
                            if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                            {
                                effectivePath = installLoc;
                            }
                            else if (!string.IsNullOrEmpty(displayIcon))
                            {
                                iconFile = displayIcon.Split(',')[0].Trim('"');
                                if (File.Exists(iconFile))
                                {
                                    var dir = Path.GetDirectoryName(iconFile) ?? "";
                                    if (!dir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        effectivePath = dir;
                                    }
                                }
                            }

                            bool isMsi = uninstallStr.Contains("msiexec", StringComparison.OrdinalIgnoreCase);
                            string extractedExe = ExtractExeFromCommand(uninstallStr);

                            if (string.IsNullOrEmpty(effectivePath) && !string.IsNullOrEmpty(extractedExe) && !isMsi)
                            {
                                if (File.Exists(extractedExe))
                                {
                                    var exeDir = Path.GetDirectoryName(extractedExe) ?? "";
                                    if (!string.IsNullOrEmpty(exeDir) && !exeDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        effectivePath = exeDir;
                                    }
                                }
                                else
                                {
                                    // Trace registre orpheline : le logiciel n'existe plus sur le disque -> proposer le nettoyage de l'entrée fantôme
                                    if (IsDriveSelected(extractedExe, targetDrives))
                                    {
                                        string hivePrefix = hive == Registry.CurrentUser ? "HKCU" : "HKLM";
                                        string regPath = $@"{hivePrefix}\{subKeyPath}\{subName}";
                                        DateTime orphanDate = DateTime.TryParseExact(installDateRaw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var od) ? od : DateTime.Now.AddYears(-1);
                                        var orphanItem = CreateItem(
                                            $"Trace registre orpheline : {displayName}",
                                            regPath,
                                            "system",
                                            0,
                                            0,
                                            $"Application déjà supprimée du disque ({extractedExe}), seule une trace fantôme subsiste dans le Registre Windows.",
                                            isFolder: false,
                                            lastModified: orphanDate,
                                            isExtra: false
                                        );
                                        try { orphanItem.Drive = Path.GetPathRoot(extractedExe)?.TrimEnd('\\') ?? "C:"; } catch { orphanItem.Drive = "C:"; }
                                        list.Add(orphanItem);
                                    }
                                    continue;
                                }
                            }

                            var pathForDisplay = !string.IsNullOrEmpty(effectivePath) ? effectivePath : (string.IsNullOrEmpty(uninstallStr) ? "Registre Windows" : uninstallStr);
                            if (!IsDriveSelected(pathForDisplay, targetDrives)) continue;

                            if (!string.IsNullOrEmpty(effectivePath))
                            {
                                seenPaths.Add(effectivePath);
                            }

                            double sizeBytes = 0;
                            DateTime lastActivity = DateTime.Now;
                            bool hasKnownDate = false;

                            if (!string.IsNullOrEmpty(effectivePath) && Directory.Exists(effectivePath))
                            {
                                sizeBytes = GetDirectorySizeSafe(effectivePath);
                                lastActivity = GetLastWriteTimeSafe(new DirectoryInfo(effectivePath));
                                hasKnownDate = true;
                            }
                            else
                            {
                                var estSize = appKey.GetValue("EstimatedSize");
                                if (estSize is int esInt && esInt > 0) sizeBytes = esInt * 1024.0;
                                else if (estSize is long esLong && esLong > 0) sizeBytes = esLong * 1024.0;

                                if (DateTime.TryParseExact(installDateRaw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedInstall))
                                {
                                    lastActivity = parsedInstall;
                                    hasKnownDate = true;
                                }
                                else if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                                {
                                    lastActivity = GetFileEffectiveActivityDate(new FileInfo(iconFile));
                                    hasKnownDate = true;
                                }
                            }

                            int daysAgo = hasKnownDate ? Math.Max(0, (int)(DateTime.Now - lastActivity).TotalDays) : 0;
                            bool isCore = IsCoreSystemOrRuntime(displayName, pathForDisplay) || (publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) && displayName.Contains("Runtime", StringComparison.OrdinalIgnoreCase));
                            bool isRunning = IsLikelyRunning(displayName, pathForDisplay, runningProcs);
                            bool isSanct = IsPathSanctuarized(effectivePath);

                            bool promoteToRecommended = hasKnownDate && daysAgo >= 365 && !isCore && !isRunning && !isSanct;
                            int risk = ComputeRiskScore("installed_app", daysAgo, isRunning, isCore, isSanct);

                            string desc = promoteToRecommended
                                ? $"Application ({publisher}) inutilisée depuis {FormatAgeText(daysAgo)} ({lastActivity:dd/MM/yyyy})."
                                : $"Application Windows ({publisher}{(string.IsNullOrEmpty(version) ? "" : ", v" + version)}){(hasKnownDate ? $" — {lastActivity:dd/MM/yyyy}" : "")}.";

                            var item = CreateItem(
                                displayName,
                                pathForDisplay,
                                "apps",
                                sizeBytes,
                                risk,
                                desc,
                                lastModified: lastActivity,
                                uninstallString: uninstallStr,
                                isSanctuarized: isSanct,
                                isExtra: !promoteToRecommended
                            );
                            list.Add(item);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return list;
        }

        #region PARAMÈTRES DE L'APPLICATION (MODE IA & CONFIG)

        private static string GetSettingsFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp");
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
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp");
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

        private static string GetProtectedFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp");
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

        #region DÉCOUVERTE DYNAMIQUE DES BIBLIOTHÈQUES (STEAM & EPIC)

        // Détecte TOUTES les bibliothèques Steam sur N'IMPORTE QUEL PC et disque
        private static List<string> DiscoverSteamLibraries()
        {
            var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Emplacements standards & Registre
            var rootCandidates = new List<string>
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam"
            };

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var sp = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(sp) && Directory.Exists(sp))
                    {
                        rootCandidates.Add(sp.Replace('/', '\\'));
                    }
                }
            }
            catch { }

            // 2. Parser libraryfolders.vdf pour extraire TOUTES les bibliothèques secondaires
            foreach (var root in rootCandidates)
            {
                if (Directory.Exists(root))
                {
                    libs.Add(root);
                    var vdfPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        try
                        {
                            var text = File.ReadAllText(vdfPath);
                            var matches = Regex.Matches(text, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                            foreach (Match m in matches)
                            {
                                var parsedPath = m.Groups[1].Value.Replace(@"\\", @"\");
                                if (Directory.Exists(parsedPath))
                                {
                                    libs.Add(parsedPath);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            // 3. Scanner tous les disques fixes montés à la racine
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var candidate = Path.Combine(drive.Name, "SteamLibrary");
                if (Directory.Exists(candidate)) libs.Add(candidate);
                var candidate2 = Path.Combine(drive.Name, "Steam");
                if (Directory.Exists(candidate2)) libs.Add(candidate2);
            }

            return libs.ToList();
        }

        // Détecte TOUS les jeux Epic Games installés via les manifestes officiels
        private static Dictionary<string, string> DiscoverEpicGames()
        {
            var epicMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var manifestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (Directory.Exists(manifestDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("DisplayName", out var nameProp) && root.TryGetProperty("InstallLocation", out var locProp))
                        {
                            var name = nameProp.GetString();
                            var loc = locProp.GetString();
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                            {
                                // Nettoyage des caractères spéciaux
                                name = Regex.Replace(name, @"[^\w\s]", "").Trim();
                                epicMap[name] = loc;
                            }
                        }
                    }
                }
                catch { }
            }

            return epicMap;
        }

        public class SteamAppInfo
        {
            public string AppId { get; set; } = "";
            public string Name { get; set; } = "";
            public string InstallDir { get; set; } = "";
            public DateTime? LastPlayed { get; set; }
            public DateTime? LastUpdated { get; set; }
        }

        private static Dictionary<string, DateTime> _steamLastPlayedCache = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime _steamLastPlayedCacheTime = DateTime.MinValue;

        private static Dictionary<string, DateTime> GetSteamLastPlayedMap()
        {
            if (_steamLastPlayedCache.Count > 0 && (DateTime.Now - _steamLastPlayedCacheTime).TotalMinutes < 5)
                return _steamLastPlayedCache;

            var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam"
            };
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var sp = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(sp)) steamRoots.Add(sp.Replace('/', '\\'));
            }
            catch { }

            foreach (var root in steamRoots)
            {
                var userDataDir = Path.Combine(root, "userdata");
                if (!Directory.Exists(userDataDir)) continue;

                foreach (var userDir in Directory.GetDirectories(userDataDir))
                {
                    var localCfg = Path.Combine(userDir, "config", "localconfig.vdf");
                    if (!File.Exists(localCfg)) continue;
                    try
                    {
                        string currentAppId = "";
                        int depthAfterAppId = 0;
                        foreach (var rawLine in File.ReadLines(localCfg))
                        {
                            var line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            if (depthAfterAppId == 0)
                            {
                                var mId = Regex.Match(line, @"^""(\d+)""$");
                                if (mId.Success)
                                {
                                    currentAppId = mId.Groups[1].Value;
                                }
                                else if (line == "{" && !string.IsNullOrEmpty(currentAppId))
                                {
                                    depthAfterAppId = 1;
                                }
                                else if (!line.StartsWith("\""))
                                {
                                    currentAppId = "";
                                }
                            }
                            else
                            {
                                if (line == "{") depthAfterAppId++;
                                else if (line == "}")
                                {
                                    depthAfterAppId--;
                                    if (depthAfterAppId <= 0)
                                    {
                                        depthAfterAppId = 0;
                                        currentAppId = "";
                                    }
                                }
                                else if (depthAfterAppId == 1 && line.StartsWith("\"LastPlayed\"", StringComparison.OrdinalIgnoreCase))
                                {
                                    var mLp = Regex.Match(line, @"""LastPlayed""\s+""(\d+)""", RegexOptions.IgnoreCase);
                                    if (mLp.Success && long.TryParse(mLp.Groups[1].Value, out long unix) && unix > 0)
                                    {
                                        var dt = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                                        if (!map.TryGetValue(currentAppId, out var existing) || dt > existing)
                                        {
                                            map[currentAppId] = dt;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            _steamLastPlayedCache = map;
            _steamLastPlayedCacheTime = DateTime.Now;
            return map;
        }

        public static SteamAppInfo? GetSteamAppInfo(string gameDir)
        {
            try
            {
                var commonDir = Path.GetDirectoryName(gameDir);
                if (commonDir == null) return null;
                var steamAppsDir = Path.GetDirectoryName(commonDir);
                if (steamAppsDir == null || !Directory.Exists(steamAppsDir)) return null;

                var folderName = Path.GetFileName(gameDir);
                var manifests = Directory.GetFiles(steamAppsDir, "appmanifest_*.acf");
                foreach (var mf in manifests)
                {
                    var content = File.ReadAllText(mf);
                    var mInst = Regex.Match(content, @"\""installdir\""\s+\""([^\""]+)\""", RegexOptions.IgnoreCase);
                    if (mInst.Success && mInst.Groups[1].Value.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    {
                        var info = new SteamAppInfo { InstallDir = folderName };
                        var mAppId = Regex.Match(content, @"\""appid\""\s+\""([^\""]+)\""", RegexOptions.IgnoreCase);
                        if (mAppId.Success) info.AppId = mAppId.Groups[1].Value;

                        var mName = Regex.Match(content, @"\""name\""\s+\""([^\""]+)\""", RegexOptions.IgnoreCase);
                        if (mName.Success) info.Name = mName.Groups[1].Value;

                        var mLp = Regex.Match(content, @"\""LastPlayed\""\s+\""(\d+)\""", RegexOptions.IgnoreCase);
                        if (mLp.Success && long.TryParse(mLp.Groups[1].Value, out long lpUnix) && lpUnix > 0)
                        {
                            info.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(lpUnix).LocalDateTime;
                        }

                        if (info.LastPlayed == null && !string.IsNullOrEmpty(info.AppId))
                        {
                            var lpMap = GetSteamLastPlayedMap();
                            if (lpMap.TryGetValue(info.AppId, out var vdfLastPlayed))
                            {
                                info.LastPlayed = vdfLastPlayed;
                            }
                        }

                        var mLu = Regex.Match(content, @"\""LastUpdated\""\s+\""(\d+)\""", RegexOptions.IgnoreCase);
                        if (mLu.Success && long.TryParse(mLu.Groups[1].Value, out long luUnix) && luUnix > 0)
                        {
                            info.LastUpdated = DateTimeOffset.FromUnixTimeSeconds(luUnix).LocalDateTime;
                        }

                        return info;
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region MODULES D'AUDIT UNIVERSELS

        private static void AuditGamesUniversal(List<CleanerItem> items, List<string> steamLibs, Dictionary<string, string> epicGames, HashSet<string>? targetDrives, HashSet<string> runningProcs)
        {
            var steamGames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lib in steamLibs)
            {
                if (!IsDriveSelected(lib, targetDrives)) continue;
                var steamApps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamApps)) continue;

                // A. Téléchargements bloqués (downloading)
                var dlPath = Path.Combine(steamApps, "downloading");
                if (Directory.Exists(dlPath))
                {
                    double size = GetDirectorySizeSafe(dlPath);
                    if (size > 50 * 1024 * 1024) // > 50 MB
                    {
                        var last = GetLastWriteTimeSafe(new DirectoryInfo(dlPath));
                        items.Add(CreateItem("Téléchargements Steam Bloqués (downloading)", dlPath, "games", size, 0, "Paquets Steam interrompus ou corrompus. 100% régénérable sans casser vos jeux.", lastModified: last));
                    }
                }

                // A2. Steam Shader Cache
                var shaderPath = Path.Combine(steamApps, "shadercache");
                if (Directory.Exists(shaderPath))
                {
                    double size = GetDirectorySizeSafe(shaderPath);
                    if (size > 150 * 1024 * 1024) // > 150 MB
                    {
                        var last = GetLastWriteTimeSafe(new DirectoryInfo(shaderPath));
                        items.Add(CreateItem($"Steam Shader Cache ({Path.GetPathRoot(lib)?.TrimEnd('\\')})", shaderPath, "games", size, 0, "Caches de shaders pré-compilés Steam (Vulkan/OpenGL/DX). Régénérables automatiquement.", lastModified: last));
                    }
                }

                // A3. Steam Workshop volumineux
                var workshopPath = Path.Combine(steamApps, "workshop", "content");
                if (Directory.Exists(workshopPath))
                {
                    double size = GetDirectorySizeSafe(workshopPath);
                    if (size > 500 * 1024 * 1024) // > 500 MB
                    {
                        var last = GetLastWriteTimeSafe(new DirectoryInfo(workshopPath));
                        int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                        int risk = daysAgo >= 180 ? 10 : 25;
                        items.Add(CreateItem($"Mods Steam Workshop ({Path.GetPathRoot(lib)?.TrimEnd('\\')})", workshopPath, "games", size, risk, $"Contenu et mods Steam Workshop ({FormatAgeText(daysAgo)}). Steam re-téléchargera les mods abonnés si besoin.", lastModified: last));
                    }
                }

                // B. Jeux Steam installés (détection des démos/PTS abandonnés >= 120j)
                var common = Path.Combine(steamApps, "common");
                if (Directory.Exists(common))
                {
                    foreach (var gameDir in Directory.GetDirectories(common))
                    {
                        var di = new DirectoryInfo(gameDir);
                        var cleanName = Regex.Replace(di.Name, @"[^\w\s]", "").Trim();
                        steamGames[cleanName] = gameDir;

                        bool isDemo = di.Name.EndsWith("demo", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Contains("playtest", StringComparison.OrdinalIgnoreCase);
                        bool isPts = di.Name.Contains("pts", StringComparison.OrdinalIgnoreCase) ||
                                     di.Name.Contains("public test", StringComparison.OrdinalIgnoreCase);

                        if (isDemo || isPts)
                        {
                            var steamInfo = GetSteamAppInfo(gameDir);
                            DateTime lastActivity = steamInfo?.LastPlayed
                                                    ?? steamInfo?.LastUpdated
                                                    ?? GetLastWriteTimeSafe(di);

                            int daysAgo = Math.Max(0, (int)(DateTime.Now - lastActivity).TotalDays);

                            if (daysAgo >= 120)
                            {
                                double size = GetDirectorySizeSafe(gameDir);
                                string appTitle = steamInfo?.Name ?? di.Name;
                                string label = isPts ? $"Serveur de Test abandonné : {appTitle}" : $"Démo Steam abandonnée : {appTitle}";
                                string reason = isPts
                                    ? $"Serveur de test public inutilisé depuis {FormatAgeText(daysAgo)} ({lastActivity:dd/MM/yyyy})."
                                    : $"Démo jouable inactive depuis {FormatAgeText(daysAgo)} (Dernière partie : {lastActivity:dd/MM/yyyy}).";

                                bool isRunning = IsLikelyRunning(appTitle, gameDir, runningProcs);
                                int risk = ComputeRiskScore("demo_pts", daysAgo, isRunning, false, false);
                                items.Add(CreateItem(label, gameDir, "games", size, risk, reason, lastModified: lastActivity));
                            }
                        }
                    }
                }
            }

            // C. Détection automatique des doublons cross-plateformes (Steam vs Epic)
            foreach (var epic in epicGames)
            {
                if (!IsDriveSelected(epic.Value, targetDrives)) continue;
                var matchKey = steamGames.Keys.FirstOrDefault(k => k.Equals(epic.Key, StringComparison.OrdinalIgnoreCase) || k.Contains(epic.Key, StringComparison.OrdinalIgnoreCase) || epic.Key.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (matchKey != null)
                {
                    var steamPath = steamGames[matchKey];
                    var epicPath = epic.Value;

                    double sizeEpic = GetDirectorySizeSafe(epicPath);
                    if (sizeEpic > 500 * 1024 * 1024)
                    {
                        var last = GetLastWriteTimeSafe(new DirectoryInfo(epicPath));
                        items.Add(CreateItem(
                            $"Doublon Cross-Platform : {epic.Key} (Epic Games)",
                            epicPath,
                            "games",
                            sizeEpic,
                            5,
                            $"Jeu installé à la fois sur Epic Games ({epicPath}) et sur Steam ({steamPath}).",
                            lastModified: last
                        ));
                    }
                }
            }

            // D. Découverte dynamique de vieux dossiers de jeux portables sur tous les disques sélectionnés
            foreach (var drive in GetTargetDrives(targetDrives))
            {
                var candidateFolders = new[] { "Autres Jeux", "Jeux", "Games", "Old Games" };
                foreach (var cf in candidateFolders)
                {
                    var full = Path.Combine(drive.Name, cf);
                    if (Directory.Exists(full))
                    {
                        foreach (var sub in Directory.GetDirectories(full))
                        {
                            var di = new DirectoryInfo(sub);
                            double size = GetDirectorySizeSafe(sub);
                            if (size > 500 * 1024 * 1024)
                            {
                                var last = GetLastWriteTimeSafe(di);
                                int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                                if (daysAgo >= 180)
                                {
                                    bool isRunning = IsLikelyRunning(di.Name, sub, runningProcs);
                                    int risk = ComputeRiskScore("portable_game", daysAgo, isRunning, false, false);
                                    items.Add(CreateItem(
                                        $"{di.Name} (Jeu Portable {drive.Name.TrimEnd('\\')})",
                                        sub,
                                        "games",
                                        size,
                                        risk,
                                        $"Jeu portable inactif depuis {FormatAgeText(daysAgo)} ({last:dd/MM/yyyy}).",
                                        lastModified: last
                                    ));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void AuditSystemUniversal(List<CleanerItem> items, HashSet<string>? targetDrives, HashSet<string> runningProcs)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // Les caches utilisateur/système résident généralement sur C: (ou le disque système)
            if (IsDriveSelected(localAppData, targetDrives))
            {
                // A. Caches GPU Shaders (NVIDIA & AMD & Intel)
                var gpuCaches = new[]
                {
                    Path.Combine(localAppData, "NVIDIA", "DXCache"),
                    Path.Combine(localAppData, "NVIDIA", "GLCache"),
                    Path.Combine(localAppData, "D3DSCache"),
                    Path.Combine(localAppData, "AMD", "DxCache"),
                    Path.Combine(localAppData, "AMD", "GLCache"),
                    Path.Combine(localAppData, "Intel", "ShaderCache")
                };
                foreach (var gc in gpuCaches)
                {
                    if (Directory.Exists(gc))
                    {
                        double size = GetDirectorySizeSafe(gc);
                        if (size > 100 * 1024 * 1024)
                        {
                            var last = GetLastWriteTimeSafe(new DirectoryInfo(gc));
                            items.Add(CreateItem("DirectX & GPU Shader Cache", gc, "system", size, 0, "Caches de rendu 3D compilés régénérables automatiquement au lancement des jeux.", lastModified: last));
                        }
                    }
                }

                // B. Caches de build développeur & IA (.gradle, .m2, ms-playwright, nuget, npm, pip, cargo, huggingface, torch, ollama, unity, unreal)
                var devCaches = new[]
                {
                    new { Name = "Gradle Build Cache", Path = Path.Combine(user, ".gradle", "caches"), Risk = 0, Desc = "Dépendances et artefacts Gradle régénérables." },
                    new { Name = "Maven Local Repository (.m2)", Path = Path.Combine(user, ".m2", "repository"), Risk = 0, Desc = "Bibliothèques Java (.jar) re-téléchargeables via Maven." },
                    new { Name = "Playwright Test Browsers", Path = Path.Combine(localAppData, "ms-playwright"), Risk = 0, Desc = "Navigateurs headless de test réinstallables via npx playwright install." },
                    new { Name = "NuGet Package Cache", Path = Path.Combine(localAppData, "NuGet", "v3-cache"), Risk = 0, Desc = "Paquets C#/.NET régénérables par dotnet / Visual Studio." },
                    new { Name = "NPM Package Cache", Path = Path.Combine(localAppData, "npm-cache"), Risk = 0, Desc = "Cache local des paquets Node.js (npm)." },
                    new { Name = "NPM Cache (Roaming)", Path = Path.Combine(appData, "npm-cache"), Risk = 0, Desc = "Cache local des paquets Node.js (npm)." },
                    new { Name = "Pip Python Cache", Path = Path.Combine(localAppData, "pip", "Cache"), Risk = 0, Desc = "Archives d'installation Python (.whl) régénérables." },
                    new { Name = "UV Python Cache", Path = Path.Combine(localAppData, "uv", "cache"), Risk = 0, Desc = "Cache de paquets Python uv régénérable." },
                    new { Name = "Cargo Rust Cache", Path = Path.Combine(user, ".cargo", "registry"), Risk = 0, Desc = "Crates Rust téléchargées, régénérables lors du build." },
                    new { Name = "HuggingFace AI Models Cache", Path = Path.Combine(user, ".cache", "huggingface", "hub"), Risk = 10, Desc = "Modèles d'IA locaux HuggingFace volumineux (re-téléchargeables si besoin)." },
                    new { Name = "PyTorch Models Cache", Path = Path.Combine(user, ".cache", "torch"), Risk = 5, Desc = "Poids et modèles PyTorch mis en cache localement." },
                    new { Name = "Unity Asset & Package Cache", Path = Path.Combine(localAppData, "Unity", "cache"), Risk = 0, Desc = "Cache de paquets et assets Unity Hub / Editor." },
                    new { Name = "Unreal Engine DerivedDataCache", Path = Path.Combine(localAppData, "UnrealEngine", "Common", "DerivedDataCache"), Risk = 0, Desc = "Shaders et données compilées Unreal Engine régénérables." },
                    new { Name = "Crash Dumps & Rapports d'erreur", Path = Path.Combine(localAppData, "CrashDumps"), Risk = 0, Desc = "Fichiers mémoire de plantages d'applications (.dmp) 100% inutiles." }
                };
                foreach (var dc in devCaches)
                {
                    if (Directory.Exists(dc.Path))
                    {
                        double size = GetDirectorySizeSafe(dc.Path);
                        if (size > 80 * 1024 * 1024)
                        {
                            var last = GetLastWriteTimeSafe(new DirectoryInfo(dc.Path));
                            items.Add(CreateItem(dc.Name, dc.Path, "system", size, dc.Risk, dc.Desc, lastModified: last));
                        }
                    }
                }

                // B2. Dossiers Temp Utilisateur & Windows & SoftwareDistribution
                var sysTempPaths = new[]
                {
                    new { Name = "Fichiers Temporaires Utilisateur (%TEMP%)", Path = Path.GetTempPath().TrimEnd('\\'), Desc = "Fichiers temporaires Windows et installeurs résiduels." },
                    new { Name = "Fichiers Temporaires Système (Windows\\Temp)", Path = Path.Combine(winDir, "Temp"), Desc = "Fichiers temporaires système Windows." },
                    new { Name = "Cache Windows Update (SoftwareDistribution)", Path = Path.Combine(winDir, "SoftwareDistribution", "Download"), Desc = "Anciens paquets de mises à jour Windows déjà installées." },
                    new { Name = "Minidumps Système (BSOD)", Path = Path.Combine(winDir, "Minidump"), Desc = "Journaux mémoire d'anciens écrans bleus." }
                };
                foreach (var st in sysTempPaths)
                {
                    if (IsDriveSelected(st.Path, targetDrives) && Directory.Exists(st.Path))
                    {
                        double size = GetDirectorySizeSafe(st.Path);
                        if (size > 100 * 1024 * 1024)
                        {
                            var last = GetLastWriteTimeSafe(new DirectoryInfo(st.Path));
                            items.Add(CreateItem(st.Name, st.Path, "system", size, 0, st.Desc, lastModified: last));
                        }
                    }
                }

                // B3. Caches de navigateurs & Spotify & Adobe
                var mediaCaches = new[]
                {
                    new { Name = "Cache Navigateur Google Chrome", Path = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache") },
                    new { Name = "Cache Navigateur Microsoft Edge", Path = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache") },
                    new { Name = "Cache Navigateur Brave", Path = Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache") },
                    new { Name = "Cache Audio Spotify (Storage)", Path = Path.Combine(localAppData, "Spotify", "Storage") },
                    new { Name = "Adobe Media Cache Files", Path = Path.Combine(appData, "Adobe", "Common", "Media Cache Files") }
                };
                foreach (var mc in mediaCaches)
                {
                    if (Directory.Exists(mc.Path))
                    {
                        double size = GetDirectorySizeSafe(mc.Path);
                        if (size > 150 * 1024 * 1024)
                        {
                            var last = GetLastWriteTimeSafe(new DirectoryInfo(mc.Path));
                            items.Add(CreateItem(mc.Name, mc.Path, "system", size, 0, "Cache média/web 100% régénérable automatiquement sans perte de favoris ou mots de passe.", lastModified: last));
                        }
                    }
                }

                // C. Scan dynamique d'AppData (>= 150 Mo)
                var appDataRoots = new[] { localAppData, appData, Path.Combine(user, "AppData", "LocalLow") };
                foreach (var root in appDataRoots)
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        var di = new DirectoryInfo(dir);
                        if (di.Name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("Programs", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("AMD", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("NuGet", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("ms-playwright", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("CrashDumps", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase)) continue;

                        double size = GetDirectorySizeSafe(dir);
                        if (size > 150 * 1024 * 1024)
                        {
                            bool isCache = di.Name.Contains("cache", StringComparison.OrdinalIgnoreCase) ||
                                           di.Name.Contains("updater", StringComparison.OrdinalIgnoreCase) ||
                                           di.Name.Contains("log", StringComparison.OrdinalIgnoreCase) ||
                                           di.Name.Contains("temp", StringComparison.OrdinalIgnoreCase);

                            var lastWrite = GetLastWriteTimeSafe(di);
                            int daysAgo = Math.Max(0, (int)(DateTime.Now - lastWrite).TotalDays);
                            bool isOld = daysAgo >= 180;
                            bool isRunning = IsLikelyRunning(di.Name, dir, runningProcs);

                            if (isCache || isOld)
                            {
                                int risk = ComputeRiskScore(isCache ? "appdata_cache" : "appdata_inactive", daysAgo, isRunning, false, false);
                                items.Add(CreateItem(
                                    isCache ? $"Cache AppData : {di.Name}" : $"Dossier Inactif AppData : {di.Name}",
                                    dir,
                                    "system",
                                    size,
                                    risk,
                                    isCache ? $"Cache / updater applicatif régénérable ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo)."
                                            : $"Données d'application inactives depuis {FormatAgeText(daysAgo)} ({lastWrite:dd/MM/yyyy}).",
                                    lastModified: lastWrite
                                ));
                            }
                        }
                    }
                }
            }

            // D. Scan des dossiers temporaires et caches standards à la racine sur TOUS les disques sélectionnés
            foreach (var drive in GetTargetDrives(targetDrives))
            {
                var rootCaches = new[] { "msdownld.tmp", "MSOCache", "Temp", "Tmp", "$Recycle.Bin", ".gradle", ".m2", "node_modules" };
                foreach (var rc in rootCaches)
                {
                    var p = Path.Combine(drive.Name, rc);
                    if (Directory.Exists(p))
                    {
                        double size = GetDirectorySizeSafe(p);
                        if (size > 10 * 1024 * 1024) // > 10 Mo
                        {
                            var last = GetLastWriteTimeSafe(new DirectoryInfo(p));
                            string label = rc.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                                ? $"Corbeille Windows ({drive.Name.TrimEnd('\\')})"
                                : $"Cache / Dossier Temporaire : {rc} ({drive.Name.TrimEnd('\\')})";
                            items.Add(CreateItem(label, p, "system", size, 0, "Dossier temporaire, corbeille ou cache d'installation résiduel à la racine du disque.", lastModified: last));
                        }
                    }
                }
            }
        }

        private static void AuditDownloadsUniversal(List<CleanerItem> items, HashSet<string>? targetDrives)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dlList = new List<string> { Path.Combine(user, "Downloads") };

            foreach (var d in GetTargetDrives(targetDrives))
            {
                var p1 = Path.Combine(d.Name, "Téléchargements");
                if (Directory.Exists(p1)) dlList.Add(p1);
                var p2 = Path.Combine(d.Name, "Downloads");
                if (Directory.Exists(p2) && !dlList.Contains(p2, StringComparer.OrdinalIgnoreCase)) dlList.Add(p2);
            }

            foreach (var dl in dlList)
            {
                if (!IsDriveSelected(dl, targetDrives) || !Directory.Exists(dl)) continue;

                // 1. Installeurs (.exe, .msi, .appimage, .iso)
                foreach (var f in Directory.GetFiles(dl))
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.ToLowerInvariant();
                    var effDate = GetFileEffectiveActivityDate(fi);
                    int daysAgo = Math.Max(0, (int)(DateTime.Now - effDate).TotalDays);

                    if (ext == ".exe" || ext == ".msi" || ext == ".appimage" || ext == ".msix")
                    {
                        if (fi.Length > 5 * 1024 * 1024)
                        {
                            int risk = ComputeRiskScore("installer", daysAgo, false, false, false);
                            items.Add(CreateItem(
                                $"Installeur : {fi.Name}",
                                f,
                                "downloads",
                                fi.Length,
                                risk,
                                $"Installeur téléchargé {FormatAgeText(daysAgo)} ({effDate:dd/MM/yyyy}). Inutile dès que le logiciel est installé.",
                                isFolder: false,
                                lastModified: effDate
                            ));
                        }
                    }
                    else if ((ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".iso" || ext == ".tar" || ext == ".gz") && fi.Length > 20 * 1024 * 1024)
                    {
                        bool isPersonal = fi.Name.Contains("Direct Messages", StringComparison.OrdinalIgnoreCase) ||
                                          fi.Name.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
                                          fi.Name.Contains("sauvegarde", StringComparison.OrdinalIgnoreCase);

                        int risk = ComputeRiskScore("archive", daysAgo, false, false, isPersonal);

                        items.Add(CreateItem(
                            $"Archive : {fi.Name}",
                            f,
                            "downloads",
                            fi.Length,
                            risk,
                            isPersonal
                                ? "Sauvegarde / archive personnelle. Recommandé de conserver !"
                                : $"Archive téléchargée {FormatAgeText(daysAgo)} ({effDate:dd/MM/yyyy}). Suppression sûre si déjà extraite.",
                            isFolder: false,
                            isSanctuarized: isPersonal,
                            lastModified: effDate
                        ));
                    }
                }

                // 2. Scan dynamique de TOUS les sous-dossiers volumineux (>= 80 Mo) dans Downloads
                string[] mediaOrDocExts = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".mp4", ".mkv", ".mov", ".avi", ".webm", ".mp3", ".wav", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".psd", ".blend" };
                foreach (var sub in Directory.GetDirectories(dl))
                {
                    var di = new DirectoryInfo(sub);
                    double size = GetDirectorySizeSafe(sub);
                    if (size < 80 * 1024 * 1024) continue;

                    var last = GetLastWriteTimeSafe(di);
                    int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                    bool isTempName = di.Name.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Equals("tmp", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase);
                    bool hasMatchingArchive = File.Exists(sub + ".zip") || File.Exists(sub + ".rar") || File.Exists(sub + ".7z");

                    bool hasMediaOrDocs = false;
                    try
                    {
                        hasMediaOrDocs = di.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 2, AttributesToSkip = FileAttributes.ReparsePoint })
                            .Take(80)
                            .Any(f => mediaOrDocExts.Contains(f.Extension.ToLowerInvariant()));
                    }
                    catch { }

                    if (isTempName || hasMatchingArchive || daysAgo >= 60)
                    {
                        int risk;
                        if ((isTempName || hasMatchingArchive) && !hasMediaOrDocs)
                        {
                            risk = 0;
                        }
                        else if (hasMediaOrDocs)
                        {
                            risk = daysAgo >= 730 ? 10 : (daysAgo >= 365 ? 15 : (daysAgo >= 180 ? 25 : 35));
                        }
                        else
                        {
                            risk = daysAgo >= 365 ? 5 : (daysAgo >= 180 ? 10 : 20);
                        }

                        string reason = hasMatchingArchive
                            ? $"Dossier déjà extrait à côté de son archive ({di.Name}.zip/.rar/.7z){(hasMediaOrDocs ? " — contient des médias/documents" : "")}."
                            : (hasMediaOrDocs
                                ? $"Contient des images, vidéos ou documents (inactif depuis {FormatAgeText(daysAgo)}, {last:dd/MM/yyyy})."
                                : (isTempName
                                    ? "Dossier de build ou temporaire dans Téléchargements."
                                    : $"Dossier téléchargé inactif depuis {FormatAgeText(daysAgo)} ({last:dd/MM/yyyy})."));
                        items.Add(CreateItem($"Dossier Téléchargé : {di.Name}", sub, "downloads", size, risk, reason, lastModified: last));
                    }
                }
            }
        }

        private static void AuditDriveRootsDynamic(
            List<CleanerItem> recommendedItems,
            List<CleanerItem> extraItems,
            HashSet<string> seenPaths,
            HashSet<string>? targetDrives,
            HashSet<string> runningProcs)
        {
            var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Windows", "Program Files", "Program Files (x86)", "ProgramData", "Users",
                "Recovery", "System Volume Information", "PerfLogs", "$Recycle.Bin",
                "WindowsApps", "WpSystem", "Steam", "SteamLibrary", "Epic Games",
                "XboxGames", "Riot Games", "GOG Games", "Ubisoft", "Battle.net",
                "Autres Jeux", "Jeux", "Games", "Old Games", "Téléchargements", "Downloads",
                "msdownld.tmp", "MSOCache", "Temp", "Tmp", ".gradle", ".m2", "node_modules"
            };

            string[] romOrGameExts = { ".iso", ".nsp", ".xci", ".wbfs", ".rvz", ".gcz", ".cso", ".chd", ".wad", ".3ds", ".cia", ".nds", ".gba", ".pak", ".ucas" };

            foreach (var d in GetTargetDrives(targetDrives))
            {
                string[] topDirs;
                try { topDirs = Directory.GetDirectories(d.Name, "*", SafeTopOpts); }
                catch { continue; }

                foreach (var dir in topDirs)
                {
                    var di = new DirectoryInfo(dir);
                    if (skipNames.Contains(di.Name) || di.Name.StartsWith("$") || seenPaths.Contains(dir) || IsPathSanctuarized(dir))
                        continue;

                    var (bytes, fCount, last) = GetDirectoryStatsSafe(dir);
                    if (bytes < 100 * 1024 * 1024) continue; // >= 100 Mo

                    int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                    bool isRunning = IsLikelyRunning(di.Name, dir, runningProcs);
                    var nLower = di.Name.ToLowerInvariant();

                    bool looksLikeCacheOrDownload = nLower.Contains("cache") || nLower.Contains("temp") ||
                                                    nLower.Contains("tmp") || nLower.Contains("download");

                    // Détection générique de dossier "conteneur" (ex: regroupe un émulateur + des ROMs, ou plusieurs jeux portables)
                    FileInfo[] directFiles = Array.Empty<FileInfo>();
                    DirectoryInfo[] directSubDirs = Array.Empty<DirectoryInfo>();
                    try
                    {
                        directFiles = di.GetFiles("*", SafeTopOpts);
                        directSubDirs = di.GetDirectories("*", SafeTopOpts);
                    }
                    catch { }

                    bool hasTopExe = directFiles.Any(f => f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase));
                    var topLargeRoms = directFiles
                        .Where(f => romOrGameExts.Contains(f.Extension.ToLowerInvariant()) && f.Length >= 50 * 1024 * 1024)
                        .ToList();

                    int subAppsOrRoms = 0;
                    if (!hasTopExe && directSubDirs.Length > 0)
                    {
                        foreach (var sub in directSubDirs.Take(20))
                        {
                            try
                            {
                                bool subHasApp = sub.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 2, AttributesToSkip = FileAttributes.ReparsePoint })
                                    .Take(40)
                                    .Any(sf => sf.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || romOrGameExts.Contains(sf.Extension.ToLowerInvariant()));
                                if (subHasApp) subAppsOrRoms++;
                            }
                            catch { }
                        }
                    }

                    bool isContainer = !hasTopExe && !looksLikeCacheOrDownload &&
                                       ((topLargeRoms.Count > 0 && directSubDirs.Length > 0) || topLargeRoms.Count >= 2 || subAppsOrRoms >= 2);

                    if (isContainer)
                    {
                        // 1. Chaque ROM / image disque directe devient un élément indépendant
                        foreach (var rf in topLargeRoms)
                        {
                            if (!seenPaths.Add(rf.FullName)) continue;
                            var rActivity = GetFileEffectiveActivityDate(rf);
                            int rDays = Math.Max(0, (int)(DateTime.Now - rActivity).TotalDays);
                            int rRisk = ComputeRiskScore("portable_game", rDays, false, false, false);
                            bool rPromote = rDays >= 180;
                            var rItem = CreateItem(
                                $"ROM / Jeu : {Path.GetFileNameWithoutExtension(rf.Name)} ({di.Name})",
                                rf.FullName,
                                "games",
                                rf.Length,
                                rRisk,
                                $"Image jeu / ROM ({rf.Extension.ToUpperInvariant()}) dans {di.Name} (Activité : {FormatAgeText(rDays)}).",
                                isFolder: false,
                                lastModified: rActivity,
                                isExtra: !rPromote
                            );
                            if (rPromote) recommendedItems.Add(rItem);
                            else extraItems.Add(rItem);
                        }

                        // 2. Chaque sous-dossier applicatif/émulateur/jeu significatif devient un élément indépendant
                        foreach (var sub in directSubDirs)
                        {
                            if (!seenPaths.Add(sub.FullName)) continue;
                            var (sBytes, sFiles, sLast) = GetDirectoryStatsSafe(sub.FullName);
                            if (sBytes < 30 * 1024 * 1024) continue;

                            int sDays = Math.Max(0, (int)(DateTime.Now - sLast).TotalDays);
                            bool sRun = IsLikelyRunning(sub.Name, sub.FullName, runningProcs);
                            bool subHasRom = false;
                            try
                            {
                                subHasRom = sub.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 2, AttributesToSkip = FileAttributes.ReparsePoint })
                                    .Take(50)
                                    .Any(sf => romOrGameExts.Contains(sf.Extension.ToLowerInvariant()));
                            }
                            catch { }

                            string sCat = subHasRom ? "games" : "apps";
                            int sRisk = ComputeRiskScore(subHasRom ? "portable_game" : "emulator", sDays, sRun, false, false);
                            bool sPromote = sDays >= 180 && !sRun;
                            var sItem = CreateItem(
                                $"{sub.Name} ({di.Name})",
                                sub.FullName,
                                sCat,
                                sBytes,
                                sRisk,
                                $"Sous-dossier dans {di.Name} ({sFiles} fichiers, {FormatAgeText(sDays)}).",
                                lastModified: sLast,
                                isExtra: !sPromote
                            );
                            if (sPromote) recommendedItems.Add(sItem);
                            else extraItems.Add(sItem);
                        }

                        seenPaths.Add(dir);
                        continue;
                    }

                    bool hasExeOrGame = hasTopExe;
                    bool hasRom = topLargeRoms.Count > 0;
                    if (!hasExeOrGame || !hasRom)
                    {
                        try
                        {
                            var sampleFiles = di.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 2, AttributesToSkip = FileAttributes.ReparsePoint }).Take(80);
                            foreach (var sf in sampleFiles)
                            {
                                var ext = sf.Extension.ToLowerInvariant();
                                if (ext == ".exe") hasExeOrGame = true;
                                if (romOrGameExts.Contains(ext)) hasRom = true;
                                if (hasExeOrGame && hasRom) break;
                            }
                        }
                        catch { }
                    }

                    if (looksLikeCacheOrDownload)
                    {
                        int risk = ComputeRiskScore(daysAgo >= 90 ? "appdata_cache" : "appdata_inactive", daysAgo, isRunning, false, false);
                        recommendedItems.Add(CreateItem(
                            $"Dossier Cache / Téléchargement : {di.Name} ({d.Name.TrimEnd('\\')})",
                            dir,
                            "system",
                            bytes,
                            risk,
                            $"Dossier racine ({fCount} fichiers, {FormatAgeText(daysAgo)}).",
                            lastModified: last
                        ));
                    }
                    else if (hasRom || hasExeOrGame)
                    {
                        string cat = (hasRom || nLower.Contains("jeu") || nLower.Contains("game") || nLower.Contains("emu") || nLower.Contains("rom")) ? "games" : "apps";
                        int risk = ComputeRiskScore("portable_game", daysAgo, isRunning, false, false);
                        bool promote = daysAgo >= 180 && !isRunning;
                        var item = CreateItem(
                            $"{di.Name} ({d.Name.TrimEnd('\\')})",
                            dir,
                            cat,
                            bytes,
                            risk,
                            $"Dossier applicatif / jeu détecté à la racine de {d.Name.TrimEnd('\\')} ({fCount} fichiers, {FormatAgeText(daysAgo)}).",
                            lastModified: last,
                            isExtra: !promote
                        );
                        if (promote) recommendedItems.Add(item);
                        else extraItems.Add(item);
                    }
                    else
                    {
                        // Dossier de données divers à la racine du disque
                        bool promote = daysAgo >= 365 && !isRunning;
                        int risk = promote ? 20 : 45;
                        var item = CreateItem(
                            $"Dossier Racine : {di.Name} ({d.Name.TrimEnd('\\')})",
                            dir,
                            "system",
                            bytes,
                            risk,
                            $"Dossier personnalisé à la racine de {d.Name.TrimEnd('\\')} ({fCount} fichiers, {FormatAgeText(daysAgo)}).",
                            lastModified: last,
                            isExtra: !promote
                        );
                        if (promote) recommendedItems.Add(item);
                        else extraItems.Add(item);
                    }
                    seenPaths.Add(dir);
                }
            }
        }

        #endregion

        #region RECHERCHE FACTUELLE & SUPPRESSION SÉCURISÉE

        public static ResearchReport ResearchItem(string name, string path, int initialRisk = 5, bool bypassAiCache = false)
        {
            if (!bypassAiCache && !string.IsNullOrWhiteSpace(path))
            {
                var aiCache = LoadAiCache();
                if (aiCache.TryGetValue(path.Trim(), out var cachedReport))
                {
                    cachedReport.IsAiEnriched = true;
                    return cachedReport;
                }
            }

            var report = new ResearchReport 
            { 
                ItemName = name, 
                ItemPath = path,
                RiskPct = initialRisk
            };

            if (path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = $"Trace fantôme dans le Registre Windows (clé de désinstallation orpheline) pour : {name.Replace("Trace registre orpheline : ", "")}.";
                report.Origin = $"Clé Registre Windows ({path}). Le programme d'origine n'existe plus sur le disque, mais son entrée est restée dans la liste des programmes installés.";
                report.UsageActivity = "Totalement inactif (aucun fichier exécutable présent sur le disque).";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Nettoie proprement cette entrée fantôme du Registre Windows sans aucun impact sur le système.";
                report.LastAccessDate = "Trace orpheline";
                report.RiskPct = 0;
                return report;
            }

            bool isRegistryEntry = path.Equals("Registre Windows", StringComparison.OrdinalIgnoreCase) ||
                                   path.Contains("msiexec", StringComparison.OrdinalIgnoreCase);
            string resolvedPath = path;
            if (!Directory.Exists(resolvedPath) && !File.Exists(resolvedPath) && !isRegistryEntry)
            {
                var cmdExe = ExtractExeFromCommand(path);
                if (!string.IsNullOrEmpty(cmdExe) && File.Exists(cmdExe))
                {
                    resolvedPath = Path.GetDirectoryName(cmdExe) ?? cmdExe;
                }
            }

            bool existsOnDisk = Directory.Exists(resolvedPath) || File.Exists(resolvedPath);
            if (!existsOnDisk && !isRegistryEntry)
            {
                report.Definition = "L'élément n'existe plus sur le disque (seule une trace dans le Registre Windows subsistait).";
                report.Origin = "Fichier ou dossier déjà supprimé.";
                report.UsageActivity = "Inactif";
                report.SafetyImpact = "Aucun impact sur le système.";
                report.RiskPct = 0;
                return report;
            }

            DateTime lastWrite = DateTime.Now;
            if (File.Exists(resolvedPath))
            {
                lastWrite = GetFileEffectiveActivityDate(new FileInfo(resolvedPath));
            }
            else if (Directory.Exists(resolvedPath))
            {
                lastWrite = GetLastWriteTimeSafe(new DirectoryInfo(resolvedPath));
            }
            else
            {
                var regTmp = FindRegistryAppDetails(name.ToLowerInvariant(), name);
                if (regTmp != null && DateTime.TryParseExact(regTmp.InstallDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dtReg))
                {
                    lastWrite = dtReg;
                }
            }
            report.LastAccessDate = lastWrite.ToString("dd MMMM yyyy");

            var baseName = isRegistryEntry
                ? name.ToLowerInvariant()
                : Path.GetFileNameWithoutExtension(resolvedPath).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(baseName) || baseName.Length < 2) baseName = name.ToLowerInvariant();
            var procs = Process.GetProcesses();
            var matchedProcs = procs.Where(p =>
            {
                try
                {
                    if (p.ProcessName.Contains(baseName, StringComparison.OrdinalIgnoreCase)) return true;
                    if (name.Contains("Steam", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase)) return true;
                    if ((name.Contains("SignalRGB", StringComparison.OrdinalIgnoreCase) || name.Contains("Whirlwind", StringComparison.OrdinalIgnoreCase) || path.Contains("Whirlwind", StringComparison.OrdinalIgnoreCase)) &&
                        (p.ProcessName.Contains("SignalRgb", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Contains("Vortx", StringComparison.OrdinalIgnoreCase))) return true;
                    if (name.Contains("Discord", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Equals("Discord", StringComparison.OrdinalIgnoreCase)) return true;
                    if (name.Contains("Overwolf", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Contains("Overwolf", StringComparison.OrdinalIgnoreCase)) return true;
                    if (name.Contains("Medal", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Contains("Medal", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }
                return false;
            }).ToList();

            bool running = matchedProcs.Count > 0;
            report.IsRunning = running;
            report.UsageActivity = running 
                ? $"⚠️ En cours d'exécution ! Processus actif en mémoire ({string.Join(", ", matchedProcs.Select(p => p.ProcessName).Distinct())})." 
                : $"Inactif. Dernière écriture le {report.LastAccessDate}.";

            var n = name.ToLowerInvariant();
            var pLower = path.ToLowerInvariant();

            // 1. Jeux & Démos Steam (Reconnaissance via appmanifest official Steam)
            if (pLower.Contains(@"\steamapps\common\"))
            {
                var steamInfo = GetSteamAppInfo(path);
                string appName = steamInfo?.Name ?? Path.GetFileName(path);
                bool isDemo = appName.Contains("demo", StringComparison.OrdinalIgnoreCase) || 
                              appName.Contains("playtest", StringComparison.OrdinalIgnoreCase) || 
                              n.Contains("demo") || n.Contains("playtest");

                report.Definition = isDemo
                    ? $"Démo jouable gratuite Steam : {appName}."
                    : $"Jeu vidéo complet installé sur Steam : {appName}.";

                report.Origin = $"Téléchargé depuis le magasin Steam dans votre bibliothèque ({Path.GetPathRoot(path)?.TrimEnd('\\')}).";

                DateTime steamLastActivity = steamInfo?.LastPlayed ?? steamInfo?.LastUpdated ?? lastWrite;
                int steamDaysAgo = Math.Max(0, (int)(DateTime.Now - steamLastActivity).TotalDays);
                report.LastAccessDate = steamLastActivity.ToString("dd MMMM yyyy");

                if (steamInfo?.LastPlayed != null)
                {
                    report.UsageActivity = steamDaysAgo == 0
                        ? "Joué aujourd'hui !"
                        : $"Dernière partie jouée {FormatAgeText(steamDaysAgo)} (le {steamInfo.LastPlayed.Value:dd MMMM yyyy}).";
                }
                else if (steamInfo?.LastUpdated != null)
                {
                    report.UsageActivity = $"Téléchargé ou mis à jour le {steamInfo.LastUpdated.Value:dd MMMM yyyy} (aucune session enregistrée).";
                }

                report.SafetyImpact = isDemo
                    ? "C'est une démo temporaire. Si vous avez terminé d'essayer le jeu ou si vous ne comptez plus y rejouer, vous pouvez la supprimer sans risque (vos succès Steam sont préservés). Si vous souhaitez continuer à y jouer, conservez-la ou protégez-la."
                    : (steamDaysAgo >= 365
                        ? $"🟢 Jeu complet inutilisé depuis {FormatAgeText(steamDaysAgo)}. Suppression sûre pour libérer de l'espace (vos sauvegardes restent sur Steam Cloud et vous pourrez le réinstaller à tout moment)."
                        : "⚠️ Jeu complet utilisé récemment. Si vous le supprimez, vous devrez le re-télécharger depuis Steam pour y rejouer.");

                report.RiskPct = ComputeRiskScore(isDemo ? "demo_pts" : "steam_epic_game", steamDaysAgo, running, false, false);
                return report;
            }

            // 2. WhirlwindFX / SignalRGB
            if (n.Contains("whirlwind") || pLower.Contains("whirlwind") || n.Contains("vortx") || pLower.Contains("vortx"))
            {
                report.Definition = "Moteur d'effets visuels et de synchronisation d'éclairage dynamique RGB pour les jeux vidéo (WhirlwindFX / SignalRGB).";
                report.Origin = "Installé conjointement avec SignalRGB (ou les périphériques WhirlwindFX/Element). Sert à analyser l'écran et le son des jeux pour synchroniser l'éclairage de vos LED (clavier, ventilateurs, RAM, bandes RGB).";
                report.SafetyImpact = "Si vous utilisez SignalRGB pour synchroniser vos LED avec vos jeux, conservez ce dossier. Si vous n'utilisez plus SignalRGB ou ne synchronisez pas vos LED en jeu, vous pouvez le supprimer en toute sécurité sans aucun impact sur Windows.";
                report.RiskPct = 5;
            }
            // 3. NuGet Cache
            else if (n.Contains("nuget") || pLower.Contains("nuget"))
            {
                report.Definition = "Cache local des paquets et bibliothèques de développement C# et .NET (fichiers de paquets .nupkg compilés).";
                report.Origin = "Téléchargé et alimenté automatiquement par Visual Studio, Unity, ou le CLI dotnet lors de la compilation de projets de code C#.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). C'est un pur cache temporaire régénérable : si un projet .NET a besoin d'un paquet à l'avenir, Visual Studio ou dotnet le re-téléchargera automatiquement en quelques secondes sans aucun impact négatif sur vos projets.";
                report.RiskPct = 0;
            }
            // 4. Gradle Cache
            else if (n.Contains("gradle") || pLower.Contains(".gradle"))
            {
                report.Definition = "Cache des artefacts, dépendances Maven et binaires de compilation Gradle (Java / Android / Kotlin).";
                report.Origin = "Outil de build Gradle utilisé dans IntelliJ IDEA, Android Studio, VS Code ou vos projets Java.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Gradle re-téléchargera automatiquement les dépendances nécessaires lors de votre prochaine compilation.";
                report.RiskPct = 0;
            }
            // 5. Maven Repository (.m2)
            else if (n.Contains("maven") || pLower.Contains(".m2"))
            {
                report.Definition = "Dépôt local des bibliothèques Java (.jar) téléchargées depuis Maven Central.";
                report.Origin = "Créé par l'outil de build Maven lors des compilations Java (Spring Boot, Eclipse, IntelliJ, Maven CLI).";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Maven re-téléchargera automatiquement les fichiers .jar nécessaires.";
                report.RiskPct = 0;
            }
            // 6. Playwright Test Browsers
            else if (n.Contains("playwright") || pLower.Contains("ms-playwright"))
            {
                report.Definition = "Navigateurs web headless autonomes précompilés (Chromium, Firefox, WebKit) pour les tests automatisés.";
                report.Origin = "Téléchargé par la commande 'npx playwright install' lors de développements web ou de tests d'interface.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Réinstallable instantanément à tout moment avec 'npx playwright install'.";
                report.RiskPct = 0;
            }
            // 7. GPU Shaders (DirectX / NVIDIA / AMD)
            else if (n.Contains("shader") || pLower.Contains("dxcache") || pLower.Contains("glcache") || pLower.Contains("d3dscache"))
            {
                report.Definition = "Caches de compilation de shaders 3D générés par les pilotes graphiques (NVIDIA GeForce / AMD Radeon).";
                report.Origin = "Généré automatiquement par le pilote de votre carte graphique lors du lancement de jeux vidéo DirectX 11/12 ou Vulkan pour accélérer le rendu 3D.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Les shaders se régénèrent automatiquement au prochain lancement de vos jeux (permet fréquemment d'éliminer les micro-saccades).";
                report.RiskPct = 0;
            }
            // 8. Steam Downloading
            else if (pLower.Contains("downloading"))
            {
                report.Definition = "Fragments et paquets de téléchargements de jeux Steam interrompus, corrompus ou en attente.";
                report.Origin = "Généré automatiquement par le client Steam lors du téléchargement d'un jeu ou d'une mise à jour.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Steam relancera un téléchargement propre sans affecter vos jeux déjà installés.";
                report.RiskPct = 0;
            }
            // 9. Steam Client Principal
            else if (n.Equals("steam", StringComparison.OrdinalIgnoreCase) || pLower.EndsWith(@"\steam") || pLower.Contains(@"\steam\steam.exe"))
            {
                report.Definition = "Client officiel et lanceur de la plateforme de jeux vidéo Steam (Valve Corporation).";
                report.Origin = "Plateforme principale de jeux vidéo installée sur votre ordinateur (C:\\Program Files (x86)\\Steam).";
                report.SafetyImpact = "🔴 CONSERVATION ABSOLUE (Risque 90%). Ne supprimez surtout pas ce dossier : il contient le logiciel Steam, vos configurations et vos jeux.";
                report.RiskPct = 90;
            }
            // 10. Serveurs de Test (PTS)
            else if (n.Contains("pts") || n.Contains("public test"))
            {
                report.Definition = "Serveur de test public (Public Test Server) d'un jeu multijoueur pour tester les mises à jour en avant-première.";
                report.Origin = "Installé via Steam ou Epic Games pour participer aux bêtas de patchs.";
                report.SafetyImpact = "Suppression sûre (Risque 5%) si vous ne testez plus les bêtas. N'altère en rien votre compte principal de jeu.";
                report.RiskPct = 5;
            }
            // 11. Doublons Cross-Platform
            else if (n.Contains("doublon cross-platform") || n.Contains("cross-platform"))
            {
                report.Definition = "Jeu vidéo installé en double exemplaire à la fois sur Steam et sur Epic Games.";
                report.Origin = "Téléchargé sur deux plateformes de jeux différentes pour le même titre.";
                report.SafetyImpact = "La suppression de l'un des deux exemplaires libère l'espace sans perdre votre progression (les sauvegardes sont synchronisées sur le cloud du jeu).";
                report.RiskPct = 5;
            }
            // 12. Émulateurs Android (Nox, BlueStacks, LDPlayer)
            else if (n.Contains("nox") || pLower.Contains("nox") || n.Contains("bluestacks") || n.Contains("ldplayer") || n.Contains("memu"))
            {
                report.Definition = "Émulateur Android sous machine virtuelle pour faire tourner des applications mobiles sur Windows.";
                report.Origin = "Installé manuellement pour jouer à des jeux mobiles Android sur grand écran.";
                report.SafetyImpact = "100% sûr à supprimer si vous ne jouez plus à des jeux mobiles sur votre PC. Aucun impact sur Windows.";
                report.RiskPct = 5;
            }
            // 13. Émulateurs Nintendo Switch / Consoles (Yuzu, Ryujinx, Dolphin)
            else if (n.Contains("yuzu") || pLower.Contains("yuzu") || n.Contains("ryujinx") || pLower.Contains("ryujinx") || n.Contains("dolphin") || pLower.Contains("dolphin"))
            {
                report.Definition = "Émulateur de console de jeux vidéo et répertoire de ROMs / configurations associées.";
                report.Origin = "Installé pour l'émulation de jeux de console sur PC.";
                report.SafetyImpact = "Sûr à supprimer si vous n'émulez plus cette console. Pensez à sauvegarder vos fichiers de sauvegarde (.sav) si vous en avez.";
                report.RiskPct = 10;
            }
            // 14. Mod Managers (Thunderstore, r2modman)
            else if (n.Contains("thunderstore") || n.Contains("r2modman") || pLower.Contains("r2modman"))
            {
                report.Definition = "Gestionnaire de mods communautaires pour jeux vidéo Unity (Lethal Company, Risk of Rain 2, etc.).";
                report.Origin = "Installé pour télécharger et synchroniser des profils de mods entre joueurs.";
                report.SafetyImpact = "Sûr à supprimer si vous ne jouez plus aux jeux moddés correspondants.";
                report.RiskPct = 5;
            }
            // 15. Overlays (Overwolf, Medal)
            else if (n.Contains("overwolf") || pLower.Contains("overwolf"))
            {
                report.Definition = "Moteur d'overlay en surimpression et plateforme d'extensions pour jeux vidéo (CurseForge, trackers de stats).";
                report.Origin = "Installé souvent avec un gestionnaire de mods de jeux (CurseForge) ou un tracker de statistiques de jeu.";
                report.SafetyImpact = "Sûr à supprimer si vous n'utilisez pas d'overlay ou de mods CurseForge. Allège les processus en mémoire.";
                report.RiskPct = 5;
            }
            else if (n.Contains("medal") || pLower.Contains("medal"))
            {
                report.Definition = "Logiciel de capture vidéo en continu et d'enregistrement de replays/clips de jeu.";
                report.Origin = "Installé pour enregistrer des temps forts de gaming et les partager.";
                report.SafetyImpact = "Sûr à supprimer si vous n'enregistrez plus vos parties avec Medal.";
                report.RiskPct = 5;
            }
            // 16. Updaters Electron (*-updater)
            else if (n.Contains("updater") || pLower.Contains("-updater"))
            {
                report.Definition = "Dossier temporaire de mise à jour automatique d'une application Electron (Discord, Slack, etc.).";
                report.Origin = "Téléchargé silencieusement en tâche de fond lors du déploiement d'une nouvelle version de l'application.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Ce sont de simples installeurs résiduels de mises à jour déjà appliquées.";
                report.RiskPct = 0;
            }
            // 17. Archives compressées (.zip, .rar, .7z, .tar, .iso)
            else if (pLower.EndsWith(".zip") || pLower.EndsWith(".rar") || pLower.EndsWith(".7z") || pLower.EndsWith(".tar.gz") || pLower.EndsWith(".iso"))
            {
                int ageDays = (int)(DateTime.Now - lastWrite).TotalDays;
                int ageYears = ageDays / 365;
                string ageStr = ageYears > 0 ? $"il y a plus de {ageYears} an{(ageYears > 1 ? "s" : "")}" : $"il y a {ageDays} jours";

                if (n.Contains("bluesoleil") || pLower.Contains("bluesoleil"))
                {
                    report.Definition = "Pilote et logiciel de gestion Bluetooth BlueSoleil (développé par IVT Corporation). Ancienne suite logicielle pour clés USB Bluetooth sous Windows XP/7/10.";
                    report.Origin = $"Archive d'installation téléchargée sur internet (Clubic/01net/IVT) le {lastWrite:dd MMMM yyyy} ({ageStr}) dans {Path.GetFileName(Path.GetDirectoryName(path))}.";
                    report.SafetyImpact = "🟢 SUPPRESSION 100% RECOMMANDÉE (Risque 0%). Windows 10 et 11 intègrent nativement leurs propres pilotes Bluetooth universels. Cette vieille archive de 2021 est totalement obsolète et inutile.";
                    report.RiskPct = 0;
                }
                else if (n.Contains("direct messages") || n.Contains("discord backup") || n.Contains("backup") || n.Contains("sauvegarde") || n.Contains("save"))
                {
                    report.Definition = "Archive contenant une sauvegarde personnelle (messages Discord, documents ou sauvegardes privées).";
                    report.Origin = $"Téléchargé ou créé manuellement dans {Path.GetFileName(Path.GetDirectoryName(path))} le {lastWrite:dd MMMM yyyy}.";
                    report.SafetyImpact = "🔴 CONSERVATION FORTEMENT CONSEILLÉE (Risque 95%). Ce fichier contient des données personnelles ou d'historique. Ne le supprimez que si vous en possédez une copie ailleurs.";
                    report.RiskPct = 95;
                }
                else if (n.Contains("better mc") || n.Contains("minecraft") || n.Contains("curseforge") || n.Contains("modpack") || n.Contains("forge") || n.Contains("fabric"))
                {
                    report.Definition = "Pack de mods ou ensemble d'extensions pour le jeu Minecraft.";
                    report.Origin = $"Téléchargé pour le modding Minecraft le {lastWrite:dd MMMM yyyy} ({ageStr}).";
                    report.SafetyImpact = "Si ce modpack a déjà été extrait ou installé dans votre lanceur Minecraft (CurseForge / Prism / .minecraft), cette archive zip est un doublon inutile supprimable (Risque 0%). Sinon, conservez-la.";
                    report.RiskPct = ageDays > 60 ? 0 : 5;
                }
                else
                {
                    report.Definition = $"Archive compressée ({Path.GetExtension(path).ToUpperInvariant()}) téléchargée sur internet.";
                    report.Origin = $"Dossier {Path.GetFileName(Path.GetDirectoryName(path))}, téléchargée le {lastWrite:dd MMMM yyyy} ({ageStr}).";
                    report.SafetyImpact = ageDays > 60
                        ? $"🟢 SUPPRESSION RECOMMANDÉE (Risque 0%). Ce fichier est un ancien reliquat de téléchargement inactif depuis {ageStr}. Si vous avez déjà extrait ou installé ce dont vous aviez besoin, cette archive ne sert plus à rien."
                        : $"Archive téléchargée récemment ({ageDays} jours). Vérifiez son contenu si vous n'avez pas encore terminé de l'utiliser.";
                    report.RiskPct = ageDays > 60 ? 0 : 10;
                }
            }
            // 18. Installeurs (.exe, .msi, .appimage)
            // 18. Installeurs (.exe, .msi, .appimage, .msix)
            else if (pLower.EndsWith(".exe") || pLower.EndsWith(".msi") || pLower.EndsWith(".appimage") || pLower.EndsWith(".msix"))
            {
                var vi = File.Exists(path) && pLower.EndsWith(".exe") ? FileVersionInfo.GetVersionInfo(path) : null;
                string prod = !string.IsNullOrWhiteSpace(vi?.ProductName) ? vi.ProductName : Path.GetFileNameWithoutExtension(path);
                string comp = !string.IsNullOrWhiteSpace(vi?.CompanyName) ? vi.CompanyName : "Éditeur internet";
                string desc = !string.IsNullOrWhiteSpace(vi?.FileDescription) ? vi.FileDescription : "Programme d'installation";

                int ageDays = Math.Max(0, (int)(DateTime.Now - lastWrite).TotalDays);
                report.Definition = $"Fichier d'installation autonome de : {prod} ({desc}).";
                report.Origin = $"Téléchargé depuis internet ({comp}) le {lastWrite:dd MMMM yyyy} ({FormatAgeText(ageDays)}) dans {Path.GetFileName(Path.GetDirectoryName(path))}.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Ce n'est que l'installeur temporaire. Une fois le logiciel installé sur votre PC, ce fichier exécutable ne sert plus à rien et peut être supprimé sans affecter l'application installée.";
                report.RiskPct = ComputeRiskScore("installer", ageDays, false, false, false);
            }
            // 19. Caches & Outils Dev / IA / Navigateurs / Système étendus
            else if (n.Contains("npm") || pLower.Contains("npm-cache"))
            {
                report.Definition = "Cache local des paquets JavaScript / Node.js (npm).";
                report.Origin = "Créé automatiquement par Node.js / npm lors de l'installation de dépendances de projets web.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). npm re-téléchargera les paquets si besoin.";
                report.RiskPct = 0;
            }
            else if (n.Contains("pip") || pLower.Contains(@"\pip\"))
            {
                report.Definition = "Cache local des archives Python (.whl / .tar.gz) téléchargées par pip.";
                report.Origin = "Généré par Python lors de l'exécution de commandes 'pip install'.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Cache purement temporaire et régénérable.";
                report.RiskPct = 0;
            }
            else if (n.Contains("huggingface") || pLower.Contains("huggingface") || n.Contains("pytorch") || pLower.Contains(@"\torch"))
            {
                report.Definition = "Cache local de modèles d'intelligence artificielle (poids .safetensors / .bin / tokenizers).";
                report.Origin = "Téléchargé par des scripts Python, HuggingFace Hub, ComfyUI, Stable Diffusion ou PyTorch.";
                report.SafetyImpact = "Suppression sûre pour le système (Risque 5-10%). Vous devrez re-télécharger ces modèles si vous relancez les scripts IA associés.";
                report.RiskPct = 5;
            }
            else if (n.Contains("cargo") || pLower.Contains(".cargo"))
            {
                report.Definition = "Registre et cache des bibliothèques Rust (crates) compilées.";
                report.Origin = "Généré par Cargo lors de la compilation de projets Rust.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Cargo re-téléchargera les dépendances à la prochaine compilation.";
                report.RiskPct = 0;
            }
            else if (n.Contains("unity") && pLower.Contains("cache"))
            {
                report.Definition = "Cache de paquets et d'assets du moteur de jeu Unity.";
                report.Origin = "Généré par Unity Hub / Unity Editor lors de l'ouverture de projets 3D/C#.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Unity re-téléchargera les paquets nécessaires à la prochaine ouverture de projet.";
                report.RiskPct = 0;
            }
            else if (n.Contains("unreal") || pLower.Contains("deriveddatacache"))
            {
                report.Definition = "DerivedDataCache : shaders et textures pré-compilés d'Unreal Engine.";
                report.Origin = "Généré par Unreal Editor lors de l'ouverture ou de la compilation de scènes 3D.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Régénéré automatiquement à l'ouverture d'Unreal (le premier chargement sera juste un peu plus long).";
                report.RiskPct = 0;
            }
            else if (n.Contains("crashdump") || pLower.Contains("crashdumps") || n.Contains("minidump") || pLower.Contains("minidump"))
            {
                report.Definition = "Fichiers mémoire de diagnostic (.dmp) enregistrés lors d'anciens plantages logiciels ou Windows.";
                report.Origin = "Généré automatiquement par Windows Error Reporting (WER) après un crash.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Fichiers de diagnostic obsolètes sans aucune utilité pour le fonctionnement du PC.";
                report.RiskPct = 0;
            }
            else if (n.Contains("softwaredistribution") || pLower.Contains("softwaredistribution"))
            {
                report.Definition = "Cache des fichiers d'installation Windows Update déjà téléchargés.";
                report.Origin = "Généré par le service Windows Update lors des mises à jour mensuelles de Windows.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Libère de l'espace sans affecter les mises à jour déjà installées.";
                report.RiskPct = 0;
            }
            else if (n.Contains("voicemod") || pLower.Contains("voicemod"))
            {
                report.Definition = "Modulateur de voix en temps réel et banque d'effets sonores (Voicemod).";
                report.Origin = "Installé pour modifier la voix sur Discord ou en jeu.";
                report.SafetyImpact = "Sûr à supprimer si vous n'utilisez plus Voicemod.";
                report.RiskPct = running ? 55 : 10;
            }
            else if (n.Contains("dry cactus") || n.Contains("poly bridge"))
            {
                report.Definition = "Données, sauvegardes et niveaux personnalisés des jeux Poly Bridge (studio Dry Cactus).";
                report.Origin = "Créé dans AppData par Poly Bridge lors de vos parties.";
                report.SafetyImpact = "Contient potentiellement des sauvegardes locales si le cloud Steam n'est pas activé.";
                report.RiskPct = 20;
            }
            else if (n.Contains("radmin") || n.Contains("pulse secure") || n.Contains("hamachi"))
            {
                report.Definition = "Outil de réseau privé virtuel (VPN) ou de LAN virtuel pour jouer/travailler à distance.";
                report.Origin = "Installé pour créer un réseau local virtuel (LAN gaming) ou accéder à un réseau d'entreprise/école.";
                report.SafetyImpact = running ? "⚠️ Service réseau actif. Vérifiez si vous en avez encore besoin avant de désinstaller." : "Sûr à supprimer si vous n'utilisez plus ce VPN.";
                report.RiskPct = running ? 60 : 15;
            }
            else if (n.Contains("discord") || n.Contains("slack") || n.Contains("teams") || n.Contains("rocket.chat"))
            {
                report.Definition = "Application de messagerie vocale/texte et ses données locales.";
                report.Origin = "Installée pour la communication (gaming, études ou travail).";
                report.SafetyImpact = running ? "🔴 Application active en cours d'utilisation !" : "Si c'est une ancienne messagerie inutilisée (ex: Slack/Teams d'un ancien stage), suppression sûre (vos messages restent sur le cloud).";
                report.RiskPct = running ? 85 : 25;
            }
            else if (n.Contains("spotify") || n.Contains("cache navigateur") || pLower.Contains(@"\default\cache"))
            {
                report.Definition = "Cache média/web temporaire (images, flux audio, scripts mis en cache).";
                report.Origin = "Généré automatiquement par votre navigateur ou Spotify pour accélérer le chargement.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Aucun favori, mot de passe ou playlist n'est supprimé.";
                report.RiskPct = 0;
            }
            else if (n.Contains("corbeille") || pLower.Contains("$recycle.bin"))
            {
                report.Definition = "Corbeille Windows contenant des fichiers déjà supprimés par l'utilisateur.";
                report.Origin = "Fichiers mis à la corbeille sur ce disque.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Vide définitivement les fichiers déjà jetés.";
                report.RiskPct = 0;
            }
            else if (n.Contains("steam workshop") || pLower.Contains(@"\workshop\"))
            {
                report.Definition = "Contenu téléchargé depuis le Steam Workshop (mods, cartes, skins communautaires).";
                report.Origin = "Téléchargé automatiquement par Steam selon vos abonnements Workshop.";
                report.SafetyImpact = "Suppression sûre : libère souvent plusieurs Go. Steam re-téléchargera les mods si vous êtes toujours abonné.";
                report.RiskPct = 10;
            }
            else if (n.Contains("shadercache") || pLower.Contains(@"\shadercache"))
            {
                report.Definition = "Cache de shaders pré-compilés par Steam pour vos jeux.";
                report.Origin = "Téléchargé/compilé par Steam pour éviter les saccades en jeu.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Régénérable automatiquement au lancement des jeux.";
                report.RiskPct = 0;
            }
            // 20. Jeux portables anciens
            else if (n.Contains("jeu portable") || pLower.Contains("autres jeux") || pLower.Contains(@"\jeux\"))
            {
                int ageDays = Math.Max(0, (int)(DateTime.Now - lastWrite).TotalDays);
                report.Definition = "Dossier de jeu vidéo portable (sans installeur Windows officiel).";
                report.Origin = $"Dossier de jeu extrait ou téléchargé sur vos disques secondaires (dernière activité : {FormatAgeText(ageDays)}).";
                report.SafetyImpact = "Suppression sécurisée si vous n'y jouez plus. Conservez vos sauvegardes si le jeu les stocke dans son propre répertoire.";
                report.RiskPct = ComputeRiskScore("portable_game", ageDays, running, false, false);
            }
            // 21. ENQUÊTE FORENSIQUE DYNAMIQUE MULTI-NIVEAUX (Registre + EXE/DLL + Structure interne)
            else
            {
                int ageDays = Math.Max(0, (int)(DateTime.Now - lastWrite).TotalDays);
                var regDetails = FindRegistryAppDetails(baseName, name);
                var exeDetails = FindExeMetadata(path);
                var forensicSummary = InspectDirectoryForensics(path);

                if (regDetails != null)
                {
                    bool isCore = IsCoreSystemOrRuntime(regDetails.DisplayName, path);
                    report.Definition = $"Application Windows : {regDetails.DisplayName}{(string.IsNullOrEmpty(regDetails.DisplayVersion) ? "" : " (v" + regDetails.DisplayVersion + ")")}, développée par {regDetails.Publisher}.{(string.IsNullOrEmpty(forensicSummary) ? "" : " " + forensicSummary)}";
                    report.Origin = $"Installée officiellement sur votre PC{(string.IsNullOrEmpty(regDetails.InstallDate) ? "" : " le " + regDetails.InstallDate)} (enregistrée dans le Registre Windows). Emplacement : {path}.";
                    report.SafetyImpact = isCore
                        ? "🔴 COMPOSANT SYSTÈME OU PLATEFORME ESSENTIELLE. Conservation fortement recommandée."
                        : (running
                            ? "⚠️ L'application est actuellement ouverte en mémoire. Fermez-la avant toute désinstallation."
                            : (ageDays >= 365
                                ? $"🟢 INUTILISÉE DEPUIS {FormatAgeText(ageDays).ToUpperInvariant()}. Peut être désinstallée proprement sans risque pour libérer de l'espace."
                                : "Application enregistrée récemment utilisée. Peut être désinstallée proprement si vous n'en avez plus l'usage."));
                    report.RiskPct = ComputeRiskScore("installed_app", ageDays, running, isCore, IsPathSanctuarized(path));
                }
                else if (exeDetails != null)
                {
                    bool isCore = IsCoreSystemOrRuntime(exeDetails.ProductName, path);
                    report.Definition = $"{exeDetails.Description} — Produit : {exeDetails.ProductName}.{(string.IsNullOrEmpty(forensicSummary) ? "" : " " + forensicSummary)}";
                    report.Origin = $"Développé par {exeDetails.Company}. Emplacement : {path} (dernière activité : {FormatAgeText(ageDays)}).";
                    report.SafetyImpact = isCore
                        ? "🔴 COMPOSANT ESSENTIEL. Conservation recommandée."
                        : (running
                            ? "⚠️ Un processus lié tourne actuellement en mémoire. Fermez-le avant de nettoyer."
                            : (ageDays >= 365
                                ? $"🟢 Dossier logiciel inactif depuis {FormatAgeText(ageDays)}. Suppression sûre si vous n'utilisez plus {exeDetails.ProductName}."
                                : "Aucun processus actif. Peut être nettoyé si vous n'utilisez plus ce logiciel."));
                    report.RiskPct = ComputeRiskScore("installed_app", ageDays, running, isCore, IsPathSanctuarized(path));
                }
                else
                {
                    string parentContext = pLower.Contains(@"\appdata\local") ? "AppData\\Local (données et caches locaux d'application)"
                        : (pLower.Contains(@"\appdata\roaming") ? "AppData\\Roaming (profils, réglages utilisateur et sauvegardes)"
                        : (pLower.Contains(@"\program files") ? "Program Files (répertoire d'installation logiciel)"
                        : Path.GetDirectoryName(path) ?? "Disque local"));

                    report.Definition = !string.IsNullOrEmpty(forensicSummary)
                        ? $"Élément « {name} » — {forensicSummary}"
                        : $"Dossier de données ou reliquat d'application : {name}.";
                    report.Origin = $"Situé dans {parentContext}. Dernière modification enregistrée {FormatAgeText(ageDays)} ({lastWrite:dd MMMM yyyy}).";
                    report.SafetyImpact = running
                        ? "⚠️ Processus actif en mémoire. Fermez l'application liée avant de nettoyer."
                        : (ageDays >= 365
                            ? $"🟢 Élément totalement inactif depuis {FormatAgeText(ageDays)}. Fortement probable qu'il s'agisse d'un reliquat supprimable sans impact sur Windows."
                            : "Inactif actuellement. Vérifiez le contenu ou utilisez « ✨ Approfondir avec l'IA » pour identifier l'éditeur exact.");
                    report.RiskPct = ComputeRiskScore(pLower.Contains("cache") ? "appdata_cache" : "appdata_inactive", ageDays, running, false, IsPathSanctuarized(path));
                }
            }

            return report;
        }

        private static string InspectDirectoryForensics(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    return $"Fichier unique {fi.Extension.ToUpperInvariant()} de {Math.Round(fi.Length / 1024.0 / 1024.0, 1)} Mo.";
                }
                if (!Directory.Exists(path)) return "";

                var topFiles = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(x => x != null).ToList();
                var topDirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(x => x != null).ToList();

                var hints = new List<string>();
                if (topFiles.Any(f => f!.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase)) || topDirs.Any(d => d!.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)))
                    hints.Add("Moteur Unity détecté (jeu ou application 3D)");
                if (topFiles.Any(f => f!.Equals("app.asar", StringComparison.OrdinalIgnoreCase)) || topDirs.Contains("resources", StringComparer.OrdinalIgnoreCase) && File.Exists(Path.Combine(path, "resources", "app.asar")))
                    hints.Add("Application bureau basée sur Electron / Chromium");
                if (topDirs.Any(d => d!.Equals("GPUCache", StringComparison.OrdinalIgnoreCase) || d.Equals("Code Cache", StringComparison.OrdinalIgnoreCase) || d.Equals("Cache_Data", StringComparison.OrdinalIgnoreCase)))
                    hints.Add("Contient des sous-dossiers de cache web Chromium régénérables");
                if (topFiles.Any(f => f!.StartsWith("unins", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                    hints.Add("Contient un désinstalleur officiel Windows (unins*.exe)");
                if (topFiles.Any(f => f!.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase)) || topDirs.Contains("Engine", StringComparer.OrdinalIgnoreCase))
                    hints.Add("Ressources de jeu (fichiers .pak / Unreal Engine)");
                if (topFiles.Any(f => f!.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
                    hints.Add("Contient des archives Java (.jar)");
                if (topFiles.Any(f => f!.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase)))
                    hints.Add("Contient des journaux (.log) ou dumps mémoire (.dmp)");

                if (hints.Count == 0 && (topFiles.Count > 0 || topDirs.Count > 0))
                {
                    var sample = topDirs.Take(3).Concat(topFiles.Take(3)).Take(4);
                    hints.Add($"Contenu principal : {string.Join(", ", sample)}");
                }

                return string.Join(" • ", hints);
            }
            catch
            {
                return "";
            }
        }

        private class RegAppInfo
        {
            public string DisplayName { get; set; } = "";
            public string Publisher { get; set; } = "Éditeur inconnu";
            public string DisplayVersion { get; set; } = "";
            public string InstallDate { get; set; } = "";
        }

        private static RegAppInfo? FindRegistryAppDetails(string baseName, string fullName)
        {
            var keys = new[]
            {
                (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, subKeyPath) in keys)
            {
                try
                {
                    using var baseKey = hive.OpenSubKey(subKeyPath);
                    if (baseKey == null) continue;

                    foreach (var sub in baseKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var k = baseKey.OpenSubKey(sub);
                            if (k == null) continue;

                            var disp = k.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(disp)) continue;

                            if (disp.Contains(baseName, StringComparison.OrdinalIgnoreCase) ||
                                baseName.Contains(disp, StringComparison.OrdinalIgnoreCase) ||
                                fullName.Contains(disp, StringComparison.OrdinalIgnoreCase))
                            {
                                return new RegAppInfo
                                {
                                    DisplayName = disp,
                                    Publisher = k.GetValue("Publisher") as string ?? "Éditeur officiel",
                                    DisplayVersion = k.GetValue("DisplayVersion") as string ?? "",
                                    InstallDate = k.GetValue("InstallDate") as string ?? ""
                                };
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return null;
        }

        private class ExeMetadata
        {
            public string ProductName { get; set; } = "";
            public string Description { get; set; } = "";
            public string Company { get; set; } = "";
        }

        private static ExeMetadata? FindExeMetadata(string path)
        {
            try
            {
                if (File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    return new ExeMetadata
                    {
                        ProductName = string.IsNullOrWhiteSpace(vi.ProductName) ? Path.GetFileNameWithoutExtension(path) : vi.ProductName,
                        Description = string.IsNullOrWhiteSpace(vi.FileDescription) ? "Programme exécutable Windows" : vi.FileDescription,
                        Company = string.IsNullOrWhiteSpace(vi.CompanyName) ? "Éditeur logiciel" : vi.CompanyName
                    };
                }

                if (Directory.Exists(path))
                {
                    var exes = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly)
                                        .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase))
                                        .ToArray();
                    if (exes.Length == 0)
                    {
                        foreach (var sub in Directory.GetDirectories(path).Take(5))
                        {
                            try
                            {
                                var subExes = Directory.GetFiles(sub, "*.exe", SearchOption.TopDirectoryOnly);
                                if (subExes.Length > 0) { exes = subExes; break; }
                            }
                            catch { }
                        }
                    }
                    if (exes.Length > 0)
                    {
                        var mainExe = exes.OrderByDescending(f => new FileInfo(f).Length).First();
                        var vi = FileVersionInfo.GetVersionInfo(mainExe);
                        return new ExeMetadata
                        {
                            ProductName = string.IsNullOrWhiteSpace(vi.ProductName) ? Path.GetFileNameWithoutExtension(mainExe) : vi.ProductName,
                            Description = string.IsNullOrWhiteSpace(vi.FileDescription) ? "Fichiers exécutables de l'application" : vi.FileDescription,
                            Company = string.IsNullOrWhiteSpace(vi.CompanyName) ? "Éditeur logiciel" : vi.CompanyName
                        };
                    }
                }
            }
            catch { }
            return null;
        }

        public static async Task<ResearchReport> ResearchItemWithAIAsync(string name, string path, int initialRisk = 5, string? customApiKey = null)
        {
            var report = ResearchItem(name, path, initialRisk, bypassAiCache: true);

            var settings = GetSettings();
            string apiKey = !string.IsNullOrWhiteSpace(customApiKey)
                ? customApiKey.Trim()
                : (!string.IsNullOrWhiteSpace(settings.GeminiApiKey) ? settings.GeminiApiKey.Trim() : (Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "").Trim());

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                report.AiModelUsed = "Clé API non configurée";
                return report;
            }

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);

                var prompt = $@"Tu es un expert système Windows et rétro-ingénierie logicielle. Un utilisateur analyse son PC avec un logiciel de nettoyage et souhaite comprendre exactement ce qu'est cet élément détecté sur son disque :

- Nom affiché : {name}
- Emplacement : {path}
- Dernière modification : {report.LastAccessDate}
- Analyse locale préalable : {report.Definition}
- Processus en mémoire : {(report.IsRunning ? "OUI, processus lié actif" : "NON, aucun processus lié actif")}

Effectue une analyse précise (cherche s'il s'agit d'un jeu, d'un outil, d'un cache applicatif, d'un pilote, d'un installeur, d'un mod, d'un émulateur, etc.).
Réponds EXCLUSIVEMENT sous la forme d'un objet JSON valide respectant cette structure exacte :
{{
  ""definition"": ""Explication claire et accessible de ce qu'est cet élément et son rôle technique exact."",
  ""origin"": ""Pourquoi et par quoi il a été installé ou téléchargé (nom du logiciel parent, contexte, etc.)."",
  ""usageActivity"": ""Analyse de son utilisation récente ou de son abandon dans le temps."",
  ""safetyImpact"": ""Impact exact sur Windows ou sur vos logiciels si supprimé. Recommandation claire : SUPPRESSION CONSEILLÉE ou CONSERVATION RECOMMANDÉE."",
  ""riskPct"": 0
}}";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                string[] modelsToTry = { "gemini-3.8-flash", "gemini-3.6-flash", "gemini-2.5-flash", "gemini-3.5-flash-lite" };
                HttpResponseMessage? response = null;
                string usedModel = modelsToTry[0];

                foreach (var modelName in modelsToTry)
                {
                    usedModel = modelName;
                    using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                    response = await http.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                        break;

                    int code = (int)response.StatusCode;
                    // Repli automatique sur un autre modèle si indisponible (404, 429, 500, 502, 503, 504)
                    if (code != 404 && code != 429 && code < 500)
                        break;

                    await Task.Delay(350);
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    var respJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                        using var parsedAi = JsonDocument.Parse(text);
                        var root = parsedAi.RootElement;

                        if (root.TryGetProperty("definition", out var defProp) && !string.IsNullOrWhiteSpace(defProp.GetString()))
                            report.Definition = "✨ [IA Gemini] " + defProp.GetString();

                        if (root.TryGetProperty("origin", out var origProp) && !string.IsNullOrWhiteSpace(origProp.GetString()))
                            report.Origin = "✨ [IA Gemini] " + origProp.GetString();

                        if (root.TryGetProperty("usageActivity", out var useProp) && !string.IsNullOrWhiteSpace(useProp.GetString()))
                            report.UsageActivity = "✨ [IA Gemini] " + useProp.GetString();

                        if (root.TryGetProperty("safetyImpact", out var safeProp) && !string.IsNullOrWhiteSpace(safeProp.GetString()))
                            report.SafetyImpact = "✨ [IA Gemini] " + safeProp.GetString();

                        if (root.TryGetProperty("riskPct", out var riskProp) && riskProp.TryGetInt32(out int aiRisk))
                            report.RiskPct = aiRisk;

                        report.IsAiEnriched = true;
                        report.AiModelUsed = usedModel;
                        SaveAiCacheEntry(report);
                    }
                }
                else
                {
                    string errDetail = "";
                    if (response != null)
                    {
                        try
                        {
                            var errBody = await response.Content.ReadAsStringAsync();
                            using var errDoc = JsonDocument.Parse(errBody);
                            if (errDoc.RootElement.TryGetProperty("error", out var errObj) && errObj.TryGetProperty("message", out var msgProp))
                                errDetail = msgProp.GetString() ?? "";
                        }
                        catch { }
                        report.AiModelUsed = $"Erreur API HTTP {(int)response.StatusCode}{(string.IsNullOrEmpty(errDetail) ? "" : " : " + errDetail)}";
                    }
                    else
                    {
                        report.AiModelUsed = "Aucune réponse du serveur Gemini.";
                    }
                }
            }
            catch (Exception ex)
            {
                report.AiModelUsed = $"Erreur : {ex.Message}";
            }

            return report;
        }

        public static async Task<bool> TestGeminiApiKeyAsync(string apiKey)
        {
            var (ok, _) = await TestGeminiApiKeyDetailedAsync(apiKey);
            return ok;
        }

        public static async Task<(bool Success, string Message)> TestGeminiApiKeyDetailedAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return (false, "Veuillez saisir une clé API.");
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey.Trim()}";
                var res = await http.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    return (true, "✓ Clé valide & connectée à Google Gemini !");
                }

                string errMsg = $"HTTP {(int)res.StatusCode}";
                try
                {
                    var body = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out var errObj) && errObj.TryGetProperty("message", out var msgProp))
                    {
                        errMsg = $"{errMsg} : {msgProp.GetString()}";
                    }
                }
                catch { }
                return (false, $"✗ {errMsg}");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Erreur réseau : {ex.Message}");
            }
        }

        #region HISTORIQUE DE NETTOYAGE

        private static string GetHistoryFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanerApp");
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

        private static bool TryRunSilentUninstall(string uninstallStr)
        {
            if (string.IsNullOrWhiteSpace(uninstallStr)) return false;
            try
            {
                string fileName = "";
                string args = "";
                var trimmed = uninstallStr.Trim();

                if (trimmed.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                {
                    var m = Regex.Match(trimmed, @"(\{[0-9A-Fa-f\-]+\})");
                    if (m.Success)
                    {
                        fileName = "msiexec.exe";
                        args = $"/X{m.Groups[1].Value} /qn /norestart";
                    }
                }
                else
                {
                    if (trimmed.StartsWith("\""))
                    {
                        int endQuote = trimmed.IndexOf('"', 1);
                        if (endQuote > 1)
                        {
                            fileName = trimmed.Substring(1, endQuote - 1);
                            args = trimmed.Substring(endQuote + 1).Trim();
                        }
                    }
                    else
                    {
                        var exeIdx = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (exeIdx > 0)
                        {
                            fileName = trimmed.Substring(0, exeIdx + 4);
                            args = trimmed.Substring(exeIdx + 4).Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                    {
                        var fnameLower = Path.GetFileName(fileName).ToLowerInvariant();
                        if (fnameLower.StartsWith("unins"))
                        {
                            if (!args.Contains("VERYSILENT", StringComparison.OrdinalIgnoreCase))
                                args = $"{args} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART".Trim();
                        }
                        else if (!args.Contains("/S", StringComparison.Ordinal) && !args.Contains("/qn", StringComparison.OrdinalIgnoreCase))
                        {
                            args = $"{args} /S".Trim();
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (string.IsNullOrEmpty(fileName)) return false;

                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                if (proc != null)
                {
                    if (!proc.WaitForExit(35000))
                    {
                        try { proc.Kill(true); } catch { }
                        return false;
                    }
                    return proc.ExitCode == 0;
                }
            }
            catch { }
            return false;
        }

        public static CleanResult DeleteItem(CleanerItem item)
        {
            // Vérification de la Liste Protégée (choix explicite de l'utilisateur)
            if (IsProtected(item.Path, item.Name))
            {
                return new CleanResult
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    FreedGB = 0,
                    Success = false,
                    Message = "Suppression bloquée : Cet élément fait partie de votre Liste Protégée. Retirez-le de la liste protégée si vous souhaitez le supprimer."
                };
            }

            // Vérification de la sanctuarisation universelle
            foreach (var sp in GetSanctuarizedRoots())
            {
                if (item.Path.StartsWith(sp, StringComparison.OrdinalIgnoreCase))
                {
                    return new CleanResult
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        FreedGB = 0,
                        Success = false,
                        Message = $"Suppression interdite : Emplacement sanctuarisé ({item.Path})."
                    };
                }
            }

            try
            {
                // Suppression sécurisée d'une trace fantôme dans le Registre (strictement limitée aux clés Uninstall)
                if (item.Path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase) || item.Path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
                {
                    bool isCu = item.Path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase);
                    string subKey = item.Path.Substring(5);
                    if (!subKey.Contains(@"\CurrentVersion\Uninstall\", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = 0, Success = false, Message = "Clé registre hors périmètre Uninstall." };
                    }
                    try
                    {
                        using var rootHive = isCu ? Registry.CurrentUser : Registry.LocalMachine;
                        rootHive.DeleteSubKeyTree(subKey, false);
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = 0, Success = true, Message = "Trace registre orpheline supprimée." };
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = 0, Success = false, Message = "Droits administrateur requis pour supprimer cette clé HKLM (relancez l'app en administrateur)." };
                    }
                }

                // Si c'est une application avec un désinstalleur officiel, tenter d'abord la désinstallation silencieuse
                if (!string.IsNullOrWhiteSpace(item.UninstallString))
                {
                    double estGb = Directory.Exists(item.Path) ? Math.Round(GetDirectorySizeSafe(item.Path) / 1024.0 / 1024.0 / 1024.0, 2) : item.SizeGB;
                    if (TryRunSilentUninstall(item.UninstallString))
                    {
                        // Nettoyer les éventuels reliquats du dossier s'il existe encore
                        if (Directory.Exists(item.Path))
                        {
                            try { Directory.Delete(item.Path, true); } catch { }
                        }
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = estGb, Success = true, Message = "Application désinstallée silencieusement." };
                    }
                }

                if (File.Exists(item.Path))
                {
                    var fi = new FileInfo(item.Path);
                    double gb = Math.Round(fi.Length / 1024.0 / 1024.0 / 1024.0, 2);
                    File.Delete(item.Path);
                    return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = gb, Success = true, Message = "Fichier supprimé." };
                }
                else if (Directory.Exists(item.Path))
                {
                    double gb = Math.Round(GetDirectorySizeSafe(item.Path) / 1024.0 / 1024.0 / 1024.0, 2);

                    var baseName = Path.GetFileName(item.Path);
                    foreach (var proc in Process.GetProcessesByName(baseName))
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }

                    Directory.Delete(item.Path, true);

                    if (item.Path.EndsWith("downloading", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(item.Path);
                    }

                    return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = gb, Success = true, Message = "Dossier supprimé." };
                }
                else
                {
                    return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = 0, Success = true, Message = "Déjà supprimé." };
                }
            }
            catch (Exception ex)
            {
                return new CleanResult
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    FreedGB = 0,
                    Success = false,
                    Message = $"Erreur : {ex.Message}"
                };
            }
        }

        private static CleanerItem CreateItem(
            string name,
            string path,
            string category,
            double bytes,
            int riskPct,
            string desc,
            bool isFolder = true,
            bool isSanctuarized = false,
            bool isExtra = false,
            DateTime? lastModified = null,
            string uninstallString = "")
        {
            var drive = "";
            try { drive = Path.GetPathRoot(path)?.TrimEnd('\\') ?? ""; } catch { }

            string level = riskPct switch
            {
                <= 10 => "Quasi Nul",
                <= 35 => "Faible",
                <= 65 => "Modéré",
                _ => "Élevé"
            };

            var modDate = lastModified ?? DateTime.Now;
            int ageDays = Math.Max(0, (int)(DateTime.Now - modDate).TotalDays);
            long unixMs = new DateTimeOffset(modDate).ToUnixTimeMilliseconds();

            return new CleanerItem
            {
                Name = name,
                Path = path,
                Category = category,
                Drive = drive,
                SizeGB = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 2),
                SizeMB = Math.Round(bytes / 1024.0 / 1024.0, 1),
                RiskPct = riskPct,
                RiskLevel = level,
                Description = desc,
                LastModified = modDate.ToString("dd/MM/yyyy"),
                LastModifiedTimestamp = unixMs,
                AgeDays = ageDays,
                UninstallString = uninstallString,
                IsFolder = isFolder,
                IsSanctuarized = isSanctuarized,
                IsExtra = isExtra
            };
        }

        private static readonly EnumerationOptions SafeRecurseOpts = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        private static readonly EnumerationOptions SafeTopOpts = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        private static double GetDirectorySizeSafe(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                long sum = 0;
                foreach (var f in di.EnumerateFiles("*", SafeRecurseOpts))
                {
                    try { sum += f.Length; } catch { }
                }
                return sum;
            }
            catch
            {
                return 0;
            }
        }

        private static (long sizeBytes, int fileCount, DateTime lastWrite) GetDirectoryStatsSafe(string path)
        {
            long sum = 0;
            int count = 0;
            DateTime maxWrite = DateTime.MinValue;
            DateTime nowLimit = DateTime.Now.AddDays(1);
            try
            {
                var di = new DirectoryInfo(path);
                maxWrite = di.LastWriteTime;
                foreach (var f in di.EnumerateFiles("*", SafeRecurseOpts))
                {
                    try
                    {
                        sum += f.Length;
                        count++;
                        var lw = f.LastWriteTime;
                        var ct = f.CreationTime;
                        var eff = (ct > lw && ct <= nowLimit) ? ct : lw;
                        if (eff > maxWrite && eff <= nowLimit) maxWrite = eff;
                    }
                    catch { }
                }
            }
            catch { }
            if (maxWrite == DateTime.MinValue) maxWrite = DateTime.Now;
            return (sum, count, maxWrite);
        }

        private static Dictionary<string, DateTime> _recentFilesCache = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime _recentFilesCacheTime = DateTime.MinValue;

        private static Dictionary<string, DateTime> GetRecentFilesActivityMap()
        {
            if (_recentFilesCache.Count > 0 && (DateTime.Now - _recentFilesCacheTime).TotalMinutes < 5)
                return _recentFilesCache;

            var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var recentDir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                if (Directory.Exists(recentDir))
                {
                    foreach (var lnk in Directory.EnumerateFiles(recentDir, "*.lnk", SafeTopOpts))
                    {
                        try
                        {
                            var fi = new FileInfo(lnk);
                            var targetName = Path.GetFileNameWithoutExtension(fi.Name);
                            if (string.IsNullOrWhiteSpace(targetName)) continue;
                            if (!map.TryGetValue(targetName, out var ex) || fi.LastWriteTime > ex)
                                map[targetName] = fi.LastWriteTime;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            _recentFilesCache = map;
            _recentFilesCacheTime = DateTime.Now;
            return map;
        }

        private static DateTime GetFileEffectiveActivityDate(FileInfo fi)
        {
            DateTime nowLimit = DateTime.Now.AddDays(1);
            DateTime best = fi.LastWriteTime;
            try
            {
                if (fi.CreationTime > best && fi.CreationTime <= nowLimit)
                    best = fi.CreationTime;

                var recentMap = GetRecentFilesActivityMap();
                var baseName = Path.GetFileNameWithoutExtension(fi.Name);
                if (recentMap.TryGetValue(fi.Name, out var r1) && r1 > best && r1 <= nowLimit)
                    best = r1;
                else if (!string.IsNullOrEmpty(baseName) && recentMap.TryGetValue(baseName, out var r2) && r2 > best && r2 <= nowLimit)
                    best = r2;
            }
            catch { }
            return best;
        }

        private static DateTime GetLastWriteTimeSafe(DirectoryInfo di)
        {
            try
            {
                DateTime nowLimit = DateTime.Now.AddDays(1);
                DateTime latest = (di.CreationTime > di.LastWriteTime && di.CreationTime <= nowLimit) ? di.CreationTime : di.LastWriteTime;
                foreach (var f in di.EnumerateFiles("*", SafeRecurseOpts))
                {
                    try
                    {
                        var lw = f.LastWriteTime;
                        var ct = f.CreationTime;
                        var eff = (ct > lw && ct <= nowLimit) ? ct : lw;
                        if (eff > latest && eff <= nowLimit) latest = eff;
                    }
                    catch { }
                }
                return latest;
            }
            catch
            {
                return di.LastWriteTime;
            }
        }

        #region EXPLORATEUR D'ESPACE ARBORESCENT (SPACE LENS)

        public static async Task<FolderNode> ScanFolderTreeAsync(string rootPath, Action<string, int>? progressCallback = null)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(rootPath)) rootPath = @"C:\";
                string normalized = rootPath.Trim();
                if (normalized.Length == 2 && normalized[1] == ':') normalized += @"\";

                progressCallback?.Invoke($"Lecture de {normalized}...", 5);

                var protectedList = GetProtectedItems();
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                var progDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\');
                var progX86Dir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\');
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');

                bool CheckSanctOrProtected(string p, string n, out bool isProt)
                {
                    isProt = protectedList.Any(x => (!string.IsNullOrEmpty(p) && x.Path.Equals(p, StringComparison.OrdinalIgnoreCase)) ||
                                                    (!string.IsNullOrEmpty(n) && x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
                    string pTrim = p.TrimEnd('\\');
                    bool isCriticalRoot = pTrim.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                                          pTrim.EndsWith(@"System32", StringComparison.OrdinalIgnoreCase);
                    return IsPathSanctuarized(p) || isCriticalRoot;
                }

                var rootInfo = new DirectoryInfo(normalized);
                bool rootSanct = CheckSanctOrProtected(normalized, rootInfo.Name, out bool rootProt);

                var rootNode = new FolderNode
                {
                    Name = string.IsNullOrEmpty(rootInfo.Name) ? normalized : rootInfo.Name,
                    Path = normalized,
                    IsFolder = true,
                    LastModified = rootInfo.Exists ? rootInfo.LastWriteTime.ToString("dd/MM/yyyy") : "",
                    IsSanctuarized = rootSanct,
                    IsProtected = rootProt
                };

                if (!rootInfo.Exists) return rootNode;

                var childrenBag = new ConcurrentBag<FolderNode>();

                try
                {
                    var subDirs = rootInfo.GetDirectories("*", SafeTopOpts);
                    int totalDirs = Math.Max(1, subDirs.Length);
                    int doneDirs = 0;

                    Parallel.ForEach(subDirs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) }, sub =>
                    {
                        try
                        {
                            if (sub.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Threading.Interlocked.Increment(ref doneDirs);
                                return;
                            }
                            var (bytes, fCount, lw) = GetDirectoryStatsSafe(sub.FullName);
                            bool sct = CheckSanctOrProtected(sub.FullName, sub.Name, out bool prt);
                            childrenBag.Add(new FolderNode
                            {
                                Name = sub.Name,
                                Path = sub.FullName,
                                SizeBytes = bytes,
                                SizeGB = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 2),
                                SizeMB = Math.Round(bytes / 1024.0 / 1024.0, 1),
                                IsFolder = true,
                                FileCount = fCount,
                                LastModified = lw.ToString("dd/MM/yyyy"),
                                IsSanctuarized = sct,
                                IsProtected = prt
                            });
                            int cur = System.Threading.Interlocked.Increment(ref doneDirs);
                            int pct = 10 + (int)Math.Round((cur / (double)totalDirs) * 85.0);
                            progressCallback?.Invoke($"Analyse ({cur}/{totalDirs}) : {sub.Name}", Math.Min(pct, 95));
                        }
                        catch { System.Threading.Interlocked.Increment(ref doneDirs); }
                    });
                }
                catch { }

                try
                {
                    var files = rootInfo.GetFiles("*", SafeTopOpts);
                    foreach (var fi in files)
                    {
                        try
                        {
                            long bytes = fi.Length;
                            bool sct = CheckSanctOrProtected(fi.FullName, fi.Name, out bool prt);
                            childrenBag.Add(new FolderNode
                            {
                                Name = fi.Name,
                                Path = fi.FullName,
                                SizeBytes = bytes,
                                SizeGB = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 2),
                                SizeMB = Math.Round(bytes / 1024.0 / 1024.0, 1),
                                IsFolder = false,
                                FileCount = 1,
                                LastModified = fi.LastWriteTime.ToString("dd/MM/yyyy"),
                                IsSanctuarized = sct,
                                IsProtected = prt
                            });
                        }
                        catch { }
                    }
                }
                catch { }

                var allChildren = childrenBag.ToList();
                long totalBytes = allChildren.Sum(c => c.SizeBytes);

                foreach (var c in allChildren)
                {
                    c.PctOfParent = totalBytes > 0 ? Math.Round((c.SizeBytes / (double)totalBytes) * 100.0, 1) : 0;
                }

                rootNode.SizeBytes = totalBytes;
                rootNode.SizeGB = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 2);
                rootNode.SizeMB = Math.Round(totalBytes / 1024.0 / 1024.0, 1);
                rootNode.FileCount = allChildren.Sum(c => c.FileCount);
                rootNode.Children = allChildren
                    .OrderByDescending(c => c.SizeBytes)
                    .Take(80)
                    .ToList();

                return rootNode;
            });
        }

        #endregion
        #endregion
    }
}
