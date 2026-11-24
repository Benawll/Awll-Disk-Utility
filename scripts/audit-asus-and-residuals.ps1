# Check Asus Armoury Crate Diagnosis and CrossDevice actual disk space
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. CONTENU DE C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis" -Force | ForEach-Object {
    $sum = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        Nom = $_.Name
        "Taille (Go)" = [math]::Round($sum / 1GB, 2)
        "Taille (Mo)" = [math]::Round($sum / 1MB, 1)
        Date = $_.LastWriteTime.ToString("yyyy-MM-dd")
    }
} | Format-Table -AutoSize

Write-Host "`n=== 2. TAILLE RÉELLE DU DISQUE C: AVANT / APRES SIMULATION ===" -ForegroundColor Cyan
$cDrive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
Write-Host "Espace Libre C: : $([math]::Round($cDrive.FreeSpace / 1GB, 2)) Go" -ForegroundColor Yellow

Write-Host "`n=== 3. VÉRIFICATION DES LOGS OU RESIDUS DANS C:\Program Files (x86)\Microsoft Visual Studio ===" -ForegroundColor Cyan
Get-ChildItem "C:\Program Files (x86)\Microsoft Visual Studio" -Recurse -File -Force | Measure-Object -Property Length -Sum | ForEach-Object {
    Write-Host "Total fichiers résiduels VS : $([math]::Round($_.Sum / 1MB, 1)) Mo ($($_.Count) fichiers)"
}

Write-Host "`n=== 4. VÉRIFICATION C:\Jts et C:\iMyfone ===" -ForegroundColor Cyan
Get-ChildItem "C:\Jts" -Recurse -File -Force | Measure-Object -Property Length -Sum | ForEach-Object {
    Write-Host "C:\Jts : $([math]::Round($_.Sum / 1MB, 1)) Mo"
}
Get-ChildItem "C:\iMyfone" -Recurse -File -Force | Measure-Object -Property Length -Sum | ForEach-Object {
    Write-Host "C:\iMyfone : $([math]::Round($_.Sum / 1MB, 1)) Mo"
}
