using SwordBound.Enemies;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// TEMPORAIRE (scène EnemySandbox) : piloter l'ennemi au clavier pour vérifier ses animations et son IA.
/// 1 = 10 dégâts, 2 = attaque, 3 = tuer, R = faire réapparaître.
/// Flèche haut/bas = forcer la vitesse (Idle / Walk / Run), V = rendre la vitesse à l'IA.
/// I/J/K/L = déplacer le DummyPlayer, H = le soigner.
/// </summary>
public class EnemyTestDriver : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private EnemyHealth enemy;
    [SerializeField] private int damage = 10;
    [SerializeField] private Rigidbody dummyPlayer;
    [SerializeField] private float dummySpeed = 4f;

    private static readonly float[] SpeedSteps = { 0f, 0.5f, 1f };
    private int speedIndex;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Transform[] spawnPatrolPoints;
    private string lastAction = "-";

    private void Start()
    {
        if (enemy != null)
        {
            spawnPosition = enemy.transform.position;
            spawnRotation = enemy.transform.rotation;
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
        else if (Pressed(7))
        {
            EnemyAnimation anim = enemy != null ? enemy.GetComponent<EnemyAnimation>() : null;
            if (anim != null) { anim.ClearSpeedOverride(); lastAction = "Vitesse : IA"; }
        }
        else if (Pressed(8))
        {
            TargetDummyHealth dummy = dummyPlayer != null ? dummyPlayer.GetComponent<TargetDummyHealth>() : null;
            if (dummy != null) { dummy.Revive(); lastAction = "DummyPlayer soigné"; }
        }
    }

    private void FixedUpdate()
    {
        if (dummyPlayer == null) return;
        Vector2 input = DummyInput();
        Vector3 v = dummyPlayer.linearVelocity;
        v.x = input.x * dummySpeed;
        v.z = input.y * dummySpeed;
        dummyPlayer.linearVelocity = v;
    }

    private void ApplySpeed()
    {
        EnemyAnimation anim = enemy != null ? enemy.GetComponent<EnemyAnimation>() : null;
        if (anim != null) anim.SetSpeedOverride(SpeedSteps[speedIndex]);
        lastAction = "Vitesse forcée " + SpeedSteps[speedIndex];
    }

    private void Respawn()
    {
        if (enemyPrefab == null) return;
        if (enemy != null)
        {
            EnemyAI oldAi = enemy.GetComponent<EnemyAI>();
            if (oldAi != null && spawnPatrolPoints == null) spawnPatrolPoints = PatrolPointsInScene();
            Destroy(enemy.gameObject);
        }
        GameObject go = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
        go.name = "Enemy";
        enemy = go.GetComponent<EnemyHealth>();
        EnemyAI ai = go.GetComponent<EnemyAI>();
        if (ai != null) ai.SetPatrolPoints(spawnPatrolPoints ?? PatrolPointsInScene());
        speedIndex = 0;
        lastAction = "Réapparition";
    }

    private static Transform[] PatrolPointsInScene()
    {
        GameObject parent = GameObject.Find("PatrolPoints");
        if (parent == null) return new Transform[0];
        Transform[] points = new Transform[parent.transform.childCount];
        for (int i = 0; i < points.Length; i++) points[i] = parent.transform.GetChild(i);
        return points;
    }

    private static Vector2 DummyInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = (k.lKey.isPressed ? 1f : 0f) - (k.jKey.isPressed ? 1f : 0f);
        float y = (k.iKey.isPressed ? 1f : 0f) - (k.kKey.isPressed ? 1f : 0f);
#else
        float x = (Input.GetKey(KeyCode.L) ? 1f : 0f) - (Input.GetKey(KeyCode.J) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.I) ? 1f : 0f) - (Input.GetKey(KeyCode.K) ? 1f : 0f);
#endif
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    // 1..3 = touches 1 à 3, 4 = flèche haut, 5 = flèche bas, 6 = R, 7 = V, 8 = H
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
            case 7: return k.vKey.wasPressedThisFrame;
            case 8: return k.hKey.wasPressedThisFrame;
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
            case 7: return Input.GetKeyDown(KeyCode.V);
            case 8: return Input.GetKeyDown(KeyCode.H);
        }
        return false;
#endif
    }

    private void OnGUI()
    {
        EnemyAI ai = enemy != null ? enemy.GetComponent<EnemyAI>() : null;
        TargetDummyHealth dummy = dummyPlayer != null ? dummyPlayer.GetComponent<TargetDummyHealth>() : null;
        string hp = enemy != null ? enemy.CurrentHealth + " / " + enemy.MaxHealth + (enemy.IsDead ? " (mort)" : "") : "aucun ennemi";
        string dummyHp = dummy != null ? dummy.CurrentHealth + " / " + dummy.MaxHealth : "-";

        GUI.Box(new Rect(10, 10, 380, 160), GUIContent.none);
        GUI.Label(new Rect(20, 16, 370, 150),
            "1 : 10 dégâts   2 : attaque   3 : tuer   R : réapparition\n" +
            "Flèches : forcer la vitesse   V : vitesse de l'IA\n" +
            "I/J/K/L : déplacer le DummyPlayer   H : le soigner\n" +
            "Ennemi : " + hp + "   État : " + (ai != null ? ai.State.ToString() : "-") + "\n" +
            "DummyPlayer : " + dummyHp + "\n" +
            "Dernière action : " + lastAction);
    }
}
