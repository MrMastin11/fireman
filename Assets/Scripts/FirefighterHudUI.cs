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
    [SerializeField] private Text healthValueText;
    [SerializeField] private Text waterValueText;

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
        ConfigureValueText(healthValueText);
        ConfigureValueText(waterValueText);

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

        if (healthValueText != null)
            healthValueText.text = $"{Mathf.RoundToInt(vitals.CurrentHealth)}/{Mathf.RoundToInt(vitals.MaxHealth)}";
        if (waterValueText != null)
            waterValueText.text = $"{Mathf.RoundToInt(vitals.CurrentWater)}/{Mathf.RoundToInt(vitals.MaxWater)}";
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

    private static void ConfigureValueText(Text valueText)
    {
        if (valueText == null)
            return;

        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 18;
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.color = Color.white;
        valueText.raycastTarget = false;
        valueText.supportRichText = false;

        Outline outline = valueText.GetComponent<Outline>();
        if (outline == null)
            outline = valueText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }
}
