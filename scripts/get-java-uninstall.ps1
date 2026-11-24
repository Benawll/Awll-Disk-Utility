# Find uninstall strings for Java versions
$ErrorActionPreference = "SilentlyContinue"

$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$results = foreach ($p in $regPaths) {
    Get-ItemProperty $p -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -match "Java" -or $_.DisplayName -match "JDK"
    } | ForEach-Object {
        [PSCustomObject]@{
            "Name"            = $_.DisplayName
            "Version"         = $_.DisplayVersion
            "UninstallString" = $_.UninstallString
            "PSPath"          = $_.PSPath
        }
    }
}

$results | Format-List
