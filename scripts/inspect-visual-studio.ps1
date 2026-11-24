# Inspect Visual Studio workloads and installation reason
$ErrorActionPreference = "SilentlyContinue"

$vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    Write-Host "=== INFORMATIONS SUR L'INSTALLATION VISUAL STUDIO ===" -ForegroundColor Cyan
    & $vswhere -products * -format json | ConvertFrom-Json | ForEach-Object {
        Write-Host "Nom du produit : $($_.displayName)" -ForegroundColor Green
        Write-Host "Version : $($_.installationVersion)" -ForegroundColor Gray
        Write-Host "Chemin : $($_.installationPath)" -ForegroundColor Gray
    }
    
    Write-Host "`n=== PACKAGES ET CHARGES DE TRAVAIL (WORKLOADS) INSTALLÉES ===" -ForegroundColor Cyan
    $packages = & $vswhere -products * -requires *
    & $vswhere -products * -format json | ConvertFrom-Json | ForEach-Object {
        Write-Host "ID d'installation : $($_.instanceId)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "vswhere.exe introuvable." -ForegroundColor Yellow
}

# Check Visual Studio Installer installed packages configuration
$vsConfig = Get-ChildItem -Path "C:\ProgramData\Microsoft\VisualStudio\Packages\_Instances" -Filter "state.json" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($vsConfig) {
    $json = Get-Content $vsConfig.FullName -Raw | ConvertFrom-Json
    Write-Host "`nCharges de travail (Workloads) détectées dans l'instance :" -ForegroundColor Yellow
    $json.selectedPackages | Where-Object { $_.id -match "Workload" } | ForEach-Object {
        Write-Host "- $($_.id)" -ForegroundColor White
    }
}
