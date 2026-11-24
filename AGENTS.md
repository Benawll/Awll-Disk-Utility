# Règles Générales de Nettoyage et d'Audit Système — Workspace Cleaner

Ce document définit les règles impératives, la politique de sécurité et les méthodologies d'analyse pour le projet d'audit et de nettoyage d'espace disque sous Windows.

---

## 1. Principes Absolus de Sécurité et Préférences Utilisateur

### Règle d'or : Zéro suppression non autorisée
- **Mode Audit / Dry-Run par défaut** : Aucune suppression, modification ou désinstallation n'est exécutée sans analyse préalable, mesure du volume à libérer et confirmation explicite de l'utilisateur.
- **Sanctuarisation permanente (Sauf demande contraire explicite)** :
  - **`C:\New Doc`** : Sauvegardes de téléphones et projets personnels (strictement conservé).
  - **`C:\Users\benja\Videos`** : Dossier de vidéos et replays de 286 Go (conservé).
  - **NVIDIA NGX / DLSS** : Modèles d'accélération graphique IA conservés car l'utilisateur utilise le DLSS.
  - **Java Runtimes** : Conservation stricte de **Java 17 (LTS)** et **Java 24**.
  - **Unity / C#** : Visual Studio Community n'est pas nécessaire (l'utilisateur et Antigravity interagissent via le MCP Unity et VS Code).

---

## 2. Déroulement d'un Audit Approfondi & Restitution

### 2.1. Audit Récursif Multi-Niveaux et Découverte Dynamique
Tout audit doit inspecter dynamiquement et exhaustivement :
1. **Zéro Oubli par Scan Dynamique au Seuil (>= 150 Mo)** : Interdiction absolue de se limiter à une liste codée en dur de logiciels ou de caches. Le script doit scanner dynamiquement chaque sous-dossier de `AppData\Local`, `AppData\Roaming`, `AppData\LocalLow` et du profil `C:\Users\benja`.
2. **Couverture Exhaustive Obligatoire** :
   - Moteurs RGB et overlays de jeu : `VortxEngine`, `WhirlwindFX`, `Overwolf`, `Medal.tv`, `VoicemodV3`.
   - Messageries et communications : `Slack`, `Microsoft Teams`, `Discord`, `Rocket.Chat`.
   - Mod managers, jeux et sauvegardes : `Thunderstore Mod Manager`, `r2modman`, `Dry Cactus` (Poly Bridge), `Lunar Client`, `CurseForge`.
   - VPN et outils réseau : `Pulse Secure`, `Radmin VPN`.
   - Caches de développement & build : `.gradle`, `.m2`, `npm-cache`, `pip`, `ms-playwright`.
   - Updaters résiduels Electron : `*-updater` dans LocalAppData.
3. Les applications du Windows Store (`WindowsApps`) qui cachent souvent des jeux géants.
4. Les logiciels résiduels dans `Program Files` et `Program Files (x86)`.
5. Les répertoires racine atypiques de `C:\` (`Jeux dolphin`, `Jts`, `iMyfone`, etc.).

### 2.2. Format de Restitution Interactif Obligatoire
- Présenter les résultats sous forme de **tableau de bord interactif HTML** (`cleaner-checklist.html`) avec :
  - Vrais boutons cliquables (**Supprimer**, **Garder**, **❓ Infos**).
  - Calculateur dynamique en direct de l'espace à libérer en Go.
  - Champ de texte de secours affiché immédiatement au clic pour copier la sélection (`Ctrl+C`) en cas de restriction de presse-papier de l'iframe sandbox.
- **Doublonner systématiquement** avec une liste numérotée claire en texte brut dans le chat (ex: `[1]`, `[2]`, `[3]`) pour permettre à l'utilisateur de répondre avec de simples numéros s'il le préfère.

### 2.3. Traitement Systématique de l'Option « ❓ Plus d'infos »
Pour chaque élément où l'utilisateur demande des informations avant de décider :
1. **Lancer le script d'enquête** (`research-item.ps1`) pour vérifier :
   - La date de dernière modification / accès aux fichiers.
   - Si un processus ou service Windows associé est actuellement actif en mémoire.
   - La clé de registre d'installation d'origine.
2. **Répondre clairement selon 4 points indispensables** :
   - **Définition** : Qu'est-ce que c'est simplement ?
   - **Origine** : Pourquoi a-t-il été installé et par quoi ?
   - **Fréquence d'usage** : Est-ce utilisé récemment ou totalement inactif ?
   - **Sécurité** : Peut-on le désinstaller sans aucun impact négatif sur le système ?

---

## 3. Règles d'Exécution et de Stabilité des Processus (Anti-Blocage Stricte)

### 3.1. Zéro Fenêtre Graphique Bloquante (Headless / Silent Obligatoire)
- **Interdiction formelle** de lancer des désinstalleurs interactifs qui ouvrent une fenêtre graphique sur le bureau utilisateur et attendent un clic manuel (`-tmp`, interfaces Qt/WPF sans argument silencieux).
- **Toujours employer les drapeaux silencieux stricts** :
  - Inno Setup : `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
  - MSI : `msiexec.exe /X{GUID} /qn /norestart`
  - NSIS : `/S`
  - InstallShield / AdvancedInstaller : `/s /v"/qn"`
- **Fallback Direct** : Si une application ne dispose pas de désinstalleur silencieux fiable, stopper les processus associés, supprimer directement ses répertoires (`Program Files`, `ProgramData`, `AppData`) et nettoyer la clé Uninstall du registre.

### 3.2. Surveillance Active et Détection des Tâches Bloquées
- Toute commande ou tâche de fond doit faire l'objet d'un contrôle proactif.
- Si un processus ne progresse plus ou dépasse le temps normal attendu pour une désinstallation, **l'interrompre immédiatement (`manage_task kill`)** et appliquer l'alternative headless directe.

### 3.3. Encapsulation dans des Scripts .ps1
- Toujours écrire les commandes PowerShell complexes dans des scripts dédiés du dossier `scripts/` plutôt que d'injecter des commandes inline dans la console afin d'éviter toute corruption par interpolation des variables `$`.
