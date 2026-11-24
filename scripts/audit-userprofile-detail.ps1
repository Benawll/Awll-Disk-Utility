# Audit 1: Detailed inspection of C:\Users\benja (.cache, .jdks, small user dirs, AppData)
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DETAIL DE C:\Users\benja\.cache ===" -ForegroundColor Cyan
if (Test-Path "$env:USERPROFILE\.cache") {
    Get-ChildItem "$env:USERPROFILE\.cache" -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{ "SousDossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "Chemin" = $_.FullName }
    } | Sort-Object TailleMo -Descending | Format-Table -AutoSize
}

Write-Host "`n=== 2. DETAIL DE C:\Users\benja\.jdks ===" -ForegroundColor Cyan
if (Test-Path "$env:USERPROFILE\.jdks") {
    Get-ChildItem "$env:USERPROFILE\.jdks" -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{ "JDK" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "Chemin" = $_.FullName }
    } | Sort-Object TailleMo -Descending | Format-Table -AutoSize
}

Write-Host "`n=== 3. TOUS LES AUTRES DOSSIERS DE C:\Users\benja (< 5 Go) ===" -ForegroundColor Cyan
Get-ChildItem "$env:USERPROFILE" -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -notin @("Videos", "AppData", "CrossDevice", "Downloads", ".lunarclient", "curseforge")
} | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    [PSCustomObject]@{ "Dossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "TailleGo" = [math]::Round($sz / 1024, 2); "Chemin" = $_.FullName }
} | Where-Object { $_.TailleMo -gt 50 } | Sort-Object TailleMo -Descending | Format-Table -AutoSize

Write-Host "`n=== 4. DETAIL DE C:\Users\benja\AppData (Top 25 dossiers dans Local, Roaming, LocalLow) ===" -ForegroundColor Cyan
$appdataPaths = @("$env:LOCALAPPDATA", "$env:APPDATA", "$env:USERPROFILE\AppData\LocalLow")
$appdataList = foreach ($ap in $appdataPaths) {
    Get-ChildItem $ap -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{ "Branche" = (Split-Path $ap -Leaf); "Dossier" = $_.Name; "TailleMo" = [math]::Round($sz, 1); "TailleGo" = [math]::Round($sz / 1024, 2); "Chemin" = $_.FullName }
    }
}
$appdataList | Where-Object { $_.TailleMo -gt 200 } | Sort-Object TailleMo -Descending | Select-Object -First 25 | Format-Table -AutoSize
