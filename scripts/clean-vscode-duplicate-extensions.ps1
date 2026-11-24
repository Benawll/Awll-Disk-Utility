# Target: Clean duplicate older versions of VS Code extensions
$ErrorActionPreference = "SilentlyContinue"

$extDir = "$env:USERPROFILE\.vscode\extensions"
if (-not (Test-Path $extDir)) {
    Write-Host "Dossier extensions VS Code introuvable." -ForegroundColor Red
    return
}

Write-Host "Analyse des extensions VS Code dans : $extDir..." -ForegroundColor Cyan

$folders = Get-ChildItem -Path $extDir -Directory

# Parse extension name and version
# Pattern: <publisher>.<name>-<version>
$parsed = foreach ($f in $folders) {
    if ($f.Name -match '^(.+)-([0-9]+\.[0-9].*)$') {
        [PSCustomObject]@{
            "BaseName"   = $matches[1]
            "VersionStr" = $matches[2]
            "Folder"     = $f
            "FullName"   = $f.FullName
            "LastWrite"  = $f.LastWriteTime
        }
    }
}

$groups = $parsed | Group-Object BaseName | Where-Object { $_.Count -gt 1 }

$totalFreedBytes = 0
$cleanedCount = 0

foreach ($grp in $groups) {
    # Sort by LastWriteTime descending so first is latest
    $sorted = $grp.Group | Sort-Object LastWrite -Descending
    $keep = $sorted[0]
    $toDelete = $sorted[1..($sorted.Count - 1)]
    
    Write-Host "`nDoublons détectés pour : $($grp.Name)" -ForegroundColor Yellow
    Write-Host "-> Conservé (le plus récent) : $($keep.Folder.Name) ($($keep.LastWrite.ToString('yyyy-MM-dd')))" -ForegroundColor Green
    
    foreach ($old in $toDelete) {
        $files = Get-ChildItem -Path $old.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $mb = [math]::Round($bytes / 1MB, 1)
        Write-Host "-> Suppression de l'ancienne version : $($old.Folder.Name) ($mb Mo)..." -ForegroundColor DarkGray
        Remove-Item -Path $old.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $totalFreedBytes += $bytes
        $cleanedCount++
    }
}

$totMB = [math]::Round($totalFreedBytes / 1MB, 1)
Write-Host "`nNettoyage terminé : $cleanedCount anciennes versions d'extensions purgées, $totMB Mo libérés !" -ForegroundColor Green
