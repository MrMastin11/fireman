using UnityEngine;
using UnityEngine.UI;

public class FireTimerUI : MonoBehaviour
{
    [SerializeField] private Text timerText;

    private void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<Text>(true);
    }

    private void Update()
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}
