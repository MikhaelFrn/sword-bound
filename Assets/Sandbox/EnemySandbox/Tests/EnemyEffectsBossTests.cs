#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SwordBound.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// TEMPORAIRE : tests PlayMode des effets et du boss (étape 4).
public class EnemyEffectsBossTests
{
    private const string EnemyPath = "Assets/Enemies/Prefabs/Enemy.prefab";
    private const string BossPath = "Assets/Enemies/Prefabs/Boss.prefab";
    private const string ScenePath = "Assets/Sandbox/EnemySandbox/EnemySandbox.unity";

    private readonly List<GameObject> spawned = new List<GameObject>();

    private static GameObject EnemyPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
    private static GameObject BossPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);

    private class TestTarget : MonoBehaviour, IDamageable
    {
        public int Total;
        public bool IsDead => false;
        public void TakeDamage(int amount) { if (amount > 0) Total += amount; }
    }

    [SetUp]
    public void SetUp()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        spawned.Add(ground);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) if (go != null) Object.Destroy(go);
        spawned.Clear();
        foreach (AutoDestroyEffect fx in Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None)) Object.Destroy(fx.gameObject);
    }

    private GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject go = Object.Instantiate(prefab, position, Quaternion.identity);
        spawned.Add(go);
        return go;
    }

    private static int CountEffects(string prefabName)
    {
        return Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None).Count(fx => fx.name.StartsWith(prefabName));
    }

    // ----------------------------------------------------------------- Effets

    [UnityTest]
    public IEnumerator Impact_AChaqueCoup_PasALaMort_MortUneSeuleFois()
    {
        EnemyHealth health = Spawn(EnemyPrefab, Vector3.zero).GetComponent<EnemyHealth>();
        yield return null;
        health.TakeDamage(10);
        health.TakeDamage(10);
        health.TakeDamage(10);
        Assert.AreEqual(3, CountEffects("Fx_EnemyHit"), "un impact par coup");
        Assert.AreEqual(0, CountEffects("Fx_EnemyDeath"));

        health.TakeDamage(health.CurrentHealth);
        health.TakeDamage(50);
        yield return null;
        Assert.AreEqual(3, CountEffects("Fx_EnemyHit"), "pas d'impact au coup mortel ni après");
        Assert.AreEqual(1, CountEffects("Fx_EnemyDeath"), "un seul effet de mort");
    }

    [UnityTest]
    public IEnumerator Effets_PasEnfantsDeLEnnemi_ALaBonneHauteur()
    {
        EnemyHealth health = Spawn(EnemyPrefab, new Vector3(2f, 0f, 3f)).GetComponent<EnemyHealth>();
        yield return null;
        health.TakeDamage(10);
        AutoDestroyEffect fx = Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None).First();
        Assert.IsNull(fx.transform.parent, "pas en enfant de l'ennemi");
        Assert.AreEqual(health.transform.position.y + health.EffectHeight, fx.transform.position.y, 0.01f);
        Assert.AreEqual(2f, fx.transform.position.x, 0.01f);
    }

    [UnityTest]
    public IEnumerator Effets_DetruitsApresLeurDuree_EtNeBouclentPas()
    {
        EnemyHealth health = Spawn(EnemyPrefab, Vector3.zero).GetComponent<EnemyHealth>();
        yield return null;
        health.TakeDamage(10);
        health.TakeDamage(health.CurrentHealth);
        yield return null;
        AutoDestroyEffect[] all = Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None);
        Assert.AreEqual(2, all.Length);
        float longest = all.Max(fx => fx.Lifetime);
        foreach (AutoDestroyEffect fx in all)
        {
            Assert.Greater(fx.Lifetime, 0.2f);
            Assert.Less(fx.Lifetime, 6f, "durée raisonnable");
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) Assert.IsFalse(ps.main.loop, "lecture unique");
        }
        yield return new WaitForSeconds(longest + 0.3f);
        Assert.AreEqual(0, Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None).Length, "effets détruits");
    }

    [UnityTest]
    public IEnumerator SansEffetAssigne_AucuneErreur()
    {
        LogAssert.NoUnexpectedReceived();
        GameObject enemy = Spawn(EnemyPrefab, Vector3.zero);
        var so = new SerializedObject(enemy.GetComponent<EnemyHealth>());
        so.FindProperty("hitEffectPrefab").objectReferenceValue = null;
        so.FindProperty("deathEffectPrefab").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        health.TakeDamage(10);
        health.TakeDamage(health.CurrentHealth);
        yield return null;
        Assert.IsTrue(health.IsDead);
        Assert.AreEqual(0, Object.FindObjectsByType<AutoDestroyEffect>(FindObjectsSortMode.None).Length);
    }

    [Test]
    public void Effets_PrefabsVariantes_MateriauxURP()
    {
        string[] fx = { "Fx_EnemyHit", "Fx_EnemyDeath", "Fx_BossHit", "Fx_BossDeath" };
        foreach (string name in fx)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Effects/" + name + ".prefab");
            Assert.IsNotNull(p, name);
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(p), name + " est une variante");
            Assert.IsNotNull(p.GetComponent<AutoDestroyEffect>(), name + " : AutoDestroyEffect");
            foreach (ParticleSystemRenderer r in p.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Assert.IsNotNull(r.sharedMaterial, name + " : matériau");
                Assert.IsTrue(r.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/"), name + " : shader URP (pas de rose) : " + r.sharedMaterial.shader.name);
                Assert.IsTrue(r.sharedMaterial.shader.isSupported, name + " : shader supporté");
            }
        }
        Assert.Greater(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Effects/Fx_BossHit.prefab").transform.localScale.x,
                       AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Effects/Fx_EnemyHit.prefab").transform.localScale.x, "effets du boss plus gros");
    }

    // ------------------------------------------------------------------- Boss

    [Test]
    public void Boss_EstUneVariante_AvecLesBonnesValeurs()
    {
        GameObject boss = BossPrefab;
        GameObject enemy = EnemyPrefab;
        Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(boss));
        Assert.AreEqual(enemy, PrefabUtility.GetCorrespondingObjectFromSource(boss), "variante de Enemy.prefab");

        EnemyHealth bh = boss.GetComponent<EnemyHealth>();
        EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
        Assert.IsTrue(bh.IsBoss);
        Assert.IsFalse(eh.IsBoss);
        Assert.AreEqual(eh.MaxHealth * 5, bh.MaxHealth, "PV x5");
        Assert.AreEqual("Fx_BossHit", bh.HitEffectPrefab.name);
        Assert.AreEqual("Fx_BossDeath", bh.DeathEffectPrefab.name);

        EnemyAI ai = boss.GetComponent<EnemyAI>();
        Assert.AreEqual(25, ai.AttackDamage);
        Assert.AreEqual(2.6f, ai.AttackRange, 0.001f);
        Assert.AreEqual(2f, ai.AttackCooldown, 0.001f);
        Assert.AreEqual(1.8f, ai.WalkSpeed, 0.001f);
        Assert.AreEqual(3.5f, ai.RunSpeed, 0.001f);
        Assert.AreEqual(20f, ai.DetectionRange, 0.001f);
        Assert.AreEqual(30f, ai.LoseRange, 0.001f);

        Assert.AreEqual(enemy.transform.localScale.x * 1.5f, boss.transform.localScale.x, 0.001f, "taille x1,5");
        Assert.Greater(boss.GetComponent<Rigidbody>().mass, enemy.GetComponent<Rigidbody>().mass);
        Renderer r = boss.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.AreEqual("Boss_Material", r.sharedMaterial.name, "matériau distinct");
    }

    [Test]
    public void Prefabs_EnemyEtBoss_SansReferenceManquante()
    {
        foreach (GameObject prefab in new[] { EnemyPrefab, BossPrefab })
        {
            foreach (Component c in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.IsNotNull(c, prefab.name + " : script manquant");
                SerializedProperty p = new SerializedObject(c).GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                        Assert.Fail(prefab.name + " : référence manquante " + c.GetType().Name + "." + p.propertyPath);
                }
            }
            Assert.IsNotNull(prefab.GetComponent<EnemyHealth>().HitEffectPrefab, prefab.name + " : effet d'impact assigné");
            Assert.IsNotNull(prefab.GetComponent<EnemyHealth>().DeathEffectPrefab, prefab.name + " : effet de mort assigné");
        }
    }

    [UnityTest]
    public IEnumerator Boss_Patrouille_Poursuit_Attaque()
    {
        GameObject bossGo = Spawn(BossPrefab, Vector3.zero);
        EnemyAI ai = bossGo.GetComponent<EnemyAI>();
        GameObject p = new GameObject("BossPoint");
        p.transform.position = new Vector3(0f, 0f, -4f);
        spawned.Add(p);
        ai.SetPatrolPoints(new[] { p.transform });
        yield return new WaitForSeconds(2f); // demi-tour (240°/s) puis marche
        Assert.AreEqual(EnemyState.Patrol, ai.State);
        Assert.Less(bossGo.transform.position.z, -0.5f, "patrouille : se dirige vers son point");

        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.transform.position = new Vector3(0f, 1f, 14f); // 14 m < détection du boss (20 m)
        TestTarget t = target.AddComponent<TestTarget>();
        spawned.Add(target);
        ai.SetTarget(target.transform);
        float end = Time.time + 1f;
        while (ai.State != EnemyState.Chase && Time.time < end) yield return null;
        Assert.AreEqual(EnemyState.Chase, ai.State, "poursuite à 14 m");

        end = Time.time + 10f;
        while (t.Total == 0 && Time.time < end) yield return null;
        Assert.AreEqual(EnemyState.Attack, ai.State);
        Assert.AreEqual(25, t.Total, "coup du boss : 25");
    }

    [UnityTest]
    public IEnumerator BossMort_AnyEnemyDied_AvecIsBoss()
    {
        EnemyHealth boss = Spawn(BossPrefab, Vector3.zero).GetComponent<EnemyHealth>();
        EnemyHealth normal = Spawn(EnemyPrefab, new Vector3(4f, 0f, 0f)).GetComponent<EnemyHealth>();
        yield return null;
        var deaths = new List<(EnemyHealth who, bool isBoss)>();
        System.Action<EnemyHealth> handler = h => deaths.Add((h, h.IsBoss));
        EnemyHealth.AnyEnemyDied += handler;
        try
        {
            normal.TakeDamage(normal.MaxHealth);
            boss.TakeDamage(boss.MaxHealth - 1);
            Assert.IsFalse(boss.IsDead, "le boss encaisse 5 fois plus");
            boss.TakeDamage(1);
            Assert.AreEqual(2, deaths.Count);
            Assert.IsFalse(deaths[0].isBoss);
            Assert.AreEqual(boss, deaths[1].who);
            Assert.IsTrue(deaths[1].isBoss);
            Assert.AreEqual(1, CountEffects("Fx_BossDeath"));
        }
        finally
        {
            EnemyHealth.AnyEnemyDied -= handler;
        }
    }

    // --------------------------------------------------------------- Captures

    [UnityTest]
    public IEnumerator Captures_Impact_Mort_Boss()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        Camera cam = Camera.main;
        GameObject enemy = GameObject.Find("Enemy");
        GameObject boss = GameObject.Find("Boss");
        GameObject dummy = GameObject.Find("DummyPlayer");
        Assert.IsNotNull(boss, "Boss dans la scène");

        // On fige les IA pour poser la scène (elles sont testées ailleurs).
        enemy.GetComponent<EnemyAI>().enabled = false;
        boss.GetComponent<EnemyAI>().enabled = false;
        dummy.SetActive(false);
        enemy.transform.SetPositionAndRotation(new Vector3(-1.4f, 0f, 2f), Quaternion.Euler(0f, 160f, 0f));
        boss.transform.SetPositionAndRotation(new Vector3(1.6f, 0f, 2.4f), Quaternion.Euler(0f, 200f, 0f));
        cam.transform.SetPositionAndRotation(new Vector3(0f, 2.6f, -5f), Quaternion.Euler(12f, 0f, 0f));
        cam.fieldOfView = 50f;
        yield return RenderFor(cam, 1f);
        Shot(cam, "fx_1_boss_et_ennemi.png");

        enemy.GetComponent<EnemyHealth>().TakeDamage(10);
        yield return RenderFor(cam, 0.3f);
        Shot(cam, "fx_2_impact.png");
        yield return RenderFor(cam, 0.6f); // laisse l'impact se terminer

        boss.GetComponent<EnemyHealth>().TakeDamage(10);
        yield return RenderFor(cam, 0.3f);
        Shot(cam, "fx_3_impact_boss.png");

        enemy.GetComponent<EnemyHealth>().TakeDamage(1000);
        yield return RenderFor(cam, 0.6f);
        Shot(cam, "fx_4_mort.png");
    }

    private static IEnumerator RenderFor(Camera cam, float seconds)
    {
        RenderTexture rt = new RenderTexture(320, 180, 24);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            yield return null;
        }
        rt.Release();
    }

    private static void Shot(Camera cam, string file)
    {
        string dir = System.Environment.GetEnvironmentVariable("SNAP_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        RenderTexture rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
    }
}
#endif
