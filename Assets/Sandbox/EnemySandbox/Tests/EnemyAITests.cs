#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SwordBound.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// TEMPORAIRE : tests PlayMode de l'IA des ennemis (étape 3).
public class EnemyAITests
{
    private const string PrefabPath = "Assets/Enemies/Prefabs/Enemy.prefab";

    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameObject enemy;

    private EnemyAI AI => enemy.GetComponent<EnemyAI>();
    private EnemyHealth Health => enemy.GetComponent<EnemyHealth>();
    private Rigidbody Body => enemy.GetComponent<Rigidbody>();
    private Animator Animator => enemy.GetComponent<Animator>();

    // ------------------------------------------------------------- Outils

    private class TestTarget : MonoBehaviour, IDamageable
    {
        public readonly List<float> HitTimes = new List<float>();
        public int Total;
        public bool Dead;
        public bool IsDead => Dead;
        public void TakeDamage(int amount)
        {
            if (amount <= 0 || Dead) return;
            Total += amount;
            HitTimes.Add(Time.time);
        }
    }

    private class MessageOnlyTarget : MonoBehaviour
    {
        public int Received;
        public void TakeDamage(int amount) { Received += amount; }
    }

    [SetUp]
    public void SetUp()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        spawned.Add(ground);
        enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Vector3.zero, Quaternion.identity);
        spawned.Add(enemy);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null) Object.Destroy(go);
        }
        spawned.Clear();
    }

    private static void Set(Object o, string field, float value)
    {
        var so = new SerializedObject(o);
        SerializedProperty p = so.FindProperty(field);
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private GameObject Target(Vector3 flatPosition, bool damageable = true)
    {
        GameObject t = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        t.name = "TestTarget";
        t.transform.position = new Vector3(flatPosition.x, 1f, flatPosition.z);
        if (damageable) t.AddComponent<TestTarget>();
        spawned.Add(t);
        return t;
    }

    private GameObject Wall(Vector3 center, Vector3 size)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "TestWall";
        w.transform.position = center;
        w.transform.localScale = size;
        spawned.Add(w);
        return w;
    }

    private Transform Point(Vector3 position)
    {
        GameObject p = new GameObject("TestPoint");
        p.transform.position = position;
        spawned.Add(p);
        return p.transform;
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private IEnumerator WaitForState(EnemyState wanted, float timeout)
    {
        float end = Time.time + timeout;
        while (AI.State != wanted && Time.time < end) yield return null;
    }

    // --------------------------------------------------------------- Patrouille

    [UnityTest]
    public IEnumerator Patrouille_PointsDansLOrdre_EnBoucle_AvecAttente()
    {
        Set(AI, "waitTimeMin", 0.5f);
        Set(AI, "waitTimeMax", 0.5f);
        Transform[] points = { Point(new Vector3(0, 0, 3)), Point(new Vector3(3, 0, 3)), Point(new Vector3(3, 0, 0)) };
        AI.SetPatrolPoints(points);

        var reached = new List<int>();
        var waits = new List<float>();
        bool wasWaiting = false;
        float waitStart = 0f;
        float end = Time.time + 25f;
        while (reached.Count < 5 && Time.time < end)
        {
            if (AI.IsWaitingAtPoint && !wasWaiting)
            {
                reached.Add(AI.PatrolIndex);
                waitStart = Time.time;
                Assert.Less(Flat(enemy.transform.position, points[AI.PatrolIndex].position), 0.6f, "arrêt sur le point");
            }
            if (!AI.IsWaitingAtPoint && wasWaiting) waits.Add(Time.time - waitStart);
            if (AI.IsWaitingAtPoint)
                Assert.Less(new Vector2(Body.linearVelocity.x, Body.linearVelocity.z).magnitude, 0.3f, "immobile pendant l'attente");
            wasWaiting = AI.IsWaitingAtPoint;
            Assert.AreEqual(EnemyState.Patrol, AI.State);
            yield return null;
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 1 }, reached, "ordre de visite");
        Assert.GreaterOrEqual(waits.Count, 3);
        foreach (float w in waits) Assert.AreEqual(0.5f, w, 0.15f, "durée d'attente");
    }

    [UnityTest]
    public IEnumerator SansPoints_SansCible_AucuneErreur()
    {
        LogAssert.NoUnexpectedReceived();
        Vector3 start = enemy.transform.position;
        AI.SetPatrolPoints(null);
        yield return new WaitForSeconds(2.5f); // couvre deux recherches de cible
        Assert.AreEqual(EnemyState.Patrol, AI.State);
        Assert.IsNull(AI.Target);
        Assert.Less(Flat(start, enemy.transform.position), 0.5f, "reste à sa position de départ");
    }

    [UnityTest]
    public IEnumerator Cible_TrouveeParLeTagPlayer()
    {
        GameObject player = Target(new Vector3(30, 0, 0));
        player.tag = "Player";
        yield return new WaitForSeconds(1.3f);
        Assert.AreEqual(player.transform, AI.Target);
        Assert.AreEqual(EnemyState.Patrol, AI.State, "trop loin pour être vu");
    }

    // ----------------------------------------------------------------- Détection

    [UnityTest]
    public IEnumerator Detection_APortee_Poursuite()
    {
        AI.SetTarget(Target(new Vector3(0, 0, 8)).transform);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        Assert.AreEqual(EnemyState.Chase, AI.State);
    }

    [UnityTest]
    public IEnumerator Detection_HorsDePortee_PasDePoursuite()
    {
        AI.SetTarget(Target(new Vector3(0, 0, 15)).transform);
        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(EnemyState.Patrol, AI.State);
    }

    [UnityTest]
    public IEnumerator Mur_BloqueLaVue_PuisSansMur_Poursuite()
    {
        GameObject wall = Wall(new Vector3(0, 1.5f, 4), new Vector3(6, 3, 0.5f));
        AI.SetTarget(Target(new Vector3(0, 0, 8)).transform);
        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(EnemyState.Patrol, AI.State, "mur : pas de poursuite");
        Object.Destroy(wall);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        Assert.AreEqual(EnemyState.Chase, AI.State, "sans mur : poursuite");
    }

    [UnityTest]
    public IEnumerator AutreEnnemi_NeCachePasLeJoueur()
    {
        GameObject other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), new Vector3(0, 0, 4), Quaternion.identity);
        spawned.Add(other);
        AI.SetTarget(Target(new Vector3(0, 0, 8)).transform);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        Assert.AreEqual(EnemyState.Chase, AI.State);
    }

    // ---------------------------------------------------------------- Perte

    [UnityTest]
    public IEnumerator Perte_TropLoin_Retour_PuisPatrouille()
    {
        GameObject t = Target(new Vector3(0, 0, 8));
        AI.SetTarget(t.transform);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        yield return new WaitForSeconds(0.3f);
        t.transform.position = new Vector3(0, 1, 40);
        yield return WaitForState(EnemyState.Return, 0.5f);
        Assert.AreEqual(EnemyState.Return, AI.State, "joueur trop loin : retour");
        yield return WaitForState(EnemyState.Patrol, 5f);
        Assert.AreEqual(EnemyState.Patrol, AI.State, "retour terminé : patrouille");
        Assert.Less(Flat(Vector3.zero, enemy.transform.position), 0.6f, "revenu à sa position de départ");
    }

    [UnityTest]
    public IEnumerator Perte_InvisibleTropLongtemps_Retour()
    {
        GameObject t = Target(new Vector3(0, 0, 8));
        AI.SetTarget(t.transform);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        Wall(new Vector3(0, 1.5f, 5.5f), new Vector3(30, 3, 0.5f)); // grand mur : plus aucune ligne de vue
        float hidden = Time.time;
        yield return WaitForState(EnemyState.Return, 5f);
        Assert.AreEqual(EnemyState.Return, AI.State);
        Assert.AreEqual(3f, Time.time - hidden, 0.4f, "perdu après loseSightTime");
    }

    [UnityTest]
    public IEnumerator Retour_RevoitLeJoueur_RepartEnPoursuite()
    {
        GameObject t = Target(new Vector3(0, 0, 8));
        AI.SetTarget(t.transform);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        t.transform.position = new Vector3(0, 1, 40);
        yield return WaitForState(EnemyState.Return, 0.5f);
        t.transform.position = enemy.transform.position + new Vector3(0, 1, 6);
        yield return WaitForState(EnemyState.Chase, 0.5f);
        Assert.AreEqual(EnemyState.Chase, AI.State);
    }

    // ----------------------------------------------------------------- Attaque

    [UnityTest]
    public IEnumerator Attaque_UnCoupParCycle_CooldownRespecte()
    {
        TestTarget t = Target(new Vector3(0, 0, 1.3f)).GetComponent<TestTarget>();
        AI.SetTarget(t.transform);
        float start = Time.time;
        yield return new WaitForSeconds(3.3f);
        Assert.AreEqual(EnemyState.Attack, AI.State);
        Assert.AreEqual(2, t.HitTimes.Count, "coups à ~0,4 s et ~1,9 s");
        Assert.AreEqual(2 * AI.AttackDamage, t.Total, "un seul coup par cycle");
        Assert.AreEqual(0.4f, t.HitTimes[0] - start, 0.15f, "attackHitDelay");
        Assert.GreaterOrEqual(t.HitTimes[1] - t.HitTimes[0], AI.AttackCooldown - 0.05f, "cooldown");
    }

    [UnityTest]
    public IEnumerator Attaque_JoueurSortDeLaPortee_AvantLeCoup_AucunDegat()
    {
        TestTarget t = Target(new Vector3(0, 0, 1.3f)).GetComponent<TestTarget>();
        AI.SetTarget(t.transform);
        float end = Time.time + 2f;
        while (!AI.IsSwinging && Time.time < end) yield return null;
        Assert.IsTrue(AI.IsSwinging, "coup lancé");
        t.transform.position = new Vector3(0, 1, 6);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(0, t.Total, "coup raté");
    }

    [UnityTest]
    public IEnumerator Attaque_JoueurMort_AucunDegat()
    {
        TestTarget t = Target(new Vector3(0, 0, 1.3f)).GetComponent<TestTarget>();
        t.Dead = true;
        AI.SetTarget(t.transform);
        yield return new WaitForSeconds(2.5f);
        Assert.AreEqual(0, t.HitTimes.Count);
        Assert.AreNotEqual(EnemyState.Attack, AI.State);
        Assert.AreNotEqual(EnemyState.Chase, AI.State);
    }

    [UnityTest]
    public IEnumerator Attaque_SendMessage_SansIDamageable()
    {
        MessageOnlyTarget t = Target(new Vector3(0, 0, 1.3f), damageable: false).AddComponent<MessageOnlyTarget>();
        AI.SetTarget(t.transform);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(AI.AttackDamage, t.Received);
    }

    [UnityTest]
    public IEnumerator Degats_SetEtScale()
    {
        yield return null;
        AI.SetAttackDamage(12);
        Assert.AreEqual(12, AI.AttackDamage);
        AI.ScaleAttackDamage(1.5f);
        Assert.AreEqual(18, AI.AttackDamage);
        AI.ScaleAttackDamage(0f);
        AI.SetAttackDamage(-3);
        Assert.AreEqual(0, AI.AttackDamage);
    }

    // ------------------------------------------------------------ Réactions

    [UnityTest]
    public IEnumerator DegatsRecus_PoursuiteMemeHorsDePortee()
    {
        GameObject t = Target(new Vector3(0, 0, 25));
        AI.SetTarget(t.transform);
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(EnemyState.Patrol, AI.State);
        float before = Flat(enemy.transform.position, t.transform.position);
        Health.TakeDamage(5);
        Assert.AreEqual(EnemyState.Chase, AI.State, "poursuite immédiate");
        yield return new WaitForSeconds(1.5f);
        Assert.Less(Flat(enemy.transform.position, t.transform.position), before - 2f, "se rapproche");
    }

    [UnityTest]
    public IEnumerator Rotation_Progressive()
    {
        AI.SetTarget(Target(new Vector3(0, 0, -6)).transform); // derrière l'ennemi
        yield return new WaitForFixedUpdate();
        Quaternion previous = Body.rotation;
        float maxSpeed = 0f;
        for (float t = 0f; t < 1f; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            float speed = Quaternion.Angle(previous, Body.rotation) / Time.fixedDeltaTime;
            maxSpeed = Mathf.Max(maxSpeed, speed);
            previous = Body.rotation;
        }
        Assert.LessOrEqual(maxSpeed, AI.TurnSpeed * 1.02f, "jamais plus de turnSpeed degrés/s");
        Assert.Greater(maxSpeed, AI.TurnSpeed * 0.5f, "a bien tourné");
        Assert.Less(Vector3.Angle(enemy.transform.forward, Vector3.back), 10f, "face au joueur");
    }

    [UnityTest]
    public IEnumerator Mort_PendantUneAttaque_NiCoupNiMouvement()
    {
        TestTarget t = Target(new Vector3(0, 0, 1.3f)).GetComponent<TestTarget>();
        AI.SetTarget(t.transform);
        float end = Time.time + 2f;
        while (!AI.IsSwinging && Time.time < end) yield return null;
        Assert.IsTrue(AI.IsSwinging);
        Health.TakeDamage(Health.MaxHealth);
        Assert.AreEqual(EnemyState.Dead, AI.State);
        Vector3 pos = enemy.transform.position;
        t.transform.position = new Vector3(0, 1, 5); // tente de l'attirer
        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(0, t.Total, "aucun dégât après la mort");
        Assert.AreEqual(EnemyState.Dead, AI.State);
        Assert.Less(Vector3.Distance(pos, enemy.transform.position), 0.02f, "ne bouge plus");
        Assert.IsFalse(AI.IsSwinging);
    }

    // --------------------------------------------------------------- Animation

    [UnityTest]
    public IEnumerator Animation_Marche_VitesseDemi()
    {
        AI.SetPatrolPoints(new[] { Point(new Vector3(0, 0, 15)) });
        yield return new WaitForSeconds(2f);
        Assert.AreEqual(0.5f, Animator.GetFloat(EnemyAnimation.VitesseHash), 0.06f);
    }

    [UnityTest]
    public IEnumerator Animation_Course_VitesseUn()
    {
        AI.SetTarget(Target(new Vector3(0, 0, 11.5f)).transform);
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(EnemyState.Chase, AI.State);
        Assert.Greater(Animator.GetFloat(EnemyAnimation.VitesseHash), 0.94f);
    }
}
#endif
