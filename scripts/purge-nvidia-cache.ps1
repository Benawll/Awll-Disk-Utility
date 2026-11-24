# Target 1: Purge NVIDIA ota-artifacts
$ErrorActionPreference = "SilentlyContinue"

$target = "C:\ProgramData\NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts"
if (Test-Path $target) {
    Write-Host "Calcul de la taille de : $target..." -ForegroundColor Cyan
    $files = Get-ChildItem -Path $target -File -Recurse -Force -ErrorAction SilentlyContinue
    $totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
    $gb = [math]::Round($totalBytes / 1GB, 2)
    Write-Host "Taille trouvée : $gb Go ($($files.Count) fichiers)" -ForegroundColor Yellow
    
    Write-Host "Suppression en cours..." -ForegroundColor Cyan
    Remove-Item -Path "$target\*" -Recurse -Force -ErrorAction SilentlyContinue
    
    if (-not (Get-ChildItem -Path $target -Force -ErrorAction SilentlyContinue)) {
        Write-Host "Succès : Dossier ota-artifacts purgé ! $gb Go libérés." -ForegroundColor Green
    } else {
        Write-Host "Dossier partiellement nettoyé (certains fichiers en cours d'utilisation ont été ignorés)." -ForegroundColor Yellow
    }
} else {
    Write-Host "Dossier introuvable : $target" -ForegroundColor Red
}

# Also clean Downloader if present
$dl = "C:\ProgramData\NVIDIA Corporation\Downloader"
if (Test-Path $dl) {
    Remove-Item -Path "$dl\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Dossier NVIDIA Downloader nettoyé." -ForegroundColor Green
}
