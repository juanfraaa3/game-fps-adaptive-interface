using System.Globalization;
using System.IO;
using UnityEngine;
using System.Collections.Generic;


public class MultitaskMetricsLogger : MonoBehaviour
{
    private HashSet<int> hitBarriers = new HashSet<int>();
    private HashSet<int> allBarriers = new HashSet<int>();

    [Header("Platform Paths")]
    public PlatformPathAdapter[] paths;
    private PlatformPathAdapter activePath;
    private float activePathDistance = float.MaxValue;

    public bool loggingActive = true;

    private int AttemptID = 1;
    private float attemptStartTime;
    private int LapID = 0;
    private float lapStartTime;
    private float devSum = 0f;
    private int devSamples = 0;

    private string fullPath;
    private CultureInfo CI = CultureInfo.InvariantCulture;
    // ---- Lap / Attempt state
    private int lapsCompleted = 0;

    // ---- Lap progress tracking
    private float lapProgressMax01 = 0f;
    private float lapProgress01 = 0f;
    private bool deathOccurred = false;
    private Vector3 _lastPos;
    private Vector3 _flatVel;
    private bool _hasLastPos = false;
    // ---- Speed variability
    private float speedSum = 0f;
    private float speedSqSum = 0f;
    private int speedSamples = 0;
    // ---- Micro-corrections
    private int microCorrectionsCount = 0;
    private Vector3 lastMoveDir = Vector3.zero;
    private bool hasLastMoveDir = false;
    // ---- Micro-correction thresholds
    private const float MIN_SPEED_FOR_MICRO = 0.3f;   // m/s
    private const float MIN_DIR_CHANGE_DEG = 8f;      // grados
    private const float MAX_DIR_CHANGE_DEG = 45f;     // grados
    private bool lapOpen = false;
    [Header("Lap Progress Normalization")]
    [Tooltip("Valor de GetProgress01 que representa una vuelta REAL completa")]
    public float lapCompletionFactor = 0.844f;


    void Start()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"Multitasking_{timestamp}.csv";

        string basePath = Path.Combine(
            Application.dataPath,
            "FPS/Scripts/MultitaskSystem"
        );

        Directory.CreateDirectory(basePath);

        fullPath = Path.Combine(basePath, fileName);

        if (!File.Exists(fullPath))
        {
            File.WriteAllText(
                fullPath,
                "AttemptID;LapID;LapDuration_s;AttemptDuration_s;LapProgress_0_1;DeathOccurred;MeanDeviation_m;SpeedSD_mps;MicroCorrectionsRate_per_s;ObstacleHitRate\n"
            );
        }

        // 🔴 CONTAR OBSTÁCULOS UNA SOLA VEZ

        StartAttempt();

