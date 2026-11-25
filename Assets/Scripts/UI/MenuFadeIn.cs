using UnityEngine;

/// <summary>
/// Fondu d'apparition (et léger glissement vers le haut) à chaque activation de l'objet.
/// Temps réel (unscaledDeltaTime) : fonctionne pendant la pause.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MenuFadeIn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.6f;
    [Tooltip("Décalage vertical de départ en pixels de référence (0 = pas de glissement).")]
    [SerializeField] private float slideOffset = 0f;
    [Tooltip("Optionnel : objet qui glisse (par défaut, celui-ci).")]
    [SerializeField] private RectTransform slideTarget;

    private CanvasGroup group;
    private Vector2 restPosition;
    private bool hasRestPosition;
    private float elapsed;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (slideTarget == null)
        {
            slideTarget = transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        if (slideTarget != null && !hasRestPosition)
        {
            restPosition = slideTarget.anchoredPosition;
            hasRestPosition = true;
        }

        elapsed = 0f;
        Apply(0f);
    }

    private void OnDisable()
    {
        Apply(1f);
    }

    private void Update()
    {
        if (elapsed >= duration)
        {
            return;
        }

        // La première image après un chargement de scène peut durer très longtemps :
        // on plafonne le pas pour que le fondu reste visible.
        elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        Apply(Mathf.Clamp01(elapsed / duration));
    }

    private void Apply(float t)
    {
        float eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubique
        if (group != null)
        {
            group.alpha = eased;
        }

        if (slideTarget != null && hasRestPosition && slideOffset != 0f)
        {
            slideTarget.anchoredPosition = restPosition + new Vector2(0f, -slideOffset * (1f - eased));
        }
    }
}
