using UnityEngine;
using UnityEngine.UI;

public class CrosshairCorrectionTraceUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RectTransform that defines the center (your visible crosshair object, e.g. V).")]
    public RectTransform crosshairCenter;

    [Tooltip("Camera transform used to measure aiming micro-movements (yaw/pitch deltas).")]
    public Transform cameraTransform;

    [Header("Trace Look (how it draws)")]
    [Tooltip("How many seconds dots stay visible.")]
    public float dotLifetime = 0.45f;

    [Tooltip("How many dots max in the pool.")]
    public int poolSize = 64;

    [Tooltip("Dot size in pixels.")]
    public float dotSize = 4f;

    [Tooltip("Max radius in pixels around crosshair center.")]
    public float maxRadiusPx = 18f;

    [Tooltip("Scales camera rotation delta to UI pixels. Increase if you see almost nothing.")]
    public float rotationToPixels = 650f;

    [Tooltip("Minimum movement (in degrees/frame) to emit dots. Keep small.")]
    public float emitThresholdDeg = 0.02f;

    [Tooltip("Emit rate limit (seconds). 0.016 ~ 60Hz, 0.03 ~ 33Hz.")]
    public float emitInterval = 0.02f;

    [Header("Appearance")]
    [Tooltip("Optional sprite for the dots. If null, uses default UI sprite.")]
    public Sprite dotSprite;

    [Tooltip("Dot alpha at birth.")]
    [Range(0f, 1f)]
    public float dotAlpha = 0.55f;

    [Tooltip("If true, dots fade with unscaled time (won't freeze if timescale changes).")]
    public bool useUnscaledTime = true;

    struct Dot
    {
        public RectTransform rt;
        public Image img;
        public float bornTime;
        public bool active;
    }

    Dot[] _dots;
    int _cursor;
    Quaternion _prevCamRot;
    float _lastEmitTime;
    Vector2 _integratedOffset; // accumulates micro-motions so patterns emerge

    float Now => useUnscaledTime ? Time.unscaledTime : Time.time;

    void Awake()
    {
        if (crosshairCenter == null)
            Debug.LogWarning("[CorrectionTrace] crosshairCenter not assigned.");

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _dots = new Dot[Mathf.Max(8, poolSize)];

        // Create pool dots under this root
        for (int i = 0; i < _dots.Length; i++)
        {
            var go = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (dotSprite != null) img.sprite = dotSprite;

            // Start hidden
            img.color = new Color(1f, 1f, 1f, 0f);

            _dots[i] = new Dot { rt = rt, img = img, bornTime = -999f, active = false };
        }

        if (cameraTransform != null)
            _prevCamRot = cameraTransform.rotation;
    }

    void OnEnable()
    {
        _lastEmitTime = -999f;
        _integratedOffset = Vector2.zero;
        if (cameraTransform != null)
            _prevCamRot = cameraTransform.rotation;
    }

    void Update()
    {
        // Fade existing dots
        float tNow = Now;
        for (int i = 0; i < _dots.Length; i++)
        {
            if (!_dots[i].active) continue;

            float age = tNow - _dots[i].bornTime;
            float a = 1f - Mathf.Clamp01(age / Mathf.Max(0.01f, dotLifetime));
            if (a <= 0f)
            {
                _dots[i].active = false;
                var c0 = _dots[i].img.color;
                _dots[i].img.color = new Color(c0.r, c0.g, c0.b, 0f);
                continue;
            }

            var c = _dots[i].img.color;
            _dots[i].img.color = new Color(c.r, c.g, c.b, a * dotAlpha);
        }

        if (cameraTransform == null || crosshairCenter == null) return;

        // Measure camera rotation delta (aim micro-movements)
        Quaternion cur = cameraTransform.rotation;
        Quaternion delta = cur * Quaternion.Inverse(_prevCamRot);
        _prevCamRot = cur;

        // Convert delta rotation to yaw/pitch degrees (small-angle approx)
        Vector3 e = delta.eulerAngles;
        // Convert 0..360 to -180..180
        float yawDeg = WrapTo180(e.y);
        float pitchDeg = WrapTo180(e.x);

        float absMove = Mathf.Abs(yawDeg) + Mathf.Abs(pitchDeg);
        if (absMove < emitThresholdDeg) return;

        // Rate limit emits
        if (tNow - _lastEmitTime < emitInterval) return;
        _lastEmitTime = tNow;

        // Map to UI offset (note: invert pitch for screen space)
        Vector2 step = new Vector2(yawDeg, pitchDeg) * rotationToPixels * 0.01f;

        // Integrate so patterns (zig-zag / overshoot) become visible
        _integratedOffset += step;

        // Clamp within radius
        if (_integratedOffset.magnitude > maxRadiusPx)
            _integratedOffset = _integratedOffset.normalized * maxRadiusPx;

        EmitDot(_integratedOffset, tNow);
    }

    void EmitDot(Vector2 localOffset, float tNow)
    {
        var d = _dots[_cursor];
        _cursor = (_cursor + 1) % _dots.Length;

        // Place dot around crosshair center (local to this root)
        // crosshairCenter is at the same anchored center; so we just use offset.
        d.rt.anchoredPosition = localOffset;

        d.bornTime = tNow;
        d.active = true;

        var c = d.img.color;
        d.img.color = new Color(c.r, c.g, c.b, dotAlpha);

        _dots[_cursor == 0 ? _dots.Length - 1 : _cursor - 1] = d;
    }

    static float WrapTo180(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        return deg;
    }
}
