using System.Collections;
using UnityEngine;

public class TargetPlatformPulse : MonoBehaviour
{
    [Header("Target Renderer")]
    public Renderer TargetRenderer;

    [Header("Pulse Settings")]
    public float PulseDuration = 0.6f;
    public float MaxAlpha = 0.6f;
    public int PulseLoops = 2;

    Material _mat;
    Color _baseColor;
    Coroutine _pulseCo;

    void Awake()
    {
        if (TargetRenderer == null)
        {
            Debug.LogError($"[PILLAR 0][Pulse] ❌ TargetRenderer NULL en {name}");
            enabled = false;
            return;
        }

        _mat = TargetRenderer.material;
        _baseColor = _mat.color;
        _baseColor.a = 0f;
        _mat.color = _baseColor;

        Debug.Log($"[PILLAR 0][Pulse] ✅ Inicializado correctamente en {name}");
    }

    public void Pulse()
    {
        //Debug.Log($"[PILLAR 0][Pulse] 🔔 Pulse() llamado en {name}");

        if (_pulseCo != null)
            StopCoroutine(_pulseCo);

        _pulseCo = StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        float t = 0f;

        while (t < PulseDuration)
        {
            float n = t / PulseDuration;
            float wave = Mathf.PingPong(n * PulseLoops * 2f, 1f);

            Color c = _baseColor;
            c.a = wave * MaxAlpha;
            _mat.color = c;

            t += Time.deltaTime;
            yield return null;
        }

        _mat.color = _baseColor;
        _pulseCo = null;

        Debug.Log($"[PILLAR 0][Pulse] ✅ Pulso finalizado en {name}");
    }

}
