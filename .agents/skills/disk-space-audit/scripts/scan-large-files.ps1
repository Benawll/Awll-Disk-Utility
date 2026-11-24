# Scan for large files across specific folders or drives
param(
    [string[]]$Paths = @($env:USERPROFILE, "D:\"),
    [int64]$MinSizeMB = 500,
    [int]$Top = 30
)

$ErrorActionPreference = "SilentlyContinue"
$minSizeBytes = $MinSizeMB * 1MB

Write-Host "=== RECHERCHE DES FICHIERS > $MinSizeMB Mo ===" -ForegroundColor Cyan
Write-Host "Dossiers analysés : $($Paths -join ', ')" -ForegroundColor DarkGray

$results = foreach ($p in $Paths) {
    if (Test-Path $p) {
        Get-ChildItem -Path $p -File -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { 
                $_.Length -ge $minSizeBytes -and 
                $_.FullName -notmatch '\\AppData\\Local\\Microsoft\\Windows\\Explorer' -and
                $_.FullName -notmatch '\\AppData\\Local\\Packages'
            } |
            Select-Object FullName, Length, LastWriteTime, Extension
    }
}

$results | Sort-Object Length -Descending | Select-Object -First $Top | ForEach-Object {
    [PSCustomObject]@{
        "Taille (Mo)"       = [math]::Round($_.Length / 1MB, 1)
        "Taille (Go)"       = [math]::Round($_.Length / 1GB, 2)
        "Dernier Accès"     = $_.LastWriteTime.ToString("yyyy-MM-dd")
        "Type"              = $_.Extension
        "Chemin du Fichier" = $_.FullName
    }
} | Format-Table -AutoSize
