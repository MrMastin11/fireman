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

        int total = fireSystem.HouseCount;
        int extinguished = Mathf.Clamp(fireSystem.ExtinguishedHouseCount, 0, total);
        progressText.text = $"HOUSES EXTINGUISHED: {extinguished}/{total}";
    }
}
