# Helper script to research an item when the user asks for "Plus d'infos"
param(
    [Parameter(Mandatory=$true)]
    [string]$TargetName,
    [string]$TargetPath = ""
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== ENQUÊTE APPROFONDIE SUR : $TargetName ===" -ForegroundColor Cyan

# 1. Check path exists and details
if ($TargetPath -and (Test-Path $TargetPath)) {
    $item = Get-Item $TargetPath
    $files = Get-ChildItem -Path $TargetPath -File -Recurse -Force -ErrorAction SilentlyContinue
    $totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
    $lastAccess = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime

    Write-Host "Chemin : $TargetPath" -ForegroundColor White
    Write-Host "Taille : $([math]::Round($totalBytes / 1GB, 2)) Go ($([math]::Round($totalBytes / 1MB, 1)) Mo)" -ForegroundColor Yellow
    Write-Host "Nombre de fichiers : $($files.Count)" -ForegroundColor Gray
    Write-Host "Date de dernière modification : $($lastAccess.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Magenta
}

# 2. Check if related processes are running right now
$procs = Get-Process | Where-Object { $_.ProcessName -match $TargetName -or $_.Path -match $TargetName }
if ($procs) {
    Write-Host "`nProcessus en cours d'exécution détecté(s) :" -ForegroundColor Red
    $procs | Select-Object Id, ProcessName, Path | Format-Table -AutoSize
} else {
    Write-Host "`nAucun processus actif lié à cet élément." -ForegroundColor Green
}

# 3. Check Windows services
$services = Get-Service | Where-Object { $_.Name -match $TargetName -or $_.DisplayName -match $TargetName }
if ($services) {
    Write-Host "`nService(s) Windows associé(s) :" -ForegroundColor Yellow
    $services | Select-Object Name, Status, DisplayName | Format-Table -AutoSize
}

# 4. Check Registry install info
$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)
$regInfo = foreach ($p in $regPaths) {
    Get-ItemProperty $p -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match $TargetName }
}
if ($regInfo) {
    Write-Host "`nInformations d'installation dans le Registre :" -ForegroundColor Cyan
    $regInfo | Select-Object DisplayName, DisplayVersion, Publisher, InstallDate, UninstallString | Format-List
}
