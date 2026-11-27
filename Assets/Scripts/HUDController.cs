using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD for Axe. Purely visual: the GameManager calls the public methods below
/// whenever the game state changes. No game logic lives here.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Image healthFill;          // Image Type = Filled, Fill Method = Horizontal
    [SerializeField] private TMP_Text healthText;       // optional, e.g. "75 / 100"

    [Header("Counters")]
    [SerializeField] private TMP_Text killCountText;
    [SerializeField] private TMP_Text itemCountText;

    [Header("Crouch Indicator")]
    [SerializeField] private RectTransform crouchIndicator;
    [Range(0.1f, 1f)]
    [SerializeField] private float crouchScaleY = 0.5f; // 0.5 = cut in half
    [SerializeField] private float crouchLerpSpeed = 15f;

    private float targetScaleY = 1f;

    private void Update()
    {
        // Smoothly animate the crouch rectangle toward its target height.
        if (crouchIndicator == null) return;

        Vector3 scale = crouchIndicator.localScale;
        scale.y = Mathf.Lerp(scale.y, targetScaleY, crouchLerpSpeed * Time.unscaledDeltaTime);
        crouchIndicator.localScale = scale;
    }

    public void SetHealth(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (healthFill != null) healthFill.fillAmount = ratio;
        if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void SetKillCount(int kills)
    {
        if (killCountText != null) killCountText.text = $"Kills: {kills}";
    }

    public void SetItemCount(int items)
    {
        if (itemCountText != null) itemCountText.text = $"Items: {items}";
    }

    public void SetCrouching(bool isCrouching)
    {
        targetScaleY = isCrouching ? crouchScaleY : 1f;
    }
}
