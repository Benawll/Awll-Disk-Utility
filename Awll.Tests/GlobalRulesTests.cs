using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Awll;

namespace Awll.Tests
{
    public class GlobalRulesTests
    {
        [Fact]
        public void IsCoreSystemOrRuntime_IdentifiesJavaAndCoreRuntimes()
        {
            Assert.True(CleanerBackend.IsCoreSystemOrRuntime("Java 17", @"C:\Program Files\Java\jdk-17"));
            Assert.True(CleanerBackend.IsCoreSystemOrRuntime("Visual Studio Code", @"C:\Users\test\AppData\Local\Programs\Microsoft VS Code"));
            Assert.False(CleanerBackend.IsCoreSystemOrRuntime("RandomGame", @"D:\Games\RandomGame"));
        }

        [Fact]
        public void RiskLevelLabel_MapsThresholdsCorrectly()
        {
            Assert.Equal("Quasi Nul", CleanerBackend.GetRiskLevelLabel(0));
            Assert.Equal("Quasi Nul", CleanerBackend.GetRiskLevelLabel(15));
            Assert.Equal("Faible", CleanerBackend.GetRiskLevelLabel(16));
            Assert.Equal("Faible", CleanerBackend.GetRiskLevelLabel(35));
            Assert.Equal("Modéré", CleanerBackend.GetRiskLevelLabel(36));
            Assert.Equal("Modéré", CleanerBackend.GetRiskLevelLabel(65));
            Assert.Equal("Élevé", CleanerBackend.GetRiskLevelLabel(66));
            Assert.Equal("Élevé", CleanerBackend.GetRiskLevelLabel(100));
        }

        [Fact]
        public void NormalizeAuditItemsGlobal_DeduplicatesAndEnforcesRunningAndCore()
        {
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "python" };
            var rec = new List<CleanerItem>
            {
                new CleanerItem { Name = "Python", Path = @"C:\Users\test\AppData\Local\Programs\Python", RiskPct = 5, Description = "Inactif", IsExtra = false },
                new CleanerItem { Name = "Python Dup", Path = @"C:\Users\test\AppData\Local\Programs\Python\", RiskPct = 5, Description = "Dup", IsExtra = false },
                new CleanerItem { Name = "pip cache", Path = @"C:\Users\test\AppData\Local\pip\cache", RiskPct = 0, Description = "Cache", IsExtra = false }
            };
            var extra = new List<CleanerItem>();

            CleanerBackend.NormalizeAuditItemsGlobal(rec, extra, running);

            // pip cache stays recommended at 0%, Python moves to extra as running (>= 55%) and deduplicated
            Assert.Single(rec);
            Assert.Equal("pip cache", rec[0].Name);
            Assert.Single(extra);
            Assert.Equal("Python", extra[0].Name);
            Assert.True(extra[0].RiskPct >= 55);
            Assert.True(extra[0].IsExtra);
        }

        [Fact]
        public void FinalizeResearchReport_BoostsRiskWhenRunningNonCache()
        {
            var report = new ResearchReport { RiskPct = 5, SafetyImpact = "RAS" };
            CleanerBackend.FinalizeResearchReport(report, @"D:\Apps\CustomTool", "CustomTool", true);
            Assert.True(report.RiskPct >= 55);
            Assert.Contains("Processus actif", report.SafetyImpact);
        }

        [Fact]
        public void FinalizeResearchReport_KeepsCacheLowRiskEvenIfAppRunning()
        {
            var report = new ResearchReport { RiskPct = 0, SafetyImpact = "Cache temporaire" };
            CleanerBackend.FinalizeResearchReport(report, @"C:\Users\test\AppData\Local\Temp\MyAppCache", "MyAppCache", true);
            Assert.Equal(0, report.RiskPct);
        }

