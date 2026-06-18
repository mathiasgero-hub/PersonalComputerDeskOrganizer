# PersonalComputerDeskOrganizer

Application Windows (.NET 8 / WPF) qui recrée automatiquement vos bureaux virtuels, leurs divisions d'écran et les applications/fichiers/URL qui doivent s'y trouver, à partir de profils que vous configurez une seule fois.

Style visuel : Art Déco, noir et or, conçu et validé à partir de maquettes HTML interactives avant l'écriture du code (voir la conversation d'origine).

## Fonctionnalités

- Démarrage automatique à l'ouverture de session (via une tâche planifiée Windows).
- Écran d'accueil minimaliste : profils existants sous forme de tuiles cliquables, menu "..." (Éditer / Supprimer avec confirmation), bouton "Nouveau profil".
- Création de profil : choix du nombre de bureaux, grille unique affichant tous les bureaux, division de chaque bureau en 1/2/3/4 zones, affectation par zone d'une application installée, d'un fichier spécifique ou d'une URL spécifique, avec suppression individuelle (croix).
- Recherche en direct dans la liste des applications installées (Menu Démarrer + registre + applications Microsoft Store/UWP), avec bouton d'actualisation.
- Lancement d'un profil : confirmation, création des bureaux virtuels manquants, ouverture des applications/fichiers/URL, positionnement précis dans la bonne zone du bon bureau.
- Persistance des profils en JSON lisible (`%AppData%\PersonalComputerDeskOrganizer\Profiles`).
- Écran de réglages pour activer/désactiver le démarrage automatique.

## Architecture du code

```
src/PersonalComputerDeskOrganizer/
  Models/             Profile, DesktopConfig, DivisionConfig, InstalledApp
  Services/
    ProfileStorageService      lecture/écriture des profils (JSON)
    InstalledAppsService       énumération des applications (menu Démarrer + registre + UWP)
    ShellLinkResolver          résolution des raccourcis .lnk (COM IShellLinkW)
    VirtualDesktopService      création / affectation des bureaux virtuels (Slions.VirtualDesktop)
    WindowPlacementService     calcul des zones d'écran + positionnement des fenêtres (Win32)
    AppLauncherService         lancement d'une application/fichier/URL + détection de sa fenêtre
    ProfileLaunchOrchestrator  orchestre l'ensemble au lancement d'un profil
    StartupService             enregistrement de la tâche planifiée de démarrage
    NativeMethods               déclarations P/Invoke Win32
  Themes/ArtDecoTheme.xaml     palette, polices, styles de contrôles
  Views/                       SplashWindow, ProfileEditorWindow, DesktopCountDialog,
                                LaunchOverlayWindow, TextInputDialog, SettingsDialog
```

## Compiler le projet

### Option A — via GitHub Actions (recommandé, pas besoin de Visual Studio)

1. Créez un nouveau dépôt sur https://github.com/new
2. Depuis ce dossier, poussez le code :
   ```
   git init
   git add .
   git commit -m "Initial commit"
   git branch -M main
   git remote add origin https://github.com/VOTRE-COMPTE/VOTRE-DEPOT.git
   git push -u origin main
   ```
3. Sur GitHub, ouvrez l'onglet **Actions** : le workflow "Build PersonalComputerDeskOrganizer" se lance automatiquement et compile l'application sur un runner Windows.
4. Une fois terminé (icône verte), ouvrez le run puis téléchargez l'archive dans **Artifacts** : elle contient l'exécutable prêt à l'emploi et ses dépendances.
5. Décompressez où vous voulez sur votre PC et lancez `PersonalComputerDeskOrganizer.exe`.

### Option B — via Visual Studio

1. Installez Visual Studio 2022 (Community suffit) avec la charge de travail ".NET desktop development".
2. Ouvrez `PersonalComputerDeskOrganizer.sln`.
3. Compilez/exécutez (F5).

## Avant de compiler (optionnel mais recommandé)

- Déposez `Cinzel-Regular.ttf` (gratuite, licence OFL, https://fonts.google.com/specimen/Cinzel) dans `Assets/Fonts/` pour obtenir exactement la police des maquettes. Sans ce fichier, l'application utilise automatiquement une police de secours et compile normalement.
- Déposez une icône `.ico` dans `Assets/Icons/` si vous voulez une icône personnalisée (voir le fichier LISEZ-MOI.txt de ce dossier).

## Points d'attention techniques

- **Bureaux virtuels Windows** : cette fonctionnalité repose sur des interfaces internes non documentées de Windows, via la bibliothèque open-source `Slions.VirtualDesktop`, qui recompile sa couche d'accès au moment de l'exécution pour s'adapter à la version de Windows installée. C'est la solution la plus robuste disponible actuellement, mais une future mise à jour majeure de Windows peut nécessiter une mise à jour de cette dépendance.
- **Premier essai de compilation** : je n'ai pas pu compiler ce projet moi-même (mon environnement de travail est Linux, sans Windows ni Visual Studio). Le code a été écrit avec soin à partir de la documentation officielle des bibliothèques utilisées, mais il est probable que la première exécution du workflow GitHub Actions révèle un ou deux ajustements à faire (en particulier la signature exacte de `VirtualDesktop.MoveToDesktop` dans `VirtualDesktopService.cs`, indiquée en commentaire dans ce fichier). Si le build échoue, copiez-moi le message d'erreur et je corrigerai.
- **Multi-écrans** : la version actuelle positionne les zones sur l'écran principal uniquement. Étendre le positionnement à plusieurs écrans physiques est listé ci-dessous comme amélioration naturelle.
- **Ordre de démarrage** : Windows ne garantit pas qu'une application démarre strictement "avant toutes les autres". La tâche planifiée avec déclencheur "à l'ouverture de session" est le mécanisme le plus précoce disponible sans écrire un service Windows dédié.

## Idées d'amélioration (point 8 du cahier des charges)

- Support multi-écrans physiques (une zone par écran, pas seulement par bureau).
- Import/export de profils (partage entre PC).
- Raccourci clavier global pour relancer rapidement un profil.
- Image personnalisée par profil (au lieu de l'initiale automatique).
- Réordonnancement des bureaux par glisser-déposer.
- Détection et relance automatique si une application configurée a été désinstallée (avertissement au lieu d'un échec silencieux).
- Mode "aperçu" avant lancement (simulation sans ouvrir réellement les applications).
