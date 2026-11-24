# Target 3: Purge safe developer caches (Maven repository, Gradle caches, Pip, Yarn, Npm)
$ErrorActionPreference = "SilentlyContinue"

$devCaches = @(
    @{ Name = "Maven Repository (.m2/repository)"; Path = "$env:USERPROFILE\.m2\repository" },
    @{ Name = "Gradle Caches"; Path = "$env:USERPROFILE\.gradle\caches" },
    @{ Name = "Pip Cache"; Path = "$env:LOCALAPPDATA\pip\cache" },
    @{ Name = "Yarn Cache"; Path = "$env:LOCALAPPDATA\Yarn\Cache" },
    @{ Name = "NPM Cache"; Path = "$env:APPDATA\npm-cache" }
)

$totalFreedBytes = 0

foreach ($c in $devCaches) {
    if (Test-Path $c.Path) {
        Write-Host "Analyse de $($c.Name)..." -ForegroundColor Cyan
        $files = Get-ChildItem -Path $c.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        if ($bytes -gt 0) {
            $mb = [math]::Round($bytes / 1MB, 1)
            $gb = [math]::Round($bytes / 1GB, 2)
            Write-Host "Purge de $($c.Name) ($mb Mo / $gb Go)..." -ForegroundColor Yellow
            Remove-Item -Path "$($c.Path)\*" -Recurse -Force -ErrorAction SilentlyContinue
            $totalFreedBytes += $bytes
            Write-Host "Purge terminée pour $($c.Name)." -ForegroundColor Green
        } else {
            Write-Host "$($c.Name) est déjà vide." -ForegroundColor Gray
        }
    }
}

$totGB = [math]::Round($totalFreedBytes / 1GB, 2)
Write-Host "Total libéré sur les caches de développement : $totGB Go !" -ForegroundColor Green
