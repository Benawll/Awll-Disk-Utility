# Inspect deep details of specific Downloads items
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. CONTENU DE Direct Messages.zip (1.05 Go) ===" -ForegroundColor Cyan
if (Test-Path "C:\Users\benja\Downloads\Direct Messages.zip") {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead("C:\Users\benja\Downloads\Direct Messages.zip")
    Write-Host "Nombre total d'entrées : $($zip.Entries.Count)"
    $zip.Entries | Select-Object -First 10 | ForEach-Object {
        Write-Host "  Fichier: $($_.FullName) ($([math]::Round($_.Length/1KB, 1)) Ko)"
    }
    $zip.Dispose()
}

Write-Host "`n=== 2. CONTENU DU DOSSIER test (569 Mo) ===" -ForegroundColor Cyan
if (Test-Path "C:\Users\benja\Downloads\test") {
    Get-ChildItem "C:\Users\benja\Downloads\test" | Select-Object Name, @{N='TailleMo';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime | Format-Table -AutoSize
}

Write-Host "`n=== 3. CONTENU DU DOSSIER dist (353 Mo) ===" -ForegroundColor Cyan
if (Test-Path "C:\Users\benja\Downloads\dist") {
    Get-ChildItem "C:\Users\benja\Downloads\dist" | Select-Object Name, PSIsContainer, LastWriteTime | Format-Table -AutoSize
}

Write-Host "`n=== 4. AUTRES DOSSIERS (Twitch Drops, r2modman, tor) ===" -ForegroundColor Cyan
$folders = @("ebkr-r2modman-3.2.9", "TwitchDropsMiner-updated-master", "Twitch.Drops.Miner.Windows", "mydata~1714949355469", "tor-expert-bundle-windows-i686-14.0.4")
foreach ($f in $folders) {
    $p = Join-Path "C:\Users\benja\Downloads" $f
    if (Test-Path $p) {
        $count = (Get-ChildItem $p -File -Recurse -Force).Count
        $sum = (Get-ChildItem $p -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
        Write-Host "$f : $count fichiers, $([math]::Round($sum/1MB,1)) Mo"
    }
}
