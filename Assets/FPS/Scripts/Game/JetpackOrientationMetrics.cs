using System.IO;
using System.Globalization;
using UnityEngine;

public class JetpackOrientationMetrics : MonoBehaviour
{
    [Header("References")]
    public Camera PlayerCamera;             // Cámara del jugador
    public Transform CurrentTargetPlatform; // Plataforma objetivo actual

    [Header("Orientation Settings")]
    public float AwayAngleThreshold = 30f;  // Ángulo para considerar "mirando fuera"
    public float ReorientationAngleThreshold = 90f; // Cambios bruscos de orientación

    [Header("File Settings")]
    private string FileName = "";
    [Header("Logging")]
    public bool EnableLogging = false;  // ← se controla desde triggers
    public Transform CurrentTargetAnchor { get; private set; }

    [Header("Death cut-off")]
    public float SegmentEndHeight = 5f;

    public JetpackOrientationMetrics OrientationMetrics;
    public int AttemptID = 1;


    // Estado interno
    bool _isTracking = false;
    string _segmentId = "";
    string _targetPlatformName = "";
    float _totalDuration;
    float _sumAngles;
    int _angleSamples;

    float _timeLookingAway;
    int _reorientationCount;

    Vector3 _lastForward;
    bool _hasLastForward = false;
    bool _endedInDeath = false;

    string CsvSeparator = ";";

    // Ruta fija pedida por ti
    string BasePath = @"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\JetpackSystem\JetpackLogs\";

    void Awake()
    {
        if (PlayerCamera == null)
            PlayerCamera = Camera.main;

        // Crear carpeta si no existe
        if (!Directory.Exists(BasePath))
            Directory.CreateDirectory(BasePath);

        // Crear nombre único para un solo archivo por partida
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        FileName = $"JetpackOrientation_{timestamp}.csv";

        string fullPath = Path.Combine(BasePath, FileName);

        // Crear archivo y escribir encabezado UNA sola vez
        using (StreamWriter writer = new StreamWriter(fullPath, false))
        {
            writer.WriteLine(
                "AttemptID;SegmentId;TotalDuration_s;AverageAngle_deg;TimeLookingAway_s;PercentTimeLookingAway;ReorientationCount;EndedInDeath;TargetPlatform"
            );
        }

        Debug.Log("[JetpackMetrics] Created session log: " + fullPath);
    }


    void Update()
    {
        if (!EnableLogging)
            return;
        if (!_isTracking || CurrentTargetPlatform == null || PlayerCamera == null)
            return;

        if (transform.position.y < SegmentEndHeight)
        {
            //Debug.Log($"[ORIENT-P1] 🔥 CORTANDO segmento por caída libre → Y={transform.position.y:F2} < {SegmentEndHeight:F2}");

            MarkDeath();             // marcar que este salto terminó en muerte
            StopTrackingAndLog();    // cerrar el segmento YA MISMO
            return;                  // salir del Update
        }
        float dt = Time.deltaTime;
        _totalDuration += dt;

        // --- Ángulo entre cámara y plataforma ---
        Vector3 dirToTarget = CurrentTargetPlatform.position - PlayerCamera.transform.position;

        if (dirToTarget.sqrMagnitude > 0.0001f)
        {
            dirToTarget.Normalize();
            Vector3 camFwd = PlayerCamera.transform.forward;

            float angle = Vector3.Angle(camFwd, dirToTarget);
            _sumAngles += angle;
            _angleSamples++;
            /*
                        // --- DEBUG VISUAL: Ángulo en cada frame ---
                        Debug.Log(
                            $"[ANGLE] Target={CurrentTargetPlatform.name} | Angle={angle:F2}° | Threshold={AwayAngleThreshold}°"
                        );

                         --- DEBUG: ¿Estoy mirando dentro o fuera del objetivo? ---
                        if (angle > AwayAngleThreshold)
                        {
                            Debug.Log($"[AWAY] {angle:F1}° > {AwayAngleThreshold}°  → MIRANDO FUERA del objetivo");
                        }
                        else
                        {
                            Debug.Log($"[ON TARGET] {angle:F1}° ≤ {AwayAngleThreshold}° → MIRANDO A LA PLATAFORMA");
                        }

                        // --- DEBUG: tiempo acumulado mirando lejos ---
                        Debug.Log($"[AWAY TIME] acumulado: {_timeLookingAway:F2}s");
                        */
            // --- DEBUG: delta para reorientaciones ---
            if (_hasLastForward)
            {
                float deltaAngle = Vector3.Angle(_lastForward, camFwd);
                //sDebug.Log($"[DELTA] Cambio frame a frame: {deltaAngle:F2}°");
            }


            // Tiempo mirando fuera
            if (angle > AwayAngleThreshold)
                _timeLookingAway += dt;

            // Reorientación brusca
            if (_hasLastForward)
            {
                float deltaAngle = Vector3.Angle(_lastForward, camFwd);

                if (deltaAngle > ReorientationAngleThreshold)
                    _reorientationCount++;
            }

            _lastForward = camFwd;
            _hasLastForward = true;
        }
    }

