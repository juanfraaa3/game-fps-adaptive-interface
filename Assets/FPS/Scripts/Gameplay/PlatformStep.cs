using UnityEngine;
using Unity.FPS.Gameplay;

public class PlatformStep : MonoBehaviour
{
    [Tooltip("La siguiente plataforma a la que debe ir el jugador después de aterrizar aquí.")]
    public Transform NextPlatform;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Jetpack jp = other.GetComponent<Jetpack>();
            if (jp != null && NextPlatform != null)
            {
                Debug.Log(
    $"[PLATFORM STEP] 🟢 Player ENTER {name} → NextTarget = {NextPlatform.name} | frame={Time.frameCount}"
);
                jp.OrientationTargetPlatform = NextPlatform;
                //Debug.Log("Nuevo objetivo: " + NextPlatform.name);
            }
        }
    }
}
