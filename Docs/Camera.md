# Caméra FPS (PlayerCameraRig)

## À quoi ça sert

Le prefab `PlayerCameraRig` fournit une caméra à la première personne prête à l'emploi : rotation à la souris, limite de l'angle vertical, verrouillage du curseur, pause, et deux effets subtils (FOV de sprint et balancement de marche).

Elle est **indépendante** : elle ne référence aucun script du joueur ni aucun autre système du jeu. On la glisse comme enfant du Player et elle fonctionne.

| Fichier | Rôle |
|---|---|
| `Assets/Prefabs/Camera/PlayerCameraRig.prefab` | Le prefab à utiliser |
| `Assets/Scripts/Camera/CameraLook.cs` | Souris, rotation, curseur, garde-fou caméras (sur la racine du prefab) |
| `Assets/Scripts/Camera/CameraEffects.cs` | FOV de sprint et head bob (sur l'objet `Camera`) |
| `Assets/Scripts/Camera/Editor/CameraRigBuilder.cs` | Régénère le prefab et la scène de test |
| `Assets/Sandbox/CameraSandbox/CameraSandbox.unity` | Scène de test avec un faux joueur (`DummyPlayer`) |

Hiérarchie du prefab :

```
PlayerCameraRig        position locale (0, 1.6, 0), composant CameraLook
└─ Camera              tag MainCamera, Camera, AudioListener, données URP, CameraEffects
   └─ HandsAnchor      objet vide pour attacher les bras ou l'épée
```

## Intégration en 4 étapes

1. **Glisser le prefab** `PlayerCameraRig` comme **enfant** du GameObject Player (dans la Hiérarchie, pas dans la scène à la racine).
2. **Régler la position locale Y** de `PlayerCameraRig` selon le pivot du Player :
   - pivot aux **pieds** : environ `1.6` (valeur par défaut, hauteur des yeux) ;
   - pivot au **centre** d'une capsule de 2 m : environ `0.6`.
   Les yeux sont à la position locale Y de la racine du prefab, il n'y a pas d'autre réglage de hauteur.
3. **Vérifier le tag `MainCamera`** sur l'objet `Camera` du prefab (déjà réglé). Les autres caméras taguées `MainCamera` seront désactivées automatiquement.
4. **Appuyer sur Play.** Le curseur se verrouille, la souris fait tourner la caméra.

Aucune modification de `SampleScene` n'est nécessaire : la caméra par défaut de la scène et son AudioListener sont désactivés au démarrage (un seul avertissement dans la Console les liste).

## Le contrat avec le PlayerController

Pour que tout fonctionne, le contrôleur du joueur doit respecter ceci :

- **Ne pas faire tourner le corps lui-même.** C'est `CameraLook` qui fait tourner le Player autour de l'axe Y du monde. Si votre contrôleur gère déjà la rotation horizontale, décochez `rotateParentYaw` sur `CameraLook` : la caméra ne fera alors que le haut/bas.
- **Se déplacer avec `transform.forward` et `transform.right`** (le corps tourne, donc ces axes suivent le regard).
- **Rigidbody avec la rotation gelée sur les 3 axes** (Freeze Rotation X, Y, Z) et **Interpolate activé**. Sans interpolation, l'image saccade.
- **Rayon du collider d'au moins 0,3** : le near clip de la caméra est à 0,05, un collider plus fin laisse voir l'intérieur des murs.
- Ne pas appeler `Cursor.lockState` ni `Cursor.visible` : `CameraLook` s'en occupe.

Le Rigidbody est optionnel. Sans Rigidbody, tout fonctionne aussi (les effets déduisent la vitesse du déplacement du Player).

## Caméras et AudioListener

Si `disableOtherMainCameras` est coché (par défaut), au démarrage `CameraLook` désactive **le composant** (pas le GameObject, les tags ne changent pas) :

- toutes les autres caméras taguées `MainCamera`,
- tous les autres `AudioListener`.

Un seul `Debug.LogWarning` indique ce qui a été désactivé. `Camera.main` renvoie donc la caméra du rig : on peut l'utiliser pour les raycasts d'attaque (`Camera.main.transform.forward`) ou viser depuis le centre de l'écran.

## API publique (`CameraLook`)

| Membre | Description |
|---|---|
| `bool LookEnabled { get; set; }` | `false` = la caméra ne tourne plus et le curseur est libéré. `true` par défaut. |
| `float Pitch { get; }` | Angle vertical courant en degrés (négatif = regarde vers le haut). |
| `void ResetLook(float yaw, float pitch)` | Replace le regard (respawn). Le yaw s'applique au Player si `rotateParentYaw` est coché. |

Exemples :

```csharp
// Menu ou inventaire : suspendre la caméra et libérer le curseur
cameraLook.LookEnabled = false;
// ... à la fermeture
cameraLook.LookEnabled = true;

// Écran de mort : figer la caméra
cameraLook.LookEnabled = false;

// Respawn : regarder vers le nord, à l'horizontale
cameraLook.ResetLook(0f, 0f);
```

Pour récupérer le composant : `GetComponentInChildren<CameraLook>()` depuis le Player.

**Pause :** mettre `Time.timeScale = 0` suffit. La caméra arrête de tourner, le curseur est libéré, et tout redevient normal à `Time.timeScale = 1`. Il n'y a rien d'autre à appeler.

## Attacher les bras ou l'épée : `HandsAnchor`

`PlayerCameraRig/Camera/HandsAnchor` est un objet vide, enfant de la caméra. Rendez les bras ou l'épée **enfants de `HandsAnchor`** : ils suivent la caméra (rotation, balancement de marche). Réglez leur position locale pour qu'ils apparaissent en bas à droite de l'écran.

## La souris

La souris est lue **directement** avec le nouvel Input System (`Mouse.current.delta`), sans passer par `InputSystem_Actions`. Le projet n'utilise que le nouvel Input System (`Active Input Handling` = Input System Package).

## Réglages

### CameraLook (racine du prefab)

| Réglage | Défaut | Effet |
|---|---|---|
| `sensitivityX` / `sensitivityY` | 0,1 | Degrés par pixel de souris |
| `invertY` | non | Inverse l'axe vertical |
| `lookSmoothing` | 0 | Lissage en secondes (0 = désactivé, 0,02 à 0,08 = léger) |
| `minPitch` / `maxPitch` | -85 / 85 | Limites de l'angle vertical en degrés |
| `rotateParentYaw` | oui | La caméra fait tourner le Player (sinon, haut/bas seulement) |
| `disableOtherMainCameras` | oui | Désactive les autres caméras MainCamera et AudioListener |

### CameraEffects (objet Camera)

| Réglage | Défaut | Effet |
|---|---|---|
| `enableSprintFov` | oui | Active le FOV de sprint |
| `sprintSpeedThreshold` | 5,5 m/s | Vitesse horizontale qui déclenche le sprint |
| `sprintFovIncrease` | +8° | FOV de base (70) + 8 = 78 |
| `fovSmoothTime` | 0,15 s | Durée de la transition |
| `enableHeadBob` | oui | Active le balancement de marche |
| `bobMinSpeed` | 0,5 m/s | Vitesse minimale pour balancer |
| `bobVerticalAmplitude` / `bobHorizontalAmplitude` | 0,03 / 0,015 m | Amplitude |
| `bobCyclesPerMeter` | 0,4 | Fréquence selon la distance parcourue |
| `bobBlendTime` | 0,12 s | Durée du démarrage et du retour à zéro |

Les effets ne font rien quand `Time.timeScale` vaut 0. Le FOV de base est lu sur la caméra au démarrage (70 dans le prefab).

## Limites connues

- Pas de détection du sol : le balancement continue en l'air si le joueur garde de la vitesse horizontale.
- Le FOV de sprint contrôle `Camera.fieldOfView` : un autre script qui change le FOV en même temps (zoom, visée) entrera en conflit.
- Un seul `PlayerCameraRig` par scène (le garde-fou désactive les autres caméras MainCamera).
- Si une manette est ajoutée plus tard, il faudra lire un autre périphérique : seule la souris est gérée.
- Dans l'éditeur, **Échap** libère le curseur : un clic gauche dans la fenêtre Game le reverrouille.
- Un Player qui a un collider de rayon inférieur à 0,3 peut laisser voir l'intérieur des murs.
- Le prefab se régénère avec `Tools > Camera > Rebuild Camera Rig And Sandbox` : les modifications faites à la main dans le prefab seraient alors écrasées. Modifiez le builder ou travaillez sur des variantes (Prefab Variant).

## Texte prêt à coller dans le README

```markdown
## Caméra FPS

La caméra à la première personne est le prefab `Assets/Prefabs/Camera/PlayerCameraRig.prefab`.
Glissez-le comme enfant du Player, réglez sa position locale Y selon le pivot du Player (1,6 si
le pivot est aux pieds) et appuyez sur Play : la souris tourne la caméra et le Player.
Le contrôleur du joueur ne doit pas faire tourner le corps lui-même, doit se déplacer avec
`transform.forward` / `transform.right` et utiliser un Rigidbody avec rotation gelée et
interpolation. Le détail (contrat, API, réglages) est dans [Docs/Camera.md](Docs/Camera.md).
```
