# Full updated breakdown of drive C: folders
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. RACINE DU DISQUE C:\ ===" -ForegroundColor Cyan
Get-ChildItem "C:\" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    [PSCustomObject]@{
        "Dossier"    = $_.Name
        "TailleGo"   = [math]::Round($sz, 2)
        "Chemin"     = $_.FullName
    }
} | Sort-Object TailleGo -Descending | Format-Table -AutoSize

Write-Host "`n=== 2. PROFIL UTILISATEUR (C:\Users\benja) ===" -ForegroundColor Cyan
Get-ChildItem "$env:USERPROFILE" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    [PSCustomObject]@{
        "Dossier"    = $_.Name
        "TailleGo"   = [math]::Round($sz, 2)
        "Chemin"     = $_.FullName
    }
} | Where-Object { $_.TailleGo -gt 0.5 } | Sort-Object TailleGo -Descending | Format-Table -AutoSize

Write-Host "`n=== 3. PROGRAMDATA (C:\ProgramData) ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
    $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    [PSCustomObject]@{
        "Dossier"    = $_.Name
        "TailleGo"   = [math]::Round($sz, 2)
        "Chemin"     = $_.FullName
    }
} | Where-Object { $_.TailleGo -gt 0.5 } | Sort-Object TailleGo -Descending | Format-Table -AutoSize
