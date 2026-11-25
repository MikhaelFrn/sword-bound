using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animation des boutons de menu : contour et texte qui passent à l'or au survol ou à la sélection
/// (clavier / manette), léger agrandissement, effet d'appui au clic.
/// Les transitions utilisent le temps réel (unscaledDeltaTime) : elles fonctionnent pendant la pause.
/// La transition intégrée du Button doit être à "None" : ce script gère tout l'aspect visuel.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Éléments")]
    [SerializeField] private Image fill;
    [SerializeField] private Image frame;
    [SerializeField] private Text label;

    [Header("Couleurs")]
    [SerializeField] private Color fillNormal = new Color32(0x2B, 0x21, 0x18, 0xF0);
    [SerializeField] private Color fillHighlighted = new Color32(0x38, 0x2A, 0x1D, 0xF5);
    [SerializeField] private Color fillPressed = new Color32(0x4A, 0x37, 0x24, 0xFF);
    [SerializeField] private Color frameNormal = new Color32(0xD4, 0xA9, 0x51, 0x70);
    [SerializeField] private Color frameHighlighted = new Color32(0xD4, 0xA9, 0x51, 0xFF);
    [SerializeField] private Color labelNormal = new Color32(0xED, 0xE3, 0xCF, 0xFF);
    [SerializeField] private Color labelHighlighted = new Color32(0xD4, 0xA9, 0x51, 0xFF);

    [Header("Mouvement")]
    [SerializeField, Min(1f)] private float highlightScale = 1.04f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.97f;
    [Tooltip("Vitesse des transitions (plus grand = plus rapide). 14 ≈ 0,15 s.")]
    [SerializeField, Min(1f)] private float speed = 14f;

    private Button button;
    private bool hovered;
    private bool selected;
    private bool pressed;
    private float highlight; // 0 = normal, 1 = survolé / sélectionné
    private float press;     // 0 = relâché, 1 = enfoncé

    private void Awake()
    {
        button = GetComponent<Button>();
        ApplyVisuals();
    }

    private void OnDisable()
    {
        // Un objet désactivé ne reçoit pas OnPointerExit : on repart d'un état propre.
        hovered = selected = pressed = false;
        highlight = press = 0f;
        ApplyVisuals();
    }

    private void Update()
    {
        bool interactable = button == null || button.IsInteractable();
        float targetHighlight = interactable && (hovered || selected) ? 1f : 0f;
        float targetPress = interactable && pressed ? 1f : 0f;

        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        highlight = Mathf.Lerp(highlight, targetHighlight, t);
        press = Mathf.Lerp(press, targetPress, Mathf.Min(1f, t * 1.5f));
        if (Mathf.Abs(highlight - targetHighlight) < 0.001f) highlight = targetHighlight;
        if (Mathf.Abs(press - targetPress) < 0.001f) press = targetPress;

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (fill != null)
        {
            Color c = Color.Lerp(fillNormal, fillHighlighted, highlight);
            fill.color = Color.Lerp(c, fillPressed, press);
        }

        if (frame != null)
        {
            frame.color = Color.Lerp(frameNormal, frameHighlighted, highlight);
        }

        if (label != null)
        {
            label.color = Color.Lerp(labelNormal, labelHighlighted, highlight);
        }

        float scale = Mathf.Lerp(1f, highlightScale, highlight) * Mathf.Lerp(1f, pressedScale, press);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        // Le survol sélectionne aussi le bouton : jamais deux boutons en surbrillance (souris + clavier).
        if (EventSystem.current != null && button != null && button.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            pressed = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }
}
