# Target 5: Uninstall old Java versions (Java 8, Java 11, Java 15) and keep Java 17 and Java 24
$ErrorActionPreference = "SilentlyContinue"

$toUninstall = @(
    @{ Name = "Java 8 Update 411"; Guid = "{77924AE4-039E-4CA4-87B4-2F32180411F0}" },
    @{ Name = "Java SE Development Kit 11.0.15"; Guid = "{7B471FA9-EA69-5362-9C44-9E290E063896}" },
    @{ Name = "Java SE Development Kit 15.0.2"; Guid = "{2041CF7D-1F63-5C58-9F35-C445251E39C9}" }
)

foreach ($item in $toUninstall) {
    Write-Host "Désinstallation silencieuse de : $($item.Name)..." -ForegroundColor Cyan
    $proc = Start-Process -FilePath "msiexec.exe" -ArgumentList "/X$($item.Guid) /qn /norestart" -Wait -PassThru
    if ($proc.ExitCode -eq 0) {
        Write-Host "Succès : $($item.Name) désinstallé (Code 0)." -ForegroundColor Green
    } else {
        Write-Host "Code de sortie pour $($item.Name) : $($proc.ExitCode)" -ForegroundColor Yellow
    }
}

Write-Host "`nVersions de Java conservées :" -ForegroundColor Cyan
Write-Host "- Java SE Development Kit 17 (LTS standard)" -ForegroundColor Green
Write-Host "- Java SE Development Kit 24 (Dernière version)" -ForegroundColor Green
