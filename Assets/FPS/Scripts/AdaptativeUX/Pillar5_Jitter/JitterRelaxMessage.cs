using UnityEngine;
using TMPro;

public class JitterRelaxMessage : MonoBehaviour
{
    [Header("References")]
    public JitterAdaptiveEvaluator Evaluator;
    public TextMeshProUGUI messageText;

    [Header("Behavior")]
    [Tooltip("If assist goes above this value, the message appears.")]
    public float showThreshold = 0.75f;

    [Tooltip("How fast the text fades in/out.")]
    public float fadeSpeed = 6f;

    [Header("Aim Filter")]
    public bool requireAimToShow = true;
    public float aimAxisThreshold = 0.3f;

    [Header("Text Colors")]
    public Color hiddenColor = new Color(1f, 1f, 1f, 0f);
    public Color visibleColor = new Color(1f, 0.25f, 0.25f, 1f);

    void Reset()
    {
        messageText = GetComponent<TextMeshProUGUI>();
    }

    void Awake()
    {
        if (messageText == null)
            messageText = GetComponent<TextMeshProUGUI>();

        if (messageText != null)
            messageText.color = hiddenColor;
    }

    void Update()
    {
        if (Evaluator == null || messageText == null)
            return;

        // ----------------------------
        // AIM FILTER
        // ----------------------------
        bool isAiming = true;

        if (requireAimToShow)
        {
            float aimAxis = Input.GetAxis("Aim");
            bool aimButton = Input.GetButton("Aim");
            bool ps4L2 = Input.GetKey(KeyCode.JoystickButton6);
            bool xboxLT = Input.GetKey(KeyCode.JoystickButton7);

            isAiming =
                aimAxis > aimAxisThreshold ||
                aimButton ||
                ps4L2 ||
                xboxLT;
        }

        float weight = Evaluator.JitterAssistWeight01;

        bool show = isAiming && weight >= showThreshold;

        Color target = show ? visibleColor : hiddenColor;

        messageText.color = Color.Lerp(
            messageText.color,
            target,
            Time.unscaledDeltaTime * fadeSpeed
        );
    }
}