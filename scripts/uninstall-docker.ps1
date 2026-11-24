# Target: Uninstall Docker Desktop and purge WSL virtual disk (23.2 Go)
$ErrorActionPreference = "SilentlyContinue"

Write-Host "Arrêt du service Docker..." -ForegroundColor Cyan
Stop-Service -Name "com.docker.service" -Force -ErrorAction SilentlyContinue

# Unregister Docker WSL distros (instantly frees the 23 GB vhdx)
if (Get-Command "wsl" -ErrorAction SilentlyContinue) {
    Write-Host "Suppression des distributions WSL de Docker..." -ForegroundColor Cyan
    wsl --shutdown
    wsl --unregister docker-desktop-data
    wsl --unregister docker-desktop
}

# Uninstall Docker Desktop application
$dockerUninstaller = "C:\Program Files\Docker\Docker\Docker Desktop Installer.exe"
if (Test-Path $dockerUninstaller) {
    Write-Host "Lancement de la désinstallation de Docker Desktop..." -ForegroundColor Cyan
    $proc = Start-Process -FilePath $dockerUninstaller -ArgumentList "uninstall --quiet" -Wait -PassThru
    Write-Host "Désinstallation de Docker terminée." -ForegroundColor Green
} else {
    Write-Host "Utilisation de winget pour désinstaller Docker..." -ForegroundColor Cyan
    winget uninstall --id Docker.DockerDesktop --silent --accept-source-agreements
}

# Clean residual data in AppData\Local\Docker
$dockerData = "$env:LOCALAPPDATA\Docker"
if (Test-Path $dockerData) {
    Write-Host "Nettoyage du dossier $dockerData..." -ForegroundColor Cyan
    Remove-Item -Path $dockerData -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $dockerData)) {
        Write-Host "Dossier $dockerData supprimé !" -ForegroundColor Green
    }
}

# Clean ProgramData\Docker if present
if (Test-Path "C:\ProgramData\DockerDesktop") {
    Remove-Item -Path "C:\ProgramData\DockerDesktop" -Recurse -Force -ErrorAction SilentlyContinue
}