        Debug.Log("CSV PATH: " + fullPath);
    }




    void Update()
    {
        if (!loggingActive)
            return;

        if (paths == null || paths.Length == 0)
            return;

        Vector3 pos = transform.position;

        // =====================================================
        // 0) SELECCIONAR PATH ACTIVO (plataforma más cercana)
        // =====================================================
        activePath = null;
        float bestDist = float.MaxValue;

        foreach (var p in paths)
        {
            if (p == null)
                continue;

            Vector3 closest = p.GetClosestPoint(pos);
            float d = Vector3.Distance(pos, closest);

            if (d < bestDist)
            {
                bestDist = d;
                activePath = p;
            }
        }

        if (activePath == null)
            return;

        /* =====================================================
         * 1) VELOCIDAD PLANA (XZ)
         * ===================================================== */
        if (_hasLastPos)
        {
            Vector3 delta = pos - _lastPos;
            delta.y = 0f;

            _flatVel = delta / Mathf.Max(Time.deltaTime, 0.0001f);
        }
        else
        {
            _flatVel = Vector3.zero;
            _hasLastPos = true;
        }

        _lastPos = pos;

        float speed = _flatVel.magnitude;

        // ---- Speed Variability accumulation
        speedSum += speed;
        speedSqSum += speed * speed;
        speedSamples++;

        /* =====================================================
         * 2) MICRO-CORRECTIONS DETECTION
         * ===================================================== */
        if (speed > MIN_SPEED_FOR_MICRO)
        {
            Vector3 dir = _flatVel.normalized;

            if (hasLastMoveDir)
            {
                float angle = Vector3.Angle(lastMoveDir, dir);

                if (angle > MIN_DIR_CHANGE_DEG && angle < MAX_DIR_CHANGE_DEG)
                {
                    microCorrectionsCount++;
                }
            }

            lastMoveDir = dir;
            hasLastMoveDir = true;
        }
        else
        {
            hasLastMoveDir = false;
        }

        /* =====================================================
         * 3) MEAN DEVIATION (path activo)
         * ===================================================== */
        Vector3 closestPoint = activePath.GetClosestPoint(pos);

        Vector3 flatPos = new Vector3(pos.x, 0f, pos.z);
        Vector3 flatClosest = new Vector3(closestPoint.x, 0f, closestPoint.z);

        float deviation = Vector3.Distance(flatPos, flatClosest);

        devSum += deviation;
        devSamples++;

        /* =====================================================
         * 4) PROGRESO DE LAP NORMALIZADO (0–1)
         * ===================================================== */
        float rawProgress = activePath.GetProgress01(pos);
        lapProgress01 = NormalizeLapProgress(rawProgress);

        if (lapOpen)
        {
            lapProgressMax01 = Mathf.Max(lapProgressMax01, lapProgress01);
        }
    }




    public void StartAttempt()
    {
        // ===============================
        // Attempt state (NO abrir laps)
        // ===============================
        attemptStartTime = Time.time;
        LapID = 0;
        lapOpen = false;

        // ===============================
        // RECONSTRUIR BARRERAS DEL INTENTO
        // ===============================
        hitBarriers.Clear();
        allBarriers.Clear();

        var reporters = FindObjectsOfType<ObstacleHitReporter>();

        foreach (var obs in reporters)
        {
            Vector3 barrierCenter =
                (obs.transform.parent != null &&
                 obs.transform.parent.name.Contains("JumpWallPlatforms"))
                    ? obs.transform.parent.position
                    : obs.transform.position;

            int barrierID = GetBarrierIDForPosition(barrierCenter);
            allBarriers.Add(barrierID);

            // Resetear estado del obstáculo para este intento
            obs.ResetForNewAttempt();
        }

        Debug.Log($"[MULTITASK START] totalBarriers = {allBarriers.Count}");
        Debug.Log($"[MULTITASK] Start Attempt {AttemptID}");

        // ===============================
        // Reset métricas ACUMULADAS
        // (NO abre ni cierra laps)
        // ===============================
        devSum = 0f;
        devSamples = 0;

        speedSum = 0f;
        speedSqSum = 0f;
        speedSamples = 0;

        microCorrectionsCount = 0;
        lastMoveDir = Vector3.zero;
        hasLastMoveDir = false;

        // ⚠️ Importante:
        // - lapStartTime se setea SOLO en OpenLap()
        // - ResetLapMetrics() se llama SOLO al abrir una lap
    }


    public void EndAttempt(bool died)
    {
        // Si hay una lap abierta, cerrarla
        if (lapOpen)
        {
            CloseLap(died);
        }

        AttemptID++;
        StartAttempt();
    }






    public Vector3 GetFlatVelocity()
    {
        return _flatVel;
    }

    // ======================================================
    // BARRIER IDENTIFICATION (AUTOMÁTICO POR PATH)
    // ======================================================

    public int GetBarrierIDForPosition(Vector3 worldPos)
    {
        if (activePath == null)
            return 0;

        var reporters = FindObjectsOfType<ObstacleHitReporter>();
        List<Vector3> centers = new List<Vector3>();

        foreach (var obs in reporters)
        {
            Vector3 center =
                (obs.transform.parent != null &&
                 obs.transform.parent.name.Contains("JumpWallPlatforms"))
                    ? obs.transform.parent.position
                    : obs.transform.position;

            if (!centers.Contains(center))
                centers.Add(center);
        }

        // Ordenar las barreras según su progreso EN EL PATH ACTIVO
        centers.Sort((a, b) =>
            activePath.GetProgress01(a).CompareTo(activePath.GetProgress01(b)));

        float p = activePath.GetProgress01(worldPos);

        for (int i = 0; i < centers.Count; i++)
        {
            float cp = activePath.GetProgress01(centers[i]);
            if (Mathf.Abs(cp - p) < 0.02f)
                return i;
        }

        // fallback seguro
        return Mathf.Clamp(
            Mathf.RoundToInt(p * centers.Count),
            0,
            centers.Count - 1
        );
    }



    public void NotifyBarrierHit(int barrierID)
    {
        hitBarriers.Add(barrierID);
    }

    private void OnApplicationQuit()
    {
        if (lapOpen)
        {
            CloseLap(false);
        }
    }


    private void EndLap(bool deathOccurred)
    {
        if (!lapOpen)
            return;

        lapOpen = false;

        float lapDuration = Time.time - lapStartTime;
        float attemptDuration = Time.time - attemptStartTime;

        float meanDev = devSamples > 0 ? devSum / devSamples : 0f;

        float speedSD = 0f;
        if (speedSamples > 1)
        {
            float meanSpeed = speedSum / speedSamples;
            float variance = (speedSqSum / speedSamples) - (meanSpeed * meanSpeed);
            speedSD = Mathf.Sqrt(Mathf.Max(variance, 0f));
        }

        float microRate = lapDuration > 0f
            ? microCorrectionsCount / lapDuration
            : 0f;

        int totalBarriers = allBarriers.Count;
        int hitCount = hitBarriers.Count;

        float obstacleHitRate = totalBarriers > 0
            ? (float)hitCount / totalBarriers
            : 0f;

        string line =
            AttemptID + ";" +
            LapID + ";" +
            lapDuration.ToString("F3", CI) + ";" +
            attemptDuration.ToString("F3", CI) + ";" +
            lapProgressMax01.ToString("F3", CI) + ";" +   // 🔴 NUEVO
            (deathOccurred ? 1 : 0) + ";" +
            meanDev.ToString("F4", CI) + ";" +
            speedSD.ToString("F4", CI) + ";" +
            microRate.ToString("F4", CI) + ";" +
            obstacleHitRate.ToString("F3", CI);

        File.AppendAllText(fullPath, line + "\n");

        Debug.Log($"[MULTITASK] Attempt {AttemptID} | Lap {LapID} | death={deathOccurred}");
    }
    private void ResetLapMetrics()
    {
        devSum = 0f;
        devSamples = 0;

        speedSum = 0f;
        speedSqSum = 0f;
        speedSamples = 0;

        microCorrectionsCount = 0;
        lastMoveDir = Vector3.zero;
        hasLastMoveDir = false;

        hitBarriers.Clear();

        lapProgress01 = 0f;
        lapProgressMax01 = 0f;   // 🔴 NUEVO
    }

    public void OpenLap()
    {
        // No abrir si ya hay una lap abierta
        if (lapOpen)
            return;

        LapID++;
        lapOpen = true;

        lapStartTime = Time.time;
        ResetLapMetrics();

        Debug.Log($"[MULTITASK] Lap {LapID} STARTED");
    }
    public void CloseLap(bool deathOccurred)
    {
        if (!lapOpen)
            return;

        EndLap(deathOccurred);
        lapOpen = false;
    }

    private float NormalizeLapProgress(float rawProgress01)
    {
        if (lapCompletionFactor <= 0f)
            return rawProgress01;

        return Mathf.Clamp01(rawProgress01 / lapCompletionFactor);
    }
    private void UpdateActivePath(Vector3 playerPos)
    {
        activePath = null;
        activePathDistance = float.MaxValue;

        foreach (var p in paths)
        {
            if (p == null)
                continue;

            Vector3 closest = p.GetClosestPoint(playerPos);
            float dist = Vector3.Distance(playerPos, closest);

            if (dist < activePathDistance)
            {
                activePathDistance = dist;
                activePath = p;
            }
        }
    }

}
