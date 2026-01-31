using UnityEngine;
using Unity.FPS.Gameplay;

public class OrientationPulseDriver : MonoBehaviour
{
    [Header("References")]
    public Jetpack Jetpack;
    public Camera PlayerCamera;

    [Header("Angle thresholds (deg)")]
    public float MaxErrorDeg = 30f;
    public float GoodAlignDeg = 5f;

    TargetPlatformPulse _activePulse;

    void Update()
    {
        if (Jetpack == null || PlayerCamera == null)
        {
            DisablePulse();
            return;
        }

        Transform target = Jetpack.OrientationTargetPlatform;

        if (target == null)
        {
            DisablePulse();
            return;
        }

        Vector3 toTarget =
            target.position - PlayerCamera.transform.position;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            DisablePulse();
            return;
        }

        float errorDeg = Vector3.Angle(
            PlayerCamera.transform.forward,
            toTarget.normalized
        );

        TargetPlatformPulse pulse =
            target.GetComponentInChildren<TargetPlatformPulse>();

        if (pulse == null)
        {
            DisablePulse();
            return;
        }

        // 🔁 MISMA LÓGICA QUE EL RING
        if (errorDeg > GoodAlignDeg && errorDeg <= MaxErrorDeg)
        {
            float intensity =
                Mathf.InverseLerp(MaxErrorDeg, GoodAlignDeg, errorDeg);

            pulse.SetIntensity(intensity);
            _activePulse = pulse;
        }
        else
        {
            DisablePulse();
        }
    }

    void DisablePulse()
    {
        if (_activePulse != null)
        {
            _activePulse.SetIntensity(0f);
            _activePulse = null;
        }
    }
}
