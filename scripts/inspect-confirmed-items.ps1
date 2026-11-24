# Verification of exact paths for user's confirmed selections
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== VERIFICATION DES CIBLES DU DISQUE D: ===" -ForegroundColor Cyan
# D:\SteamLibrary\steamapps\downloading
$dDownloading = "D:\SteamLibrary\steamapps\downloading"
if (Test-Path $dDownloading) {
    $sum = (Get-ChildItem $dDownloading -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "OK: $dDownloading -> $([math]::Round($sum/1GB, 2)) Go"
} else { Write-Host "NON TROUVÉ: $dDownloading" -ForegroundColor Red }

# Paladins PTS
Get-ChildItem "D:\SteamLibrary\steamapps\common" -Directory -Filter "*paladins*" | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "Trouvé: $($_.FullName) -> $([math]::Round($sum/1GB, 2)) Go"
}

# Rainbow Six Siege
Get-ChildItem "D:\SteamLibrary\steamapps\common" -Directory -Filter "*Rainbow*" | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "Trouvé: $($_.FullName) -> $([math]::Round($sum/1GB, 2)) Go"
}

# D:\Autres Jeux (The Walking Dead, It Takes Two, Phasmophobia)
Write-Host "`n=== D:\Autres Jeux ===" -ForegroundColor Cyan
if (Test-Path "D:\Autres Jeux") {
    Get-ChildItem "D:\Autres Jeux" -Directory | ForEach-Object {
        $sum = (Get-ChildItem $_.FullName -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
        Write-Host "Trouvé: $($_.FullName) -> $([math]::Round($sum/1GB, 2)) Go"
    }
}

# Nox
$nox = "D:\Program Files\Nox"
if (Test-Path $nox) {
    $sum = (Get-ChildItem $nox -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "`nTrouvé: $nox -> $([math]::Round($sum/1GB, 2)) Go"
}

# D:\Téléchargements old installers
Write-Host "`n=== D:\Téléchargements (Installeurs) ===" -ForegroundColor Cyan
if (Test-Path "D:\Téléchargements") {
    Get-ChildItem "D:\Téléchargements" -File -Recurse -Force | Where-Object { 
        $_.Extension -match '^\.(exe|msi|iso)$' -or $_.Name -match '(docker|android-studio|icue|nvidia|geforce|wamp|phpstorm|virtualbox)'
    } | ForEach-Object {
        Write-Host "Installeur: $($_.FullName) ($([math]::Round($_.Length/1MB, 1)) Mo) - $($_.LastWriteTime.ToString('yyyy-MM-dd'))"
    }
}

Write-Host "`n=== VERIFICATION DES CIBLES DU DISQUE E: ===" -ForegroundColor Cyan
# E:\Yuzu
$yuzu = "E:\Yuzu"
if (Test-Path $yuzu) {
    $sum = (Get-ChildItem $yuzu -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "Trouvé: $yuzu -> $([math]::Round($sum/1GB, 2)) Go"
}

# Little Nightmares (Steam or other)
Get-ChildItem "E:\SteamLibrary\steamapps\common", "E:\Jeux" -Directory -Filter "*Little*Nightmares*" | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "Trouvé: $($_.FullName) -> $([math]::Round($sum/1GB, 2)) Go"
}

# Démos Steam sur E:
Write-Host "`n=== Démos Steam E: ===" -ForegroundColor Cyan
Get-ChildItem "E:\SteamLibrary\steamapps\common" -Directory | Where-Object { 
    $_.Name -match '(demo|MineGeon|Kaz|Wanderburg|YAPYAP)' 
} | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
    Write-Host "Trouvé Démo: $($_.FullName) -> $([math]::Round($sum/1GB, 2)) Go"
}
