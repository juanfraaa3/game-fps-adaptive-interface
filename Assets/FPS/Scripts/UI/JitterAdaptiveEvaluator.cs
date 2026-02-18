using UnityEngine;


public class JitterAdaptiveEvaluator : MonoBehaviour
{
    [Header("Reference")]
    public Unity.FPS.Game.JitterMetricsLogger Logger;

    [Header("Thresholds (Higher = Worse)")]
    public float P75_SmoothedJitter = 1.5f;
    public float P90_SmoothedJitter = 3.0f;

    [Header("Output")]
    [Range(0f, 1f)]
    public float JitterAssistWeight01 = 0f;

    void Update()
    {
        if (Logger == null)
            return;

        float smoothed = Logger.JitterSource.SmoothedJitter;

        JitterAssistWeight01 = NormalizeHigherWorse(
            smoothed,
            P75_SmoothedJitter,
            P90_SmoothedJitter
        );
    }

    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;

        return Mathf.InverseLerp(p75, p90, value);
    }
}
