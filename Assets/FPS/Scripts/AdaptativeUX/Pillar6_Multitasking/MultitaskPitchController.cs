using UnityEngine;
using UnityEngine.Audio;

public class MultitaskPitchController : MonoBehaviour
{
    [Header("References")]
    public AudioSource audioSource;
    public MultitaskLoadEvaluator loadSource;

    [Header("Pitch Range")]
    public float basePitch = 1.0f;
    public float minPitch = 0.8f;

    [Header("Smoothing")]
    public float pitchSmooth = 5f;

    [Header("Enemy Attack Regulation")]
    public AudioMixer mainMixer;
    public string enemyVolumeParameter = "EnemyAttackVolume";

    public float enemyBaseDb = 0f;
    public float enemyCalmDb = -6f;

    float currentPitch;
    float currentEnemyDb;

    void Start()
    {
        currentPitch = basePitch;
        audioSource.pitch = basePitch;

        currentEnemyDb = enemyBaseDb;
        mainMixer.SetFloat(enemyVolumeParameter, enemyBaseDb);
    }

    void Update()
    {
        if (loadSource == null || audioSource == null || mainMixer == null)
            return;

        // --- MUSIC PITCH ---
        float targetPitch = Mathf.Lerp(
            basePitch,
            minPitch,
            loadSource.load01
        );

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            Time.deltaTime * pitchSmooth
        );

        audioSource.pitch = currentPitch;

        // --- ENEMY ATTACK VOLUME (SMOOTHED) ---
        float targetEnemyDb = Mathf.Lerp(
            enemyBaseDb,
            enemyCalmDb,
            loadSource.load01
        );

        currentEnemyDb = Mathf.Lerp(
            currentEnemyDb,
            targetEnemyDb,
            Time.deltaTime * pitchSmooth
        );

        mainMixer.SetFloat(enemyVolumeParameter, currentEnemyDb);
    }
}
