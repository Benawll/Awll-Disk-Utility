# Règles Générales de Nettoyage et d'Audit Système — Workspace Cleaner

Ce document définit les règles impératives, la politique de sécurité et les méthodologies d'analyse pour le projet d'audit et de nettoyage d'espace disque sous Windows.

---

## 1. Principes Absolus de Sécurité et Prévention des Risques

### Règle d'or : Zéro suppression non autorisée
- **Mode Audit / Dry-Run par défaut** : Aucune suppression, modification ou désinstallation n'est exécutée sans analyse préalable, mesure du volume à libérer et confirmation explicite de l'utilisateur.
- **Interdiction absolue** de supprimer ou d'altérer :
  - Les fichiers personnels de l'utilisateur (Bureau, Documents, Téléchargements récents, Images, Vidéos, etc.) sans examen ciblé et accord explicite.
  - Les dépôts de code source actifs, configurations de projets personnels, clés SSH/GPG (`~/.ssh`, `~/.gnupg`).
  - Les composants critiques du système Windows (`C:\Windows\System32`, pilotes matériels, ruches de registre actives, points de restauration système).

### Niveaux de Risque des Éléments Identifiés

| Niveau | Catégorie | Exemples | Action recommandée |
| :--- | :--- | :--- | :--- |
| **🟢 Risque Nul / Faible** | Caches & Fichiers temporaires | `%TEMP%`, `C:\Windows\Temp`, corbeille, caches navigateurs, caches téléchargements Windows Update, caches gestionnaires de paquets (`npm cache`, `pip cache`). | Nettoyage automatisable après validation du volume. |
| **🟡 Risque Moyen** | Caches de dev & Environnements | Dossiers `node_modules` de projets inactifs, venv python orphelins, images/volumes Docker inutilisés, caches HuggingFace / PyTorch, caches Gradle / Maven. | Proposer avec le chemin exact, la date de dernier accès et la taille. |
| **🟠 Risque Élevé / Décision Utilisateur** | Logiciels & Jeux installés | Logiciels obsolètes, jeux volumineux (Steam, Epic, Xbox), runtimes multiples (anciens SDKs Java, Python, .NET), applications préinstallées (bloatware). | Présentation sous forme de liste claire avec éditeur, taille, fréquence estimée d'usage et commande de désinstallation (`winget uninstall`). |
| **🔴 Risque Critique / Ne pas toucher** | Données système et clés d'authentification | Fichiers système Windows, profils utilisateurs, bases de données de production locales, clés privées. | Exclusion systématique. |

---

## 2. Règles d'Exécution et de Stabilité des Processus (Anti-Blocage)

### 2.1. Zéro Fenêtre Graphique Bloquante (Headless / Silent Obligatoire)
- **Interdiction formelle** de lancer des désinstalleurs interactifs qui ouvrent une fenêtre graphique sur le bureau utilisateur et attendent un clic manuel (`-tmp`, interfaces Qt/WPF sans argument silencieux).
- **Toujours employer les drapeaux silencieux stricts** :
  - Inno Setup : `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
  - MSI : `msiexec.exe /X{GUID} /qn /norestart`
  - NSIS : `/S`
  - InstallShield / AdvancedInstaller : `/s /v"/qn"`
- **Fallback Direct** : Si une application ne dispose pas de désinstalleur silencieux fiable, stopper les processus associés, supprimer directement ses répertoires (`Program Files`, `ProgramData`, `AppData`) et nettoyer la clé Uninstall du registre.

### 2.2. Surveillance Active et Détection des Tâches Bloquées
- Toute commande ou tâche de fond doit faire l'objet d'un contrôle proactif.
- Si un processus ne progresse plus ou dépasse le temps normal attendu pour une désinstallation, **l'interrompre immédiatement (`manage_task kill`)** et appliquer l'alternative headless directe.

### 2.3. Encapsulation dans des Scripts .ps1
- Toujours écrire les commandes PowerShell complexes dans des scripts dédiés du dossier `scripts/` plutôt que d'injecter des commandes inline dans la console afin d'éviter toute corruption par interpolation des variables `$`.

---

## 3. Déroulement d'une Mission d'Audit et de Nettoyage

1. **Cartographie globale** : Évaluer l'espace total, utilisé et libre sur chaque lecteur (`C:`, `D:`, etc.).
2. **Identification des gisements d'espace** : Caches système, caches dev, gros fichiers, logiciels obsolètes.
3. **Restitution structurée** : Synthétiser les résultats dans un tableau avec gain potentiel en Go et niveau de risque.
4. **Exécution contrôlée** : Demander confirmation avant toute action et appliquer les règles d'exécution headless.
