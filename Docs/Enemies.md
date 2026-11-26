# Ennemis et boss

## À quoi ça sert

| Prefab | Rôle |
|---|---|
| `Assets/Enemies/Prefabs/Enemy.prefab` | Ennemi normal : patrouille, repère le joueur, le poursuit, l'attaque à l'épée, revient à sa patrouille, meurt. |
| `Assets/Enemies/Prefabs/Boss.prefab` | **Variante** de Enemy (pas une copie) : 1,5 fois plus grand, armure sombre à reflets dorés, 5 fois plus de PV, frappe plus fort et plus loin, mais plus lentement. |

Tout le code est dans `Assets/Enemies/Scripts` (namespace `SwordBound.Enemies`, assembly `SwordBound.Enemies`). Il ne dépend d'**aucun** script du projet principal : on peut l'utiliser depuis n'importe quel script.

| Script | Rôle |
|---|---|
| `IDamageable` | Interface « peut recevoir des dégâts » : `TakeDamage(int)` et `IsDead`. |
| `EnemyHealth` | PV, mort (une seule fois), effets d'impact et de mort, événements. |
| `EnemyAI` | Machine à états `Patrol`, `Chase`, `Attack`, `Return`, `Dead`. Déplacement par Rigidbody, sans NavMesh. |
| `EnemyAnimation` | Envoie la vitesse et les triggers à l'Animator. |
| `AutoDestroyEffect` | Détruit un effet de particules quand il est terminé. |

## Intégration pour le SPAWN

```csharp
using SwordBound.Enemies;

[SerializeField] private GameObject enemyPrefab;   // Assets/Enemies/Prefabs/Enemy.prefab (ou Boss.prefab)
[SerializeField] private Transform[] patrolPoints; // Transforms vides placés dans l'étage
[SerializeField] private Transform player;

void SpawnEnemy(Vector3 position, int etage)
{
    GameObject go = Instantiate(enemyPrefab, position, Quaternion.identity);

    EnemyAI ai = go.GetComponent<EnemyAI>();
    ai.SetPatrolPoints(patrolPoints);   // sans points : l'ennemi reste à sa position de départ
    ai.SetTarget(player);               // optionnel : sinon il cherche le tag "Player" (puis un objet nommé "Player")

    float multiplicateur = 1f + 0.5f * (etage - 1);              // ex. : +50 % par étage
    go.GetComponent<EnemyHealth>().ScaleMaxHealth(multiplicateur); // remet aussi les PV au max
    ai.ScaleAttackDamage(multiplicateur);
}
```

Compter les ennemis restants (GameManager), **sans aucune référence** vers les ennemis :

```csharp
void OnEnable()  { EnemyHealth.AnyEnemyDied += OnEnemyDied; }
void OnDisable() { EnemyHealth.AnyEnemyDied -= OnEnemyDied; }

void OnEnemyDied(EnemyHealth enemy)
{
    ennemisRestants--;
    if (enemy.IsBoss) { /* victoire de la tour */ }
}
```

`AnyEnemyDied` est appelé **une seule fois** par ennemi, au moment de sa mort. L'objet est détruit environ 4 s plus tard (après l'animation de mort).

## Intégration pour le COMBAT (attaques du joueur)

Il n'y a **pas de tag Enemy** (on ne touche pas aux ProjectSettings) : on cherche l'interface.

```csharp
if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, porteeEpee))
{
    hit.collider.GetComponentInParent<SwordBound.Enemies.IDamageable>()?.TakeDamage(degats);
}
```

- Les valeurs `<= 0` sont ignorées. Un ennemi mort n'encaisse plus rien (`IsDead`).
- Chaque coup qui ne tue pas joue l'animation Hit et l'effet d'impact. Le coup mortel joue Death et l'effet de mort.
- Un ennemi touché se met **immédiatement** en poursuite, même s'il ne voyait pas le joueur.
- Le collider de l'ennemi est une capsule sur la racine du prefab (1,8 m, 2,7 m pour le boss).

## Ce que le Player doit avoir

1. Le **tag `Player`** (tag intégré à Unity). Sinon, appeler `SetTarget(player)` sur chaque ennemi.
2. Un **collider** (pour la ligne de vue des ennemis).
3. Pour recevoir les coups, l'un ou l'autre :
   - un `PlayerHealth` qui **implémente `SwordBound.Enemies.IDamageable`** (recommandé : les ennemis arrêtent de frapper quand `IsDead` est vrai) ;
   - ou n'importe quel script du Player avec une méthode `public void TakeDamage(int amount)` : les ennemis l'appellent par `SendMessage`.

## L'Animator (`Assets/Enemies/Controllers/Enemy_Controller.controller`)

| Paramètre | Type | Effet |
|---|---|---|
| `Vitesse` | Float | Blend Tree « Locomotion » : 0 = Idle, 0,5 = Walk, 1 = Run |
| `Attaque` | Trigger | Any State vers Attack, retour à Locomotion à la fin |
| `Degats` | Trigger | Any State vers Hit, retour à Locomotion à la fin |
| `Mort` | Trigger | Any State vers Death, pas de sortie |

Rig Humanoid (clips du pack Pro Sword and Shield, « Copy From Other Avatar »), Apply Root Motion désactivé. `EnemyAnimation` règle tout ça : il ne faut pas appeler l'Animator directement. Les vitesses de l'IA sont synchronisées automatiquement (marche = 0,5, course = 1).

## Réglages (Inspector)

### EnemyAI

