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

Tout élément identifié doit être catégorisé selon son niveau de risque :

| Niveau | Catégorie | Exemples | Action recommandée |
| :--- | :--- | :--- | :--- |
| **🟢 Risque Nul / Faible** | Caches & Fichiers temporaires | `%TEMP%`, `C:\Windows\Temp`, corbeille, caches navigateurs, caches téléchargements Windows Update, caches gestionnaires de paquets (`npm cache`, `pip cache`). | Nettoyage automatisable après validation du volume. |
| **🟡 Risque Moyen** | Caches de dev & Environnements | Dossiers `node_modules` de projets inactifs, venv python orphelins, images/volumes Docker inutilisés, caches HuggingFace / PyTorch, caches Gradle / Maven. | Proposer avec le chemin exact, la date de dernier accès et la taille. |
| **🟠 Risque Élevé / Décision Utilisateur** | Logiciels & Jeux installés | Logiciels obsolètes, jeux volumineux (Steam, Epic, Xbox), runtimes multiples (anciens SDKs Java, Python, .NET), applications préinstallées (bloatware). | Présentation sous forme de liste claire avec éditeur, taille, fréquence estimée d'usage et commande de désinstallation (`winget uninstall`). |
| **🔴 Risque Critique / Ne pas toucher** | Données système et clés d'authentification | Fichiers système Windows, profils utilisateurs, bases de données de production locales, clés privées. | Exclusion systématique. |

---

## 2. Déroulement d'une Mission d'Audit et de Nettoyage

Chaque intervention doit respecter le cycle suivant :

1. **Cartographie globale** :
   - Évaluer l'espace total, utilisé et libre sur chaque lecteur (`C:`, `D:`, etc.).
   - Identifier les lecteurs saturés.
2. **Identification des gisements d'espace** :
   - Caches et résidus système Windows.
   - Caches de développement et d'IA (Hugging Face, Torch, Docker, Pip, Npm, Cargo, Gradle, venv).
   - Dossiers surdimensionnés dans `%USERPROFILE%` et `AppData` (Local, Roaming, LocalLow).
   - Fichiers géants (> 500 Mo et > 1 Go) orphelins (fichiers ISO, vidéos oubliées, archives ZIP, installateurs `.exe` / `.msi`).
   - Logiciels et applications lourdes installées inutilisées ou redondantes.
3. **Restitution structurée** :
   - Synthétiser les résultats dans un tableau lisible indiquant le gain potentiel en Go.
   - Détailler les actions sans risque immédiates.
   - Proposer une liste de programmes candidats à la désinstallation pour décision de l'utilisateur.
4. **Exécution contrôlée** :
   - Traiter en priorité les éléments 🟢 Risque Faible.
   - Demander confirmation pour chaque groupe d'actions ou désinstallation.
