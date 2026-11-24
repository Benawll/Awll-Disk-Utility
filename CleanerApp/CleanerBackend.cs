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
    public static partial class CleanerBackend
    {
        // Protection dynamique et adaptative des dossiers personnels et composants critiques (100% portable sur tout PC)
        private static List<string> GetSanctuarizedRoots()
        {
            var list = new List<string>
            {
                // Conservés uniquement pour les runtimes/pilotes critiques (aucun dossier personnel bloqué)
                // (Documents accessibles librement par l'utilisateur)
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

        public static async Task<List<CleanerItem>> RunAuditAsync(Action<string, int>? progressCallback = null, List<string>? selectedDrives = null, List<string>? customPaths = null)
        {
            return await Task.Run(() =>
            {
                HashSet<string>? targetDrives = null;
                if (selectedDrives != null && selectedDrives.Count > 0)
                {
                    targetDrives = new HashSet<string>(selectedDrives.Select(d => d.TrimEnd('\\')), StringComparer.OrdinalIgnoreCase);
                }

                var runningProcs = GetRunningProcessNamesSafe();
                ExecutionHistoryService.RefreshIfNeeded(force: true);
                var recommendedItems = new List<CleanerItem>();
                var extraItems = new List<CleanerItem>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (customPaths != null && customPaths.Count > 0)
                {
                    AuditCustomFolders(customPaths, recommendedItems, extraItems, seenPaths, runningProcs, progressCallback);
                }
                else
                {
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
                AuditSystemUniversal(recommendedItems, extraItems, targetDrives, runningProcs);
                foreach (var it in recommendedItems) seenPaths.Add(it.Path);

                // 4. Audit Recommandé : Téléchargements et disques secondaires (archives, installeurs)
                progressCallback?.Invoke("Analyse des fichiers téléchargés et archives...", 58);
                AuditDownloadsUniversal(recommendedItems, extraItems, targetDrives, runningProcs);
                foreach (var it in recommendedItems) seenPaths.Add(it.Path);

                // 5. Inventaire Complet : Autres jeux et logiciels installés
                //    Les éléments inutilisés depuis >= 365 jours (et non critiques) sont promus dans l'Audit Recommandé !
                progressCallback?.Invoke("Inventaire des jeux et logiciels installés (avec analyse d'inactivité)...", 72);
                DiscoverInstalledGamesAndApps(recommendedItems, extraItems, steamLibs, epicGames, seenPaths, seenNames, targetDrives, runningProcs);

                // 6. Découverte Dynamique des Dossiers Racines atypiques sur tous les disques (jeux portables, émulateurs, caches...)
                progressCallback?.Invoke("Analyse dynamique des dossiers racines multi-disques...", 86);
                AuditDriveRootsDynamic(recommendedItems, extraItems, seenPaths, targetDrives, runningProcs);
                AuditPersonalFolders(extraItems, seenPaths, targetDrives);
                }

                // 6b. Normalisation & déduplication globale (règles appliquées uniformément à tous les scanners)
                NormalizeAuditItemsGlobal(recommendedItems, extraItems, runningProcs);

                // 7. Tri par défaut : Recommandés par Risque croissant puis Taille décroissante ; Extra par Taille décroissante
                var orderedRecommended = recommendedItems
                    .Where(x => (customPaths != null && customPaths.Count > 0) || IsDriveSelected(x.Path, targetDrives))
                    .OrderBy(x => x.RiskPct)
                    .ThenByDescending(x => x.SizeGB)
                    .ToList();

                var orderedExtra = extraItems
                    .Where(x => (customPaths != null && customPaths.Count > 0) || IsDriveSelected(x.Path, targetDrives))
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

        private static HashSet<string> _runningExePaths = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _resolvedExeProcessNames = new(StringComparer.OrdinalIgnoreCase);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        private static HashSet<string> GetRunningProcessNamesSafe()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var exeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolvedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        set.Add(p.ProcessName);
                        string? file = null;
                        try { file = p.MainModule?.FileName; } catch { }
                        if (string.IsNullOrEmpty(file))
                        {
                            try
                            {
                                var sb = new StringBuilder(1024);
                                int sz = sb.Capacity;
                                if (QueryFullProcessImageName(p.Handle, 0, sb, ref sz))
                                    file = sb.ToString(0, sz);
                            }
                            catch { }
                        }
                        if (!string.IsNullOrEmpty(file))
                        {
                            exeSet.Add(file);
                            resolvedNames.Add(p.ProcessName);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            _runningExePaths = exeSet;
            _resolvedExeProcessNames = resolvedNames;
            return set;
        }

        private static string NormalizeProcToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        internal static bool IsLikelyRunning(string name, string path, HashSet<string> runningProcs, HashSet<string>? overrideExePaths = null, HashSet<string>? overrideResolvedNames = null)
        {
            if (runningProcs.Count == 0) return false;
            var exePaths = overrideExePaths ?? _runningExePaths;
            var resolvedNames = overrideResolvedNames ?? _resolvedExeProcessNames;

            if (!string.IsNullOrWhiteSpace(path) && exePaths.Count > 0)
            {
                string pNorm = path.TrimEnd('\\');
                if (exePaths.Any(ep => ep.Equals(pNorm, StringComparison.OrdinalIgnoreCase) || ep.StartsWith(pNorm + "\\", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Ne faire un matching par nom que pour les processus dont le chemin exact n'a pas pu être résolu
            var unresolvedProcs = runningProcs.Where(p => !resolvedNames.Contains(p)).ToList();
            if (unresolvedProcs.Count > 0)
            {
                var baseNorm = NormalizeProcToken(Path.GetFileNameWithoutExtension(path));
                var nameNorm = NormalizeProcToken(name);
                if (baseNorm.Length >= 4 && unresolvedProcs.Any(p =>
                {
                    var pNorm = NormalizeProcToken(p);
                    return pNorm.Contains(baseNorm) || baseNorm.Contains(pNorm);
                }))
                    return true;
                if (nameNorm.Length >= 4 && unresolvedProcs.Any(p =>
                {
                    var pNorm = NormalizeProcToken(p);
                    return pNorm == nameNorm;
                }))
                    return true;
            }

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

        private static void AuditCustomFolders(
            List<string> customPaths,
            List<CleanerItem> recommendedItems,
            List<CleanerItem> extraItems,
            HashSet<string> seenPaths,
            HashSet<string> runningProcs,
            Action<string, int>? progressCallback)
        {
            const long minDirBytes = 50L * 1024 * 1024;
            const long minFileBytes = 25L * 1024 * 1024;
            var targets = customPaths.Where(p => Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (int idx = 0; idx < targets.Count; idx++)
            {
                var root = targets[idx];
                int pct = 10 + (int)Math.Round(75.0 * (idx + 1) / Math.Max(1, targets.Count));
                progressCallback?.Invoke($"Analyse du dossier ciblé ({idx + 1}/{targets.Count}) : {Path.GetFileName(root)}", pct);

                try
                {
                    foreach (var sub in Directory.GetDirectories(root))
                    {
                        if (!seenPaths.Add(sub)) continue;
                        var di = new DirectoryInfo(sub);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        double bytes = GetDirectorySizeSafe(sub);
                        if (bytes < minDirBytes) continue;

                        var last = GetLastWriteTimeSafe(di);
                        int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                        bool running = IsLikelyRunning(di.Name, sub, runningProcs);
                        bool isCore = IsCoreSystemOrRuntime(di.Name, sub);
                        bool isCache = IsPureCacheOrDisposable(di.Name, sub);
                        string kind = isCache ? "pure_cache" : "appdata_inactive";
                        int risk = ComputeRiskScore(kind, daysAgo, running && !isCache, isCore, IsPathSanctuarized(sub));
                        bool isExtra = risk > 35 || running || isCore;
                        var item = CreateItem(
                            di.Name,
                            sub,
                            "system",
                            bytes,
                            risk,
                            $"Sous-dossier dans {Path.GetFileName(root)} ({FormatAgeText(daysAgo)}, {last:dd/MM/yyyy}).",
                            lastModified: last,
                            isFolder: true,
                            isExtra: isExtra
                        );
                        if (isExtra) extraItems.Add(item);
                        else recommendedItems.Add(item);
                    }

                    foreach (var file in Directory.GetFiles(root))
                    {
                        if (!seenPaths.Add(file)) continue;
                        var fi = new FileInfo(file);
                        if (fi.Length < minFileBytes) continue;
                        var eff = GetFileEffectiveActivityDate(fi);
                        int daysAgo = Math.Max(0, (int)(DateTime.Now - eff).TotalDays);
                        string ext = fi.Extension.ToLowerInvariant();
                        string kind = (ext == ".exe" || ext == ".msi") ? "installer"
                            : (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".iso") ? "archive" : "other";
                        int risk = ComputeRiskScore(kind, daysAgo, false, false, IsPathSanctuarized(file));
                        bool isExtra = risk > 35;
                        var item = CreateItem(
                            fi.Name,
                            file,
                            "downloads",
                            fi.Length,
                            risk,
                            $"Fichier dans {Path.GetFileName(root)} ({FormatAgeText(daysAgo)}, {eff:dd/MM/yyyy}).",
                            lastModified: eff,
                            isFolder: false,
                            isExtra: isExtra
                        );
                        if (isExtra) extraItems.Add(item);
                        else recommendedItems.Add(item);
                    }
                }
                catch { }
            }
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

    }
}