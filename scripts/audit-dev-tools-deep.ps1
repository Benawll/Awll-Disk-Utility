# Deep audit of JetBrains, VS Code, Visual Studio and Office
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== AUDIT DÉTAILLÉ DE JETBRAINS ===" -ForegroundColor Cyan
if (Test-Path "C:\Program Files\JetBrains") {
    Get-ChildItem -Path "C:\Program Files\JetBrains" -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
        [PSCustomObject]@{ "Application / Version" = $_.Name; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
    } | Format-Table -AutoSize
}

Write-Host "`n=== AUDIT DÉTAILLÉ DE VISUAL STUDIO ET MICROSOFT ===" -ForegroundColor Cyan
if (Test-Path "C:\Program Files\Microsoft Visual Studio") {
    Get-ChildItem -Path "C:\Program Files\Microsoft Visual Studio" -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
        [PSCustomObject]@{ "Version Visual Studio" = $_.Name; "Taille (Go)" = [math]::Round($sz, 2); "Chemin" = $_.FullName }
    } | Format-Table -AutoSize
}

Write-Host "`n=== AUDIT DÉTAILLÉ DE VS CODE (Extensions & App) ===" -ForegroundColor Cyan
$vsExtPath = "$env:USERPROFILE\.vscode\extensions"
if (Test-Path $vsExtPath) {
    $exts = Get-ChildItem -Path $vsExtPath -Directory | ForEach-Object {
        $sz = (Get-ChildItem $_.FullName -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        [PSCustomObject]@{ "Extension" = $_.Name; "Taille (Mo)" = [math]::Round($sz, 1) }
    }
    $totExtMB = ($exts | Measure-Object -Property "Taille (Mo)" -Sum).Sum
    Write-Host "Nombre total d'extensions VS Code : $($exts.Count) (Taille totale : $([math]::Round($totExtMB / 1024, 2)) Go)" -ForegroundColor Yellow
    Write-Host "Top 8 des extensions les plus lourdes :" -ForegroundColor DarkGray
    $exts | Sort-Object "Taille (Mo)" -Descending | Select-Object -First 8 | Format-Table -AutoSize
}
