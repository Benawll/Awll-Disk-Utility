---
name: disk-space-audit
description: >-
  Audit disk space usage on Windows drives, identify bloated folders, analyze AppData,
  and discover large files (>500MB, >1GB). Use whenever scanning disk space or searching for storage leaks.
---

# Skill : Audit de l'Espace Disque (Windows)

Ce skill permet de cartographier rapidement l'occupation de l'espace disque sur un poste Windows, de repérer les partitions saturées, les dossiers les plus lourds dans le profil utilisateur et `AppData`, ainsi que les fichiers volumineux oubliés.

## Procédure d'Utilisation

### 1. Analyse globale des partitions
Pour obtenir la vue d'ensemble des lecteurs (C:, D:, etc.), de leur capacité totale, utilisée et libre :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/disk-space-audit/scripts/scan-drives.ps1
```

### 2. Détection des dossiers les plus lourds
Pour analyser les répertoires volumineux du profil utilisateur :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/disk-space-audit/scripts/scan-user-folders.ps1 -TargetFolder $env:USERPROFILE
```

Pour zoomer sur les répertoires `AppData\Local` ou `AppData\Roaming` (sources fréquentes de caches non vidés) :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/disk-space-audit/scripts/scan-user-folders.ps1 -TargetFolder "$env:LOCALAPPDATA"
powershell -ExecutionPolicy Bypass -File .agents/skills/disk-space-audit/scripts/scan-user-folders.ps1 -TargetFolder "$env:APPDATA"
```

### 3. Recherche des fichiers géants (> 500 Mo / > 1 Go)
Pour trouver les fichiers volumineux (ISOs, vidéos, archives ZIP, installeurs `.exe` ou `.msi`) :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/disk-space-audit/scripts/scan-large-files.ps1 -Paths @($env:USERPROFILE, "D:\") -MinSizeMB 500
```

## Règles de Restitution
- Toujours présenter les volumes en Go ou Mo lisibles avec 1 ou 2 décimales.
- Signaler les partitions dont le taux d'occupation dépasse 80% (attention) ou 90% (critique).
- Ne jamais supprimer un fichier volumineux sans l'accord préalable de l'utilisateur.
