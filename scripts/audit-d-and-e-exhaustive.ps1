# Exhaustive scan of D: and E: for hidden caches, workshop, downloads, software, and games
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     INVENTAIRE EXHAUSTIF ET DÉTAILLÉ DE D:\ ET E:\       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Caches Steam cachés : Workshop, ShaderCache, Downloading sur D: et E:
Write-Host "`n=== 1. CACHES INTERNES STEAM (Workshop, ShaderCache, Downloading) ===" -ForegroundColor Yellow
$steamDirs = @("D:\SteamLibrary\steamapps", "E:\SteamLibrary\steamapps", "C:\Program Files (x86)\Steam\steamapps")
foreach ($sd in $steamDirs) {
    if (Test-Path $sd) {
        Get-ChildItem $sd -Directory -Force | Where-Object { $_.Name -match '^(workshop|shadercache|downloading|temp)$' } | ForEach-Object {
            $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            if ($sum -gt 50MB) {
                [PSCustomObject]@{
                    Dossier = $_.FullName
                    "Taille (Go)" = [math]::Round($sum / 1GB, 2)
                    "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
                }
            }
        } | Format-Table -AutoSize
    }
}

# 2. Logiciels installés dans D:\Program Files et E:\Program Files
Write-Host "`n=== 2. LOGICIELS DANS D:\Program Files et E:\Program Files ===" -ForegroundColor Yellow
$pfPaths = @("D:\Program Files", "E:\Program Files")
foreach ($pf in $pfPaths) {
    if (Test-Path $pf) {
        Write-Host "Chemin : $pf" -ForegroundColor Cyan
        Get-ChildItem $pf -Directory -Force | ForEach-Object {
            $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            [PSCustomObject]@{
                Logiciel = $_.Name
                "Taille (Go)" = [math]::Round($sum / 1GB, 2)
                "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
                Chemin = $_.FullName
            }
        } | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize
    }
}

# 3. Jeux Xbox Game Pass dans E:\XBoxGames (54.5 Go)
Write-Host "`n=== 3. CONTENU DE E:\XBoxGames (54.5 Go) ===" -ForegroundColor Yellow
if (Test-Path "E:\XBoxGames") {
    Get-ChildItem "E:\XBoxGames" -Directory -Force | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
            Chemin = $_.FullName
        }
    } | Sort-Object "Taille (Go)" -Descending | Format-Table -AutoSize
}

# 4. Versions de UnityEditor dans D:\UnityEditor (7.88 Go) et Uplay Jeux (6.61 Go)
Write-Host "`n=== 4. VERSIONS UNITY ET UPLAY SUR D: ===" -ForegroundColor Yellow
if (Test-Path "D:\UnityEditor") {
    Write-Host "D:\UnityEditor :" -ForegroundColor Cyan
    Get-ChildItem "D:\UnityEditor" -Directory -Force | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Version = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        }
    } | Format-Table -AutoSize
}
if (Test-Path "D:\Uplay Jeux") {
    Write-Host "D:\Uplay Jeux :" -ForegroundColor Cyan
    Get-ChildItem "D:\Uplay Jeux" -Directory -Force | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Jeu = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        }
    } | Format-Table -AutoSize
}

# 5. Contenu de D:\Téléchargements (19.56 Go)
Write-Host "`n=== 5. GROS FICHIERS DANS D:\Téléchargements (19.56 Go) ===" -ForegroundColor Yellow
if (Test-Path "D:\Téléchargements") {
    Get-ChildItem "D:\Téléchargements" -File -Recurse -Force | Where-Object { $_.Length -gt 200MB } | ForEach-Object {
        [PSCustomObject]@{
            Fichier = $_.Name
            "Taille (Go)" = [math]::Round($_.Length / 1GB, 2)
            "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
            Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
        }
    } | Sort-Object "Taille (Mo)" -Descending | Select-Object -First 20 | Format-Table -AutoSize
}

# 6. Tous les jeux Steam sur D: et E: (> 1 Go) avec date de dernier accès
Write-Host "`n=== 6. LISTE COMPLÈTE DE TOUS LES JEUX STEAM INACTIFS (> 1 Go) ===" -ForegroundColor Yellow
$allSteam = @(
    "D:\SteamLibrary\steamapps\common",
    "E:\SteamLibrary\steamapps\common"
)
$steamGames = foreach ($lib in $allSteam) {
    if (Test-Path $lib) {
        $drive = $lib.Substring(0, 2)
        Get-ChildItem $lib -Directory | ForEach-Object {
            $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
            if ($sum -gt 1GB) {
                [PSCustomObject]@{
                    Disque = $drive
                    Jeu = $_.Name
                    "Taille (Go)" = [math]::Round($sum / 1GB, 2)
                    "Dernier Accès" = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
                    Chemin = $_.FullName
                    Raw = $sum
                }
            }
        }
    }
}
$steamGames | Sort-Object Raw -Descending | Select-Object Disque, Jeu, "Taille (Go)", "Dernier Accès" | Format-Table -AutoSize
