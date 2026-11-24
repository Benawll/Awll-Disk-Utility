# Target 2: Remove Android Studio AVD emulators and Android SDK
$ErrorActionPreference = "SilentlyContinue"

$targets = @(
    @{ Name = "Android Virtual Devices (AVD)"; Path = "$env:USERPROFILE\.android\avd" },
    @{ Name = "Android Studio SDK"; Path = "$env:LOCALAPPDATA\Android\Sdk" }
)

foreach ($t in $targets) {
    if (Test-Path $t.Path) {
        Write-Host "Calcul de la taille pour $($t.Name)..." -ForegroundColor Cyan
        $files = Get-ChildItem -Path $t.Path -File -Recurse -Force -ErrorAction SilentlyContinue
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        $gb = [math]::Round($bytes / 1GB, 2)
        Write-Host "Taille : $gb Go dans $($t.Path)" -ForegroundColor Yellow
        
        Write-Host "Suppression en cours..." -ForegroundColor Cyan
        Remove-Item -Path $t.Path -Recurse -Force -ErrorAction SilentlyContinue
        
        if (-not (Test-Path $t.Path)) {
            Write-Host "Succès : $($t.Name) supprimé ($gb Go libérés) !" -ForegroundColor Green
        } else {
            Write-Host "Le dossier existe encore, nettoyage des fichiers restants..." -ForegroundColor Yellow
            Remove-Item -Path "$($t.Path)\*" -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "Dossier déjà absent : $($t.Path)" -ForegroundColor Gray
    }
}
