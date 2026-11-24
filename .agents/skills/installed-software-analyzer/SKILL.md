---
name: installed-software-analyzer
description: >-
  Analyze installed software, games, runtimes, and Windows Store apps on Windows to identify unused,
  redundant, or heavy programs that can be uninstalled to save space.
---

# Skill : Analyse des Logiciels et Runtimes Installés

Ce skill inspecte le registre Windows, Winget et les services de démarrage pour lister les programmes installés, repérer les applications volumineuses, les doublons de versions (ex : plusieurs versions de Python, Java ou .NET) et les logiciels obsolètes.

## Procédure d'Utilisation

### 1. Lister les programmes les plus volumineux
Pour obtenir le classement des applications qui consomment le plus d'espace disque :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/installed-software-analyzer/scripts/get-heavy-apps.ps1 -MinSizeMB 200
```

### 2. Inventaire complet ou filtré par mot-clé
Pour rechercher un logiciel ou un éditeur précis (ex: "Adobe", "Autodesk", "Game", "Visual Studio") :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/installed-software-analyzer/scripts/get-installed-programs.ps1 -Filter "Autodesk"
```

Pour afficher tous les programmes ayant une taille déclarée :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/installed-software-analyzer/scripts/get-installed-programs.ps1 -OnlyWithSize
```

### 3. Examiner les applications au démarrage
Pour voir les logiciels qui se lancent au démarrage et vérifier s'ils sont encore utiles :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/installed-software-analyzer/scripts/get-startup-apps.ps1
```

## Recommandations pour la Désinstallation
- Vérifier d'abord si le logiciel peut être désinstallé proprement via `winget` :
  ```powershell
  winget list <NomOuId>
  winget uninstall <NomOuId>
  ```
- Si indisponible via winget, utiliser la commande de désinstallation officielle répertoriée dans la colonne `UninstallString` du registre ou ouvrir les paramètres Windows (`ms-settings:appsfeatures`).
- Ne jamais désinstaller de composants critiques sans l'accord préalable de l'utilisateur.
