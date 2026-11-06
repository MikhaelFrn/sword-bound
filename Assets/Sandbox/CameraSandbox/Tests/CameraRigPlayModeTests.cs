#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Tests temporaires de la caméra FPS (PlayMode). Les scripts de la caméra sont dans
/// Assembly-CSharp, qu'un asmdef ne peut pas référencer : on y accède donc par réflexion,
/// ce qui prouve aussi que le prefab n'a aucune dépendance de compilation vers d'autres scripts.
/// </summary>
public class CameraRigPlayModeTests : InputTestFixture
{
    private const string PrefabPath = "Assets/Prefabs/Camera/PlayerCameraRig.prefab";

    private Mouse mouse;
    private GameObject prefab;
    private readonly List<GameObject> created = new List<GameObject>();
    private int warningCount;
    private readonly List<string> warnings = new List<string>();

    [SetUp]
    public void SetUpScene()
    {
        mouse = InputSystem.AddDevice<Mouse>();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "Prefab introuvable : " + PrefabPath);

        Time.timeScale = 1f;
        warningCount = 0;
        warnings.Clear();
        Application.logMessageReceived += OnLog;
    }

    [TearDown]
    public void TearDownScene()
    {
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        foreach (GameObject go in created)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
        created.Clear();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warningCount++;
            warnings.Add(condition);
        }
    }

    // ----------------------------------------------------------------- Helpers

    private GameObject MakePlayer(bool withRigidbody, bool gravity = false)
    {
        GameObject player = new GameObject("TestPlayer");
        created.Add(player);
        if (withRigidbody)
        {
            Rigidbody body = player.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = gravity;
            body.linearDamping = 0f;
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.5f;
        }
        return player;
    }

    private GameObject AddRig(Transform parent)
    {
        GameObject rig = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
        rig.name = "PlayerCameraRig";
        if (parent == null)
        {
            created.Add(rig);
        }
        return rig;
    }

    private static Component GetLook(GameObject root)
    {
        Component look = root.GetComponentInChildren(CameraTypes.Look, true);
        Assert.IsNotNull(look, "CameraLook introuvable sur le prefab.");
        return look;
    }

    private IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    /// <summary>Injecte le même delta de souris pendant N frames.</summary>
    private IEnumerator Drive(Vector2 delta, int frames, Action onEachFrame = null)
    {
        for (int i = 0; i < frames; i++)
        {
            Set(mouse.delta, delta);
            yield return null;
            onEachFrame?.Invoke();
        }
        // Laisse le delta retomber à zéro.
        yield return null;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : (angle < -180f ? angle + 360f : angle);
    }

    // ------------------------------------------------------------ Intégration

    [UnityTest]
    public IEnumerator Integration_DisablesDefaultCameraAndListener_WithSingleWarning()
    {
        GameObject defaultCamera = new GameObject("Main Camera");
        created.Add(defaultCamera);
        defaultCamera.tag = "MainCamera";
        Camera defaultCam = defaultCamera.AddComponent<Camera>();
        AudioListener defaultListener = defaultCamera.AddComponent<AudioListener>();

        GameObject player = MakePlayer(true);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[CameraLook\].*Main Camera"));
        AddRig(player.transform);

        yield return Frames(5);

        Assert.IsFalse(defaultCam.enabled, "La Main Camera par défaut doit être désactivée.");
        Assert.IsFalse(defaultListener.enabled, "L'AudioListener par défaut doit être désactivé.");
        Assert.IsTrue(defaultCamera.activeSelf, "Seul le composant doit être désactivé, pas le GameObject.");
        Assert.AreEqual("MainCamera", defaultCamera.tag, "Le tag ne doit pas changer.");

        int activeListeners = 0;
        foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (listener.enabled && listener.gameObject.activeInHierarchy) activeListeners++;
        }
        Assert.AreEqual(1, activeListeners, "Un seul AudioListener actif attendu.");

        Camera rigCamera = player.GetComponentInChildren<Camera>();
        Assert.IsNotNull(rigCamera);
        Assert.IsTrue(rigCamera.enabled);
        Assert.AreSame(rigCamera, Camera.main, "Camera.main doit être la caméra du rig.");

        Assert.AreEqual(1, warningCount, "Un seul avertissement attendu. Reçus : " + string.Join(" | ", warnings));
        LogAssert.NoUnexpectedReceived();
    }

    // ---------------------------------------------------------------- Rotation

    [UnityTest]
    public IEnumerator Look_YawRotatesParent_PitchIsClamped_ParentNeverTilts()
    {
        GameObject player = MakePlayer(true);
        GameObject rig = AddRig(player.transform);
        Component look = GetLook(rig);
        yield return Frames(5);

        Assert.IsTrue(CameraTypes.CursorLocked(look), "Le curseur doit être verrouillé par CameraLook.");

        float maxTilt = 0f;
        Action checkTilt = () =>
        {
            maxTilt = Mathf.Max(maxTilt, Vector3.Angle(player.transform.up, Vector3.up));
        };

        float yawBefore = player.transform.eulerAngles.y;
        yield return Drive(new Vector2(100f, 0f), 5, checkTilt);
        float yawDelta = Mathf.DeltaAngle(yawBefore, player.transform.eulerAngles.y);
        Assert.Greater(yawDelta, 20f, "Le parent doit tourner vers la droite (yaw " + yawDelta + ").");
        Assert.Less(yawDelta, 80f, "Rotation excessive : " + yawDelta);

        // Souris vers le bas : le pitch augmente jusqu'à la limite sans la dépasser.
        float maxPitch = float.MinValue;
        yield return Drive(new Vector2(30f, -100f), 30, () =>
        {
            checkTilt();
            maxPitch = Mathf.Max(maxPitch, CameraTypes.Pitch(look));
        });
        Assert.LessOrEqual(maxPitch, 85.0001f, "Le pitch dépasse la limite haute.");
        Assert.AreEqual(85f, CameraTypes.Pitch(look), 0.01f, "Le pitch doit être collé à la limite.");
        Assert.AreEqual(85f, NormalizeAngle(rig.transform.localEulerAngles.x), 0.05f, "La rotation locale doit suivre le pitch.");

        // Souris vers le haut.
        float minPitch = float.MaxValue;
        yield return Drive(new Vector2(-30f, 100f), 60, () =>
        {
            checkTilt();
            minPitch = Mathf.Min(minPitch, CameraTypes.Pitch(look));
        });
        Assert.GreaterOrEqual(minPitch, -85.0001f, "Le pitch dépasse la limite basse.");
        Assert.AreEqual(-85f, CameraTypes.Pitch(look), 0.01f);

        Assert.Less(maxTilt, 0.01f, "Le parent ne doit jamais pencher en X ou Z (" + maxTilt + "°).");
        Assert.AreEqual(0f, NormalizeAngle(rig.transform.localEulerAngles.z), 0.01f, "Pas de roulis sur le rig.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Look_NoRotation_WhenPausedOrDisabled_ThenResumes()
    {
        GameObject player = MakePlayer(true);
        GameObject rig = AddRig(player.transform);
        Component look = GetLook(rig);
        yield return Frames(5);

        // Pause (Time.timeScale = 0)
        Time.timeScale = 0f;
        yield return Frames(2);
        float yaw = player.transform.eulerAngles.y;
        float pitch = CameraTypes.Pitch(look);
        yield return Drive(new Vector2(200f, 100f), 5);
        Assert.AreEqual(yaw, player.transform.eulerAngles.y, 0.0001f, "Aucune rotation pendant la pause (yaw).");
        Assert.AreEqual(pitch, CameraTypes.Pitch(look), 0.0001f, "Aucune rotation pendant la pause (pitch).");
        Assert.IsFalse(CameraTypes.CursorLocked(look), "Le curseur doit être libéré en pause.");
        Assert.IsTrue(Cursor.visible);

        // Reprise
        Time.timeScale = 1f;
        yield return Frames(5);
        Assert.IsTrue(CameraTypes.CursorLocked(look), "Le curseur doit être reverrouillé à la reprise.");

        // LookEnabled = false
        CameraTypes.SetLookEnabled(look, false);
        yield return Frames(2);
        yaw = player.transform.eulerAngles.y;
        pitch = CameraTypes.Pitch(look);
        yield return Drive(new Vector2(200f, 100f), 5);
        Assert.AreEqual(yaw, player.transform.eulerAngles.y, 0.0001f, "Aucune rotation avec LookEnabled = false (yaw).");
        Assert.AreEqual(pitch, CameraTypes.Pitch(look), 0.0001f, "Aucune rotation avec LookEnabled = false (pitch).");
        Assert.IsFalse(CameraTypes.CursorLocked(look), "Le curseur doit être libéré avec LookEnabled = false.");

        // Réactivation : ça retourne.
        CameraTypes.SetLookEnabled(look, true);
        yield return Frames(5);
        yield return Drive(new Vector2(100f, 0f), 3);
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)), 10f, "La rotation doit reprendre.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Look_ResetLook_SetsYawAndPitch()
    {
        GameObject player = MakePlayer(true);
        GameObject rig = AddRig(player.transform);
        Component look = GetLook(rig);
        yield return Frames(5);

        CameraTypes.ResetLook(look, 90f, 30f);
        yield return Frames(1);
        Assert.AreEqual(90f, player.transform.eulerAngles.y, 0.01f);
        Assert.AreEqual(30f, CameraTypes.Pitch(look), 0.01f);

        CameraTypes.ResetLook(look, 0f, 500f);
        yield return Frames(1);
        Assert.AreEqual(85f, CameraTypes.Pitch(look), 0.01f, "ResetLook doit respecter la limite.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Look_NoParent_RotatesRigItself_WithoutException()
    {
        GameObject rig = AddRig(null);
        Component look = GetLook(rig);
        yield return Frames(5);

        float yaw = rig.transform.eulerAngles.y;
        yield return Drive(new Vector2(100f, -20f), 5);
        Assert.Greater(Mathf.DeltaAngle(yaw, rig.transform.eulerAngles.y), 20f, "Sans parent, le rig tourne lui-même.");
        Assert.Greater(CameraTypes.Pitch(look), 1f);
        Assert.Less(Vector3.Angle(rig.transform.up, Vector3.up), 90f);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Look_ParentWithoutRigidbody_Works()
    {
        GameObject player = MakePlayer(false);
        GameObject rig = AddRig(player.transform);
        Component look = GetLook(rig);
        yield return Frames(5);

        float yaw = player.transform.eulerAngles.y;
        yield return Drive(new Vector2(100f, 20f), 5);
        Assert.Greater(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y), 20f);
        Assert.Less(Vector3.Angle(player.transform.up, Vector3.up), 0.01f);
        Assert.Less(CameraTypes.Pitch(look), -1f);
        LogAssert.NoUnexpectedReceived();
    }

    // ----------------------------------------------------------------- Effets

    [UnityTest]
    public IEnumerator Effects_SprintFovAndHeadBob_RiseAndFall_WithRigidbody()
    {
        GameObject player = MakePlayer(true);
        Rigidbody body = player.GetComponent<Rigidbody>();
        GameObject rig = AddRig(player.transform);
        Camera cam = rig.GetComponentInChildren<Camera>();
        Transform camTransform = cam.transform;
        yield return Frames(5);

        float baseFov = cam.fieldOfView;
        Assert.AreEqual(70f, baseFov, 0.01f);

        // Marche lente : pas de sprint.
        body.linearVelocity = new Vector3(0f, 0f, 3f);
        yield return new WaitForSeconds(0.6f);
        Assert.Less(cam.fieldOfView, baseFov + 0.5f, "Pas de FOV de sprint en dessous du seuil.");

        // Sprint.
        body.linearVelocity = new Vector3(0f, 0f, 8f);
        float minY = float.MaxValue, maxY = float.MinValue;
        float end = Time.time + 1.2f;
        while (Time.time < end)
        {
            minY = Mathf.Min(minY, camTransform.localPosition.y);
            maxY = Mathf.Max(maxY, camTransform.localPosition.y);
            yield return null;
        }
        Assert.Greater(cam.fieldOfView, baseFov + 7f, "Le FOV doit monter vers 78 au sprint (" + cam.fieldOfView + ").");
        Assert.LessOrEqual(cam.fieldOfView, baseFov + 8.05f);
        Assert.Greater(maxY - minY, 0.005f, "Le head bob doit bouger la caméra pendant la marche.");
        Assert.Less(maxY - minY, 0.2f, "Le head bob doit rester subtil.");

        // Arrêt.
        body.linearVelocity = Vector3.zero;
        yield return new WaitForSeconds(1.2f);
        Assert.Less(cam.fieldOfView, baseFov + 0.3f, "Le FOV doit revenir à la base (" + cam.fieldOfView + ").");
        Assert.Less(camTransform.localPosition.magnitude, 0.002f, "La caméra doit revenir à zéro à l'arrêt.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Effects_WorkWithoutRigidbody_UsingPositionDelta()
    {
        GameObject player = MakePlayer(false);
        GameObject rig = AddRig(player.transform);
        Camera cam = rig.GetComponentInChildren<Camera>();
        yield return Frames(5);
        float baseFov = cam.fieldOfView;

        float end = Time.time + 1.2f;
        while (Time.time < end)
        {
            player.transform.position += Vector3.forward * (8f * Time.deltaTime);
            yield return null;
        }
        Assert.Greater(cam.fieldOfView, baseFov + 7f, "Sans Rigidbody, la vitesse vient de la position (" + cam.fieldOfView + ").");

        yield return new WaitForSeconds(1.2f);
        Assert.Less(cam.fieldOfView, baseFov + 0.3f);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Effects_DoNothing_WhenPaused()
    {
        GameObject player = MakePlayer(true);
        Rigidbody body = player.GetComponent<Rigidbody>();
        GameObject rig = AddRig(player.transform);
        Camera cam = rig.GetComponentInChildren<Camera>();
        Transform camTransform = cam.transform;
        yield return Frames(5);

        float baseFov = cam.fieldOfView;
        body.linearVelocity = new Vector3(0f, 0f, 8f);

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(baseFov, cam.fieldOfView, 0.0001f, "Le FOV ne doit pas bouger en pause.");
        Assert.AreEqual(0f, camTransform.localPosition.magnitude, 0.0001f, "Le head bob ne doit pas bouger en pause.");

        // Reprise : les effets fonctionnent de nouveau.
        Time.timeScale = 1f;
        body.linearVelocity = new Vector3(0f, 0f, 8f);
        yield return new WaitForSeconds(1.2f);
        Assert.Greater(cam.fieldOfView, baseFov + 7f);
        LogAssert.NoUnexpectedReceived();
    }
}

/// <summary>Accès par réflexion aux scripts de la caméra (Assembly-CSharp).</summary>
internal static class CameraTypes
{
    public static readonly Type Look = Find("CameraLook");

    private static Type Find(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, "Type introuvable : " + name);
        return type;
    }

    public static float Pitch(Component look)
    {
        return (float)Look.GetProperty("Pitch").GetValue(look);
    }

    public static bool CursorLocked(Component look)
    {
        return (bool)Look.GetProperty("CursorLocked").GetValue(look);
    }

    public static void SetLookEnabled(Component look, bool value)
    {
        Look.GetProperty("LookEnabled").SetValue(look, value);
    }

    public static void ResetLook(Component look, float yaw, float pitch)
    {
        Look.GetMethod("ResetLook", BindingFlags.Public | BindingFlags.Instance).Invoke(look, new object[] { yaw, pitch });
    }
}

#endif
