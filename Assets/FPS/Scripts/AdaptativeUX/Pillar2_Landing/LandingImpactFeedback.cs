using System.Collections;
using UnityEngine;
using Unity.FPS.Gameplay;
using TMPro;

public class LandingImpactFeedback : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public AudioSource audioSource;
    public AudioClip hardImpactClip;

    [Tooltip("Opcional: para leer LastVerticalSpeedBeforeGrounding (recomendado). Si no, se intenta auto-asignar.")]
    public PlayerCharacterController PlayerController;

    [Tooltip("Opcional: para usar su LandingAssistWeight01 (si quieres gatear el feedback adaptativo).")]
    public LandingAdaptiveEvaluator Evaluator;

    [Header("Landing Message UI")]
    public CanvasGroup landingMessageGroup;
    public TextMeshProUGUI landingMessageText;
    public float messageDuration = 1.2f;

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

    Coroutine shakeRoutine;
    Coroutine messageRoutine;

    // Guardamos velocidad vertical real previa al impacto (como el controlador)
    float _lastAirVerticalSpeed = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        if (PlayerController == null)
            PlayerController = GetComponent<PlayerCharacterController>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        cam = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (cam != null)
            baseFOV = cam.fieldOfView;

        if (landingMessageGroup != null)
            landingMessageGroup.alpha = 0f;
    }

    void Update()
    {
        bool groundedNow = IsGrounded();

        // Capturar velocidad vertical real en aire
        if (!groundedNow && PlayerController != null)
            _lastAirVerticalSpeed = PlayerController.LastVerticalSpeedBeforeGrounding;

        // Detectar aterrizaje
        if (!wasGrounded && groundedNow)
            OnLanding(_lastAirVerticalSpeed);

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

    void OnLanding(float verticalSpeedPreImpact)
    {
        float absSpeed = Mathf.Abs(verticalSpeedPreImpact);

        // Severidad física (misma escala que daño)
        float baseSeverity = Mathf.InverseLerp(
            MinSpeedForFallDamage,
            MaxSpeedForFallDamage,
            absSpeed
        );

        // Si no llega al mínimo, no hacemos nada (ni mensaje)
        if (baseSeverity <= 0f)
            return;

        // Gate adaptativo (opcional): si quieres que el shake/sonido solo ocurra sobre P75
        float assistWeight = 0f;
        if (Evaluator != null)
            assistWeight = Evaluator.LandingAssistWeight01;

        // 🔸 El MENSAJE siempre es coherente con daño (baseSeverity), independiente del gate adaptativo
        ShowLandingMessage(baseSeverity);

        // Si quieres que el shake/sonido sea “adaptativo”, lo gateamos por assistWeight
        if (assistWeight <= 0f)
            return;

        // Intensidad final: física x adaptativa
        float severity = baseSeverity * assistWeight;

        if (audioSource != null && hardImpactClip != null)
            audioSource.PlayOneShot(hardImpactClip, Mathf.Lerp(0.4f, 1f, severity));

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(CameraShake(severity));
    }

    // =========================================================
    // MENSAJE CON 3 ESTILOS (verde suave / amarillo fuerte / rojo punch)
    // =========================================================
    void ShowLandingMessage(float severity01)
    {
        if (landingMessageText == null || landingMessageGroup == null)
            return;

        // cortar animación previa
        if (messageRoutine != null)
            StopCoroutine(messageRoutine);

        // Decide tier por severidad (misma escala que daño)
        if (severity01 < 0.33f)
        {
            landingMessageText.text = "Aterrizaje suave";
            landingMessageText.color = Color.green;
            messageRoutine = StartCoroutine(AnimateMessageSoft());
        }
        else if (severity01 < 0.66f)
        {
            landingMessageText.text = "Aterrizaje intermedio";
            landingMessageText.color = Color.yellow;
            messageRoutine = StartCoroutine(AnimateMessageMedium());
        }
        else
        {
            landingMessageText.text = "Impacto severo";
            landingMessageText.color = Color.red;
            messageRoutine = StartCoroutine(AnimateMessageHard());
        }
    }

    IEnumerator AnimateMessageSoft()
    {
        RectTransform rt = landingMessageText.rectTransform;
        Vector3 baseScale = Vector3.one;
        rt.localScale = baseScale;

        // Fade in suave
        float t = 0f;
        float fadeIn = 0.35f;
        landingMessageGroup.alpha = 0f;

        while (t < fadeIn)
        {
            landingMessageGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
            t += Time.deltaTime;
            yield return null;
        }

        landingMessageGroup.alpha = 1f;

        yield return new WaitForSeconds(messageDuration);

        // Fade out suave
        t = 0f;
        float fadeOut = 0.45f;
        while (t < fadeOut)
        {
            landingMessageGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
            t += Time.deltaTime;
            yield return null;
        }

        landingMessageGroup.alpha = 0f;
    }

    IEnumerator AnimateMessageMedium()
    {
        RectTransform rt = landingMessageText.rectTransform;
        Vector3 baseScale = Vector3.one;

        // Aparece más “firme”: pop leve + alpha fuerte
        landingMessageGroup.alpha = 1f;

        float popTime = 0.12f;
        float t = 0f;
        while (t < popTime)
        {
            float k = t / popTime;
            float s = Mathf.Lerp(1f, 1.12f, Mathf.Sin(k * Mathf.PI));
            rt.localScale = baseScale * s;
            t += Time.deltaTime;
            yield return null;
        }
        rt.localScale = baseScale;

        yield return new WaitForSeconds(messageDuration);

        // Fade out rápido
        t = 0f;
        float fadeOut = 0.35f;
        while (t < fadeOut)
        {
            landingMessageGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
            t += Time.deltaTime;
            yield return null;
        }

        landingMessageGroup.alpha = 0f;
        rt.localScale = baseScale;
    }

    IEnumerator AnimateMessageHard()
    {
        RectTransform rt = landingMessageText.rectTransform;
        Vector3 baseScale = Vector3.one;

        landingMessageGroup.alpha = 1f;

        // Punch: pop + micro-shake UI corto
        float punchTime = 0.18f;
        float t = 0f;

        while (t < punchTime)
        {
            float k = t / punchTime;

            // pop más fuerte
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.20f;
            rt.localScale = baseScale * s;

            // micro-shake (solo UI)
            float shake = (1f - k) * 6f;
            float x = Mathf.Sin(Time.time * 80f) * shake;
            float y = Mathf.Cos(Time.time * 75f) * shake;
            rt.anchoredPosition += new Vector2(x, y) * Time.deltaTime;

            t += Time.deltaTime;
            yield return null;
        }

        // Reset pos/scale
        rt.localScale = baseScale;
        rt.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(messageDuration);

        // Fade out
        t = 0f;
        float fadeOut = 0.30f;
        while (t < fadeOut)
        {
            landingMessageGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
            t += Time.deltaTime;
            yield return null;
        }

        landingMessageGroup.alpha = 0f;
        rt.localScale = baseScale;
        rt.anchoredPosition = Vector2.zero;
    }

    // =========================================================
    // SHAKE DE CÁMARA (tu lógica original)
    // =========================================================
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
