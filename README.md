# Awll Disk Utility

> **Utilitaire Windows 10 / 11 de nettoyage disque, d'analyse d'arborescence (`Space Lens`) et de détection de doublons (`SHA-256`).**

![Platform](https://img.shields.io/badge/Windows-10%20%2F%2011%20(x64)-181816?style=flat-square)
![Version](https://img.shields.io/badge/Version-1.0.0-B84B2A?style=flat-square)
![App](https://img.shields.io/badge/.NET%206-WPF%20%2B%20WebView2-2D6A4F?style=flat-square)
![Installer](https://img.shields.io/badge/Setup-.NET%204.8%20(~63%20Mo)-5C7080?style=flat-square)
![Tests](https://img.shields.io/badge/Tests%20xUnit-21%20%2F%2021-2D6A4F?style=flat-square)

---

## Téléchargement (v1.0.0)

**[Accéder à la dernière Release sur GitHub](https://github.com/Benawll/Awll-Disk-Utility/releases/latest)**

- **Installeur Windows (recommandé)** : **`Awll-Disk-Utility-Setup-1.0.0.exe`** (~62,8 Mo) — exécutable autonome avec raccourcis, mise à jour en place et désinstalleur intégré.
- **Version portable** : **`Awll-Disk-Utility.exe`** (~68,0 Mo) — à lancer directement sans installation.

> **Note Windows SmartScreen :** l'exécutable étant open-source et non signé par une autorité payante, Windows peut afficher *« Windows a protégé votre ordinateur / Éditeur inconnu »* au premier lancement. Cliquez simplement sur **Informations complémentaires → Exécuter quand même**.

---

**Awll Disk Utility** associe un moteur d'analyse système bas niveau en C# à une interface sobre Clair / Sombre (adaptée automatiquement au thème Windows au premier lancement), propulsée par WPF et WebView2.

---

## Fonctionnalités principales

### 1. Audit Intelligent Multi-Disques
- **Symétrie totale sur tous les disques fixes (`C:`, `D:`, `E:`, …)** ou ciblage de dossiers spécifiques via sélecteur natif.
- **Détection dynamique sans liste figée** :
  - Caches GPU (NVIDIA `DXCache`/`GLCache`, AMD, Intel), caches de développement (`.gradle`, `.m2`, `NuGet`, `npm`, `pip`, `uv`, `Cargo`, `Playwright`, `Unity`, `Unreal`, `HuggingFace`, `PyTorch`).
  - Bibliothèques de jeux (Steam, Epic Games, GOG, Riot, Ubisoft, XboxGames, jeux portables, démos/PTS abandonnés, doublons cross-platform).
  - Inventaire des logiciels installés (Registre Windows + `Program Files`) avec détection réelle d'activité (processus actifs, BAM/UserAssist, dernière écriture).
  - **Corbeille Windows & résidus cachés (`$Recycle.Bin`)** : interroge `SHQueryRecycleBin` et le dossier SID utilisateur pour détecter et purger aussi bien les éléments visibles que les dossiers `$R*` orphelins invisibles dans Windows.
  - **WinSxS (DISM)** : analyse lecture seule de l'espace récupérable du magasin de composants Windows.
  - **Dossiers personnels** (`Documents`, `Vidéos`, `Images`, `Musique`, `Bureau`) classés à part, jamais mélangés aux suppressions automatiques.

### 2. Explorateur d'Espace (`Space Lens`)
- **Indexation bottom-up en une passe** via `FileSystemEnumerator` bas niveau (sans allocation d'objets `FileInfo` par fichier).
- **Navigation instantanée (0 ms)** dans toute l'arborescence déjà indexée, avec barre treemap proportionnelle interactive et bouton **Pré-indexer les disques**.
- Invalidation automatique de l'index après chaque suppression ou actualisation manuelle.

### 3. Détecteur de Doublons (`SHA-256`)
- Pipeline en **3 étapes** :
  1. Parcours sans allocation filtrant les fichiers sous le seuil (`50 Mo`, `100 Mo`, `500 Mo`).
  2. Pré-empreinte rapide parallèle (début + milieu + fin) sur les collisions de taille.
  3. Empreinte **SHA-256** complète parallèle uniquement sur les candidats restants, avec suggestion intelligente de la copie à conserver.

### 4. Enquête forensique locale & Mode IA Gemini (optionnel)
- **100 % utilisable hors-ligne** : chaque carte dispose d'un bouton **Infos** présentant 4 rubriques factuelles (*Définition*, *Origine*, *Activité & Usage*, *Sécurité & Réversibilité*) à partir du Registre, des métadonnées EXE/DLL et de la structure du dossier.
- **Mode IA Gemini** (optionnel, clé API personnelle stockée localement dans `%LocalAppData%\Awll\settings.json`) : lancement en arrière-plan depuis chaque carte avec file de concurrence, cache disque 30 jours et bascule instantanée `Locale | IA Gemini`.

### 5. Sécurité & Nettoyage maîtrisé
- **Modale Dry-Run obligatoire** avant toute action, avec répartition proportionnelle par disque et **Corbeille Windows** sélectionnée par défaut (suppression définitive en option explicite).
- **Liste Protégée permanente** et sanctuarisation des composants critiques (`NVIDIA NGX`, `Java`, `System32`).
- **Désinstallation silencieuse (headless)** via drapeaux `/VERYSILENT`, `/qn`, `/S` avec repli propre si nécessaire.
- **Historique cumulatif** des Go récupérés et comparaison d'évolution entre audits.

---

## Installation & Utilisation

| Mode | Fichier généré | Détails |
| :--- | :--- | :--- |
| **Installeur Windows** *(recommandé)* | `Awll-Disk-Utility-Setup-1.0.0.exe` (~62,8 Mo) | Exécutable autonome unique (`.NET Framework 4.8` natif Windows 10/11). Installe dans `%LocalAppData%\Programs\Awll Disk Utility`, crée les raccourcis, enregistre la désinstallation (`--uninstall` / `/S`), gère la mise à jour en place et intègre un bouton **Désinstaller** complet. |
| **Version Portable** | `Awll-Disk-Utility.exe` (~68,0 Mo) | Binaire single-file `.NET 6` autonome, lançable directement sans installation. |

> Toutes les données runtime (réglages, cache IA, liste protégée, historique, assets WebView2) sont stockées dans `%LocalAppData%\Awll\` (avec migration automatique des anciens `.json` depuis `%LocalAppData%\CleanerApp\`).

### Désinstallation propre
- **Méthode Windows standard (recommandée)** : *Menu Démarrer → clic droit sur Awll Disk Utility → Désinstaller* ou *Paramètres Windows → Applications → Applications installées → Awll Disk Utility → Désinstaller*.
- **Via le Setup** : relancez `Awll-Disk-Utility-Setup-1.0.0.exe` puis cliquez sur **Désinstaller** (avec option pour conserver ou effacer `%LocalAppData%\Awll`).

---

## Architecture du projet

```text
Awll-Disk-Utility/
├── Awll/                         # Application principale WPF (.NET 6 + WebView2)
│   ├── Awll.csproj               # AssemblyName: Awll-Disk-Utility (v1.0.0)
│   ├── MainWindow.xaml(.cs)      # Bridge WebView2 <-> Backend + sync thème Windows (DWM)
│   ├── App.xaml(.cs)             # Point d'entrée & support --uninstall (/S)
│   ├── CleanerBackend.cs         # Orchestration Audit & détection des applications installées
│   ├── Models/
│   │   └── CleanerModels.cs      # Modèles de données (items, doublons, rapports, snapshots)
│   ├── Scanners/
│   │   ├── CleanerBackend.Scanners.cs    # Scanners jeux, AppData, téléchargements, racines, perso
│   │   ├── CleanerBackend.SpaceLens.cs   # Moteur Space Lens & indexation bottom-up
│   │   └── CleanerBackend.Duplicates.cs  # Détecteur de doublons SHA-256 en 3 étapes
│   ├── Services/
│   │   ├── CleanerBackend.ResearchAndClean.cs # Enquête locale/IA, suppression & Corbeille Win32
│   │   ├── CleanerBackend.Persistence.cs      # Settings, cache IA, liste protégée, historique
│   │   ├── CleanerBackend.Snapshots.cs        # Bilans d'évolution entre audits
│   │   ├── CleanerBackend.WinSxS.cs           # Analyse DISM lecture seule
│   │   ├── ExecutionHistoryService.cs         # Lecture BAM / UserAssist Windows
│   │   └── NativeFolderPicker.cs              # Sélecteur multi-dossiers natif
│   ├── index.html / styles.css / app.js       # Interface Clair / Sombre (sync Windows par défaut)
│   └── app.ico                                # Icône géométrique vectorielle
├── Awll.Tests/                   # Suite de tests unitaires xUnit (21 tests)
├── AwllInstaller/                # Installeur & désinstalleur natif (.NET Framework 4.8)
└── scripts/
    ├── build-release.ps1         # Tests + publish portable + compression payload + Setup.exe
    └── generate-icon.ps1         # Générateur multi-résolutions d'app.ico
```

---

## Développement, Tests & Build Release

### Prérequis
- Windows 10 / 11 (x64)
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- WebView2 Runtime (préinstallé sur Windows 11 et la majorité des Windows 10)
- Node.js (optionnel, pour `node --check Awll/app.js`)

### Lancer les tests unitaires
```powershell
dotnet test Awll.Tests\Awll.Tests.csproj --verbosity minimal
```

### Générer une release complète
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-release.ps1
```
Le script exécute les 21 tests xUnit, publie `Awll-Disk-Utility.exe` à la racine, compresse le payload dans `AwllInstaller/Payload/Awll-Disk-Utility.exe.gz` puis génère `Awll-Disk-Utility-Setup-1.0.0.exe` à la racine.
