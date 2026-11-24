# Force remove BlueStacks processes, virtual disks and program files
$ErrorActionPreference = "SilentlyContinue"

Write-Host "Arrêt forcé des processus BlueStacks..." -ForegroundColor Cyan
Stop-Process -Name "*bluestacks*", "*hd-player*" -Force -ErrorAction SilentlyContinue

# Stop any BlueStacks services
Get-Service | Where-Object { $_.Name -match "BlueStacks" -or $_.DisplayName -match "BlueStacks" } | ForEach-Object {
    Write-Host "Arrêt du service : $($_.Name)" -ForegroundColor Yellow
    Stop-Service -Name $_.Name -Force -ErrorAction SilentlyContinue
    sc.exe delete $_.Name
}

# Delete virtual machine images in ProgramData (12.8 Go)
$dataDir = "C:\ProgramData\BlueStacks_nxt"
if (Test-Path $dataDir) {
    Write-Host "Suppression des 12.8 Go d'images virtuelles dans $dataDir..." -ForegroundColor Cyan
    Remove-Item -Path $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $dataDir)) {
        Write-Host "Succès : Dossier $dataDir entièrement supprimé !" -ForegroundColor Green
    } else {
        Remove-Item -Path "$dataDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Delete Program Files
$progDir = "C:\Program Files\BlueStacks_nxt"
if (Test-Path $progDir) {
    Write-Host "Suppression des fichiers de programme dans $progDir..." -ForegroundColor Cyan
    Remove-Item -Path $progDir -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $progDir)) {
        Write-Host "Succès : Dossier $progDir entièrement supprimé !" -ForegroundColor Green
    }
}

# Clean registry uninstall keys
$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
)

foreach ($r in $regPaths) {
    Get-ChildItem -Path $r -ErrorAction SilentlyContinue | ForEach-Object {
        $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if ($props.DisplayName -match "BlueStacks") {
            Write-Host "Suppression de la clé registre : $($_.PSChildName)" -ForegroundColor Yellow
            Remove-Item -Path $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "Nettoyage forcé de BlueStacks terminé avec succès !" -ForegroundColor Green
