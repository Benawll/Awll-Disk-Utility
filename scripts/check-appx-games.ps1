Get-AppxPackage | Where-Object { 
    $_.Name -match "SeaofThieves|COREBase|PowerBI|VampireSurvivors|WinDbg" 
} | Select-Object Name, PackageFullName, InstallLocation | Format-Table -AutoSize
