using UnityEngine;

/// <summary>
/// Caméra FPS (squelette). À placer sur la racine du prefab PlayerCameraRig.
/// La logique de la souris sera ajoutée à l'étape suivante.
/// </summary>
public class CameraLook : MonoBehaviour
{
    [Header("Souris")]
    [Tooltip("Degrés par pixel de mouvement de souris.")]
    [SerializeField, Min(0f)] private float sensitivity = 0.1f;

    [Header("Limites verticales (degrés)")]
    [SerializeField, Range(-89f, 0f)] private float minPitch = -85f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 85f;

    [Header("Options")]
    [Tooltip("Si vrai, la rotation horizontale (yaw) est appliquée au parent (le Player).")]
    [SerializeField] private bool rotateParentYaw = true;

    [Tooltip("Si vrai, désactive les autres caméras taguées MainCamera au démarrage.")]
    [SerializeField] private bool disableOtherMainCameras = true;

    /// <summary>Permet à d'autres systèmes (pause, menu, cinématique) de suspendre la rotation.</summary>
    public bool LookEnabled { get; set; } = true;

    public float Sensitivity => sensitivity;
    public float MinPitch => minPitch;
    public float MaxPitch => maxPitch;
    public bool RotateParentYaw => rotateParentYaw;
    public bool DisableOtherMainCameras => disableOtherMainCameras;
}
