# Target: Remove Core Games (8.65 GB), AlphaAntiLeak (398 MB), Cygwin (119 MB)
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DÉSINSTALLATION DE CORE GAMES (MANTICORE) ===" -ForegroundColor Cyan
$core = Get-AppxPackage *COREBase*
if ($core) {
    Write-Host "Désinstallation du package AppX : $($core.PackageFullName)..." -ForegroundColor Yellow
    Remove-AppxPackage -Package $core.PackageFullName
    Write-Host "Core Games désinstallé avec succès (8.65 Go libérés) !" -ForegroundColor Green
} else {
    Write-Host "Core Games n'est plus présent dans AppX." -ForegroundColor Gray
}

Write-Host "`n=== 2. SUPPRESSION DE C:\AlphaAntiLeak ===" -ForegroundColor Cyan
if (Test-Path "C:\AlphaAntiLeak") {
    Remove-Item "C:\AlphaAntiLeak" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "C:\AlphaAntiLeak")) {
        Write-Host "C:\AlphaAntiLeak supprimé (398 Mo libérés) !" -ForegroundColor Green
    }
}

Write-Host "`n=== 3. SUPPRESSION DE C:\cygwin64 ===" -ForegroundColor Cyan
if (Test-Path "C:\cygwin64") {
    Remove-Item "C:\cygwin64" -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path "C:\cygwin64")) {
        Write-Host "C:\cygwin64 supprimé (119 Mo libérés) !" -ForegroundColor Green
    }
}

Write-Host "`nSuppression des 3 éléments terminée !" -ForegroundColor Green
