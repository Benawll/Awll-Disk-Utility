# Inspect Visual Studio and Unity configuration
$ErrorActionPreference = "SilentlyContinue"

$vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    & $vswhere -products * -format json | ConvertFrom-Json | ForEach-Object {
        Write-Host "Produit : $($_.displayName)" -ForegroundColor Green
        Write-Host "Chemin : $($_.installationPath)" -ForegroundColor Gray
    }
}

# Measure size of Visual Studio folder
$vsPath = "C:\Program Files\Microsoft Visual Studio\18"
if (Test-Path $vsPath) {
    $sz = (Get-ChildItem -Path $vsPath -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    Write-Host "`nTaille de C:\Program Files\Microsoft Visual Studio\18 : $([math]::Round($sz, 2)) Go" -ForegroundColor Yellow
}

# Measure size of Visual Studio installer packages cache in ProgramData
$pkgPath = "C:\ProgramData\Microsoft\VisualStudio\Packages"
if (Test-Path $pkgPath) {
    $szPkg = (Get-ChildItem -Path $pkgPath -File -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
    Write-Host "Taille du cache d'installation Visual Studio (ProgramData) : $([math]::Round($szPkg, 2)) Go" -ForegroundColor Yellow
}

# Check Unity installations
Write-Host "`nRecherche des installations Unity..." -ForegroundColor Cyan
$unityPaths = @("C:\Program Files\Unity\Hub\Editor", "C:\Program Files\Unity", "D:\Unity", "E:\Unity")
foreach ($up in $unityPaths) {
    if (Test-Path $up) {
        Get-ChildItem $up -Directory | ForEach-Object {
            Write-Host "- Version Unity trouvée : $($_.Name) dans $($_.FullName)" -ForegroundColor Green
        }
    }
}
