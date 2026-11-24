# Research helper for the 4 requested items
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== RECHERCHE SUR LES 4 ÉLÉMENTS DEMANDÉS ===" -ForegroundColor Cyan

# 1. Core Games
Write-Host "`n--- 1. CORE GAMES (MANTICORE) ---" -ForegroundColor Yellow
$coreAppx = Get-AppxPackage *CORE*
if ($coreAppx) {
    Write-Host "Package AppX : $($coreAppx.PackageFullName)" -ForegroundColor White
    Write-Host "InstallLocation : $($coreAppx.InstallLocation)" -ForegroundColor White
}
$coreProcs = Get-Process *core* -ErrorAction SilentlyContinue
Write-Host "Processus actif : $(if ($coreProcs) { ($coreProcs.Name -join ', ') } else { 'Aucun' })" -ForegroundColor Gray

# 2. OpenAI Codex
Write-Host "`n--- 2. OPENAI CODEX APP & RUNTIMES ---" -ForegroundColor Yellow
$codexAppx = Get-AppxPackage *Codex*
if ($codexAppx) {
    $codexAppx | ForEach-Object {
        Write-Host "Package AppX : $($_.PackageFullName)" -ForegroundColor White
    }
}
$codexProcs = Get-Process *codex* -ErrorAction SilentlyContinue
Write-Host "Processus actif : $(if ($codexProcs) { ($codexProcs.Name -join ', ') } else { 'Aucun' })" -ForegroundColor Gray
if (Test-Path "$env:USERPROFILE\.codex") {
    $lastCodex = (Get-ChildItem "$env:USERPROFILE\.codex" -Recurse -File -Force | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    Write-Host "Dernière utilisation/écriture dans .codex : $($lastCodex.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Magenta
}

# 3. AlphaAntiLeak
Write-Host "`n--- 3. ALPHAANTILEAK ---" -ForegroundColor Yellow
if (Test-Path "C:\AlphaAntiLeak") {
    $aalFiles = Get-ChildItem "C:\AlphaAntiLeak" -Recurse -File -Force
    $lastAal = ($aalFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    Write-Host "Chemin : C:\AlphaAntiLeak ($($aalFiles.Count) fichiers)" -ForegroundColor White
    Write-Host "Date de dernière modification : $($lastAal.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Magenta
    Write-Host "Fichiers présents : $((Get-ChildItem 'C:\AlphaAntiLeak' | Select-Object -First 6).Name -join ', ')" -ForegroundColor Gray
}

# 4. Cygwin
Write-Host "`n--- 4. CYGWIN ---" -ForegroundColor Yellow
if (Test-Path "C:\cygwin64") {
    $cygFiles = Get-ChildItem "C:\cygwin64" -Recurse -File -Force
    $lastCyg = ($cygFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    Write-Host "Chemin : C:\cygwin64 ($($cygFiles.Count) fichiers)" -ForegroundColor White
    Write-Host "Date de dernière modification : $($lastCyg.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Magenta
    Write-Host "Dossiers : $((Get-ChildItem 'C:\cygwin64' -Directory).Name -join ', ')" -ForegroundColor Gray
}
