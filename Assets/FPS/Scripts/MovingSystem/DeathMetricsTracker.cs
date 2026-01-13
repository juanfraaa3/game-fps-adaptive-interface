using System.Globalization;
using UnityEngine;

public class DeathMetricsTracker : MonoBehaviour
{
    public static DeathMetricsTracker Instance;

    [Header("Death Windows (seconds)")]
    public float obstacleDeathWindow = 1.2f;

    // Último evento relevante
    private int lastSegmentID = -1;
    private int lastElementID = -1;
    private string lastEventType = ""; // "Obstacle" o "Platform"

    private float lastObstacleExitTime = -100f;
    private float lastPlatformTouchTime = -100f;

    private static readonly CultureInfo CsvCulture = CultureInfo.GetCultureInfo("es-ES");

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // --------------------
    // Hooks desde otros scripts
    // --------------------

    public void RegisterObstacleExit(int segmentID, int obstacleID)
    {
        lastSegmentID = segmentID;
        lastElementID = obstacleID;
        lastEventType = "Obstacle";
        lastObstacleExitTime = Time.time;
    }

    public void RegisterPlatformTouch(int segmentID, int platformID)
    {
        lastSegmentID = segmentID;
        lastElementID = platformID;
        lastEventType = "Platform";
        lastPlatformTouchTime = Time.time;
    }

    // --------------------
    // LLAMAR ESTO CUANDO EL JUGADOR MUERE
    // --------------------

    public void RegisterDeath()
    {
        string deathType = ClassifyDeathType();
        WriteDeathCSVRow(deathType);

        Debug.Log($"[DEATH] Type={deathType}, Segment={lastSegmentID}, Element={lastElementID}");
    }

    // --------------------
    // Clasificación de muerte
    // --------------------

    private string ClassifyDeathType()
    {
        float now = Time.time;

        // 1️⃣ Muerte asociada a obstáculo (ventana amplia)
        if (now - lastObstacleExitTime <= obstacleDeathWindow)
        {
            return "Obstacle";
        }

        // 2️⃣ Falló aterrizaje tras obstáculo
        if (lastEventType == "Obstacle" &&
            lastPlatformTouchTime < lastObstacleExitTime)
        {
            return "MissedLanding";
        }

        // 3️⃣ Todo lo demás
        return "Other";
    }

    // --------------------
    // CSV
    // --------------------

    private void WriteDeathCSVRow(string deathType)
    {
        string header =
            "EventType;SegmentID;ElementID;TimeOnPlatform;RelativePositionStd;RelativePositionMean;RelativePositionMax;RelativePositionMin;" +
            "SampleCount;EdgeRiskTime;CorrectionPeaks;ObstacleType;ClearType;JetpackAttempted;MinClearance;MicroCorrections;ObstacleDuration;Died;DeathType";

        string[] fields = new string[19];

        fields[0] = "Death";
        fields[1] = lastSegmentID.ToString(CsvCulture);
        fields[2] = lastElementID.ToString(CsvCulture);

        // métricas no aplican
        for (int i = 3; i <= 16; i++)
            fields[i] = "";

        fields[17] = "1";
        fields[18] = deathType;

        string line = string.Join(";", fields);

        CSVMetricWriter.WriteLine("MovementMetrics", header, line);
    }
}
