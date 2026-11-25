using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Awll
{
    public static partial class CleanerBackend
    {
        private static string GetAuditSnapshotsFilePath()
        {
            var folder = GetAppDataDir();
            return Path.Combine(folder, "audit_snapshots.json");
        }

        public static List<AuditSnapshot> GetAuditSnapshots()
        {
            try
            {
                var file = GetAuditSnapshotsFilePath();
                if (!File.Exists(file)) return new List<AuditSnapshot>();
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<List<AuditSnapshot>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AuditSnapshot>();
            }
            catch { return new List<AuditSnapshot>(); }
        }

        public static void SaveAuditSnapshot(List<CleanerItem> recommendedItems)
        {
            try
            {
                var drives = GetDrivesStatus();
                var snap = new AuditSnapshot
                {
                    Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    RecommendedCount = recommendedItems.Count,
                    RecommendedGB = Math.Round(recommendedItems.Sum(x => x.SizeGB), 2),
                    Drives = drives.Select(d => new AuditDriveSnapshot
                    {
                        Drive = d.Name,
                        FreeGB = d.FreeGB,
                        UsedGB = d.UsedGB
                    }).ToList()
                };
                var list = GetAuditSnapshots();
                list.Insert(0, snap);
                if (list.Count > 15) list = list.Take(15).ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetAuditSnapshotsFilePath(), json);
            }
            catch { }
        }
    }
}
