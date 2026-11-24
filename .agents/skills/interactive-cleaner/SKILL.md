---
name: interactive-cleaner
description: >-
  Orchestrate full recursive Windows disk audit, generate interactive HTML decision dashboards with live GB counters,
  research unknown applications on demand ("Plus d'infos"), calculate risk percentages (0% to 100%), and safely execute headless uninstallations without popups.
---

# Skill : Nettoyeur Interactif & Audit Approfondi (Interactive Cleaner)

Ce skill est le chef d'orchestre du projet **Cleaner**. Il structure l'audit au cas par cas, évalue automatiquement le **pourcentage de risque (0% à 100%)** de chaque élément, génère le tableau de bord interactif pour l'utilisateur, répond aux demandes d'informations complémentaires (*"Plus d'infos"*) par une enquête factuelle et exécute les suppressions en mode 100% headless.

---

## 1. Déclencheurs & Comment Lancer ce Skill

### A. Déclenchement Automatique via Antigravity (Conversation naturelle)
Dès que l'utilisateur exprime l'une de ces intentions dans le chat, ce skill s'active automatiquement :
- *"Fais un audit de mon disque"*
- *"Nettoie mon PC"*
- *"Qu'est-ce qui prend de la place ?"*
- *"Lance interactive-cleaner"*
- *"Trouve les fichiers inutiles"*
- *"Fais un audit approfondi"*

### B. Déclenchement Manuel via PowerShell
Pour exécuter l'audit complet en ligne de commande de manière autonome :
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/interactive-cleaner/scripts/deep-audit-all.ps1
```

Pour les modules spécialisés individuels :
- **Audit de l'espace par lecteur :** `.agents/skills/disk-space-audit/scripts/scan-drives.ps1`
- **Audit des caches développeurs/IA :** `.agents/skills/developer-cache-cleaner/scripts/scan-dev-caches.ps1`
- **Audit des gros logiciels installés :** `.agents/skills/installed-software-analyzer/scripts/get-heavy-apps.ps1`
- **Enquête factuelle sur un élément :** `.agents/skills/interactive-cleaner/scripts/research-item.ps1 -TargetName "<Nom>" -TargetPath "<Chemin>"`

---

## 2. Système d'Évaluation du Pourcentage de Risque (0% à 100%)

Chaque élément détecté lors de l'audit reçoit une note de risque objective basée sur son usage effectif, son impact système et la réversibilité de sa suppression :

| Tranche de Risque | Niveau | Critères d'Évaluation | Exemples |
| :--- | :--- | :--- | :--- |
| **0% - 10%** | **Quasi Nul** | Fichiers temporaires, logs de crash, doublons d'anciennes versions, caches régénérables automatiquement, logiciels sans service actif et non lancés depuis des mois. | User Temp, PUBG CrashDumps, VS Code duplicate extensions, Caches JetBrains résiduels, Radmin VPN inactif. |
| **15% - 40%** | **Faible** | Caches de modèles IA (HuggingFace, PyTorch), profils de mods de jeux (Thunderstore), simulateurs/émulateurs non utilisés récemment. La suppression nécessite un retéléchargement si l'utilisateur souhaite réutiliser l'outil. | Caches Unity UPM, Thunderstore Lethal Company mods. |
| **45% - 70%** | **Modéré / Vigilance** | Logiciels ayant une activité récente (modifications ou processus actifs dans les 7 derniers jours), environnements de travail actifs, outils de développement quotidiens. | OpenAI Codex App, outils bureautiques avec documents récents. |
| **75% - 100%** | **Élevé / Sanctuarisé** | Pilotes GPU actifs, runtimes système essentiels (Java 17 LTS, Java 24), dossiers protégés par l'utilisateur (`C:\New Doc`, `Videos`), bibliothèques DLL Windows critiques. | NVIDIA DLSS (NGX), OpenJDK 17/24, Windows System32, Sauvegardes de téléphones. |

---

## 3. Déroulement du Workflow & Découverte Dynamique Multi-Disques

### Règle d'Exhaustivité : Zéro Oubli par Scan au Seuil & Multi-Disques (C:, D:, E:)
Pour éviter tout angle mort, l'audit ne se limite jamais à une liste statique ni au seul lecteur système C:. Il explore automatiquement l'ensemble des disques fixes connectés :
1. **Sur le Disque Système (C:)** :
   - `AppData\Local`, `Roaming`, `LocalLow` (Seuil >= 150 Mo) : Overlays (Overwolf, Medal), moteurs RGB (SignalRGB/VortxEngine), messageries (Slack), updaters.
   - Profil `C:\Users\benja` : Caches de build (.gradle, .m2), launchers et modpacks (.lunarclient, curseforge).
2. **Sur les Disques Secondaires de Jeux et Données (D:, E:)** :
   - **Caches Steam cachés** : `steamapps\downloading` (téléchargements Steam bloqués ou corrompus consommant des dizaines de Go), `steamapps\workshop` (mods orphelins), `shadercache`.
   - **Doublons de jeux cross-plateformes** : Détection des jeux installés à la fois sur Steam, Epic Games ou Xbox App (ex: Rocket League).
   - **Serveurs de test publics (PTS) & Démos** : Versions de test et démos oubliées (Paladins PTS, démos Steam de festivals).
   - **Émulateurs tiers & ROMs orphelines** : NoxPlayer, BlueStacks, Yuzu Switch ROMs (.nsp), Dolphin Wii ISOs.
   - **Dossiers Téléchargements secondaires** : `D:\Téléchargements` pour les vieux installeurs volumineux (.exe, .msi, .zip) de logiciels déjà désinstallés.
   - **Vieux jeux portables inactifs** : `D:\Autres Jeux` ou `E:\Jeux` non touchés depuis plus d'un an.
3. **Sur les Répertoires de Téléchargements (Downloads)** :
   - **Dossier principal (`C:\Users\benja\Downloads`) & secondaires (`D:\Téléchargements`)** :
     - Détection des installeurs résiduels (.exe, .msi, .AppImage) dès que le logiciel est installé.
     - Détection des archives doublons (.zip existant à côté de son dossier déjà extrait).
     - Détection des builds temporaires (`dist\`) et exports de données.
   - **Règle d'Isolation en Onglet Dédié (`📥 Téléchargements`)** :
     - Dès que le volume d'éléments détectés dans Downloads est important, il est obligatoirement isolé dans un onglet distinct au sein du tableau de bord interactif pour préserver la lisibilité de l'analyse système et jeux.

```
[1. Audit Récursif Dynamique Multi-Niveaux (Seuil >= 150 Mo)]
       │
       ▼
