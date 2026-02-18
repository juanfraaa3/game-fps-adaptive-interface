using UnityEngine;

public class LandingAdaptiveEvaluator : MonoBehaviour
{
    [Header("Thresholds (Higher = Worse)")]
    public float P75_LandingOffset = 0.5f;
    public float P90_LandingOffset = 1.0f;

    public float P75_PostLandingDrift = 0.5f;
    public float P90_PostLandingDrift = 1.5f;

    public float P75_VerticalSpeed = 12f;
    public float P90_VerticalSpeed = 20f;

    [Header("Output")]
    [Range(0f, 1f)]
    public float LandingAssistWeight01 = 0f;

    public void Evaluate(
        float landingOffset,
        float postLandingDrift,
        float verticalSpeed
    )
    {
        float offsetScore = NormalizeHigherWorse(landingOffset, P75_LandingOffset, P90_LandingOffset);
        float driftScore = NormalizeHigherWorse(postLandingDrift, P75_PostLandingDrift, P90_PostLandingDrift);
        float speedScore = NormalizeHigherWorse(verticalSpeed, P75_VerticalSpeed, P90_VerticalSpeed);

        LandingAssistWeight01 =
            (offsetScore + driftScore + speedScore) / 3f;

        Debug.Log($"[LANDING ASSIST] Weight={LandingAssistWeight01:F2}");
    }

    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;

        return Mathf.InverseLerp(p75, p90, value);
    }
}
