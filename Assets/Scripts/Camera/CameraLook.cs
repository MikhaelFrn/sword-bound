using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Caméra FPS indépendante : à placer sur la racine du prefab PlayerCameraRig,
/// enfant du Player. Aucune dépendance vers les scripts du joueur.
///
/// - Pitch (haut/bas) : rotation locale en X de ce rig, limitée par minPitch/maxPitch.
/// - Yaw (gauche/droite) : appliqué au parent autour de l'axe Y du monde (rotateParentYaw),
///   ou au rig lui-même s'il n'a pas de parent.
/// - Curseur : verrouillé/caché tant que LookEnabled est vrai et que Time.timeScale &gt; 0.
/// </summary>
public class CameraLook : MonoBehaviour
{
    // Après un verrouillage, le delta de souris est parfois énorme (le curseur est recentré
    // par le système) et peut n'arriver qu'à la frame suivante : on ignore donc 2 frames.
    private const int FramesToSkipAfterLock = 2;

    [Header("Souris (degrés par pixel)")]
    [SerializeField, Min(0f)] private float sensitivityX = 0.1f;
    [SerializeField, Min(0f)] private float sensitivityY = 0.1f;
    [Tooltip("Inverse l'axe vertical (souris vers le haut = regarder vers le bas).")]
    [SerializeField] private bool invertY = false;

    [Header("Limites verticales (degrés)")]
    [SerializeField, Range(-89f, 0f)] private float minPitch = -85f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 85f;

    [Header("Options")]
    [Tooltip("Si vrai, la rotation horizontale (yaw) est appliquée au parent (le Player). " +
             "Sans parent, elle est appliquée à ce rig.")]
    [SerializeField] private bool rotateParentYaw = true;

    [Tooltip("Si vrai, désactive au démarrage les autres caméras taguées MainCamera " +
             "et les autres AudioListener qui ne font pas partie de ce rig.")]
    [SerializeField] private bool disableOtherMainCameras = true;

    private float pitch;
    private float selfYaw;
    private bool cursorLockedByUs;
    private int framesToSkip;

    /// <summary>Permet à d'autres systèmes (pause, menu, cinématique) de suspendre la rotation.</summary>
    public bool LookEnabled { get; set; } = true;

    /// <summary>Angle vertical courant en degrés (négatif = regarder vers le haut).</summary>
    public float Pitch => pitch;

    public float SensitivityX => sensitivityX;
    public float SensitivityY => sensitivityY;
    public bool InvertY => invertY;
    public float MinPitch => minPitch;
    public float MaxPitch => maxPitch;
    public bool RotateParentYaw => rotateParentYaw;
    public bool DisableOtherMainCameras => disableOtherMainCameras;

    /// <summary>
    /// Replace la caméra (respawn, téléportation). Le yaw n'est appliqué que si ce rig
    /// pilote une rotation horizontale (parent avec rotateParentYaw, ou rig sans parent).
    /// </summary>
    public void ResetLook(float yaw, float pitch)
    {
        this.pitch = ClampPitch(pitch);

        Transform parent = transform.parent;
        if (parent == null)
        {
            selfYaw = yaw;
        }
        else if (rotateParentYaw)
        {
            parent.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        framesToSkip = FramesToSkipAfterLock;
        ApplyPitch();
    }

    private void Awake()
    {
        float localPitch = transform.localEulerAngles.x;
        if (localPitch > 180f)
        {
            localPitch -= 360f;
        }
        pitch = ClampPitch(localPitch);
        selfYaw = transform.eulerAngles.y;
        ApplyPitch();

        if (disableOtherMainCameras)
        {
            DisableOtherCamerasAndListeners();
        }
    }

    private void OnDisable()
    {
        if (cursorLockedByUs)
        {
            UnlockCursor();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Le système libère le curseur quand l'application perd le focus : on le reprend au retour.
        if (hasFocus && cursorLockedByUs && IsLookActive())
        {
            LockCursor();
        }
    }

    private void Update()
    {
        bool active = IsLookActive();
        UpdateCursor(active);

        if (!active)
        {
            return;
        }

        if (framesToSkip > 0)
        {
            framesToSkip--;
            return;
        }

        // Pas de rotation tant que le curseur n'est pas verrouillé (focus perdu, Échap dans l'éditeur).
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        // Le delta est déjà en pixels par frame : surtout pas de Time.deltaTime.
        Vector2 delta = ReadMouseDelta();
        if (delta.x == 0f && delta.y == 0f)
        {
            return;
        }

        float yawDelta = delta.x * sensitivityX;
        float pitchDelta = delta.y * sensitivityY * (invertY ? 1f : -1f);

        pitch = ClampPitch(pitch + pitchDelta);
        ApplyYaw(yawDelta);
        ApplyPitch();
    }

    // ------------------------------------------------------------- Rotation

    private float ClampPitch(float value)
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);
        return Mathf.Clamp(value, low, high);
    }

    private void ApplyYaw(float yawDelta)
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            selfYaw += yawDelta;
        }
        else if (rotateParentYaw)
        {
            parent.Rotate(0f, yawDelta, 0f, Space.World);
        }
    }

    private void ApplyPitch()
    {
        // Pitch stocké comme un angle (pas relu depuis la rotation) : aucune dérive.
        // Rotation construite par Euler : pas de blocage de cardan dans [-89, 89].
        float yaw = transform.parent == null ? selfYaw : 0f;
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // --------------------------------------------------------------- Curseur

    private bool IsLookActive()
    {
        return LookEnabled && Time.timeScale > 0f;
    }

    private void UpdateCursor(bool wantLocked)
    {
        if (wantLocked)
        {
            // On ne touche au curseur que si l'état change, ou si le joueur reclique
            // après que le système l'a libéré (Échap dans l'éditeur par exemple).
            if (!cursorLockedByUs || (Cursor.lockState != CursorLockMode.Locked && ReadClickThisFrame()))
            {
                LockCursor();
            }
        }
        else if (cursorLockedByUs)
        {
            UnlockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLockedByUs = true;
        framesToSkip = FramesToSkipAfterLock;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLockedByUs = false;
    }

    // ----------------------------------------------------------------- Entrées

    private static Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    private static bool ReadClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // ------------------------------------------------------ Garde-fou caméras

    private void DisableOtherCamerasAndListeners()
    {
        Camera ownCamera = GetComponentInChildren<Camera>(true);
        AudioListener ownListener = GetComponentInChildren<AudioListener>(true);
        List<string> disabled = new List<string>();

        if (ownCamera != null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera other in cameras)
            {
                if (other.enabled && other.CompareTag("MainCamera") && !other.transform.IsChildOf(transform))
                {
                    other.enabled = false;
                    disabled.Add("Camera " + GetPath(other.transform));
                }
            }
        }

        if (ownListener != null)
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (AudioListener other in listeners)
            {
                if (other.enabled && !other.transform.IsChildOf(transform))
                {
                    other.enabled = false;
                    disabled.Add("AudioListener " + GetPath(other.transform));
                }
            }
        }

        if (disabled.Count > 0)
        {
            Debug.LogWarning("[CameraLook] Composants désactivés pour éviter les doublons : " +
                             string.Join(", ", disabled), this);
        }
    }

    private static string GetPath(Transform target)
    {
        StringBuilder path = new StringBuilder(target.name);
        for (Transform t = target.parent; t != null; t = t.parent)
        {
            path.Insert(0, t.name + "/");
        }
        return path.ToString();
    }
}
