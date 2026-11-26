using UnityEngine;

namespace SwordBound.Enemies
{
    /// <summary>
    /// Détruit un effet de particules une fois qu'il est terminé (durée + durée de vie des particules),
    /// sauf s'il se détruit déjà tout seul (Stop Action = Destroy sur le système racine).
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoDestroyEffect : MonoBehaviour
    {
        [Tooltip("Délai forcé (s). 0 = calculé à partir des systèmes de particules.")]
        [SerializeField, Min(0f)] private float overrideLifetime = 0f;
        [Tooltip("Délai utilisé si un système boucle (sa durée serait infinie).")]
        [SerializeField, Min(0.1f)] private float loopingFallback = 3f;
        [Tooltip("Marge ajoutée après la fin calculée.")]
        [SerializeField, Min(0f)] private float extraDelay = 0.2f;

        /// <summary>Durée de vie retenue (s) avant destruction.</summary>
        public float Lifetime { get; private set; }

        private void Awake()
        {
            Lifetime = overrideLifetime > 0f ? overrideLifetime : ComputeLifetime(GetComponentsInChildren<ParticleSystem>(true)) + extraDelay;

            if (TryGetComponent(out ParticleSystem root) && root.main.stopAction == ParticleSystemStopAction.Destroy && !root.main.loop)
            {
                return; // l'effet se détruit déjà tout seul
            }

            Destroy(gameObject, Lifetime);
        }

        /// <summary>Temps (s) jusqu'à la disparition de la dernière particule.</summary>
        public float ComputeLifetime(ParticleSystem[] systems)
        {
            float longest = 0f;
            foreach (ParticleSystem ps in systems)
            {
                ParticleSystem.MainModule main = ps.main;
                if (main.loop)
                {
                    return loopingFallback;
                }
                float end = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                longest = Mathf.Max(longest, end / Mathf.Max(0.01f, main.simulationSpeed));
            }
            return longest > 0f ? longest : loopingFallback;
        }
    }
}
