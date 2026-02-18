using UnityEngine;

public class TrajectoryAdaptiveEvaluator : MonoBehaviour
{
    [Header("Reference")]
    public Unity.FPS.Gameplay.JetpackTrajectoryLogger Logger;

    [Header("P75 Thresholds")]
    public float P75_Efficiency;
    public float P75_ZigZag;
    public float P75_Curvature;
    public float P75_LateralOffset;
    public float P75_LandingError;

    [Header("P90 Thresholds")]
    public float P90_Efficiency;
    public float P90_ZigZag;
    public float P90_Curvature;
    public float P90_LateralOffset;
    public float P90_LandingError;

    public float TrajectoryAssistWeight01 { get; private set; }
    public float LastTrajectoryQuality01 { get; private set; }
    public bool LastEndedInDeath { get; private set; }

    void OnEnable()
    {
        if (Logger != null)
            Logger.OnTrajectorySegmentEvaluated += Evaluate;
    }

    void OnDisable()
    {
        if (Logger != null)
            Logger.OnTrajectorySegmentEvaluated -= Evaluate;
    }

    void Evaluate(
    float duration,
    float efficiency,
    float zigzag,
    float curvature,
    float lateral,
    float landingError,
    float totalDistance,
    float verticalOsc,
    int endedInDeath)
    {
        // Guardar si terminó en muerte
        LastEndedInDeath = (endedInDeath == 1);

        float effScore = NormalizeLowerWorse(efficiency, P75_Efficiency, P90_Efficiency);
        float zigzagScore = NormalizeHigherWorse(zigzag, P75_ZigZag, P90_ZigZag);
        float curvatureScore = NormalizeHigherWorse(curvature, P75_Curvature, P90_Curvature);
        float lateralScore = NormalizeHigherWorse(lateral, P75_LateralOffset, P90_LateralOffset);
        float landingScore = NormalizeHigherWorse(landingError, P75_LandingError, P90_LandingError);

        TrajectoryAssistWeight01 =
            (effScore + zigzagScore + curvatureScore + lateralScore + landingScore) / 5f;

        Debug.Log($"[TRAJECTORY ASSIST] Weight={TrajectoryAssistWeight01:F2} | Death={LastEndedInDeath}");
    }



    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;
        return Mathf.InverseLerp(p75, p90, value);
    }

    float NormalizeLowerWorse(float value, float p75, float p90)
    {
        if (value >= p75) return 0f;
        if (value <= p90) return 1f;
        return Mathf.InverseLerp(p75, p90, value);
    }
}