| Réglage | Ennemi | Boss | Rôle |
|---|---|---|---|
| `patrolPoints` | (vide) | (vide) | Points visités en boucle, dans l'ordre |
| `waitTimeMin` / `waitTimeMax` | 1 / 2,5 s | 1 / 2,5 s | Attente à chaque point |
| `walkSpeed` / `runSpeed` | 2 / 4,5 m/s | 1,8 / 3,5 m/s | Patrouille et retour / poursuite |
| `turnSpeed` | 360 °/s | 240 °/s | Rotation maximale |
| `detectionRange` | 12 m | 20 m | Distance de repérage |
| `loseRange` | 18 m | 30 m | Au-delà, le joueur est perdu |
| `loseSightTime` | 3 s | 3 s | Temps sans le voir avant de le perdre |
| `requireLineOfSight` | oui | oui | Les murs bloquent la vue (les autres ennemis, non) |
| `eyeHeight` | 1,6 m | 2,4 m | Hauteur du regard |
| `attackRange` (+ `attackRangeTolerance`) | 1,8 m (+0,4) | 2,6 m (+0,5) | S'arrête et frappe ; au-delà de la marge, le coup est raté |
| `attackHitDelay` | 0,4 s | 0,5 s | Délai entre l'animation et le coup |
| `attackDamage` | 10 | 25 | Dégâts d'un coup |
| `attackCooldown` | 1,5 s | 2 s | Temps entre deux coups |

API : `SetTarget`, `SetPatrolPoints`, `SetAttackDamage`, `ScaleAttackDamage`, `State`, `event OnStateChanged`.

### EnemyHealth

| Réglage | Ennemi | Boss | Rôle |
|---|---|---|---|
| `maxHealth` | 100 | 500 | PV max |
| `isBoss` | non | oui | Lu par `IsBoss` (GameManager) |
| `hitEffectPrefab` / `deathEffectPrefab` | Fx_EnemyHit / Fx_EnemyDeath | Fx_BossHit / Fx_BossDeath | Effets (vide = aucun effet, aucune erreur) |
| `effectHeight` | 1,2 m | 1,8 m | Hauteur où apparaissent les effets |
| `destroyDelay` | 4 s | 4 s | Destruction après la mort (au moins la durée de l'animation) |
| `disableOnDeath` | (vide) | (vide) | Composants en plus à désactiver à la mort |

API : `TakeDamage`, `SetMaxHealth`, `ScaleMaxHealth`, `CurrentHealth`, `MaxHealth`, `IsDead`, `IsBoss`, `event OnHealthChanged(courants, max)`, `event OnDied`, `static event AnyEnemyDied`.

## Les effets (`Assets/Enemies/Effects`)

| Prefab | Source (pack Hovl Studio, non modifié) | Réglage |
|---|---|---|
| `Fx_EnemyHit` | Sparks / **Sparks explode red** | Étincelles rouges, plus grosses et plus courtes |
| `Fx_EnemyDeath` | Smoke effects / **Smoke puff** | Fumée sombre, échelle 0,3 |
| `Fx_BossHit` / `Fx_BossDeath` | Variantes des deux précédents | 1,7 fois plus grands |

- Ce sont des **variantes de prefab** : les originaux du pack ne sont pas touchés.
- Lecture unique (pas de boucle). `AutoDestroyEffect` les détruit à la fin (environ 1,7 s pour l'impact, 3,5 s pour la fumée).
- Les matériaux du pack utilisent un shader du pipeline Built-in, qui serait **rose en URP**. Les variantes utilisent donc `Effects/Materials/Fx_Spark_URP` et `Fx_Smoke_URP` (shader URP « Particles/Unlit », mêmes textures).
- Les effets sont créés à la position de l'ennemi, jamais en enfant : ils survivent à sa destruction.

## Scène de test

`Assets/Sandbox/EnemySandbox/EnemySandbox.unity` : un ennemi qui patrouille sur 4 points, un Boss, un mur qui cache un « DummyPlayer » (capsule bleue taguée Player).

Touches : `1` dégâts, `2` attaque, `3` tuer, `R` réapparition, flèches = forcer la vitesse, `V` rendre la vitesse à l'IA, `I`/`J`/`K`/`L` déplacer le DummyPlayer, `H` le soigner. Les coups reçus par le DummyPlayer s'affichent dans la Console.

Les tests automatiques (PlayMode) sont dans `Assets/Sandbox/EnemySandbox/Tests` (Window > General > Test Runner).

## Limites connues

- **Pas de NavMesh** : l'ennemi va en ligne droite. Derrière un mur, il avance contre le mur, perd le joueur au bout de 3 s, puis revient à sa patrouille. Les points de patrouille doivent être reliés par des lignes droites sans obstacle.
- Pas d'évitement entre ennemis (ils peuvent se pousser).
- L'Animator est en « Cull Update Transforms » : un ennemi hors champ ne bouge pas ses os (l'IA continue de fonctionner).
- Les dégâts du coup sont décidés par distance (pas par collision de l'épée).
- Le boss utilise les mêmes animations que l'ennemi, simplement agrandies.

## Texte prêt à coller dans le README

```markdown
## Ennemis

Les ennemis sont dans `Assets/Enemies` : `Prefabs/Enemy.prefab` (ennemi normal) et `Prefabs/Boss.prefab`
(variante plus grande et plus forte). Ils patrouillent, repèrent le joueur (tag `Player`), le poursuivent,
l'attaquent et reviennent à leur patrouille, sans NavMesh. Pour toucher un ennemi :
`hit.collider.GetComponentInParent<SwordBound.Enemies.IDamageable>()?.TakeDamage(degats)`.
Pour compter les morts : `EnemyHealth.AnyEnemyDied` (avec `IsBoss` pour le boss).
Tout est détaillé dans [Docs/Enemies.md](Docs/Enemies.md).
```
