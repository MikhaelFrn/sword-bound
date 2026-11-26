using System;
using UnityEngine;

namespace SwordBound.Enemies
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Return,
        Dead
    }

    /// <summary>
    /// IA simple sans NavMesh : patrouille, poursuite, attaque, retour, mort.
    /// Déplacement par Rigidbody dans FixedUpdate (la gravité et les murs sont respectés).
    /// Aucune dépendance vers les scripts du joueur : la cible est un Transform, les dégâts passent par
    /// IDamageable ou, à défaut, par SendMessage("TakeDamage", int).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyAI : MonoBehaviour
    {
        private const int RaycastBufferSize = 16;

        [Header("Cible")]
        [Tooltip("Cible (le joueur). Si vide, recherche le tag \"Player\" puis un objet nommé \"Player\".")]
        [SerializeField] private Transform target;
        [Tooltip("Intervalle (s) entre deux recherches de la cible tant qu'elle est introuvable.")]
        [SerializeField, Min(0.1f)] private float targetSearchInterval = 1f;

        [Header("Patrouille")]
        [Tooltip("Points visités en boucle, dans l'ordre. Vide = l'ennemi reste à sa position de départ.")]
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [Tooltip("Attente minimale (s) à chaque point.")]
        [SerializeField, Min(0f)] private float waitTimeMin = 1f;
        [Tooltip("Attente maximale (s) à chaque point.")]
        [SerializeField, Min(0f)] private float waitTimeMax = 2.5f;
        [Tooltip("Vitesse de marche (m/s) : patrouille et retour.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2f;
        [Tooltip("Distance (m) à laquelle un point est considéré comme atteint.")]
        [SerializeField, Min(0.05f)] private float pointReachedDistance = 0.35f;

        [Header("Détection")]
        [Tooltip("Distance (m) à laquelle le joueur est repéré.")]
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [Tooltip("Distance (m) au-delà de laquelle le joueur est perdu (plus grande que la détection).")]
        [SerializeField, Min(0f)] private float loseRange = 18f;
        [Tooltip("Temps (s) sans voir le joueur avant de le perdre.")]
        [SerializeField, Min(0f)] private float loseSightTime = 3f;
        [Tooltip("Si vrai, un mur entre l'ennemi et le joueur empêche de le voir.")]
        [SerializeField] private bool requireLineOfSight = true;
        [Tooltip("Hauteur des yeux (m) au-dessus des pieds.")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
        [Tooltip("Couches qui peuvent bloquer la vue (par défaut : tout).")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        [Header("Poursuite")]
        [Tooltip("Vitesse de course (m/s). Sert aussi de référence à l'animation Run.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 4.5f;
        [Tooltip("Vitesse de rotation maximale (degrés par seconde).")]
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

        [Header("Attaque")]
        [Tooltip("Distance (m) à laquelle l'ennemi s'arrête pour attaquer.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
        [Tooltip("Marge (m) ajoutée à la portée au moment du coup : au-delà, le coup est raté.")]
        [SerializeField, Min(0f)] private float attackRangeTolerance = 0.4f;
        [Tooltip("Délai (s) entre le début de l'animation et le moment où le coup touche.")]
        [SerializeField, Min(0f)] private float attackHitDelay = 0.4f;
        [Tooltip("Dégâts d'un coup.")]
        [SerializeField, Min(0)] private int attackDamage = 10;
        [Tooltip("Temps minimum (s) entre deux coups.")]
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.5f;

        private readonly RaycastHit[] hits = new RaycastHit[RaycastBufferSize];

        private Rigidbody body;
        private EnemyHealth health;
        private EnemyAnimation enemyAnimation;
        private IDamageable targetDamageable;

        private EnemyState state = EnemyState.Patrol;
        private Vector3 startPosition;
        private int patrolIndex;
        private bool waiting;
        private float waitUntil;
        private Vector3 returnPoint;
        private float nextSearchTime;
        private float lastSeenTime = float.NegativeInfinity;
        private float provokedUntil = float.NegativeInfinity;
        private float nextAttackTime;
        private bool swinging;
        private float hitTime;

        // Ordres de mouvement calculés dans Update, appliqués dans FixedUpdate.
        private Vector3 moveDirection;
        private float moveSpeed;
        private Vector3 lookDirection;

        /// <summary>État courant.</summary>
        public EnemyState State => state;

        /// <summary>Appelé à chaque changement d'état.</summary>
        public event Action<EnemyState> OnStateChanged;

        public Transform Target => target;
        public int AttackDamage => attackDamage;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float TurnSpeed => turnSpeed;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float DetectionRange => detectionRange;
        public float LoseRange => loseRange;

        /// <summary>Index du point de patrouille visé.</summary>
        public int PatrolIndex => patrolIndex;

        /// <summary>Vrai pendant l'attente à un point de patrouille.</summary>
        public bool IsWaitingAtPoint => waiting;

        /// <summary>Vrai entre le début d'un coup et le moment où il touche (ou rate).</summary>
        public bool IsSwinging => swinging;

        // ------------------------------------------------------------- API publique

        /// <summary>Fixe la cible (le joueur). Null = recherche automatique.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            targetDamageable = newTarget != null ? newTarget.GetComponentInParent<IDamageable>() : null;
        }

        /// <summary>Remplace les points de patrouille (ex. : par le spawner).</summary>
        public void SetPatrolPoints(Transform[] points)
        {
            patrolPoints = points ?? Array.Empty<Transform>();
            patrolIndex = 0;
            waiting = false;
        }

        public void SetAttackDamage(int damage)
        {
            attackDamage = Mathf.Max(0, damage);
        }

        /// <summary>Multiplie les dégâts (ex. : 1.5 à l'étage 2, 3 pour un boss).</summary>
        public void ScaleAttackDamage(float multiplier)
        {
            if (multiplier > 0f)
            {
                attackDamage = Mathf.Max(0, Mathf.RoundToInt(attackDamage * multiplier));
            }
        }

        // ---------------------------------------------------------------- Cycle de vie

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            TryGetComponent(out health);
            TryGetComponent(out enemyAnimation);
            startPosition = transform.position;
            SyncAnimationSpeeds();
            if (target != null)
            {
                SetTarget(target);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                health.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnDied -= HandleDied;
            }
        }

        private void Start()
        {
            startPosition = transform.position;
            if (target == null)
            {
                FindTarget();
            }
        }

        private void OnValidate()
        {
            loseRange = Mathf.Max(loseRange, detectionRange);
            waitTimeMax = Mathf.Max(waitTimeMax, waitTimeMin);
            walkSpeed = Mathf.Min(walkSpeed, runSpeed);
            if (enemyAnimation == null) TryGetComponent(out enemyAnimation);
            SyncAnimationSpeeds();
        }

        private void SyncAnimationSpeeds()
        {
            if (enemyAnimation != null)
            {
                enemyAnimation.VitesseCourse = runSpeed;
                enemyAnimation.VitesseMarche = walkSpeed;
            }
        }

        // ---------------------------------------------------------------- Décisions

        private void Update()
        {
            if (state == EnemyState.Dead)
            {
                return;
            }

            if (target == null && Time.time >= nextSearchTime)
            {
                FindTarget();
            }

            moveDirection = Vector3.zero;
            moveSpeed = 0f;
            lookDirection = Vector3.zero;

            switch (state)
            {
                case EnemyState.Patrol: UpdatePatrol(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Attack: UpdateAttack(); break;
                case EnemyState.Return: UpdateReturn(); break;
            }
        }

        private void UpdatePatrol()
        {
            if (CanSeeTarget())
            {
                StartChase();
                return;
            }

            if (!HasPatrolPoints())
            {
                // Pas de points : rester (ou revenir) à la position de départ.
                MoveTowards(startPosition, walkSpeed);
                return;
            }

            if (waiting)
            {
                if (Time.time < waitUntil) return;
                waiting = false;
                patrolIndex = NextValidIndex(patrolIndex + 1);
            }

            Transform point = patrolPoints[patrolIndex];
            if (point == null)
            {
                patrolIndex = NextValidIndex(patrolIndex + 1);
                return;
            }

            if (MoveTowards(point.position, walkSpeed))
            {
                StopHorizontal();
                waiting = true;
                waitUntil = Time.time + UnityEngine.Random.Range(waitTimeMin, Mathf.Max(waitTimeMin, waitTimeMax));
            }
        }

        private void UpdateChase()
        {
            if (target == null || IsTargetDead())
            {
                StartReturn();
                return;
            }

            bool provoked = Time.time < provokedUntil;
            float distance = FlatDistance(transform.position, target.position);

            if (CanSeeTarget(ignoreRange: true) && (provoked || distance <= loseRange))
            {
                lastSeenTime = Time.time;
            }

            if (!provoked && (distance > loseRange || Time.time - lastSeenTime > loseSightTime))
            {
                StartReturn();
                return;
            }

            if (distance <= attackRange)
            {
                SetState(EnemyState.Attack);
                UpdateAttack();
                return;
            }

            MoveTowards(target.position, runSpeed, stopDistance: 0f);
        }

        private void UpdateAttack()
        {
            if (target == null || IsTargetDead())
            {
                swinging = false;
                StartReturn();
                return;
            }

            float distance = FlatDistance(transform.position, target.position);
            lookDirection = Flat(target.position - transform.position);

            if (swinging)
            {
                if (Time.time >= hitTime)
                {
                    swinging = false;
                    if (distance <= attackRange + attackRangeTolerance && !IsTargetDead())
                    {
                        DealDamage();
                    }
                }
                return; // on reste sur place pendant le coup
            }

            if (distance > attackRange + attackRangeTolerance)
            {
                SetState(EnemyState.Chase);
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                swinging = true;
                hitTime = Time.time + attackHitDelay;
                nextAttackTime = Time.time + attackCooldown;
                if (enemyAnimation != null)
                {
                    enemyAnimation.PlayAttack();
                }
            }
        }

        private void UpdateReturn()
        {
            if (CanSeeTarget())
            {
                StartChase();
                return;
            }

            if (MoveTowards(returnPoint, walkSpeed))
            {
                StopHorizontal();
                waiting = false;
                SetState(EnemyState.Patrol);
            }
        }

        // ---------------------------------------------------------------- Transitions

        private void StartChase()
        {
            lastSeenTime = Time.time;
            SetState(EnemyState.Chase);
        }

        private void StartReturn()
        {
            swinging = false;
            if (HasPatrolPoints())
            {
                patrolIndex = NearestPatrolIndex();
                returnPoint = patrolPoints[patrolIndex].position;
            }
            else
            {
                returnPoint = startPosition;
            }
            SetState(EnemyState.Return);
        }

        private void SetState(EnemyState newState)
        {
            if (state == newState) return;
            state = newState;
            OnStateChanged?.Invoke(state);
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (state == EnemyState.Dead || (health != null && health.IsDead)) return;

            if (target == null) FindTarget();
            if (target == null) return;

            // Touché : poursuite immédiate, même hors de portée.
            provokedUntil = Time.time + Mathf.Max(loseSightTime, 1f);
            lastSeenTime = Time.time;
            if (state != EnemyState.Attack)
            {
                SetState(EnemyState.Chase);
            }
        }

        private void HandleDied(EnemyHealth _)
        {
            swinging = false;
            moveDirection = Vector3.zero;
            moveSpeed = 0f;
            lookDirection = Vector3.zero;
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            SetState(EnemyState.Dead);
            enabled = false;
        }

        // ---------------------------------------------------------------- Physique

        private void FixedUpdate()
        {
            if (state == EnemyState.Dead || body == null || body.isKinematic)
            {
                return;
            }

            Vector3 facing = lookDirection.sqrMagnitude > 0.0001f ? lookDirection : moveDirection;
            if (facing.sqrMagnitude > 0.0001f)
            {
                Quaternion wanted = Quaternion.LookRotation(facing, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, wanted, turnSpeed * Time.fixedDeltaTime));
            }

            Vector3 velocity = body.linearVelocity;
            if (moveSpeed > 0f && moveDirection.sqrMagnitude > 0.0001f)
            {
                // On avance surtout quand on regarde dans la bonne direction : pas de glissade de côté.
                Vector3 forward = body.rotation * Vector3.forward;
                float alignment = Mathf.Clamp01(Vector3.Dot(forward, moveDirection));
                Vector3 horizontal = moveDirection * (moveSpeed * alignment);
                velocity.x = horizontal.x;
                velocity.z = horizontal.z;
            }
            else
            {
                velocity.x = 0f;
                velocity.z = 0f;
            }
            body.linearVelocity = velocity;
        }

        /// <summary>Arrêt net (sinon l'ennemi glisse jusqu'au prochain pas de physique).</summary>
        private void StopHorizontal()
        {
            if (body != null && !body.isKinematic)
            {
                Vector3 v = body.linearVelocity;
                v.x = 0f;
                v.z = 0f;
                body.linearVelocity = v;
            }
        }

        /// <summary>Prépare un déplacement vers une position. Renvoie vrai si elle est atteinte.</summary>
        private bool MoveTowards(Vector3 destination, float speed, float stopDistance = -1f)
        {
            float reach = stopDistance < 0f ? pointReachedDistance : stopDistance;
            Vector3 delta = Flat(destination - transform.position);
            if (delta.magnitude <= reach)
            {
                return true;
            }

            moveDirection = delta.normalized;
            moveSpeed = speed;
            return false;
        }

        // ---------------------------------------------------------------- Vision

        private bool CanSeeTarget(bool ignoreRange = false)
        {
            if (target == null || IsTargetDead())
            {
                return false;
            }

            float distance = FlatDistance(transform.position, target.position);
            if (!ignoreRange && distance > detectionRange)
            {
                return false;
            }

            return !requireLineOfSight || HasLineOfSight();
        }

        private bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 aim = TargetAimPoint();
            Vector3 toTarget = aim - eye;
            float length = toTarget.magnitude;
            if (length < 0.01f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(eye, toTarget / length, hits, length, sightBlockers, QueryTriggerInteraction.Ignore);
            float nearestBlocker = float.PositiveInfinity;
            float nearestTarget = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Transform t = hits[i].transform;
                if (t.IsChildOf(transform))
                {
                    continue; // soi-même
                }
                if (t.IsChildOf(target) || target.IsChildOf(t))
                {
                    nearestTarget = Mathf.Min(nearestTarget, hits[i].distance);
                    continue;
                }
                if (t.GetComponentInParent<EnemyHealth>() != null)
                {
                    continue; // les autres ennemis ne cachent pas le joueur
                }
                nearestBlocker = Mathf.Min(nearestBlocker, hits[i].distance);
            }

            return nearestBlocker > nearestTarget || float.IsPositiveInfinity(nearestBlocker);
        }

        private Vector3 TargetAimPoint()
        {
            Collider c = target.GetComponentInChildren<Collider>();
            if (c != null && c.enabled)
            {
                return c.bounds.center;
            }
            return target.position + Vector3.up * 1f;
        }

        // ---------------------------------------------------------------- Dégâts

        private void DealDamage()
        {
            if (state == EnemyState.Dead || target == null || attackDamage <= 0)
            {
                return;
            }

            if (targetDamageable == null)
            {
                targetDamageable = target.GetComponentInParent<IDamageable>();
            }

            if (targetDamageable != null)
            {
                if (!targetDamageable.IsDead)
                {
                    targetDamageable.TakeDamage(attackDamage);
                }
                return;
            }

            // Compatible avec un futur PlayerHealth qui n'implémenterait pas IDamageable.
            target.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        private bool IsTargetDead()
        {
            if (target != null && targetDamageable == null)
            {
                targetDamageable = target.GetComponentInParent<IDamageable>();
            }
            return targetDamageable != null && targetDamageable.IsDead;
        }

        // ---------------------------------------------------------------- Outils

        private void FindTarget()
        {
            nextSearchTime = Time.time + targetSearchInterval;
            GameObject found = null;
            try
            {
                found = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                // Tag absent : on essaie par le nom.
            }

            if (found == null)
            {
                found = GameObject.Find("Player");
            }

            if (found != null && !found.transform.IsChildOf(transform))
            {
                SetTarget(found.transform);
            }
        }

        private bool HasPatrolPoints()
        {
            if (patrolPoints == null) return false;
            foreach (Transform p in patrolPoints)
            {
                if (p != null) return true;
            }
            return false;
        }

        private int NextValidIndex(int from)
        {
            int n = patrolPoints.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = (from + i) % n;
                if (patrolPoints[idx] != null) return idx;
            }
            return 0;
        }

        private int NearestPatrolIndex()
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                float d = FlatDistance(transform.position, patrolPoints[i].position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flat(a - b).magnitude;
        }

        // ---------------------------------------------------------------- Gizmos

        private void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(p, detectionRange);
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(p, loseRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(p, attackRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(p + Vector3.up * eyeHeight, p + Vector3.up * eyeHeight + transform.forward * 1.5f);

            if (patrolPoints == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Transform first = null, previous = null;
            foreach (Transform point in patrolPoints)
            {
                if (point == null) continue;
                Gizmos.DrawSphere(point.position, 0.25f);
                if (previous != null) Gizmos.DrawLine(previous.position, point.position);
                if (first == null) first = point;
                previous = point;
            }
            if (first != null && previous != null && first != previous)
            {
                Gizmos.DrawLine(previous.position, first.position); // la boucle
            }
        }
    }
}
