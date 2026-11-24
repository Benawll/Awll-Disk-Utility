# Inspect Programs and Packages in AppData\Local
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. DOSSIERS DANS C:\Users\benja\AppData\Local\Programs ===" -ForegroundColor Cyan
Get-ChildItem "C:\Users\benja\AppData\Local\Programs" -Directory -Force | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        Nom = $_.Name
        "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
        Chemin = $_.FullName
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize

Write-Host "`n=== 2. DOSSIERS DANS C:\Users\benja\AppData\Local\Packages (> 300 Mo) ===" -ForegroundColor Cyan
Get-ChildItem "C:\Users\benja\AppData\Local\Packages" -Directory -Force | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($sum -gt 300MB) {
        [PSCustomObject]@{
            Package = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
        }
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize
