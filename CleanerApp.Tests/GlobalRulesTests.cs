using System;
using System.Collections.Generic;
using System.IO;
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
    }
}
