# Scan all logical drives and report space statistics
param()

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== ÉTAT DES LECTEURS ET ESPACE DISQUE ===" -ForegroundColor Cyan

Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {
    $totalGB = [math]::Round($_.Size / 1GB, 2)
    $freeGB  = [math]::Round($_.FreeSpace / 1GB, 2)
    $usedGB  = [math]::Round(($_.Size - $_.FreeSpace) / 1GB, 2)
    $pctUsed = if ($_.Size -gt 0) { [math]::Round((($_.Size - $_.FreeSpace) / $_.Size) * 100, 1) } else { 0 }
    
    $statusColor = "Green"
    if ($pctUsed -gt 90) { $statusColor = "Red" }
    elseif ($pctUsed -gt 80) { $statusColor = "Yellow" }

    [PSCustomObject]@{
        "Lecteur"     = $_.DeviceID
        "Nom Volume"  = $_.VolumeName
        "Total (Go)"  = $totalGB
        "Utilisé (Go)" = $usedGB
        "Libre (Go)"  = $freeGB
        "% Utilisé"   = "$pctUsed %"
    }
} | Format-Table -AutoSize
