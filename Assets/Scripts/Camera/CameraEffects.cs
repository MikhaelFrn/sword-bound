using UnityEngine;

/// <summary>
/// Effets de caméra subtils : FOV de sprint et balancement de marche (head bob).
/// À placer sur l'objet Camera du prefab PlayerCameraRig.
///
/// Indépendant du contrôleur du joueur : la vitesse horizontale vient du Rigidbody trouvé
/// via GetComponentInParent, ou, à défaut (ou si le Rigidbody est cinématique), du déplacement
/// de la position du rig.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraEffects : MonoBehaviour
{
    // Au-delà de cette vitesse (m/s) entre deux frames, on suppose une téléportation (respawn) et on l'ignore.
    private const float MaxPlausibleSpeed = 100f;

    [Header("FOV de sprint")]
    [SerializeField] private bool enableSprintFov = true;
    [Tooltip("Vitesse horizontale (m/s) au-dessus de laquelle le FOV de sprint s'active.")]
    [SerializeField, Min(0f)] private float sprintSpeedThreshold = 5.5f;
    [Tooltip("Degrés ajoutés au FOV de base pendant le sprint (70 -> 78 par défaut).")]
    [SerializeField, Min(0f)] private float sprintFovIncrease = 8f;
    [Tooltip("Durée approximative de la transition, en secondes.")]
    [SerializeField, Min(0.01f)] private float fovSmoothTime = 0.15f;

    [Header("Balancement de marche (head bob)")]
    [SerializeField] private bool enableHeadBob = true;
    [Tooltip("Vitesse horizontale minimale (m/s) pour déclencher le balancement.")]
    [SerializeField, Min(0f)] private float bobMinSpeed = 0.5f;
    [Tooltip("Amplitude verticale en mètres.")]
    [SerializeField, Min(0f)] private float bobVerticalAmplitude = 0.03f;
    [Tooltip("Amplitude horizontale en mètres.")]
    [SerializeField, Min(0f)] private float bobHorizontalAmplitude = 0.015f;
    [Tooltip("Cycles de balancement par mètre parcouru (0.4 = un cycle tous les 2,5 m).")]
    [SerializeField, Min(0f)] private float bobCyclesPerMeter = 0.4f;
    [Tooltip("Durée approximative du retour à zéro (et du démarrage), en secondes.")]
    [SerializeField, Min(0.01f)] private float bobBlendTime = 0.12f;

    private Camera cam;
    private Rigidbody body;
    private Transform rig;
    private Vector3 lastRigPosition;
    private Vector3 baseLocalPosition;
    private float baseFov;
    private float fovVelocity;
    private float bobWeight;
    private float bobWeightVelocity;
    private float bobPhase;
    private float horizontalSpeed;

    /// <summary>Vitesse horizontale mesurée à la dernière frame (m/s).</summary>
    public float HorizontalSpeed => horizontalSpeed;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        body = GetComponentInParent<Rigidbody>();
        rig = transform.parent != null ? transform.parent : transform;

        baseFov = cam.fieldOfView;
        baseLocalPosition = transform.localPosition;
        lastRigPosition = rig.position;
    }

    private void OnEnable()
    {
        if (rig != null)
        {
            lastRigPosition = rig.position;
        }
    }

    private void OnDisable()
    {
        // Remet la caméra dans son état de base.
        if (cam != null)
        {
            cam.fieldOfView = baseFov;
        }
        transform.localPosition = baseLocalPosition;
        bobWeight = 0f;
        bobWeightVelocity = 0f;
        fovVelocity = 0f;
    }

    private void LateUpdate()
    {
        // En pause : rien ne bouge.
        if (Time.timeScale <= 0f)
        {
            lastRigPosition = rig.position;
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        horizontalSpeed = MeasureHorizontalSpeed(deltaTime);

        UpdateSprintFov(deltaTime);
        UpdateHeadBob(deltaTime);
    }

    // ----------------------------------------------------------------- Vitesse

    private float MeasureHorizontalSpeed(float deltaTime)
    {
        Vector3 position = rig.position;
        float speed;

        if (body != null && !body.isKinematic)
        {
            Vector3 velocity = body.linearVelocity;
            speed = new Vector2(velocity.x, velocity.z).magnitude;
        }
        else
        {
            Vector3 moved = position - lastRigPosition;
            speed = new Vector2(moved.x, moved.z).magnitude / deltaTime;
            if (speed > MaxPlausibleSpeed)
            {
                speed = 0f;
            }
        }

        lastRigPosition = position;
        return speed;
    }

    // --------------------------------------------------------------------- FOV

    private void UpdateSprintFov(float deltaTime)
    {
        float target = baseFov;
        if (enableSprintFov && horizontalSpeed > sprintSpeedThreshold)
        {
            target = baseFov + sprintFovIncrease;
        }

        // Si l'interrupteur est coupé, on ne touche plus au FOV (un autre script peut s'en charger)
        // sauf pour revenir doucement à la base si un sprint était en cours.
        if (!enableSprintFov && Mathf.Approximately(cam.fieldOfView, baseFov))
        {
            return;
        }

        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, target, ref fovVelocity, fovSmoothTime, Mathf.Infinity, deltaTime);
    }

    // ---------------------------------------------------------------- Head bob

    private void UpdateHeadBob(float deltaTime)
    {
        bool moving = enableHeadBob && horizontalSpeed > bobMinSpeed;

        bobWeight = Mathf.SmoothDamp(bobWeight, moving ? 1f : 0f, ref bobWeightVelocity, bobBlendTime, Mathf.Infinity, deltaTime);
        if (!moving && bobWeight < 0.001f)
        {
            bobWeight = 0f;
            bobWeightVelocity = 0f;
            transform.localPosition = baseLocalPosition;
            return;
        }

        if (moving)
        {
            bobPhase += horizontalSpeed * bobCyclesPerMeter * deltaTime * Mathf.PI * 2f;
            if (bobPhase > Mathf.PI * 200f)
            {
                bobPhase -= Mathf.PI * 200f;
            }
        }

        // Deux oscillations verticales par cycle (un pas par jambe), une horizontale.
        float vertical = Mathf.Sin(bobPhase * 2f) * bobVerticalAmplitude;
        float horizontal = Mathf.Sin(bobPhase) * bobHorizontalAmplitude;
        transform.localPosition = baseLocalPosition + new Vector3(horizontal, vertical, 0f) * bobWeight;
    }
}
