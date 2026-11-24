# Scan subfolders in User Profile and AppData to find heaviest folders
param(
    [string]$TargetFolder = $env:USERPROFILE,
    [int]$Depth = 1,
    [int]$Top = 25
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== DOSSIERS LES PLUS VOLUMINEUX DANS : $TargetFolder ===" -ForegroundColor Cyan

$subdirs = Get-ChildItem -Path $TargetFolder -Directory -Force -ErrorAction SilentlyContinue

$results = foreach ($dir in $subdirs) {
    # Skip symlinks/junctions
    if ($dir.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
    
    $measure = Get-ChildItem -Path $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum
        
    $sumBytes = if ($null -ne $measure.Sum) { $measure.Sum } else { 0 }
    
    [PSCustomObject]@{
        "Dossier"       = $dir.Name
        "Chemin"        = $dir.FullName
        "Taille (Go)"   = [math]::Round($sumBytes / 1GB, 2)
        "Taille (Mo)"   = [math]::Round($sumBytes / 1MB, 1)
        "Fichiers"      = $measure.Count
        "RawBytes"      = $sumBytes
    }
}

$results | Sort-Object RawBytes -Descending | Select-Object -First $Top | ForEach-Object {
    [PSCustomObject]@{
        "Dossier"     = $_.Dossier
        "Taille (Go)" = "$($_.`"Taille (Go)`") Go"
        "Taille (Mo)" = "$($_.`"Taille (Mo)`") Mo"
        "Fichiers"    = $_.Fichiers
        "Chemin"      = $_.Chemin
    }
} | Format-Table -AutoSize
