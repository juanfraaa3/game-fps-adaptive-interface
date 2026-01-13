using UnityEngine;
using System.Collections;

public class PerfectSyncedTwoPoints_WithMargin : MonoBehaviour
{
    [Header("Plataformas a sincronizar")]
    public Transform platformA;
    public Transform platformB;

    [Header("Configuración")]
    public float margin = 2f;        // distancia desde el punto medio donde se detendrán
    public float travelTime = 2f;    // tiempo de ida/vuelta
    public float pauseTime = 0.5f;   // pausa al cambiar de dirección

    [Header("Activación")]
    public string playerTag = "Player";

    private Vector3 startA;
    private Vector3 startB;
    private Vector3 leftLimitA;
    private Vector3 rightLimitB;
    private Vector3 midPoint;

    private bool isActive = false;
    private bool canActivate = false;
    private Coroutine moveRoutine;

    void Start()
    {
        startA = platformA.position;
        startB = platformB.position;

        // Calcular el punto medio entre ambas plataformas
        float midX = (startA.x + startB.x) / 2f;
        midPoint = new Vector3(midX, startA.y, startA.z);

        // Calcular los puntos donde se detendrán según el margen
        leftLimitA = new Vector3(midX - margin, startA.y, startA.z);
        rightLimitB = new Vector3(midX + margin, startB.y, startB.z);

        Invoke(nameof(EnableActivation), 0.1f);
    }
    void EnableActivation()
    {
        canActivate = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canActivate) return;
        if (isActive) return;

        if (other.CompareTag(playerTag))
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (isActive) return;

        isActive = true;
        moveRoutine = StartCoroutine(MoveCycle());
    }

    public void ResetPlatforms()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        isActive = false;

        platformA.position = startA;
        platformB.position = startB;
    }

    IEnumerator MoveCycle()
    {
        while (true)
        {
            // 1️⃣ Ir desde posición inicial hacia el punto de margen (acercamiento)
            yield return MovePlatforms(startA, leftLimitA, startB, rightLimitB);

            // 2️⃣ Pausa breve cuando llegan al margen
            yield return new WaitForSeconds(pauseTime);

            // 3️⃣ Regresar a las posiciones iniciales
            yield return MovePlatforms(leftLimitA, startA, rightLimitB, startB);

            yield return new WaitForSeconds(pauseTime);
        }
    }

    IEnumerator MovePlatforms(Vector3 fromA, Vector3 toA, Vector3 fromB, Vector3 toB)
    {
        float elapsed = 0f;

        while (elapsed < travelTime)
        {
            float t = elapsed / travelTime;
            platformA.position = Vector3.Lerp(fromA, toA, t);
            platformB.position = Vector3.Lerp(fromB, toB, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        platformA.position = toA;
        platformB.position = toB;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (platformA == null || platformB == null) return;

        Vector3 a = Application.isPlaying ? startA : platformA.position;
        Vector3 b = Application.isPlaying ? startB : platformB.position;

        float midX = (a.x + b.x) / 2f;
        Vector3 mid = new Vector3(midX, a.y, a.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(mid.x, mid.y + 5, mid.z), new Vector3(mid.x, mid.y - 5, mid.z));

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(mid.x - margin, mid.y + 5, mid.z), new Vector3(mid.x - margin, mid.y - 5, mid.z));
        Gizmos.DrawLine(new Vector3(mid.x + margin, mid.y + 5, mid.z), new Vector3(mid.x + margin, mid.y - 5, mid.z));
    }
#endif
}
