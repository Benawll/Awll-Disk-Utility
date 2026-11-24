# Deep inspection of huge folders identified on C:
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. ENQUÊTE C:\Users\benja\CrossDevice ===" -ForegroundColor Cyan
Get-ChildItem "C:\Users\benja\CrossDevice" -Force | Select-Object Name, Attributes, LinkType, Target | Format-Table -AutoSize
Get-ChildItem "C:\Users\benja\CrossDevice" -Directory -Force | ForEach-Object {
    $dirName = $_.Name
    $subFiles = Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $subSum = ($subFiles | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        SousDossier = $dirName
        "Taille (Go)" = [math]::Round($subSum / 1GB, 2)
        NbFichiers = $subFiles.Count
    }
} | Format-Table -AutoSize

Write-Host "`n=== 2. ENQUÊTE C:\ProgramData\ASUS (18.27 Go) ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\ASUS" -Directory -Force | ForEach-Object {
    $dirName = $_.Name
    $subFiles = Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $subSum = ($subFiles | Measure-Object -Property Length -Sum).Sum
    if ($subSum -gt 100MB) {
        [PSCustomObject]@{
            SousDossier = $dirName
            "Taille (Go)" = [math]::Round($subSum / 1GB, 2)
            "Taille (Mo)" = [math]::Round($subSum / 1MB, 1)
        }
    }
} | Sort-Object "Taille (Go)" -Descending | Format-Table -AutoSize

Write-Host "`n=== 3. ENQUÊTE C:\Users\benja\AppData\Local\NVIDIA (17.99 Go) ===" -ForegroundColor Cyan
Get-ChildItem "C:\Users\benja\AppData\Local\NVIDIA" -Directory -Force | ForEach-Object {
    $dirName = $_.Name
    $subFiles = Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $subSum = ($subFiles | Measure-Object -Property Length -Sum).Sum
    if ($subSum -gt 100MB) {
        [PSCustomObject]@{
            SousDossier = $dirName
            "Taille (Go)" = [math]::Round($subSum / 1GB, 2)
            "Taille (Mo)" = [math]::Round($subSum / 1MB, 1)
        }
    }
} | Sort-Object "Taille (Go)" -Descending | Format-Table -AutoSize

Write-Host "`n=== 4. ENQUÊTE C:\Jeux dolphin (4.42 Go) ===" -ForegroundColor Cyan
Get-ChildItem "C:\Jeux dolphin" -Recurse -File -Force | Select-Object Name, Length, LastWriteTime | ForEach-Object {
    [PSCustomObject]@{
        Fichier = $_.Name
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        DerniereModif = $_.LastWriteTime.ToString("yyyy-MM-dd")
    }
} | Format-Table -AutoSize

Write-Host "`n=== 5. ENQUÊTE C:\ProgramData\bitmonero (496 Mo) ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\bitmonero" -Recurse -File -Force | Select-Object Name, Length, LastWriteTime | ForEach-Object {
    [PSCustomObject]@{
        Fichier = $_.Name
        "Taille (Mo)" = [math]::Round($_.Length / 1MB, 1)
        DerniereModif = $_.LastWriteTime.ToString("yyyy-MM-dd")
    }
} | Format-Table -AutoSize

Write-Host "`n=== 6. ENQUÊTE C:\ProgramData\Ubisoft (4.49 Go) ===" -ForegroundColor Cyan
Get-ChildItem "C:\ProgramData\Ubisoft" -Directory -Force | ForEach-Object {
    $dirName = $_.Name
    $subFiles = Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue
    $subSum = ($subFiles | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
        SousDossier = $dirName
        "Taille (Go)" = [math]::Round($subSum / 1GB, 2)
        "Taille (Mo)" = [math]::Round($subSum / 1MB, 1)
    }
} | Format-Table -AutoSize
