# Script d'exécution sécurisée et silencieuse du nettoyage tri-disques
# Basé sur la sélection explicite et validée de l'utilisateur

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     EXÉCUTION DU NETTOYAGE MULTI-DISQUES VALIDÉ         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 0. Mesure initiale des disques
$drivesBefore = @{}
Get-PSDrive -PSProvider FileSystem | ForEach-Object {
    $drivesBefore[$_.Root] = $_.Free
}

$freedReport = [System.Collections.Generic.List[PSCustomObject]]::new()

function Safe-DeleteFolder {
    param(
        [string]$Path,
        [string]$Label,
        [string]$Category,
        [bool]$RecreateEmpty = $false
    )

    if (Test-Path $Path) {
        Write-Host "Suppression en cours : $Label ($Path)..." -ForegroundColor Yellow -NoNewline
        $size = (Get-ChildItem $Path -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        if (-not $size) { $size = 0 }

        Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 200

        if ($RecreateEmpty -and (-not (Test-Path $Path))) {
            New-Item -ItemType Directory -Path $Path -Force -ErrorAction SilentlyContinue | Out-Null
        }

        $freedGb = [math]::Round($size / 1GB, 2)
        Write-Host " [OK] ($freedGb Go libérés)" -ForegroundColor Green

        $freedReport.Add([PSCustomObject]@{
            Element = $Label
            Chemin = $Path
            Categorie = $Category
            TailleGo = $freedGb
            Statut = "Supprimé"
        })
    } else {
        Write-Host "Non trouvé / Déjà nettoyé : $Label" -ForegroundColor DarkGray
    }
}

function Safe-DeleteFiles {
    param(
        [string[]]$Paths,
        [string]$Label,
        [string]$Category
    )

    $totalSize = 0
    $count = 0
    foreach ($p in $Paths) {
        if (Test-Path $p) {
            $f = Get-Item $p -Force -ErrorAction SilentlyContinue
            if ($f -and -not $f.PSIsContainer) {
                $totalSize += $f.Length
                $count++
                Remove-Item -Path $p -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($count -gt 0) {
        $freedGb = [math]::Round($totalSize / 1GB, 2)
        Write-Host "Suppression : $Label ($count fichiers, $freedGb Go libérés)" -ForegroundColor Green
        $freedReport.Add([PSCustomObject]@{
            Element = $Label
            Chemin = "$count fichiers"
            Categorie = $Category
            TailleGo = $freedGb
            Statut = "Supprimé"
        })
    } else {
        Write-Host "Aucun fichier trouvé pour : $Label" -ForegroundColor DarkGray
    }
}

# --- 1. DISQUE D:\ ---

# D1. Steam Téléchargements Bloqués
Safe-DeleteFolder -Path "D:\SteamLibrary\steamapps\downloading" `
                  -Label "Steam Téléchargements Bloqués (downloading)" `
                  -Category "Caches D:" `
                  -RecreateEmpty $true

# D2. Paladins PTS (Serveur de test) & son appmanifest
Safe-DeleteFolder -Path "D:\SteamLibrary\steamapps\common\paladins pts" `
                  -Label "Paladins PTS (Serveur de Test)" `
                  -Category "Jeux D:"
if (Test-Path "D:\SteamLibrary\steamapps\appmanifest_596350.acf") {
    Remove-Item "D:\SteamLibrary\steamapps\appmanifest_596350.acf" -Force -ErrorAction SilentlyContinue
}

# D3. Tom Clancy's Rainbow Six Siege
Safe-DeleteFolder -Path "D:\SteamLibrary\steamapps\common\Tom Clancy's Rainbow Six Siege" `
                  -Label "Rainbow Six Siege" `
                  -Category "Jeux D:"

# D4. The Walking Dead The Telltale Definitive Series
Safe-DeleteFolder -Path "D:\Autres Jeux\The.Walking.Dead.The.Telltale.Definitive.Series" `
                  -Label "The Walking Dead Telltale Definitive Series" `
                  -Category "Jeux Portables D:"

# D5. It Takes Two
Safe-DeleteFolder -Path "D:\Autres Jeux\It takes two" `
                  -Label "It Takes Two" `
                  -Category "Jeux Portables D:"

# D6. Phasmophobia Cracké (STG GEGE)
Safe-DeleteFolder -Path "D:\Autres Jeux\Phasmophobia By STG GEGE" `
                  -Label "Phasmophobia Cracké (STG GEGE)" `
                  -Category "Jeux Portables D:"

# D7. Émulateur Android Nox
# Arrêt préalable de tout processus lié
Get-Process -Name "*nox*", "*Bignox*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Safe-DeleteFolder -Path "D:\Program Files\Nox" `
                  -Label "Émulateur Android NoxPlayer" `
                  -Category "Logiciels D:"

# Nettoyage clé de registre Nox
$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Nox",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Nox"
)
foreach ($rp in $regPaths) {
    if (Test-Path $rp) {
        Remove-Item -Path $rp -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Clé registre nettoyée : $rp" -ForegroundColor DarkCyan
    }
}

# D8. Installeurs obsolètes (.exe, .msi) dans D:\Téléchargements
Write-Host "`nPurge des anciens installeurs dans D:\Téléchargements..." -ForegroundColor Yellow
$downloadInstallers = Get-ChildItem "D:\Téléchargements" -File -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -match '^\.(exe|msi)$'
}
if ($downloadInstallers) {
    $installerPaths = $downloadInstallers | ForEach-Object { $_.FullName }
    Safe-DeleteFiles -Paths $installerPaths `
                     -Label "Anciens installeurs .exe et .msi (D:\Téléchargements)" `
                     -Category "Téléchargements D:"
}
# Dossiers résiduels d'installeurs ASUS dans D:\Téléchargements
Safe-DeleteFolder -Path "D:\Téléchargements\ArmouryCrateInstallTool" `
                  -Label "Résidus ArmouryCrateInstallTool" `
                  -Category "Téléchargements D:"
Safe-DeleteFolder -Path "D:\Téléchargements\Armoury_Crate_Uninstall_Tool" `
                  -Label "Résidus Armoury_Crate_Uninstall_Tool" `
                  -Category "Téléchargements D:"


# --- 2. DISQUE E:\ ---

Write-Host "`n--- NETTOYAGE DU DISQUE E:\ ---" -ForegroundColor Cyan

# E1. ROMs Switch Super Smash Bros (E:\Yuzu)
Safe-DeleteFolder -Path "E:\Yuzu" `
                  -Label "ROMs Switch Super Smash Bros (E:\Yuzu)" `
                  -Category "ROMs E:"

# E2. Little Nightmares 1 (Steam) & Little Nightmares II
Safe-DeleteFolder -Path "E:\SteamLibrary\steamapps\common\Little Nightmares" `
                  -Label "Little Nightmares 1 (Steam)" `
                  -Category "Jeux E:"
if (Test-Path "E:\SteamLibrary\steamapps\appmanifest_424840.acf") {
    Remove-Item "E:\SteamLibrary\steamapps\appmanifest_424840.acf" -Force -ErrorAction SilentlyContinue
}

Safe-DeleteFolder -Path "E:\Jeux\Little Nightmares II" `
                  -Label "Little Nightmares II" `
                  -Category "Jeux E:"

# E3. Démos Steam inactives sur E:\
$demos = @(
    @{ Path = "E:\SteamLibrary\steamapps\common\MineGeon Renegades Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_4224830.acf"; Name = "MineGeon Demo" },
    @{ Path = "E:\SteamLibrary\steamapps\common\Kaz Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_3705890.acf"; Name = "Kaz Demo" },
    @{ Path = "E:\SteamLibrary\steamapps\common\Wanderburg Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_4268810.acf"; Name = "Wanderburg Demo" },
    @{ Path = "E:\SteamLibrary\steamapps\common\YAPYAP Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_3997990.acf"; Name = "YAPYAP Demo" },
    @{ Path = "E:\SteamLibrary\steamapps\common\A Game About A Black Hole Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_4041100.acf"; Name = "Black Hole Demo" },
    @{ Path = "E:\SteamLibrary\steamapps\common\Keyboard Warrior Demo"; Acf = "E:\SteamLibrary\steamapps\appmanifest_4385030.acf"; Name = "Keyboard Warrior Demo" }
)

foreach ($d in $demos) {
    Safe-DeleteFolder -Path $d.Path -Label "Démo : $($d.Name)" -Category "Démos E:"
    if (Test-Path $d.Acf) {
        Remove-Item $d.Acf -Force -ErrorAction SilentlyContinue
    }
}


# --- BILAN FINAL ET CALCUL DES GAINS ---
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "                BILAN DU NETTOYAGE EFFECTUÉ                " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

$freedReport | Format-Table Element, Categorie, TailleGo, Statut -AutoSize

$totalFreed = ($freedReport | Measure-Object -Property TailleGo -Sum).Sum

Write-Host "`nEspace total purgé : $([math]::Round($totalFreed, 2)) Go" -ForegroundColor Green

Write-Host "`n=== ÉTAT COMPARATIF DE L'ESPACE DISQUE ===" -ForegroundColor Cyan
Get-PSDrive -PSProvider FileSystem | ForEach-Object {
    $before = $drivesBefore[$_.Root]
    $after = $_.Free
    $diffGb = [math]::Round(($after - $before) / 1GB, 2)
    $freeNowGb = [math]::Round($after / 1GB, 2)
    $usedNowGb = [math]::Round($_.Used / 1GB, 2)
    Write-Host "Disque $($_.Root) -> Libre : $freeNowGb Go (Gain : +$diffGb Go) | Utilisé : $usedNowGb Go" -ForegroundColor White
}
