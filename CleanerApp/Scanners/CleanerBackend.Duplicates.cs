using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CleanerApp
{
    public static partial class CleanerBackend
    {
        private static readonly string[] DuplicateSkipSegments =
        {
            @"\windows\",
            @"\$recycle.bin",
            @"\system volume information",
            @"\recovery\",
            @"\$winreagent",
            @"\perflogs\",
            @"\programdata\microsoft\windows\"
        };

        private static bool ShouldSkipDuplicateDirectory(string dirPath)
        {
            var lower = (dirPath.EndsWith(@"\") ? dirPath : dirPath + @"\").ToLowerInvariant();
            foreach (var seg in DuplicateSkipSegments)
            {
                if (lower.Contains(seg)) return true;
            }
            return false;
        }

        public static Task<List<DuplicateGroup>> ScanDuplicatesGlobalAsync(
            Action<string, int>? progressCallback = null,
            List<string>? selectedDrives = null,
            long minBytes = 50L * 1024 * 1024,
            List<string>? customPaths = null)
        {
            return Task.Run(() =>
            {
                List<string> roots;
                if (customPaths != null && customPaths.Count > 0)
                {
                    roots = customPaths.Where(p => Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    progressCallback?.Invoke($"Étape 1/3 : Parcours de {roots.Count} dossier(s) ciblé(s)...", 5);
                }
                else
                {
                    roots = (selectedDrives != null && selectedDrives.Count > 0)
                        ? selectedDrives.Where(d => Directory.Exists(d)).ToList()
                        : DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.Name).ToList();
                    progressCallback?.Invoke($"Étape 1/3 : Parcours global sur {roots.Count} disque(s)...", 5);
                }

                var result = FindDuplicateGroupsInDirectories(roots, minBytes, progressCallback);
                progressCallback?.Invoke($"Analyse terminée ({result.Count} groupe(s) de doublons).", 100);
                return result;
            });
        }

        internal static List<DuplicateGroup> FindDuplicateGroupsInDirectories(
            IEnumerable<string> roots,
            long minBytes = 50L * 1024 * 1024,
            Action<string, int>? progressCallback = null)
        {
            var bySize = new Dictionary<long, List<FileInfo>>();
            var stack = new Stack<string>();
            foreach (var r in roots)
            {
                if (Directory.Exists(r)) stack.Push(r);
            }

            int dirsVisited = 0;
            while (stack.Count > 0)
            {
                var currentDir = stack.Pop();
                if (ShouldSkipDuplicateDirectory(currentDir)) continue;
                dirsVisited++;
                if (dirsVisited % 250 == 0)
                {
                    int walkPct = 5 + (int)Math.Round(45.0 * dirsVisited / (dirsVisited + 4000.0));
                    progressCallback?.Invoke($"Étape 1/3 : Parcours des dossiers ({dirsVisited:N0} analysés, {bySize.Count:N0} fichiers >= {Math.Round(minBytes / 1024.0 / 1024.0)} Mo)...", Math.Min(49, walkPct));
                }

                try
                {
                    var di = new DirectoryInfo(currentDir);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                    foreach (var fi in di.EnumerateFiles("*", SafeTopOpts))
                    {
                        try
                        {
                            if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (fi.Length < minBytes) continue;
                            if (!bySize.TryGetValue(fi.Length, out var list))
                            {
                                list = new List<FileInfo>();
                                bySize[fi.Length] = list;
                            }
                            list.Add(fi);
                        }
                        catch { }
                    }

                    foreach (var sub in di.EnumerateDirectories("*", SafeTopOpts))
                    {
                        try
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (!ShouldSkipDuplicateDirectory(sub.FullName))
                                stack.Push(sub.FullName);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Étape 2 : Pré-empreinte rapide (début + milieu + fin) sur les tailles en double
            var sizeCandidates = bySize.Where(kv => kv.Value.Count >= 2).ToList();
            int totalPartialFiles = sizeCandidates.Sum(kv => kv.Value.Count);
            int donePartial = 0;
            progressCallback?.Invoke($"Étape 2/3 : Pré-empreinte rapide sur {totalPartialFiles} fichiers candidats...", 50);

            var byPartialHash = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
            foreach (var kv in sizeCandidates)
            {
                foreach (var fi in kv.Value)
                {
                    donePartial++;
                    if (donePartial % 10 == 0 || donePartial == totalPartialFiles)
                    {
                        int pPct = 50 + (totalPartialFiles > 0 ? (int)Math.Round(25.0 * donePartial / totalPartialFiles) : 25);
                        progressCallback?.Invoke($"Étape 2/3 : Pré-empreinte ({donePartial}/{totalPartialFiles}) : {fi.Name}", Math.Min(75, pPct));
                    }
                    var pHash = ComputePartialFileHash(fi.FullName, fi.Length);
                    if (string.IsNullOrEmpty(pHash)) continue;
                    string key = $"{fi.Length}:{pHash}";
                    if (!byPartialHash.TryGetValue(key, out var pList))
                    {
                        pList = new List<FileInfo>();
                        byPartialHash[key] = pList;
                    }
                    pList.Add(fi);
                }
            }

            // Étape 3 : SHA-256 complet uniquement sur les collisions d'empreinte rapide
            var partialCandidates = byPartialHash.Where(kv => kv.Value.Count >= 2).ToList();
            int totalFullFiles = partialCandidates.Sum(kv => kv.Value.Count);
            int doneFull = 0;
            progressCallback?.Invoke($"Étape 3/3 : Vérification SHA-256 exacte sur {totalFullFiles} fichiers...", 75);

            var byFullHash = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
            foreach (var kv in partialCandidates)
            {
                foreach (var fi in kv.Value)
                {
                    doneFull++;
                    int fPct = 75 + (totalFullFiles > 0 ? (int)Math.Round(24.0 * doneFull / totalFullFiles) : 24);
                    progressCallback?.Invoke($"Étape 3/3 : SHA-256 ({doneFull}/{totalFullFiles}) : {fi.Name}", Math.Min(99, fPct));
                    var fullHash = ComputeFullFileSha256(fi.FullName);
                    if (string.IsNullOrEmpty(fullHash)) continue;
                    if (!byFullHash.TryGetValue(fullHash, out var fList))
                    {
                        fList = new List<FileInfo>();
                        byFullHash[fullHash] = fList;
                    }
                    fList.Add(fi);
                }
            }

            var groups = new List<DuplicateGroup>();
            foreach (var kv in byFullHash.Where(x => x.Value.Count >= 2))
            {
                var files = kv.Value
                    .Select(fi =>
                    {
                        bool sanct = IsPathSanctuarized(fi.FullName);
                        bool prot = IsProtected(fi.FullName, fi.Name);
                        var effDate = GetFileEffectiveActivityDate(fi);
                        return new DuplicateFileItem
                        {
                            Name = fi.Name,
                            Path = fi.FullName,
                            Drive = Path.GetPathRoot(fi.FullName) ?? "",
                            SizeGB = Math.Round(fi.Length / 1024.0 / 1024.0 / 1024.0, 3),
                            SizeMB = Math.Round(fi.Length / 1024.0 / 1024.0, 1),
                            LastModified = effDate.ToString("dd/MM/yyyy"),
                            IsSanctuarized = sanct,
                            IsProtected = prot
                        };
                    })
                    // Garder en premier la copie la plus légitime (sanctuarisée > protégée > hors Téléchargements > plus ancienne)
                    .OrderByDescending(f => f.IsSanctuarized)
                    .ThenByDescending(f => f.IsProtected)
                    .ThenBy(f => f.Path.Contains(@"\Downloads\", StringComparison.OrdinalIgnoreCase) || f.Path.Contains(@"\Téléchargements\", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(f => f.Path.Length)
                    .ToList();

                for (int i = 0; i < files.Count; i++)
                {
                    files[i].SuggestedDelete = i > 0 && !files[i].IsSanctuarized && !files[i].IsProtected;
                }

                long sizeBytes = kv.Value[0].Length;
                long reclaimBytes = sizeBytes * (files.Count - 1);
                groups.Add(new DuplicateGroup
                {
                    Hash = kv.Key.Length > 12 ? kv.Key.Substring(0, 12) : kv.Key,
                    SizeBytes = sizeBytes,
                    SizeGB = Math.Round(sizeBytes / 1024.0 / 1024.0 / 1024.0, 3),
                    SizeMB = Math.Round(sizeBytes / 1024.0 / 1024.0, 1),
                    ReclaimableGB = Math.Round(reclaimBytes / 1024.0 / 1024.0 / 1024.0, 2),
                    ReclaimableMB = Math.Round(reclaimBytes / 1024.0 / 1024.0, 1),
                    Files = files
                });
            }

            return groups
                .OrderByDescending(g => (g.Files.Count - 1) * g.SizeBytes)
                .Take(150)
                .ToList();
        }

        private static string ComputePartialFileHash(string path, long length)
        {
            try
            {
                const int chunkSize = 65536;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sha = SHA256.Create();
                var buffer = new byte[chunkSize];

                int read1 = fs.Read(buffer, 0, (int)Math.Min(chunkSize, length));
                sha.TransformBlock(buffer, 0, read1, null, 0);

                if (length > chunkSize * 2)
                {
                    fs.Seek(length / 2 - chunkSize / 2, SeekOrigin.Begin);
                    int read2 = fs.Read(buffer, 0, chunkSize);
                    sha.TransformBlock(buffer, 0, read2, null, 0);
                }

                if (length > chunkSize)
                {
                    fs.Seek(Math.Max(0, length - chunkSize), SeekOrigin.Begin);
                    int read3 = fs.Read(buffer, 0, chunkSize);
                    sha.TransformBlock(buffer, 0, read3, null, 0);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
            }
            catch
            {
                return "";
            }
        }

        private static string ComputeFullFileSha256(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1048576);
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(fs));
            }
            catch
            {
                return "";
            }
        }
    }
}
