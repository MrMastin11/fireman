using UnityEngine;
using UnityEngine.UI;

public class FirefighterHudUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FirefighterVitals vitals;
    [SerializeField] private RectTransform healthFill;
    [SerializeField] private RectTransform waterFill;
    [SerializeField] private RawImage healthBackground;
    [SerializeField] private RawImage healthFillGraphic;
    [SerializeField] private RawImage waterBackground;
    [SerializeField] private RawImage waterFillGraphic;

    [Header("Layout")]
    [SerializeField] private float healthBarWidth = 300f;
    [SerializeField] private float waterBarHeight = 220f;

    private void Awake()
    {
        if (vitals == null)
            vitals = FindFirstObjectByType<FirefighterVitals>();

        ConfigureGraphic(healthBackground, new Color(0.08f, 0.03f, 0.03f, 0.78f));
        ConfigureGraphic(healthFillGraphic, new Color(0.85f, 0.04f, 0.03f, 1f));
        ConfigureGraphic(waterBackground, new Color(0.02f, 0.06f, 0.12f, 0.78f));
        ConfigureGraphic(waterFillGraphic, new Color(0.04f, 0.45f, 0.95f, 1f));

        if (healthFill != null)
            healthBarWidth = healthFill.sizeDelta.x > 0f ? healthFill.sizeDelta.x : healthBarWidth;
        if (waterFill != null)
            waterBarHeight = waterFill.sizeDelta.y > 0f ? waterFill.sizeDelta.y : waterBarHeight;

        RefreshBars();
    }

    private void Update()
    {
        RefreshBars();
    }

    private void RefreshBars()
    {
        if (vitals == null)
            return;

        SetHorizontalFill(healthFill, healthBarWidth, vitals.Health01);
        SetVerticalFill(waterFill, waterBarHeight, vitals.Water01);
    }

    private static void SetHorizontalFill(RectTransform fill, float maxWidth, float ratio)
    {
        if (fill == null)
            return;

        Vector2 size = fill.sizeDelta;
        size.x = maxWidth * Mathf.Clamp01(ratio);
        fill.sizeDelta = size;
    }

    private static void SetVerticalFill(RectTransform fill, float maxHeight, float ratio)
    {
        if (fill == null)
            return;

        Vector2 size = fill.sizeDelta;
        size.y = maxHeight * Mathf.Clamp01(ratio);
        fill.sizeDelta = size;
    }

    private static void ConfigureGraphic(RawImage graphic, Color color)
    {
        if (graphic == null)
            return;

        graphic.texture = Texture2D.whiteTexture;
        graphic.color = color;
        graphic.raycastTarget = false;
    }
}
