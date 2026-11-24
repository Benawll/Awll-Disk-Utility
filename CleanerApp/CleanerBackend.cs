using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public string Drive { get; set; } = ""; // C:, D:, E:
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public int RiskPct { get; set; } // 0 to 100
        public string RiskLevel { get; set; } = "Quasi Nul"; // Quasi Nul, Faible, Modéré, Élevé
        public string Description { get; set; } = "";
        public string LastModified { get; set; } = "";
        public bool IsFolder { get; set; }
        public bool IsSanctuarized { get; set; }
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
        private static readonly HashSet<string> SanctuarizedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\New Doc",
            @"C:\Users\benja\Videos",
            @"C:\ProgramData\NVIDIA\NGX"
        };

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

                // 1. Audit Jeux & Bibliothèques (Steam downloading, doublons, demos)
                progressCallback?.Invoke("Audit des caches Steam et doublons de jeux...", 20);
                AuditGamesLibraries(items);

                // 2. Audit Système & Caches Développeurs
                progressCallback?.Invoke("Audit des caches système, GPU et développeur...", 40);
                AuditSystemAndDev(items);

                // 3. Audit Téléchargements
                progressCallback?.Invoke("Audit du dossier Téléchargements...", 65);
                AuditDownloads(items);

                // 4. Audit Logiciels et Émulateurs
                progressCallback?.Invoke("Audit des émulateurs et applications...", 85);
                AuditAppsAndEmulators(items);

                progressCallback?.Invoke("Finalisation de l'audit...", 100);
                return items.OrderBy(x => x.RiskPct).ThenByDescending(x => x.SizeGB).ToList();
            });
        }

        private static void AuditGamesLibraries(List<CleanerItem> items)
        {
            // A. Steam downloading sur tous les disques
            var steamAppsPaths = new[] { @"D:\SteamLibrary\steamapps", @"E:\SteamLibrary\steamapps", @"C:\Program Files (x86)\Steam\steamapps" };
            foreach (var sa in steamAppsPaths)
            {
                var dlPath = Path.Combine(sa, "downloading");
                if (Directory.Exists(dlPath))
                {
                    double size = GetDirectorySizeSafe(dlPath);
                    if (size > 50 * 1024 * 1024) // > 50 MB
                    {
                        items.Add(CreateItem(
                            "Téléchargements Steam Bloqués",
                            dlPath,
                            "games",
                            size,
                            0,
                            "Paquets Steam corrompus ou abandonnés dans downloading. 100% régénérable sans casser les jeux installés."
                        ));
                    }
                }
            }

            // B. Doublon Rocket League (Epic sur D: vs Steam sur E:)
            var rlEpic = @"D:\Epic games jeux\rocketleague";
            var rlSteam = @"E:\SteamLibrary\steamapps\common\rocketleague";
            if (Directory.Exists(rlEpic) && Directory.Exists(rlSteam))
            {
                double size = GetDirectorySizeSafe(rlEpic);
                if (size > 1024 * 1024 * 1024)
                {
                    items.Add(CreateItem(
                        "Rocket League (Doublon Epic Games sur D:)",
                        rlEpic,
                        "games",
                        size,
                        5,
                        "Doublon avec votre version officielle Steam sur E: (jouée récemment). Aucun impact sur la progression en ligne."
                    ));
                }
            }

            // C. Paladins PTS
            var pts = @"D:\SteamLibrary\steamapps\common\paladins pts";
            if (Directory.Exists(pts))
            {
                double size = GetDirectorySizeSafe(pts);
                items.Add(CreateItem(
                    "Paladins PTS (Public Test Server sur D:)",
                    pts,
                    "games",
                    size,
                    5,
                    "Serveur de test public de Paladins inactif depuis janvier 2025. Le jeu normal existe indépendamment."
                ));
            }

            // D. Vieux jeux portables inactifs dans D:\Autres Jeux
            var autresJeux = @"D:\Autres Jeux";
            if (Directory.Exists(autresJeux))
            {
                foreach (var dir in Directory.GetDirectories(autresJeux))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    double size = GetDirectorySizeSafe(dir);
                    if (size > 500 * 1024 * 1024) // > 500 MB
                    {
                        var lastWrite = GetLastWriteTimeSafe(dirInfo);
                        bool isOld = (DateTime.Now - lastWrite).TotalDays > 365;
                        int risk = isOld ? 5 : 15;
                        items.Add(CreateItem(
                            $"{dirInfo.Name} (Jeu Portable D:)",
                            dir,
                            "games",
                            size,
                            risk,
                            $"Dossier de jeu portable inactif depuis le {lastWrite:dd/MM/yyyy}."
                        ));
                    }
                }
            }

            // E. Monster Hunter World ou autres gros jeux Steam inactifs (> 1 an)
            var mh = @"E:\SteamLibrary\steamapps\common\Monster Hunter World";
            if (Directory.Exists(mh))
            {
                double size = GetDirectorySizeSafe(mh);
                items.Add(CreateItem(
                    "Monster Hunter: World (E:)",
                    mh,
                    "games",
                    size,
                    10,
                    "Jeu Steam complet inactif depuis août 2025. Réinstallable à tout moment via Steam."
                ));
            }

            // F. Hades & Outer Wilds
            var hades = @"E:\SteamLibrary\steamapps\common\Hades";
            if (Directory.Exists(hades))
            {
                double size = GetDirectorySizeSafe(hades);
                items.Add(CreateItem("Hades (Steam sur E:)", hades, "games", size, 5, "Inactif depuis 2024. Réinstallable via Steam."));
            }
            var ow = @"E:\SteamLibrary\steamapps\common\Outer Wilds";
            if (Directory.Exists(ow))
            {
                double size = GetDirectorySizeSafe(ow);
                items.Add(CreateItem("Outer Wilds (Steam sur E:)", ow, "games", size, 5, "Inactif depuis 2025. Réinstallable via Steam."));
            }
        }

        private static void AuditSystemAndDev(List<CleanerItem> items)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // A. DirectX Shader Cache (DXCache)
            var dxCache = Path.Combine(localAppData, "D3DSCache");
            var nvidiaDx = Path.Combine(localAppData, "NVIDIA", "DXCache");
            if (Directory.Exists(nvidiaDx))
            {
                double size = GetDirectorySizeSafe(nvidiaDx);
                if (size > 100 * 1024 * 1024)
                {
                    items.Add(CreateItem("NVIDIA DirectX Shader Cache (DXCache)", nvidiaDx, "system", size, 0, "Caches de shaders compilés régénérables automatiquement au lancement des jeux."));
                }
            }

            // B. ASUS Armoury Crate logs
            var asusLog = @"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis";
            if (Directory.Exists(asusLog))
            {
                double size = GetDirectorySizeSafe(asusLog);
                if (size > 200 * 1024 * 1024)
                {
                    items.Add(CreateItem("ASUS Armoury Crate (Logs de Diagnostic)", asusLog, "system", size, 0, "Accumulation de logs textuels (.log) d'Armoury Crate. Purgeable sans impact."));
                }
            }

            // C. Caches de build développeur (.gradle, ms-playwright, etc.)
            var gradle = Path.Combine(user, ".gradle", "caches");
            if (Directory.Exists(gradle))
            {
                double size = GetDirectorySizeSafe(gradle);
                if (size > 100 * 1024 * 1024)
                {
                    items.Add(CreateItem("Gradle Build Cache (.gradle)", gradle, "system", size, 0, "Dépendances et artefacts de build Java/Android régénérables."));
                }
            }

            var playwright = Path.Combine(localAppData, "ms-playwright");
            if (Directory.Exists(playwright))
            {
                double size = GetDirectorySizeSafe(playwright);
                items.Add(CreateItem("Playwright Test Browsers", playwright, "system", size, 0, "Binaires de navigateurs de test automatisés Playwright."));
            }

            // D. Overwolf & Slack résiduels
            var overwolf = Path.Combine(localAppData, "Overwolf");
            if (Directory.Exists(overwolf))
            {
                double size = GetDirectorySizeSafe(overwolf);
                items.Add(CreateItem("Overwolf Overlay & Caches", overwolf, "system", size, 0, "Résidus de cache du logiciel Overwolf."));
            }

            var slack = Path.Combine(appData, "Slack");
            if (Directory.Exists(slack))
            {
                double size = GetDirectorySizeSafe(slack);
                items.Add(CreateItem("Slack Cache & Data", slack, "system", size, 0, "Caches locaux de l'application Slack."));
            }

            // E. Caches Mod Managers (Thunderstore, Lunar Client, CurseForge)
            var thunderstore = Path.Combine(appData, "Thunderstore Mod Manager");
            if (Directory.Exists(thunderstore))
            {
                double size = GetDirectorySizeSafe(thunderstore);
                if (size > 200 * 1024 * 1024)
                {
                    items.Add(CreateItem("Thunderstore Mod Manager Caches", thunderstore, "system", size, 5, "Caches et profils de mods de jeux moddés (Lethal Company, etc.)."));
                }
            }

            var lunar = Path.Combine(user, ".lunarclient");
            if (Directory.Exists(lunar))
            {
                double size = GetDirectorySizeSafe(lunar);
                if (size > 200 * 1024 * 1024)
                {
                    items.Add(CreateItem("Lunar Client Minecraft (.lunarclient)", lunar, "system", size, 10, "Versions et caches du client Minecraft Lunar."));
                }
            }

            var curseforge = Path.Combine(user, "curseforge");
            if (Directory.Exists(curseforge))
            {
                double size = GetDirectorySizeSafe(curseforge);
                if (size > 200 * 1024 * 1024)
                {
                    items.Add(CreateItem("CurseForge Modpacks", curseforge, "system", size, 10, "Instances de packs de mods Minecraft téléchargés."));
                }
            }
        }

        private static void AuditDownloads(List<CleanerItem> items)
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dlDirs = new[] { Path.Combine(user, "Downloads"), @"D:\Téléchargements" };

            foreach (var dl in dlDirs)
            {
                if (!Directory.Exists(dl)) continue;

                // 1. Installeurs (.exe, .msi, .AppImage)
                var files = Directory.GetFiles(dl);
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.ToLowerInvariant();
                    if (ext == ".exe" || ext == ".msi" || ext == ".appimage")
                    {
                        double size = fi.Length;
                        if (size > 5 * 1024 * 1024) // > 5 MB
                        {
                            items.Add(CreateItem(
                                $"Installeur : {fi.Name}",
                                f,
                                "downloads",
                                size,
                                0,
                                $"Installeur résiduel téléchargé le {fi.LastWriteTime:dd/MM/yyyy}. Inutile une fois le logiciel installé.",
                                isFolder: false
                            ));
                        }
                    }
                }

                // 2. Archives volumineuses
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.ToLowerInvariant();
                    if ((ext == ".zip" || ext == ".rar" || ext == ".7z") && fi.Length > 50 * 1024 * 1024)
                    {
                        bool isPersonal = fi.Name.Contains("Direct Messages", StringComparison.OrdinalIgnoreCase);
                        int risk = isPersonal ? 95 : 5;
                        items.Add(CreateItem(
                            $"Archive : {fi.Name}",
                            f,
                            "downloads",
                            fi.Length,
                            risk,
                            isPersonal ? "Archive personnelle Discrub/Discord. Recommandé de conserver !" : "Archive téléchargée potentiellement déjà extraite.",
                            isFolder: false,
                            isSanctuarized: isPersonal
                        ));
                    }
                }

                // 3. Sous-dossiers de builds ou outils
                foreach (var subDir in Directory.GetDirectories(dl))
                {
                    var di = new DirectoryInfo(subDir);
                    if (di.Name.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Contains("r2modman", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Contains("TwitchDropsMiner", StringComparison.OrdinalIgnoreCase) ||
                        di.Name.Contains("test", StringComparison.OrdinalIgnoreCase))
                    {
                        double size = GetDirectorySizeSafe(subDir);
                        if (size > 50 * 1024 * 1024)
                        {
                            items.Add(CreateItem(
                                $"Dossier : {di.Name}",
                                subDir,
                                "downloads",
                                size,
                                5,
                                $"Dossier de build temporaire ou outil extrait dans Téléchargements."
                            ));
                        }
                    }
                }
            }
        }

        private static void AuditAppsAndEmulators(List<CleanerItem> items)
        {
            // A. Émulateurs tiers & ROMs orphelines
            var emulators = new[]
            {
                new { Name = "Émulateur Android NoxPlayer", Path = @"D:\Program Files\Nox", Risk = 5 },
                new { Name = "ROMs Switch Super Smash Bros (Yuzu)", Path = @"E:\Yuzu", Risk = 5 },
                new { Name = "Émulateur Dolphin & ISO Wii", Path = @"C:\Jeux dolphin", Risk = 0 }
            };

            foreach (var emu in emulators)
            {
                if (Directory.Exists(emu.Path))
                {
                    double size = GetDirectorySizeSafe(emu.Path);
                    if (size > 10 * 1024 * 1024)
                    {
                        items.Add(CreateItem(
                            emu.Name,
                            emu.Path,
                            "apps",
                            size,
                            emu.Risk,
                            "Émulateur tiers ou collection de ROMs orphelines."
                        ));
                    }
                }
            }
        }

        public static ResearchReport ResearchItem(string name, string path)
        {
            var report = new ResearchReport
            {
                ItemName = name,
                ItemPath = path
            };

            bool exists = Directory.Exists(path) || File.Exists(path);
            if (!exists)
            {
                report.Definition = "L'élément est déjà supprimé ou n'existe plus.";
                report.UsageActivity = "Inactif";
                report.SafetyImpact = "Aucun impact.";
                report.RiskPct = 0;
                return report;
            }

            // Vérification de la date de dernier accès
            DateTime lastWrite;
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                lastWrite = fi.LastWriteTime;
            }
            else
            {
                var di = new DirectoryInfo(path);
                lastWrite = GetLastWriteTimeSafe(di);
            }
            report.LastAccessDate = lastWrite.ToString("dd MMMM yyyy");

            // Détection de processus en mémoire liés
            var baseName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var running = Process.GetProcesses().Any(p =>
                p.ProcessName.Contains(baseName, StringComparison.OrdinalIgnoreCase) ||
                (name.Contains("Steam", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase)) ||
                (name.Contains("Armoury", StringComparison.OrdinalIgnoreCase) && p.ProcessName.Contains("armoury", StringComparison.OrdinalIgnoreCase))
            );
            report.IsRunning = running;

            // Définition et Origine adaptatives
            if (path.Contains("downloading", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = "Caches temporaires de téléchargements Steam incomplets ou interrompus.";
                report.Origin = "Généré par le client Steam lors des mises à jour.";
                report.SafetyImpact = "Suppression 100% sans risque. Steam retéléchargera proprement si nécessaire.";
                report.RiskPct = 0;
            }
            else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = "Fichier d'installation autonome utilisé pour déployer un logiciel.";
                report.Origin = "Téléchargé par l'utilisateur depuis un navigateur web.";
                report.SafetyImpact = "Suppression 100% sécurisée dès que le logiciel est déjà installé sur le PC.";
                report.RiskPct = 0;
            }
            else if (path.Contains("Direct Messages", StringComparison.OrdinalIgnoreCase))
            {
                report.Definition = "Sauvegarde d'historique de messages et photos Discord via Discrub.";
                report.Origin = "Export manuel de vos conversations Discord.";
                report.SafetyImpact = "⚠️ Risque Élevé : Contient des souvenirs personnels et des fichiers privés irréversibles.";
                report.RiskPct = 95;
            }
            else
            {
                report.Definition = $"Dossier ou application situé dans {path}.";
                report.Origin = "Installé ou généré par une application tierce.";
                report.SafetyImpact = running
                    ? "⚠️ Attention : Un processus lié est actuellement actif en mémoire. Fermez-le avant de supprimer."
                    : "Aucun processus actif détecté. Suppression sécurisée en mode silencieux.";
                report.RiskPct = running ? 20 : 5;
            }

            report.UsageActivity = running
                ? "Processus ACTIF en mémoire actuellement !"
                : $"Dernière activité détectée le {report.LastAccessDate} (Inactif).";

            return report;
        }

        public static CleanResult DeleteItem(CleanerItem item)
        {
            // Vérification stricte des sanctuarisations
            foreach (var sp in SanctuarizedPaths)
            {
                if (item.Path.StartsWith(sp, StringComparison.OrdinalIgnoreCase))
                {
                    return new CleanResult
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        FreedGB = 0,
                        Success = false,
                        Message = $"Suppression bloquée : Le chemin {item.Path} est sanctuarisé."
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
                    
                    // Stop process if needed
                    var baseName = Path.GetFileName(item.Path);
                    foreach (var proc in Process.GetProcessesByName(baseName))
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }

                    Directory.Delete(item.Path, true);

                    // Si c'est downloading, on recrée le dossier vide
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
                    Message = $"Erreur lors de la suppression : {ex.Message}"
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
    }
}
