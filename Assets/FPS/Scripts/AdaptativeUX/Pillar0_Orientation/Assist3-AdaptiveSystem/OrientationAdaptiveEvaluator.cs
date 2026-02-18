using UnityEngine;

public class OrientationAdaptiveEvaluator : MonoBehaviour
{
    [Header("Reference")]
    public JetpackOrientationMetrics Metrics;

    [Header("P75 Thresholds")]
    public float P75_TotalDuration;
    public float P75_AverageAngle;
    public float P75_TimeAway;
    public float P75_PercentAway;
    public float P75_Death;

    [Header("P90 Thresholds")]
    public float P90_TotalDuration;
    public float P90_AverageAngle;
    public float P90_TimeAway;
    public float P90_PercentAway;
    public float P90_Death;

    public float OrientationAssistWeight01 { get; private set; }

    void OnEnable()
    {
        Metrics.OnSegmentEvaluated += EvaluateSegment;
    }

    void OnDisable()
    {
        Metrics.OnSegmentEvaluated -= EvaluateSegment;
    }

    void EvaluateSegment(
        float totalDuration,
        float avgAngle,
        float timeAway,
        float percentAway,
        int endedInDeath)
    {
        float durScore = Normalize(totalDuration, P75_TotalDuration, P90_TotalDuration);
        float angleScore = Normalize(avgAngle, P75_AverageAngle, P90_AverageAngle);
        float timeScore = Normalize(timeAway, P75_TimeAway, P90_TimeAway);
        float percentScore = Normalize(percentAway, P75_PercentAway, P90_PercentAway);
        float deathScore = Normalize(endedInDeath, P75_Death, P90_Death);

        OrientationAssistWeight01 =
            (durScore + angleScore + timeScore + percentScore + deathScore) / 5f;

        Debug.Log($"[ORIENTATION ASSIST] Weight={OrientationAssistWeight01:F2}");
    }

    float Normalize(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;
        return Mathf.InverseLerp(p75, p90, value);
    }
}
