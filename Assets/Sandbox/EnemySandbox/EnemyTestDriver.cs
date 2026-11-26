using SwordBound.Enemies;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// TEMPORAIRE (scène EnemySandbox) : piloter l'ennemi au clavier pour vérifier ses animations.
/// 1 = 10 dégâts, 2 = attaque, 3 = tuer, Flèche haut/bas = vitesse (Idle / Walk / Run), R = faire réapparaître.
/// </summary>
public class EnemyTestDriver : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private EnemyHealth enemy;
    [SerializeField] private int damage = 10;

    private static readonly float[] SpeedSteps = { 0f, 0.5f, 1f };
    private int speedIndex;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private string lastAction = "-";

    private void Start()
    {
        if (enemy != null)
        {
            spawnPosition = enemy.transform.position;
            spawnRotation = enemy.transform.rotation;
            ApplySpeed();
        }
    }

    private void Update()
    {
        if (Pressed(1))
        {
            if (enemy != null) { enemy.TakeDamage(damage); lastAction = "Dégâts " + damage; }
        }
        else if (Pressed(2))
        {
            EnemyAnimation anim = enemy != null ? enemy.GetComponent<EnemyAnimation>() : null;
            if (anim != null) lastAction = anim.PlayAttack() ? "Attaque" : "Attaque ignorée";
        }
        else if (Pressed(3))
        {
            if (enemy != null) { enemy.TakeDamage(enemy.CurrentHealth); lastAction = "Tué"; }
        }
        else if (Pressed(4))
        {
            speedIndex = Mathf.Min(speedIndex + 1, SpeedSteps.Length - 1);
            ApplySpeed();
        }
        else if (Pressed(5))
        {
            speedIndex = Mathf.Max(speedIndex - 1, 0);
            ApplySpeed();
        }
        else if (Pressed(6))
        {
            Respawn();
        }
    }

    private void ApplySpeed()
    {
        EnemyAnimation anim = enemy != null ? enemy.GetComponent<EnemyAnimation>() : null;
        if (anim != null) anim.SetSpeedOverride(SpeedSteps[speedIndex]);
        lastAction = "Vitesse " + SpeedSteps[speedIndex];
    }

    private void Respawn()
    {
        if (enemyPrefab == null) return;
        if (enemy != null) Destroy(enemy.gameObject);
        GameObject go = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
        go.name = "Enemy";
        enemy = go.GetComponent<EnemyHealth>();
        speedIndex = 0;
        ApplySpeed();
        lastAction = "Réapparition";
    }

    // 1..3 = touches 1 à 3, 4 = flèche haut, 5 = flèche bas, 6 = R
    private static bool Pressed(int action)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return false;
        switch (action)
        {
            case 1: return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
            case 2: return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
            case 3: return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
            case 4: return k.upArrowKey.wasPressedThisFrame;
            case 5: return k.downArrowKey.wasPressedThisFrame;
            case 6: return k.rKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (action)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3);
            case 4: return Input.GetKeyDown(KeyCode.UpArrow);
            case 5: return Input.GetKeyDown(KeyCode.DownArrow);
            case 6: return Input.GetKeyDown(KeyCode.R);
        }
        return false;
#endif
    }

    private void OnGUI()
    {
        string hp = enemy != null ? enemy.CurrentHealth + " / " + enemy.MaxHealth + (enemy.IsDead ? " (mort)" : "") : "aucun ennemi";
        GUI.Box(new Rect(10, 10, 330, 112), GUIContent.none);
        GUI.Label(new Rect(20, 16, 320, 100),
            "1 : 10 dégâts   2 : attaque   3 : tuer\n" +
            "Flèche haut/bas : vitesse   R : réapparition\n" +
            "PV : " + hp + "\n" +
            "Vitesse : " + SpeedSteps[speedIndex] + "\n" +
            "Dernière action : " + lastAction);
    }
}
