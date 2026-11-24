# Master Deep Audit Script for Cleaner
# Runs all specialized audits and scores cleanup candidates with Risk %
param(
    [switch]$JsonOutput
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         AUDIT DE DISQUE APPROFONDI & ANALYSE DE RISQUE   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. État des lecteurs
Write-Host "`n[1/4] VÉRIFICATION DE L'ESPACE DISQUE..." -ForegroundColor Yellow
$drives = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {
    $totalGB = [math]::Round($_.Size / 1GB, 2)
    $freeGB = [math]::Round($_.FreeSpace / 1GB, 2)
    $usedGB = [math]::Round($totalGB - $freeGB, 2)
    $pctUsed = if ($totalGB -gt 0) { [math]::Round(($usedGB / $totalGB) * 100, 1) } else { 0 }
    [PSCustomObject]@{
        Lecteur     = $_.DeviceID
        "Total (Go)"= $totalGB
        "Libre (Go)"= $freeGB
        "Utilisé (Go)" = $usedGB
        "% Utilisé" = "$pctUsed %"
    }
}
$drives | Format-Table -AutoSize

# 2. Caches Développeurs et IA
Write-Host "`n[2/4] SCAN DES CACHES DE DÉVELOPPEMENT & RUNTIMES..." -ForegroundColor Yellow
$devTargets = @(
    @{ Name = "Pip Cache (Python)"; Path = "$env:LOCALAPPDATA\pip\cache"; Risk = 0 },
    @{ Name = "Npm Cache (Node.js)"; Path = "$env:APPDATA\npm-cache"; Risk = 0 },
    @{ Name = "Yarn Cache"; Path = "$env:LOCALAPPDATA\Yarn\Cache"; Risk = 0 },
    @{ Name = "Cargo Cache (Rust)"; Path = "$env:USERPROFILE\.cargo\registry"; Risk = 5 },
    @{ Name = "Gradle Caches"; Path = "$env:USERPROFILE\.gradle\caches"; Risk = 5 },
    @{ Name = "Maven Repository (.m2)"; Path = "$env:USERPROFILE\.m2\repository"; Risk = 5 },
    @{ Name = "HuggingFace (Modèles IA)"; Path = "$env:USERPROFILE\.cache\huggingface"; Risk = 30 },
    @{ Name = "PyTorch Cache"; Path = "$env:USERPROFILE\.cache\torch"; Risk = 10 },
    @{ Name = "Unity Package Cache"; Path = "$env:LOCALAPPDATA\Unity\cache"; Risk = 10 }
)

$devFound = foreach ($t in $devTargets) {
    if (Test-Path $t.Path) {
        $files = Get-ChildItem -Path $t.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        if ($bytes -gt 10MB) {
            [PSCustomObject]@{
                Cible        = $t.Name
                "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
                "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
                "Risque %"   = "$($t.Risk)%"
                Chemin       = $t.Path
            }
        }
    }
}
if ($devFound) {
    $devFound | Format-Table -AutoSize
} else {
    Write-Host "Aucun cache développeur volumineux (>10 Mo) résiduel." -ForegroundColor Green
}

# 3. Fichiers Temporaires Système
Write-Host "`n[3/4] SCAN DES TEMPORAIRES & LOGS SYSTÈME..." -ForegroundColor Yellow
$tempTargets = @(
    @{ Name = "User Temp"; Path = "$env:TEMP"; Risk = 0 },
    @{ Name = "Windows Temp"; Path = "$env:windir\Temp"; Risk = 0 },
    @{ Name = "Crash Dumps Windows"; Path = "$env:LOCALAPPDATA\CrashDumps"; Risk = 0 }
)
$tempFound = foreach ($t in $tempTargets) {
    if (Test-Path $t.Path) {
        $files = Get-ChildItem -Path $t.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        if ($bytes -gt 50MB) {
            [PSCustomObject]@{
                Zone         = $t.Name
                "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
                "Risque %"   = "$($t.Risk)%"
                Chemin       = $t.Path
            }
        }
    }
}
if ($tempFound) {
    $tempFound | Format-Table -AutoSize
} else {
    Write-Host "Temporaires système propres (<50 Mo)." -ForegroundColor Green
}

# 4. Éléments Restants et Évaluation des Risques
Write-Host "`n[4/4] CANDIDATS DE NETTOYAGE & RISQUE ASSOCIÉ..." -ForegroundColor Yellow
$candidates = @(
    @{
        Nom = "Thunderstore Mod Manager (Roaming)"
        Path = "$env:APPDATA\Thunderstore Mod Manager"
        Type = "Mod Manager"
        Risk = 5
        Justification = "Mods de jeux installés. Risque quasi nul si les jeux associés ne sont plus moddés."
    },
    @{
        Nom = "OpenAI Codex App"
        Path = "$env:LOCALAPPDATA\Programs\OpenAI Codex"
        Type = "Application"
        Risk = 65
        Justification = "Actif récemment (service actif ou modification récente). Risque moyen à élevé si utilisé."
    },
    @{
        Nom = "Radmin VPN"
        Path = "C:\Program Files (x86)\Radmin VPN"
        Type = "Logiciel VPN"
        Risk = 0
        Justification = "VPN de réseau local de jeu. Zéro processus en cours. Zéro risque de casse système."
    }
)

$candidatesEvaluated = foreach ($c in $candidates) {
    if (Test-Path $c.Path) {
        $files = Get-ChildItem -Path $c.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $sizeGB = [math]::Round($bytes / 1GB, 2)
        $sizeMB = [math]::Round($bytes / 1MB, 1)

        [PSCustomObject]@{
            "Élément"       = $c.Nom
            "Taille (Go)"   = $sizeGB
            "Taille (Mo)"   = $sizeMB
            "Risque"        = "$($c.Risk)%"
            "Évaluation"    = $c.Justification
        }
    }
}

if ($candidatesEvaluated) {
    $candidatesEvaluated | Format-Table -AutoSize
} else {
    Write-Host "Tous les candidats audités ont été traités." -ForegroundColor Green
}

Write-Host "`nAudit terminé avec succès." -ForegroundColor Cyan
