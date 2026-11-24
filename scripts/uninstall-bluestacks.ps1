# Target: Uninstall BlueStacks Services, BlueStacks and remove virtual disk images
$ErrorActionPreference = "SilentlyContinue"

# 1. BlueStacks Services quiet uninstall
$bsServicesUninstaller = "$env:LOCALAPPDATA\Programs\bluestacks-services\Uninstall BlueStacksServices.exe"
if (Test-Path $bsServicesUninstaller) {
    Write-Host "Désinstallation de BlueStacks Services..." -ForegroundColor Cyan
    Start-Process -FilePath $bsServicesUninstaller -ArgumentList "/currentuser /S" -Wait
    Write-Host "BlueStacks Services désinstallé." -ForegroundColor Green
}

# 2. Main BlueStacks Uninstaller
$bsUninstaller = "C:\Program Files\BlueStacks_nxt\BlueStacksUninstaller.exe"
if (Test-Path $bsUninstaller) {
    Write-Host "Lancement du désinstalleur BlueStacks..." -ForegroundColor Cyan
    # Run uninstaller with silent or standard tmp flag
    $proc = Start-Process -FilePath $bsUninstaller -ArgumentList "-tmp" -Wait -PassThru
    Write-Host "Désinstalleur BlueStacks terminé." -ForegroundColor Green
}

# 3. Clean residual virtual data in C:\ProgramData\BlueStacks_nxt (12.8 Go)
$dataFolder = "C:\ProgramData\BlueStacks_nxt"
if (Test-Path $dataFolder) {
    Write-Host "Suppression des images de disques virtuels dans $dataFolder..." -ForegroundColor Cyan
    $files = Get-ChildItem -Path $dataFolder -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    $gb = [math]::Round($bytes / 1GB, 2)
    Write-Host "Taille des résidus : $gb Go" -ForegroundColor Yellow
    
    Remove-Item -Path $dataFolder -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $dataFolder)) {
        Write-Host "Dossier $dataFolder supprimé ($gb Go libérés) !" -ForegroundColor Green
    } else {
        Remove-Item -Path "$dataFolder\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
}
