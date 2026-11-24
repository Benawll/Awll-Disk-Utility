---
name: developer-cache-cleaner
description: >-
  Detect and analyze developer caches, build artifacts, package manager caches (npm, pip, cargo, docker, wsl, huggingface, gradle, nuget), and virtual environments.
---

# Skill : Nettoyage des Caches de Développement et d'IA

Les environnements de développement et d'IA accumulent rapidement des dizaines voire des centaines de gigaoctets de caches (modèles HuggingFace/Torch, packages pip/npm, caches de compilation Rust/C++, conteneurs Docker, disques virtuels WSL). Ce skill permet de les inventorier et de les purger en toute sécurité.

## Procédure d'Utilisation

### 1. Scanner tous les caches de développement
Pour mesurer l'espace occupé par chaque cache :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/developer-cache-cleaner/scripts/scan-dev-caches.ps1
```

### 2. Purger les caches sans risque (Pip, NPM, NuGet)
Toujours vérifier d'abord en mode simulation :
```powershell
# Simulation (aucun fichier supprimé)
powershell -ExecutionPolicy Bypass -File .agents/skills/developer-cache-cleaner/scripts/clean-dev-caches.ps1 -Target all-safe

# Exécution réelle (après accord de l'utilisateur)
powershell -ExecutionPolicy Bypass -File .agents/skills/developer-cache-cleaner/scripts/clean-dev-caches.ps1 -Target all-safe -Execute
```

### 3. Modèles d'IA volumineux (HuggingFace, PyTorch)
- Répertoire HuggingFace : `~/.cache/huggingface/hub`
- Si la CLI HuggingFace est présente :
  ```powershell
  huggingface-cli delete-cache
  ```
  Cet outil interactif permet de sélectionner spécifiquement les modèles inutilisés à supprimer.

### 4. Docker et WSL
- Si Docker consomme beaucoup d'espace :
  ```powershell
  docker system df
  # Nettoyage des images orphelines et conteneurs arrêtés
  docker system prune -a --volumes
  ```
- Si un disque WSL (`.vhdx`) a grossi démesurément :
  ```powershell
  wsl --shutdown
  diskpart
  # select vdisk file="<chemin_vers_ext4.vhdx>"
  # compact vdisk
  ```
