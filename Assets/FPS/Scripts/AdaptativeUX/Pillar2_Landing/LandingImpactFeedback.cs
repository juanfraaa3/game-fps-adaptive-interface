using System.Collections;
using UnityEngine;

/// <summary>
/// Landing Impact Feedback – ALINEADO CON FALL DAMAGE
/// Usa exactamente los mismos criterios que PlayerCharacterController:
/// - MinSpeedForFallDamage = 10 m/s
/// - MaxSpeedForFallDamage = 30 m/s
/// Feedback proporcional al daño por caída.
/// </summary>
public class LandingImpactFeedback : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public AudioSource audioSource;
    public AudioClip hardImpactClip;

    [Header("Fall Damage Thresholds (MATCH GAME)")]
    public float MinSpeedForFallDamage = 10f;
    public float MaxSpeedForFallDamage = 30f;

    [Header("Camera Feedback Scaling")]
    public float maxShakeRotationDeg = 12f;
    public float maxShakePosition = 0.35f;
    public float shakeDuration = 0.12f;

    Camera cam;
    float baseFOV;

    CharacterController cc;
    Rigidbody rb;
    bool wasGrounded;

    Vector3 lastPos;
    float lastTime;

    Coroutine shakeRoutine;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        cam = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (cam != null)
            baseFOV = cam.fieldOfView;

        lastPos = transform.position;
        lastTime = Time.time;
    }

    void Update()
    {
        bool groundedNow = IsGrounded();
        Vector3 velocity = GetVelocity();

        if (!wasGrounded && groundedNow)
            OnLanding(velocity);

        wasGrounded = groundedNow;
    }

    bool IsGrounded()
    {
        if (cc != null)
            return cc.isGrounded;

        return Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            0.2f
        );
    }

    Vector3 GetVelocity()
    {
        if (rb != null)
            return rb.velocity;

        float dt = Time.time - lastTime;
        if (dt <= Mathf.Epsilon) return Vector3.zero;

        Vector3 vel = (transform.position - lastPos) / dt;
        lastPos = transform.position;
        lastTime = Time.time;
        return vel;
    }

    void OnLanding(Vector3 velocity)
    {

        float verticalDownSpeed = Mathf.Max(0f, -velocity.y);
        Debug.Log(
            $"[ON LANDING] raw verticalDownSpeed={verticalDownSpeed:F2}"
        );

        // MISMA NORMALIZACIÓN QUE EL DAÑO
        float severity = Mathf.InverseLerp(
            MinSpeedForFallDamage,
            MaxSpeedForFallDamage,
            verticalDownSpeed
        );

        // Bajo el mínimo → no hay feedback
        if (severity <= 0f)
            return;

        Debug.Log(
            $"[ADAPTIVE LANDING] speed={verticalDownSpeed:F2} | severity={severity:F2}"
        );

        if (audioSource != null && hardImpactClip != null)
            audioSource.PlayOneShot(hardImpactClip, Mathf.Lerp(0.4f, 1f, severity));

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(CameraShake(severity));
    }

    IEnumerator CameraShake(float severity)
    {
        Transform camT = playerCamera;
        Vector3 basePos = camT.localPosition;
        Quaternion baseRot = camT.localRotation;

        float rot = Mathf.Lerp(3f, maxShakeRotationDeg, severity);
        float pos = Mathf.Lerp(0.05f, maxShakePosition, severity);
        float fovKick = Mathf.Lerp(2f, 12f, severity);

        float t = 0f;
        float total = shakeDuration * Mathf.Lerp(1f, 1.8f, severity);

        while (t < total)
        {
            float k = Mathf.Pow(1f - t / total, 1.5f);

            Vector3 r = new Vector3(
                Mathf.Sin(Time.time * 45f) * rot,
                Mathf.Sin(Time.time * 32f) * rot * 0.6f,
                Mathf.Sin(Time.time * 52f) * rot * 0.8f
            ) * k;

            Vector3 p = new Vector3(
                Mathf.Sin(Time.time * 35f) * pos,
                Mathf.Cos(Time.time * 28f) * pos,
                0f
            ) * k;

            camT.localPosition = basePos + p;
            camT.rotation = camT.rotation * Quaternion.Euler(r);

            if (cam != null)
                cam.fieldOfView = baseFOV + fovKick * k;

            t += Time.deltaTime;
            yield return null;
        }

        camT.localPosition = basePos;
        camT.localRotation = baseRot;

        if (cam != null)
            cam.fieldOfView = baseFOV;
    }
}
