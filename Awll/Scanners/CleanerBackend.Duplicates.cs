using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Awll
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

        private sealed class FastDuplicateDirScanner : FileSystemEnumerator<byte>
        {
            private readonly long _minBytes;

            public List<string> SubDirs { get; } = new();
            public List<(string Path, string Name, long Length)> LargeFiles { get; } = new();

            public FastDuplicateDirScanner(string directory, long minBytes)
                : base(directory, SafeTopOpts)
            {
                _minBytes = minBytes;
            }

            public void Run()
            {
                try
                {
                    while (MoveNext()) { }
                }
                catch { }
            }

            protected override byte TransformEntry(ref FileSystemEntry entry) => 0;

            protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;

                if (entry.IsDirectory)
                {
                    string fullDir = entry.ToFullPath();
                    if (!ShouldSkipDuplicateDirectory(fullDir))
                        SubDirs.Add(fullDir);
                    return false;
                }

                long len = entry.Length;
                if (len >= _minBytes)
                {
                    LargeFiles.Add((entry.ToFullPath(), entry.FileName.ToString(), len));
                }
                return false;
            }
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
                        ? selectedDrives
                            .Select(d => { var t = (d ?? "").Trim(); return (t.Length == 2 && t[1] == ':') ? t + @"\" : t; })
                            .Where(d => Directory.Exists(d))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                        : DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.Name).ToList();
                    progressCallback?.Invoke($"Étape 1/3 : Parcours sur {string.Join(", ", roots.Select(r => r.TrimEnd('\\')))}...", 5);
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
            var bySize = new Dictionary<long, List<(string Path, string Name, long Length)>>();
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
                    if ((File.GetAttributes(currentDir) & FileAttributes.ReparsePoint) != 0) continue;

                    using var scanner = new FastDuplicateDirScanner(currentDir, minBytes);
                    scanner.Run();

                    for (int i = 0; i < scanner.LargeFiles.Count; i++)
                    {
                        var f = scanner.LargeFiles[i];
                        if (!bySize.TryGetValue(f.Length, out var list))
                        {
                            list = new List<(string Path, string Name, long Length)>();
                            bySize[f.Length] = list;
                        }
                        list.Add(f);
                    }

                    for (int i = 0; i < scanner.SubDirs.Count; i++)
                    {
                        stack.Push(scanner.SubDirs[i]);
                    }
                }
                catch { }
            }

            // Étape 2 : Pré-empreinte rapide parallèle (début + milieu + fin) sur les tailles en double
            var flatPartialCandidates = bySize.Where(kv => kv.Value.Count >= 2).SelectMany(kv => kv.Value).ToList();
            int totalPartialFiles = flatPartialCandidates.Count;
            int donePartial = 0;
            progressCallback?.Invoke($"Étape 2/3 : Pré-empreinte rapide sur {totalPartialFiles} fichiers candidats...", 50);

            var byPartialHash = new ConcurrentDictionary<string, ConcurrentBag<(string Path, string Name, long Length)>>(StringComparer.Ordinal);
            if (totalPartialFiles > 0)
            {
                Parallel.ForEach(
                    flatPartialCandidates,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount)) },
                    f =>
                    {
                        int cur = Interlocked.Increment(ref donePartial);
                        if (cur % 10 == 0 || cur == totalPartialFiles)
                        {
                            int pPct = 50 + (int)Math.Round(25.0 * cur / totalPartialFiles);
                            progressCallback?.Invoke($"Étape 2/3 : Pré-empreinte ({cur}/{totalPartialFiles}) : {f.Name}", Math.Min(75, pPct));
                        }
                        var pHash = ComputePartialFileHash(f.Path, f.Length);
                        if (string.IsNullOrEmpty(pHash)) return;
                        string key = $"{f.Length}:{pHash}";
                        var bag = byPartialHash.GetOrAdd(key, _ => new ConcurrentBag<(string Path, string Name, long Length)>());
                        bag.Add(f);
                    });
            }

            // Étape 3 : SHA-256 complet parallèle uniquement sur les collisions d'empreinte rapide
            var flatFullCandidates = byPartialHash.Where(kv => kv.Value.Count >= 2).SelectMany(kv => kv.Value).ToList();
            int totalFullFiles = flatFullCandidates.Count;
            int doneFull = 0;
            progressCallback?.Invoke($"Étape 3/3 : Vérification SHA-256 exacte sur {totalFullFiles} fichiers...", 75);

            var byFullHash = new ConcurrentDictionary<string, ConcurrentBag<(string Path, string Name, long Length)>>(StringComparer.Ordinal);
            if (totalFullFiles > 0)
            {
                Parallel.ForEach(
                    flatFullCandidates,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount)) },
                    f =>
                    {
                        int cur = Interlocked.Increment(ref doneFull);
                        int fPct = 75 + (int)Math.Round(24.0 * cur / totalFullFiles);
                        progressCallback?.Invoke($"Étape 3/3 : SHA-256 ({cur}/{totalFullFiles}) : {f.Name}", Math.Min(99, fPct));
                        var fullHash = ComputeFullFileSha256(f.Path);
                        if (string.IsNullOrEmpty(fullHash)) return;
                        var bag = byFullHash.GetOrAdd(fullHash, _ => new ConcurrentBag<(string Path, string Name, long Length)>());
                        bag.Add(f);
                    });
            }

            var groups = new List<DuplicateGroup>();
            foreach (var kv in byFullHash.Where(x => x.Value.Count >= 2))
            {
                var itemsList = kv.Value.ToList();
                var files = itemsList
                    .Select(f =>
                    {
                        bool sanct = IsPathSanctuarized(f.Path);
                        bool prot = IsProtected(f.Path, f.Name);
                        DateTime effDate;
                        try { effDate = GetFileEffectiveActivityDate(new FileInfo(f.Path)); }
                        catch { effDate = DateTime.Now; }
                        return new DuplicateFileItem
                        {
                            Name = f.Name,
                            Path = f.Path,
                            Drive = Path.GetPathRoot(f.Path) ?? "",
                            SizeGB = Math.Round(f.Length / 1024.0 / 1024.0 / 1024.0, 3),
                            SizeMB = Math.Round(f.Length / 1024.0 / 1024.0, 1),
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

                long sizeBytes = itemsList[0].Length;
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
