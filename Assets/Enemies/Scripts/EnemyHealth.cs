using System;
using UnityEngine;
using UnityEngine.AI;

namespace SwordBound.Enemies
{
    /// <summary>
    /// Points de vie d'un ennemi. La mort ne se produit qu'une seule fois : animation "Mort",
    /// colliders et IA désactivés, corps figé, puis destruction après un délai.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Points de vie")]
        [SerializeField, Min(1)] private int maxHealth = 100;

        [Header("Mort")]
        [Tooltip("Délai avant la destruction de l'objet. Allongé automatiquement si l'animation de mort est plus longue.")]
        [SerializeField, Min(0f)] private float destroyDelay = 4f;
        [Tooltip("Marge ajoutée après la fin de l'animation de mort avant la destruction.")]
        [SerializeField, Min(0f)] private float deathAnimationMargin = 0.5f;
        [Tooltip("Composants à désactiver à la mort (ex. : l'IA). Le NavMeshAgent est désactivé automatiquement.")]
        [SerializeField] private Behaviour[] disableOnDeath = Array.Empty<Behaviour>();

        private int currentHealth;
        private bool isDead;
        private EnemyAnimation enemyAnimation;

        /// <summary>PV courants et PV max, à chaque changement.</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Appelé une seule fois, à la mort de cet ennemi.</summary>
        public event Action<EnemyHealth> OnDied;

        /// <summary>Appelé à la mort de n'importe quel ennemi (ex. : GameManager qui compte les ennemis restants).</summary>
        public static event Action<EnemyHealth> AnyEnemyDied;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;

        /// <summary>Délai réel entre la mort et la destruction (au moins la durée de l'animation de mort + marge).</summary>
        public float EffectiveDestroyDelay
        {
            get
            {
                float clip = enemyAnimation != null ? enemyAnimation.DeathClipLength : 0f;
                return Mathf.Max(destroyDelay, clip + deathAnimationMargin);
            }
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            enemyAnimation = GetComponent<EnemyAnimation>();
        }

        /// <summary>Retire des PV. Ignoré si amount &lt;= 0 ou si l'ennemi est déjà mort.</summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0 || isDead)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth == 0)
            {
                Die();
            }
            else if (enemyAnimation != null)
            {
                enemyAnimation.PlayHit();
            }
        }

        /// <summary>Fixe les PV max et remet les PV courants au max (ex. : à l'apparition, selon l'étage).</summary>
        public void SetMaxHealth(int value)
        {
            if (isDead)
            {
                return;
            }

            maxHealth = Mathf.Max(1, value);
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>Multiplie les PV max (ex. : 1.5 à l'étage 2) et remet les PV courants au max.</summary>
        public void ScaleMaxHealth(float multiplier)
        {
            if (multiplier <= 0f)
            {
                return;
            }

            SetMaxHealth(Mathf.RoundToInt(maxHealth * multiplier));
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;

            if (enemyAnimation != null)
            {
                enemyAnimation.PlayDeath();
            }

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            // Sans collider, un Rigidbody dynamique tomberait à travers le sol.
            if (TryGetComponent(out Rigidbody body))
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }

            if (TryGetComponent(out NavMeshAgent agent))
            {
                agent.enabled = false;
            }

            foreach (Behaviour b in disableOnDeath)
            {
                if (b != null)
                {
                    b.enabled = false;
                }
            }

            OnDied?.Invoke(this);
            AnyEnemyDied?.Invoke(this);

            Destroy(gameObject, EffectiveDestroyDelay);
        }

        // Remet l'événement statique à zéro à chaque Play (utile si "Enter Play Mode Options" est activé).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyEnemyDied = null;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
        }
    }
}
