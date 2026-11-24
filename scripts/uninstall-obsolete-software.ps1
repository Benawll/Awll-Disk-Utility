# Uninstall obsolete applications from Program Files
$ErrorActionPreference = "SilentlyContinue"

# 1. Astah UML and Astah Professional
$astahUmlUninst = "C:\Program Files\astah-UML\unins000.exe"
if (Test-Path $astahUmlUninst) {
    Write-Host "Désinstallation d'Astah UML..." -ForegroundColor Cyan
    Start-Process -FilePath $astahUmlUninst -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
    Remove-Item "C:\Program Files\astah-UML" -Recurse -Force -ErrorAction SilentlyContinue
}
$astahProUninst = "C:\Program Files\astah-professional\unins000.exe"
if (Test-Path $astahProUninst) {
    Write-Host "Désinstallation d'Astah Professional..." -ForegroundColor Cyan
    Start-Process -FilePath $astahProUninst -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
    Remove-Item "C:\Program Files\astah-professional" -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. EaseUS MobiSaver and Todo PCTrans
$mobiUninst = "C:\Program Files (x86)\EaseUS\EaseUS MobiSaver\unins000.exe"
if (Test-Path $mobiUninst) {
    Write-Host "Désinstallation d'EaseUS MobiSaver..." -ForegroundColor Cyan
    Start-Process -FilePath $mobiUninst -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
}
Remove-Item "C:\Program Files (x86)\EaseUS" -Recurse -Force -ErrorAction SilentlyContinue

# 3. MiniTool ShadowMaker and Partition Wizard
$shadowUninst = "C:\Program Files\MiniTool ShadowMaker\unins000.exe"
if (Test-Path $shadowUninst) {
    Write-Host "Désinstallation de MiniTool ShadowMaker..." -ForegroundColor Cyan
    Start-Process -FilePath $shadowUninst -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
}
Remove-Item "C:\Program Files\MiniTool ShadowMaker" -Recurse -Force -ErrorAction SilentlyContinue

# 4. MatchWare MindView (MSI)
Write-Host "Désinstallation de MatchWare MindView..." -ForegroundColor Cyan
Start-Process -FilePath "msiexec.exe" -ArgumentList "/X{B8DC9547-7B8D-4FE7-A5A1-D55230EF567A} /qn /norestart" -Wait
Remove-Item "C:\Program Files\MatchWare" -Recurse -Force -ErrorAction SilentlyContinue

# 5. SQL Developer (Portable Oracle directory)
if (Test-Path "C:\Program Files\sqldeveloper") {
    Write-Host "Suppression de SQL Developer..." -ForegroundColor Cyan
    Remove-Item "C:\Program Files\sqldeveloper" -Recurse -Force -ErrorAction SilentlyContinue
}

# 6. Android Studio
if (Test-Path "C:\Program Files\Android") {
    Write-Host "Suppression d'Android Studio..." -ForegroundColor Cyan
    Remove-Item "C:\Program Files\Android" -Recurse -Force -ErrorAction SilentlyContinue
}

# 7. BlueStacks X
if (Test-Path "C:\Program Files (x86)\BlueStacks X") {
    Write-Host "Suppression de BlueStacks X..." -ForegroundColor Cyan
    Remove-Item "C:\Program Files (x86)\BlueStacks X" -Recurse -Force -ErrorAction SilentlyContinue
}

# 8. BlueSoleil
if (Test-Path "C:\Program Files\BlueSoleil 10.0.498.0") {
    Write-Host "Suppression de BlueSoleil..." -ForegroundColor Cyan
    Remove-Item "C:\Program Files\BlueSoleil 10.0.498.0" -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`nDésinstallations terminées avec succès !" -ForegroundColor Green
