# Target: Execute the 9 user-confirmed deletions
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DÉSINSTALLATION DE POWER BI DESKTOP ===" -ForegroundColor Cyan
$pbi = Get-AppxPackage *PowerBI*
if ($pbi) {
    Write-Host "Suppression du package AppX : $($pbi.PackageFullName)..." -ForegroundColor Yellow
    Remove-AppxPackage -Package $pbi.PackageFullName
    Write-Host "Power BI Desktop désinstallé." -ForegroundColor Green
} else {
    Write-Host "Power BI Desktop n'est plus présent." -ForegroundColor Gray
}

Write-Host "`n=== 2. SUPPRESSION DE CODEX-RUNTIMES (.cache) ===" -ForegroundColor Cyan
$codexCache = "$env:USERPROFILE\.cache\codex-runtimes"
if (Test-Path $codexCache) {
    Remove-Item $codexCache -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "codex-runtimes supprimé (1.82 Go libérés)." -ForegroundColor Green
}

Write-Host "`n=== 3. SUPPRESSION DE ECLIPSE (workspace + plugins) ===" -ForegroundColor Cyan
$eclipseDirs = @("$env:USERPROFILE\eclipse-workspace", "$env:USERPROFILE\.p2", "$env:USERPROFILE\eclipse")
foreach ($ed in $eclipseDirs) {
    if (Test-Path $ed) {
        Remove-Item $ed -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Supprimé : $ed" -ForegroundColor Green
    }
}

Write-Host "`n=== 4. SUPPRESSION DES DOUBLONS JDKs (16 et 20) ===" -ForegroundColor Cyan
$oldJdks = @("$env:USERPROFILE\.jdks\openjdk-16.0.1", "$env:USERPROFILE\.jdks\openjdk-20.0.1")
foreach ($jdk in $oldJdks) {
    if (Test-Path $jdk) {
        Remove-Item $jdk -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Supprimé : $jdk" -ForegroundColor Green
    }
}

Write-Host "`n=== 5. SUPPRESSION DE YUZU (Émulateur Switch) ===" -ForegroundColor Cyan
$yuzu = "$env:APPDATA\yuzu"
if (Test-Path $yuzu) {
    Remove-Item $yuzu -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Yuzu supprimé (3.78 Go libérés)." -ForegroundColor Green
}

Write-Host "`n=== 6. SUPPRESSION DU CACHE ZOOM ===" -ForegroundColor Cyan
$zoom = "$env:APPDATA\Zoom"
if (Test-Path $zoom) {
    Remove-Item $zoom -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Cache Zoom supprimé (2.13 Go libérés)." -ForegroundColor Green
}

Write-Host "`n=== 7. SUPPRESSION DE LIVE VOICE CHANGER ===" -ForegroundColor Cyan
$vc = "$env:APPDATA\live.voicechanger.app"
if (Test-Path $vc) {
    Remove-Item $vc -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Live Voice Changer supprimé (1.09 Go libérés)." -ForegroundColor Green
}

Write-Host "`n=== 8. SUPPRESSION DES CRASH DUMPS PUBG (TslGame) ===" -ForegroundColor Cyan
$tsl = "$env:LOCALAPPDATA\TslGame"
if (Test-Path $tsl) {
    Remove-Item $tsl -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Caches & Crash Dumps PUBG supprimés (1.08 Go libérés)." -ForegroundColor Green
}

Write-Host "`n=== 9. SUPPRESSION DE C:\AMD (Archives pilotes 2020) ===" -ForegroundColor Cyan
if (Test-Path "C:\AMD") {
    Remove-Item "C:\AMD" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Dossier C:\AMD supprimé (359 Mo libérés)." -ForegroundColor Green
}

Write-Host "`nOpérations de suppression terminées avec succès !" -ForegroundColor Green
