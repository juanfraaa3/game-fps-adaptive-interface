using UnityEngine;
using UnityEngine.UI;
using Unity.FPS.Gameplay;

public class CrosshairAlignmentRing : MonoBehaviour
{
    [Header("References")]
    public Jetpack Jetpack;
    public Camera PlayerCamera;
    public Image RingImage;

    [Header("Angle thresholds (deg)")]
    public float MaxErrorDeg = 30f;   // Muy mal alineado
    public float GoodAlignDeg = 5f;   // Casi alineado
    public float HideDeg = 2f;        // Perfecto (verde + pulso)

    [Header("Smoothing")]
    public float LerpSpeed = 12f;

    [Header("Colors")]
    public Color NormalColor = Color.white;
    public Color BadAlignColor = Color.red;
    public Color PerfectColor = Color.green;

    [Header("Color thresholds")]
    [Range(0f, 1f)]
    public float RedThreshold = 0.3f; // "30%"

    [Header("Perfect Alignment Pulse")]
    public float PulseSpeed = 4f;
    public float PulseScale = 1.15f;

    float _fill;
    RectTransform _ringRect;
    Vector3 _baseScale;

    void Awake()
    {
        if (RingImage != null)
        {
            _ringRect = RingImage.rectTransform;
            _baseScale = _ringRect.localScale;
        }
    }

    void Update()
    {
        if (Jetpack == null || PlayerCamera == null || RingImage == null)
            return;

        Transform target = Jetpack.OrientationTargetPlatform;

        // Sin objetivo => ocultar
        if (target == null)
        {
            RingImage.enabled = false;
            return;
        }

        Vector3 toTarget = target.position - PlayerCamera.transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            RingImage.enabled = false;
            return;
        }

        float errorDeg = Vector3.Angle(PlayerCamera.transform.forward, toTarget.normalized);

        // ===== PERFECTO: verde + pulso =====
        if (errorDeg <= HideDeg)
        {
            RingImage.enabled = true;
            RingImage.fillAmount = 1f;

            Color pc = PerfectColor;
            pc.a = 0.9f;
            RingImage.color = pc;

            if (_ringRect != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * (PulseScale - 1f);
                _ringRect.localScale = _baseScale * pulse;
            }

            return;
        }
        // ==================================

        // Reset visual cuando no está perfecto
        if (_ringRect != null)
            _ringRect.localScale = _baseScale;

        RingImage.enabled = true;

        // Normalización (0 = muy mal, 1 = casi perfecto)
        float t = Mathf.InverseLerp(MaxErrorDeg, GoodAlignDeg, errorDeg);
        float targetFill = Mathf.Clamp01(t);

        _fill = Mathf.Lerp(_fill, targetFill, Time.deltaTime * LerpSpeed);
        RingImage.fillAmount = _fill;

        // ===== COLOR ROJO / NORMAL SEGÚN "30%" =====
        if (t <= RedThreshold)
            RingImage.color = BadAlignColor;
        else
            RingImage.color = NormalColor;
        // ==========================================

        // Opacidad proporcional al error
        Color c = RingImage.color;
        float alpha = Mathf.Lerp(0.25f, 0.9f,
            Mathf.InverseLerp(GoodAlignDeg, MaxErrorDeg, errorDeg));
        c.a = alpha;
        RingImage.color = c;
    }
}
