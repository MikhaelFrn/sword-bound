# Menu principal et menu pause

## Ce qui existe

| Fichier | Rôle |
|---|---|
| `Assets/Scenes/MainMenu.unity` | Écran titre : **Jouer** (charge `TerrainTest`) et **Quitter**. Scène 0 du Build. |
| `Assets/Prefabs/UI/Resources/PauseMenu.prefab` | Menu pause : **Continuer**, **Recommencer**, **Menu principal**, **Quitter**. |
| `Assets/Scripts/UI/MainMenu.cs` | Logique de l'écran titre. |
| `Assets/Scripts/UI/PauseMenu.cs` | Pause (Échap / Start manette), `Time.timeScale`, ajout automatique dans les niveaux. |
| `Assets/Scripts/UI/SceneLoader.cs` | Noms des scènes et changements de scène (un seul endroit à modifier). |
| `Assets/Scripts/UI/MenuUtility.cs` | EventSystem, sélection clavier/manette, touche pause, curseur. |
| `Assets/Scripts/UI/MenuButtonFX.cs` | Animation des boutons (survol, sélection, clic), en temps réel : marche pendant la pause. |
| `Assets/Scripts/UI/MenuFadeIn.cs` | Fondu d'apparition du menu principal et du panneau pause. |
| `Assets/UI/Logo/` | `Logo_Emblem.png` (menu principal) et `Logo_Wordmark.png` (menu pause). |
| `Assets/UI/Menu/` | Textures de l'UI : vignette, lueur, fond et contour arrondis des boutons, séparateur. |

Ordre du Build (File > Build Profiles > Scene List) : `0 MainMenu`, `1 TerrainTest`. `SampleScene` reste dans la liste mais décochée.

## Le menu pause est ajouté tout seul

**Il n'y a rien à glisser dans `TerrainTest`.** À chaque chargement d'une scène qui est **dans le Build** et qui n'est **pas** `MainMenu`, `PauseMenu` instancie le prefab s'il n'y en a pas déjà un dans la scène. Les scènes de démo des packs d'assets (hors Build) ne sont pas touchées.

- Une nouvelle scène de niveau (ex. `InTheTowerLevel`) a le menu pause dès qu'on l'ajoute au Build.
- Pour personnaliser le menu dans une scène, glissez le prefab dans la scène : l'ajout automatique ne fera alors rien.
- Pour désactiver complètement l'ajout automatique : `PauseMenu.AutoSpawn = false;`

## Ce que fait la pause

- `Time.timeScale = 0` et `AudioListener.pause = true` (toute la physique, les animations et les sons s'arrêtent).
- Le curseur est libéré. `CameraLook` le reverrouille tout seul à la reprise : ne pas toucher à `Cursor.lockState` ailleurs.
- **Échap** ouvre et ferme le menu. Le bouton **Continuer** est sélectionné : on peut naviguer aux flèches / à la manette.
- **Recommencer** recharge la scène courante. **Menu principal** retourne à `MainMenu`. Le temps est toujours remis à 1 avant de changer de scène.
- En build, Alt+Tab met le jeu en pause (`pauseOnFocusLost`, désactivé dans l'éditeur).

> Dans l'éditeur, Échap libère aussi le curseur de la fenêtre Game : c'est normal, un clic le reprend.

## Pour le GameManager (Adam) et le joueur (Mikhaël)

```csharp
// Écran de victoire / défaite : empêcher Échap d'ouvrir la pause par-dessus.
PauseMenu.PauseAllowed = false;   // remis à true automatiquement au chargement de la scène suivante

// Savoir si le jeu est en pause (ex. : ignorer l'attaque au clic pendant la pause).
if (PauseMenu.IsPaused) return;

// Réagir à la pause.
PauseMenu.Paused     += () => { /* cacher le HUD, baisser la musique... */ };
PauseMenu.Resumed    += () => { /* ... */ };
PauseMenu.Restarting += () => { /* remettre les PV, l'étage... avant le rechargement */ };
// (se désabonner dans OnDestroy avec -=)

// Changer de scène depuis un autre script (écran de victoire, passage au niveau 2) :
SceneLoader.ReloadCurrent();
SceneLoader.LoadMainMenu();
SceneLoader.Load("InTheTowerLevel");
```

Les entrées du joueur lues dans `Update` continuent d'être lues pendant la pause (seul le temps est figé). Les déplacements en `FixedUpdate` et tout ce qui est multiplié par `Time.deltaTime` s'arrêtent d'eux-mêmes ; une attaque déclenchée au clic doit vérifier `PauseMenu.IsPaused`.

## Renommer ou changer le niveau 1

Modifier `SceneLoader.FirstLevelScene` (ou le champ **First Level Scene** du composant `MainMenu` dans la scène) et vérifier que la scène est dans le Build.

## Style

Palette du logo : or patiné `#D4A951`, brun foncé `#2B2118`, noir chaud `#0E0C0A`, texte crème `#EDE3CF`.
Les boutons ont la transition Unity à **None** : c'est `MenuButtonFX` qui gère les couleurs et l'échelle (réglables dans l'Inspector).
Texte en `Text` intégré (police `LegacyRuntime`) : TextMesh Pro n'est pas importé dans le projet.
Canvas Scaler : Scale With Screen Size, 1920×1080, Match 0.5 (vérifié en 16:9, 16:10 et 4:3).
