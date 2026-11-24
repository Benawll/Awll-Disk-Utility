# Scan common developer caches, package managers, and AI model directories
param()

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== SCAN DES CACHES DE DÉVELOPPEMENT ET D'IA ===" -ForegroundColor Cyan

$targets = @(
    @{ Name = "HuggingFace (Modèles IA)"; Path = "$env:USERPROFILE\.cache\huggingface"; SafeToPurge = "Moyen (modèles téléchargés)" },
    @{ Name = "PyTorch Cache"; Path = "$env:USERPROFILE\.cache\torch"; SafeToPurge = "Élevé (re-téléchargeable)" },
    @{ Name = "Pip Cache (Python)"; Path = "$env:LOCALAPPDATA\pip\cache"; SafeToPurge = "Très élevé" },
    @{ Name = "Npm Cache (Node.js)"; Path = "$env:APPDATA\npm-cache"; SafeToPurge = "Très élevé" },
    @{ Name = "Yarn Cache"; Path = "$env:LOCALAPPDATA\Yarn\Cache"; SafeToPurge = "Très élevé" },
    @{ Name = "Cargo Cache (Rust)"; Path = "$env:USERPROFILE\.cargo\registry"; SafeToPurge = "Élevé" },
    @{ Name = "NuGet Packages (.NET)"; Path = "$env:USERPROFILE\.nuget\packages"; SafeToPurge = "Élevé (re-téléchargeable)" },
    @{ Name = "Gradle Caches (Java/Android)"; Path = "$env:USERPROFILE\.gradle\caches"; SafeToPurge = "Élevé" },
    @{ Name = "Maven Repository (.m2)"; Path = "$env:USERPROFILE\.m2\repository"; SafeToPurge = "Élevé" },
    @{ Name = "Android SDK & Emulateurs"; Path = "$env:LOCALAPPDATA\Android\Sdk"; SafeToPurge = "Moyen (contient SDKs/AVD)" },
    @{ Name = "Docker Desktop Data"; Path = "$env:LOCALAPPDATA\Docker\wsl\data"; SafeToPurge = "Moyen (images & conteneurs)" },
    @{ Name = "Unity Package Manager Cache"; Path = "$env:LOCALAPPDATA\Unity\cache"; SafeToPurge = "Élevé" },
    @{ Name = "Unreal Engine DerivedData"; Path = "$env:LOCALAPPDATA\UnrealEngine\Common\DerivedDataCache"; SafeToPurge = "Très élevé" }
)

$results = foreach ($t in $targets) {
    if (Test-Path $t.Path) {
        $measure = Get-ChildItem -Path $t.Path -File -Recurse -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum
        
        $sumBytes = if ($null -ne $measure.Sum) { $measure.Sum } else { 0 }
        
        [PSCustomObject]@{
            "Outil / Cache"   = $t.Name
            "Taille (Go)"     = [math]::Round($sumBytes / 1GB, 2)
            "Taille (Mo)"     = [math]::Round($sumBytes / 1MB, 1)
            "Sécurité Purge"  = $t.SafeToPurge
            "Chemin"          = $t.Path
            "RawBytes"        = $sumBytes
        }
    }
}

$results | Sort-Object RawBytes -Descending | ForEach-Object {
    [PSCustomObject]@{
        "Outil / Cache"   = $_."Outil / Cache"
        "Taille (Go)"     = "$($_."Taille (Go)") Go"
        "Taille (Mo)"     = "$($_."Taille (Mo)") Mo"
        "Niveau de Sécurité"= $_."Sécurité Purge"
        "Chemin"          = $_.Chemin
    }
} | Format-Table -AutoSize

# Docker status check
if (Get-Command "docker" -ErrorAction SilentlyContinue) {
    Write-Host "`n=== ESPACE DOCKER (docker system df) ===" -ForegroundColor Yellow
    try {
        docker system df
    } catch {
        Write-Host "Le daemon Docker n'est pas actif actuellement." -ForegroundColor Gray
    }
}

# WSL status check
if (Get-Command "wsl" -ErrorAction SilentlyContinue) {
    Write-Host "`n=== DISTRIBUTIONS WSL INSTALLÉES (wsl -l -v) ===" -ForegroundColor Yellow
    try {
        wsl -l -v
    } catch {
        Write-Host "Impossible d'interroger WSL." -ForegroundColor Gray
    }
}