    // ---------------------------------------------------------------
    // Inicio del tracking
    // ---------------------------------------------------------------
    public void StartTracking(string segmentId, Transform targetPlatform)
    {
        // Siempre actualizamos las referencias
        CurrentTargetAnchor = targetPlatform;
        CurrentTargetPlatform = targetPlatform;
        _targetPlatformName = (targetPlatform != null ? targetPlatform.name : "None");

        _segmentId = segmentId;

        // PERO solo evitamos loggear, no evitamos actualizar estado
        if (!EnableLogging)
        {
            Debug.Log("[JetpackMetrics] StartTracking: logging DESACTIVADO, pero sí se actualizó targetPlatform.");
            _isTracking = false;   // ← imprescindible
            return;
        }

        // Reset normal
        _isTracking = true;

        _totalDuration = 0f;
        _sumAngles = 0f;
        _angleSamples = 0;
        _timeLookingAway = 0f;
        _reorientationCount = 0;

        _hasLastForward = false;

        Debug.Log("[JetpackMetrics] START segment " + segmentId);
    }



    // ---------------------------------------------------------------
    // Fin del tracking
    // ---------------------------------------------------------------
    public void StopTrackingAndLog()
    {
        if (!_isTracking || !EnableLogging)
            return;

        _isTracking = false;

        if (_angleSamples == 0)
        {
            Debug.LogWarning("[JetpackMetrics] No data to log (no samples)");
            return;
        }

        float avgAngle = _sumAngles / _angleSamples;
        float percentAway = _timeLookingAway / _totalDuration;

        WriteRowToCsv(_segmentId, _totalDuration, avgAngle, _timeLookingAway, percentAway, _reorientationCount);

        Debug.Log("[JetpackMetrics] END segment " + _segmentId);
    }

    // ---------------------------------------------------------------
    // Escritura en CSV en TU carpeta fija
    // ---------------------------------------------------------------
    void WriteRowToCsv(string segmentId,
                       float totalDuration,
                       float avgAngle,
                       float timeAway,
                       float percentAway,
                       int reorientations)
    {
        string fullPath = Path.Combine(BasePath, FileName);

        bool exists = File.Exists(fullPath);

        using (StreamWriter writer = new StreamWriter(fullPath, true))
        {
            // Header si no existe el archivo
            if (!exists)
            {
                writer.WriteLine(
                    "AttemptID" + CsvSeparator +
                    "SegmentId" + CsvSeparator +
                    "TotalDuration_s" + CsvSeparator +
                    "AverageAngle_deg" + CsvSeparator +
                    "TimeLookingAway_s" + CsvSeparator +
                    "PercentTimeLookingAway" + CsvSeparator +
                    "ReorientationCount" + CsvSeparator +
                    "EndedInDeath" + CsvSeparator +
                    "PlatformID"
                );
            }

            var ci = CultureInfo.InvariantCulture;
            string ended = _endedInDeath ? "1" : "0";

            writer.WriteLine(
                AttemptID.ToString() + CsvSeparator +
                segmentId + CsvSeparator +
                totalDuration.ToString(ci) + CsvSeparator +
                avgAngle.ToString(ci) + CsvSeparator +
                timeAway.ToString(ci) + CsvSeparator +
                percentAway.ToString(ci) + CsvSeparator +
                reorientations + CsvSeparator +
                ended + CsvSeparator +
                _targetPlatformName
            );
            _endedInDeath = false;
        }

        Debug.Log("[JetpackMetrics] Logged to file: " + fullPath);
    }
    public void MarkDeath()
    {
        // Si NO está trackeando, evitar doble muerte (previene fila fantasma)
        if (!_isTracking)
            return;

        _endedInDeath = true;

        // Cerrar el segmento correctamente
        StopTrackingAndLog();

        // Reset para que el respawn no cree un segundo segmento muerto
        _isTracking = false;

        // Orientation NO usa flags de vuelo como otros pilares,
        // así que NO hay ningún _lastFlyingFlag que resetear.
    }



    public void ForceSetTargetAnchor(Transform newAnchor)
    {
        CurrentTargetPlatform = newAnchor;
        CurrentTargetAnchor = newAnchor;
        _targetPlatformName = newAnchor != null ? newAnchor.name : "None";
    }



}
