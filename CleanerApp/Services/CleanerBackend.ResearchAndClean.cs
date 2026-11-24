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
        #region RECHERCHE FACTUELLE & SUPPRESSION SÉCURISÉE

        public static ResearchReport ResearchItem(string name, string path, int initialRisk = 5, bool bypassAiCache = false)
        {
            if (!bypassAiCache && !string.IsNullOrWhiteSpace(path))
            {
                var aiCache = LoadAiCache();
                if (aiCache.TryGetValue(path.Trim(), out var cachedReport))
                {
                    cachedReport.IsAiEnriched = true;
                    long maxAgeMs = 30L * 24 * 60 * 60 * 1000;
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (cachedReport.CachedAtUnixMs == 0 || (nowMs - cachedReport.CachedAtUnixMs) <= maxAgeMs)
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
                report.UsageActivity = $"Dernière écriture fichiers : {report.LastAccessDate} (peut correspondre à une mise à jour automatique du launcher sans avoir lancé le jeu).";
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
                report.RiskPct = ComputeRiskScore("installer", ageDays, running, false, false);
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

            FinalizeResearchReport(report, path, name, running, initialRisk);
            return report;
        }

        internal static bool IsPureCacheOrDisposable(string name, string path)
        {
            string combined = ((name ?? "") + " " + (path ?? "")).ToLowerInvariant();
            return combined.Contains("cache")
                || combined.Contains("shadercache")
                || combined.Contains("\\temp")
                || combined.Contains("\\tmp")
                || combined.Contains("$recycle.bin")
                || combined.Contains("corbeille")
                || combined.Contains("softwaredistribution")
                || combined.Contains("\\downloading")
                || combined.Contains("crashdumps")
                || combined.Contains("-updater")
                || combined.Contains("trace registre")
                || combined.Contains("\\.nuget")
                || combined.Contains("\\.gradle")
                || combined.Contains("\\.m2")
                || combined.Contains("\\ms-playwright")
                || combined.Contains("\\pip\\")
                || combined.Contains("\\npm-cache");
        }

        internal static string GetRiskLevelLabel(int riskPct) => riskPct switch
        {
            <= 15 => "Quasi Nul",
            <= 35 => "Faible",
            <= 65 => "Modéré",
            _ => "Élevé"
        };

        internal static void NormalizeAuditItemsGlobal(List<CleanerItem> recommendedItems, List<CleanerItem> extraItems, HashSet<string> runningProcs)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedRec = new List<CleanerItem>(recommendedItems.Count);
            var normalizedExtra = new List<CleanerItem>(extraItems.Count);

            foreach (var item in recommendedItems.Concat(extraItems))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Path)) continue;
                string key = item.Path.TrimEnd('\\');
                if (!seen.Add(key)) continue;

                bool sanct = item.IsSanctuarized || IsPathSanctuarized(item.Path);
                bool isCore = IsCoreSystemOrRuntime(item.Name, item.Path);
                bool isCache = IsPureCacheOrDisposable(item.Name, item.Path);
                bool isRunning = !isCache && IsLikelyRunning(item.Name, item.Path, runningProcs);

                if (sanct)
                {
                    item.IsSanctuarized = true;
                    item.RiskPct = Math.Max(item.RiskPct, 95);
                }
                else if (isCore)
                {
                    item.RiskPct = Math.Max(item.RiskPct, 85);
                }
                else if (isRunning)
                {
                    item.RiskPct = Math.Max(item.RiskPct, 55);
                    if (!item.Description.Contains("En cours", StringComparison.OrdinalIgnoreCase))
                        item.Description = $"⚠️ En cours d'exécution — {item.Description}";
                }

                item.RiskPct = Math.Clamp(item.RiskPct, 0, 100);
                item.RiskLevel = GetRiskLevelLabel(item.RiskPct);

                bool forceExtra = item.IsExtra || item.IsPersonal || sanct || isCore || isRunning || item.RiskPct > 35;
                item.IsExtra = forceExtra;
                if (forceExtra) normalizedExtra.Add(item);
                else normalizedRec.Add(item);
            }

            recommendedItems.Clear();
            recommendedItems.AddRange(normalizedRec);
            extraItems.Clear();
            extraItems.AddRange(normalizedExtra);
        }

        internal static void FinalizeResearchReport(ResearchReport report, string path, string name, bool running, int initialRisk = -1)
        {
            if (initialRisk >= 0)
            {
                report.RiskPct = initialRisk;
            }
            bool isCache = IsPureCacheOrDisposable(name, path);
            bool sanct = IsPathSanctuarized(path);
            bool isCore = IsCoreSystemOrRuntime(name, path);

            if (sanct)
            {
                report.RiskPct = Math.Max(report.RiskPct, 95);
                if (!report.SafetyImpact.Contains("Sanctuaris", StringComparison.OrdinalIgnoreCase))
                    report.SafetyImpact = $"🛡️ Dossier sanctuarisé protégé. {report.SafetyImpact}";
            }
            else if (isCore)
            {
                report.RiskPct = Math.Max(report.RiskPct, 85);
            }
            else if (running && !isCache)
            {
                report.RiskPct = Math.Max(report.RiskPct + 25, 55);
                if (!report.SafetyImpact.Contains("En cours", StringComparison.OrdinalIgnoreCase) &&
                    !report.SafetyImpact.Contains("actif", StringComparison.OrdinalIgnoreCase))
                {
                    report.SafetyImpact = $"⚠️ Processus actif en mémoire. Fermez l'application avant de nettoyer. {report.SafetyImpact}";
                }
            }

            report.RiskPct = Math.Clamp(report.RiskPct, 0, 100);
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
                http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

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
                string[] modelsToTry = { "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-3.8-flash" };
                HttpResponseMessage? response = null;
                string usedModel = modelsToTry[0];

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (attempt > 0) await Task.Delay(400);
                    foreach (var modelName in modelsToTry)
                    {
                        usedModel = modelName;
                        try
                        {
                            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(7));
                            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                            response = await http.PostAsync(url, content, cts.Token);
                            if (response.IsSuccessStatusCode)
                                break;

                            int code = (int)response.StatusCode;
                            if (code != 404 && code != 429 && code < 500)
                                break;
                        }
                        catch (TaskCanceledException)
                        {
                            response = null;
                        }

                        await Task.Delay(150);
                    }
                    if (response != null && response.IsSuccessStatusCode) break;
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    var respJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                        if (ParseGeminiResearchJson(text, report))
                        {
                            report.IsAiEnriched = true;
                            report.AiModelUsed = usedModel;
                            report.CachedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            SaveAiCacheEntry(report);
                        }
                        else
                        {
                            report.AiModelUsed = "Réponse Gemini invalide (format JSON inattendu).";
                        }
                    }
                }
                else
                {
                    string errDetail = "";
                    if (response != null)
                    {
                        int statusCode = (int)response.StatusCode;
                        if (statusCode == 503 || statusCode == 429)
                        {
                            report.AiModelUsed = $"Serveurs Gemini temporairement saturés (HTTP {statusCode}). Réessayez dans quelques secondes.";
                        }
                        else
                        {
                            try
                            {
                                var errBody = await response.Content.ReadAsStringAsync();
                                using var errDoc = JsonDocument.Parse(errBody);
                                if (errDoc.RootElement.TryGetProperty("error", out var errObj) && errObj.TryGetProperty("message", out var msgProp))
                                    errDetail = msgProp.GetString() ?? "";
                            }
                            catch { }
                            report.AiModelUsed = $"Erreur API HTTP {statusCode}{(string.IsNullOrEmpty(errDetail) ? "" : " : " + errDetail)}";
                        }
                    }
                    else
                    {
                        report.AiModelUsed = "Délai dépassé : aucune réponse du serveur Gemini.";
                    }
                }
            }
            catch (Exception ex)
            {
                report.AiModelUsed = $"Erreur : {ex.Message}";
            }

            return report;
        }

        internal static bool ParseGeminiResearchJson(string rawText, ResearchReport report)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return false;
            try
            {
                var cleaned = rawText.Trim();
                if (cleaned.StartsWith("```"))
                {
                    int firstNewline = cleaned.IndexOf('\n');
                    if (firstNewline >= 0) cleaned = cleaned.Substring(firstNewline + 1);
                    int lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
                    if (lastFence >= 0) cleaned = cleaned.Substring(0, lastFence);
                    cleaned = cleaned.Trim();
                }

                using var parsedAi = JsonDocument.Parse(cleaned);
                var root = parsedAi.RootElement;

                if (root.TryGetProperty("definition", out var defProp) && !string.IsNullOrWhiteSpace(defProp.GetString()))
                    report.Definition = defProp.GetString() ?? "";

                if (root.TryGetProperty("origin", out var origProp) && !string.IsNullOrWhiteSpace(origProp.GetString()))
                    report.Origin = origProp.GetString() ?? "";

                if (root.TryGetProperty("usageActivity", out var useProp) && !string.IsNullOrWhiteSpace(useProp.GetString()))
                    report.UsageActivity = useProp.GetString() ?? "";

                if (root.TryGetProperty("safetyImpact", out var safeProp) && !string.IsNullOrWhiteSpace(safeProp.GetString()))
                    report.SafetyImpact = safeProp.GetString() ?? "";

                if (root.TryGetProperty("riskPct", out var riskProp) && riskProp.TryGetInt32(out int aiRisk))
                    report.RiskPct = Math.Clamp(aiRisk, 0, 100);

                return true;
            }
            catch
            {
                return false;
            }
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

        public static CleanResult DeleteItem(CleanerItem item, bool useRecycleBin = false, bool allowManualPersonalChild = false)
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

            // Vérification de la sanctuarisation universelle (les sous-éléments de Documents/Vidéos peuvent être supprimés manuellement depuis Explorateur/Doublons)
            var docsRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).TrimEnd('\\');
            var videosRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos).TrimEnd('\\');
            var itemTrim = (item.Path ?? "").TrimEnd('\\');
            bool isPersonalChild = allowManualPersonalChild &&
                ((!string.IsNullOrEmpty(docsRoot) && itemTrim.StartsWith(docsRoot + @"\", StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrEmpty(videosRoot) && itemTrim.StartsWith(videosRoot + @"\", StringComparison.OrdinalIgnoreCase)));
            if (!isPersonalChild)
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
                    if (useRecycleBin)
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            item.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = gb, Success = true, Message = "Fichier envoyé à la Corbeille." };
                    }
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

                    if (useRecycleBin && !item.Path.EndsWith("downloading", StringComparison.OrdinalIgnoreCase))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            item.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        return new CleanResult { ItemId = item.Id, ItemName = item.Name, FreedGB = gb, Success = true, Message = "Dossier envoyé à la Corbeille." };
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

            if (!isSanctuarized && IsPathSanctuarized(path))
            {
                isSanctuarized = true;
            }
            if (isSanctuarized)
            {
                riskPct = Math.Max(riskPct, 95);
                isExtra = true;
            }

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
            if (ExecutionHistoryService.TryGetFolderLastExecution(path, out var execTime))
            {
                maxWrite = execTime;
            }
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
            if (fi.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                ExecutionHistoryService.TryGetFileLastExecution(fi.FullName, out var execTime))
            {
                return execTime;
            }
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
            if (ExecutionHistoryService.TryGetFolderLastExecution(di.FullName, out var folderExec))
            {
                return folderExec;
            }
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
        #endregion
    }
}