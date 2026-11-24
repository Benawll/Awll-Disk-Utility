using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public bool IsFolder { get; set; }
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
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
    }

    public class CleanResult
    {
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public double FreedGB { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public static class CleanerBackend
    {
        // Sanctuarisation dynamique et adaptative (universelle quel que soit le PC)
        private static List<string> GetSanctuarizedRoots()
        {
            var list = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NVIDIA", "NGX"),
                @"C:\New Doc", // Sanctuarisation spécifique demandée
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

        public static async Task<List<CleanerItem>> RunAuditAsync(Action<string, int>? progressCallback = null)
        {
            return await Task.Run(() =>
            {
                var items = new List<CleanerItem>();

                // 1. Découverte dynamique de Steam & Epic Games (Universelle)
                progressCallback?.Invoke("Détection dynamique des bibliothèques de jeux (Steam, Epic)...", 15);
                var steamLibs = DiscoverSteamLibraries();
                var epicGames = DiscoverEpicGames();

                // 2. Audit Jeux & Bibliothèques
                progressCallback?.Invoke("Scan des caches de téléchargement et doublons...", 35);
                AuditGamesUniversal(items, steamLibs, epicGames);

                // 3. Audit Système & Caches Développeurs
                progressCallback?.Invoke("Scan des caches système, GPU et outils de build...", 55);
                AuditSystemUniversal(items);

                // 4. Audit Téléchargements Universel
                progressCallback?.Invoke("Analyse du dossier Téléchargements et disques secondaires...", 75);
                AuditDownloadsUniversal(items);

                // 5. Audit Logiciels et Émulateurs
                progressCallback?.Invoke("Recherche des émulateurs et applications résiduelles...", 90);
                AuditEmulatorsUniversal(items);

                // 6. Marquage des éléments protégés
                var protectedList = GetProtectedItems();
                foreach (var it in items)
                {
                    it.IsProtected = protectedList.Any(p => p.Path.Equals(it.Path, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(it.Name, StringComparison.OrdinalIgnoreCase));
                }

                progressCallback?.Invoke("Finalisation de l'audit...", 100);
                return items.OrderBy(x => x.RiskPct).ThenByDescending(x => x.SizeGB).ToList();
            });
        }

        // =========================================================================
        // DEUXIÈME SCAN : INVENTAIRE EXHAUSTIF (TOUS LES JEUX & LOGICIELS DU PC)
        // =========================================================================
        public static async Task<List<CleanerItem>> RunExhaustiveScanAsync(Action<string, int>? progressCallback = null)
        {
            return await Task.Run(() =>
            {
                var items = new List<CleanerItem>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Découverte de toutes les bibliothèques
                progressCallback?.Invoke("Découverte des bibliothèques de jeux sur tous les disques...", 10);
                var steamLibs = DiscoverSteamLibraries();
                var epicGames = DiscoverEpicGames();

                // 2. Inventaire exhaustif de TOUS les jeux Steam (sur tous les disques)
                progressCallback?.Invoke("Inventaire exhaustif de tous les jeux Steam...", 25);
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
                                var drive = Path.GetPathRoot(gameDir)?.TrimEnd('\\') ?? "C:";
                                string appTitle = steamInfo?.Name ?? di.Name;
                                string activityDetail = steamInfo?.LastPlayed != null 
                                    ? $"Dernière partie : {steamInfo.LastPlayed.Value:dd/MM/yyyy}" 
                                    : $"Dernière mise à jour : {last:dd/MM/yyyy}";

                                items.Add(CreateItem(
                                    appTitle,
                                    gameDir,
                                    "games",
                                    size,
                                    5,
                                    $"Jeu Steam complet sur {drive} ({activityDetail})"
                                ));
                                seenNames.Add(di.Name);
                                if (!string.IsNullOrEmpty(steamInfo?.Name)) seenNames.Add(steamInfo.Name);
                            }
                        }
                    }
                }

                // 3. Inventaire exhaustif de TOUS les jeux Epic Games
                progressCallback?.Invoke("Inventaire exhaustif des jeux Epic Games...", 40);
                foreach (var eg in epicGames)
                {
                    if (seenPaths.Add(eg.Value))
                    {
                        double size = GetDirectorySizeSafe(eg.Value);
                        var di = new DirectoryInfo(eg.Value);
                        var last = GetLastWriteTimeSafe(di);
                        var drive = Path.GetPathRoot(eg.Value)?.TrimEnd('\\') ?? "C:";
                        items.Add(CreateItem(
                            eg.Key,
                            eg.Value,
                            "games",
                            size,
                            5,
                            $"Jeu Epic Games complet sur {drive} (Dernière activité : {last:dd/MM/yyyy})"
                        ));
                        seenNames.Add(eg.Key);
                    }
                }

                // 4. Inventaire des launchers & dossiers de jeux portables sur tous les disques
                progressCallback?.Invoke("Scan de Riot Games, Ubisoft, GOG, Battle.net et jeux portables...", 55);
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    var gameFolders = new[] { "Autres Jeux", "Jeux", "Games", "Old Games", "Riot Games", "GOG Games", "Ubisoft", "Battle.net" };
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
                                    var last = GetLastWriteTimeSafe(di);
                                    items.Add(CreateItem(
                                        $"{di.Name} ({gf} {drive.Name.TrimEnd('\\')})",
                                        sub,
                                        "games",
                                        size,
                                        5,
                                        $"Jeu ou composant ({gf}) sur {drive.Name.TrimEnd('\\')}. Dernière activité : {last:dd/MM/yyyy}"
                                    ));
                                    seenNames.Add(di.Name);
                                }
                            }
                        }
                    }
                }

                // 5. Inventaire de TOUTES les applications enregistrées dans le Registre Windows
                progressCallback?.Invoke("Inventaire des applications installées (Registre Windows HKLM/HKCU)...", 75);
                var regApps = GetRegistryInstalledApps(seenPaths, seenNames);
                items.AddRange(regApps);

                // 6. Scan complémentaire de Program Files & Program Files (x86) sur tous les disques
                progressCallback?.Invoke("Vérification des répertoires Program Files multi-disques...", 85);
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
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
                                    di.Name.Equals("Windows NT", StringComparison.OrdinalIgnoreCase)) continue;

                                if (seenPaths.Add(sub) && !seenNames.Contains(di.Name))
                                {
                                    double size = GetDirectorySizeSafe(sub);
                                    if (size > 20 * 1024 * 1024) // > 20 Mo
                                    {
                                        items.Add(CreateItem(
                                            di.Name,
                                            sub,
                                            "apps",
                                            size,
                                            5,
                                            $"Logiciel installé dans {pfp}"
                                        ));
                                        seenNames.Add(di.Name);
                                    }
                                }
                            }
                        }
                    }
                }

                // 7. Émulateurs sur tous les disques
                progressCallback?.Invoke("Inventaire des émulateurs sur tous les disques...", 92);
                AuditEmulatorsUniversal(items);

                // 8. Marquage des éléments protégés
                progressCallback?.Invoke("Application de la liste protégée...", 98);
                var protectedList = GetProtectedItems();
                foreach (var it in items)
                {
                    it.IsProtected = protectedList.Any(p => p.Path.Equals(it.Path, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(it.Name, StringComparison.OrdinalIgnoreCase));
                }

                progressCallback?.Invoke("Inventaire exhaustif terminé !", 100);
                return items.OrderByDescending(x => x.SizeGB).ToList();
            });
        }

        private static List<CleanerItem> GetRegistryInstalledApps(HashSet<string> seenPaths, HashSet<string> seenNames)
        {
            var list = new List<CleanerItem>();

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
                            var uninstallStr = appKey.GetValue("UninstallString") as string;
                            var displayIcon = appKey.GetValue("DisplayIcon") as string;
                            var publisher = appKey.GetValue("Publisher") as string ?? "Éditeur inconnu";
                            var version = appKey.GetValue("DisplayVersion") as string ?? "";

                            string effectivePath = "";
                            if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                            {
                                effectivePath = installLoc;
                            }
                            else if (!string.IsNullOrEmpty(displayIcon))
                            {
                                var iconPath = displayIcon.Split(',')[0].Trim('"');
                                if (File.Exists(iconPath))
                                {
                                    effectivePath = Path.GetDirectoryName(iconPath) ?? "";
                                }
                            }

                            if (!string.IsNullOrEmpty(effectivePath))
                            {
                                seenPaths.Add(effectivePath);
                            }

                            double sizeBytes = 0;
                            if (!string.IsNullOrEmpty(effectivePath) && Directory.Exists(effectivePath))
                            {
                                sizeBytes = GetDirectorySizeSafe(effectivePath);
                            }
                            else
                            {
                                var estSize = appKey.GetValue("EstimatedSize");
                                if (estSize is int esInt && esInt > 0) sizeBytes = esInt * 1024.0;
                                else if (estSize is long esLong && esLong > 0) sizeBytes = esLong * 1024.0;
                            }

                            var pathForDisplay = !string.IsNullOrEmpty(effectivePath) ? effectivePath : (uninstallStr ?? "Registre Windows");
                            var drive = "";
                            try { drive = Path.GetPathRoot(pathForDisplay)?.TrimEnd('\\') ?? "C:"; } catch { drive = "C:"; }

                            list.Add(CreateItem(
                                displayName,
                                pathForDisplay,
                                "apps",
                                sizeBytes,
                                5,
                                $"Application Windows enregistrée ({publisher}{(string.IsNullOrEmpty(version) ? "" : ", v" + version)})."
                            ));
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return list;
        }

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

        private static void AuditGamesUniversal(List<CleanerItem> items, List<string> steamLibs, Dictionary<string, string> epicGames)
        {
            var steamGames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lib in steamLibs)
            {
                var steamApps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamApps)) continue;

                // A. Téléchargements bloqués (downloading)
                var dlPath = Path.Combine(steamApps, "downloading");
                if (Directory.Exists(dlPath))
                {
                    double size = GetDirectorySizeSafe(dlPath);
                    if (size > 50 * 1024 * 1024) // > 50 MB
                    {
                        items.Add(CreateItem("Téléchargements Steam Bloqués (downloading)", dlPath, "games", size, 0, "Paquets Steam interrompus ou corrompus. 100% régénérable sans casser vos jeux."));
                    }
                }

                // B. Jeux Steam installés
                var common = Path.Combine(steamApps, "common");
                if (Directory.Exists(common))
                {
                    foreach (var gameDir in Directory.GetDirectories(common))
                    {
                        var di = new DirectoryInfo(gameDir);
                        var cleanName = Regex.Replace(di.Name, @"[^\w\s]", "").Trim();
                        steamGames[cleanName] = gameDir;

                        // Détection Démos Steam et serveurs de test (PTS / Playtest)
                        bool isDemo = di.Name.EndsWith("demo", StringComparison.OrdinalIgnoreCase) || 
                                      di.Name.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
                                      di.Name.Contains("playtest", StringComparison.OrdinalIgnoreCase);
                        bool isPts = di.Name.Contains("pts", StringComparison.OrdinalIgnoreCase) || 
                                     di.Name.Contains("public test", StringComparison.OrdinalIgnoreCase);

                        if (isDemo || isPts)
                        {
                            var steamInfo = GetSteamAppInfo(gameDir);
                            // La date d'activité la plus récente : dernière session jouée ou dernière mise à jour
                            DateTime lastActivity = steamInfo?.LastPlayed 
                                                    ?? steamInfo?.LastUpdated 
                                                    ?? GetLastWriteTimeSafe(di);

                            double daysAgo = (DateTime.Now - lastActivity).TotalDays;

                            // RÈGLE STRICTE AUDIT RECOMMANDÉ :
                            // Tout jeu, démo ou test joué ou actif dans les 120 derniers jours est STRICTEMENT EXCLU de l'Audit Recommandé !
                            // Il ne doit apparaître QUE dans l'Inventaire Complet ("Tous les jeux & logiciels").
                            if (daysAgo >= 120)
                            {
                                double size = GetDirectorySizeSafe(gameDir);
                                string appTitle = steamInfo?.Name ?? di.Name;
                                string label = isPts ? $"Serveur de Test abandonné : {appTitle}" : $"Démo Steam abandonnée : {appTitle}";
                                string reason = isPts 
                                    ? $"Serveur de test public inutilisé depuis {(int)daysAgo} jours ({lastActivity:dd/MM/yyyy})." 
                                    : $"Démo jouable inactive depuis {(int)daysAgo} jours (Dernière partie : {lastActivity:dd/MM/yyyy}).";

                                items.Add(CreateItem(label, gameDir, "games", size, 5, reason));
                            }
                        }
                    }
                }
            }

            // C. Détection automatique des doublons cross-plateformes (Steam vs Epic)
            foreach (var epic in epicGames)
            {
                // Si le même jeu existe sur Steam
                var matchKey = steamGames.Keys.FirstOrDefault(k => k.Equals(epic.Key, StringComparison.OrdinalIgnoreCase) || k.Contains(epic.Key, StringComparison.OrdinalIgnoreCase) || epic.Key.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (matchKey != null)
                {
                    var steamPath = steamGames[matchKey];
                    var epicPath = epic.Value;

                    double sizeEpic = GetDirectorySizeSafe(epicPath);
                    if (sizeEpic > 500 * 1024 * 1024)
                    {
                        items.Add(CreateItem(
                            $"Doublon Cross-Platform : {epic.Key} (Epic Games)",
                            epicPath,
                            "games",
                            sizeEpic,
                            5,
                            $"Jeu installé à la fois sur Epic Games ({epicPath}) et sur Steam ({steamPath})."
                        ));
                    }
                }
            }

            // D. Découverte dynamique de vieux dossiers de jeux portables et launchers sur tous les disques
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var candidateFolders = new[] { "Autres Jeux", "Jeux", "Games", "Old Games", "Riot Games", "GOG Games", "Ubisoft", "Battle.net" };
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
                                bool isOld = (DateTime.Now - last).TotalDays > 180;
                                int risk = isOld ? 5 : 15;
                                items.Add(CreateItem(
                                    $"{di.Name} (Jeu Portable {drive.Name.TrimEnd('\\')})",
                                    sub,
                                    "games",
                                    size,
                                    risk,
                                    $"Jeu portable inactif depuis le {last:dd/MM/yyyy}."
                                ));
                            }
                        }
                    }
                }
            }
        }

        private static void AuditSystemUniversal(List<CleanerItem> items)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // A. Caches GPU Shaders (NVIDIA & AMD)
            var gpuCaches = new[]
            {
                Path.Combine(localAppData, "NVIDIA", "DXCache"),
                Path.Combine(localAppData, "NVIDIA", "GLCache"),
                Path.Combine(localAppData, "D3DSCache"),
                Path.Combine(localAppData, "AMD", "DxCache")
            };
            foreach (var gc in gpuCaches)
            {
                if (Directory.Exists(gc))
                {
                    double size = GetDirectorySizeSafe(gc);
                    if (size > 100 * 1024 * 1024)
                    {
                        items.Add(CreateItem("DirectX & GPU Shader Cache", gc, "system", size, 0, "Caches de rendu 3D compilés régénérables automatiquement au lancement des jeux."));
                    }
                }
            }

            // B. Caches de build développeur (.gradle, .m2, ms-playwright, .nuget)
            var devCaches = new[]
            {
                new { Name = "Gradle Build Cache", Path = Path.Combine(user, ".gradle", "caches") },
                new { Name = "Maven Local Repository (.m2)", Path = Path.Combine(user, ".m2", "repository") },
                new { Name = "Playwright Test Browsers", Path = Path.Combine(localAppData, "ms-playwright") },
                new { Name = "NuGet Package Cache", Path = Path.Combine(localAppData, "NuGet", "v3-cache") }
            };
            foreach (var dc in devCaches)
            {
                if (Directory.Exists(dc.Path))
                {
                    double size = GetDirectorySizeSafe(dc.Path);
                    if (size > 100 * 1024 * 1024)
                    {
                        items.Add(CreateItem(dc.Name, dc.Path, "system", size, 0, "Dépendances et paquets de build téléchargeables automatiquement par les outils de développement."));
                    }
                }
            }

            // C. Scan dynamique d'AppData (>= 150 Mo - Caches, updaters et applications inactives > 180 jours uniquement)
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
                        di.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase)) continue;

                    double size = GetDirectorySizeSafe(dir);
                    if (size > 150 * 1024 * 1024)
                    {
                        bool isCache = di.Name.Contains("cache", StringComparison.OrdinalIgnoreCase) ||
                                       di.Name.Contains("updater", StringComparison.OrdinalIgnoreCase) ||
                                       di.Name.Contains("log", StringComparison.OrdinalIgnoreCase) ||
                                       di.Name.Contains("temp", StringComparison.OrdinalIgnoreCase);

                        var lastWrite = GetLastWriteTimeSafe(di);
                        bool isOld = (DateTime.Now - lastWrite).TotalDays > 180;

                        // Dans l'Audit Recommandé, on n'ajoute que les vrais caches ou les applications abandonnées (> 6 mois)
                        if (isCache || isOld)
                        {
                            int risk = isCache ? 0 : 10;
                            items.Add(CreateItem(
                                isCache ? $"Cache AppData : {di.Name}" : $"Dossier Inactif AppData : {di.Name}",
                                dir,
                                "system",
                                size,
                                risk,
                                isCache ? $"Cache applicatif régénérable ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo)."
                                        : $"Application inactive depuis le {lastWrite:dd/MM/yyyy} ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo)."
                            ));
                        }
                    }
                }
            }

            // D. Scan des dossiers temporaires et caches à la racine sur TOUS les disques fixes
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                // Dossiers temporaires et caches à la racine de chaque disque
                var rootCaches = new[] { "msdownld.tmp", "MSOCache", "MobiSaverCache", "Temp", "Tmp", "TeraBoxDownload", "$Recycle.Bin", ".gradle", ".m2", "node_modules" };
                foreach (var rc in rootCaches)
                {
                    var p = Path.Combine(drive.Name, rc);
                    if (Directory.Exists(p))
                    {
                        double size = GetDirectorySizeSafe(p);
                        if (size > 10 * 1024 * 1024) // > 10 Mo
                        {
                            items.Add(CreateItem($"Cache / Dossier Temporaire : {rc} ({drive.Name.TrimEnd('\\')})", p, "system", size, 0, "Dossier temporaire ou cache d'installation résiduel à la racine du disque."));
                        }
                    }
                }
            }
        }

        private static void AuditDownloadsUniversal(List<CleanerItem> items)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dlList = new List<string> { Path.Combine(user, "Downloads") };

            // Ajouter tout dossier Téléchargements ou Downloads sur les disques secondaires
            foreach (var d in DriveInfo.GetDrives().Where(dr => dr.IsReady && dr.DriveType == DriveType.Fixed))
            {
                var p1 = Path.Combine(d.Name, "Téléchargements");
                if (Directory.Exists(p1)) dlList.Add(p1);
                var p2 = Path.Combine(d.Name, "Downloads");
                if (Directory.Exists(p2) && !dlList.Contains(p2, StringComparer.OrdinalIgnoreCase)) dlList.Add(p2);
            }

            foreach (var dl in dlList)
            {
                if (!Directory.Exists(dl)) continue;

                // 1. Installeurs (.exe, .msi, .AppImage)
                foreach (var f in Directory.GetFiles(dl))
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.ToLowerInvariant();
                    if (ext == ".exe" || ext == ".msi" || ext == ".appimage")
                    {
                        if (fi.Length > 5 * 1024 * 1024)
                        {
                            items.Add(CreateItem(
                                $"Installeur : {fi.Name}",
                                f,
                                "downloads",
                                fi.Length,
                                0,
                                $"Installeur téléchargé le {fi.LastWriteTime:dd/MM/yyyy}. Inutile dès que le logiciel est installé.",
                                isFolder: false
                            ));
                        }
                    }
                    else if ((ext == ".zip" || ext == ".rar" || ext == ".7z") && fi.Length > 20 * 1024 * 1024)
                    {
                        bool isPersonal = fi.Name.Contains("Direct Messages", StringComparison.OrdinalIgnoreCase) || 
                                          fi.Name.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
                                          fi.Name.Contains("sauvegarde", StringComparison.OrdinalIgnoreCase);

                        double daysAgo = (DateTime.Now - fi.LastWriteTime).TotalDays;
                        int risk = isPersonal ? 95 : (daysAgo >= 60 ? 0 : 15);

                        string ageText = daysAgo >= 365 
                            ? $"il y a {(int)(daysAgo / 365)} an(s)" 
                            : $"il y a {(int)daysAgo} jours";

                        items.Add(CreateItem(
                            $"Archive : {fi.Name}",
                            f,
                            "downloads",
                            fi.Length,
                            risk,
                            isPersonal 
                                ? "Sauvegarde / archive personnelle. Recommandé de conserver !" 
                                : $"Archive téléchargée {ageText} ({fi.LastWriteTime:dd/MM/yyyy}). Suppression sûre.",
                            isFolder: false,
                            isSanctuarized: isPersonal
                        ));
                    }
                }

                // 2. Dossiers de builds temporaires dans Downloads
                foreach (var sub in Directory.GetDirectories(dl))
                {
                    var di = new DirectoryInfo(sub);
                    if (di.Name.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Contains("r2modman", StringComparison.OrdinalIgnoreCase))
                    {
                        double size = GetDirectorySizeSafe(sub);
                        if (size > 50 * 1024 * 1024)
                        {
                            items.Add(CreateItem($"Dossier Temporaire : {di.Name}", sub, "downloads", size, 5, "Dossier de build temporaire ou décompressé dans Téléchargements."));
                        }
                    }
                }
            }
        }

        private static void AuditEmulatorsUniversal(List<CleanerItem> items)
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            var emuFolderNames = new[] { "Nox", "BlueStacks", "Yuzu", "Jeux dolphin", "Dolphin-x64", "Ryujinx", "LDPlayer", "MEmu" };

            foreach (var d in drives)
            {
                foreach (var baseFolder in new[] { @"Program Files", @"Program Files (x86)", "" })
                {
                    var root = string.IsNullOrEmpty(baseFolder) ? d.Name : Path.Combine(d.Name, baseFolder);
                    if (!Directory.Exists(root)) continue;

                    foreach (var ef in emuFolderNames)
                    {
                        var target = Path.Combine(root, ef);
                        if (Directory.Exists(target))
                        {
                            double size = GetDirectorySizeSafe(target);
                            if (size > 10 * 1024 * 1024)
                            {
                                items.Add(CreateItem($"Émulateur : {ef} ({d.Name.TrimEnd('\\')})", target, "apps", size, 5, "Émulateur ou ROMs installés sur cette machine."));
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region RECHERCHE FACTUELLE & SUPPRESSION SÉCURISÉE

        public static ResearchReport ResearchItem(string name, string path, int initialRisk = 5)
        {
            var report = new ResearchReport 
            { 
                ItemName = name, 
                ItemPath = path,
                RiskPct = initialRisk
            };

            bool exists = Directory.Exists(path) || File.Exists(path);
            if (!exists)
            {
                report.Definition = "L'élément est déjà supprimé ou n'existe plus sur le disque.";
                report.Origin = "Fichier ou dossier supprimé.";
                report.UsageActivity = "Inactif";
                report.SafetyImpact = "Aucun impact sur le système.";
                report.RiskPct = 0;
                return report;
            }

            DateTime lastWrite = File.Exists(path) ? new FileInfo(path).LastWriteTime : GetLastWriteTimeSafe(new DirectoryInfo(path));
            report.LastAccessDate = lastWrite.ToString("dd MMMM yyyy");

            var baseName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
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

                if (steamInfo?.LastPlayed != null)
                {
                    int daysAgo = (int)(DateTime.Now - steamInfo.LastPlayed.Value).TotalDays;
                    report.UsageActivity = daysAgo == 0
                        ? "Joué aujourd'hui !"
                        : $"Dernière partie jouée il y a {daysAgo} jour{(daysAgo > 1 ? "s" : "")} (le {steamInfo.LastPlayed.Value:dd MMMM yyyy}).";
                }
                else if (steamInfo?.LastUpdated != null)
                {
                    report.UsageActivity = $"Téléchargé ou mis à jour le {steamInfo.LastUpdated.Value:dd MMMM yyyy} (aucune session enregistrée).";
                }

                report.SafetyImpact = isDemo
                    ? "C'est une démo temporaire. Si vous avez terminé d'essayer le jeu ou si vous ne comptez plus y rejouer, vous pouvez la supprimer sans risque (vos succès Steam sont préservés). Si vous souhaitez continuer à y jouer, conservez-la ou protégez-la."
                    : "⚠️ Jeu complet. Si vous le supprimez, vous libérerez de l'espace mais devrez le re-télécharger depuis Steam pour y rejouer. Vos sauvegardes cloud sont sauvegardées sur Steam Cloud.";

                report.RiskPct = isDemo ? 5 : 20;
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
            else if (pLower.EndsWith(".exe") || pLower.EndsWith(".msi") || pLower.EndsWith(".appimage"))
            {
                var vi = File.Exists(path) && pLower.EndsWith(".exe") ? FileVersionInfo.GetVersionInfo(path) : null;
                string prod = !string.IsNullOrWhiteSpace(vi?.ProductName) ? vi.ProductName : Path.GetFileNameWithoutExtension(path);
                string comp = !string.IsNullOrWhiteSpace(vi?.CompanyName) ? vi.CompanyName : "Éditeur internet";
                string desc = !string.IsNullOrWhiteSpace(vi?.FileDescription) ? vi.FileDescription : "Programme d'installation";

                int ageDays = (int)(DateTime.Now - lastWrite).TotalDays;
                string ageStr = ageDays > 365 ? $"il y a {(int)(ageDays / 365)} an(s)" : $"il y a {ageDays} jours";

                report.Definition = $"Fichier d'installation autonome de : {prod} ({desc}).";
                report.Origin = $"Téléchargé depuis internet ({comp}) le {lastWrite:dd MMMM yyyy} ({ageStr}) dans {Path.GetFileName(Path.GetDirectoryName(path))}.";
                report.SafetyImpact = "🟢 SUPPRESSION 100% SÛRE (Risque 0%). Ce n'est que l'installeur temporaire. Une fois le logiciel installé sur votre PC, ce fichier exécutable ne sert plus à rien et peut être supprimé sans affecter l'application installée.";
                report.RiskPct = 0;
            }
            // 19. Jeux portables anciens
            else if (n.Contains("jeu portable") || pLower.Contains("autres jeux") || pLower.Contains(@"\jeux\"))
            {
                report.Definition = "Dossier de jeu vidéo portable (sans installeur Windows officiel).";
                report.Origin = "Dossier de jeu extrait ou téléchargé directement sur vos disques secondaires.";
                report.SafetyImpact = "Suppression sécurisée si vous n'y jouez plus. Conservez vos sauvegardes si le jeu les stocke dans son propre répertoire.";
                report.RiskPct = 10;
            }
            // 20. Recherche dynamique dans le Registre Windows
            else
            {
                var regDetails = FindRegistryAppDetails(baseName, name);
                if (regDetails != null)
                {
                    report.Definition = $"Application Windows : {regDetails.DisplayName} (v{regDetails.DisplayVersion}), développée par {regDetails.Publisher}.";
                    report.Origin = $"Installée officiellement sur votre PC{(string.IsNullOrEmpty(regDetails.InstallDate) ? "" : " le " + regDetails.InstallDate)} (enregistrée dans le Registre Windows).";
                    report.SafetyImpact = running
                        ? "⚠️ L'application est actuellement ouverte en mémoire. Fermez-la avant toute désinstallation."
                        : "Application enregistrée. Peut être désinstallée proprement si vous ne l'utilisez plus.";
                    report.RiskPct = initialRisk;
                }
                else
                {
                    var exeDetails = FindExeMetadata(path);
                    if (exeDetails != null)
                    {
                        report.Definition = $"{exeDetails.Description} ({exeDetails.ProductName})";
                        report.Origin = $"Développé par {exeDetails.Company}. Emplacement : {path}.";
                        report.SafetyImpact = running
                            ? "⚠️ Un processus lié tourne actuellement en mémoire. Fermez-le avant de nettoyer."
                            : "Aucun processus actif. Peut être nettoyé si vous n'utilisez plus ce dossier.";
                        report.RiskPct = initialRisk;
                    }
                    else
                    {
                        report.Definition = $"Dossier de données ou cache d'application : {name}.";
                        report.Origin = $"Généré par une application ou un outil dans {Path.GetDirectoryName(path)}.";
                        report.SafetyImpact = running
                            ? "⚠️ Processus actif en mémoire. Fermez l'application liée avant de nettoyer."
                            : "Inactif. Peut être supprimé si l'application associée n'est plus utilisée.";
                        report.RiskPct = initialRisk;
                    }
                }
            }

            return report;
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
                    var exes = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);
                    if (exes.Length > 0)
                    {
                        var vi = FileVersionInfo.GetVersionInfo(exes[0]);
                        return new ExeMetadata
                        {
                            ProductName = string.IsNullOrWhiteSpace(vi.ProductName) ? Path.GetFileNameWithoutExtension(exes[0]) : vi.ProductName,
                            Description = string.IsNullOrWhiteSpace(vi.FileDescription) ? "Fichiers exécutables de l'application" : vi.FileDescription,
                            Company = string.IsNullOrWhiteSpace(vi.CompanyName) ? "Éditeur logiciel" : vi.CompanyName
                        };
                    }
                }
            }
            catch { }
            return null;
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

        private static CleanerItem CreateItem(string name, string path, string category, double bytes, int riskPct, string desc, bool isFolder = true, bool isSanctuarized = false)
        {
            var drive = "";
            try { drive = Path.GetPathRoot(path)?.TrimEnd('\\') ?? ""; } catch { }

            string level = riskPct switch
            {
                <= 10 => "Quasi Nul",
                <= 40 => "Faible",
                <= 70 => "Modéré",
                _ => "Élevé"
            };

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
                IsFolder = isFolder,
                IsSanctuarized = isSanctuarized
            };
        }

        private static double GetDirectorySizeSafe(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime GetLastWriteTimeSafe(DirectoryInfo di)
        {
            try
            {
                var latest = di.EnumerateFiles("*", SearchOption.AllDirectories)
                               .OrderByDescending(f => f.LastWriteTime)
                               .FirstOrDefault();
                return latest?.LastWriteTime ?? di.LastWriteTime;
            }
            catch
            {
                return di.LastWriteTime;
            }
        }
        #endregion
    }
}
