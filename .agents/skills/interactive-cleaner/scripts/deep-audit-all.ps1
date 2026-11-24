# Master Deep Audit Script for Cleaner
# Performs an exhaustive multi-drive audit (C:, D:, E:) covering:
# - AppData & User Profile (dynamic threshold >= 150 MB)
# - Steam downloading stuck caches, workshop & shadercache across all drives
# - Cross-platform game duplicates (Steam vs Epic vs Xbox) & Public Test Servers (PTS)
# - Secondary drive Program Files & third-party emulators (Nox, BlueStacks)
# - Secondary download folders (D:\Téléchargements) with obsolete installers
# - Orphan emulator ROMs (Yuzu, Dolphin)
param(
    [int]$ThresholdMB = 150
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    AUDIT MULTI-DISQUES APPROFONDI (C:, D:, E:) & RISQUES  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. État global des disques connectés
Write-Host "`n[1/7] VÉRIFICATION DE L'ESPACE DISQUE (TOUS LES LECTEURS)..." -ForegroundColor Yellow
$drives = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {
    $totalGB = [math]::Round($_.Size / 1GB, 2)
    $freeGB = [math]::Round($_.FreeSpace / 1GB, 2)
    $usedGB = [math]::Round($totalGB - $freeGB, 2)
    $pctUsed = if ($totalGB -gt 0) { [math]::Round(($usedGB / $totalGB) * 100, 1) } else { 0 }
    [PSCustomObject]@{
        Lecteur     = $_.DeviceID
        "Nom Volume"= $_.VolumeName
        "Total (Go)"= $totalGB
        "Libre (Go)"= $freeGB
        "Utilisé (Go)" = $usedGB
        "% Utilisé" = "$pctUsed %"
    }
}
$drives | Format-Table -AutoSize

# 2. Caches internes Steam cachés (downloading, workshop) sur tous les lecteurs
Write-Host "`n[2/7] SCAN DES CACHES STEAM INTERNES (Downloading bloqués, Workshop)..." -ForegroundColor Yellow
$steamAppsDirs = @(
    "C:\Program Files (x86)\Steam\steamapps",
    "D:\SteamLibrary\steamapps",
    "E:\SteamLibrary\steamapps"
)
$steamCaches = foreach ($sd in $steamAppsDirs) {
    if (Test-Path $sd) {
        Get-ChildItem $sd -Directory -Force | Where-Object { $_.Name -match '^(downloading|workshop|shadercache|temp)$' } | ForEach-Object {
            $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            if ($sum -gt 50MB) {
                $risk = 5
                if ($_.Name -eq "downloading") { $risk = 0 } # Morceaux de téléchargements corrompus/abandonnés
                [PSCustomObject]@{
                    Dossier       = $_.FullName
                    "Taille (Go)" = [math]::Round($sum / 1GB, 2)
                    "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
                    "Risque %"    = "$risk%"
                    RawBytes      = $sum
                }
            }
        }
    }
}
if ($steamCaches) {
    $steamCaches | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize
} else {
    Write-Host "Aucun cache Steam bloqué détecté." -ForegroundColor Green
}

# 3. Détection des doublons de jeux et serveurs de test (PTS)
Write-Host "`n[3/7] DÉTECTION DES DOUBLONS DE JEUX ET SERVEURS DE TEST (PTS)..." -ForegroundColor Yellow
$duplicatesFound = @()
# A. Rocket League
$rlSteam = "E:\SteamLibrary\steamapps\common\rocketleague"
$rlEpic = "D:\Epic games jeux\rocketleague"
if ((Test-Path $rlSteam) -and (Test-Path $rlEpic)) {
    $sizeEpic = (Get-ChildItem $rlEpic -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $duplicatesFound += [PSCustomObject]@{
        "Anomalie / Doublon" = "Rocket League (Doublon Epic sur D: vs Steam sur E:)"
        "Taille (Go)"        = [math]::Round($sizeEpic / 1GB, 2)
        "Risque %"           = "5%"
        Chemin               = $rlEpic
    }
}
# B. Paladins PTS
$pts = "D:\SteamLibrary\steamapps\common\paladins pts"
if (Test-Path $pts) {
    $sizePts = (Get-ChildItem $pts -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $duplicatesFound += [PSCustomObject]@{
        "Anomalie / Doublon" = "Paladins PTS (Public Test Server Steam inactif)"
        "Taille (Go)"        = [math]::Round($sizePts / 1GB, 2)
        "Risque %"           = "5%"
        Chemin               = $pts
    }
}
# C. Phasmophobia cracké vs officiel
$phasmoCrack = "D:\Autres Jeux\Phasmophobia By STG GEGE"
if (Test-Path $phasmoCrack) {
    $sizePh = (Get-ChildItem $phasmoCrack -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $duplicatesFound += [PSCustomObject]@{
        "Anomalie / Doublon" = "Phasmophobia Cracké (Doublon vs Steam E:)"
        "Taille (Go)"        = [math]::Round($sizePh / 1GB, 2)
        "Risque %"           = "0%"
        Chemin               = $phasmoCrack
    }
}
if ($duplicatesFound) {
    $duplicatesFound | Format-Table -AutoSize
} else {
    Write-Host "Aucun doublon de jeu détecté." -ForegroundColor Green
}

# 4. Émulateurs tiers & ROMs orphelines (D:\Program Files\Nox, E:\Yuzu, C:\Jeux dolphin)
Write-Host "`n[4/7] SCAN DES ÉMULATEURS ET ROMS ORPHELINES (D:, E:, C:)..." -ForegroundColor Yellow
$emuCandidates = @(
    @{ Name = "NoxPlayer Android Emulator"; Path = "D:\Program Files\Nox"; Risk = 5 },
    @{ Name = "ROMs Switch Yuzu (Super Smash Bros)"; Path = "E:\Yuzu"; Risk = 5 },
    @{ Name = "Émulateur Dolphin & ISO Wii"; Path = "C:\Jeux dolphin"; Risk = 0 }
)
$emuFound = foreach ($e in $emuCandidates) {
    if (Test-Path $e.Path) {
        $sum = (Get-ChildItem $e.Path -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        if ($sum -gt 10MB) {
            [PSCustomObject]@{
                "Émulateur / ROM" = $e.Name
                "Taille (Go)"     = [math]::Round($sum / 1GB, 2)
                "Risque %"        = "$($e.Risk)%"
                Chemin            = $e.Path
            }
        }
    }
}
if ($emuFound) {
    $emuFound | Format-Table -AutoSize
} else {
    Write-Host "Aucun émulateur ou ROM orpheline résiduelle." -ForegroundColor Green
}

# 5. Installeurs obsolètes dans les dossiers Téléchargements secondaires
Write-Host "`n[5/7] SCAN DES INSTALLEURS OBSOLÈTES (D:\Téléchargements)..." -ForegroundColor Yellow
if (Test-Path "D:\Téléchargements") {
    $oldInstallers = Get-ChildItem "D:\Téléchargements" -File -Recurse -Force | 
        Where-Object { $_.Length -gt 200MB -and $_.Extension -match '^(exe|msi|zip|rar)$' } | ForEach-Object {
            [PSCustomObject]@{
                Fichier       = $_.Name
                "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
                "Risque %"    = "0%"
                Date          = $_.LastWriteTime.ToString("yyyy-MM-dd")
            }
        }
    if ($oldInstallers) {
        $oldInstallers | Sort-Object "Taille (Mo)" -Descending | Select-Object -First 10 | Format-Table -AutoSize
    } else {
        Write-Host "Aucun gros installeur résiduel dans D:\Téléchargements." -ForegroundColor Green
    }
}

# 6. Scan dynamique de AppData (Local, Roaming, LocalLow) sur C: (Seuil >= 150 Mo)
Write-Host "`n[6/7] SCAN DYNAMIQUE APPDATA (SEUIL >= $ThresholdMB Mo)..." -ForegroundColor Yellow
$appDataPaths = @("$env:LOCALAPPDATA", "$env:APPDATA", "$env:USERPROFILE\AppData\LocalLow")
$appDataResults = foreach ($base in $appDataPaths) {
    if (Test-Path $base) {
        $subDirs = Get-ChildItem $base -Directory -Force
        foreach ($d in $subDirs) {
            if ($d.Name -match '^(Microsoft|Packages|Programs)$') { continue }
            $files = Get-ChildItem $d.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
            $bytes = ($files | Measure-Object -Property Length -Sum).Sum
            if (($bytes / 1MB) -ge $ThresholdMB) {
                $risk = 10
                if ($d.Name -match '(cache|temp|log|dump|updater)' -or $d.Name -in @('VortxEngine','WhirlwindFX','Medal','VoicemodV3','MultiVersus','yuzu')) { $risk = 5 }
                [PSCustomObject]@{
                    Dossier      = "$((Get-Item $base).Name)\$($d.Name)"
                    "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
                    "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
                    "Risque %"   = "$risk%"
                    RawBytes     = $bytes
                }
            }
        }
    }
}
if ($appDataResults) {
    $appDataResults | Sort-Object RawBytes -Descending | Select-Object -First 15 Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize
}

# 7. Archives et anciens jeux inactifs depuis 2020/2021 (D:\Autres Jeux)
Write-Host "`n[7/7] SCAN DES ANCIENS JEUX PORTABLES INACTIFS (D:\Autres Jeux)..." -ForegroundColor Yellow
if (Test-Path "D:\Autres Jeux") {
    $oldGames = Get-ChildItem "D:\Autres Jeux" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $last = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Jeu           = $_.Name
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            "Dernier Accès"= if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
            "Risque %"    = "5%"
            RawBytes      = $sum
        }
    }
    if ($oldGames) {
        $oldGames | Sort-Object RawBytes -Descending | Select-Object -First 10 Jeu, "Taille (Go)", "Dernier Accès", "Risque %" | Format-Table -AutoSize
    }
}

Write-Host "`nAudit multi-disques exhaustif terminé avec succès." -ForegroundColor Cyan
