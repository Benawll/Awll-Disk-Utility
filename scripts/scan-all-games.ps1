# Comprehensive games discovery across all drives
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== JEUX VIDÉO INSTALLÉS DÉTECTÉS ===" -ForegroundColor Cyan

# Check Steam
$steamPaths = @("C:\Program Files (x86)\Steam\steamapps\common", "D:\SteamLibrary\steamapps\common", "E:\SteamLibrary\steamapps\common")
foreach ($sp in $steamPaths) {
    if (Test-Path $sp) {
        Get-ChildItem -Path $sp -Directory | ForEach-Object {
            $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
            [PSCustomObject]@{ "Jeu" = $_.Name; "Plateforme" = "Steam"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
        }
    }
}

# Check Epic Games
$epicPaths = @("C:\Program Files\Epic Games", "D:\Epic games jeux", "E:\Epic Games", "D:\Epic Games")
foreach ($ep in $epicPaths) {
    if (Test-Path $ep) {
        Get-ChildItem -Path $ep -Directory | ForEach-Object {
            $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
            [PSCustomObject]@{ "Jeu" = $_.Name; "Plateforme" = "Epic Games"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
        }
    }
}

# Check Riot Games
if (Test-Path "C:\Riot Games") {
    Get-ChildItem -Path "C:\Riot Games" -Directory | Where-Object { $_.Name -ne "Riot Client" } | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
        [PSCustomObject]@{ "Jeu" = $_.Name; "Plateforme" = "Riot Games"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
    }
}

# Check Dolphin / Emulators
if (Test-Path "C:\Jeux dolphin") {
    $sz = (Get-ChildItem "C:\Jeux dolphin" -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    [PSCustomObject]@{ "Jeu" = "Jeux Dolphin (GameCube/Wii)"; "Plateforme" = "Dolphin Emulator"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = "C:\Jeux dolphin" }
}

# Check Xbox Games
if (Test-Path "C:\XboxGames") {
    Get-ChildItem -Path "C:\XboxGames" -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
        [PSCustomObject]@{ "Jeu" = $_.Name; "Plateforme" = "Xbox / Microsoft Store"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
    }
}

# Check Ubisoft
$ubiPaths = @("C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games", "D:\Ubisoft Games", "E:\Ubisoft Games")
foreach ($up in $ubiPaths) {
    if (Test-Path $up) {
        Get-ChildItem -Path $up -Directory | ForEach-Object {
            $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
            [PSCustomObject]@{ "Jeu" = $_.Name; "Plateforme" = "Ubisoft Connect"; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
        }
    }
}
