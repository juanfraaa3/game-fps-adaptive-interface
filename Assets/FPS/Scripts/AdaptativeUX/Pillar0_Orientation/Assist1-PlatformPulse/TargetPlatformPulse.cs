using UnityEngine;

public class TargetPlatformPulse : MonoBehaviour
{
    [Header("Renderer")]
    public Renderer TargetRenderer;

    [Header("Visual")]
    public Color PulseColor = Color.yellow;
    public float MaxAlpha = 0.8f;
    public float PulseSpeed = 4f;

    Material _mat;
    Color _baseColor;
    float _intensity;

    void Awake()
    {
        if (TargetRenderer == null)
        {
            Debug.LogError($"[Pulse] ❌ TargetRenderer NULL en {name}");
            enabled = false;
            return;
        }

        _mat = TargetRenderer.material;
        _baseColor = _mat.color;
        _baseColor.a = 0f;
        _mat.color = _baseColor;
    }

    void Update()
    {
        if (_intensity <= 0f)
        {
            _mat.color = _baseColor;
            return;
        }

        float pulse =
            (Mathf.Sin(Time.time * PulseSpeed) * 0.5f + 0.5f) * _intensity;

        Color c = PulseColor;
        c.a = pulse * MaxAlpha;
        _mat.color = c;
    }

    public void SetIntensity(float intensity01)
    {
        _intensity = Mathf.Clamp01(intensity01);
    }
}
