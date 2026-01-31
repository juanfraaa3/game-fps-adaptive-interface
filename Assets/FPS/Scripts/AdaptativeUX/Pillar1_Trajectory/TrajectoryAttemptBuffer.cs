using UnityEngine;

public class TrajectoryAttemptBuffer : MonoBehaviour
{
    [Header("Two visualizers")]
    public JetpackTrajectoryVisualizer current;  // el que captura ahora
    public JetpackTrajectoryVisualizer previous; // el fantasma (intento anterior)

    [Header("Optional: auto-start current on spawn")]
    public bool activateCurrentOnStart = true;

    void Start()
    {
        if (previous != null) previous.Clear(true); // sin anterior al inicio

        if (activateCurrentOnStart && current != null)
            current.Activate();
    }

    // Llama esto cuando el jugador MUERE
    public void OnPlayerDied()
    {
        if (current == null || previous == null) return;

        // 1) Copiar el intento actual a "previous"
        var pts = current.GetPoints();
        previous.SetPoints(pts, enableLine: pts.Length > 0);

        // 2) Limpiar el current para el próximo intento (pero NO borres previous)
        current.Clear(disableLine: false); // lo dejamos habilitado si quieres
    }

    // Llama esto cuando el jugador RESPAWNEA / comienza el nuevo intento
    public void OnPlayerRespawned()
    {
        if (current == null) return;
        current.Activate(); // empieza a capturar el nuevo intento
    }
}
