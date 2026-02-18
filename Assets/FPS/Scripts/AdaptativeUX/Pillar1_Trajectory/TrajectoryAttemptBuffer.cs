using UnityEngine;

public class TrajectoryAttemptBuffer : MonoBehaviour
{
    [Header("Two visualizers")]
    public JetpackTrajectoryVisualizer current;  // el que captura ahora
    public JetpackTrajectoryVisualizer previous; // el fantasma (intento anterior)

    [Header("Optional: auto-start current on spawn")]
    public bool activateCurrentOnStart = true;
    public TrajectoryAdaptiveEvaluator Evaluator;

    void Start()
    {
        if (previous != null) previous.Clear(true); // sin anterior al inicio

        if (activateCurrentOnStart && current != null)
            current.Activate();
    }

    // Llama esto cuando el jugador MUERE
    public void OnPlayerDied()
    {
        if (current == null || previous == null)
            return;

        var pts = current.GetPoints();

        float assistWeight = 0f;

        if (Evaluator != null)
            assistWeight = Evaluator.TrajectoryAssistWeight01;

        // Bajo P75 → no mostrar nada
        if (assistWeight <= 0f || pts.Length == 0)
        {
            previous.Clear(true);
            current.Clear(disableLine: false);
            return;
        }

        // Copiar trayectoria anterior
        previous.SetPoints(pts, enableLine: true);

        // ------------------------
        // COLOR SOLO BASADO EN NIVEL ADAPTATIVO
        // ------------------------
        Color color;

        if (assistWeight < 0.5f)
            color = Color.yellow;
        else
            color = Color.red;

        // Alpha proporcional
        color.a = assistWeight;

        previous.line.startColor = color;
        previous.line.endColor = color;

        // ------------------------
        // GROSOR ADAPTATIVO
        // ------------------------
        float minWidth = 2f;
        float maxWidth = 5f;

        float width = Mathf.Lerp(minWidth, maxWidth, assistWeight);

        previous.line.widthMultiplier = width;

        // Limpiar current para el siguiente intento
        current.Clear(disableLine: false);
    }



    // Llama esto cuando el jugador RESPAWNEA / comienza el nuevo intento
    public void OnPlayerRespawned()
    {
        if (current == null) return;
        current.Activate(); // empieza a capturar el nuevo intento
    }
}
