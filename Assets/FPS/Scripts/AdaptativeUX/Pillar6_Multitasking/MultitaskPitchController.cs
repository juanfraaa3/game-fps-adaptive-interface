using UnityEngine;
using UnityEngine.Audio;

public class MultitaskPitchController : MonoBehaviour
{
    [Header("References")]
    public AudioSource audioSource;
    public MultitaskLoadEvaluator loadSource;

    [Header("Adaptive Thresholds (Load 0–1)")]
    [Tooltip("Percentil 75 → comienza la regulación emocional")]
    [Range(0f, 1f)]
    public float P75_Load = 0.6f;

    [Tooltip("Percentil 90 → regulación máxima")]
    [Range(0f, 1f)]
    public float P90_Load = 0.85f;

    [Header("Music Pitch Regulation")]
    public float basePitch = 1.0f;
    public float minPitch = 0.75f;
    public float pitchSmooth = 3f;

    [Header("Enemy Attack Regulation")]
    public AudioMixer mainMixer;
    public string enemyVolumeParameter = "EnemyAttackVolume";

    public float enemyBaseDb = 0f;
    public float enemyCalmDb = -12f;

    float currentPitch;
    float currentEnemyDb;
    [Header("Activation")]
    public bool adaptiveEnabled = false;
    void Start()
    {
        currentPitch = basePitch;
        audioSource.pitch = basePitch;

        currentEnemyDb = enemyBaseDb;
        if (mainMixer != null)
            mainMixer.SetFloat(enemyVolumeParameter, enemyBaseDb);
    }

    void Update()
    {
        if (!adaptiveEnabled)
            return;
        if (loadSource == null || audioSource == null || mainMixer == null)
            return;

        float load = loadSource.load01;

        // ------------------------------------------------
        // ACTIVACIÓN SOLO DESDE P75
        // ------------------------------------------------
        float assistWeight = NormalizeHigherWorse(load, P75_Load, P90_Load);

        // ------------------------------------------------
        // MUSIC PITCH
        // ------------------------------------------------
        float targetPitch = Mathf.Lerp(
            basePitch,
            minPitch,
            assistWeight
        );

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            Time.deltaTime * pitchSmooth
        );

        audioSource.pitch = currentPitch;

        // ------------------------------------------------
        // ENEMY ATTACK VOLUME
        // ------------------------------------------------
        float targetEnemyDb = Mathf.Lerp(
            enemyBaseDb,
            enemyCalmDb,
            assistWeight
        );

        currentEnemyDb = Mathf.Lerp(
            currentEnemyDb,
            targetEnemyDb,
            Time.deltaTime * pitchSmooth
        );

        mainMixer.SetFloat(enemyVolumeParameter, currentEnemyDb);

        Debug.Log($"[MULTITASK ADAPT] load={load:F2} | weight={assistWeight:F2}");
    }

    // ------------------------------------------------
    // NORMALIZACIÓN PERCENTIL (IGUAL QUE OTROS PILARES)
    // ------------------------------------------------
    float NormalizeHigherWorse(float value, float p75, float p90)
    {
        if (value <= p75) return 0f;
        if (value >= p90) return 1f;

        return Mathf.InverseLerp(p75, p90, value);
    }
    public void ActivateAdaptiveMusic()
    {
        adaptiveEnabled = true;
        Debug.Log("[MULTITASK ADAPT] Activated.");
    }

}
