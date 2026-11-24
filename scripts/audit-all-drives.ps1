# Full audit of drives C:, D:, E:
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. ÉTAT GLOBAL DES LECTEURS C:, D:, E: ===" -ForegroundColor Cyan
$drives = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {
    $totalGB = [math]::Round($_.Size / 1GB, 2)
    $freeGB = [math]::Round($_.FreeSpace / 1GB, 2)
    $usedGB = [math]::Round($totalGB - $freeGB, 2)
    $pctUsed = if ($totalGB -gt 0) { [math]::Round(($usedGB / $totalGB) * 100, 1) } else { 0 }
    [PSCustomObject]@{
        Lecteur     = $_.DeviceID
        "Nom Volume"= $_.VolumeName
        "Total (Go)"= $totalGB
        "Libre (Go)"= $freeGB
        "Utilisé (Go)" = $usedGB
        "% Utilisé" = "$pctUsed %"
    }
}
$drives | Format-Table -AutoSize

Write-Host "`n=== 2. ARBORESCENCE ET DOSSIERS SUR LE DISQUE D:\ ===" -ForegroundColor Cyan
if (Test-Path "D:\") {
    $dDirs = Get-ChildItem "D:\" -Directory -Force | Where-Object { $_.Name -notmatch '^(\$Recycle\.Bin|System Volume Information)$' }
    $resD = foreach ($dir in $dDirs) {
        $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
    $resD | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize
}

Write-Host "`n=== 3. ARBORESCENCE ET DOSSIERS SUR LE DISQUE E:\ ===" -ForegroundColor Cyan
if (Test-Path "E:\") {
    $eDirs = Get-ChildItem "E:\" -Directory -Force | Where-Object { $_.Name -notmatch '^(\$Recycle\.Bin|System Volume Information)$' }
    $resE = foreach ($dir in $eDirs) {
        $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
    $resE | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize
}
