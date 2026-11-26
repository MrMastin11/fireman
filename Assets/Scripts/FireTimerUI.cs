using System;
using UnityEngine;
using UnityEngine.UI;

public class FireTimerUI : MonoBehaviour
{
    [SerializeField] private Text timerText;
    [SerializeField] private FireSpreadSystem fireSystem;

    private bool timerStopped;
    private double stoppedElapsed;

    private void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<Text>(true);

        if (fireSystem == null)
            fireSystem = FindFirstObjectByType<FireSpreadSystem>();

        if (fireSystem != null)
            fireSystem.AllHousesExtinguished += StopTimer;
    }

    private void Update()
    {
        if (timerText == null)
            return;

        if (!timerStopped && fireSystem != null && fireSystem.IsAllHousesExtinguished)
            StopTimer();

        double elapsed = timerStopped
            ? stoppedElapsed
            : Math.Max(0d, Time.timeSinceLevelLoadAsDouble);
        RefreshText(elapsed);
    }

    private void StopTimer()
    {
        if (timerStopped)
            return;

        stoppedElapsed = Math.Max(0d, Time.timeSinceLevelLoadAsDouble);
        timerStopped = true;
        RefreshText(stoppedElapsed);
    }

    private void RefreshText(double elapsed)
    {
        long totalMilliseconds = (long)Math.Floor(elapsed * 1000d);
        long minutes = totalMilliseconds / 60000L;
        long seconds = (totalMilliseconds / 1000L) % 60L;
        long milliseconds = totalMilliseconds % 1000L;
        timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }

    private void OnDestroy()
    {
        if (fireSystem != null)
            fireSystem.AllHousesExtinguished -= StopTimer;
    }
}
