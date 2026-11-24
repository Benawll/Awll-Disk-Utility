# Scan startup programs and background tasks that launch automatically
param()

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== PROGRAMMES AU DÉMARRAGE ET SERVICES D'ARRIÈRE-PLAN ===" -ForegroundColor Cyan

$startupApps = Get-CimInstance Win32_StartupCommand | ForEach-Object {
    [PSCustomObject]@{
        "Nom"        = $_.Name
        "Commande"   = $_.Command
        "Emplacement"= $_.Location
        "Utilisateur"= $_.User
    }
}

$startupApps | Format-Table -AutoSize
