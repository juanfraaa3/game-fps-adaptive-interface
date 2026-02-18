using System.Collections.Generic;
using UnityEngine;

public class StabilityBandController : MonoBehaviour
{
    [Header("Debug")]
    public bool forceAlwaysOn = false;

    [Header("References")]
    public Transform player;
    public Transform platformRoot;
    public Transform bandVisual;

    [Header("Floor Instability Visual")]
    public Transform floorRoot;
    public string instabilityProperty = "_Instability";

    [Header("Platform Collider (for true center)")]
    public BoxCollider platformCollider;

    [Header("Sampling")]
    public float sampleInterval = 0.05f;
    public float windowSeconds = 2.0f;
    public float yOffset = 0.01f;

    [Header("Platform dimensions (local XZ)")]
    public float platformWidth = 9f;
    public float platformLength = 9f;

    [Header("Band behavior")]
    public float baseBandWidth = 2.0f;
    public float minBandWidth = 1.0f;
    public float maxBandWidth = 3.0f;

    [Header("Activation thresholds")]
    public float stdOn = 0.65f;
    public float stdOff = 0.45f;

    [Header("Smoothing")]
    public float alphaLerp = 6f;
    public float widthLerp = 6f;
    public float instabilityLerp = 4f;
    public float CurrentInstability01 => currentInstability;

    [Header("Adaptive")]
    public MovementAdaptiveEvaluator adaptiveEvaluator;

    // ================= INTERNAL =================

    class Sample
    {
        public float t;
        public float lateralLocal;
    }

    static List<StabilityBandController> allControllers = new List<StabilityBandController>();

    List<Sample> samples = new List<Sample>();
    float timer;

    float currentBandWidth;
    float currentInstability;

    Material bandMat;
    Color baseBandColor;

    Renderer[] floorRenderers;
    Material[] floorMats;

    Quaternion bandBaseRotation;

    // ------------------------------------------------

    void OnEnable()
    {
        if (!allControllers.Contains(this))
            allControllers.Add(this);
    }

    void OnDisable()
    {
        allControllers.Remove(this);
    }

    void Awake()
    {
        if (bandVisual != null)
        {
            bandBaseRotation = bandVisual.rotation;

            var r = bandVisual.GetComponent<Renderer>();
            if (r != null)
            {
                bandMat = r.material;
                baseBandColor = bandMat.color;
                SetBandAlpha(0f);
            }
        }

        if (platformCollider == null && platformRoot != null)
        {
            platformCollider = platformRoot.GetComponent<BoxCollider>();
            if (platformCollider == null)
                platformCollider = platformRoot.GetComponentInChildren<BoxCollider>();
        }

        if (floorRoot != null)
        {
            floorRenderers = floorRoot.GetComponentsInChildren<Renderer>();
            floorMats = new Material[floorRenderers.Length];

            for (int i = 0; i < floorRenderers.Length; i++)
                floorMats[i] = floorRenderers[i].material;
        }

        currentBandWidth = baseBandWidth;
    }

    void Update()
    {
        if (!player || !platformRoot)
            return;

        // 🔴 ACTIVACIÓN SOLO SI ES LA MÁS CERCANA
        if (!IsClosestPlatform())
        {
            DisableVisuals();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= sampleInterval)
        {
            timer = 0f;
            AddSample();
        }

        float stdLateral = ComputeStd();

        // ================= BAND =================
        float targetWidth = baseBandWidth;

        if (forceAlwaysOn || stdLateral >= stdOn)
        {
            float t = Mathf.InverseLerp(stdOff, stdOn, stdLateral);
            targetWidth = Mathf.Lerp(maxBandWidth, minBandWidth, t);
            SetBandAlpha(1f);
        }
        else
        {
            SetBandAlpha(0f);
        }

        currentBandWidth = Mathf.Lerp(
            currentBandWidth,
            targetWidth,
            Time.deltaTime * widthLerp
        );

        UpdateBandTransformCentered(currentBandWidth);

        // ================= FLOOR INSTABILITY =================
        float instability01 = Mathf.InverseLerp(stdOff, stdOn, stdLateral);

        if (adaptiveEvaluator != null)
        {
            instability01 *= adaptiveEvaluator.MovementAssistWeight01;
        }

        currentInstability = Mathf.Lerp(
            currentInstability,
            instability01,
            Time.deltaTime * instabilityLerp
        );

        ApplyInstability(currentInstability);
    }

