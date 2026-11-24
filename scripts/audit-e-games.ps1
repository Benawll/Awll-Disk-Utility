# Check games in E:\SteamLibrary and E:\Epic Games
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== JEUX DANS E:\SteamLibrary (501 Go) ===" -ForegroundColor Cyan
if (Test-Path "E:\SteamLibrary\steamapps\common") {
    Get-ChildItem "E:\SteamLibrary\steamapps\common" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
            Raw = $sum
        }
    } | Sort-Object Raw -Descending | Select-Object Jeu, "Taille (Go)", "Dernier Accès" | Format-Table -AutoSize
}

Write-Host "`n=== JEUX DANS E:\Epic Games (75 Go) ===" -ForegroundColor Cyan
if (Test-Path "E:\Epic Games") {
    Get-ChildItem "E:\Epic Games" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
        }
    } | Format-Table -AutoSize
}

Write-Host "`n=== JEUX DANS E:\Jeux (30 Go) ===" -ForegroundColor Cyan
if (Test-Path "E:\Jeux") {
    Get-ChildItem "E:\Jeux" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
        }
    } | Format-Table -AutoSize
}
