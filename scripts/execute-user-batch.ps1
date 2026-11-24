# Clean up the 6 user-selected items in headless mode
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       EXÉCUTION DU NETTOYAGE DU LOT SÉLECTIONNÉ          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. NVIDIA DirectX Shader Cache (DXCache)
Write-Host "`n[1/6] Purge de NVIDIA DXCache (AppData\Local\NVIDIA\DXCache)..." -ForegroundColor Yellow
$dxcachePath = "$env:LOCALAPPDATA\NVIDIA\DXCache"
if (Test-Path $dxcachePath) {
    # Supprimer les fichiers de cache en ignorant ceux verrouillés par les processus en cours
    Get-ChildItem -Path $dxcachePath -File -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
    Write-Host "NVIDIA DXCache purgé avec succès !" -ForegroundColor Green
} else {
    Write-Host "DXCache déjà vide ou absent." -ForegroundColor Gray
}

# 2. Playwright Test Browsers (ms-playwright)
Write-Host "`n[2/6] Suppression des navigateurs Playwright (AppData\Local\ms-playwright)..." -ForegroundColor Yellow
$pwPath = "$env:LOCALAPPDATA\ms-playwright"
if (Test-Path $pwPath) {
    Remove-Item $pwPath -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $pwPath)) {
        Write-Host "Navigateurs Playwright supprimés (1.04 Go libérés) !" -ForegroundColor Green
    }
}

# 3. Résidus Visual Studio (Program Files x86\Microsoft Visual Studio)
Write-Host "`n[3/6] Suppression des résidus Visual Studio (C:\Program Files (x86)\Microsoft Visual Studio)..." -ForegroundColor Yellow
$vsPath = "C:\Program Files (x86)\Microsoft Visual Studio"
if (Test-Path $vsPath) {
    Remove-Item $vsPath -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $vsPath)) {
        Write-Host "Résidus Visual Studio supprimés (347 Mo libérés) !" -ForegroundColor Green
    }
}

# 4. Monero Local Blockchain DB (ProgramData\bitmonero)
Write-Host "`n[4/6] Suppression de la base Monero (C:\ProgramData\bitmonero)..." -ForegroundColor Yellow
$moneroPath = "C:\ProgramData\bitmonero"
if (Test-Path $moneroPath) {
    Remove-Item $moneroPath -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $moneroPath)) {
        Write-Host "Base Monero supprimée (496 Mo libérés) !" -ForegroundColor Green
    }
}

# 5. Caches Electron Updaters (AppData\Local\*updater)
Write-Host "`n[5/6] Suppression des caches Electron Updaters..." -ForegroundColor Yellow
$updaters = @(
    "$env:LOCALAPPDATA\atk-hub-updater",
    "$env:LOCALAPPDATA\@badlionnative-desktop-updater",
    "$env:LOCALAPPDATA\wootility-updater",
    "$env:LOCALAPPDATA\r2modman-updater",
    "$env:LOCALAPPDATA\curseforge-updater"
)
foreach ($u in $updaters) {
    if (Test-Path $u) {
        Remove-Item $u -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Supprimé : $u" -ForegroundColor Green
    }
}

# 6. Outils Divers (Blankos, BetterCrewLink, doublon Wootility-lekker)
Write-Host "`n[6/6] Suppression de Blankos, BetterCrewLink et Wootility-lekker..." -ForegroundColor Yellow
$tools = @(
    "$env:LOCALAPPDATA\Programs\Blankos",
    "$env:LOCALAPPDATA\Programs\bettercrewlink",
    "$env:LOCALAPPDATA\Programs\wootility-lekker"
)
foreach ($t in $tools) {
    if (Test-Path $t) {
        Remove-Item $t -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Supprimé : $t" -ForegroundColor Green
    }
}

# Nettoyage des raccourcis du menu démarrer correspondants si orphelins
$startMenu = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$shortcuts = @("Blankos", "BetterCrewLink", "Wootility Lekker")
foreach ($s in $shortcuts) {
    Get-ChildItem -Path $startMenu -Recurse -Filter "*$s*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Host "`n=== VÉRIFICATION DE L'ESPACE DISQUE APRES NETTOYAGE ===" -ForegroundColor Cyan
$c = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeGB = [math]::Round($c.FreeSpace / 1GB, 2)
$totalGB = [math]::Round($c.Size / 1GB, 2)
$usedGB = [math]::Round($totalGB - $freeGB, 2)
Write-Host "Espace Libre C: : $freeGB Go (sur $totalGB Go, Utilisé: $usedGB Go)" -ForegroundColor Yellow
