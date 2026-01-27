using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public class AdaptivePillarEvaluator : MonoBehaviour
{
    [Header("General")]
    public int AttemptsWindow = 3;   // N últimos attempts
    public string CurrentPillarID;   // ej: "Orientation", "Pillar0"

    [Header("Orientation Logs Folder")]
    public string OrientationLogsFolder;


    [Header("Thresholds – Orientation")]
    public float MaxAvgAngleAllowed = 35f;
    public float MaxTimeLookingAway = 1.2f;

    [Header("Runtime Flags (READ ONLY)")]
    public bool OrientationAssistEnabled = false;


    void Awake()
    {
        OrientationAssistEnabled = false;
        Debug.Log("[ADAPTIVE] Init → OrientationAssistEnabled = FALSE");
    }

    /// <summary>
    /// Llamar SOLO cuando el jugador respawnea
    /// </summary>
    public void EvaluateAtRespawn()
    {
        Debug.Log($"[ADAPTIVE] EvaluateAtRespawn CALLED (pillar={CurrentPillarID})");

        if (CurrentPillarID == "Orientation")
        {
            OrientationAssistEnabled = EvaluateOrientation();
            Debug.Log($"[ADAPTIVE] OrientationAssist = {OrientationAssistEnabled}");
        }
    }



    bool EvaluateOrientation()
    {
        string csvPath = GetLatestCSV(OrientationLogsFolder);

        if (string.IsNullOrEmpty(csvPath))
        {
            Debug.LogWarning("[ADAPTIVE][Orientation] No CSV disponible");
            return false;
        }

        var lines = File.ReadAllLines(csvPath);

        if (lines.Length <= 1)
        {
            Debug.LogWarning("[ADAPTIVE][Orientation] CSV vacío");
            return false;
        }

        // =========================================
        // 1️⃣ Parsear filas
        // =========================================
        List<OrientationRow> rows = new();

        for (int i = 1; i < lines.Length; i++) // saltar header
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] cols = line.Split(';');
            if (cols.Length < 8)
                continue;

            try
            {
                rows.Add(new OrientationRow
                {
                    AttemptID = int.Parse(cols[0]),
                    AvgAngle = float.Parse(cols[3], System.Globalization.CultureInfo.InvariantCulture),
                    TimeAway = float.Parse(cols[4], System.Globalization.CultureInfo.InvariantCulture),
                    EndedInDeath = int.Parse(cols[7])
                });
            }
            catch
            {
                Debug.LogWarning("[ADAPTIVE][Orientation] Error parseando fila CSV");
            }
        }

        if (rows.Count == 0)
            return false;

        // =========================================
        // 2️⃣ Agrupar por AttemptID (intentos reales)
        // =========================================
        var attempts = rows
            .GroupBy(r => r.AttemptID)
            .OrderByDescending(g => g.Key)   // más recientes primero
            .Take(AttemptsWindow)
            .ToList();

        Debug.Log($"[ADAPTIVE][Orientation] Attempts detectados = {attempts.Count}");

        if (attempts.Count < AttemptsWindow)
        {
            Debug.Log("[ADAPTIVE][Orientation] Aún no hay suficientes intentos");
            return false;
        }

        // =========================================
        // 3️⃣ Resumir cada intento (peor caso)
        // =========================================
        float sumWorstAngle = 0f;
        float sumWorstAway = 0f;

        foreach (var attempt in attempts)
        {
            float worstAngle = attempt.Max(r => r.AvgAngle);
            float worstAway = attempt.Max(r => r.TimeAway);

            sumWorstAngle += worstAngle;
            sumWorstAway += worstAway;

            Debug.Log(
                $"[ADAPTIVE][Orientation] Attempt {attempt.Key} → " +
                $"WorstAngle={worstAngle:F1}, WorstAway={worstAway:F2}"
            );
        }

        float meanAngle = sumWorstAngle / AttemptsWindow;
        float meanAway = sumWorstAway / AttemptsWindow;

        Debug.Log(
            $"[ADAPTIVE][Orientation] MEAN Angle={meanAngle:F1} | MEAN Away={meanAway:F2}"
        );

        // =========================================
        // 4️⃣ Decisión
        // =========================================
        bool activateAssist =
            meanAngle > MaxAvgAngleAllowed ||
            meanAway > MaxTimeLookingAway;

        Debug.Log($"[ADAPTIVE][Orientation] Assist decision = {activateAssist}");

        return activateAssist;
    }



    float Average(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float sum = 0f;
        foreach (var v in values) sum += v;
        return sum / values.Count;
    }

    string GetLatestCSV(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[ADAPTIVE] Folder not found: {folderPath}");
            return null;
        }

        var files = new DirectoryInfo(folderPath)
            .GetFiles("*.csv");

        if (files.Length == 0)
        {
            Debug.LogWarning("[ADAPTIVE] No CSV files found in folder");
            return null;
        }

        FileInfo latest = files[0];
        foreach (var f in files)
        {
            if (f.LastWriteTime > latest.LastWriteTime)
                latest = f;
        }

        Debug.Log($"[ADAPTIVE] Latest CSV detected: {latest.Name}");
        return latest.FullName;
    }
    class OrientationRow
    {
        public int AttemptID;
        public float AvgAngle;
        public float TimeAway;
        public int EndedInDeath;
    }

}
