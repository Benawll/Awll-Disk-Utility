# Target: Purge user caches (CrossDevice 28GB, FortniteGame 3.8GB, JetBrains caches 3.5GB, RLBotGUIX 1.9GB)
$ErrorActionPreference = "SilentlyContinue"

$caches = @(
    @{ Name = "CrossDevice (Synchro mobile)"; Path = "$env:USERPROFILE\CrossDevice" },
    @{ Name = "FortniteGame Caches"; Path = "$env:LOCALAPPDATA\FortniteGame" },
    @{ Name = "JetBrains Caches (Local)"; Path = "$env:LOCALAPPDATA\JetBrains" },
    @{ Name = "RLBotGUIX (Rocket League bots)"; Path = "$env:LOCALAPPDATA\RLBotGUIX" }
)

$totalFreedBytes = 0

foreach ($c in $caches) {
    if (Test-Path $c.Path) {
        Write-Host "Calcul de la taille pour $($c.Name)..." -ForegroundColor Cyan
        $files = Get-ChildItem -Path $c.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $gb = [math]::Round($bytes / 1GB, 2)
        Write-Host "Taille trouvée : $gb Go dans $($c.Path)" -ForegroundColor Yellow
        
        Write-Host "Suppression en cours..." -ForegroundColor Cyan
        Remove-Item -Path $c.Path -Recurse -Force -ErrorAction SilentlyContinue
        
        if (-not (Test-Path $c.Path)) {
            Write-Host "Succès : $($c.Name) supprimé ($gb Go libérés) !" -ForegroundColor Green
            $totalFreedBytes += $bytes
        } else {
            Remove-Item -Path "$($c.Path)\*" -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "$($c.Name) purgé en profondeur." -ForegroundColor Green
            $totalFreedBytes += $bytes
        }
    } else {
        Write-Host "Dossier déjà absent : $($c.Path)" -ForegroundColor Gray
    }
}

$totGB = [math]::Round($totalFreedBytes / 1GB, 2)
Write-Host "`nTotal caches purgés avec succès : $totGB Go !" -ForegroundColor Green
