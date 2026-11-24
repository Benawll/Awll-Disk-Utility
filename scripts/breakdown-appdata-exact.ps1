# Exact itemized breakdown of all folders in AppData (Local, Roaming, LocalLow) and User profile
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. TOUS LES DOSSIERS DANS C:\Users\benja\AppData\Local (> 150 Mo) ===" -ForegroundColor Cyan
$localDirs = Get-ChildItem "C:\Users\benja\AppData\Local" -Directory -Force
$resLocal = foreach ($d in $localDirs) {
    $bytes = (Get-ChildItem $d.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 150MB) {
        [PSCustomObject]@{
            Dossier = "AppData\Local\$($d.Name)"
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resLocal | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)" | Format-Table -AutoSize

Write-Host "`n=== 2. TOUS LES DOSSIERS DANS C:\Users\benja\AppData\Roaming (> 150 Mo) ===" -ForegroundColor Cyan
$roamDirs = Get-ChildItem "C:\Users\benja\AppData\Roaming" -Directory -Force
$resRoam = foreach ($d in $roamDirs) {
    $bytes = (Get-ChildItem $d.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 150MB) {
        [PSCustomObject]@{
            Dossier = "AppData\Roaming\$($d.Name)"
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resRoam | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)" | Format-Table -AutoSize

Write-Host "`n=== 3. TOUS LES DOSSIERS DANS C:\Users\benja\AppData\LocalLow (> 100 Mo) ===" -ForegroundColor Cyan
$lowDirs = Get-ChildItem "C:\Users\benja\AppData\LocalLow" -Directory -Force
$resLow = foreach ($d in $lowDirs) {
    $bytes = (Get-ChildItem $d.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 100MB) {
        [PSCustomObject]@{
            Dossier = "AppData\LocalLow\$($d.Name)"
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resLow | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)" | Format-Table -AutoSize

Write-Host "`n=== 4. AUTRES DOSSIERS PROFIL C:\Users\benja (> 100 Mo, hors Videos, Downloads, New Doc) ===" -ForegroundColor Cyan
$userDirs = Get-ChildItem "C:\Users\benja" -Directory -Force | Where-Object { 
    $_.Name -notmatch '^(AppData|Videos|Downloads|CrossDevice)$' 
}
$resUser = foreach ($d in $userDirs) {
    $bytes = (Get-ChildItem $d.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 100MB) {
        [PSCustomObject]@{
            Dossier = "Users\benja\$($d.Name)"
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resUser | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)" | Format-Table -AutoSize
