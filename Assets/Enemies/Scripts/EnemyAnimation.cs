using UnityEngine;
using UnityEngine.AI;

namespace SwordBound.Enemies
{
    /// <summary>
    /// Fait le lien entre le déplacement de l'ennemi et son Animator (Enemy_Controller) :
    /// paramètre Float "Vitesse" (0 = Idle, 0.5 = Walk, 1 = Run) et triggers "Attaque", "Degats", "Mort".
    /// La vitesse est lue sur le NavMeshAgent s'il y en a un, sinon sur le Rigidbody, sinon par le déplacement.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimation : MonoBehaviour
    {
        public static readonly int VitesseHash = Animator.StringToHash("Vitesse");
        public static readonly int AttaqueHash = Animator.StringToHash("Attaque");
        public static readonly int DegatsHash = Animator.StringToHash("Degats");
        public static readonly int MortHash = Animator.StringToHash("Mort");

        private const string AttackState = "Attack";
        private const string HitState = "Hit";
        private const string DeathClipName = "Death";

        [Tooltip("Vitesse (m/s) qui correspond à la course (Vitesse = 1). Réglée automatiquement par EnemyAI (runSpeed).")]
        [SerializeField, Min(0.1f)] private float vitesseCourse = 4f;
        [Tooltip("Vitesse (m/s) qui correspond à la marche (Vitesse = 0,5). 0 = moitié de la course. Réglée automatiquement par EnemyAI (walkSpeed).")]
        [SerializeField, Min(0f)] private float vitesseMarche = 0f;
        [Tooltip("Lissage du paramètre Vitesse (secondes).")]
        [SerializeField, Min(0f)] private float dampTime = 0.1f;
        [Tooltip("Délai minimum entre deux animations de dégâts.")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.4f;

        private Animator animator;
        private NavMeshAgent agent;
        private Rigidbody body;
        private const float SpeedWindow = 0.1f;

        private Vector3 lastPosition;
        private float windowDistance;
        private float windowTime;
        private float measuredSpeed;
        private float lastHitTime = float.NegativeInfinity;
        private bool isDead;
        private bool hasSpeedOverride;
        private float speedOverride;

        public Animator Animator => animator;
        /// <summary>Vitesse (m/s) qui donne Run (Vitesse = 1).</summary>
        public float VitesseCourse
        {
            get => vitesseCourse;
            set => vitesseCourse = Mathf.Max(0.1f, value);
        }

        /// <summary>Vitesse (m/s) qui donne Walk (Vitesse = 0,5). 0 = moitié de la course.</summary>
        public float VitesseMarche
        {
            get => vitesseMarche > 0f ? Mathf.Min(vitesseMarche, vitesseCourse) : vitesseCourse * 0.5f;
            set => vitesseMarche = Mathf.Max(0f, value);
        }
        public bool IsDead => isDead;

        /// <summary>Durée du clip "Death" du contrôleur (0 si introuvable).</summary>
        public float DeathClipLength
        {
            get
            {
                RuntimeAnimatorController ctrl = animator != null ? animator.runtimeAnimatorController : null;
                if (ctrl == null) return 0f;
                foreach (AnimationClip clip in ctrl.animationClips)
                {
                    if (clip != null && clip.name == DeathClipName) return clip.length;
                }
                return 0f;
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            TryGetComponent(out agent);
            TryGetComponent(out body);
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (isDead || animator == null)
            {
                return;
            }

            float normalized = hasSpeedOverride ? speedOverride : Normalize(MeasureSpeed());
            animator.SetFloat(VitesseHash, normalized, dampTime, Time.deltaTime);
        }

        /// <summary>
        /// Vitesse en m/s vers le paramètre Vitesse : 0 → 0, marche → 0,5, course → 1 (linéaire entre les deux).
        /// </summary>
        public float Normalize(float speed)
        {
            float walk = VitesseMarche;
            if (speed <= walk)
            {
                return walk <= 0f ? 0f : 0.5f * Mathf.Clamp01(speed / walk);
            }
            float span = vitesseCourse - walk;
            return span <= 0f ? 1f : Mathf.Clamp01(0.5f + 0.5f * (speed - walk) / span);
        }

        private float MeasureSpeed()
        {
            if (agent != null && agent.enabled)
            {
                Vector3 v = agent.velocity;
                v.y = 0f;
                return v.magnitude;
            }

            if (body != null && !body.isKinematic)
            {
                Vector3 v = body.linearVelocity;
                v.y = 0f;
                return v.magnitude;
            }

            // Mesure par le déplacement, moyennée sur une courte fenêtre : diviser le déplacement d'une
            // image par la durée de l'image suivante donne une vitesse fausse quand les images varient.
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;
            windowDistance += delta.magnitude;
            windowTime += Time.deltaTime;
            if (windowTime >= SpeedWindow)
            {
                measuredSpeed = windowDistance / windowTime;
                windowDistance = 0f;
                windowTime = 0f;
            }
            return measuredSpeed;
        }

        /// <summary>Force la vitesse normalisée (0 à 1), par exemple pour un test ou une cinématique.</summary>
        public void SetSpeedOverride(float normalizedSpeed)
        {
            hasSpeedOverride = true;
            speedOverride = Mathf.Clamp01(normalizedSpeed);
        }

        /// <summary>Revient à la vitesse mesurée.</summary>
        public void ClearSpeedOverride()
        {
            hasSpeedOverride = false;
        }

        /// <summary>Lance l'animation d'attaque. Renvoie false si elle est ignorée (mort, déjà en train d'attaquer).</summary>
        public bool PlayAttack()
        {
            if (isDead || IsInOrEntering(AttackState))
            {
                return false;
            }

            animator.SetTrigger(AttaqueHash);
            return true;
        }

        /// <summary>Lance l'animation de dégâts. Renvoie false si elle est ignorée (mort, délai minimum, déjà en cours).</summary>
        public bool PlayHit()
        {
            if (isDead || Time.time - lastHitTime < hitCooldown || IsInOrEntering(HitState))
            {
                return false;
            }

            lastHitTime = Time.time;
            animator.SetTrigger(DegatsHash);
            return true;
        }

        /// <summary>Lance l'animation de mort. Ensuite, plus aucune animation n'est jouée.</summary>
        public void PlayDeath()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            animator.ResetTrigger(AttaqueHash);
            animator.ResetTrigger(DegatsHash);
            animator.SetFloat(VitesseHash, 0f);
            animator.SetTrigger(MortHash);
        }

        // Vrai si l'Animator est dans cet état (et n'en sort pas) ou est en train d'y entrer.
        // Pendant le fondu de sortie (ex. Attack -> Locomotion), on peut déjà relancer l'animation.
        private bool IsInOrEntering(string stateName)
        {
            if (animator.IsInTransition(0))
            {
                return animator.GetNextAnimatorStateInfo(0).IsName(stateName);
            }

            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }
    }
}
