using SwordBound.Enemies;
using UnityEngine;

/// <summary>
/// TEMPORAIRE (scène EnemySandbox) : faux joueur qui encaisse les coups des ennemis et les affiche dans la Console.
/// </summary>
public class TargetDummyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int maxHealth = 100;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log("[DummyPlayer] -" + amount + " PV (" + currentHealth + "/" + maxHealth + ")" + (IsDead ? " : mort" : ""), this);
    }

    /// <summary>Remet les PV au max (touche H dans EnemyTestDriver).</summary>
    public void Revive()
    {
        currentHealth = maxHealth;
        Debug.Log("[DummyPlayer] PV remis à " + maxHealth, this);
    }
}
