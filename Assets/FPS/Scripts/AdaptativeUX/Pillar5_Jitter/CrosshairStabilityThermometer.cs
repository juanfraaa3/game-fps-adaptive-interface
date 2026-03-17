using UnityEngine;
using UnityEngine.UI;

public class CrosshairStabilityThermometer : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [Tooltip("The visual root that will jitter/pulse (child RectTransform).")]
    public RectTransform crosshairVisual;

    [Tooltip("Optional Image component of the crosshair so we can tint it red under stress.")]
    public Image crosshairImage;

    [Header("Input (0..1)")]
    [Range(0f, 1f)]
    public float targetStability = 1f;  // 1 = estable, 0 = inestable (debug por ahora)

    [Header("Adaptive")]
    public JitterAdaptiveEvaluator Evaluator;

    [Header("Smoothing")]
    [Tooltip("Seconds to smooth changes in stability (prevents sudden jumps).")]
    public float stabilitySmoothTime = 0.25f;

    [Header("Jitter Motion")]
    [Tooltip("Max pixel offset when completely unstable (stability=0). Keep small.")]
    public float maxJitterPixels = 28f;

    [Tooltip("How fast the jitter changes (Hz-ish).")]
    public float jitterSpeed = 32f;

    [Header("Pulse (optional)")]
    [Tooltip("Scale pulse at instability. 0.06 = 6% max scale up.")]
    public float pulseScaleAmount = 0.18f;

    [Tooltip("Pulse speed.")]
    public float pulseSpeed = 3.5f;

    [Header("Stress Warning")]
    [Tooltip("If assist weight goes above this threshold, the crosshair turns red and pulses harder.")]
    public float stressThreshold = 0.75f;

    public Color normalColor = Color.white;
    public Color stressColor = Color.red;

    [Tooltip("Extra pulse multiplier when stressed.")]
    public float stressPulseMultiplier = 2f;

    [Header("Aim Filter")]
    public float AimActivationThreshold = 0.3f;

    [Header("Debug")]
    [Header("Aim Gating")]
    public bool requireAimToActivate = true;
    public float aimAxisThreshold = 0.3f;

    public bool debugKeys = true;
    public KeyCode setStableKey = KeyCode.Alpha1;     // stability = 1
    public KeyCode setUnstableKey = KeyCode.Alpha2;   // stability = 0
    public KeyCode setMidKey = KeyCode.Alpha3;        // stability = 0.5

    float _visualStability;
    float _stabilityVel;

    Vector2 _baseAnchoredPos;
    Vector3 _baseLocalScale;

    void Awake()
    {
        if (crosshairVisual == null)
            crosshairVisual = GetComponent<RectTransform>();

        _visualStability = Mathf.Clamp01(targetStability);

        if (crosshairVisual != null)
        {
            _baseAnchoredPos = crosshairVisual.anchoredPosition;
            _baseLocalScale = crosshairVisual.localScale;
        }

        if (crosshairImage == null && crosshairVisual != null)
            crosshairImage = crosshairVisual.GetComponent<Image>();
    }

    void Update()
    {
        if (crosshairVisual == null)
            return;

        // ----------------------------------------------------
        // AIM FILTER (PS4 + Xbox compatible)
        // ----------------------------------------------------
        if (requireAimToActivate)
        {
            float aimAxis = Input.GetAxis("Aim");
            bool aimButton = Input.GetButton("Aim");
            bool ps4L2 = Input.GetKey(KeyCode.JoystickButton6);
            bool xboxLT = Input.GetKey(KeyCode.JoystickButton7);

            bool isAiming =
                aimAxis > aimAxisThreshold ||
                aimButton ||
                ps4L2 ||
                xboxLT;

            if (!isAiming)
            {
                // Restaurar estado estable
                crosshairVisual.anchoredPosition = _baseAnchoredPos;
                crosshairVisual.localScale = _baseLocalScale;

                if (crosshairImage != null)
                    crosshairImage.color = normalColor;

                return;
            }
        }

        // Debug quick-test without any adaptive logic
        if (debugKeys)
        {
            if (Input.GetKeyDown(setStableKey)) targetStability = 1f;
            if (Input.GetKeyDown(setUnstableKey)) targetStability = 0f;
            if (Input.GetKeyDown(setMidKey)) targetStability = 0.5f;
        }

        // =======================================
        // ADAPTIVE INPUT (reemplaza debug real)
        // =======================================
        float assistWeight = 0f;

        if (Evaluator != null)
        {
            assistWeight = Evaluator.JitterAssistWeight01;

            if (assistWeight <= 0f)
                targetStability = 1f; // totalmente estable
            else
                targetStability = 1f - assistWeight;
        }

        targetStability = Mathf.Clamp01(targetStability);

        // Smooth stability so it feels continuous (no sudden jumps)
        _visualStability = Mathf.SmoothDamp(
            _visualStability,
            targetStability,
            ref _stabilityVel,
            stabilitySmoothTime
        );

        float instability = 1f - _visualStability;
        bool stressed = assistWeight >= stressThreshold;

        // --- Crosshair Color ---
        if (crosshairImage != null)
        {
            crosshairImage.color = stressed ? stressColor : normalColor;
        }

        // --- Jitter: small animated offset in UI pixels ---
        // PerlinNoise gives smooth randomness (no harsh shaking)
        float t = Time.unscaledTime * jitterSpeed;

        float nx = Mathf.PerlinNoise(10.13f, t) * 2f - 1f;
        float ny = Mathf.PerlinNoise(25.77f, t + 0.37f) * 2f - 1f;

        Vector2 jitter = new Vector2(nx, ny) * (maxJitterPixels * instability);

        crosshairVisual.anchoredPosition = _baseAnchoredPos + jitter;

        // --- Pulse: subtle "breathing" only when unstable ---
        if (pulseScaleAmount > 0f)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * pulseSpeed) * 0.5f + 0.5f; // 0..1
            float pulseBoost = stressed ? stressPulseMultiplier : 1f;
            float scaleAdd = pulseScaleAmount * instability * pulse * pulseBoost;
            crosshairVisual.localScale = _baseLocalScale * (1f + scaleAdd);
        }
        else
        {
            crosshairVisual.localScale = _baseLocalScale;
        }
    }

    // Optional API so later you can feed stability from your jitter system
    public void SetStability01(float stability01)
    {
        targetStability = Mathf.Clamp01(stability01);
    }
}