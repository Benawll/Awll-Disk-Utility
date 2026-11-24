# Extract installed programs from Windows Registry (64-bit, 32-bit, and Current User)
param(
    [string]$Filter = "",
    [switch]$OnlyWithSize,
    [int]$Top = 50
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== INVENTAIRE DES LOGICIELS INSTALLÉS ===" -ForegroundColor Cyan

$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$apps = foreach ($path in $regPaths) {
    Get-ItemProperty $path -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -and 
        $_.SystemComponent -ne 1 -and 
        $_.ParentKeyName -eq $null -and
        $_.DisplayName -notmatch '^KB[0-9]{6,}' -and
        $_.DisplayName -notmatch 'Update for'
    } | ForEach-Object {
        $sizeMB = if ($_.EstimatedSize) { [math]::Round($_.EstimatedSize / 1024, 1) } else { $null }
        $installDate = $_.InstallDate
        if ($installDate -match '^[0-9]{8}$') {
            $installDate = "$($installDate.Substring(0,4))-$($installDate.Substring(4,2))-$($installDate.Substring(6,2))"
        }

        [PSCustomObject]@{
            "Nom"             = $_.DisplayName.Trim()
            "Éditeur"         = if ($_.Publisher) { $_.Publisher.Trim() } else { "Inconnu" }
            "Version"         = $_.DisplayVersion
            "Taille (Mo)"     = $sizeMB
            "Taille (Go)"     = if ($sizeMB) { [math]::Round($sizeMB / 1024, 2) } else { $null }
            "Date Install"    = $installDate
            "UninstallString" = $_.UninstallString
        }
    }
}

# Deduplicate by name
$uniqueApps = $apps | Group-Object Nom | ForEach-Object { $_.Group[0] }

if ($Filter) {
    $uniqueApps = $uniqueApps | Where-Object { $_.Nom -match $Filter -or $_.Éditeur -match $Filter }
}

if ($OnlyWithSize) {
    $uniqueApps = $uniqueApps | Where-Object { $null -ne $_."Taille (Mo)" } | Sort-Object "Taille (Mo)" -Descending
} else {
    $uniqueApps = $uniqueApps | Sort-Object Nom
}

$uniqueApps | Select-Object -First $Top | ForEach-Object {
    [PSCustomObject]@{
        "Nom de l'application" = $_.Nom
        "Éditeur"              = $_.Éditeur
        "Version"              = $_.Version
        "Taille (Mo)"          = if ($_."Taille (Mo)") { "$($_."Taille (Mo)") Mo" } else { "-" }
        "Date Installation"    = if ($_."Date Install") { $_."Date Install" } else { "-" }
    }
} | Format-Table -AutoSize
