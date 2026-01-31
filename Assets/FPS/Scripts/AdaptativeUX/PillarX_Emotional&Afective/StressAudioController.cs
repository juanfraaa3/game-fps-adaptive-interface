using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Controla la regulación auditiva adaptativa ante sobrecarga de amenaza,
/// utilizando Snapshots del MainAudioMixer.
/// 
/// Diseñado para amortiguar principalmente EnemyAttack (balas enemigas)
/// sin modificar dificultad ni mecánicas.
/// </summary>
public class StressAudioController_Snapshots : MonoBehaviour
{
    [Header("Main Audio Mixer")]
    [Tooltip("MainAudioMixer del FPS Sample")]
    public AudioMixer mainMixer;

    [Header("Snapshots")]
    [Tooltip("Snapshot normal (sin regulación)")]
    public AudioMixerSnapshot normalSnapshot;

    [Tooltip("Snapshot de estrés (EnemyAttack amortiguado)")]
    public AudioMixerSnapshot stressSnapshot;

    [Header("Transition")]
    [Tooltip("Tiempo de transición entre snapshots (segundos)")]
    [Range(0.05f, 2f)]
    public float transitionTime = 0.35f;

    [Header("Stress State (runtime)")]
    [Range(0f, 1f)]
    [SerializeField] private float stress01;

    [Tooltip("Umbral a partir del cual se considera estrés activo")]
    [Range(0f, 1f)]
    public float stressThreshold = 0.5f;

    [Header("Debug")]
    public bool debugWithKey = true;
    public KeyCode debugStressKey = KeyCode.T;

    // Estado interno
    private bool isInStressState;

    void Update()
    {
        // ============================
        // DEBUG RÁPIDO (para probar hoy)
        // ============================
        if (debugWithKey)
        {
            stress01 = Input.GetKey(debugStressKey) ? 1f : 0f;
        }

        // ============================
        // LÓGICA DE TRANSICIÓN
        // ============================
        if (!isInStressState && stress01 >= stressThreshold)
        {
            // Entramos en estado de estrés
            stressSnapshot.TransitionTo(transitionTime);
            isInStressState = true;
        }
        else if (isInStressState && stress01 < stressThreshold)
        {
            // Volvemos a estado normal
            normalSnapshot.TransitionTo(transitionTime);
            isInStressState = false;
        }
    }

    // ======================================================
    // MÉTODOS PÚBLICOS (para conectar luego con gameplay)
    // ======================================================

    /// <summary>
    /// Fuerza el estado de estrés (por ejemplo, muchos enemigos disparando).
    /// </summary>
    public void ForceStress()
    {
        stress01 = 1f;
    }

    /// <summary>
    /// Reduce el estrés gradualmente.
    /// </summary>
    public void ClearStress()
    {
        stress01 = 0f;
    }

    /// <summary>
    /// Permite setear estrés desde otra métrica (0..1).
    /// </summary>
    public void SetStress01(float value)
    {
        stress01 = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Solo para logging o análisis.
    /// </summary>
    public float GetStress01()
    {
        return stress01;
    }
}
