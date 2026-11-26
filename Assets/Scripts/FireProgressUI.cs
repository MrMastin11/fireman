using UnityEngine;
using UnityEngine.UI;

public class FireProgressUI : MonoBehaviour
{
    [SerializeField] private Text progressText;
    [SerializeField] private FireSpreadSystem fireSystem;

    private void Awake()
    {
        if (progressText == null)
            progressText = GetComponentInChildren<Text>(true);

        if (fireSystem == null)
            fireSystem = FindFirstObjectByType<FireSpreadSystem>();

        if (progressText == null)
            return;

        progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progressText.fontSize = 21;
        progressText.fontStyle = FontStyle.Bold;
        progressText.alignment = TextAnchor.MiddleCenter;
        progressText.color = Color.white;
        progressText.raycastTarget = false;
        progressText.supportRichText = false;

        Outline outline = progressText.GetComponent<Outline>();
        if (outline == null)
            outline = progressText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private void Update()
    {
        if (fireSystem == null)
            fireSystem = FindFirstObjectByType<FireSpreadSystem>();

        if (progressText == null || fireSystem == null)
            return;

        int total = fireSystem.FinalHouseInitialFireCount;
        int extinguished = Mathf.Min(fireSystem.ExtinguishedFinalHouseFireCount, total);
        string status = fireSystem.FinalHouseIsClear ? "FINAL HOUSE: CLEAR" : "FINAL HOUSE: ON FIRE";
        progressText.text = $"EXTINGUISHED: {extinguished}/{total}   |   {status}";
    }
}
