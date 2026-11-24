# Target: Headless uninstall of obsolete JetBrains IDEs (PhpStorm, IntelliJ 2021, WebStorm), keeping PyCharm safe
$ErrorActionPreference = "SilentlyContinue"

$ides = @(
    @{ Name = "IntelliJ IDEA Community 2021.1.2"; Path = "C:\Program Files\JetBrains\IntelliJ IDEA Community Edition 2021.1.2" },
    @{ Name = "PhpStorm 2023.1.2"; Path = "C:\Program Files\JetBrains\PhpStorm 2023.1.2" },
    @{ Name = "WebStorm 2022.1.3"; Path = "C:\Program Files\JetBrains\WebStorm 2022.1.3" }
)

$totalFreedBytes = 0

foreach ($ide in $ides) {
    if (Test-Path $ide.Path) {
        Write-Host "Calcul de la taille pour $($ide.Name)..." -ForegroundColor Cyan
        $files = Get-ChildItem -Path $ide.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $gb = [math]::Round($bytes / 1GB, 2)
        Write-Host "Taille : $gb Go dans $($ide.Path)" -ForegroundColor Yellow
        
        # Check for NSIS silent uninstaller
        $uninst = Join-Path $ide.Path "bin\Uninstall.exe"
        if (Test-Path $uninst) {
            Write-Host "Lancement de la désinstallation silencieuse de $($ide.Name)..." -ForegroundColor Cyan
            Start-Process -FilePath $uninst -ArgumentList "/S" -Wait
        }
        
        # Remove leftover directory
        if (Test-Path $ide.Path) {
            Remove-Item -Path $ide.Path -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        if (-not (Test-Path $ide.Path)) {
            Write-Host "Succès : $($ide.Name) supprimé ($gb Go libérés) !" -ForegroundColor Green
            $totalFreedBytes += $bytes
        }
    } else {
        Write-Host "$($ide.Name) est déjà absent." -ForegroundColor Gray
    }
}

$totGB = [math]::Round($totalFreedBytes / 1GB, 2)
Write-Host "`nTotal libéré sur JetBrains : $totGB Go !" -ForegroundColor Green
