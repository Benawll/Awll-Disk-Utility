# Scan Program Files and Program Files (x86) top directories
$ErrorActionPreference = "SilentlyContinue"

$progDirs = @("C:\Program Files", "C:\Program Files (x86)")
$results = foreach ($pd in $progDirs) {
    Get-ChildItem -Path $pd -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { return }
        $sz = (Get-ChildItem -Path $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{
            "Dossier"    = $_.Name
            "Emplacement"= $pd
            "TailleMo"   = [math]::Round($sz, 1)
            "TailleGo"   = [math]::Round($sz / 1024, 2)
            "Chemin"     = $_.FullName
        }
    }
}

$results | Where-Object { $_.TailleMo -gt 150 } | Sort-Object TailleMo -Descending | Format-Table -AutoSize
