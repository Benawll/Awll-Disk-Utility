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
                                string appTitle = steamInfo?.Name ?? di.Name;
                                bool isRunning = IsLikelyRunning(appTitle, gameDir, runningProcs);
                                if (!isRunning)
                                {
                                    double size = GetDirectorySizeSafe(gameDir);
                                    string label = isPts ? $"Serveur de Test abandonné : {appTitle}" : $"Démo Steam abandonnée : {appTitle}";
                                    string reason = isPts
                                        ? $"Serveur de test public inutilisé depuis {FormatAgeText(daysAgo)} ({lastActivity:dd/MM/yyyy})."
                                        : $"Démo jouable inactive depuis {FormatAgeText(daysAgo)} (Dernière partie : {lastActivity:dd/MM/yyyy}).";

                                    int risk = ComputeRiskScore("demo_pts", daysAgo, false, false, false);
                                    items.Add(CreateItem(label, gameDir, "games", size, risk, reason, lastModified: lastActivity));
                                }
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
                        var diEpic = new DirectoryInfo(epicPath);
                        var lastWrite = GetLastWriteTimeSafe(diEpic);
                        bool hasLaunch = ExecutionHistoryService.TryGetFolderLastExecution(epicPath, out var epicLaunch);
                        var displayDate = hasLaunch ? epicLaunch : lastWrite;
                        string launchNote = hasLaunch
                            ? $"Dernier lancement détecté le {epicLaunch:dd/MM/yyyy}."
                            : $"Dernière activité fichiers : {lastWrite:dd/MM/yyyy} (aucun lancement récent enregistré dans BAM/UserAssist).";
                        items.Add(CreateItem(
                            $"Doublon Cross-Platform : {epic.Key} (Epic Games)",
                            epicPath,
                            "games",
                            sizeEpic,
                            5,
                            $"Jeu installé à la fois sur Epic Games ({epicPath}) et sur Steam ({steamPath}). {launchNote}",
                            lastModified: displayDate
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
                                    if (!isRunning)
                                    {
                                        int risk = ComputeRiskScore("portable_game", daysAgo, false, false, false);
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
        }

        private static void AuditSystemUniversal(List<CleanerItem> items, List<CleanerItem> extraItems, HashSet<string>? targetDrives, HashSet<string> runningProcs)
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

                            if (isCache)
                            {
                                int risk = ComputeRiskScore("appdata_cache", daysAgo, isRunning, false, false);
                                items.Add(CreateItem(
                                    $"Cache AppData : {di.Name}",
                                    dir,
                                    "system",
                                    size,
                                    risk,
                                    $"Cache / updater applicatif régénérable ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo).",
                                    lastModified: lastWrite
                                ));
                            }
                            else if (isOld && !isRunning)
                            {
                                bool isCore = IsCoreSystemOrRuntime(di.Name, dir);
                                int risk = ComputeRiskScore("appdata_inactive", daysAgo, false, isCore, false);
                                var appItem = CreateItem(
                                    isCore ? $"Composant Actif AppData : {di.Name}" : $"Dossier Inactif AppData : {di.Name}",
                                    dir,
                                    "system",
                                    size,
                                    risk,
                                    isCore
                                        ? $"Données d'outil essentiel ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo)."
                                        : $"Données d'application inactives depuis {FormatAgeText(daysAgo)} ({lastWrite:dd/MM/yyyy}).",
                                    lastModified: lastWrite,
                                    isExtra: isCore
                                );
                                if (isCore) extraItems.Add(appItem);
                                else items.Add(appItem);
                            }
                            else if (isRunning)
                            {
                                int risk = ComputeRiskScore("appdata_inactive", daysAgo, true, false, false);
                                extraItems.Add(CreateItem(
                                    $"Dossier AppData Actif (en cours) : {di.Name}",
                                    dir,
                                    "system",
                                    size,
                                    risk,
                                    $"Processus associé actif en mémoire ({Math.Round(size / 1024.0 / 1024.0, 1)} Mo).",
                                    lastModified: lastWrite,
                                    isExtra: true
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

        private static void AuditDownloadsUniversal(List<CleanerItem> items, List<CleanerItem> extraItems, HashSet<string>? targetDrives, HashSet<string> runningProcs)
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
                            bool isRunning = IsLikelyRunning(fi.Name, f, runningProcs);
                            int risk = ComputeRiskScore("installer", daysAgo, isRunning, false, false);
                            var item = CreateItem(
                                isRunning ? $"Exécutable actif : {fi.Name}" : $"Installeur : {fi.Name}",
                                f,
                                "downloads",
                                fi.Length,
                                risk,
                                isRunning ? "Programme actuellement en cours d'exécution depuis Téléchargements." : $"Installeur téléchargé {FormatAgeText(daysAgo)} ({effDate:dd/MM/yyyy}). Inutile dès que le logiciel est installé.",
                                isFolder: false,
                                lastModified: effDate,
                                isExtra: isRunning
                            );
                            if (isRunning) extraItems.Add(item);
                            else items.Add(item);
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

                    bool isRunning = IsLikelyRunning(di.Name, sub, runningProcs);
                    if (isRunning || isTempName || hasMatchingArchive || daysAgo >= 60)
                    {
                        int risk;
                        if ((isTempName || hasMatchingArchive) && !hasMediaOrDocs && !isRunning)
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
                        if (isRunning) risk = Math.Max(risk + 25, 55);

                        string reason = isRunning
                            ? "Dossier associé à un processus actuellement actif en mémoire."
                            : (hasMatchingArchive
                                ? $"Dossier déjà extrait à côté de son archive ({di.Name}.zip/.rar/.7z){(hasMediaOrDocs ? " — contient des médias/documents" : "")}."
                                : (hasMediaOrDocs
                                    ? $"Contient des images, vidéos ou documents (inactif depuis {FormatAgeText(daysAgo)}, {last:dd/MM/yyyy})."
                                    : (isTempName
                                        ? "Dossier de build ou temporaire dans Téléchargements."
                                        : $"Dossier téléchargé inactif depuis {FormatAgeText(daysAgo)} ({last:dd/MM/yyyy}).")));
                        var dlItem = CreateItem(
                            isRunning ? $"Dossier Actif (Téléchargements) : {di.Name}" : $"Dossier Téléchargé : {di.Name}",
                            sub,
                            "downloads",
                            size,
                            risk,
                            reason,
                            lastModified: last,
                            isExtra: isRunning
                        );
                        if (isRunning) extraItems.Add(dlItem);
                        else items.Add(dlItem);
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
                        bool isPureCacheName = nLower.Contains("cache") || nLower.Contains("temp") || nLower.Contains("tmp");
                        int risk = ComputeRiskScore(daysAgo >= 90 ? "appdata_cache" : "appdata_inactive", daysAgo, isRunning, false, false);
                        bool promote = !isRunning || isPureCacheName;
                        var cItem = CreateItem(
                            isRunning && !isPureCacheName ? $"Dossier Racine Actif (en cours) : {di.Name} ({d.Name.TrimEnd('\\')})" : $"Dossier Cache / Téléchargement : {di.Name} ({d.Name.TrimEnd('\\')})",
                            dir,
                            "system",
                            bytes,
                            risk,
                            $"Dossier racine ({fCount} fichiers, {FormatAgeText(daysAgo)}).",
                            lastModified: last,
                            isExtra: !promote
                        );
                        if (promote) recommendedItems.Add(cItem);
                        else extraItems.Add(cItem);
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
                        // Dossier personnel / sauvegardes à la racine du disque (ex: New Doc, Backups...) -> exploration en profondeur dans Dossiers Personnels
                        int subAdded = ScanPersonalDirectoryRecursive(dir, $"{di.Name} ({d.Name.TrimEnd('\\')})", "docs", 0, extraItems, seenPaths, "Sauvegardes & Dossiers Racines");
                        if (subAdded == 0 && seenPaths.Add(dir))
                        {
                            var item = CreateItem(
                                $"Dossier Perso ({di.Name}) : {di.Name} ({d.Name.TrimEnd('\\')})",
                                dir,
                                "system",
                                bytes,
                                daysAgo >= 365 ? 35 : 55,
                                $"Dossier personnalisé à la racine de {d.Name.TrimEnd('\\')} ({fCount} fichiers, {FormatAgeText(daysAgo)}).",
                                lastModified: last,
                                isExtra: true
                            );
                            item.IsPersonal = true;
                            item.PersonalGroup = "Sauvegardes & Dossiers Racines";
                            extraItems.Add(item);
                        }
                    }
                    seenPaths.Add(dir);
                }
            }
        }

        private static void AuditPersonalFolders(List<CleanerItem> extraItems, HashSet<string> seenPaths, HashSet<string>? targetDrives)
        {
            var personalRoots = new (string Path, string Label, string Kind)[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", "docs"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vidéos", "video"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Images", "image"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Musique", "music"),
                (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Bureau", "desktop")
            };

            foreach (var (root, label, kind) in personalRoots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                if (!IsDriveSelected(root, targetDrives)) continue;
                ScanPersonalDirectoryRecursive(root, label, kind, 0, extraItems, seenPaths, label);
            }

            // Dossiers personnalisés supplémentaires directement dans C:\Users\<user> (ex: OneDrive, Saved Games, dossiers persos...)
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile) && IsDriveSelected(userProfile, targetDrives))
            {
                var knownProfileSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "AppData", "Downloads", "Documents", "Videos", "Pictures", "Music", "Desktop",
                    "Searches", "Links", "Favorites", "Contacts", "3D Objects", "IntelGraphicsProfiles",
                    "Application Data", "Local Settings", "Cookies", "NetHood", "PrintHood", "Recent", "SendTo", "Start Menu", "Templates"
                };
                foreach (var r in personalRoots)
                {
                    if (!string.IsNullOrWhiteSpace(r.Path))
                        knownProfileSkip.Add(Path.GetFileName(r.Path.TrimEnd('\\')));
                }

                try
                {
                    foreach (var sub in Directory.GetDirectories(userProfile))
                    {
                        var di = new DirectoryInfo(sub);
                        if (di.Name.StartsWith(".") || knownProfileSkip.Contains(di.Name)) continue;
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if (seenPaths.Contains(sub)) continue;
                        double bytes = GetDirectorySizeSafe(sub);
                        if (bytes < 150L * 1024 * 1024) continue;
                        int subAdded = ScanPersonalDirectoryRecursive(sub, di.Name, "docs", 0, extraItems, seenPaths, "Profil Utilisateur");
                        if (subAdded == 0 && seenPaths.Add(sub))
                        {
                            var last = GetLastWriteTimeSafe(di);
                            int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                            var item = CreateItem(
                                $"Dossier Profil : {di.Name}",
                                sub,
                                "system",
                                bytes,
                                daysAgo >= 365 ? 35 : 55,
                                $"Dossier dans votre profil utilisateur ({FormatAgeText(daysAgo)}, {last:dd/MM/yyyy}).",
                                lastModified: last,
                                isExtra: true
                            );
                            item.IsPersonal = true;
                            item.PersonalGroup = "Profil Utilisateur";
                            extraItems.Add(item);
                        }
                    }
                }
                catch { }
            }
        }

        private static int ScanPersonalDirectoryRecursive(
            string dir,
            string label,
            string kind,
            int depth,
            List<CleanerItem> extraItems,
            HashSet<string> seenPaths,
            string personalGroup = "")
        {
            const long minDirBytes = 150L * 1024 * 1024;
            const long minFileBytes = 100L * 1024 * 1024;
            const long minMismatchFileBytes = 50L * 1024 * 1024;
            var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" };
            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic", ".raw", ".psd" };
            var musicExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma" };

            int addedCount = 0;
            try
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        bool matchesFolderMedia =
                            (kind == "video" && videoExts.Contains(fi.Extension)) ||
                            (kind == "image" && imageExts.Contains(fi.Extension)) ||
                            (kind == "music" && musicExts.Contains(fi.Extension));
                        bool isMediaMismatch = (kind == "video" || kind == "image" || kind == "music") && !matchesFolderMedia;
                        long threshold = isMediaMismatch ? minMismatchFileBytes : minFileBytes;
                        if (fi.Length < threshold) continue;
                        if (!seenPaths.Add(file)) continue;

                        var effDate = GetFileEffectiveActivityDate(fi);
                        int daysAgo = Math.Max(0, (int)(DateTime.Now - effDate).TotalDays);
                        int risk = matchesFolderMedia ? 75 : (daysAgo >= 365 ? 30 : 50);
                        string desc = isMediaMismatch
                            ? $"Fichier d'un autre type dans {label} ({FormatAgeText(daysAgo)}, {effDate:dd/MM/yyyy})."
                            : $"Fichier dans {label} ({FormatAgeText(daysAgo)}, {effDate:dd/MM/yyyy}).";

                        var item = CreateItem(
                            $"Fichier Perso ({label}) : {fi.Name}",
                            file,
                            "system",
                            fi.Length,
                            risk,
                            desc,
                            lastModified: effDate,
                            isFolder: false,
                            isExtra: true
                        );
                        item.IsPersonal = true;
                        item.PersonalGroup = string.IsNullOrEmpty(personalGroup) ? label : personalGroup;
                        extraItems.Add(item);
                        addedCount++;
                    }
                    catch { }
                }

                if (depth < 5)
                {
                    foreach (var sub in Directory.GetDirectories(dir))
                    {
                        try
                        {
                            var di = new DirectoryInfo(sub);
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            double subBytes = GetDirectorySizeSafe(sub);
                            if (subBytes < minDirBytes) continue;

                            int subAdded = ScanPersonalDirectoryRecursive(sub, label, kind, depth + 1, extraItems, seenPaths, personalGroup);
                            if (subAdded == 0 && seenPaths.Add(sub))
                            {
                                var last = GetLastWriteTimeSafe(di);
                                int daysAgo = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                                int risk = (kind == "video" || kind == "image" || kind == "music") ? 65 : (daysAgo >= 365 ? 35 : 55);

                                var item = CreateItem(
                                    $"Dossier Perso ({label}) : {di.Name}",
                                    sub,
                                    "system",
                                    subBytes,
                                    risk,
                                    $"Dossier dans {label} ({FormatAgeText(daysAgo)}, {last:dd/MM/yyyy}). Vérifiez avant suppression.",
                                    lastModified: last,
                                    isExtra: true
                                );
                                item.IsPersonal = true;
                                item.PersonalGroup = string.IsNullOrEmpty(personalGroup) ? label : personalGroup;
                                extraItems.Add(item);
                                addedCount++;
                            }
                            else
                            {
                                addedCount += subAdded;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return addedCount;
        }

        #endregion

    }
}
