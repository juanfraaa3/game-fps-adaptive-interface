using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace Unity.FPS.Game
{
    public class JitterMetricsLogger : MonoBehaviour
    {
        [Header("References")]
        public AimJitterDetector JitterSource;

        [Header("Logging Settings")]
        public float Interval = 0.2f;
        public float HardJitterThreshold = 2f;

        [Header("Gameplay Metadata")]
        public int AttemptID = 1;
        public int RetryNumber = 0;
        public int WaveNumber = 0;
        public int Death = 0;

        [Header("Activation")]
        public bool loggingActive = false;

        private List<float> jitterSamples = new List<float>();
        private List<float> smoothSamples = new List<float>();

        private int jitterEvents = 0;
        private int overshootCount = 0;

        private float lastJitter = 0f;
        private float jitterAreaAccumulator = 0f;

        private bool inCluster = false;
        private int jitterClusterCount = 0;
        private float directionBias = 0f;

        private float intervalTimer;
        private string filePath;
        private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;


        public string FilePath => filePath;

        public float LastWaveStartTime = 0f;

        // ============================================================
        // 🔥 NUEVO: Soporte para múltiples enemigos
        // ============================================================
        private class TargetData
        {
            public Transform enemy;
            public float spawnTime;
            public bool completed;
            public bool killedBeforeAim;
        }

        private List<TargetData> activeTargets = new List<TargetData>();
        private float aimThresholdDegrees = 6f;

        private HashSet<Transform> completedEnemies = new HashSet<Transform>();

        // ============================================================
        void Start()
        {
            if (JitterSource == null)
            {
                Debug.LogError("JitterMetricsLogger: No AimJitterDetector assigned.");
                enabled = false;
                return;
            }

            string folder = Application.dataPath + "/FPS/Scripts/JitterSystem/JitterLogs";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            filePath = Path.Combine(folder,
                "jitterMetrics_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv");

            string header =
                "AttemptID;RetryNumber;WaveNumber;Death;" +
                "CurrentJitter;SmoothedJitter;IsHard;" +
                "JitterPeak;JitterVariance;JitterStdDev;" +
                "JitterEvents;OvershootCount;JitterArea;" +
                "JitterDirectionBias;JitterClusters\n";

            File.WriteAllText(filePath, header);
        }

        // ============================================================

        bool IsFacingAnyTarget()
        {
            if (activeTargets == null || activeTargets.Count == 0)
                return false;

            Transform cam = JitterSource.CameraTransform;
            if (cam == null)
                return false;

            Vector3 camPos = cam.position;
            Vector3 camFwd = cam.forward;

            float bestAngle = 999f;

            // recorremos targets
            foreach (var t in activeTargets)
            {
                if (t.completed || t.enemy == null)
                    continue;

                Vector3 dir = (t.enemy.position - camPos).normalized;
                float angle = Vector3.Angle(camFwd, dir);

                if (angle < bestAngle)
                    bestAngle = angle;
            }

            // 15 grados funciona perfecto para tu escenario
            return bestAngle <= 15f;
        }

        void Update()
        {
            if (!loggingActive)
                return;

            // 🔥 Time-to-target
            //ProcessTargetMetrics();
            if (!Input.GetButton("Aim"))
                return;
            // ============================================================
            // Tu sistema actual de jitter (no se toca)
            // ============================================================
            float j = JitterSource.CurrentJitter;
            float s = JitterSource.SmoothedJitter;
            if (!IsFacingAnyTarget())
            {
                WriteDiscardedFrame("OutOfAngle", j);
                return;
            }

            if (Mathf.Abs(j - lastJitter) < 0.001f)
            {
                WriteDiscardedFrame("NoMovement", j);
                return;
            }
            jitterSamples.Add(j);
            smoothSamples.Add(s);

            jitterAreaAccumulator += j;

            if (j > HardJitterThreshold)
                jitterEvents++;

            if (Mathf.Sign(j - lastJitter) != Mathf.Sign(lastJitter))
                overshootCount++;

            directionBias += (j - lastJitter);

            bool strong = j > HardJitterThreshold;
            if (strong && !inCluster)
            {
                jitterClusterCount++;
                inCluster = true;
            }
            else if (!strong)
            {
                inCluster = false;
            }

            lastJitter = j;

            intervalTimer += Time.deltaTime;
            if (intervalTimer >= Interval)
            {
                WriteMetrics();
                ResetInterval();
            }
        }

        // ============================================================
        // 🔥 NUEVO: Proceso Time-to-Target multi-enemy
        // ============================================================
        void ProcessTargetMetrics()
        {
            if (activeTargets.Count == 0) return;

            Vector3 camPos = JitterSource.CameraTransform.position;
            Vector3 camFwd = JitterSource.CameraTransform.forward;

            foreach (var t in activeTargets)
            {
                if (t.completed) continue;

                if (t.enemy == null)
                {
                    t.killedBeforeAim = true;
                    t.completed = true;
                    continue;
                }

                Vector3 dir = (t.enemy.position - camPos).normalized;
                float angle = Vector3.Angle(camFwd, dir);

                if (angle < aimThresholdDegrees)
                {
                    float ttt = Time.time - t.spawnTime;

                    Debug.Log(
                        "\n\n" +
                        "=============================================\n" +
                        "🔥🔥🔥 **TIME TO TARGET COMPLETADO** 🔥🔥🔥\n" +
                        "=============================================\n" +
                        $"⏱️ Wave      : {WaveNumber}\n" +
                        $"🎯 Enemy     : {t.enemy.name}\n" +
                        $"⏳ SpawnTime : {t.spawnTime:F3}\n" +
                        $"⚡ TTT (s)   : {ttt:F4}\n" +
                        "=============================================\n" +
                        "🚀 EL JUGADOR APUNTÓ AL ENEMIGO! 🚀\n" +
                        "=============================================\n\n"
                    );


                    File.AppendAllText(filePath,
                        $"TimeToTarget;{WaveNumber};{ttt.ToString("F4", CsvCulture)}\n");

                    t.completed = true;
                }
            }
        }

        // ============================================================
        // 🔥 NUEVO: Recibir enemigos desde WaveManager
        // ============================================================
        public void NotifyEnemySpawned(Transform enemy)
        {
            activeTargets.Add(new TargetData
            {

                enemy = enemy.root,
                spawnTime = Time.time,
                completed = false,
                killedBeforeAim = false
            });
            Debug.Log("📌 Enemy spawned: " + enemy.name + " → spawnTime = " + Time.time);

        }

        public void NotifyEnemyKilled(Transform enemy)
        {
            Transform root = enemy.root;
            foreach (var t in activeTargets)
            {
                if (t.enemy == enemy.root && !t.completed)
                {
                    t.killedBeforeAim = true;
                    t.completed = true;
                    break;
                }
            }
        }

        // ============================================================
        void WriteMetrics()
        {
            if (jitterSamples.Count == 0)
                return;

            float peak = 0f;
            foreach (float v in jitterSamples)
                if (v > peak) peak = v;

            float mean = 0f;
            foreach (float v in jitterSamples)
                mean += v;
            mean /= jitterSamples.Count;

            float variance = 0f;
            foreach (float v in jitterSamples)
                variance += (v - mean) * (v - mean);
            variance /= jitterSamples.Count;

            float stddev = Mathf.Sqrt(variance);

            float lastJ = jitterSamples[jitterSamples.Count - 1];
            float lastS = smoothSamples[smoothSamples.Count - 1];
            int isHard = lastJ > HardJitterThreshold ? 1 : 0;

            string line =
                $"{AttemptID};{RetryNumber};{WaveNumber};{Death};" +
                $"{lastJ.ToString("F4", CsvCulture)};" +
                $"{lastS.ToString("F4", CsvCulture)};" +
                $"{isHard};" +
                $"{peak.ToString("F4", CsvCulture)};" +
                $"{variance.ToString("F4", CsvCulture)};" +
                $"{stddev.ToString("F4", CsvCulture)};" +
                $"{jitterEvents};{overshootCount};" +
                $"{jitterAreaAccumulator.ToString("F4", CsvCulture)};" +
                $"{directionBias.ToString("F4", CsvCulture)};" +
                $"{jitterClusterCount}\n";

            File.AppendAllText(filePath, line);
        }

        // ============================================================
        void ResetInterval()
        {
            jitterSamples.Clear();
            smoothSamples.Clear();
            jitterEvents = 0;
            overshootCount = 0;
            jitterAreaAccumulator = 0;
            directionBias = 0f;
            jitterClusterCount = 0;

            // 🔥 limpieza de objetivos ya procesados
            activeTargets.RemoveAll(t => t.completed);

            intervalTimer = 0f;
        }

        // ============================================================

        public void WriteDeathSeparator(int wave)
        {
            float failTime = Time.time - LastWaveStartTime;

            File.AppendAllText(filePath,
                $"WaveFailTime;{wave};{failTime.ToString("F4", CsvCulture)}\n");
        }

        public void WriteWaveCompletedSeparator(int wave)
        {
            File.AppendAllText(filePath, $"COMPLETED WAVE {wave}\n");
        }

        public void NotifyEnemyHit(Transform enemy)
        {
            Transform root = enemy.root;

            // ❌ Ya registrado → ignorar
            if (completedEnemies.Contains(root))
                return;

            // 🔍 Buscar el target asociado
            foreach (var t in activeTargets)
            {
                if (t.enemy == root)
                {
                    float ttt = Time.time - t.spawnTime;

                    // 🔥 LOG SUPER LLAMATIVO
                    Debug.Log(
                        "\n\n" +
                        "=============================================\n" +
                        "🔥🔥🔥 TIME TO TARGET — IMPACTO 🔥🔥🔥\n" +
                        "=============================================\n" +
                        $"🎯 Enemy : {root.name}\n" +
                        $"🌊 Wave  : {WaveNumber}\n" +
                        $"⏳ TTT   : {ttt:F4} segundos\n" +
                        "=============================================\n\n"
                    );

                    // 🔥 CSV
                    File.AppendAllText(
                        filePath,
                        $"TimeToTargetHit;{WaveNumber};{root.name};{ttt.ToString("F4", CsvCulture)}\n"
                    );

                    // 🔐 Marcar como completado
                    t.completed = true;
                    completedEnemies.Add(root);

                    break;
                }
            }
        }

        void WriteDiscardedFrame(string reason, float j)
        {
            string path = FilePath;
            string line =
                $"{AttemptID};" +             // A
        $"{RetryNumber};" +           // B
        $"{WaveNumber};" +            // C
        $"{Death};" +                 // D

        $"---;" +                     // CurrentJitter (E)
        $"---;" +                     // SmoothedJitter (F)
        $"---;" +                     // IsHard (G)
        $"---;" +                     // JitterPeak (H)
        $"---;" +                     // JitterVariance (I)
        $"---;" +                     // JitterStdDev (J)
        $"---;" +                     // JitterEvents (K)
        $"---;" +                     // OvershootCount (L)
        $"---;" +                     // JitterArea (M)
        $"---;" +                     // JitterDirectionBias (N)
        $"---;" +                     // JitterClusters (O)

        $"{reason}\n";
            File.AppendAllText(path, line);
        }



    }
}
