# Audit 2: Detailed inspection of Program Files, Program Files (x86), ProgramData and Root directories
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DETAIL DES 'AUTRES' DANS C:\Program Files (x86) ===" -ForegroundColor Cyan
Get-ChildItem "C:\Program Files (x86)" -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -notin @("Steam", "Microsoft", "Epic Games", "Battle.net", "Google", "ASUS")
} | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    [PSCustomObject]@{ "Dossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "Chemin" = $_.FullName }
} | Where-Object { $_.TailleMo -gt 10 } | Sort-Object TailleMo -Descending | Format-Table -AutoSize

Write-Host "`n=== 2. DETAIL DES 'AUTRES' ET SOUS-DOSSIERS DANS C:\Program Files ===" -ForegroundColor Cyan
Get-ChildItem "C:\Program Files" -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -notin @("WindowsApps", "Microsoft Office", "PyCharm 2026.2.0.1")
} | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    [PSCustomObject]@{ "Dossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "Chemin" = $_.FullName }
} | Where-Object { $_.TailleMo -gt 20 } | Sort-Object TailleMo -Descending | Format-Table -AutoSize

Write-Host "`n=== 3. DETAIL DES PACKAGES DANS WindowsApps (Top 15) ===" -ForegroundColor Cyan
if (Test-Path "C:\Program Files\WindowsApps") {
    Get-ChildItem "C:\Program Files\WindowsApps" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{ "Package" = $_.Name; "TailleMo" = [math]::Round($sz, 1) }
    } | Where-Object { $_.TailleMo -gt 200 } | Sort-Object TailleMo -Descending | Select-Object -First 15 | Format-Table -AutoSize
}

Write-Host "`n=== 4. DETAIL DE C:\ProgramData ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    [PSCustomObject]@{ "Dossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "Chemin" = $_.FullName }
} | Where-Object { $_.TailleMo -gt 50 } | Sort-Object TailleMo -Descending | Format-Table -AutoSize

Write-Host "`n=== 5. DETAIL DES PETITS DOSSIERS RACINE DE C:\ ===" -ForegroundColor Cyan
$rootTargets = @("C:\AlphaAntiLeak", "C:\AMD", "C:\Jts", "C:\cygwin64", "C:\iMyfone", "C:\OneDriveTemp")
$rootRes = foreach ($rt in $rootTargets) {
    if (Test-Path $rt) {
        $sz = (Get-ChildItem $rt -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        $files = Get-ChildItem $rt -Force -ErrorAction SilentlyContinue | Select-Object -First 5
        [PSCustomObject]@{ "DossierRacine" = $rt; "TailleMo" = [math]::Round($sz, 1); "ExemplesFichiers" = ($files.Name -join ', ') }
    }
}
$rootRes | Format-Table -AutoSize
