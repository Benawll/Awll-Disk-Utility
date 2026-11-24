---
name: interactive-cleaner
description: >-
  Orchestrate full recursive Windows disk audit, generate interactive HTML decision dashboards with live GB counters,
  research unknown applications on demand ("Plus d'infos"), and safely execute headless uninstallations without popups.
---

# Skill : Nettoyeur Interactif & Audit Approfondi (Interactive Cleaner)

Ce skill est le chef d'orchestre du projet **Cleaner**. Il structure l'audit au cas par cas, génère le tableau de bord interactif pour l'utilisateur, répond aux demandes d'informations complémentaires (*"Plus d'infos"*) par une enquête factuelle et exécute les suppressions en mode 100% headless.

## 1. Cycle de Fonctionnement

```
[1. Audit Récursif Multi-Niveaux]
       │
       ▼
[2. Génération du Dashboard Interactif (HTML Artifact)]
       │
       ▼
[3. Traitement des Retours Utilisateur]
       ├── Si "❓ Plus d'infos" ──► Enquête d'usage, origine & sécurité
       └── Si "🗑️ Supprimer"   ──► Exécution Headless stricte (zéro fenêtre)
```

---

## 2. Procédure pour l'Enquête Approfondie ("❓ Plus d'infos")

Quand l'utilisateur demande des informations sur un élément avant de décider, l'agent doit systématiquement :
1. **Lancer le script d'enquête** :
   ```powershell
   powershell -ExecutionPolicy Bypass -File .agents/skills/interactive-cleaner/scripts/research-item.ps1 -TargetName "<Nom>" -TargetPath "<Chemin>"
   ```
2. **Répondre selon ces 4 axes stricts** :
   - **Qu'est-ce que c'est concrètement ?** (Définition simple et sans jargon).
   - **Pourquoi est-ce sur le PC ?** (Quel jeu, quel projet d'étude ou quel logiciel l'a installé ?).
   - **Est-ce utilisé récemment ?** (Vérification des processus actifs et de la date de dernière modification des fichiers).
   - **Impact de la désinstallation :** Est-ce que cela casse quelque chose ? Peut-on le supprimer l'esprit tranquille ?

---

## 3. Règles d'Exécution Headless
- Toujours vérifier que le désinstalleur possède un drapeau silencieux (`/VERYSILENT`, `/qn`, `/S`, `/quiet`).
- Ne jamais lancer de processus interactif ouvrant une fenêtre sur l'écran utilisateur.
- Si une application ne supporte pas le mode silencieux, stopper ses processus, supprimer directement son répertoire et nettoyer sa clé registre.

---

## 4. Génération du Dashboard Interactif (Artifact HTML)
- Toujours inclure un calcul dynamique en temps réel de l'espace sélectionné (`total-freed`).
- Toujours inclure le mécanisme de copie sécurisé :
  1. Affichage du texte dans une `<textarea readonly>` visible en un clic pour copie manuelle (`Ctrl+C`).
  2. Commande `document.execCommand('copy')` et `navigator.clipboard.writeText`.
