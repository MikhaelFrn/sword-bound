namespace SwordBound.Enemies
{
    /// <summary>
    /// Tout ce qui peut recevoir des dégâts (ennemis, boss...).
    /// Exemple côté joueur : if (hit.collider.GetComponentInParent&lt;IDamageable&gt;() is IDamageable cible) cible.TakeDamage(10);
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Retire des points de vie. Les valeurs inférieures ou égales à 0 sont ignorées.</summary>
        void TakeDamage(int amount);

        /// <summary>Vrai une fois la cible morte (elle n'accepte plus de dégâts).</summary>
        bool IsDead { get; }
    }
}
