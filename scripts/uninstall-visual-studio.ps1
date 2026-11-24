# Headless silent uninstall of Visual Studio 18 and cache purge
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== DÉSINSTALLATION SILENCIEUSE DE VISUAL STUDIO 18 ===" -ForegroundColor Cyan

$installer = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe"
$installPath = "C:\Program Files\Microsoft Visual Studio\18\Community"

if (Test-Path $installer) {
    Write-Host "Lancement de la désinstallation silencieuse via setup.exe..." -ForegroundColor Cyan
    $args = "uninstall --installPath `"$installPath`" --quiet --norestart"
    $proc = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
    Write-Host "Désinstallation terminée (Code sortie: $($proc.ExitCode))." -ForegroundColor Green
}

# Clean any residual Visual Studio 18 folder
if (Test-Path "C:\Program Files\Microsoft Visual Studio\18") {
    Write-Host "Suppression des fichiers résiduels dans C:\Program Files\Microsoft Visual Studio\18..." -ForegroundColor Cyan
    Remove-Item -Path "C:\Program Files\Microsoft Visual Studio\18" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "C:\Program Files\Microsoft Visual Studio\18")) {
        Write-Host "Dossier Visual Studio 18 supprimé !" -ForegroundColor Green
    }
}

# Clean Visual Studio Packages cache in ProgramData (3.94 Go)
$pkgPath = "C:\ProgramData\Microsoft\VisualStudio\Packages"
if (Test-Path $pkgPath) {
    Write-Host "Suppression du cache d'installation dans $pkgPath (3.94 Go)..." -ForegroundColor Cyan
    Remove-Item -Path $pkgPath -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $pkgPath)) {
        Write-Host "Cache de packages Visual Studio supprimé !" -ForegroundColor Green
    }
}

Write-Host "`nOpération terminée avec succès !" -ForegroundColor Green
