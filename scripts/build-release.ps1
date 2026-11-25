Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cleanerCsproj = Join-Path $repoRoot "Awll\Awll.csproj"
$cleanerPublishTmp = Join-Path $repoRoot "Awll\publish_tmp"
$testsCsproj = Join-Path $repoRoot "Awll.Tests\Awll.Tests.csproj"
$installerCsproj = Join-Path $repoRoot "AwllInstaller\AwllInstaller.csproj"
$payloadDir = Join-Path $repoRoot "AwllInstaller\Payload"
$payloadGz = Join-Path $payloadDir "Awll-Disk-Utility.exe.gz"
$publishTmp = Join-Path $repoRoot "AwllInstaller\publish_tmp"
$legacyDistDir = Join-Path $repoRoot "dist_app"

[xml]$csprojXml = Get-Content -Path $cleanerCsproj -Encoding UTF8
$version = [string]$csprojXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Impossible de lire <Version> dans $cleanerCsproj"
}
$version = $version.Trim()
Write-Output "=== Release Build Awll Disk Utility v$version (Root Layout) ==="

Write-Output "[1/4] Exécution des tests unitaires..."
dotnet test $testsCsproj --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "Échec des tests unitaires ($LASTEXITCODE)."
}

Write-Output "[2/4] Publication d'Awll-Disk-Utility.exe à la racine..."
if (Test-Path $legacyDistDir) {
    Remove-Item -Path $legacyDistDir -Recurse -Force
}
if (Test-Path $cleanerPublishTmp) {
    Remove-Item -Path $cleanerPublishTmp -Recurse -Force
}
foreach ($pattern in @("Awll.exe", "Awll-Setup-*.exe", "Awll-Disk-Utility.exe", "Awll-Disk-Utility-Setup-*.exe")) {
    Get-ChildItem -Path $repoRoot -Filter $pattern -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

dotnet publish $cleanerCsproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $cleanerPublishTmp

if ($LASTEXITCODE -ne 0) {
    throw "Échec de publication d'Awll Disk Utility ($LASTEXITCODE)."
}

$publishedExe = Join-Path $cleanerPublishTmp "Awll-Disk-Utility.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Awll-Disk-Utility.exe introuvable dans $cleanerPublishTmp"
}

$awllExe = Join-Path $repoRoot "Awll-Disk-Utility.exe"
Move-Item -Path $publishedExe -Destination $awllExe -Force
if (Test-Path $cleanerPublishTmp) {
    Remove-Item -Path $cleanerPublishTmp -Recurse -Force
}

Write-Output "[3/4] Compression GZip de Awll-Disk-Utility.exe dans Payload..."
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
Get-ChildItem -Path $payloadDir -Filter "*.gz" -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$inStream = [System.IO.File]::OpenRead($awllExe)
$outStream = [System.IO.File]::Create($payloadGz)
$gzip = New-Object System.IO.Compression.GZipStream($outStream, [System.IO.Compression.CompressionLevel]::Optimal)
try {
    $inStream.CopyTo($gzip)
} finally {
    $gzip.Dispose()
    $outStream.Dispose()
    $inStream.Dispose()
}

Write-Output "[4/4] Publication de Awll-Disk-Utility-Setup-$version.exe à la racine..."
if (Test-Path $publishTmp) {
    Remove-Item -Path $publishTmp -Recurse -Force
}

dotnet build $installerCsproj `
    -c Release `
    -p:Version=$version `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishTmp

if ($LASTEXITCODE -ne 0) {
    throw "Échec de publication d'AwllInstaller ($LASTEXITCODE)."
}

$setupSrc = Join-Path $publishTmp "Awll-Disk-Utility-Setup.exe"
if (-not (Test-Path $setupSrc)) {
    throw "Awll-Disk-Utility-Setup.exe introuvable dans $publishTmp"
}

$setupDest = Join-Path $repoRoot "Awll-Disk-Utility-Setup-$version.exe"
Move-Item -Path $setupSrc -Destination $setupDest -Force

if (Test-Path $publishTmp) {
    Remove-Item -Path $publishTmp -Recurse -Force
}

Write-Output ""
Write-Output "=== Release terminée avec succès ==="
foreach ($itemPath in @($awllExe, $setupDest)) {
    $fi = Get-Item $itemPath
    $mb = [Math]::Round($fi.Length / 1MB, 2)
    Write-Output (" -> {0} ({1} Mo)" -f $fi.FullName, $mb)
}
