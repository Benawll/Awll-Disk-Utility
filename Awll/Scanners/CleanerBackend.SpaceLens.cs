using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace Awll
{
    public static partial class CleanerBackend
    {
        #region EXPLORATEUR D'ESPACE ARBORESCENT (SPACE LENS — INDEXATION BOTTOM-UP)

        private sealed class IndexedChild
        {
            public string Name { get; }
            public string Path { get; }
            public long SizeBytes { get; }
            public int FileCount { get; }
            public long WriteTicks { get; }
            public bool IsFolder { get; }

            public IndexedChild(string name, string path, long sizeBytes, int fileCount, long writeTicks, bool isFolder)
            {
                Name = name;
                Path = path;
                SizeBytes = sizeBytes;
                FileCount = fileCount;
                WriteTicks = writeTicks;
                IsFolder = isFolder;
            }
        }

        private sealed class IndexedDirNode
        {
            public string Name { get; }
            public string Path { get; }
            public long SizeBytes { get; }
            public int FileCount { get; }
            public long WriteTicks { get; }
            public List<IndexedChild> TopChildren { get; }

            public IndexedDirNode(string name, string path, long sizeBytes, int fileCount, long writeTicks, List<IndexedChild> topChildren)
            {
                Name = name;
                Path = path;
                SizeBytes = sizeBytes;
                FileCount = fileCount;
                WriteTicks = writeTicks;
                TopChildren = topChildren;
            }
        }

        private static readonly ConcurrentDictionary<string, IndexedDirNode> _spaceIndex = new(StringComparer.OrdinalIgnoreCase);

        private static string NormalizeSpaceKey(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            string k = path.Trim().Replace('/', '\\');
            if (k.Length == 2 && k[1] == ':') return k + @"\";
            if (k.Length > 3) return k.TrimEnd('\\');
            return k;
        }

        internal static bool IsPathIndexedInSpaceLens(string path)
        {
            var key = NormalizeSpaceKey(path);
            return !string.IsNullOrEmpty(key) && _spaceIndex.ContainsKey(key);
        }

        internal static bool TryGetIndexedDirStats(string path, out long sizeBytes, out int fileCount, out DateTime lastWrite)
        {
            sizeBytes = 0;
            fileCount = 0;
            lastWrite = DateTime.Now;
            var key = NormalizeSpaceKey(path);
            if (string.IsNullOrEmpty(key) || !_spaceIndex.TryGetValue(key, out var node))
                return false;

            sizeBytes = node.SizeBytes;
            fileCount = node.FileCount;
            if (ExecutionHistoryService.TryGetFolderLastExecution(path, out var execTime))
            {
                lastWrite = execTime;
            }
            else if (node.WriteTicks > 0)
            {
                try { lastWrite = new DateTime(node.WriteTicks, DateTimeKind.Utc).ToLocalTime(); }
                catch { lastWrite = DateTime.Now; }
            }
            return true;
        }

        private sealed class FastDirStatsOnlyScanner : FileSystemEnumerator<byte>
        {
            private readonly long _nowLimitUtcTicks;

            public List<string> SubDirs { get; } = new();
            public long DirectFileBytes { get; private set; }
            public int DirectFileCount { get; private set; }
            public long MaxWriteTicks { get; private set; }

            public FastDirStatsOnlyScanner(string directory, long nowLimitUtcTicks)
                : base(directory, SafeTopOpts)
            {
                _nowLimitUtcTicks = nowLimitUtcTicks;
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
                    if (!entry.FileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                    {
                        SubDirs.Add(entry.ToFullPath());
                    }
                    return false;
                }

                DirectFileBytes += entry.Length;
                DirectFileCount++;

                long lw = entry.LastWriteTimeUtc.UtcTicks;
                long ct = entry.CreationTimeUtc.UtcTicks;
                long eff = (ct > lw && ct <= _nowLimitUtcTicks) ? ct : lw;
                if (eff > MaxWriteTicks && eff <= _nowLimitUtcTicks)
                {
                    MaxWriteTicks = eff;
                }

                return false;
            }
        }

        internal static void PrepopulateAuditStatsBottomUp(
            string rootPath,
            ConcurrentDictionary<string, (long sizeBytes, int fileCount, DateTime lastWrite)> targetCache)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return;

            long nowLimitUtcTicks = DateTimeOffset.UtcNow.AddDays(1).UtcTicks;
            var localStats = new Dictionary<string, (long sizeBytes, int fileCount, long writeTicks)>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<(string Path, bool Expanded, FastDirStatsOnlyScanner? Scan)>();
            stack.Push((rootPath, false, null));

            while (stack.Count > 0)
            {
                var (curPath, expanded, scan) = stack.Pop();
                if (!expanded)
                {
                    var s = new FastDirStatsOnlyScanner(curPath, nowLimitUtcTicks);
                    s.Run();
                    stack.Push((curPath, true, s));
                    for (int i = 0; i < s.SubDirs.Count; i++)
                    {
                        stack.Push((s.SubDirs[i], false, null));
                    }
                }
                else
                {
                    if (scan == null) continue;
                    long totalBytes = scan.DirectFileBytes;
                    int totalFiles = scan.DirectFileCount;
                    long maxWrite = scan.MaxWriteTicks;

                    for (int i = 0; i < scan.SubDirs.Count; i++)
                    {
                        string subKey = NormalizeSpaceKey(scan.SubDirs[i]);
                        if (localStats.TryGetValue(subKey, out var child))
                        {
                            totalBytes += child.sizeBytes;
                            totalFiles += child.fileCount;
                            if (child.writeTicks > maxWrite && child.writeTicks <= nowLimitUtcTicks)
                            {
                                maxWrite = child.writeTicks;
                            }
                        }
                    }

                    scan.Dispose();

                    if (maxWrite == 0)
                    {
                        try { maxWrite = Directory.GetLastWriteTimeUtc(curPath).Ticks; }
                        catch { maxWrite = DateTime.UtcNow.Ticks; }
                    }

                    string curKey = NormalizeSpaceKey(curPath);
                    localStats[curKey] = (totalBytes, totalFiles, maxWrite);

                    DateTime effectiveDt;
                    if (ExecutionHistoryService.TryGetFolderLastExecution(curPath, out var execTime))
                    {
                        effectiveDt = execTime;
                    }
                    else
                    {
                        try { effectiveDt = new DateTime(maxWrite, DateTimeKind.Utc).ToLocalTime(); }
                        catch { effectiveDt = DateTime.Now; }
                    }
                    targetCache[curKey] = (totalBytes, totalFiles, effectiveDt);
                }
            }
        }

        public static List<string> GetIndexedDriveRoots()
        {
            var roots = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                var key = NormalizeSpaceKey(d.Name);
                if (_spaceIndex.ContainsKey(key))
                    roots.Add(key);
            }
            return roots;
        }

        public static void InvalidateSpaceIndex(string? path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _spaceIndex.Clear();
                return;
            }

            string target = NormalizeSpaceKey(path);
            if (string.IsNullOrEmpty(target)) return;
            string targetWithSlash = target.EndsWith(@"\", StringComparison.Ordinal) ? target : target + @"\";

            foreach (var k in _spaceIndex.Keys.ToList())
            {
                string kWithSlash = k.EndsWith(@"\", StringComparison.Ordinal) ? k : k + @"\";
                bool isSameOrDescendant = k.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                          k.StartsWith(targetWithSlash, StringComparison.OrdinalIgnoreCase);
                bool isAncestor = target.StartsWith(kWithSlash, StringComparison.OrdinalIgnoreCase);
                if (isSameOrDescendant || isAncestor)
                {
                    _spaceIndex.TryRemove(k, out _);
                }
            }
        }

        private sealed class FastDirScanner : FileSystemEnumerator<byte>
        {
            private readonly long _nowLimitUtcTicks;
            private readonly List<IndexedChild> _topFiles = new();
            private long _minTopFileBytes;

            public List<string> SubDirs { get; } = new();
            public long DirectFileBytes { get; private set; }
            public int DirectFileCount { get; private set; }
            public long MaxWriteTicks { get; private set; }

            public FastDirScanner(string directory, long nowLimitUtcTicks)
                : base(directory, SafeTopOpts)
            {
                _nowLimitUtcTicks = nowLimitUtcTicks;
            }

            public void Run()
            {
                try
                {
                    while (MoveNext()) { }
                }
                catch { }
            }

            public IEnumerable<IndexedChild> GetSortedTopFiles(int maxCount)
            {
                if (_topFiles.Count == 0) return Array.Empty<IndexedChild>();
                return _topFiles.OrderByDescending(f => f.SizeBytes).Take(maxCount);
            }

            protected override byte TransformEntry(ref FileSystemEntry entry) => 0;

            protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;

                if (entry.IsDirectory)
                {
                    if (!entry.FileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                    {
                        SubDirs.Add(entry.ToFullPath());
                    }
                    return false;
                }

                long len = entry.Length;
                DirectFileBytes += len;
                DirectFileCount++;

                long lw = entry.LastWriteTimeUtc.UtcTicks;
                long ct = entry.CreationTimeUtc.UtcTicks;
                long eff = (ct > lw && ct <= _nowLimitUtcTicks) ? ct : lw;
                if (eff > MaxWriteTicks && eff <= _nowLimitUtcTicks)
                {
                    MaxWriteTicks = eff;
                }

                if (_topFiles.Count < 80 || len > _minTopFileBytes)
                {
                    _topFiles.Add(new IndexedChild(
                        entry.FileName.ToString(),
                        entry.ToFullPath(),
                        len,
                        1,
                        eff,
                        false
                    ));

                    if (_topFiles.Count >= 160)
                    {
                        _topFiles.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                        _topFiles.RemoveRange(80, _topFiles.Count - 80);
                        _minTopFileBytes = _topFiles[_topFiles.Count - 1].SizeBytes;
                    }
                    else if (_topFiles.Count == 80)
                    {
                        long min = long.MaxValue;
                        for (int i = 0; i < _topFiles.Count; i++)
                        {
                            if (_topFiles[i].SizeBytes < min) min = _topFiles[i].SizeBytes;
                        }
                        _minTopFileBytes = min == long.MaxValue ? 0 : min;
                    }
                }

                return false;
            }
        }

        private static IndexedDirNode? IndexSubtreeBottomUp(string startPath, long nowLimitUtcTicks)
        {
            var stack = new Stack<(string Path, bool Expanded, FastDirScanner? Scan)>();
            stack.Push((startPath, false, null));

            while (stack.Count > 0)
            {
                var (curPath, expanded, scan) = stack.Pop();
                if (!expanded)
                {
                    var s = new FastDirScanner(curPath, nowLimitUtcTicks);
                    s.Run();
                    stack.Push((curPath, true, s));
                    for (int i = 0; i < s.SubDirs.Count; i++)
                    {
                        stack.Push((s.SubDirs[i], false, null));
                    }
                }
                else
                {
                    if (scan == null) continue;
                    long totalBytes = scan.DirectFileBytes;
                    int totalFiles = scan.DirectFileCount;
                    long maxWrite = scan.MaxWriteTicks;

                    var children = new List<IndexedChild>(scan.SubDirs.Count + 80);
                    children.AddRange(scan.GetSortedTopFiles(80));

                    for (int i = 0; i < scan.SubDirs.Count; i++)
                    {
                        string subKey = NormalizeSpaceKey(scan.SubDirs[i]);
                        if (_spaceIndex.TryGetValue(subKey, out var childNode))
                        {
                            totalBytes += childNode.SizeBytes;
                            totalFiles += childNode.FileCount;
                            if (childNode.WriteTicks > maxWrite && childNode.WriteTicks <= nowLimitUtcTicks)
                            {
                                maxWrite = childNode.WriteTicks;
                            }
                            children.Add(new IndexedChild(
                                childNode.Name,
                                childNode.Path,
                                childNode.SizeBytes,
                                childNode.FileCount,
                                childNode.WriteTicks,
                                true
                            ));
                        }
                    }

                    scan.Dispose();

                    if (maxWrite == 0)
                    {
                        try { maxWrite = Directory.GetLastWriteTimeUtc(curPath).Ticks; }
                        catch { maxWrite = DateTime.UtcNow.Ticks; }
                    }

                    var topChildren = children.OrderByDescending(c => c.SizeBytes).Take(80).ToList();
                    string curName = Path.GetFileName(curPath);
                    if (string.IsNullOrEmpty(curName)) curName = curPath;

                    var node = new IndexedDirNode(curName, curPath, totalBytes, totalFiles, maxWrite, topChildren);
                    _spaceIndex[NormalizeSpaceKey(curPath)] = node;
                }
            }

            return _spaceIndex.TryGetValue(NormalizeSpaceKey(startPath), out var result) ? result : null;
        }

        private static FolderNode BuildFolderNodeFromIndex(IndexedDirNode entry)
        {
            var protectedList = GetProtectedItems();
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            var personalDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).TrimEnd('\\'),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos).TrimEnd('\\'),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures).TrimEnd('\\'),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic).TrimEnd('\\'),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory).TrimEnd('\\')
            }.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            bool CheckSanctOrProtected(string p, string n, out bool isProt, out bool isPersonalSub)
            {
                isProt = protectedList.Any(x => (!string.IsNullOrEmpty(p) && x.Path.Equals(p, StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrEmpty(n) && x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
                string pTrim = p.TrimEnd('\\');
                bool isCriticalRoot = pTrim.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                                      pTrim.EndsWith(@"System32", StringComparison.OrdinalIgnoreCase);
                isPersonalSub = personalDirs.Any(pd => pTrim.StartsWith(pd + @"\", StringComparison.OrdinalIgnoreCase));
                if (isPersonalSub) return isCriticalRoot;
                return IsPathSanctuarized(p) || isCriticalRoot;
            }

            string FormatTicks(long utcTicks, string? folderPath = null)
            {
                if (!string.IsNullOrEmpty(folderPath) && ExecutionHistoryService.TryGetFolderLastExecution(folderPath, out var execTime))
                    return execTime.ToString("dd/MM/yyyy");
                if (utcTicks <= 0) return "";
                try { return new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy"); }
                catch { return ""; }
            }

            bool rootSanct = CheckSanctOrProtected(entry.Path, entry.Name, out bool rootProt, out bool rootPersonalSub);
            long totalBytes = entry.SizeBytes;

            var rootNode = new FolderNode
            {
                Name = string.IsNullOrEmpty(entry.Name) ? entry.Path : entry.Name,
                Path = entry.Path,
                SizeBytes = totalBytes,
                SizeGB = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 2),
                SizeMB = Math.Round(totalBytes / 1024.0 / 1024.0, 1),
                IsFolder = true,
                FileCount = entry.FileCount,
                LastModified = FormatTicks(entry.WriteTicks, entry.Path),
                IsSanctuarized = rootSanct,
                IsPersonalSubItem = rootPersonalSub,
                IsProtected = rootProt
            };

            foreach (var c in entry.TopChildren)
            {
                bool sct = CheckSanctOrProtected(c.Path, c.Name, out bool prt, out bool pSub);
                rootNode.Children.Add(new FolderNode
                {
                    Name = c.Name,
                    Path = c.Path,
                    SizeBytes = c.SizeBytes,
                    SizeGB = Math.Round(c.SizeBytes / 1024.0 / 1024.0 / 1024.0, 2),
                    SizeMB = Math.Round(c.SizeBytes / 1024.0 / 1024.0, 1),
                    PctOfParent = totalBytes > 0 ? Math.Round((c.SizeBytes / (double)totalBytes) * 100.0, 1) : 0,
                    IsFolder = c.IsFolder,
                    FileCount = c.FileCount,
                    LastModified = FormatTicks(c.WriteTicks, c.IsFolder ? c.Path : null),
                    IsSanctuarized = sct,
                    IsPersonalSubItem = pSub,
                    IsProtected = prt
                });
            }

            return rootNode;
        }

        public static async Task<FolderNode> ScanFolderTreeAsync(string rootPath, Action<string, int>? progressCallback = null, bool forceRefresh = false)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(rootPath)) rootPath = @"C:\";
                string normalized = NormalizeSpaceKey(rootPath);

                if (forceRefresh)
                {
                    InvalidateSpaceIndex(normalized);
                }
                else if (_spaceIndex.TryGetValue(normalized, out var cached))
                {
                    return BuildFolderNodeFromIndex(cached);
                }

                if (!Directory.Exists(normalized))
                {
                    return new FolderNode
                    {
                        Name = normalized,
                        Path = normalized,
                        IsFolder = true
                    };
                }

                progressCallback?.Invoke($"Lecture de {normalized}...", 5);
                long nowLimitUtcTicks = DateTimeOffset.UtcNow.AddDays(1).UtcTicks;

                using var topScan = new FastDirScanner(normalized, nowLimitUtcTicks);
                topScan.Run();

                int totalDirs = Math.Max(1, topScan.SubDirs.Count);
                int doneDirs = 0;

                if (topScan.SubDirs.Count > 0)
                {
                    Parallel.ForEach(
                        topScan.SubDirs,
                        new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) },
                        sub =>
                        {
                            try
                            {
                                IndexSubtreeBottomUp(sub, nowLimitUtcTicks);
                            }
                            catch { }
                            finally
                            {
                                int cur = System.Threading.Interlocked.Increment(ref doneDirs);
                                int pct = 10 + (int)Math.Round((cur / (double)totalDirs) * 85.0);
                                progressCallback?.Invoke($"Indexation ({cur}/{totalDirs}) : {Path.GetFileName(sub)}", Math.Min(pct, 95));
                            }
                        });
                }

                long totalBytes = topScan.DirectFileBytes;
                int totalFiles = topScan.DirectFileCount;
                long maxWrite = topScan.MaxWriteTicks;

                var children = new List<IndexedChild>(topScan.SubDirs.Count + 80);
                children.AddRange(topScan.GetSortedTopFiles(80));

                for (int i = 0; i < topScan.SubDirs.Count; i++)
                {
                    string subKey = NormalizeSpaceKey(topScan.SubDirs[i]);
                    if (_spaceIndex.TryGetValue(subKey, out var childNode))
                    {
                        totalBytes += childNode.SizeBytes;
                        totalFiles += childNode.FileCount;
                        if (childNode.WriteTicks > maxWrite && childNode.WriteTicks <= nowLimitUtcTicks)
                        {
                            maxWrite = childNode.WriteTicks;
                        }
                        children.Add(new IndexedChild(
                            childNode.Name,
                            childNode.Path,
                            childNode.SizeBytes,
                            childNode.FileCount,
                            childNode.WriteTicks,
                            true
                        ));
                    }
                }

                if (maxWrite == 0)
                {
                    try { maxWrite = Directory.GetLastWriteTimeUtc(normalized).Ticks; }
                    catch { maxWrite = DateTime.UtcNow.Ticks; }
                }

                var topChildren = children.OrderByDescending(c => c.SizeBytes).Take(80).ToList();
                string rootName = Path.GetFileName(normalized);
                if (string.IsNullOrEmpty(rootName)) rootName = normalized;

                var rootEntry = new IndexedDirNode(rootName, normalized, totalBytes, totalFiles, maxWrite, topChildren);
                _spaceIndex[normalized] = rootEntry;

                progressCallback?.Invoke($"Indexation terminée : {normalized}", 100);
                return BuildFolderNodeFromIndex(rootEntry);
            });
        }

        #endregion
    }
}
