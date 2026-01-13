using UnityEngine;
using Unity.FPS.Gameplay;
using System.Collections.Generic;
using System.Globalization;

public class MovingPlatformRelativeMetrics : MonoBehaviour
{
    public int PlatformID;
    public int SegmentID;
    public PlayerCharacterController player;

    List<Vector3> relativePositions = new List<Vector3>();
    // --- Micro-corrections (conteo bruto) ---
    private float lastMag = -1f;
    private float lastDelta = 0f;
    private int rawCorrectionsCount = 0;

    float timeOnPlatform = 0f;

    bool playerOn = false;
    bool hasLogged = false;

    [Header("Risk & Correction Metrics")]
    public float correctionPeakThreshold = 0.5f; // ajustable

    float edgeRiskTime = 0f;
    int correctionPeaks = 0;

    float lastRelativeMagnitude = -1f;
    [SerializeField] private Transform referenceTransform;
    private Transform RefT => referenceTransform != null ? referenceTransform : transform;
    [SerializeField] private BoxCollider platformCollider;

    void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<PlayerCharacterController>()) return;

        playerOn = true;
        hasLogged = false;
        relativePositions.Clear();
        rawCorrectionsCount = 0;
        lastMag = -1f;
        lastDelta = 0f;
        timeOnPlatform = 0f;
        edgeRiskTime = 0f;
        correctionPeaks = 0;
        lastRelativeMagnitude = -1f;

    }

    void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerCharacterController>()) return;
        if (!playerOn || hasLogged) return;

        playerOn = false;
        hasLogged = true;
        LogMetrics();
    }


    void Update()
    {
        if (!playerOn) return;

        Vector3 center = GetPlatformCenterWorld();
        Vector3 rel = player.transform.position - center;

        // 🔥 ignorar altura: solo plano horizontal
        rel.y = 0f;
        // 🔥 ESTA LÍNEA FALTABA
        relativePositions.Add(rel);
        float mag = rel.magnitude;
        // --- Conteo bruto de micro-correcciones ---
        if (lastMag >= 0f)
        {
            float delta = mag - lastMag;

            const float EPS = 0.05f; // sensibilidad mínima (ruido)

            if (Mathf.Abs(delta) > EPS)
            {
                // cambio de tendencia real
                if (Mathf.Sign(delta) != Mathf.Sign(lastDelta))
                {
                    rawCorrectionsCount++;
                }

                lastDelta = delta;
            }
        }

        lastMag = mag;


        timeOnPlatform += Time.deltaTime;

        // ---------- CorrectionPeaks ----------
        if (lastRelativeMagnitude >= 0f)
        {
            float delta = Mathf.Abs(mag - lastRelativeMagnitude);
            if (delta > correctionPeakThreshold)
            {
                correctionPeaks++;
            }
        }
        lastRelativeMagnitude = mag;

        // ---------- EdgeRiskTime ----------
        if (relativePositions.Count > 5)
        {
            float mean = 0f;
            foreach (var p in relativePositions)
                mean += p.magnitude;
            mean /= relativePositions.Count;

            float variance = 0f;
            foreach (var p in relativePositions)
            {
                float d = p.magnitude - mean;
                variance += d * d;
            }
            float std = Mathf.Sqrt(variance / relativePositions.Count);

            if (mag > mean + std)
            {
                edgeRiskTime += Time.deltaTime;
            }
        }
    }


    void LogMetrics()
    {
        if (relativePositions.Count == 0)
            return;

        int n = relativePositions.Count;

        float sum = 0f;
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (var p in relativePositions)
        {
            float mag = p.magnitude;
            sum += mag;
            if (mag < min) min = mag;
            if (mag > max) max = mag;
        }

        float mean = sum / n;

        float variance = 0f;
        foreach (var p in relativePositions)
        {
            float diff = p.magnitude - mean;
            variance += diff * diff;
        }

        float std = Mathf.Sqrt(variance / n);

        string header =
            "EventType;SegmentID;ElementID;" +
            "TimeOnPlatform;RelativePositionStd;RelativePositionMean;" +
            "RelativePositionMax;RelativePositionMin;SampleCount;" +
            "EdgeRiskTime;CorrectionPeaks;RawCorrectionsCount;";

        string line = string.Format(
    CultureInfo.InvariantCulture,
    "Platform;{0};{1};{2:F2};{3:F4};{4:F4};{5:F4};{6:F4};{7};{8:F2};{9};{10}",
            SegmentID,
            PlatformID,
            timeOnPlatform,
            std,
            mean,
            max,
            min,
            n,
            edgeRiskTime,
            correctionPeaks,
            rawCorrectionsCount
        );



        CSVMetricWriter.WriteLine(
            "MovementMetrics",
            header,
            line
        );
    }
    private Vector3 GetPlatformCenterWorld()
    {
        if (platformCollider != null)
            return platformCollider.bounds.center;

        // Fallback seguro: si no asignaste collider, usa transform.position (no revienta)
        return transform.position;
    }



}
