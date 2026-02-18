using UnityEngine;

public class MovementAdaptiveEvaluator : MonoBehaviour
{
    [Header("Thresholds (Higher = Worse)")]

    public float P75_Std = 0.4f;
    public float P90_Std = 0.8f;

    public float P75_EdgeRatio = 0.15f;   // EdgeRiskTime / TimeOnPlatform
    public float P90_EdgeRatio = 0.35f;

    public float P75_CorrectionsPerSec = 1.5f;
    public float P90_CorrectionsPerSec = 3.5f;

    [Header("Output")]
    [Range(0f, 1f)]
    public float MovementAssistWeight01;

    public void Evaluate(
        float std,
        float edgeRiskTime,
        float timeOnPlatform,
        int correctionPeaks,
        int rawCorrectionsCount
    )
    {
        if (timeOnPlatform <= 0.1f)
            return;

        float edgeRatio = edgeRiskTime / timeOnPlatform;

        float correctionsPerSec =
            (correctionPeaks + rawCorrectionsCount) / timeOnPlatform;

        float stdScore = NormalizeHigherWorse(std, P75_Std, P90_Std);
        float edgeScore = NormalizeHigherWorse(edgeRatio, P75_EdgeRatio, P90_EdgeRatio);
        float correctionScore = NormalizeHigherWorse(correctionsPerSec, P75_CorrectionsPerSec, P90_CorrectionsPerSec);

        MovementAssistWeight01 =
            0.45f * stdScore +
            0.30f * edgeScore +
            0.25f * correctionScore;

        Debug.Log($"[MOVEMENT ASSIST] Weight={MovementAssistWeight01:F2}");
    }

    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;

        return Mathf.InverseLerp(p75, p90, value);
    }
}
