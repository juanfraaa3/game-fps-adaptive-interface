using UnityEngine;

public class CameraInstabilityTilt : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;
    public StabilityBandController[] stabilitySources;
    public MovementAdaptiveEvaluator adaptiveEvaluator;

    [Header("Tilt Settings")]
    public float maxTiltDegrees = 1.5f;
    public float tiltLerp = 5f;
    public float lateralDeadzone = 0.08f;

    float currentRoll;

    void Awake()
    {
        if (cameraPivot == null)
            cameraPivot = transform;
    }

    void LateUpdate()
    {
        StabilityBandController source = GetClosestStabilitySource();
        if (!cameraPivot || source == null)
            return;

        Vector3 euler = cameraPivot.localEulerAngles;

        float yaw = NormalizeAngle(euler.y);
        float pitch = NormalizeAngle(euler.x);

        float instability = Mathf.Clamp01(source.GetInstability01());
        float lateralNorm = source.GetNormalizedLateral01();

        if (Mathf.Abs(lateralNorm) < lateralDeadzone)
            lateralNorm = 0f;

        float adaptiveMultiplier = 1f;

        if (adaptiveEvaluator != null)
            adaptiveMultiplier = adaptiveEvaluator.MovementAssistWeight01;

        float targetRoll = Mathf.Clamp(
            lateralNorm * instability * maxTiltDegrees * adaptiveMultiplier,
            -maxTiltDegrees,
            maxTiltDegrees
        );


        currentRoll = Mathf.Lerp(
            currentRoll,
            targetRoll,
            Time.deltaTime * tiltLerp
        );

        cameraPivot.localRotation =
            Quaternion.Euler(pitch, yaw, currentRoll);
    }

    // 🔹 Elegimos la plataforma MÁS CERCANA al jugador
    StabilityBandController GetClosestStabilitySource()
    {
        if (stabilitySources == null || stabilitySources.Length == 0)
            return null;

        Transform player = stabilitySources[0].player;
        if (!player) return null;

        StabilityBandController closest = null;
        float minDist = float.MaxValue;

        foreach (var s in stabilitySources)
        {
            if (!s || !s.platformRoot) continue;

            float d = Vector3.Distance(
                player.position,
                s.platformRoot.position
            );

            if (d < minDist)
            {
                minDist = d;
                closest = s;
            }
        }

        return closest;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
