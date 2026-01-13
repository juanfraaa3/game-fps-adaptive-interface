using UnityEngine;

public class MovingPlatformMultiple : MonoBehaviour
{
    public Transform[] points; // Arreglo de puntos de destino
    public float speed = 2f;

    private int currentTargetIndex = 0; // Índice del punto actual
    private Vector3 lastPosition;
    public string playerTag = "Player";

    private bool isActive = false;
    private Vector3 initialPosition;
    private int initialTargetIndex;

    [Header("Linked Platform (Optional)")]
    public MovingPlatformMultiple linkedPlatform;
    public bool isFollower = false;

    void Start()
    {
        initialPosition = transform.position;
        initialTargetIndex = currentTargetIndex;

        if (points.Length > 0)
        {
            lastPosition = transform.position;
        }
    }


    void Update()
    {
        if (!isActive) return;
        if (points.Length == 0) return;

        Vector3 movement = Vector3.MoveTowards(
            transform.position,
            points[currentTargetIndex].position,
            speed * Time.deltaTime
        );

        Vector3 delta = movement - transform.position;
        transform.position = movement;
        lastPosition = transform.position;

        if (Vector3.Distance(transform.position, points[currentTargetIndex].position) < 0.1f)
        {
            currentTargetIndex = (currentTargetIndex + 1) % points.Length;
        }
    }


    public Vector3 GetDeltaMovement()
    {
        return transform.position - lastPosition;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (isFollower) return;

        if (other.CompareTag(playerTag))
        {
            ActivatePlatform();
        }
    }


    public void ResetPlatform()
    {
        isActive = false;
        currentTargetIndex = initialTargetIndex;
        transform.position = initialPosition;
        lastPosition = transform.position;
    }
    public void ActivatePlatform()
    {
        if (isActive) return;

        isActive = true;

        // Si esta plataforma controla otra, activarla también
        if (linkedPlatform != null)
        {
            linkedPlatform.ActivatePlatform();
        }
    }

}
