# Check file types in AsusLog and LightingService
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== FICHIERS DANS AsusLog ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\AsusLog" -File -Recurse | Group-Object Extension | Select-Object Name, Count, @{N="Taille (Go)";E={[math]::Round(($_.Group | Measure-Object Length -Sum).Sum / 1GB, 2)}} | Format-Table -AutoSize

Write-Host "`n=== FICHIERS DANS LightingService ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\LightingService" -File -Recurse | Group-Object Extension | Select-Object Name, Count, @{N="Taille (Go)";E={[math]::Round(($_.Group | Measure-Object Length -Sum).Sum / 1GB, 2)}} | Format-Table -AutoSize
