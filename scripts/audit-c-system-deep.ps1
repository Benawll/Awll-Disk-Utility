# Exhaustive scan of C:\Program Files, C:\Program Files (x86), C:\ProgramData, WindowsApps and Root C:\
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DOSSIERS DANS C:\Program Files (> 300 Mo) ===" -ForegroundColor Cyan
$pfFolders = Get-ChildItem "C:\Program Files" -Directory -Force | Where-Object { $_.Name -notmatch 'WindowsApps' }
$resPF = foreach ($dir in $pfFolders) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 300MB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            Chemin  = $dir.FullName
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
            RawBytes = $bytes
        }
    }
}
$resPF | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 2. DOSSIERS DANS C:\Program Files (x86) (> 200 Mo) ===" -ForegroundColor Cyan
$pfx86Folders = Get-ChildItem "C:\Program Files (x86)" -Directory -Force
$resPFx86 = foreach ($dir in $pfx86Folders) {
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
$resPFx86 | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 3. DOSSIERS DANS C:\ProgramData (> 200 Mo) ===" -ForegroundColor Cyan
$pdFolders = Get-ChildItem "C:\ProgramData" -Directory -Force
$resPD = foreach ($dir in $pdFolders) {
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
$resPD | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 4. DOSSIERS RACINE ATYPIQUES SUR C:\ (> 100 Mo) ===" -ForegroundColor Cyan
$rootFolders = Get-ChildItem "C:\" -Directory -Force | Where-Object { 
    $_.Name -notmatch '^(Windows|Program Files|Program Files \(x86\)|ProgramData|Users|New Doc|\$Recycle\.Bin|System Volume Information|Recovery)$' 
}
$resRoot = foreach ($dir in $rootFolders) {
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
$resRoot | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 5. PACKAGES WINDOWSAPPS VOLUMINEUX (> 500 Mo) ===" -ForegroundColor Cyan
Get-AppxPackage -AllUsers | ForEach-Object {
    if ($_.InstallLocation -and (Test-Path $_.InstallLocation)) {
        $files = Get-ChildItem $_.InstallLocation -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        if ($bytes -gt 500MB) {
            [PSCustomObject]@{
                Nom = $_.Name
                "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
                "Taille (Mo)" = [math]::Round($bytes / 1MB, 1)
                Emplacement = $_.InstallLocation
                RawBytes = $bytes
            }
        }
    }
} | Sort-Object RawBytes -Descending | Select-Object Nom, "Taille (Go)", "Taille (Mo)", Emplacement | Format-Table -AutoSize
