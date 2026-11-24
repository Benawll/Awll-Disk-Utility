# Audit exhaustif de C:\Users\benja\Downloads
$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      AUDIT EXHAUSTIF DU DOSSIER TÉLÉCHARGEMENTS          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$dlPath = "C:\Users\benja\Downloads"

if (-not (Test-Path $dlPath)) {
    Write-Host "Le dossier $dlPath n'existe pas !" -ForegroundColor Red
    exit
}

$allFiles = Get-ChildItem -Path $dlPath -File -Recurse -Force
$totalSize = ($allFiles | Measure-Object -Property Length -Sum).Sum
$totalSizeGb = [math]::Round($totalSize / 1GB, 2)
$fileCount = $allFiles.Count

Write-Host "Taille totale de $dlPath : $totalSizeGb Go ($fileCount fichiers)" -ForegroundColor Green

# 1. Répartition par extension
Write-Host "`n=== 1. RÉPARTITION PAR TYPE DE FICHIER ===" -ForegroundColor Yellow
$extSummary = $allFiles | Group-Object Extension | ForEach-Object {
    $sum = ($_.Group | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        Extension = if ($_.Name) { $_.Name.ToLower() } else { "(sans extension)" }
        Nombre = $_.Count
        "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
        "Taille (Go)" = [math]::Round($sum / 1GB, 2)
    }
} | Sort-Object "Taille (Mo)" -Descending

$extSummary | Select-Object -First 15 | Format-Table -AutoSize

# 2. Sous-dossiers volumineux dans Downloads
Write-Host "`n=== 2. SOUS-DOSSIERS DANS DOWNLOADS (> 50 Mo) ===" -ForegroundColor Yellow
Get-ChildItem -Path $dlPath -Directory -Force | ForEach-Object {
    $subFiles = Get-ChildItem $_.FullName -File -Recurse -Force
    $sum = ($subFiles | Measure-Object -Property Length -Sum).Sum
    if ($sum -gt 50MB) {
        $last = ($subFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        [PSCustomObject]@{
            Dossier = $_.Name
            Fichiers = $subFiles.Count
            "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
            "Taille (Go)" = [math]::Round($sum / 1GB, 2)
            DerniereModif = if ($last) { $last.ToString("yyyy-MM-dd") } else { "N/A" }
        }
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize

# 3. Tous les fichiers exécutables / installeurs (.exe, .msi, .iso, .bat, .cmd)
Write-Host "`n=== 3. TOUS LES EXÉCUTABLES ET INSTALLEURS (.exe, .msi, .iso) ===" -ForegroundColor Yellow
$installers = $allFiles | Where-Object { $_.Extension -match '^\.(exe|msi|iso|dmg|apk)$' }
$installers | ForEach-Object {
    [PSCustomObject]@{
        Nom = $_.Name
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
        CheminRelatif = $_.FullName.Substring($dlPath.Length)
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize

# 4. Grosses archives (.zip, .rar, .7z, .tar, .gz > 50 Mo)
Write-Host "`n=== 4. ARCHIVES VOLUMINEUSES (> 50 Mo) ===" -ForegroundColor Yellow
$archives = $allFiles | Where-Object { $_.Extension -match '^\.(zip|rar|7z|tar|gz|bz2)$' -and $_.Length -gt 50MB }
$archives | ForEach-Object {
    [PSCustomObject]@{
        Nom = $_.Name
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
        CheminRelatif = $_.FullName.Substring($dlPath.Length)
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize

# 5. Gros fichiers multimédias ou inclassables (> 100 Mo)
Write-Host "`n=== 5. AUTRES GROS FICHIERS (> 100 Mo) HORS INSTALL / ARCHIVES ===" -ForegroundColor Yellow
$others = $allFiles | Where-Object { 
    $_.Extension -notmatch '^\.(exe|msi|iso|zip|rar|7z|tar|gz)$' -and $_.Length -gt 100MB 
}
$others | ForEach-Object {
    [PSCustomObject]@{
        Nom = $_.Name
        Extension = $_.Extension
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
    }
} | Sort-Object "Taille (Mo)" -Descending | Format-Table -AutoSize

# 6. Répartition par ancienneté (< 30j, 1-6 mois, 6-12 mois, > 1 an)
Write-Host "`n=== 6. RÉPARTITION PAR ANCIENNETÉ ===" -ForegroundColor Yellow
$now = Get-Date
$age30d = $allFiles | Where-Object { ($now - $_.LastWriteTime).Days -le 30 }
$age1_6m = $allFiles | Where-Object { ($now - $_.LastWriteTime).Days -gt 30 -and ($now - $_.LastWriteTime).Days -le 180 }
$age6_12m = $allFiles | Where-Object { ($now - $_.LastWriteTime).Days -gt 180 -and ($now - $_.LastWriteTime).Days -le 365 }
$age1yPlus = $allFiles | Where-Object { ($now - $_.LastWriteTime).Days -gt 365 }

[PSCustomObject]@{
    Tranche = "< 30 jours (Récents)"
    Nombre = $age30d.Count
    "Taille (Go)" = [math]::Round(($age30d | Measure-Object -Property Length -Sum).Sum / 1GB, 2)
},
[PSCustomObject]@{
    Tranche = "1 à 6 mois"
    Nombre = $age1_6m.Count
    "Taille (Go)" = [math]::Round(($age1_6m | Measure-Object -Property Length -Sum).Sum / 1GB, 2)
},
[PSCustomObject]@{
    Tranche = "6 à 12 mois"
    Nombre = $age6_12m.Count
    "Taille (Go)" = [math]::Round(($age6_12m | Measure-Object -Property Length -Sum).Sum / 1GB, 2)
},
[PSCustomObject]@{
    Tranche = "> 1 an (Très anciens)"
    Nombre = $age1yPlus.Count
    "Taille (Go)" = [math]::Round(($age1yPlus | Measure-Object -Property Length -Sum).Sum / 1GB, 2)
} | Format-Table -AutoSize
