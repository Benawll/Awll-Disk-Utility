# Scan Windows temporary folders, recycle bin, update cache, and crash dumps
param()

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== SCAN DES FICHIERS TEMPORAIRES ET SYSTÈME WINDOWS ===" -ForegroundColor Cyan

$tempTargets = @(
    @{ Name = "Dossier Temp Utilisateur (%TEMP%)"; Path = $env:TEMP },
    @{ Name = "Dossier Temp Windows (C:\Windows\Temp)"; Path = "C:\Windows\Temp" },
    @{ Name = "Téléchargements Windows Update"; Path = "C:\Windows\SoftwareDistribution\Download" },
    @{ Name = "Rapports de plantage (CrashDumps)"; Path = "$env:LOCALAPPDATA\CrashDumps" },
    @{ Name = "Minidumps Windows"; Path = "C:\Windows\Minidump" },
    @{ Name = "Cache Explorateur / Miniatures"; Path = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer" },
    @{ Name = "Fichiers temporaires Internet (Edge)"; Path = "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache" },
    @{ Name = "Fichiers temporaires Internet (Chrome)"; Path = "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache" }
)

$results = foreach ($t in $tempTargets) {
    if (Test-Path $t.Path) {
        $measure = Get-ChildItem -Path $t.Path -File -Recurse -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum
            
        $sumBytes = if ($null -ne $measure.Sum) { $measure.Sum } else { 0 }
        
        [PSCustomObject]@{
            "Emplacement" = $t.Name
            "Taille (Mo)" = [math]::Round($sumBytes / 1MB, 1)
            "Taille (Go)" = [math]::Round($sumBytes / 1GB, 2)
            "Fichiers"    = $measure.Count
            "Chemin"      = $t.Path
            "RawBytes"    = $sumBytes
        }
    }
}

$results | Sort-Object RawBytes -Descending | ForEach-Object {
    [PSCustomObject]@{
        "Emplacement" = $_.Emplacement
        "Taille (Mo)" = "$($_."Taille (Mo)") Mo"
        "Taille (Go)" = "$($_."Taille (Go)") Go"
        "Fichiers"    = $_.Fichiers
        "Chemin"      = $_.Chemin
    }
} | Format-Table -AutoSize

# Check Recycle Bin size across all drives
Write-Host "`n=== CORBEILLE (Recycle Bin) ===" -ForegroundColor Yellow
try {
    $shell = New-Object -ComObject Shell.Application
    $recycleBin = $shell.Namespace(0xa)
    $items = $recycleBin.Items()
    $totalRecycleBytes = 0
    foreach ($item in $items) {
        $totalRecycleBytes += $item.Size
    }
    $rbMB = [math]::Round($totalRecycleBytes / 1MB, 1)
    $rbGB = [math]::Round($totalRecycleBytes / 1GB, 2)
    Write-Host "Taille totale dans la corbeille : $rbGB Go ($rbMB Mo) pour $($items.Count) éléments." -ForegroundColor Green
} catch {
    Write-Host "Impossible de sonder la corbeille via l'objet Shell." -ForegroundColor Gray
}
