# In-depth scan of game titles, duplicates, and folders on D: and E:
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. JEUX DANS D:\SteamLibrary (380 Go) ===" -ForegroundColor Cyan
if (Test-Path "D:\SteamLibrary\steamapps\common") {
    Get-ChildItem "D:\SteamLibrary\steamapps\common" -Directory | ForEach-Object {
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

Write-Host "`n=== 2. DOSSIER D:\Autres Jeux (141 Go) ===" -ForegroundColor Cyan
if (Test-Path "D:\Autres Jeux") {
    Get-ChildItem "D:\Autres Jeux" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Dossier = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
            Raw = $sum
        }
    } | Sort-Object Raw -Descending | Select-Object Dossier, "Taille (Go)", "Dernier Accès" | Format-Table -AutoSize
}

Write-Host "`n=== 3. DOSSIER D:\Epic games jeux (41 Go) ===" -ForegroundColor Cyan
if (Test-Path "D:\Epic games jeux") {
    Get-ChildItem "D:\Epic games jeux" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        }
    } | Format-Table -AutoSize
}

Write-Host "`n=== 4. DOSSIERS SUSPECTS OU ARCHIVES SUR D: (Clé boot, Transfert, etc.) ===" -ForegroundColor Cyan
$suspectsD = @("D:\Clé boot windows", "D:\dual boot ubuntu", "D:\Transfert PC Benjamin")
foreach ($s in $suspectsD) {
    if (Test-Path $s) {
        $sum = (Get-ChildItem $s -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $s -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Dossier = $s
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernière Modif" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
        }
    }
}
if ($suspectsD) { $suspectsD | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } | Select-Object FullName, LastWriteTime | Format-Table -AutoSize }

Write-Host "`n=== 5. JEUX DANS E:\SteamLibrary (501 Go) ===" -ForegroundColor Cyan
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

Write-Host "`n=== 6. JEUX DANS E:\Epic Games (75 Go) ===" -ForegroundColor Cyan
if (Test-Path "E:\Epic Games") {
    Get-ChildItem "E:\Epic Games" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        }
    } | Format-Table -AutoSize
}

Write-Host "`n=== 7. DOSSIER E:\Yuzu (22.4 Go) ===" -ForegroundColor Cyan
if (Test-Path "E:\Yuzu") {
    Get-ChildItem "E:\Yuzu" -File -Recurse | Select-Object Name, Length, LastWriteTime | ForEach-Object {
        [PSCustomObject]@{
            Fichier = $_.Name
            "Taille (Go)" = [math]::Round($_.Length / 1GB, 2)
            "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
            Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
        }
    } | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize
}
