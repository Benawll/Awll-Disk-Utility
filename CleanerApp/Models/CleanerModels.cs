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
    public class DriveStatus
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
        public double PctUsed { get; set; }
    }

    public class AppSettings
    {
        public string GeminiApiKey { get; set; } = "";
        public bool EnableAI { get; set; } = true;
    }

    public class CleanerItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Category { get; set; } = ""; // games, system, downloads, apps
        public string Drive { get; set; } = "";
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public int RiskPct { get; set; }
        public string RiskLevel { get; set; } = "Quasi Nul";
        public string Description { get; set; } = "";
        public string LastModified { get; set; } = "";
        public long LastModifiedTimestamp { get; set; }
        public int AgeDays { get; set; }
        public string UninstallString { get; set; } = "";
        public bool IsFolder { get; set; }
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
        public bool IsExtra { get; set; }
    }

    public class CleanHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        public int ItemsCount { get; set; }
        public double FreedGB { get; set; }
        public List<string> ItemNames { get; set; } = new();
    }

    public class ProtectedItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Category { get; set; } = "";
        public string Drive { get; set; } = "";
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public string ProtectedDate { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        public string Description { get; set; } = "";
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
        public bool IsAiEnriched { get; set; }
        public string AiModelUsed { get; set; } = "";
        public double CachedSizeGB { get; set; }
        public long CachedAtUnixMs { get; set; }
    }

    public class CleanResult
    {
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public double FreedGB { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class FolderNode
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public double PctOfParent { get; set; }
        public bool IsFolder { get; set; }
        public int FileCount { get; set; }
        public string LastModified { get; set; } = "";
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
        public List<FolderNode> Children { get; set; } = new();
    }

    public class DuplicateFileItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Drive { get; set; } = "";
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public string LastModified { get; set; } = "";
        public bool IsSanctuarized { get; set; }
        public bool IsProtected { get; set; }
        public bool SuggestedDelete { get; set; }
    }

    public class DuplicateGroup
    {
        public string Hash { get; set; } = "";
        public long SizeBytes { get; set; }
        public double SizeGB { get; set; }
        public double SizeMB { get; set; }
        public double ReclaimableGB { get; set; }
        public double ReclaimableMB { get; set; }
        public List<DuplicateFileItem> Files { get; set; } = new();
    }

    public class WinSxSReport
    {
        public bool Success { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool CleanupRecommended { get; set; }
        public string ActualSize { get; set; } = "";
        public string ReclaimablePackages { get; set; } = "";
        public string BackupsAndDisabled { get; set; } = "";
        public string RawSummary { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class AuditDriveSnapshot
    {
        public string Drive { get; set; } = "";
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
    }

    public class AuditSnapshot
    {
        public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        public long TimestampMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public int RecommendedCount { get; set; }
        public double RecommendedGB { get; set; }
        public List<AuditDriveSnapshot> Drives { get; set; } = new();
    }
}