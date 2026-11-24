# Script to purge ASUS Armoury Crate logs safely and remove C:\Jts & C:\iMyfone
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. PURGE DES LOGS ASUS ARMOURY CRATE (17.26 Go) ===" -ForegroundColor Cyan

# Stopper temporairement le service de log ArmouryCrate si nécessaire pour débloquer les fichiers ouverts
$serviceRunning = $false
$asusService = Get-Service -Name "ArmouryCrateService" -ErrorAction SilentlyContinue
if ($asusService -and $asusService.Status -eq "Running") {
    Write-Host "Arrêt temporaire du service ArmouryCrateService pour libérer les verrous..." -ForegroundColor Yellow
    Stop-Service -Name "ArmouryCrateService" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    $serviceRunning = $true
}

$diagPath = "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis"
if (Test-Path $diagPath) {
    # Supprimer récursivement le contenu volumineux (AsusLog, LightingService, etc.)
    Get-ChildItem -Path $diagPath -File -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
    
    # Recréer les sous-dossiers de base pour que le service puisse redémarrer proprement
    New-Item -Path "$diagPath\AsusLog" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path "$diagPath\LightingService" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
    
    Write-Host "Dossiers de diagnostic ASUS Armoury Crate purgés !" -ForegroundColor Green
}

# Redémarrer le service s'il tournait
if ($serviceRunning) {
    Write-Host "Redémarrage de ArmouryCrateService..." -ForegroundColor Yellow
    Start-Service -Name "ArmouryCrateService" -ErrorAction SilentlyContinue
    $status = (Get-Service -Name "ArmouryCrateService").Status
    Write-Host "Statut du service : $status" -ForegroundColor Green
}

Write-Host "`n=== 2. SUPPRESSION DE C:\Jts (187.9 Mo) ===" -ForegroundColor Cyan
if (Test-Path "C:\Jts") {
    Remove-Item "C:\Jts" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "C:\Jts")) {
        Write-Host "C:\Jts supprimé avec succès !" -ForegroundColor Green
    }
}

Write-Host "`n=== 3. SUPPRESSION DE C:\iMyfone (40.7 Mo) ===" -ForegroundColor Cyan
if (Test-Path "C:\iMyfone") {
    Remove-Item "C:\iMyfone" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "C:\iMyfone")) {
        Write-Host "C:\iMyfone supprimé avec succès !" -ForegroundColor Green
    }
}

Write-Host "`n=== 4. VÉRIFICATION NOUVEL ESPACE DISQUE C: ===" -ForegroundColor Cyan
$c = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeGB = [math]::Round($c.FreeSpace / 1GB, 2)
$totalGB = [math]::Round($c.Size / 1GB, 2)
$usedGB = [math]::Round($totalGB - $freeGB, 2)
Write-Host "Espace Libre C: : $freeGB Go (sur $totalGB Go, Utilisé: $usedGB Go)" -ForegroundColor Yellow
