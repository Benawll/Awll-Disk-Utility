# Check uninstall commands for BlueStacks and WampServer
$ErrorActionPreference = "SilentlyContinue"

$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$results = foreach ($p in $regPaths) {
    Get-ItemProperty $p -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -match "BlueStacks" -or $_.DisplayName -match "Wamp"
    } | ForEach-Object {
        [PSCustomObject]@{
            "DisplayName"     = $_.DisplayName
            "UninstallString" = $_.UninstallString
            "QuietUninstall"  = $_.QuietUninstallString
            "InstallLocation" = $_.InstallLocation
        }
    }
}
$results | Format-List
