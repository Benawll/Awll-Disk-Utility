---
name: system-cleanup-guide
description: >-
  Safely identify and clean Windows temporary files, delivery optimization, Windows update leftovers,
  crash dumps, and obsolete system files.
---

# Skill : Guide de Nettoyage Système Windows

Ce skill permet d'analyser et de purger les résidus temporaires du système d'exploitation Windows (fichiers temp utilisateur et système, corbeille, résidus de Windows Update, minidumps de plantage, caches navigateurs).

## Procédure d'Utilisation

### 1. Scanner les emplacements temporaires
Pour mesurer l'espace occupé par chaque catégorie de fichiers temporaires :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/system-cleanup-guide/scripts/scan-windows-temp.ps1
```

### 2. Purge sécurisée des fichiers temporaires
Le script cible les fichiers temporaires non utilisés datant de plus de 24 heures (pour éviter de toucher aux fichiers actuellement ouverts par des processus en cours d'exécution) :
```powershell
# Mode simulation (estimation de l'espace récupérable)
powershell -ExecutionPolicy Bypass -File .agents/skills/system-cleanup-guide/scripts/clean-windows-temp.ps1 -DaysOld 1

# Exécution réelle (après accord utilisateur)
powershell -ExecutionPolicy Bypass -File .agents/skills/system-cleanup-guide/scripts/clean-windows-temp.ps1 -DaysOld 1 -Execute
```

### 3. Vider la corbeille
Pour purger la corbeille de tous les lecteurs après vérification de son contenu :
```powershell
Clear-RecycleBin -Force -ErrorAction SilentlyContinue
```

### 4. Utilitaire natif de nettoyage Windows (Cleanmgr / Assistant de Stockage)
Pour lancer le nettoyage des fichiers système obsolètes (sauvegardes d'anciennes versions de Windows, points de restauration obsolètes, etc.) :
```powershell
# Lance l'outil officiel Windows en interface graphique
cleanmgr /sageset:1
cleanmgr /sagerun:1
```
Ou ouvrir l'Assistant de stockage Windows :
```powershell
Start-Process "ms-settings:storagesense"
```
