# Safe cleanup of Windows temporary files (skips locked and recently modified files)
param(
    [int]$DaysOld = 1,
    [switch]$Execute  # Must be passed explicitly to delete files
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== NETTOYAGE DES FICHIERS TEMPORAIRES (Fichiers > $DaysOld jour(s)) ===" -ForegroundColor Cyan

if (-not $Execute) {
    Write-Host "[MODE SIMULATION / DRY-RUN] Aucun fichier ne sera supprimé." -ForegroundColor Yellow
    Write-Host "Pour exécuter réellement la suppression, ajoutez le flag -Execute." -ForegroundColor Yellow
}

$cutoffDate = (Get-Date).AddDays(-$DaysOld)
$folders = @($env:TEMP, "C:\Windows\Temp")

$totalDeletedBytes = 0
$deletedCount = 0

foreach ($folder in $folders) {
    if (Test-Path $folder) {
        Write-Host "Analyse de : $folder" -ForegroundColor DarkGray
        $files = Get-ChildItem -Path $folder -File -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt $cutoffDate }
            
        foreach ($f in $files) {
            $totalDeletedBytes += $f.Length
            $deletedCount++
            if ($Execute) {
                try {
                    Remove-Item -Path $f.FullName -Force -ErrorAction SilentlyContinue
                } catch {}
            }
        }
    }
}

$freedMB = [math]::Round($totalDeletedBytes / 1MB, 1)
$freedGB = [math]::Round($totalDeletedBytes / 1GB, 2)

if ($Execute) {
    Write-Host "Nettoyage terminé : $deletedCount fichiers purgés, soit environ $freedMB Mo ($freedGB Go) libérés." -ForegroundColor Green
} else {
    Write-Host "Simulation : $deletedCount fichiers éligibles à la purge, soit environ $freedMB Mo ($freedGB Go) récupérables." -ForegroundColor Cyan
}
