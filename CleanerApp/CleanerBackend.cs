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
                                var last = GetLastWriteTimeSafe(di);
                                var drive = Path.GetPathRoot(gameDir)?.TrimEnd('\\') ?? "C:";
                                items.Add(CreateItem(
                                    di.Name,
                                    gameDir,
                                    "games",
                                    size,
                                    5,
                                    $"Jeu Steam complet sur {drive} (Dernière activité : {last:dd/MM/yyyy})"
                                ));
                                seenNames.Add(di.Name);
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

                        // Détection PTS / Test servers
                        if (di.Name.Contains("pts", StringComparison.OrdinalIgnoreCase) || di.Name.Contains("public test", StringComparison.OrdinalIgnoreCase))
                        {
                            double size = GetDirectorySizeSafe(gameDir);
                            items.Add(CreateItem($"Serveur de Test : {di.Name}", gameDir, "games", size, 5, "Serveur de test public souvent abandonné une fois la mise à jour sortie."));
                        }

                        // Détection Démos Steam
                        if (di.Name.EndsWith("demo", StringComparison.OrdinalIgnoreCase))
                        {
                            double size = GetDirectorySizeSafe(gameDir);
                            items.Add(CreateItem($"Démo Steam : {di.Name}", gameDir, "games", size, 0, "Démo jouable temporaire de festival Steam."));
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
                        di.Name.Equals("Programs", StringComparison.OrdinalIgnoreCase)) continue;

                    double size = GetDirectorySizeSafe(dir);
                    if (size > 150 * 1024 * 1024)
                    {
                        int risk = 10;
                        if (di.Name.Contains("cache", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Contains("updater", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Contains("log", StringComparison.OrdinalIgnoreCase) ||
                            di.Name.Contains("temp", StringComparison.OrdinalIgnoreCase))
                        {
                            risk = 0;
                        }

                        items.Add(CreateItem(
                            $"Cache AppData : {di.Name}",
                            dir,
                            "system",
                            size,
                            risk,
                            $"Dossier d'application volumineux dans AppData ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo)."
                        ));
                    }
                }
            }

            // D. Scan des dossiers temporaires, caches racine et Program Files sur TOUS les disques fixes
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                // 1. Dossiers temporaires et caches à la racine de chaque disque
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

                // 2. Logiciels installés dans Program Files / Program Files (x86) sur chaque disque secondaire
                foreach (var pfName in new[] { "Program Files", "Program Files (x86)" })
                {
                    var pfPath = Path.Combine(drive.Name, pfName);
                    if (Directory.Exists(pfPath))
                    {
                        foreach (var sub in Directory.GetDirectories(pfPath))
                        {
                            var di = new DirectoryInfo(sub);
                            if (di.Name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("Common Files", StringComparison.OrdinalIgnoreCase) ||
                                di.Name.Equals("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase)) continue;

                            double size = GetDirectorySizeSafe(sub);
                            if (size > 100 * 1024 * 1024) // > 100 Mo
                            {
                                items.Add(CreateItem(
                                    $"Logiciel résiduel : {di.Name} ({drive.Name.TrimEnd('\\')})",
                                    sub,
                                    "apps",
                                    size,
                                    5,
                                    $"Application installée dans {pfPath}."
                                ));
                            }
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
                    else if ((ext == ".zip" || ext == ".rar" || ext == ".7z") && fi.Length > 50 * 1024 * 1024)
                    {
                        bool isPersonal = fi.Name.Contains("Direct Messages", StringComparison.OrdinalIgnoreCase) || fi.Name.Contains("backup", StringComparison.OrdinalIgnoreCase);
                        int risk = isPersonal ? 95 : 5;
                        items.Add(CreateItem(
                            $"Archive : {fi.Name}",
                            f,
                            "downloads",
                            fi.Length,
                            risk,
                            isPersonal ? "Sauvegarde / archive personnelle. Recommandé de conserver !" : "Archive volumineuse téléchargée.",
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

        public static ResearchReport ResearchItem(string name, string path)
        {
            var report = new ResearchReport { ItemName = name, ItemPath = path };

            bool exists = Directory.Exists(path) || File.Exists(path);
            if (!exists)
            {
                report.Definition = "L'élément est déjà supprimé ou n'existe plus.";
                report.UsageActivity = "Inactif";
                report.SafetyImpact = "Aucun impact.";
                report.RiskPct = 0;
                return report;
            }

            DateTime lastWrite = File.Exists(path) ? new FileInfo(path).LastWriteTime : GetLastWriteTimeSafe(new DirectoryInfo(path));
            report.LastAccessDate = lastWrite.ToString("dd MMMM yyyy");

            var baseName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var running = Process.GetProcesses().Any(p =>
                p.ProcessName.Contains(baseName, StringComparison.OrdinalIgnoreCase) ||
                (name.Contains("Steam", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase))
            );
            report.IsRunning = running;

            if (path.Contains("downloading", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = "Caches temporaires de téléchargements Steam incomplets ou interrompus.";
                report.Origin = "Généré automatiquement par le client Steam lors des mises à jour.";
                report.SafetyImpact = "Suppression 100% sécurisée. Aucun impact sur les jeux déjà installés.";
                report.RiskPct = 0;
            }
            else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = "Fichier d'installation autonome d'un logiciel.";
                report.Origin = "Téléchargé depuis internet pour installer une application.";
                report.SafetyImpact = "100% sécurisé à supprimer dès lors que le logiciel est déjà installé.";
                report.RiskPct = 0;
            }
            else
            {
                report.Definition = $"Fichiers ou dossier situés dans {path}.";
                report.Origin = "Créé par une application ou un jeu sur ce PC.";
                report.SafetyImpact = running
                    ? "⚠️ Attention : Un processus lié tourne en mémoire actuellement. Fermez-le avant de nettoyer."
                    : "Aucun processus actif. Suppression silencieuse sans risque pour le système.";
                report.RiskPct = running ? 20 : 5;
            }

            report.UsageActivity = running
                ? "Processus ACTIF en mémoire en ce moment !"
                : $"Inactif depuis le {report.LastAccessDate}.";

            return report;
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
