# Uninstall WampServer and clean C:\wamp64
$ErrorActionPreference = "SilentlyContinue"

Write-Host "Arrêt des services WampServer s'ils sont actifs..." -ForegroundColor Cyan
Stop-Service -Name "wampapache64" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "wampmysqld64" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "wampmariadb64" -Force -ErrorAction SilentlyContinue

$uninstaller = "c:\wamp64\unins000.exe"
if (Test-Path $uninstaller) {
    Write-Host "Lancement de la désinstallation silencieuse de WampServer..." -ForegroundColor Cyan
    $proc = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait -PassThru
    Write-Host "Désinstallation terminée (Code sortie: $($proc.ExitCode))." -ForegroundColor Green
}

# Clean residual folder if present
if (Test-Path "c:\wamp64") {
    Write-Host "Nettoyage du dossier résiduel c:\wamp64..." -ForegroundColor Cyan
    Remove-Item -Path "c:\wamp64" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "c:\wamp64")) {
        Write-Host "Dossier c:\wamp64 entièrement supprimé !" -ForegroundColor Green
    }
}
