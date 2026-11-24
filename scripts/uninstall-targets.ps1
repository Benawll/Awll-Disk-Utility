# Target: Uninstall Badlion Client, Corsair iCUE and League of Legends registry
$ErrorActionPreference = "SilentlyContinue"

# 1. Badlion Client
Write-Host "Désinstallation de Badlion Client..." -ForegroundColor Cyan
$badlionUninstaller = "C:\Program Files\Badlion Client\Uninstall Badlion Client.exe"
if (Test-Path $badlionUninstaller) {
    Start-Process -FilePath $badlionUninstaller -ArgumentList "/allusers /S" -Wait
    Write-Host "Badlion Client désinstallé." -ForegroundColor Green
}
$badlionData = "C:\ProgramData\BadlionClient"
if (Test-Path $badlionData) {
    Remove-Item -Path $badlionData -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Dossier ProgramData BadlionClient purgé." -ForegroundColor Green
}
if (Test-Path "C:\Program Files\Badlion Client") {
    Remove-Item -Path "C:\Program Files\Badlion Client" -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. Corsair Services & iCUE
Write-Host "Désinstallation des services Corsair..." -ForegroundColor Cyan
# Stop services
Stop-Service -Name "*corsair*" -Force -ErrorAction SilentlyContinue
# MSI uninstallations
Start-Process -FilePath "msiexec.exe" -ArgumentList "/X{7C44AA9B-3C56-41E4-A084-F0A5680C5AD9} /qn /norestart" -Wait
Start-Process -FilePath "msiexec.exe" -ArgumentList "/X{0482CEAA-254D-4D51-B24E-D1DCE8F0EF33} /qn /norestart" -Wait

# Uninstall main iCUE
$icueUninst = "C:\Program Files\Corsair\Corsair iCUE5 Software (1)\icue-uninstaller.exe"
if (Test-Path $icueUninst) {
    Write-Host "Lancement de icue-uninstaller..." -ForegroundColor Cyan
    Start-Process -FilePath $icueUninst -ArgumentList "--action=uninstall" -Wait
}

# Clean ProgramData\Corsair (2.35 Go)
$corsairData = "C:\ProgramData\Corsair"
if (Test-Path $corsairData) {
    Write-Host "Suppression des 2.35 Go de résidus Corsair dans $corsairData..." -ForegroundColor Cyan
    Remove-Item -Path $corsairData -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $corsairData)) {
        Write-Host "Dossier ProgramData Corsair purgé !" -ForegroundColor Green
    }
}
if (Test-Path "C:\Program Files\Corsair") {
    Remove-Item -Path "C:\Program Files\Corsair" -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. League of Legends
Write-Host "Nettoyage du reliquat League of Legends..." -ForegroundColor Cyan
$riotClient = "C:\Riot Games\Riot Client\RiotClientServices.exe"
if (Test-Path $riotClient) {
    Start-Process -FilePath $riotClient -ArgumentList "--uninstall-product=league_of_legends --uninstall-patchline=live" -Wait
}
# Clean registry entry if still present
$regLoL = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game league_of_legends.live"
if (Test-Path $regLoL) {
    Remove-Item -Path $regLoL -Force -ErrorAction SilentlyContinue
}

Write-Host "Opération terminée avec succès !" -ForegroundColor Green
