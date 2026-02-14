using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class JetpackTrajectoryLogger : MonoBehaviour
    {
        public static float LastRespawnTime = -999f;
        [Header("References")]
        [Tooltip("Referencia al script Jetpack (el tuyo actual)")]
        public Jetpack JetpackScript; // ← ESTE es tu Jetpack.cs real

        [Tooltip("Referencia al sistema de orientación para obtener el TargetAnchor dinámico")]
        public JetpackOrientationMetrics OrientationMetrics;


        [Header("CSV Output")]
        public string OutputDirectory = @"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\JetpackSystem\JetpackLogs";

        public string FileBaseName = "JetpackTrajectory";
        public char CsvSeparator = ';';
        [Header("Death cut-off")]
        public float SegmentEndHeight = 5f;
        public int AttemptID = 1;    // inicia en 1 por claridad

        [Header("Activation Control")]
        public bool EnableLogging = false;



        // Estado interno
        bool _fileInitialized = false;
        string _filePath;

        bool _lastFlyingState = false;
        bool _isTracking = false;
        bool _endedInDeath = false;

        float _startTime;
        Vector3 _startPos;
        Vector3 _endPos;

        List<Vector3> _positions = new List<Vector3>();

        // Métricas
        float _totalDistance;
        float _verticalOscillation;
        float _usefulDistance;
        float _uselessDistance;
        float _zigzagAngleSum;
        int _zigzagCount;
        float _maxLateralOffset;

        void Awake()
        {
            InitializeFile();
        }

        void Update()
        {
            if (!EnableLogging)
            {
                _isTracking = false;
                _lastFlyingState = false;
                return;
            }

            bool isFlying = GetFlyingFlag();

            // INICIO del segmento
            if (isFlying && !_lastFlyingState)
                StartTracking();

            // Durante vuelo
            if (isFlying && _isTracking)
                TrackPosition();

            if (_isTracking && transform.position.y < SegmentEndHeight)
            {
                //Debug.Log($"[TRAJ-P2] 🔥 CORTANDO segmento por caída → Y={transform.position.y:F2} < {SegmentEndHeight:F2}");

                _endedInDeath = true;     // este salto terminó en caída
                StopAndCompute();         // cerrar el segmento inmediatamente
                Debug.Log("[TRAJ-P2] 🚫 Tracking detenido por caída libre");
                return;
            }

            // FIN del segmento
            if (!isFlying && _lastFlyingState && _isTracking)
                StopAndCompute();

            _lastFlyingState = isFlying;
        }

        // --------------------------------------------------------------------
        //  LECTURA DEL FLAG REAL DEL JETPACK: m_IsFlyingSegment
        // --------------------------------------------------------------------
        bool GetFlyingFlag()
        {
            if (JetpackScript == null)
                return false;

            // Accedemos al campo private "m_IsFlyingSegment" por reflection
            var field = typeof(Jetpack).GetField("m_IsFlyingSegment",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
                return (bool)field.GetValue(JetpackScript);

            return false;
        }

        // --------------------------------------------------------------------
        //  INICIO DE TRACKING
        // --------------------------------------------------------------------
        void StartTracking()
        {
            // -------------------------------------------------------------
            // ANTI-RUIDO: no iniciar segmento si ocurrió un respawn muy reciente
            // -------------------------------------------------------------
            if (Time.time - LastRespawnTime < 0.20f)
            {
                //Debug.Log("[TRAJ] ❌ Segmento ignorado: muy cerca del respawn");
                return;
            }

            _isTracking = true;
            _positions.Clear();

            _startTime = Time.time;
            _startPos = transform.position;

            _totalDistance = 0f;
            _verticalOscillation = 0f;
            _usefulDistance = 0f;
            _uselessDistance = 0f;
            _zigzagAngleSum = 0f;
            _zigzagCount = 0;
            _maxLateralOffset = 0f;

            _positions.Add(_startPos);
        }

        // --------------------------------------------------------------------
        //  DURANTE EL VUELO (cada frame)
        // --------------------------------------------------------------------
        void TrackPosition()
        {
            Vector3 currentPos = transform.position;

            if (_positions.Count > 0)
            {
                Vector3 prevPos = _positions[_positions.Count - 1];
                Vector3 segment = currentPos - prevPos;

                float segLen = segment.magnitude;
                if (segLen > 0.0001f)
                {
                    _totalDistance += segLen;
                    _verticalOscillation += Mathf.Abs(currentPos.y - prevPos.y);
                }
            }

            _positions.Add(currentPos);
        }

        // --------------------------------------------------------------------
        //  FIN DEL VUELO: cálculo de métricas
        // --------------------------------------------------------------------
        void StopAndCompute()
        {
            _isTracking = false;
            _endPos = transform.position;
            float duration = Time.time - _startTime;

            if (_positions.Count < 3 || _totalDistance <= 0.0001f)
            {
                LogRow(duration, 0, 0, 0, 0, 0, 0, 0);
                return;
            }

            // Obtener anchor dinámico desde orientación
            Transform targetAnchor = (OrientationMetrics != null)
                ? OrientationMetrics.CurrentTargetAnchor
                : null;

            Vector3 idealTarget = targetAnchor ? targetAnchor.position : _endPos;
            Vector3 idealDir = (idealTarget - _startPos);

            float idealMag = idealDir.magnitude;
            if (idealMag > 0.0001f)
                idealDir /= idealMag;
            else
                idealDir = Vector3.forward;

            Vector3? prevDir = null;

            for (int i = 1; i < _positions.Count; i++)
            {
                Vector3 p0 = _positions[i - 1];
                Vector3 p1 = _positions[i];
                Vector3 segment = p1 - p0;

                float segLen = segment.magnitude;
                if (segLen < 0.0001f) continue;

                Vector3 dir = segment / segLen;

                // Useful vs Useless distance
                float advance = Vector3.Dot(segment, idealDir);
                if (advance > 0) _usefulDistance += advance;
                else _uselessDistance += -advance;

                // Zig-Zag (cambio angular entre direcciones)
                if (prevDir.HasValue)
                {
                    float angle = Vector3.Angle(prevDir.Value, dir);
                    _zigzagAngleSum += angle;
                    _zigzagCount++;
                }
                prevDir = dir;

                // Desvío lateral (perpendicular al ideal)
                Vector3 projected = Vector3.Dot(segment, idealDir) * idealDir;
                float lateral = (segment - projected).magnitude;
                if (lateral > _maxLateralOffset)
                    _maxLateralOffset = lateral;
            }

            // Métricas finales
            float denom = _usefulDistance + _uselessDistance;
            float efficiency = denom > 0.0001f ? _usefulDistance / denom : 0f;

            float zigzagAverage = _zigzagCount > 0 ? _zigzagAngleSum / _zigzagCount : 0f;

            float curvatureNormalized = _totalDistance > 0.0001f
                ? _zigzagAngleSum / _totalDistance
                : 0f;

            float landingError = Vector3.Distance(_endPos, idealTarget);

            // Escribir CSV
            LogRow(duration, efficiency, zigzagAverage, curvatureNormalized,
                   _maxLateralOffset, landingError, _totalDistance, _verticalOscillation);
        }

        // --------------------------------------------------------------------
        //  CSV
        // --------------------------------------------------------------------
        void InitializeFile()
        {
            if (_fileInitialized) return;
            Debug.Log($"[TRAJ LOGGER] Using HARDCODED path: {OutputDirectory}");
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
                    "StartTime",
                    "Duration",
                    "Efficiency",
                    "ZigZag_Average",
                    "Curvature_Normalized",
                    "MaxLateralOffset",
                    "LandingError",
                    "TotalDistance",
                    "VerticalOscillation",
                    "EndedInDeath",
                    "TargetAnchorName"
                });
                w.WriteLine(header);
            }

            _fileInitialized = true;
        }

        void LogRow(float duration,
            float efficiency,
            float zigzagAverage,
            float curvatureNormalized,
            float maxLateralOffset,
            float landingError,
            float totalDistance,
            float verticalOscillation)
        {
            // ============================================================
            //   ANTI-RUIDO: FILTROS DE SEGMENTOS BASURA
            // ============================================================

            // 1) Segmento demasiado corto → ruido del juego
            if (duration < 0.10f)
            {
                //Debug.Log($"[TRAJ] ❌ Descartado (duración muy baja = {duration:F3}s)");
                _endedInDeath = false;
                return;
            }

            // 2) Segmento que terminó en "muerte" pero sin distancia recorrida
            if (_endedInDeath && totalDistance < 0.10f)
            {
                //Debug.Log("[TRAJ] ❌ Descartado (muerte técnica / caída inmediata sin vuelo real)");
                _endedInDeath = false;
                return;
            }

            using (var w = new StreamWriter(_filePath, true))
            {
                var c = CultureInfo.InvariantCulture;

                // Obtener nombre del TargetAnchor dinámico desde OrientationMetrics
                string anchorName = (OrientationMetrics != null &&
                                     OrientationMetrics.CurrentTargetAnchor != null)
                                    ? OrientationMetrics.CurrentTargetAnchor.name
                                    : "None";
                string endedInDeath = _endedInDeath ? "1" : "0";

                string line = string.Join(CsvSeparator.ToString(), new[]
                {
                    AttemptID.ToString(),
                    _startTime.ToString(c),
                    duration.ToString(c),
                    efficiency.ToString(c),
                    zigzagAverage.ToString(c),
                    curvatureNormalized.ToString(c),
                    maxLateralOffset.ToString(c),
                    landingError.ToString(c),
                    totalDistance.ToString(c),
                    verticalOscillation.ToString(c),
                    endedInDeath,
                    anchorName
                });

                w.WriteLine(line);
            }
            _endedInDeath = false;
        }
        public void MarkDeath()
        {
            // Si NO estamos trackeando, evitar doble muerte
            if (!_isTracking)
                return;

            _endedInDeath = true;

            // Cerrar el segmento correctamente
            StopAndCompute();

            // Prevenir segmento fantasma después del respawn
            _isTracking = false;
            _lastFlyingState = false;
        }



    }
}
