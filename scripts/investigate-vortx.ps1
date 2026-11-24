# Script to investigate VortxEngine and WhirlwindFX
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== ENQUÊTE SUR VORTXENGINE & WHIRLWIND FX ===" -ForegroundColor Cyan

# 1. Vérifier les chemins et tailles
$paths = @(
    "C:\Users\benja\AppData\Local\VortxEngine",
    "C:\Users\benja\AppData\Local\WhirlwindFX"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        $files = Get-ChildItem -Path $p -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $lastWrite = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        Write-Host "`nChemin : $p" -ForegroundColor White
        Write-Host "Taille : $([math]::Round($bytes / 1GB, 2)) Go ($([math]::Round($bytes / 1MB, 1)) Mo)" -ForegroundColor Yellow
        Write-Host "Nombre de fichiers : $($files.Count)" -ForegroundColor Gray
        Write-Host "Dernière modification : $($lastWrite.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Magenta
        
        # Types de fichiers principaux
        Get-ChildItem -Path $p -File -Recurse -Force | Group-Object Extension | Sort-Object Count -Descending | Select-Object -First 5 Name, Count | Format-Table -AutoSize
    }
}

# 2. Processus en mémoire
Write-Host "`nProcessus en cours d'exécution liés :" -ForegroundColor Yellow
$procs = Get-Process | Where-Object { $_.ProcessName -match '(Vortx|Whirlwind|Element)' }
if ($procs) {
    $procs | Select-Object Id, ProcessName, Path | Format-Table -AutoSize
} else {
    Write-Host "Aucun processus actif en mémoire." -ForegroundColor Green
}

# 3. Services Windows
Write-Host "`nServices Windows liés :" -ForegroundColor Yellow
$services = Get-Service | Where-Object { $_.Name -match '(Vortx|Whirlwind)' -or $_.DisplayName -match '(Vortx|Whirlwind)' }
if ($services) {
    $services | Select-Object Name, Status, DisplayName | Format-Table -AutoSize
} else {
    Write-Host "Aucun service Windows associé." -ForegroundColor Green
}

# 4. Inscriptions registre
Write-Host "`nInformations Registre / Désinstallation :" -ForegroundColor Cyan
$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)
$regInfo = foreach ($rp in $regPaths) {
    Get-ItemProperty $rp -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match '(Vortx|Whirlwind|Element)' }
}
if ($regInfo) {
    $regInfo | Select-Object DisplayName, DisplayVersion, Publisher, InstallDate, UninstallString | Format-List
} else {
    Write-Host "Aucune entrée de désinstallation trouvée dans le Registre (données orphelines/portables)." -ForegroundColor Gray
}
