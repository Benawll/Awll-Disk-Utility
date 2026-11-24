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
                                LastModified = GetFileEffectiveActivityDate(fi).ToString("dd/MM/yyyy"),
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
    }
}