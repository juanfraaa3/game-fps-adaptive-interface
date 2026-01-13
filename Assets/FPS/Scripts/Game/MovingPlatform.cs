using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    [Header("Activation")]
    public string playerTag = "Player";
    private bool isActive = false;

    private Transform target;
    private Vector3 lastPosition;
    // Reset support (misma idea que Multiple)
    private Vector3 initialPosition;
    private Transform initialTarget;
    void Start()
    {
        initialPosition = transform.position;
        target = pointB;
        initialTarget = target;
        lastPosition = transform.position;
    }

    void Update()
    {
        if (!isActive) return;
        if (pointA == null || pointB == null) return;

        Vector3 movement = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        transform.position = movement;

        // Importante: lastPosition debe quedar en la pos final del frame
        lastPosition = transform.position;

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            target = (target == pointA) ? pointB : pointA;
        }
    }

    public Vector3 GetDeltaMovement()
    {
        return transform.position - lastPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            ActivatePlatform();
        }
    }

    public void ActivatePlatform()
    {
        if (isActive) return;
        isActive = true;
    }

    public void ResetPlatform()
    {
        isActive = false;
        target = initialTarget;
        transform.position = initialPosition;
        lastPosition = transform.position;
    }
}
