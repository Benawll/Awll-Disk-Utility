# Exhaustive deep scan of C: drive candidates
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DOSSIERS VOLUMINEUX DANS C:\Users\benja\AppData\Local (> 200 Mo) ===" -ForegroundColor Cyan
$localFolders = Get-ChildItem "C:\Users\benja\AppData\Local" -Directory -Force
$resLocal = foreach ($dir in $localFolders) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 200MB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resLocal | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 2. DOSSIERS VOLUMINEUX DANS C:\Users\benja\AppData\Roaming (> 200 Mo) ===" -ForegroundColor Cyan
$roamingFolders = Get-ChildItem "C:\Users\benja\AppData\Roaming" -Directory -Force
$resRoaming = foreach ($dir in $roamingFolders) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 200MB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resRoaming | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 3. DOSSIERS VOLUMINEUX DANS C:\Users\benja\AppData\LocalLow (> 100 Mo) ===" -ForegroundColor Cyan
$lowFolders = Get-ChildItem "C:\Users\benja\AppData\LocalLow" -Directory -Force
$resLow = foreach ($dir in $lowFolders) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 100MB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resLow | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 4. DOSSIERS CACHÉS ET PROFIL C:\Users\benja (> 100 Mo) ===" -ForegroundColor Cyan
$userFolders = Get-ChildItem "C:\Users\benja" -Directory -Force | Where-Object { $_.Name -notmatch '^(AppData|Videos|Documents|Pictures|Music|Desktop|Downloads)$' }
$resUser = foreach ($dir in $userFolders) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 100MB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resUser | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize
