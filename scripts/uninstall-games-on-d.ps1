# Uninstall Aim Lab, Deceit 2, and Deadzone Rogue on D:
$ErrorActionPreference = "SilentlyContinue"

$games = @(
    @{ Name = "Aim Lab"; Path = "D:\SteamLibrary\steamapps\common\Aim Lab" },
    @{ Name = "Deceit 2"; Path = "D:\SteamLibrary\steamapps\common\Deceit 2" },
    @{ Name = "Deadzone Rogue"; Path = "D:\SteamLibrary\steamapps\common\Deadzone Rogue" }
)

$totalFreedBytes = 0

foreach ($g in $games) {
    if (Test-Path $g.Path) {
        Write-Host "Calcul de la taille pour $($g.Name)..." -ForegroundColor Cyan
        $files = Get-ChildItem -Path $g.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $gb = [math]::Round($bytes / 1GB, 2)
        Write-Host "Suppression de $($g.Name) ($gb Go)..." -ForegroundColor Yellow
        
        Remove-Item -Path $g.Path -Recurse -Force -ErrorAction SilentlyContinue
        $totalFreedBytes += $bytes
        Write-Host "Suppression terminée pour $($g.Name) !" -ForegroundColor Green
    }
}

# Also remove corresponding appmanifest files in D:\SteamLibrary\steamapps\
if (Test-Path "D:\SteamLibrary\steamapps") {
    Get-ChildItem -Path "D:\SteamLibrary\steamapps" -Filter "appmanifest_*.acf" | ForEach-Object {
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -match '"installdir"\s+"(Aim Lab|Deceit 2|Deadzone Rogue)"') {
            Write-Host "Suppression du manifeste Steam : $($_.Name)" -ForegroundColor DarkGray
            Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
        }
    }
}

$totGB = [math]::Round($totalFreedBytes / 1GB, 2)
Write-Host "`nTotal libéré sur le disque D: : $totGB Go !" -ForegroundColor Green
