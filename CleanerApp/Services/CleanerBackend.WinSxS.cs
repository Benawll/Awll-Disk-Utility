using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CleanerApp
{
    public static partial class CleanerBackend
    {
        public static Task<WinSxSReport> AnalyzeWinSxSAsync()
        {
            return Task.Run(() =>
            {
                var report = new WinSxSReport();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = "/Online /Cleanup-Image /AnalyzeComponentStore",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        report.Message = "Impossible de lancer DISM.";
                        return report;
                    }

                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(30000))
                    {
                        try { proc.Kill(); } catch { }
                        report.Message = "Délai dépassé lors de l'analyse DISM WinSxS.";
                        return report;
                    }

                    string combined = (stdout + "\n" + stderr).Trim();
                    if (proc.ExitCode == 740 || combined.Contains("740", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("elevated", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("privilèges", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("administrateur", StringComparison.OrdinalIgnoreCase))
                    {
                        report.RequiresAdmin = true;
                        report.Message = "Droits administrateur requis pour analyser le magasin de composants WinSxS via DISM.";
                        report.RawSummary = combined;
                        return report;
                    }

                    report.Success = proc.ExitCode == 0;
                    report.RawSummary = combined;

                    foreach (var line in combined.Split('\n'))
                    {
                        var l = line.Trim();
                        if (l.Contains("Actual Size of Component Store", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("Taille réelle du magasin de composants", StringComparison.OrdinalIgnoreCase))
                        {
                            report.ActualSize = ExtractColonValue(l);
                        }
                        else if (l.Contains("Backups and Disabled Features", StringComparison.OrdinalIgnoreCase) ||
                                 l.Contains("Sauvegardes et fonctionnalités désactivées", StringComparison.OrdinalIgnoreCase))
                        {
                            report.BackupsAndDisabled = ExtractColonValue(l);
                        }
                        else if (l.Contains("Reclaimable Packages", StringComparison.OrdinalIgnoreCase) ||
                                 l.Contains("Packages récupérables", StringComparison.OrdinalIgnoreCase))
                        {
                            report.ReclaimablePackages = ExtractColonValue(l);
                        }
                        else if (l.Contains("Component Store Cleanup Recommended", StringComparison.OrdinalIgnoreCase) ||
                                 l.Contains("Nettoyage du magasin de composants recommandé", StringComparison.OrdinalIgnoreCase))
                        {
                            var val = ExtractColonValue(l);
                            report.CleanupRecommended = val.Contains("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                        val.Contains("Oui", StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    report.Message = report.Success
                        ? (report.CleanupRecommended
                            ? "Nettoyage WinSxS recommandé par Windows (lecture seule, sans modification)."
                            : "Magasin WinSxS sain : aucun nettoyage urgent recommandé.")
                        : $"DISM terminé avec le code {proc.ExitCode}.";
                }
                catch (Exception ex)
                {
                    report.Message = $"Erreur DISM : {ex.Message}";
                }
                return report;
            });
        }

        private static string ExtractColonValue(string line)
        {
            int idx = line.IndexOf(':');
            return idx >= 0 && idx + 1 < line.Length ? line.Substring(idx + 1).Trim() : line;
        }
    }
}
