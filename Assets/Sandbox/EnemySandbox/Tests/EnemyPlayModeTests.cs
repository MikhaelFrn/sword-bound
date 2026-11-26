#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SwordBound.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// TEMPORAIRE : tests PlayMode de l'étape 2 des ennemis.
public class EnemyPlayModeTests
{
    private const string PrefabPath = "Assets/Enemies/Prefabs/Enemy.prefab";
    private GameObject ground;
    private GameObject enemy;

    private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

    [SetUp]
    public void SetUp()
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        enemy = Object.Instantiate(Prefab, Vector3.zero, Quaternion.identity);
    }

    [TearDown]
    public void TearDown()
    {
        if (enemy != null) Object.Destroy(enemy);
        Object.Destroy(ground);
    }

    private void RemoveAI()
    {
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null) Object.DestroyImmediate(ai);
    }

    private EnemyHealth Health => enemy.GetComponent<EnemyHealth>();
    private EnemyAnimation Anim => enemy.GetComponent<EnemyAnimation>();
    private Animator Animator => enemy.GetComponent<Animator>();

    private string DominantClip()
    {
        var infos = Animator.GetCurrentAnimatorClipInfo(0);
        return infos.Length == 0 ? "" : infos.OrderByDescending(i => i.weight).First().clip.name;
    }

    private bool InOrEntering(string state)
    {
        return Animator.GetCurrentAnimatorStateInfo(0).IsName(state) ||
               (Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsName(state));
    }

    // ------------------------------------------------------------- Santé

    [UnityTest]
    public IEnumerator Degats_RetirentDesPV()
    {
        yield return null;
        int changes = 0;
        Health.OnHealthChanged += (c, m) => changes++;
        int before = Health.CurrentHealth;
        Health.TakeDamage(10);
        Assert.AreEqual(before - 10, Health.CurrentHealth);
        Assert.AreEqual(1, changes);
    }

    [UnityTest]
    public IEnumerator ValeursNulles_OuNegatives_Ignorees()
    {
        yield return null;
        int before = Health.CurrentHealth;
        int changes = 0;
        Health.OnHealthChanged += (c, m) => changes++;
        Health.TakeDamage(0);
        Health.TakeDamage(-5);
        Assert.AreEqual(before, Health.CurrentHealth);
        Assert.AreEqual(0, changes);
    }

    [UnityTest]
    public IEnumerator Mort_UneSeuleFois_EtPlusDeDegatsApres()
    {
        yield return null;
        int died = 0, anyDied = 0, changes = 0;
        Health.OnDied += h => died++;
        System.Action<EnemyHealth> any = h => { if (h == Health) anyDied++; };
        EnemyHealth.AnyEnemyDied += any;
        try
        {
            Health.TakeDamage(Health.MaxHealth + 50);
            Assert.IsTrue(Health.IsDead);
            Assert.AreEqual(0, Health.CurrentHealth);
            Health.OnHealthChanged += (c, m) => changes++;
            Health.TakeDamage(10);
            Health.TakeDamage(1000);
            yield return null;
            Assert.AreEqual(1, died, "OnDied");
            Assert.AreEqual(1, anyDied, "AnyEnemyDied");
            Assert.AreEqual(0, Health.CurrentHealth);
            Assert.AreEqual(0, changes, "aucun changement de PV après la mort");
        }
        finally
        {
            EnemyHealth.AnyEnemyDied -= any;
        }
    }

    [UnityTest]
    public IEnumerator Mort_DesactiveCollidersEtFigeLeCorps()
    {
        yield return null;
        Health.TakeDamage(Health.MaxHealth);
        Assert.IsTrue(enemy.GetComponentsInChildren<Collider>().All(c => !c.enabled));
        Assert.IsTrue(enemy.GetComponent<Rigidbody>().isKinematic);
        Vector3 pos = enemy.transform.position;
        yield return new WaitForSeconds(0.5f);
        Assert.Less(Vector3.Distance(pos, enemy.transform.position), 0.01f, "le corps ne tombe pas à travers le sol");
    }

    [UnityTest]
    public IEnumerator ScaleMaxHealth_EtSetMaxHealth()
    {
        yield return null;
        Health.SetMaxHealth(100);
        Health.TakeDamage(30);
        Health.ScaleMaxHealth(1.5f);
        Assert.AreEqual(150, Health.MaxHealth);
        Assert.AreEqual(150, Health.CurrentHealth);
        Health.SetMaxHealth(80);
        Assert.AreEqual(80, Health.MaxHealth);
        Assert.AreEqual(80, Health.CurrentHealth);
        Health.ScaleMaxHealth(0f);
        Health.ScaleMaxHealth(-2f);
        Assert.AreEqual(80, Health.MaxHealth, "multiplicateurs <= 0 ignorés");
    }

    [UnityTest]
    public IEnumerator EnnemiMort_DetruitApresLeDelai()
    {
        yield return null;
        float delay = Health.EffectiveDestroyDelay;
        Assert.GreaterOrEqual(delay, 4f);
        Assert.GreaterOrEqual(delay, Anim.DeathClipLength, "le délai couvre l'animation de mort");
        Health.TakeDamage(Health.MaxHealth);
        yield return new WaitForSeconds(delay - 0.5f);
        Assert.IsTrue(enemy != null, "encore là juste avant le délai");
        yield return new WaitForSeconds(1f);
        Assert.IsTrue(enemy == null, "détruit après le délai");
    }

    // ------------------------------------------------------------ Animator

    [UnityTest]
    public IEnumerator Vitesse_IdleWalkRun()
    {
        Anim.enabled = false; // on pilote le paramètre directement
        float[] values = { 0f, 0.5f, 1f };
        string[] expected = { "Idle", "Walk", "Run" };
        for (int i = 0; i < values.Length; i++)
        {
            Animator.SetFloat(EnemyAnimation.VitesseHash, values[i]);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.AreEqual(expected[i], DominantClip(), "Vitesse " + values[i]);
        }
    }

    [UnityTest]
    public IEnumerator TriggerAttaque_VaDansAttack_PuisRevientALocomotion()
    {
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(Anim.PlayAttack());
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(InOrEntering("Attack"), "état Attack");
        Assert.IsFalse(Anim.PlayAttack(), "pas de relance pendant l'attaque");
        yield return new WaitForSeconds(2.2f);
        Assert.IsTrue(Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "retour à Locomotion");
    }

    [UnityTest]
    public IEnumerator TriggerDegats_VaDansHit_AvecDelaiMinimum()
    {
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(Anim.PlayHit());
        Assert.IsFalse(Anim.PlayHit(), "délai minimum entre deux dégâts");
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(InOrEntering("Hit"), "état Hit");
        yield return new WaitForSeconds(1.5f);
        Assert.IsTrue(Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "retour à Locomotion");
        Assert.IsTrue(Anim.PlayHit(), "rejouable après le délai");
    }

    [UnityTest]
    public IEnumerator DegatsViaEnemyHealth_JouentHit()
    {
        yield return new WaitForSeconds(0.2f);
        Health.TakeDamage(10);
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(InOrEntering("Hit"));
    }

    [UnityTest]
    public IEnumerator TriggerMort_VaDansDeath_EtYReste()
    {
        yield return new WaitForSeconds(0.2f);
        Health.TakeDamage(Health.MaxHealth);
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(InOrEntering("Death"), "état Death");
        Assert.IsFalse(Anim.PlayAttack(), "un mort n'attaque plus");
        Assert.IsFalse(Anim.PlayHit(), "un mort ne joue plus Hit");
        yield return new WaitForSeconds(3f);
        AnimatorStateInfo info = Animator.GetCurrentAnimatorStateInfo(0);
        Assert.IsTrue(info.IsName("Death"), "toujours dans Death");
        Assert.IsFalse(Animator.IsInTransition(0));
        Assert.Greater(info.normalizedTime, 1f, "animation terminée, sans relance");
    }

    [UnityTest]
    public IEnumerator EnemyAnimation_LitLaVitesseDuRigidbody()
    {
        RemoveAI(); // on teste EnemyAnimation seul : l'IA remettrait la vitesse à zéro
        Rigidbody body = enemy.GetComponent<Rigidbody>();
        for (float t = 0; t < 1f; t += Time.fixedDeltaTime)
        {
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, Anim.VitesseCourse);
            yield return new WaitForFixedUpdate();
        }
        Assert.Greater(Animator.GetFloat(EnemyAnimation.VitesseHash), 0.9f, "course");
        body.linearVelocity = Vector3.zero;
        yield return new WaitForSeconds(1f);
        Assert.Less(Animator.GetFloat(EnemyAnimation.VitesseHash), 0.05f, "arrêt");
    }

    [UnityTest]
    public IEnumerator EnemyAnimation_SansRigidbody_LitLeDeplacement()
    {
        RemoveAI(); // l'IA exige un Rigidbody (RequireComponent)
        Object.DestroyImmediate(enemy.GetComponent<Rigidbody>());
        yield return null;
        float walk = Anim.VitesseCourse * 0.5f;
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            enemy.transform.position += Vector3.forward * walk * Time.deltaTime;
            yield return null;
        }
        Assert.AreEqual(0.5f, Animator.GetFloat(EnemyAnimation.VitesseHash), 0.08f, "marche");
    }

    // -------------------------------------------------------------- Prefab

    [Test]
    public void Prefab_SansReferenceManquante()
    {
        GameObject prefab = Prefab;
        Assert.IsNotNull(prefab);
        foreach (Component c in prefab.GetComponentsInChildren<Component>(true))
        {
            Assert.IsNotNull(c, "script manquant dans le prefab");
            SerializedProperty p = new SerializedObject(c).GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType == SerializedPropertyType.ObjectReference &&
                    p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                {
                    Assert.Fail("Référence manquante : " + c.GetType().Name + "." + p.propertyPath + " sur " + c.name);
                }
            }
        }

        Animator anim = prefab.GetComponent<Animator>();
        Assert.IsNotNull(anim.runtimeAnimatorController, "controller assigné");
        Assert.AreEqual("Enemy_Controller", anim.runtimeAnimatorController.name);
        Assert.IsTrue(anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman, "avatar Humanoid valide");
        Assert.IsFalse(anim.applyRootMotion, "Apply Root Motion désactivé");
        Assert.IsNotNull(prefab.GetComponent<EnemyHealth>());
        Assert.IsNotNull(prefab.GetComponent<EnemyAnimation>());
        CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
        Assert.AreEqual(1.8f, capsule.height * prefab.transform.lossyScale.y, 0.05f, "capsule de 1,8 m");
        Rigidbody body = prefab.GetComponent<Rigidbody>();
        Assert.AreEqual(RigidbodyConstraints.FreezeRotation, body.constraints);
        Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
    }
}
#endif
