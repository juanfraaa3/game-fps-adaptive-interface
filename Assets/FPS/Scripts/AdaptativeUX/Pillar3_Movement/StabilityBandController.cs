using System.Collections.Generic;
using UnityEngine;

public class StabilityBandController : MonoBehaviour
{
    [Header("Debug")]
    public bool forceAlwaysOn = true;

    [Header("References")]
    public Transform player;
    public Transform platformRoot;
    public Transform bandVisual;

    // 🔹 NUEVO: referencia al collider para obtener el centro real
    [Header("Platform Center Reference")]
    public BoxCollider platformCollider;

    [Header("Sampling")]
    public float sampleInterval = 0.05f;
    public float windowSeconds = 2.0f;
    public float yOffset = 0.01f;

    [Header("Platform dimensions (local XZ)")]
    public float platformWidth = 6f;
    public float platformLength = 10f;

    [Header("Band behavior")]
    public float baseBandWidth = 2.0f;
    public float minBandWidth = 1.0f;
    public float maxBandWidth = 3.0f;
    public float bandLengthPadding = 0.0f;

    [Header("Activation thresholds")]
    public float stdOn = 0.65f;
    public float stdOff = 0.45f;
    public float edgeRiskOn = 0.35f;
    public float edgeMargin = 0.6f;

    [Header("Smoothing")]
    public float alphaLerp = 6f;
    public float widthLerp = 6f;

    // ================= INTERNAL =================

    class Sample
    {
        public float t;
        public float xLocal;
    }

    List<Sample> samples = new List<Sample>();
    float timer = 0f;

    float currentAlpha = 0f;
    float targetAlpha = 0f;
    float currentWidth;

    Material bandMat;
    Color baseColor;

    // 🔹 NUEVO: centro real de la plataforma en local space
    Vector3 platformCenterLocal = Vector3.zero;

    void Awake()
    {
        if (!bandVisual)
            bandVisual = transform;

        var renderer = bandVisual.GetComponent<Renderer>();
        if (renderer != null)
            bandMat = renderer.material;

        currentWidth = baseBandWidth;

        if (bandMat != null)
        {
            baseColor = bandMat.color;
            SetAlpha(0f);
        }

        bandVisual.gameObject.SetActive(true);

        // 🔹 NUEVO: obtener centro real desde el collider
        if (platformCollider != null)
        {
            platformCenterLocal = platformCollider.center;
        }
        else
        {
            Debug.LogWarning(
                "[StabilityBandController] PlatformCollider no asignado. " +
                "La franja se anclará al pivot."
            );
        }
    }

    void Update()
    {
        if (!player || !platformRoot || !bandVisual)
            return;

        timer += Time.deltaTime;
        if (timer >= sampleInterval)
        {
            timer = 0f;
            AddSample();
        }

        float stdX = ComputeStdX(out _);
        float edgeRisk = ComputeEdgeRisk();

        // ===== ACTIVATION =====
        if (forceAlwaysOn)
        {
            targetAlpha = 1f;
        }
        else
        {
            bool shouldOn =
                (stdX >= stdOn) ||
                (edgeRisk >= edgeRiskOn);

            bool shouldOff =
                (stdX <= stdOff) &&
                (edgeRisk < edgeRiskOn * 0.6f);

            if (targetAlpha <= 0.01f && shouldOn)
                targetAlpha = 1f;
            else if (targetAlpha >= 0.99f && shouldOff)
                targetAlpha = 0f;
        }

        // ===== WIDTH (solo depende de estabilidad lateral) =====
        float t = Mathf.InverseLerp(stdOff, stdOn, stdX);
        float desiredWidth =
            Mathf.Lerp(maxBandWidth, minBandWidth, Mathf.Clamp01(t));

        currentWidth = Mathf.Lerp(
            currentWidth,
            desiredWidth,
            Time.deltaTime * widthLerp
        );

        currentAlpha = Mathf.Lerp(
            currentAlpha,
            targetAlpha,
            Time.deltaTime * alphaLerp
        );

        UpdateBandTransform(currentWidth);
        SetAlpha(currentAlpha);
    }

    // ================= SAMPLING =================

    void AddSample()
    {
        Vector3 localPos = platformRoot.InverseTransformPoint(player.position);

        samples.Add(new Sample
        {
            t = Time.time,
            xLocal = localPos.z
        });

        float minTime = Time.time - windowSeconds;
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].t < minTime)
                samples.RemoveAt(i);
        }
    }

    float ComputeStdX(out float mean)
    {
        mean = 0f;
        int n = samples.Count;
        if (n <= 1) return 0f;

        for (int i = 0; i < n; i++)
            mean += samples[i].xLocal;

        mean /= n;

        float variance = 0f;
        for (int i = 0; i < n; i++)
        {
            float d = samples[i].xLocal - mean;
            variance += d * d;
        }

        variance /= (n - 1);
        return Mathf.Sqrt(variance);
    }

    float ComputeEdgeRisk()
    {
        int n = samples.Count;
        if (n == 0) return 0f;

        float halfWidth = platformWidth * 0.5f;
        int risky = 0;

        for (int i = 0; i < n; i++)
        {
            float distToEdge = halfWidth - Mathf.Abs(samples[i].xLocal);
            if (distToEdge <= edgeMargin)
                risky++;
        }

        return (float)risky / n;
    }

    // ================= VISUAL =================

    // 🔹 MODIFICADO: ahora la franja se ancla al centro REAL del collider
    void UpdateBandTransform(float width)
    {
        Vector3 pos = platformCenterLocal;
        pos.y += yOffset;

        bandVisual.localPosition = pos;

        float length = platformLength + bandLengthPadding;
        bandVisual.localScale = new Vector3(width, length, 1f);
    }

    void SetAlpha(float alpha)
    {
        if (bandMat == null) return;

        Color c = baseColor;
        c.a = Mathf.Clamp01(alpha) * baseColor.a;
        bandMat.color = c;
    }
}
