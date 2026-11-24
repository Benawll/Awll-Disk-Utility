# Script d'exécution silencieux et sécurisé pour le nettoyage du dossier Downloads
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      NETTOYAGE SÉCURISÉ DE C:\Users\benja\Downloads       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$dlPath = "C:\Users\benja\Downloads"
$initialFree = (Get-PSDrive C).Free

$freedReport = [System.Collections.Generic.List[PSCustomObject]]::new()

# 1. Suppression des installeurs .exe, .msi et .AppImage à la racine de Downloads
Write-Host "1. Purge des installeurs (.exe, .msi, .AppImage) à la racine..." -ForegroundColor Yellow
$installers = Get-ChildItem -Path $dlPath -File -Force | Where-Object { 
    $_.Extension -match '^\.(exe|msi|AppImage)$'
}

$instTotal = 0
$instCount = 0
foreach ($inst in $installers) {
    $size = $inst.Length
    $instTotal += $size
    $instCount++
    Remove-Item -Path $inst.FullName -Force -ErrorAction SilentlyContinue
}

$instGb = [math]::Round($instTotal / 1GB, 2)
Write-Host "   -> $instCount installeurs supprimés ($instGb Go libérés)" -ForegroundColor Green

$freedReport.Add([PSCustomObject]@{
    Element = "Installeurs & Exécutables (.exe, .msi, .AppImage)"
    Details = "$instCount fichiers à la racine de Downloads"
    TailleGo = $instGb
    Statut = "Supprimé"
})

# 2. Dossier extrait r2modman
$r2folder = Join-Path $dlPath "ebkr-r2modman-3.2.9"
if (Test-Path $r2folder) {
    Write-Host "2. Suppression du dossier extrait r2modman..." -ForegroundColor Yellow
    $r2Size = (Get-ChildItem $r2folder -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Remove-Item -Path $r2folder -Recurse -Force -ErrorAction SilentlyContinue
    $r2Gb = [math]::Round($r2Size / 1GB, 2)
    Write-Host "   -> Dossier ebkr-r2modman-3.2.9 supprimé ($r2Gb Go libérés)" -ForegroundColor Green
    $freedReport.Add([PSCustomObject]@{
        Element = "Dossier extrait ebkr-r2modman-3.2.9"
        Details = "Dossier non zippé"
        TailleGo = $r2Gb
        Statut = "Supprimé"
    })
}

# 3. Archive minecraft survie.zip (doublon du dossier extrait)
$mcZip = Join-Path $dlPath "minecraft survie.zip"
if (Test-Path $mcZip) {
    Write-Host "3. Suppression du doublon minecraft survie.zip..." -ForegroundColor Yellow
    $mcSize = (Get-Item $mcZip).Length
    Remove-Item -Path $mcZip -Force -ErrorAction SilentlyContinue
    $mcGb = [math]::Round($mcSize / 1GB, 2)
    Write-Host "   -> minecraft survie.zip supprimé ($mcGb Go libérés, dossier extrait conservé)" -ForegroundColor Green
    $freedReport.Add([PSCustomObject]@{
        Element = "minecraft survie.zip (Doublon)"
        Details = "Dossier extrait minecraft survie strictement conservé"
        TailleGo = $mcGb
        Statut = "Supprimé"
    })
}

# 4. Archives Better MC et succubus dashboard
$archives = @(
    "Better MC [FORGE] 1.20.1 v39.zip",
    "succubus-dashboard-main.zip"
)
foreach ($archName in $archives) {
    $archPath = Join-Path $dlPath $archName
    if (Test-Path $archPath) {
        $aSize = (Get-Item $archPath).Length
        Remove-Item -Path $archPath -Force -ErrorAction SilentlyContinue
        $aGb = [math]::Round($aSize / 1GB, 2)
        Write-Host "4. Suppression de $archName ($aGb Go libérés)..." -ForegroundColor Green
        $freedReport.Add([PSCustomObject]@{
            Element = $archName
            Details = "Archive obsolète"
            TailleGo = $aGb
            Statut = "Supprimé"
        })
    }
}

# Bilan
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "                BILAN DU NETTOYAGE DOWNLOADS               " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

$freedReport | Format-Table Element, Details, TailleGo, Statut -AutoSize

$totalFreedGb = ($freedReport | Measure-Object -Property TailleGo -Sum).Sum
Write-Host "`nTotal libéré dans Downloads : $([math]::Round($totalFreedGb, 2)) Go" -ForegroundColor Green

$finalFree = (Get-PSDrive C).Free
$gainC = [math]::Round(($finalFree - $initialFree) / 1GB, 2)
$currentFreeC = [math]::Round($finalFree / 1GB, 2)

Write-Host "Disque C:\ -> Libre maintenant : $currentFreeC Go (Gain sur ce run : +$gainC Go)" -ForegroundColor Cyan
