$regPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$patterns = @("Android Studio", "sql developer", "sqldeveloper", "MindView", "MobiSaver", "EaseUS", "ShadowMaker", "MiniTool", "BlueStacks X", "astah", "BlueSoleil")

$results = foreach ($p in $regPaths) {
    Get-ItemProperty $p -ErrorAction SilentlyContinue | Where-Object {
        $name = $_.DisplayName
        if ($name) {
            foreach ($pat in $patterns) {
                if ($name -match $pat) {
                    [PSCustomObject]@{
                        "Name"            = $name
                        "UninstallString" = $_.UninstallString
                        "QuietUninstall"  = $_.QuietUninstallString
                        "InstallLocation" = $_.InstallLocation
                        "PSPath"          = $_.PSPath
                    }
                    break
                }
            }
        }
    }
}

$results | Format-List
