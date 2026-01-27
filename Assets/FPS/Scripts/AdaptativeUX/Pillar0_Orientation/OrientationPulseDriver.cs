using UnityEngine;

public class OrientationPulseDriver : MonoBehaviour
{
    [Header("References")]
    public JetpackOrientationMetrics OrientationMetrics;
    public AdaptivePillarEvaluator adaptiveEvaluator;

    [Header("Orientation Rules")]
    public float MaxAllowedAngleDeg = 35f;
    public float TimeAwayToTrigger = 0.8f;

    [Header("Cooldown")]
    public float PulseCooldown = 1.2f;

    private float _timeLookingAway = 0f;
    private float _nextPulseTime = 0f;

    void Awake()
    {
        if (OrientationMetrics == null)
            OrientationMetrics = GetComponent<JetpackOrientationMetrics>();

        if (adaptiveEvaluator == null)
            adaptiveEvaluator = GetComponent<AdaptivePillarEvaluator>();
    }

    void Update()
    {
        Debug.Log(
            $"[ORIENT UPDATE] frame={Time.frameCount} | " +
            $"Target={(OrientationMetrics.CurrentTargetPlatform != null ? OrientationMetrics.CurrentTargetPlatform.name : "NULL")}"
        );

        // =====================================================
        // 🔒 GATE ADAPTATIVO (decisión tomada al respawn)
        // =====================================================
        if (adaptiveEvaluator == null)
            return;

        if (!adaptiveEvaluator.OrientationAssistEnabled)
            return;

        // =====================================================
        // 🔍 VALIDACIONES BÁSICAS
        // =====================================================
        if (OrientationMetrics == null)
            return;

        if (OrientationMetrics.PlayerCamera == null)
            return;

        if (OrientationMetrics.CurrentTargetPlatform == null)
            return;

        // =====================================================
        // 🎯 TARGET ACTUAL + PULSO DINÁMICO
        // =====================================================
        Transform target = OrientationMetrics.CurrentTargetPlatform;

        TargetPlatformPulse targetPulse =
            target.GetComponent<TargetPlatformPulse>();

        if (targetPulse == null)
            targetPulse = target.GetComponentInChildren<TargetPlatformPulse>();

        if (targetPulse == null)
            return; // esta plataforma no tiene pulso, se ignora

        // =====================================================
        // 📐 CÁLCULO DE ORIENTACIÓN
        // =====================================================
        Vector3 camPos = OrientationMetrics.PlayerCamera.transform.position;
        Vector3 camForward = OrientationMetrics.PlayerCamera.transform.forward;
        Vector3 targetPos = target.position;

        Vector3 toTarget = targetPos - camPos;
        if (toTarget.sqrMagnitude < 0.001f)
            return;

        Debug.Log(
            $"[ORIENT ANGLE] 🎯 Target={target.name} | camForward={camForward} | toTarget={toTarget.normalized}"
        );

        float angle = Vector3.Angle(camForward, toTarget.normalized);

        if (angle > MaxAllowedAngleDeg)
        {
            _timeLookingAway += Time.deltaTime;
        }
        else
        {
            _timeLookingAway = 0f;
        }

        // =====================================================
        // 🚨 ACTIVACIÓN DEL PULSO (ESCALA AUTOMÁTICAMENTE)
        // =====================================================
        if (_timeLookingAway >= TimeAwayToTrigger &&
            Time.time >= _nextPulseTime)
        {
            Debug.Log(
    $"[PULSE FIRE] 🚨 frame={Time.frameCount} | " +
    $"Target={target.name} | timeAway={_timeLookingAway:F2}"
);
            targetPulse.Pulse();
            _nextPulseTime = Time.time + PulseCooldown;
            _timeLookingAway = 0f;
        }
    }
}
