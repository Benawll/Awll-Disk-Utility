# Identify large programs, game directories, redundant runtimes and unused tools
param(
    [int]$MinSizeMB = 200
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== APPLICATIONS ET OUTILS VOLUMINEUX (> $MinSizeMB Mo) ===" -ForegroundColor Cyan

$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$apps = foreach ($path in $regPaths) {
    Get-ItemProperty $path -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -and 
        $_.SystemComponent -ne 1 -and 
        $_.EstimatedSize -and
        ($_.EstimatedSize / 1024) -ge $MinSizeMB
    } | ForEach-Object {
        $sizeMB = [math]::Round($_.EstimatedSize / 1024, 1)
        $sizeGB = [math]::Round($sizeMB / 1024, 2)
        [PSCustomObject]@{
            "Nom"             = $_.DisplayName.Trim()
            "Éditeur"         = if ($_.Publisher) { $_.Publisher.Trim() } else { "Inconnu" }
            "Taille (Go)"     = $sizeGB
            "Taille (Mo)"     = $sizeMB
            "Date Install"    = $_.InstallDate
            "UninstallString" = $_.UninstallString
        }
    }
}

$heavyApps = $apps | Group-Object Nom | ForEach-Object { $_.Group[0] } | Sort-Object "Taille (Mo)" -Descending

$heavyApps | ForEach-Object {
    [PSCustomObject]@{
        "Application"  = $_.Nom
        "Éditeur"      = $_.Éditeur
        "Taille (Go)"  = "$($_.`"Taille (Go)`") Go"
        "Taille (Mo)"  = "$($_.`"Taille (Mo)`") Mo"
        "Désinstalleur"= if ($_.UninstallString) { "Disponible" } else { "Non spécifié" }
    }
} | Format-Table -AutoSize

Write-Host "`n=== DÉTECTION DES RUNTIMES MULTIPLES (Java, Python, .NET, Node) ===" -ForegroundColor Yellow

$runtimes = @(
    @{ Name = "Python"; Pattern = "Python\s+[0-9]" },
    @{ Name = "Java JDK/JRE"; Pattern = "(Java|JDK|JRE|OpenJDK)" },
    @{ Name = ".NET Core / SDK"; Pattern = "\.NET\s+(Core|SDK|Runtime)" },
    @{ Name = "Node.js"; Pattern = "Node\.js" }
)

foreach ($rt in $runtimes) {
    $matches = $heavyApps + ($apps | Group-Object Nom | ForEach-Object { $_.Group[0] }) | 
        Where-Object { $_.Nom -match $rt.Pattern } |
        Select-Object Nom, Éditeur, "Taille (Mo)"
    
    if ($matches.Count -gt 1) {
        Write-Host "Alerte : Plusieurs versions détectées pour $($rt.Name) ($($matches.Count) versions) :" -ForegroundColor Red
        $matches | Format-Table -AutoSize
    }
}
