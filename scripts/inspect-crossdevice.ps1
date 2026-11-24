# Check if CrossDevice files are real on-disk files or junctions/virtual MTP
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== CROSSDEVICE INSPECTION ===" -ForegroundColor Cyan
if (Test-Path "C:\Users\benja\CrossDevice") {
    Get-ChildItem "C:\Users\benja\CrossDevice" -Force | Select-Object Name, Attributes, LinkType, Target | Format-Table -AutoSize
    
    Get-ChildItem "C:\Users\benja\CrossDevice" -Directory -Force | ForEach-Object {
        $sub = $_.FullName
        $measure = Get-ChildItem $sub -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
        [PSCustomObject]@{
            Dossier = $_.Name
            Attributes = $_.Attributes
            "Taille (Go)" = [math]::Round($measure.Sum / 1GB, 2)
            Fichiers = $measure.Count
        }
    } | Format-Table -AutoSize
}
