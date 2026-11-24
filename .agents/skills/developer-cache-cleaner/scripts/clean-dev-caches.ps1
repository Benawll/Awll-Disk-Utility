# Safe purge script for developer caches with DryRun by default
param(
    [ValidateSet("pip", "npm", "yarn", "nuget", "all-safe")]
    [string]$Target = "pip",
    [switch]$Execute  # Must be explicitly set to perform deletion
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== PURGE DES CACHES DE DÉVELOPPEMENT ===" -ForegroundColor Cyan

if (-not $Execute) {
    Write-Host "[MODE SIMULATION / DRY-RUN] Aucun fichier ne sera supprimé." -ForegroundColor Yellow
    Write-Host "Pour exécuter réellement la suppression, ajoutez le flag -Execute." -ForegroundColor Yellow
}

function Clear-PipCache {
    Write-Host "-> Vérification Pip Cache..." -ForegroundColor Cyan
    if (Get-Command "pip" -ErrorAction SilentlyContinue) {
        if ($Execute) {
            Write-Host "Exécution: pip cache purge" -ForegroundColor Green
            pip cache purge
        } else {
            Write-Host "Commande simulée: pip cache purge" -ForegroundColor Gray
            pip cache dir
        }
    } else {
        $p = "$env:LOCALAPPDATA\pip\cache"
        if (Test-Path $p) {
            $sz = (Get-ChildItem $p -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
            Write-Host "Dossier trouvé: $p ($([math]::Round($sz, 1)) Mo)" -ForegroundColor Gray
            if ($Execute) {
                Remove-Item -Path "$p\*" -Recurse -Force
                Write-Host "Dossier vidé." -ForegroundColor Green
            }
        }
    }
}

function Clear-NpmCache {
    Write-Host "-> Vérification NPM Cache..." -ForegroundColor Cyan
    if (Get-Command "npm" -ErrorAction SilentlyContinue) {
        if ($Execute) {
            Write-Host "Exécution: npm cache clean --force" -ForegroundColor Green
            npm cache clean --force
        } else {
            Write-Host "Commande simulée: npm cache clean --force" -ForegroundColor Gray
        }
    }
}

function Clear-NugetCache {
    Write-Host "-> Vérification NuGet Cache..." -ForegroundColor Cyan
    if (Get-Command "dotnet" -ErrorAction SilentlyContinue) {
        if ($Execute) {
            Write-Host "Exécution: dotnet nuget locals all --clear" -ForegroundColor Green
            dotnet nuget locals all --clear
        } else {
            Write-Host "Commande simulée: dotnet nuget locals all --clear" -ForegroundColor Gray
        }
    }
}

switch ($Target) {
    "pip"      { Clear-PipCache }
    "npm"      { Clear-NpmCache }
    "nuget"    { Clear-NugetCache }
    "all-safe" { 
        Clear-PipCache
        Clear-NpmCache
        Clear-NugetCache
    }
}
