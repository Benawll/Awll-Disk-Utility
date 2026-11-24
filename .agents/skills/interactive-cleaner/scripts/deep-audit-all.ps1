# Master Deep Audit Script for Cleaner
# Performs an exhaustive threshold scan across AppData (Local, Roaming, LocalLow), User Profile, System & Root
param(
    [int]$ThresholdMB = 150
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AUDIT DYNAMIQUE EXHAUSTIF DU LECTEUR C:            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. État des lecteurs
Write-Host "`n[1/5] VÉRIFICATION DE L'ESPACE DISQUE..." -ForegroundColor Yellow
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

# 2. Scan dynamique de AppData\Local (> ThresholdMB)
Write-Host "`n[2/5] SCAN DYNAMIQUE DE APPDATA\LOCAL (> $ThresholdMB Mo)..." -ForegroundColor Yellow
$localDirs = Get-ChildItem "$env:LOCALAPPDATA" -Directory -Force
$localResults = foreach ($dir in $localDirs) {
    if ($dir.Name -match '^(Microsoft|Packages|Programs)$') { continue }
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if (($bytes / 1MB) -ge $ThresholdMB) {
        $risk = 10
        if ($dir.Name -match '(cache|temp|log|dump|updater)' -or $dir.Name -in @('VortxEngine','WhirlwindFX','Medal','VoicemodV3','MultiVersus','yuzu')) {
            $risk = 5
        }
        [PSCustomObject]@{
            Dossier      = "AppData\Local\$($dir.Name)"
            "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
            "Risque %"   = "$risk%"
            RawBytes     = $bytes
        }
    }
}
$localResults | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize

# 3. Scan dynamique de AppData\Roaming (> ThresholdMB)
Write-Host "`n[3/5] SCAN DYNAMIQUE DE APPDATA\ROAMING (> $ThresholdMB Mo)..." -ForegroundColor Yellow
$roamDirs = Get-ChildItem "$env:APPDATA" -Directory -Force
$roamResults = foreach ($dir in $roamDirs) {
    if ($dir.Name -match '^(Microsoft)$') { continue }
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if (($bytes / 1MB) -ge $ThresholdMB) {
        $risk = 15
        if ($dir.Name -in @('Thunderstore Mod Manager','r2modman','r2modmanPlus-local','EasyAntiCheat')) { $risk = 5 }
        if ($dir.Name -in @('Slack','Pulse Secure','Rocket.Chat','TabNine')) { $risk = 10 }
        [PSCustomObject]@{
            Dossier      = "AppData\Roaming\$($dir.Name)"
            "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
            "Risque %"   = "$risk%"
            RawBytes     = $bytes
        }
    }
}
$roamResults | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize

# 4. Scan dynamique de AppData\LocalLow (> 100 Mo)
Write-Host "`n[4/5] SCAN DYNAMIQUE DE APPDATA\LOCALLOW (> 100 Mo)..." -ForegroundColor Yellow
$lowDirs = Get-ChildItem "$env:USERPROFILE\AppData\LocalLow" -Directory -Force
$lowResults = foreach ($dir in $lowDirs) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if (($bytes / 1MB) -ge 100) {
        $risk = 5
        [PSCustomObject]@{
            Dossier      = "AppData\LocalLow\$($dir.Name)"
            "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
            "Risque %"   = "$risk%"
            RawBytes     = $bytes
        }
    }
}
$lowResults | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize

# 5. Scan dynamique du profil C:\Users\benja (dossiers cachés et racine utilisateur > 100 Mo)
Write-Host "`n[5/5] SCAN DYNAMIQUE DU PROFIL UTILISATEUR (> 100 Mo)..." -ForegroundColor Yellow
$userDirs = Get-ChildItem "$env:USERPROFILE" -Directory -Force | Where-Object {
    $_.Name -notmatch '^(AppData|Videos|Downloads|New Doc|CrossDevice|Documents|Pictures|Music|Desktop)$'
}
$userResults = foreach ($dir in $userDirs) {
    $files = Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    if (($bytes / 1MB) -ge 100) {
        $risk = 10
        if ($dir.Name -in @('.gradle','.m2','Skyblock Hypixel','career-ops-data')) { $risk = 5 }
        if ($dir.Name -in @('.lunarclient','curseforge')) { $risk = 10 }
        if ($dir.Name -in @('.jdks')) { $risk = 80 } # Sanctuarisé (Java 17 / 24)
        [PSCustomObject]@{
            Dossier      = "Profil\$($dir.Name)"
            "Taille (Go)"= [math]::Round($bytes / 1GB, 2)
            "Taille (Mo)"= [math]::Round($bytes / 1MB, 1)
            "Risque %"   = "$risk%"
            RawBytes     = $bytes
        }
    }
}
$userResults | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", "Taille (Mo)", "Risque %" | Format-Table -AutoSize

Write-Host "`nAudit dynamique complet terminé." -ForegroundColor Cyan