        [Fact]
        public void FindDuplicateGroupsInDirectories_FindsDuplicatesAcrossMultipleFoldersAndIgnoresUnique()
        {
            var root = Path.Combine(Path.GetTempPath(), "CleanerDupTest_" + Guid.NewGuid().ToString("N"));
            var dirA = Path.Combine(root, "DirA");
            var dirB = Path.Combine(root, "DirB");
            Directory.CreateDirectory(dirA);
            Directory.CreateDirectory(dirB);

            try
            {
                var contentSame = new byte[128 * 1024];
                new Random(42).NextBytes(contentSame);
                var contentDiff = new byte[128 * 1024];
                new Random(99).NextBytes(contentDiff);

                File.WriteAllBytes(Path.Combine(dirA, "iso_copy1.bin"), contentSame);
                File.WriteAllBytes(Path.Combine(dirB, "iso_copy2.bin"), contentSame);
                File.WriteAllBytes(Path.Combine(dirB, "unique_same_size.bin"), contentDiff);

                var groups = CleanerBackend.FindDuplicateGroupsInDirectories(new[] { dirA, dirB }, minBytes: 64 * 1024);

                Assert.Single(groups);
                Assert.Equal(2, groups[0].Files.Count);
                Assert.False(groups[0].Files[0].SuggestedDelete);
                Assert.True(groups[0].Files[1].SuggestedDelete);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void FinalizeResearchReport_PreservesCardInitialRiskWhenSafeAndInactive()
        {
            var report = new ResearchReport { RiskPct = 5, SafetyImpact = "RAS" };
            CleanerBackend.FinalizeResearchReport(report, @"D:\Data\SomeFolder", "SomeFolder", false, initialRisk: 35);
            Assert.Equal(35, report.RiskPct);
        }

        [Fact]
        public void ParseGeminiResearchJson_ParsesMarkdownFencedJsonWithoutPrefix()
        {
            var report = new ResearchReport();
            var raw = "```json\n{\"definition\":\"Outil test\",\"origin\":\"Installateur\",\"usageActivity\":\"Inactif\",\"safetyImpact\":\"SUPPRESSION CONSEILLÉE\",\"riskPct\":10}\n```";
            bool ok = CleanerBackend.ParseGeminiResearchJson(raw, report);
            Assert.True(ok);
            Assert.Equal("Outil test", report.Definition);
            Assert.Equal(10, report.RiskPct);
            Assert.DoesNotContain("[IA Gemini]", report.Definition);
        }

        [Fact]
        public void IsLikelyRunning_DistinguishesSteamVsEpicCopyByExactExePath()
        {
            var runningProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RocketLeague" };
            var exePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"D:\SteamLibrary\steamapps\common\rocketleague\Binaries\Win64\RocketLeague.exe" };
            var resolvedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RocketLeague" };

            Assert.True(CleanerBackend.IsLikelyRunning("Rocket League", @"D:\SteamLibrary\steamapps\common\rocketleague", runningProcs, exePaths, resolvedNames));
            Assert.False(CleanerBackend.IsLikelyRunning("Doublon Cross-Platform : Rocket League (Epic Games)", @"E:\Epic Games\rocketleague", runningProcs, exePaths, resolvedNames));
        }

        [Fact]
        public void IsLikelyRunning_MatchesSpacedFolderNameWhenProcessPathUnresolved()
        {
            var runningProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RocketLeague" };
            var emptyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emptyResolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Assert.True(CleanerBackend.IsLikelyRunning("Rocket League", @"D:\SteamLibrary\steamapps\common\Rocket League", runningProcs, emptyPaths, emptyResolved));
        }

        [Fact]
        public void ExecutionHistoryService_ConvertsDevicePathAndFindsFolderLastExecution()
        {
            var devMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { @"\Device\HarddiskVolume2", "D:" },
                { @"\Device\HarddiskVolume8", "E:" }
            };
            var dos = ExecutionHistoryService.ConvertDevicePathToDosPath(@"\Device\HarddiskVolume2\SteamLibrary\steamapps\common\rocketleague\Binaries\Win64\RocketLeague.exe", devMap);
            Assert.Equal(@"D:\SteamLibrary\steamapps\common\rocketleague\Binaries\Win64\RocketLeague.exe", dos);

            var execMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase)
            {
                { dos, new DateTime(2026, 11, 24, 22, 7, 23) }
            };
            Assert.True(ExecutionHistoryService.TryGetFolderLastExecution(@"D:\SteamLibrary\steamapps\common\rocketleague", out var dt, execMap));
            Assert.Equal(new DateTime(2026, 11, 24, 22, 7, 23), dt);
            Assert.False(ExecutionHistoryService.TryGetFolderLastExecution(@"E:\Epic Games\rocketleague", out _, execMap));
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_responder(request));
        }

        [Fact]
        public async Task ResearchItemWithAIAsync_EnrichesReportOn200()
        {
            var handler = new MockHttpHandler(_ =>
            {
                var geminiPayload = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"definition\\\":\\\"Jeu d'action\\\",\\\"origin\\\":\\\"Epic Games\\\",\\\"usageActivity\\\":\\\"Inactif\\\",\\\"safetyImpact\\\":\\\"SUPPRESSION CONSEILLÉE\\\",\\\"riskPct\\\":5}\"}]}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(geminiPayload, Encoding.UTF8, "application/json")
                };
            });

            var tempFile = Path.Combine(Path.GetTempPath(), "AwllAiTest_" + Guid.NewGuid().ToString("N") + ".dat");
            File.WriteAllBytes(tempFile, new byte[128]);
            try
            {
                var report = await CleanerBackend.ResearchItemWithAIAsync("TestApp", tempFile, 10, "fake-key-for-test", handler);
                Assert.True(report.IsAiEnriched);
                Assert.Equal("Jeu d'action", report.Definition);
                Assert.Equal("Epic Games", report.Origin);
                Assert.Equal(5, report.RiskPct);
                Assert.Equal("gemini-3.8-flash", report.AiModelUsed);
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public async Task ResearchItemWithAIAsync_FallsBackToSecondModelOn503()
        {
            int calls = 0;
            var handler = new MockHttpHandler(_ =>
            {
                calls++;
                if (calls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("{\"error\":{\"message\":\"High demand\"}}", Encoding.UTF8, "application/json")
                    };
                }
                var okPayload = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"definition\\\":\\\"Outil OK\\\",\\\"origin\\\":\\\"Test\\\",\\\"usageActivity\\\":\\\"Inactif\\\",\\\"safetyImpact\\\":\\\"Sûr\\\",\\\"riskPct\\\":0}\"}]}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(okPayload, Encoding.UTF8, "application/json")
                };
            });

            var report = await CleanerBackend.ResearchItemWithAIAsync("TestApp", @"C:\Windows\System32\notepad.exe", 10, "fake-key-for-test", handler);
            Assert.True(report.IsAiEnriched);
            Assert.Equal(2, calls);
            Assert.Equal("gemini-3.7-flash", report.AiModelUsed);
        }

        [Fact]
        public async Task ResearchItemWithAIAsync_ReportsClearErrorWhenAllModels503()
        {
            var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\":{\"message\":\"High demand\"}}", Encoding.UTF8, "application/json")
            });

            var report = await CleanerBackend.ResearchItemWithAIAsync("TestApp", @"C:\Windows\System32\notepad.exe", 10, "fake-key-for-test", handler);
            Assert.False(report.IsAiEnriched);
            Assert.Contains("503", report.AiModelUsed);
        }

        [Fact]
        public void IsRecycleBinPath_MatchesDriveRootsOnly()
        {
            Assert.True(CleanerBackend.IsRecycleBinPath(@"C:\$Recycle.Bin"));
            Assert.True(CleanerBackend.IsRecycleBinPath(@"D:\$Recycle.Bin\"));
            Assert.False(CleanerBackend.IsRecycleBinPath(@"C:\Temp\$Recycle.Bin"));
            Assert.False(CleanerBackend.IsRecycleBinPath(@"C:\$Recycle.Bin\S-1-5-21-123"));
            Assert.False(CleanerBackend.IsRecycleBinPath(""));
        }

        [Fact]
        public void CleanRecycleBinSidDirectory_RemovesOrphanedItemsAndKeepsDesktopIni()
        {
            var sidRoot = Path.Combine(Path.GetTempPath(), "CleanerRbSidTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sidRoot);
            try
            {
                var desktopIni = Path.Combine(sidRoot, "desktop.ini");
                File.WriteAllText(desktopIni, "[.ShellClassInfo]");
                File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);

                var orphanDir = Path.Combine(sidRoot, "$R1TEST");
                Directory.CreateDirectory(orphanDir);
                var orphanFile = Path.Combine(orphanDir, "payload.bin");
                File.WriteAllBytes(orphanFile, new byte[64 * 1024]);
                File.SetAttributes(orphanFile, FileAttributes.ReadOnly | FileAttributes.Hidden);

                var (freedBytes, remainingErrors, _) = CleanerBackend.CleanRecycleBinSidDirectory(sidRoot);

                Assert.Equal(0, remainingErrors);
                Assert.True(freedBytes >= 64 * 1024);
                Assert.True(File.Exists(desktopIni));
                Assert.False(Directory.Exists(orphanDir));
            }
            finally
            {
                try
                {
                    if (File.Exists(Path.Combine(sidRoot, "desktop.ini")))
                        File.SetAttributes(Path.Combine(sidRoot, "desktop.ini"), FileAttributes.Normal);
                    Directory.Delete(sidRoot, true);
                }
                catch { }
            }
        }

        [Fact]
        public async Task ScanFolderTreeAsync_BuildsBottomUpIndexAndServesSubfoldersInstantly()
        {
            var root = Path.Combine(Path.GetTempPath(), "CleanerSpaceLensTest_" + Guid.NewGuid().ToString("N"));
            var subA = Path.Combine(root, "SubA");
            var deep = Path.Combine(subA, "Deep");
            var subB = Path.Combine(root, "SubB");
            Directory.CreateDirectory(deep);
            Directory.CreateDirectory(subB);

            try
            {
                var bigFile = Path.Combine(deep, "big.bin");
                File.WriteAllBytes(bigFile, new byte[128 * 1024]);
                File.WriteAllBytes(Path.Combine(subB, "small.bin"), new byte[16 * 1024]);
                File.WriteAllBytes(Path.Combine(root, "rootfile.bin"), new byte[32 * 1024]);

                var rootNode = await CleanerBackend.ScanFolderTreeAsync(root, forceRefresh: true);

                Assert.Equal((128 + 16 + 32) * 1024L, rootNode.SizeBytes);
                Assert.Equal(3, rootNode.FileCount);
                Assert.Equal(3, rootNode.Children.Count);
                Assert.Equal("SubA", rootNode.Children[0].Name);
                Assert.True(CleanerBackend.IsPathIndexedInSpaceLens(subA));
                Assert.True(CleanerBackend.IsPathIndexedInSpaceLens(deep));

                // Delete bigFile on disk; cached bottom-up lookup should still return 128 KB instantaneously
                File.Delete(bigFile);
                var cachedSubA = await CleanerBackend.ScanFolderTreeAsync(subA, forceRefresh: false);
                Assert.Equal(128 * 1024L, cachedSubA.SizeBytes);

                // Force refresh should re-read disk and report 0 bytes
                var refreshedSubA = await CleanerBackend.ScanFolderTreeAsync(subA, forceRefresh: true);
                Assert.Equal(0L, refreshedSubA.SizeBytes);
            }
            finally
            {
                CleanerBackend.InvalidateSpaceIndex(root);
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void PrepopulateAuditStatsBottomUp_ComputesAllSubfolderStatsInSinglePass()
        {
            var root = Path.Combine(Path.GetTempPath(), "AwllAuditStatsTest_" + Guid.NewGuid().ToString("N"));
            var subA = Path.Combine(root, "SubA");
            var deep = Path.Combine(subA, "Deep");
            var subB = Path.Combine(root, "SubB");
            Directory.CreateDirectory(deep);
            Directory.CreateDirectory(subB);

            try
            {
                File.WriteAllBytes(Path.Combine(deep, "a.bin"), new byte[80 * 1024]);
                File.WriteAllBytes(Path.Combine(subB, "b.bin"), new byte[20 * 1024]);

                var cache = new ConcurrentDictionary<string, (long sizeBytes, int fileCount, DateTime lastWrite)>(StringComparer.OrdinalIgnoreCase);
                CleanerBackend.PrepopulateAuditStatsBottomUp(root, cache);

                Assert.True(cache.TryGetValue(root, out var rootStats));
                Assert.Equal(100 * 1024L, rootStats.sizeBytes);
                Assert.Equal(2, rootStats.fileCount);

                Assert.True(cache.TryGetValue(subA, out var subAStats));
                Assert.Equal(80 * 1024L, subAStats.sizeBytes);
                Assert.Equal(1, subAStats.fileCount);

                Assert.True(cache.TryGetValue(deep, out var deepStats));
                Assert.Equal(80 * 1024L, deepStats.sizeBytes);
                Assert.Equal(1, deepStats.fileCount);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void ParseGeminiResearchJson_ExtractsEmbeddedJsonAndStringRisk()
        {
            var report = new ResearchReport();
            var raw = "Voici l'analyse demandée :\n```json\n{\"Definition\":\"Mod manager\",\"origin\":\"Communauté\",\"usageActivity\":\"Inactif\",\"safetyImpact\":\"SUPPRESSION CONSEILLÉE\",\"riskPct\":\"35%\"}\n```\nBonne journée.";
            bool ok = CleanerBackend.ParseGeminiResearchJson(raw, report);
            Assert.True(ok);
            Assert.Equal("Mod manager", report.Definition);
            Assert.Equal(35, report.RiskPct);
        }

        [Fact]
        public async Task ResearchItemWithAIAsync_SkipsThoughtPartAndPreservesCalibratedRisk()
        {
            var handler = new MockHttpHandler(_ =>
            {
                var payload = "{\"candidates\":[{\"content\":{\"parts\":[{\"thought\":true,\"text\":\"Réflexion interne...\"},{\"text\":\"{\\\"definition\\\":\\\"Logiciel récent\\\",\\\"origin\\\":\\\"Editeur\\\",\\\"usageActivity\\\":\\\"Actif\\\",\\\"safetyImpact\\\":\\\"CONSERVATION RECOMMANDÉE\\\",\\\"riskPct\\\":40}\"}]}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            });

            var tempFile = Path.Combine(Path.GetTempPath(), "AwllAiTest_" + Guid.NewGuid().ToString("N") + ".dat");
            File.WriteAllBytes(tempFile, new byte[128]);
            try
            {
                var report = await CleanerBackend.ResearchItemWithAIAsync("CustomApp", tempFile, 40, "fake-key-for-test", handler);
                Assert.True(report.IsAiEnriched);
                Assert.Equal("Logiciel récent", report.Definition);
                Assert.Equal(40, report.RiskPct);
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public async Task ResearchItemWithAIAsync_EnforcesSafetyFloorForCoreRuntime()
        {
            var handler = new MockHttpHandler(_ =>
            {
                var payload = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"definition\\\":\\\"JDK 17\\\",\\\"origin\\\":\\\"Oracle\\\",\\\"usageActivity\\\":\\\"Runtime\\\",\\\"safetyImpact\\\":\\\"CONSERVATION RECOMMANDÉE\\\",\\\"riskPct\\\":0}\"}]}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            });

            var report = await CleanerBackend.ResearchItemWithAIAsync("Java 17", @"C:\Program Files\Java\jdk-17", 90, "fake-key-for-test", handler);
            Assert.True(report.IsAiEnriched);
            Assert.True(report.RiskPct >= 85);
        }

        [Fact]
        public void ParseGeminiDuplicateAnalysisJson_EnforcesSafetyAndNeverDeletesAll()
        {
            var group = new DuplicateGroup
            {
                Hash = "ABC123",
                SizeMB = 120,
                Files = new List<DuplicateFileItem>
                {
                    new DuplicateFileItem { Name = "app.dll", Path = @"C:\Apps\ToolA\app.dll", Drive = "C:", IsProtected = true },
                    new DuplicateFileItem { Name = "app.dll", Path = @"D:\Backup\app.dll", Drive = "D:" },
                    new DuplicateFileItem { Name = "app.dll", Path = @"C:\Users\test\Downloads\app.dll", Drive = "C:" }
                }
            };

            var report = new DuplicateGroupAiReport { GroupHash = "ABC123" };
            var raw = "```json\n{\"summary\":\"Copies d'une DLL applicative.\",\"recommendationType\":\"safe_clean\",\"riskPct\":5,\"files\":[{\"index\":0,\"action\":\"delete\",\"reason\":\"Copie A\"},{\"index\":1,\"action\":\"delete\",\"reason\":\"Copie B\"},{\"index\":2,\"action\":\"delete\",\"reason\":\"Copie C\"}]}\n```";

            bool ok = CleanerBackend.ParseGeminiDuplicateAnalysisJson(raw, group, report);
            Assert.True(ok);
            Assert.Equal("keep", report.Decisions[0].Action); // Protected forced to keep
            Assert.Equal("delete", report.Decisions[1].Action);
            Assert.Equal("delete", report.Decisions[2].Action);
            Assert.Equal("safe_clean", report.RecommendationType);

            // Test all-delete fallback with non-protected files
            var groupUnprotected = new DuplicateGroup
            {
                Hash = "DEF456",
                SizeMB = 200,
                Files = new List<DuplicateFileItem>
                {
                    new DuplicateFileItem { Name = "video.mov", Path = @"D:\Videos\video.mov", Drive = "D:" },
                    new DuplicateFileItem { Name = "video.mov", Path = @"E:\Backup\video.mov", Drive = "E:" }
                }
            };
            var report2 = new DuplicateGroupAiReport { GroupHash = "DEF456" };
            var rawAllDel = "{\"summary\":\"Vidéo dupliquée.\",\"recommendationType\":\"safe_clean\",\"riskPct\":10,\"files\":[{\"index\":0,\"action\":\"delete\",\"reason\":\"A\"},{\"index\":1,\"action\":\"delete\",\"reason\":\"B\"}]}";
            bool ok2 = CleanerBackend.ParseGeminiDuplicateAnalysisJson(rawAllDel, groupUnprotected, report2);
            Assert.True(ok2);
            Assert.Equal("keep", report2.Decisions[0].Action);
            Assert.Equal("delete", report2.Decisions[1].Action);
            Assert.Equal("safe_clean", report2.RecommendationType);
        }

        [Fact]
        public async Task AnalyzeDuplicateGroupWithAIAsync_ReturnsKeepAllForSoftwareCopiesOn200()
        {
            var group = new DuplicateGroup
            {
                Hash = "SW999",
                SizeMB = 85,
                Files = new List<DuplicateFileItem>
                {
                    new DuplicateFileItem { Name = "ffmpeg.dll", Path = @"C:\Program Files\AppA\ffmpeg.dll", Drive = "C:" },
                    new DuplicateFileItem { Name = "ffmpeg.dll", Path = @"C:\Program Files\AppB\ffmpeg.dll", Drive = "C:" }
                }
            };

            var handler = new MockHttpHandler(_ =>
            {
                var payload = "{\"candidates\":[{\"content\":{\"parts\":[{\"thought\":true,\"text\":\"Analyse...\"},{\"text\":\"{\\\"summary\\\":\\\"Bibliothèque partagée requise par deux logiciels distincts.\\\",\\\"recommendationType\\\":\\\"keep_all\\\",\\\"riskPct\\\":20,\\\"files\\\":[{\\\"index\\\":0,\\\"action\\\":\\\"keep\\\",\\\"reason\\\":\\\"Requis par AppA.\\\"},{\\\"index\\\":1,\\\"action\\\":\\\"keep\\\",\\\"reason\\\":\\\"Requis par AppB.\\\"}]}\"}]}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            });

            var report = await CleanerBackend.AnalyzeDuplicateGroupWithAIAsync(group, "fake-key-for-test", handler);
            Assert.True(report.IsAiEnriched);
            Assert.Equal("keep_all", report.RecommendationType);
            Assert.True(report.RiskPct >= 65);
            Assert.Equal(2, report.Decisions.Count);
            Assert.All(report.Decisions, d => Assert.Equal("keep", d.Action));
        }
    }
}
