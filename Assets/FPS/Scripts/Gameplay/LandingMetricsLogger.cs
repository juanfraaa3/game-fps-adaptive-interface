using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

public class LandingMetricsLogger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player character controller (FPS Microgame)")]
    public PlayerCharacterController PlayerController;

    [Tooltip("Jetpack script para leer estado de segmento de vuelo")]
    public Jetpack JetpackScript;

    [Tooltip("Métricas de orientación para obtener el TargetAnchor dinámico")]
    public JetpackOrientationMetrics OrientationMetrics;

    [Header("Landing Settings")]
    [Tooltip("Ventana (en segundos) antes del impacto para analizar microcorrecciones")]
    public float PreLandingWindow = 0.5f;

    [Tooltip("Altura mínima; si cae por debajo de esto durante el segmento → se considera caída")]
    public float SegmentEndHeight = 5f;

    [Tooltip("Umbral de velocidad horizontal para considerar que el jugador está estabilizado")]
    public float StabilizedHorizontalSpeedThreshold = 0.5f;

    [Tooltip("Tiempo mínimo con velocidad baja para considerar que se estabilizó")]
    public float StabilizedMinDuration = 0.4f;

    [Tooltip("Duración máxima del tracking post-landing (por seguridad)")]
    public float MaxPostLandingDuration = 2.0f;

    [Tooltip("Umbral angular (en grados) para contar una microcorrección en la ventana pre-landing")]
    public float MicroCorrectionAngleThreshold = 12f;

    [Tooltip("Mínima magnitud de velocidad horizontal para considerar dirección válida")]
    public float MinHorizontalSpeedForDirection = 0.5f;

    [Header("CSV Output")]
    public string OutputDirectory = @"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\LandingSystem";
    public string FileBaseName = "LandingMetrics";
    public char CsvSeparator = ';';
    [Tooltip("Si está desactivado, igual se calculan métricas pero no se escribe el CSV")]
    public bool EnableLogging = false;
    public int AttemptID = 1;



    // --------------------------------------------------------------------
    // ESTADO INTERNO
    // --------------------------------------------------------------------
    string _filePath;
    bool _fileInitialized = false;

    int _segmentCounter = 0;

    bool _lastGrounded = true;
    bool _lastFlyingFlag = false;

    bool _segmentActive = false;          // hay un vuelo actual (pilar 3)
    bool _hasLanded = false;              // ya hubo impacto con suelo/plataforma
    bool _postLandingTracking = false;    // estamos midiendo estabilización
    Vector3 _lastAirPosition;
    bool _suppressNextSegment = false; // Evita crear segmentos fantasma inmediatamente después de caídas reales

    float _segmentStartTime;
    float _landingTime;

    Vector3 _landingPosition;             // posición al primer contacto
    Vector3 _landingVelocity;             // velocidad al primer contacto

    float _postLandingStartTime;
    float _postStabilizationTime;         // tiempo desde landing hasta estabilizar
    bool _stabilized = false;
    float _stableTimer = 0f;

    float _postLandingDrift;              // distancia total recorrida sobre la plataforma
    bool _postLandingFall = false;        // 1 si se cae después de aterrizar
    bool _landingFailed = false;          // 1 si nunca aterriza en plataforma objetivo o se cae
                                          // Última altura Y registrada para calcular velocidad vertical post-landing
    float _lastY;
    private float _lastAirVerticalSpeed = 0f;         // velocidad vertical mientras está en el aire
    private float _verticalSpeedAtImpact = 0f;        // velocidad final capturada en el frame antes del aterrizaje

    // buffer circular de la ventana pre-landing
    struct Sample
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;
    }

    List<Sample> _preLandingSamples = new List<Sample>();

    void Awake()
    {
        if (PlayerController == null)
            PlayerController = GetComponent<PlayerCharacterController>();

        if (JetpackScript == null)
            JetpackScript = GetComponent<Jetpack>();

        if (!_fileInitialized)
            InitializeFile();
    }

    void Update()
    {
        if (PlayerController == null || JetpackScript == null)
            return;

        if (!EnableLogging)
        {
            _segmentActive = false;
            _hasLanded = false;
            _postLandingTracking = false;
            _preLandingSamples.Clear();
            _stableTimer = 0f;
            _stabilized = false;

            _lastGrounded = PlayerController.IsGrounded;
            _lastFlyingFlag = false;
            return;
        }

        bool grounded = PlayerController.IsGrounded;
        bool flyingFlag = GetFlyingFlag();
        float now = Time.time;

        // ------------------------------------------------------------
        // A) INICIO DEL SEGMENTO
        // ------------------------------------------------------------
        if (flyingFlag && !_lastFlyingFlag)
        {
            StartNewSegment(now);
        }

        // ------------------------------------------------------------
        // B) EN VUELO: registrar muestras y última posición válida
        // ------------------------------------------------------------
        if (_segmentActive && !grounded)
        {
            _lastAirVerticalSpeed = PlayerController.LastVerticalSpeedBeforeGrounding;

            //Debug.Log($"[AIR] verticalSpeed = {_lastAirVerticalSpeed}");

            RecordPreLandingSample(now);

            // Guardamos última posición válida en aire, para fallos sin impacto
            _lastAirPosition = transform.position;
        }

        // ------------------------------------------------------------
        // C) FALLA SIN IMPACTO (cruzó Y = 0 pero NO aterrizó)
        // ------------------------------------------------------------
        if (_segmentActive && !_hasLanded && transform.position.y <= 0f)
        {
            Debug.Log("[LAND] FAIL @Y=0 (no hubo impacto)");

            // Usamos la última posición aérea válida como “punto de impacto”
            _landingPosition = _lastAirPosition;

            _landingFailed = true;
            _postLandingFall = true;

            EndSegmentAndLog("Missed_NoImpact");
            ResetStateAfterSegment();

            // Evita que el respawn genere otro segmento falso
            _suppressNextSegment = true;

            _lastGrounded = grounded;
            _lastFlyingFlag = flyingFlag;
            return;
        }

        // ------------------------------------------------------------
        // D) FALLA PROFUNDA (se cayó más abajo del límite permitido)
        //    → SegmentEndHeight maneja caídas reales al vacío
        // ------------------------------------------------------------
        if (_segmentActive && !_hasLanded && transform.position.y < SegmentEndHeight)
        {
            Debug.Log("[LAND] FAIL below SegmentEndHeight (fall real)");
            _landingPosition = _lastAirPosition;
            _landingFailed = true;
            _postLandingFall = true;

            EndSegmentAndLog("NoLanding_Fall");
            ResetStateAfterSegment();
            _suppressNextSegment = true;

            _lastGrounded = grounded;
            _lastFlyingFlag = flyingFlag;
            return;
        }

        // ------------------------------------------------------------
        // E) DETECTAR ATERRIZAJE REAL
        // ------------------------------------------------------------
        if (_segmentActive && !_hasLanded && grounded)
        {
            OnLanding(now);
        }

        // ------------------------------------------------------------
        // F) POST-LANDING: estabilización o caída posterior
        // ------------------------------------------------------------
        if (_segmentActive && _hasLanded && _postLandingTracking)
        {
            UpdatePostLanding(now, grounded);
        }

        // ------------------------------------------------------------
        // G) GUARDAR ESTADOS
        // ------------------------------------------------------------
        _lastGrounded = grounded;
        _lastFlyingFlag = flyingFlag;
    }


    // --------------------------------------------------------------------
    //  LECTURA DEL FLAG DE VUELO DESDE JETPACK (como en TrajectoryLogger)
    // --------------------------------------------------------------------
    bool GetFlyingFlag()
    {
        if (JetpackScript == null)
            return false;

        var field = typeof(Jetpack).GetField("m_IsFlyingSegment",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
            return (bool)field.GetValue(JetpackScript);

        return false;
    }

    // --------------------------------------------------------------------
    //  INICIO DE SEGMENTO
    // --------------------------------------------------------------------
    void StartNewSegment(float now)
    {
        // ❌ Si venimos de una caída o resbalón, NO iniciar segmento nuevo
        if (_suppressNextSegment)
        {
            //Debug.Log("[LANDING] Segmento bloqueado por _suppressNextSegment (evita fantasma)");
            _suppressNextSegment = false;   // se consume el bloqueo
            return;
        }

        _segmentActive = true;
        _hasLanded = false;
        _postLandingTracking = false;
        _postLandingFall = false;
        _landingFailed = false;

        _preLandingSamples.Clear();

        _postLandingDrift = 0f;
        _postStabilizationTime = 0f;
        _stabilized = false;
        _stableTimer = 0f;

        _segmentStartTime = now;
    }

    // --------------------------------------------------------------------
    //  REGISTRO DE MUESTRAS PRE-LANDING
    // --------------------------------------------------------------------
    void RecordPreLandingSample(float now)
    {
        Sample s = new Sample
        {
            time = now,
            position = transform.position,
            velocity = PlayerController.CharacterVelocity
        };
        _preLandingSamples.Add(s);

        // mantener sólo la ventana PreLandingWindow
        float minTime = now - PreLandingWindow;
        while (_preLandingSamples.Count > 0 && _preLandingSamples[0].time < minTime)
        {
            _preLandingSamples.RemoveAt(0);
        }
    }

    // --------------------------------------------------------------------
    //  ATERRIZAJE
    // --------------------------------------------------------------------
    void OnLanding(float now)
    {
        // 1) capturar la velocidad REAL previa al impacto
        _verticalSpeedAtImpact = _lastAirVerticalSpeed;

        // 🔥 LOG: velocidad registrada al momento del impacto
        // Debug.Log($"[LAND] impact vertical speed = {_verticalSpeedAtImpact}");

        // 2) marcar aterrizaje
        _hasLanded = true;
        _landingTime = now;
        _landingPosition = transform.position;

        // ❗ MUY IMPORTANTE: NO leer aquí CharacterController.velocity
        // porque ya fue clamped por Unity.
        // ❌ _landingVelocity = PlayerController.CharacterVelocity;

        // 3) iniciar fase post-landing
        _postLandingStartTime = now;
        _postLandingTracking = true;
        _suppressNextSegment = false;
    }



    // --------------------------------------------------------------------
    //  POST-LANDING: ESTABILIZACIÓN O CAÍDA
    // --------------------------------------------------------------------
    void UpdatePostLanding(float now, bool grounded)
    {
        // ------------------------------------------------------------
        // 1. Medir drift horizontal desde el punto de aterrizaje
        // ------------------------------------------------------------
        Vector3 horizVel = PlayerController.CharacterVelocity;
        horizVel.y = 0f;
        float horizSpeed = horizVel.magnitude;

        Vector3 landingPosXZ = new Vector3(_landingPosition.x, 0f, _landingPosition.z);
        Vector3 currentPosXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        float drift = Vector3.Distance(landingPosXZ, currentPosXZ);
        if (drift > _postLandingDrift)
            _postLandingDrift = drift;

        // ------------------------------------------------------------
        // 2. DETECCIÓN ROBUSTA DE CAÍDA DESPUÉS DEL ATERRIZAJE
        // ------------------------------------------------------------
        if (_hasLanded)
        {
            float timeSinceLanding = now - _landingTime;

            // Parámetros ajustables
            float gracePeriod = 0.20f;          // evita falsos positivos justo al aterrizar
            float postLandingWindow = 2.0f;     // cuánto tiempo consideramos "post-landing"
            float fallSpeedThreshold = 0.6f;    // velocidad vertical negativa mínima (caída real)
            float heightDropThreshold = 0.15f;  // distancia mínima bajo la altura de aterrizaje

            // Solo evaluamos caída dentro de la ventana válida
            if (timeSinceLanding >= gracePeriod && timeSinceLanding <= postLandingWindow)
            {
                float currentY = transform.position.y;
                float verticalSpeed = (currentY - _lastY) / Time.deltaTime;

                float landingHeight = _landingPosition.y;
                bool fallingFast = verticalSpeed < -fallSpeedThreshold;
                bool belowSurface = currentY < (landingHeight - heightDropThreshold);

                if (fallingFast && belowSurface)
                {
                    /*
                    Debug.Log(
                        $"[LANDING][PostLandingFall] ACTIVADO\n" +
                        $"→ Motivo: caída REAL después de aterrizar\n" +
                        $"    fallingFast = {fallingFast} (verticalSpeed={verticalSpeed:F3})\n" +
                        $"    belowSurface = {belowSurface} (currentY={currentY:F3}, landingHeight={landingHeight:F3})\n" +
                        $"    timeSinceLanding = {timeSinceLanding:F3}s\n" +
                        $"    gracePeriod = 0.20s | postLandingWindow = 2.0s\n" +
                        $"    DriftActual = {_postLandingDrift:F3}m"
                    );
                    */

                    _postLandingFall = true;
                    _landingFailed = true;
                    EndSegmentAndLog("HitThenFall");
                    ResetStateAfterSegment();
                    _suppressNextSegment = true;
                    return;
                }

            }
        }

        // ------------------------------------------------------------
        // 3. DETECCIÓN DE ESTABILIZACIÓN DESPUÉS DE ATERRIZAR
        // ------------------------------------------------------------
        if (grounded && horizSpeed < StabilizedHorizontalSpeedThreshold)
        {
            _stableTimer += Time.deltaTime;

            if (!_stabilized && _stableTimer >= StabilizedMinDuration)
            {
                _stabilized = true;
                _postStabilizationTime = now - _landingTime;

                /*
                Debug.Log(
                    $"[LANDING][CLEAN LANDING]\n" +
                    $"→ Aterrizaje estable detectado\n" +
                    $"→ Tiempo hasta estabilizar: {_postStabilizationTime:F3}s\n" +
                    $"→ Drift final: {_postLandingDrift:F3}m\n" +
                    $"→ Velocidad horizontal actual: {horizSpeed:F3} m/s\n" +
                    $"→ Grounded = {grounded}\n" +
                    $"→ LandingPosition = {_landingPosition}\n"
                );
                */
                EndSegmentAndLog("CleanOrEdge");
                ResetStateAfterSegment();
                return;
            }

        }
        else
        {
            // Si el jugador vuelve a moverse rápido, reinicia el contador
            _stableTimer = 0f;
        }

        // ------------------------------------------------------------
        // 4. TIMEOUT DE SEGURIDAD (NO SE QUEDÓ ESTABLE NI SE CAYÓ)
        // ------------------------------------------------------------
        // Seguridad: si pasa demasiado tiempo post-landing, cerramos igual
        if (now - _postLandingStartTime >= MaxPostLandingDuration)
        {
            /*
            Debug.Log(
                $"[LANDING][LandingFailed - TIMEOUT]\n" +
                $"→ Razón: No hubo estabilización ni caída dentro de {MaxPostLandingDuration} segundos.\n" +
                $"→ TiempoTranscurrido = {now - _postLandingStartTime:F3}s\n" +
                $"→ DriftFinal = {_postLandingDrift:F3}m\n" +
                $"→ Grounded = {grounded}\n" +
                $"→ PostLandingFall = {_postLandingFall}\n" +
                $"→ HasLanded = {_hasLanded}\n"
            );
            */
            if (!_stabilized)
            {
                _postStabilizationTime = now - _landingTime;
            }

            EndSegmentAndLog("Timeout");
            ResetStateAfterSegment();
            return;
        }


        // ------------------------------------------------------------
        // 5. Actualizar última altura para detección de velocidad vertical
        // ------------------------------------------------------------
        _lastY = transform.position.y;
    }


    // --------------------------------------------------------------------
    //  FIN DEL SEGMENTO Y CÁLCULO DE TODAS LAS MÉTRICAS
    // --------------------------------------------------------------------
    void EndSegmentAndLog(string provisionalLandingType)
    {
        _segmentCounter++;
        string segmentId = "LandingSegment_" + _segmentCounter;

        // ---------------- IMPACTO ----------------
        Transform targetAnchor = OrientationMetrics != null
            ? OrientationMetrics.CurrentTargetAnchor
            : null;

        string targetName = targetAnchor ? targetAnchor.name : "None";

        Vector3 landingPosXZ = new Vector3(_landingPosition.x, 0f, _landingPosition.z);

        float landingOffset = 0f;
        float offsetForward = 0f;
        float offsetSide = 0f;
        float safetyMargin = 0f;

        if (targetAnchor != null)
        {
            Vector3 targetPosXZ = new Vector3(targetAnchor.position.x, 0f, targetAnchor.position.z);
            Vector3 offsetVec = landingPosXZ - targetPosXZ;
            landingOffset = offsetVec.magnitude;

            // definir ejes forward/side usando la orientación de la plataforma (o world forward)
            Vector3 fwd = targetAnchor.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 side = new Vector3(fwd.z, 0f, -fwd.x); // perpendicular en el plano XZ

            offsetForward = Vector3.Dot(offsetVec, fwd);
            offsetSide = Vector3.Dot(offsetVec, side);

            // SafetyMargin basado en el collider real de la plataforma
            Collider col = targetAnchor.GetComponent<Collider>();

            // Si el anchor no tiene collider, buscar en el parent y sus hijos (PlatformSurface_X)
            if (col == null && targetAnchor.parent != null)
            {
                col = targetAnchor.parent.GetComponentInChildren<Collider>();
            }

            if (col != null)
            {
                Vector3 centerXZ = new Vector3(col.bounds.center.x, 0f, col.bounds.center.z);
                Vector3 ext = col.bounds.extents;

                // radio aproximado de la plataforma (en plano XZ)
                float radiusApprox = Mathf.Max(ext.x, ext.z);

                float distCenter = Vector3.Distance(landingPosXZ, centerXZ);
                safetyMargin = Mathf.Max(0f, radiusApprox - distCenter);
            }
            else
            {
                // fallback de seguridad si la plataforma no tiene collider (no debería pasar)
                safetyMargin = 0f;
            }

        }

        // velocidad vertical al impacto
        float verticalSpeed = _verticalSpeedAtImpact;

        // ApproachAngle: ángulo entre velocidad y plano horizontal
        Vector3 v = _landingVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);
        float approachAngle = 0f;
        if (v.sqrMagnitude > 0.0001f && horiz.sqrMagnitude > 0.0001f)
        {
            approachAngle = Vector3.Angle(v, horiz);
        }

        // ---------------- PRE-LANDING ----------------
        int microCorrections = 0;
        float preLandingJitter = 0f;

        if (_preLandingSamples.Count >= 2)
        {
            Vector3? lastDir = null;
            float jitterAngleSum = 0f;
            int jitterCount = 0;

            for (int i = 1; i < _preLandingSamples.Count; i++)
            {
                Vector3 v0 = _preLandingSamples[i].velocity;
                v0.y = 0f;
                float mag = v0.magnitude;
                if (mag < MinHorizontalSpeedForDirection)
                    continue;

                Vector3 dir = v0 / mag;

                if (lastDir.HasValue)
                {
                    float angle = Vector3.Angle(lastDir.Value, dir);
                    if (angle > MicroCorrectionAngleThreshold)
                        microCorrections++;

                    jitterAngleSum += angle;
                    jitterCount++;
                }

                lastDir = dir;
            }

            if (jitterCount > 0)
                preLandingJitter = jitterAngleSum / jitterCount;
        }

        // ---------------- POST-LANDING ----------------
        float postStabTime = _postStabilizationTime;
        float postDrift = _postLandingDrift;
        int postFall = _postLandingFall ? 1 : 0;

        // ---------------- OUTCOME GLOBAL ----------------
        // LandingFailed: no aterrizó en la plataforma objetivo o terminó en caída
        bool landedOnTarget = (targetAnchor != null);

        // si hay target y el offset es mayor que el radio aprox (cuando lo calculamos),
        // lo consideramos fuera.
        if (landedOnTarget && safetyMargin <= 0.0001f)
        {
            landedOnTarget = false;
        }

        int landingFailed = (!_hasLanded || !landedOnTarget || _postLandingFall || _landingFailed) ? 1 : 0;

        string landingType = ClassifyLanding(
            landedOnTarget,
            landingOffset,
            safetyMargin,
            postFall == 1,
            provisionalLandingType,
            offsetForward,
            offsetSide,
            targetName
        );
        var evaluator = GetComponent<LandingAdaptiveEvaluator>();
        if (evaluator != null)
        {
            evaluator.Evaluate(
                landingOffset,
                postDrift,
                verticalSpeed
            );
        }


        // ---------------- LOG CSV ----------------
        if (EnableLogging)
        {
            WriteRowToCsv(
                segmentId,
                targetName,
                landingOffset,
                offsetForward,
                offsetSide,
                safetyMargin,
                verticalSpeed,
                approachAngle,
                microCorrections,
                preLandingJitter,
                postStabTime,
                postDrift,
                landingFailed,
                postFall,
                landingType
            );
        }
    }

    void ResetStateAfterSegment()
    {
        _segmentActive = false;
        _hasLanded = false;
        _postLandingTracking = false;
        _preLandingSamples.Clear();
        _stableTimer = 0f;
        _stabilized = false;
    }

    // --------------------------------------------------------------------
    //  CLASIFICADOR CUALITATIVO DEL LANDING
    // --------------------------------------------------------------------
    string ClassifyLanding(
    bool landedOnTarget,
    float landingOffset,
    float safetyMargin,
    bool postFall,
    string provisionalType,
    float offsetForward,
    float offsetSide,
    string targetName)
    {
        // ------------------------------------------------------------
        // 1. CASO: NO aterrizó bien en la plataforma objetivo
        // ------------------------------------------------------------
        // 1. CASO: NO aterrizó en la plataforma objetivo
        if (!landedOnTarget)
        {
            // Si se cayó, clasificamos usando forward / side
            if (postFall)
            {
                // === Regla simple: SIDE domina sobre FORWARD si es claramente lateral ===
                if (Mathf.Abs(offsetSide) >= Mathf.Abs(offsetForward))
                {
                    if (Mathf.Abs(offsetSide) < 0.3f)
                        return "SideFall"; // No se puede determinar

                    return (offsetSide < 0f) ? "RightSideFall" : "LeftSideFall";
                }

                // === Sino, usar forward ===
                if (Mathf.Abs(offsetForward) < 0.3f)
                    return "ForwardFall"; // indeterminado

                return (offsetForward > 0f) ? "UnderShoot" : "OverShoot";
            }

            // No cayó, pero no aterrizó bien
            return "MissedPlatform_Standing";
        }


        // ------------------------------------------------------------
        // 2. CASO: Aterrizó en la plataforma objetivo
        // ------------------------------------------------------------
        if (postFall)
        {
            return "HitThenFall";
        }

        // Borde vs limpio
        if (safetyMargin < 0.3f)
            return "EdgeLanding";

        return "CleanLanding";
    }


    // --------------------------------------------------------------------
    //  CSV
    // --------------------------------------------------------------------
    void InitializeFile()
    {
        if (!Directory.Exists(OutputDirectory))
            Directory.CreateDirectory(OutputDirectory);

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"{FileBaseName}_{timestamp}.csv";

        _filePath = Path.Combine(OutputDirectory, fileName);

        using (var w = new StreamWriter(_filePath, false))
        {
            string header = string.Join(CsvSeparator.ToString(), new[]
            {
                "AttemptID",
                "SegmentId",
                "TargetPlatform",
                "LandingOffset_m",
                "LandingOffset_Forward_m",
                "LandingOffset_Side_m",
                "SafetyMargin_m",
                "VerticalSpeed_mps",
                "ApproachAngle_deg",
                "MicroCorrections_0_5s",
                "PreLandingHorizontalJitter",
                "PostStabilizationTime_s",
                "PostLandingDrift_m",
                "LandingFailed",
                "PostLandingFall",
                "LandingType"
            });
            w.WriteLine(header);
        }

        _fileInitialized = true;
    }

    void WriteRowToCsv(
        string segmentId,
        string targetPlatform,
        float landingOffset,
        float offsetForward,
        float offsetSide,
        float safetyMargin,
        float verticalSpeed,
        float approachAngle,
        int microCorrections,
        float preLandingJitter,
        float postStabTime,
        float postDrift,
        int landingFailed,
        int postFall,
        string landingType)
    {
        using (var w = new StreamWriter(_filePath, true))
        {
            var c = CultureInfo.InvariantCulture;

            string line = string.Join(CsvSeparator.ToString(), new[]
            {
                AttemptID.ToString(),
                segmentId,
                targetPlatform,
                landingOffset.ToString(c),
                offsetForward.ToString(c),
                offsetSide.ToString(c),
                safetyMargin.ToString(c),
                verticalSpeed.ToString(c),
                approachAngle.ToString(c),
                microCorrections.ToString(),
                preLandingJitter.ToString(c),
                postStabTime.ToString(c),
                postDrift.ToString(c),
                landingFailed.ToString(),
                postFall.ToString(),
                landingType
            });

            w.WriteLine(line);
        }
    }
    public void ForceClearSuppressFlag()
    {
        _suppressNextSegment = false;
    }

}
