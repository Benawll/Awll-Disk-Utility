using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CleanerApp;

namespace CleanerApp.Tests
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

            var report = await CleanerBackend.ResearchItemWithAIAsync("TestApp", @"C:\Windows\System32\notepad.exe", 10, "fake-key-for-test", handler);
            Assert.True(report.IsAiEnriched);
            Assert.Equal("Jeu d'action", report.Definition);
            Assert.Equal("Epic Games", report.Origin);
            Assert.Equal(5, report.RiskPct);
            Assert.Equal("gemini-2.5-flash-lite", report.AiModelUsed);
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
            Assert.Equal("gemini-2.5-flash", report.AiModelUsed);
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
    }
}
