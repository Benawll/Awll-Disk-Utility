# 🧹 PC Cleaner & Space Optimizer (Windows)

Projet d'audit d'espace disque, d'inventaire des logiciels et d'optimisation sécurisée pour Windows.

## 🎯 Objectifs
1. Identifier ce qui est installé sur le PC qui ne sert plus ou prend une place disproportionnée.
2. Détecter les gros gisements d'espace : caches de dev (IA, Python, Node, Docker), résidus système, gros fichiers orphelins.
3. Proposer des actions de nettoyage classées par niveau de risque avec confirmation utilisateur obligatoire.

---

## 🛡️ Règles de Sécurité (`AGENTS.md` / `GEMINI.md`)
- **Mode Simulation par défaut** : aucune suppression sans validation explicite.
- **Protection des données personnelles** : Bureau, Documents, Projets actifs et clés d'accès sont sanctuarisés.
- **Classification claire des risques** : Vert (Caches sans risque), Jaune (Caches dev à vérifier), Orange (Désinstallation de logiciels), Rouge (Système critique).

---

## 🧰 Skills et Outils Disponibles (`.agents/skills/`)

### 1. `disk-space-audit`
- `scan-drives.ps1` : État global des disques (C:, D:, etc.) et taux de remplissage.
- `scan-user-folders.ps1` : Identification des dossiers les plus volumineux (`%USERPROFILE%`, `AppData`).
- `scan-large-files.ps1` : Détection des fichiers géants (> 500 Mo / > 1 Go).

### 2. `installed-software-analyzer`
- `get-heavy-apps.ps1` : Classement des logiciels les plus lourds et détection de runtimes multiples.
- `get-installed-programs.ps1` : Inventaire complet (registre 32/64 bits) avec filtre de recherche.
- `get-startup-apps.ps1` : Programmes au démarrage et bloatware potentiel.

### 3. `developer-cache-cleaner`
- `scan-dev-caches.ps1` : Analyse des caches HuggingFace, PyTorch, Pip, Npm, Cargo, NuGet, Docker, WSL.
- `clean-dev-caches.ps1` : Purge contrôlée (simulation par défaut, exécution avec `-Execute`).

### 4. `system-cleanup-guide`
- `scan-windows-temp.ps1` : Analyse des dossiers temporaires, corbeille, résidus Windows Update.
- `clean-windows-temp.ps1` : Purge sécurisée des fichiers temporaires plus vieux que X jours.