[2. Calcul Automatique des Risques (%) et Justifications Factuelles]
       │
       ▼
[3. Génération du Dashboard Interactif (Artifact HTML & Liste Numérotée)]
       │
       ▼
[4. Traitement des Retours Utilisateur]
       ├── Si "❓ Plus d'infos" ──► Enquête d'usage, processus actifs & sécurité (research-item.ps1)
       └── Si "🗑️ Supprimer"   ──► Exécution Headless stricte (zéro popup, vérification post-suppression)
```

---

## 4. Procédure pour l'Enquête Approfondie ("❓ Plus d'infos")

Quand l'utilisateur clique sur **❓ Infos** ou demande des détails :
1. **Lancer le script d'enquête** :
   ```powershell
   powershell -ExecutionPolicy Bypass -File .agents/skills/interactive-cleaner/scripts/research-item.ps1 -TargetName "<Nom>" -TargetPath "<Chemin>"
   ```
2. **Restituer l'analyse selon les 4 points indispensables** :
   - **Définition :** Qu'est-ce que c'est concrètement et simplement ?
   - **Origine :** Pourquoi est-ce présent et quel composant l'a installé ?
   - **Fréquence d'usage :** Est-ce utilisé récemment ? (Processus actif en mémoire, date de dernier accès).
   - **Sécurité & Risque :** Quel est le risque de suppression (en %) et l'impact direct sur le système ?

---

## 5. Règles d'Exécution Headless (Anti-Blocage)

- **Zéro fenêtre interactive :** Interdiction d'exécuter des désinstalleurs qui affichent des boîtes de dialogue ou attendent des clics sur le bureau.
- **Drapeaux silencieux stricts obligatoires :**
  - Inno Setup : `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
  - MSI : `msiexec.exe /X{GUID} /qn /norestart`
  - NSIS : `/S`
  - AdvancedInstaller / InstallShield : `/s /v"/qn"`
- **Fallback Direct :** Si aucun désinstalleur silencieux n'est disponible, stopper les processus associés (`Stop-Process`), supprimer l'arborescence des dossiers et nettoyer la clé registre correspondante.
