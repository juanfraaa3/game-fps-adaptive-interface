using UnityEngine;

public class ObstacleHitReporter : MonoBehaviour
{
    // Este flag evita contar múltiples triggers del MISMO bloque en el mismo intento
    private bool hitThisAttempt = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hitThisAttempt)
            return;

        // Buscar el logger en el Player (robusto para FPS Microgame)
        MultitaskMetricsLogger logger =
            other.transform.root.GetComponentInChildren<MultitaskMetricsLogger>();

        if (logger == null)
            return;

        hitThisAttempt = true;

        // 🔴 IMPORTANTE: usar el centro de la "barrera" (padre JumpWallPlatforms) si existe
        Vector3 barrierCenter = GetBarrierCenterPosition();

        // BarrierID automático por posición sobre el path
        int barrierID = logger.GetBarrierIDForPosition(barrierCenter);

        logger.NotifyBarrierHit(barrierID);
    }

    public void ResetForNewAttempt()
    {
        hitThisAttempt = false;
    }

    private Vector3 GetBarrierCenterPosition()
    {
        // Si el bloque está bajo un contenedor de barrera, usamos el centro del contenedor
        if (transform.parent != null &&
            transform.parent.name.Contains("JumpWallPlatforms"))
        {
            return transform.parent.position;
        }

        // Fallback: si no tiene padre, usa su propia posición
        return transform.position;
    }
}
