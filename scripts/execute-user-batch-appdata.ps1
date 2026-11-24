# Clean up 9 items selected by user: Medal, Voicemod, Dry Cactus, MultiVersus/Roblox, Updaters, Overwolf, Slack, Gradle, Pulse Secure
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "          NETTOYAGE DU LOT APPDATA & MODS/CACHES          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Medal.tv Caches & Clips Buffer
Write-Host "`n[1/9] Suppression des caches et clips Medal.tv (AppData\Local\Medal)..." -ForegroundColor Yellow
Stop-Process -Name "*Medal*" -Force -ErrorAction SilentlyContinue
$medal = "$env:LOCALAPPDATA\Medal"
if (Test-Path $medal) {
    Remove-Item $medal -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Medal.tv purgé (919 Mo libérés) !" -ForegroundColor Green
}

# 2. Voicemod V3 Audio Data
Write-Host "`n[2/9] Suppression de Voicemod V3 (AppData\Local\VoicemodV3)..." -ForegroundColor Yellow
Stop-Process -Name "*Voicemod*" -Force -ErrorAction SilentlyContinue
$vm = "$env:LOCALAPPDATA\VoicemodV3"
if (Test-Path $vm) {
    Remove-Item $vm -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Voicemod V3 purgé (441 Mo libérés) !" -ForegroundColor Green
}

# 3. Poly Bridge / Dry Cactus (Sauvegardes)
Write-Host "`n[3/9] Suppression de Poly Bridge (AppData\LocalLow\Dry Cactus)..." -ForegroundColor Yellow
$dry = "$env:USERPROFILE\AppData\LocalLow\Dry Cactus"
if (Test-Path $dry) {
    Remove-Item $dry -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Poly Bridge / Dry Cactus supprimé (971 Mo libérés) !" -ForegroundColor Green
}

# 4. Caches MultiVersus & Roblox
Write-Host "`n[4/9] Suppression des caches MultiVersus et Roblox..." -ForegroundColor Yellow
Stop-Process -Name "*Roblox*", "*MultiVersus*" -Force -ErrorAction SilentlyContinue
$mv = "$env:LOCALAPPDATA\MultiVersus"
$rbx = "$env:LOCALAPPDATA\Roblox"
if (Test-Path $mv) { Remove-Item $mv -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "MultiVersus purgé !" -ForegroundColor Green }
if (Test-Path $rbx) { Remove-Item $rbx -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Roblox purgé !" -ForegroundColor Green }

# 5. Updaters et Caches Résiduels (Yuzu, Pawns, etc.)
Write-Host "`n[5/9] Suppression des updaters et résidus (Yuzu, Pawns, etc.)..." -ForegroundColor Yellow
$updaters = @(
    "$env:LOCALAPPDATA\yuzu",
    "$env:LOCALAPPDATA\wootility-lekker-updater",
    "$env:LOCALAPPDATA\bettercrewlink-updater",
    "$env:LOCALAPPDATA\pawns.app-updater"
)
foreach ($u in $updaters) {
    if (Test-Path $u) {
        Remove-Item $u -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Supprimé : $u" -ForegroundColor Green
    }
}

# 6. Overwolf Gaming Overlay
Write-Host "`n[6/9] Suppression de Overwolf (Local + Roaming)..." -ForegroundColor Yellow
Stop-Process -Name "*Overwolf*" -Force -ErrorAction SilentlyContinue
$owLocal = "$env:LOCALAPPDATA\Overwolf"
$owRoam = "$env:APPDATA\Overwolf"
$owPF = "C:\Program Files (x86)\Overwolf"
if (Test-Path $owLocal) { Remove-Item $owLocal -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $owRoam) { Remove-Item $owRoam -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $owPF) { Remove-Item $owPF -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "Overwolf supprimé (~1.99 Go libérés) !" -ForegroundColor Green

# 7. Slack Caches & Data
Write-Host "`n[7/9] Suppression des caches Slack (Local + Roaming)..." -ForegroundColor Yellow
Stop-Process -Name "*slack*" -Force -ErrorAction SilentlyContinue
$slackLocal = "$env:LOCALAPPDATA\slack"
$slackRoam = "$env:APPDATA\Slack"
if (Test-Path $slackLocal) { Remove-Item $slackLocal -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $slackRoam) { Remove-Item $slackRoam -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "Slack purgé (1.60 Go libérés) !" -ForegroundColor Green

# 8. Gradle Build Cache (.gradle)
Write-Host "`n[8/9] Suppression des caches de build Gradle (C:\Users\benja\.gradle)..." -ForegroundColor Yellow
$gradle = "$env:USERPROFILE\.gradle\caches"
if (Test-Path $gradle) {
    Remove-Item $gradle -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Caches de dépendances Gradle purgés (856 Mo libérés) !" -ForegroundColor Green
}

# 9. Pulse Secure VPN Cache
Write-Host "`n[9/9] Suppression de Pulse Secure (AppData\Roaming\Pulse Secure)..." -ForegroundColor Yellow
Stop-Process -Name "*PulseSecure*" -Force -ErrorAction SilentlyContinue
$pulse = "$env:APPDATA\Pulse Secure"
if (Test-Path $pulse) {
    Remove-Item $pulse -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Pulse Secure purgé (772 Mo libérés) !" -ForegroundColor Green
}

Write-Host "`n=== VÉRIFICATION DU NOUVEL ESPACE DISQUE C: ===" -ForegroundColor Cyan
$c = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeGB = [math]::Round($c.FreeSpace / 1GB, 2)
$totalGB = [math]::Round($c.Size / 1GB, 2)
$usedGB = [math]::Round($totalGB - $freeGB, 2)
Write-Host "Espace Libre C: : $freeGB Go (sur $totalGB Go, Utilisé: $usedGB Go)" -ForegroundColor Yellow
