using System;
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

        double elapsed = Math.Max(0d, Time.timeSinceLevelLoadAsDouble);
        long totalMilliseconds = (long)Math.Floor(elapsed * 1000d);
        long minutes = totalMilliseconds / 60000L;
        long seconds = (totalMilliseconds / 1000L) % 60L;
        long milliseconds = totalMilliseconds % 1000L;
        timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }
}