    // ================= CLOSEST PLATFORM =================

    bool IsClosestPlatform()
    {
        float myDist = DistanceToPlayer();

        for (int i = 0; i < allControllers.Count; i++)
        {
            var other = allControllers[i];
            if (other == this || other.player != player)
                continue;

            if (other.DistanceToPlayer() < myDist)
                return false;
        }
        return true;
    }

    float DistanceToPlayer()
    {
        Vector3 center =
            platformCollider != null
                ? platformCollider.transform.TransformPoint(platformCollider.center)
                : platformRoot.position;

        return Vector3.Distance(player.position, center);
    }

    void DisableVisuals()
    {
        SetBandAlpha(0f);
        ApplyInstability(0f);
    }

    void ApplyInstability(float value)
    {
        if (floorMats == null) return;

        float lateral = GetNormalizedLateral01(); // ← ESTE YA EXISTE

        for (int i = 0; i < floorMats.Length; i++)
        {
            if (floorMats[i] == null) continue;

            floorMats[i].SetFloat("_Instability", value);
            floorMats[i].SetFloat("_Lateral", lateral);
        }
    }


    // ================= SAMPLING =================

    void AddSample()
    {
        Vector3 localPos = platformRoot.InverseTransformPoint(player.position);

        samples.Add(new Sample
        {
            t = Time.time,
            lateralLocal = localPos.z
        });

        float minTime = Time.time - windowSeconds;
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].t < minTime)
                samples.RemoveAt(i);
        }
    }

    float ComputeStd()
    {
        int n = samples.Count;
        if (n <= 1) return 0f;

        float mean = 0f;
        for (int i = 0; i < n; i++)
            mean += samples[i].lateralLocal;

        mean /= n;

        float variance = 0f;
        for (int i = 0; i < n; i++)
        {
            float d = samples[i].lateralLocal - mean;
            variance += d * d;
        }

        variance /= (n - 1);
        return Mathf.Sqrt(variance);
    }

    // ================= VISUAL =================

    void UpdateBandTransformCentered(float width)
    {
        if (!bandVisual) return;

        bandVisual.localScale = new Vector3(width, platformLength, 1f);

        Vector3 center =
            platformCollider != null
                ? platformCollider.transform.TransformPoint(platformCollider.center)
                : platformRoot.position;

        bandVisual.position = center + platformRoot.up * yOffset;
        bandVisual.rotation = platformRoot.rotation * bandBaseRotation;
    }

    void SetBandAlpha(float a)
    {
        if (!bandMat) return;

        Color c = baseBandColor;
        c.a = a;
        bandMat.color = c;
    }

    public float GetInstability01()
    {
        return currentInstability;
    }
    public float GetSignedLateralOffset()
    {
        if (!platformRoot || !player) return 0f;

        Vector3 localPos = platformRoot.InverseTransformPoint(player.position);
        return localPos.z; // o .x según tu eje lateral
    }

    public float GetNormalizedLateral01()
    {
        if (!platformRoot || !player)
            return 0f;

        Vector3 localPos = platformRoot.InverseTransformPoint(player.position);

        float halfWidth = platformWidth * 0.5f;
        if (halfWidth <= 0.001f)
            return 0f;

        // 🔑 UNA SOLA inversión, aquí y solo aquí
        return Mathf.Clamp(-localPos.z / halfWidth, -1f, 1f);
    }


}
