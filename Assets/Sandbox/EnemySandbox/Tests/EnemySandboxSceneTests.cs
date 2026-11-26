#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using SwordBound.Enemies;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// TEMPORAIRE : la scène EnemySandbox en mode Play (driver branché, ennemi animé, captures).
public class EnemySandboxSceneTests
{
    private const string ScenePath = "Assets/Sandbox/EnemySandbox/EnemySandbox.unity";

    [UnityTest]
    public IEnumerator Sandbox_EnnemiAnime_PasEnTPose()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return new WaitForSeconds(1f);

        // EnemyTestDriver est dans Assembly-CSharp (invisible depuis cette asmdef) : recherche par nom.
        GameObject driverGo = GameObject.Find("EnemyTestDriver");
        Assert.IsNotNull(driverGo, "objet EnemyTestDriver présent");
        Component driver = driverGo.GetComponent("EnemyTestDriver");
        Assert.IsNotNull(driver, "script EnemyTestDriver présent (pas de script manquant)");
        var so = new UnityEditor.SerializedObject(driver);
        Assert.IsNotNull(so.FindProperty("enemyPrefab").objectReferenceValue, "driver.enemyPrefab");
        Assert.IsNotNull(so.FindProperty("enemy").objectReferenceValue, "driver.enemy");
        EnemyHealth health = GameObject.Find("Enemy").GetComponent<EnemyHealth>(); // (un Boss est aussi dans la scène)
        Assert.IsNotNull(health, "ennemi présent");
        Animator anim = health.GetComponent<Animator>();
        Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));

        // L'Animator est en "Cull Update Transforms" : les os ne bougent que si l'ennemi est vu.
        // En batch rien n'est affiché à l'écran, on rend donc la caméra à chaque image (= un joueur qui regarde).
        Camera cam = Camera.main;
        yield return RenderFor(cam, 0.5f);

        float hands = HandSpread(anim);
        Assert.Less(hands, 1.1f, "mains rapprochées (T-pose ≈ 1,6 m) : " + hands.ToString("0.00"));
        Shot("sandbox_play_idle.png");

        health.GetComponent<EnemyAnimation>().SetSpeedOverride(1f);
        yield return RenderFor(cam, 1f);
        Assert.Greater(anim.GetFloat(EnemyAnimation.VitesseHash), 0.9f);
        Shot("sandbox_play_run.png");

        health.TakeDamage(health.CurrentHealth);
        yield return RenderFor(cam, 2.6f);
        Shot("sandbox_play_death.png");
    }

    [UnityTest]
    public IEnumerator Sandbox_IA_Patrouille_Poursuite_Attaque()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        Camera cam = Camera.main;
        EnemyAI ai = GameObject.Find("Enemy").GetComponent<EnemyAI>(); // (un Boss est aussi dans la scène)
        GameObject dummy = GameObject.Find("DummyPlayer");
        Assert.IsNotNull(ai, "EnemyAI dans la scène");
        Assert.IsNotNull(dummy, "DummyPlayer dans la scène");
        Assert.AreEqual("Player", dummy.tag);
        Component dummyHealth = dummy.GetComponent("TargetDummyHealth");
        Assert.IsNotNull(dummyHealth, "TargetDummyHealth (pas de script manquant)");
        System.Func<int> dummyHp = () => (int)dummyHealth.GetType().GetProperty("CurrentHealth").GetValue(dummyHealth);

        // 1) Patrouille : le DummyPlayer est caché derrière le mur.
        yield return RenderFor(cam, 4f);
        Assert.AreEqual(EnemyState.Patrol, ai.State, "caché derrière le mur : patrouille");
        Assert.AreEqual(dummy.transform, ai.Target, "cible trouvée par le tag Player");
        Shot("ai_1_patrol.png");

        // 2) Poursuite : le DummyPlayer sort de derrière le mur.
        Rigidbody body = dummy.GetComponent<Rigidbody>();
        body.position = new Vector3(-9f, 1f, 9f);
        yield return RenderFor(cam, 1.2f);
        Assert.AreEqual(EnemyState.Chase, ai.State, "vu : poursuite");
        Shot("ai_2_chase.png");

        // 3) Attaque : on attend que l'ennemi arrive au contact.
        int hpBefore = dummyHp();
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[DummyPlayer\] -10 PV"));
        float end = Time.time + 8f;
        while (dummyHp() == hpBefore && Time.time < end)
        {
            yield return RenderFor(cam, 0.05f);
        }
        Assert.AreEqual(EnemyState.Attack, ai.State, "au contact : attaque");
        Assert.AreEqual(hpBefore - ai.AttackDamage, dummyHp(), "le DummyPlayer a perdu des PV");
        yield return RenderFor(cam, 0.3f);
        Shot("ai_3_attack.png");
    }

    private static IEnumerator RenderFor(Camera cam, float seconds)
    {
        // Toujours rendre vers une texture : en batch il n'y a pas d'écran (rendre "à l'écran" fait planter Unity).
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

    private static float HandSpread(Animator anim)
    {
        Transform l = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform r = anim.GetBoneTransform(HumanBodyBones.RightHand);
        return Vector3.Distance(l.position, r.position);
    }

    private static void Shot(string file)
    {
        string dir = System.Environment.GetEnvironmentVariable("SNAP_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Camera cam = Camera.main;
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
