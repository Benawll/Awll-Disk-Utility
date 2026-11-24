# Full accounting of every single gigabyte on C:
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. TOUS LES DOSSIERS À LA RACINE DE C:\ ===" -ForegroundColor Cyan
$rootDirs = Get-ChildItem "C:\" -Directory -Force
$rootResults = foreach ($dir in $rootDirs) {
    # Skip measuring ReparsePoint junctions to external/virtual targets if any
    $bytes = (Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        Nom = $dir.Name
        "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
        Chemin = $dir.FullName
        RawBytes = $bytes
    }
}
$rootResults | Sort-Object RawBytes -Descending | Select-Object Nom, "Taille (Go)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 2. FICHIERS SYSTÈME CACHÉS À LA RACINE DE C:\ (pagefile, hiberfil, etc.) ===" -ForegroundColor Cyan
Get-ChildItem "C:\" -File -Force | ForEach-Object {
    [PSCustomObject]@{
        Fichier = $_.Name
        "Taille (Go)" = [math]::Round($_.Length / 1GB, 2)
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        Attributs = $_.Attributes
    }
} | Format-Table -AutoSize

Write-Host "`n=== 3. VENTILATION DU DOSSIER C:\Users\benja ===" -ForegroundColor Cyan
$userDirs = Get-ChildItem "C:\Users\benja" -Directory -Force
$userResults = foreach ($dir in $userDirs) {
    if ($dir.Name -eq "CrossDevice") {
        # S (2) is a virtual junction to phone, don't count it as C: SSD blocks
        continue
    }
    $bytes = (Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        Dossier = $dir.Name
        "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
        Chemin = $dir.FullName
        RawBytes = $bytes
    }
}
$userResults | Sort-Object RawBytes -Descending | Select-Object Dossier, "Taille (Go)", Chemin | Format-Table -AutoSize

Write-Host "`n=== 4. VENTILATION DE C:\Program Files ===" -ForegroundColor Cyan
$pfDirs = Get-ChildItem "C:\Program Files" -Directory -Force
$pfResults = foreach ($dir in $pfDirs) {
    $bytes = (Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 1GB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            RawBytes = $bytes
        }
    }
}
$pfResults | Sort-Object RawBytes -Descending | Format-Table -AutoSize

Write-Host "`n=== 5. VENTILATION DE C:\Program Files (x86) ===" -ForegroundColor Cyan
$pfx86Dirs = Get-ChildItem "C:\Program Files (x86)" -Directory -Force
$pfx86Results = foreach ($dir in $pfx86Dirs) {
    $bytes = (Get-ChildItem $dir.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($bytes -gt 1GB) {
        [PSCustomObject]@{
            Dossier = $dir.Name
            "Taille (Go)" = [math]::Round($bytes / 1GB, 2)
            RawBytes = $bytes
        }
    }
}
$pfx86Results | Sort-Object RawBytes -Descending | Format-Table -AutoSize
