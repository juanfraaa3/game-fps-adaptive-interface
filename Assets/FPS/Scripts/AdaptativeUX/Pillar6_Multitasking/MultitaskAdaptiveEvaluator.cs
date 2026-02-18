using UnityEngine;

public class MultitaskAdaptiveEvaluator : MonoBehaviour
{
    [Header("Thresholds (Higher = Worse)")]
    [Range(0f, 1f)]
    public float P75_Load = 0.55f;

    [Range(0f, 1f)]
    public float P90_Load = 0.75f;

    [Header("References")]
    public MultitaskLoadEvaluator loadSource;

    [Header("Output")]
    [Range(0f, 1f)]
    public float MultitaskAssistWeight01 = 0f;

    void Update()
    {
        if (loadSource == null)
            return;

        float load = loadSource.load01;

        MultitaskAssistWeight01 =
            NormalizeHigherWorse(load, P75_Load, P90_Load);
    }

    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;

        return Mathf.InverseLerp(p75, p90, value);
    }
}
