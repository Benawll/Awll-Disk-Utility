# Règles & Architecture — Awll Disk Utility

Ce fichier définit les conventions techniques, UX/UI, de sécurité et de publication pour le projet **Awll Disk Utility** (`https://github.com/Benawll/Awll-Disk-Utility`).

---

## 1. Architecture du dépôt

- **`Awll/`** — Application principale WPF (.NET 6 + WebView2, `AssemblyName: Awll-Disk-Utility`) :
  - `MainWindow.xaml(.cs)` : bridge JSON WebView2 ↔ backend + synchronisation thème Windows (DWM).
  - `App.xaml(.cs)` : point d'entrée & support désinstallation silencieuse (`--uninstall` / `/S`).
  - `CleanerBackend.cs` & `Scanners/` : audit multi-disques symétrique, Space Lens (`SpaceLens.cs`) et détecteur de doublons SHA-256 (`Duplicates.cs`).
  - `Services/` : enquête locale & IA Gemini (`ResearchAndClean.cs`), persistance `%LocalAppData%\Awll\` (`Persistence.cs`), snapshots (`Snapshots.cs`), WinSxS DISM (`WinSxS.cs`), historique BAM/UserAssist (`ExecutionHistoryService.cs`), sélecteur de dossiers (`NativeFolderPicker.cs`).
  - `index.html`, `styles.css`, `app.js`, `tailwindcss.min.js` : ressources embarquées chargées via `https://cleaner.app/`.
- **`Awll.Tests/`** — Suite de tests unitaires xUnit (`GlobalRulesTests.cs`).
- **`AwllInstaller/`** — Installeur & désinstalleur autonome (.NET Framework 4.8) embarquant `Payload\Awll-Disk-Utility.exe.gz`.
- **`scripts/`** — Uniquement les scripts d'outillage du projet :
  - `scripts/build-release.ps1` : tests xUnit + publication single-file + compression GZip + compilation du Setup.
  - `scripts/generate-icon.ps1` : génération de `Awll/app.ico`.

---

## 2. Design System & Règles UI/UX

- **Thèmes** : **Atelier Suisse** (clair `#F0EEE7` / `#FAF9F5` / `#B84B2A`) et **Suisse Nocturne** (sombre `#141412` / `#20201D` / `#DF6E46`).
  - Au premier lancement (sans préférence utilisateur enregistrée), l'application et l'installeur adoptent automatiquement le thème Windows Clair ou Sombre.
  - Utiliser exclusivement les variables CSS `--app-*` (jamais de couleurs codées en dur qui casseraient un thème, aucun effet néon).
- **Zéro emoji** dans l'interface WebView2 (`index.html`, `app.js`), l'installeur (`AwllInstaller`) et les chaînes C# affichées à l'utilisateur ; utiliser les icônes SVG vectorielles géométriques (`icon(...)`).
- **Clarté & cohérence** : les vues multi-disques (`Audit Intelligent`, `Détecteur de Doublons`) respectent le filtre d'affichage latéral (`activeDrives`) et séparent explicitement les dossiers personnels des éléments recommandés.

---

## 3. Sécurité, Nettoyage & Garde-fous IA

- **Mode Dry-Run obligatoire** : aucune suppression ni désinstallation n'est lancée sans confirmation explicite dans la modale récapitulative.
- **Corbeille Windows par défaut** (`useRecycleBin: true`), suppression définitive uniquement sur choix explicite de l'utilisateur.
- **Protections & Sanctuarisation** :
  - Respect permanent de la **Liste Protégée** utilisateur (`protected_items.json`) et des emplacements critiques système/runtimes (`System32`, `NVIDIA\NGX`, `Java`, etc.).
  - Ne jamais écrire de chemins personnels ou spécifiques à une machine en dur dans le code du dépôt.
- **Désinstallation silencieuse (Headless)** : utiliser `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` (Inno), `/qn /norestart` (MSI), `/S` (NSIS) — jamais de désinstalleur graphique bloquant.
- **Garde-fous Gemini IA** :
  - Toute recommandation IA conserve un plancher de risque pour les composants système/runtimes/actifs et pour les emplacements protégés.
  - Sur les groupes de doublons, l'IA ne doit jamais recommander de supprimer un fichier protégé ni l'ensemble des copies d'un groupe.

---

## 4. Versioning & Publication Release (SemVer)

À chaque mise à jour publiée, incrémenter la version **proportionnellement à l'ampleur du changement** (`PATCH` pour correctifs ciblés, `MINOR` pour nouvelles fonctionnalités, `MAJOR` pour refontes/ruptures) dans les 5 emplacements suivants :
1. `Awll/Awll.csproj` (`<Version>`)
2. `AwllInstaller/AwllInstaller.csproj` (`<Version>`)
3. `AwllInstaller/InstallerEngine.cs` (version de repli)
4. `AwllInstaller/MainWindow.xaml` (`TxtSubtitle`, `TxtCompleteTitle`)
5. `README.md` (badge Version, section Téléchargement, arborescence et notes)

Puis exécuter `powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1` pour valider les tests xUnit et générer `Awll-Disk-Utility.exe` et `Awll-Disk-Utility-Setup-<Version>.exe`.
