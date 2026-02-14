using System.Collections.Generic;
using UnityEngine;

public class MultitaskLoadEvaluator : MonoBehaviour
{
    [Header("Output (0–1)")]
    [Range(0f, 1f)]
    public float load01 = 0f;

    [Header("Reference Frame")]
    [Tooltip("Root de la plataforma móvil. Si es null, se usa mundo.")]
    public Transform platformTransform;

    [Header("Sampling")]
    [Tooltip("Cada cuánto se evalúa (segundos).")]
    public float sampleInterval = 0.2f;

    [Tooltip("Ventana temporal para calcular acciones por segundo.")]
    public float windowSeconds = 1.5f;

    [Header("Sensitivity")]
    [Tooltip("Movimiento mínimo (en espacio local) para contar como acción.")]
    public float moveDeltaMin = 0.01f;

    [Tooltip("Rotación mínima (grados) para contar como acción.")]
    public float angleDeltaMin = 0.5f;

    [Header("Normalization")]
    [Tooltip("Acciones/seg que representan multitasking máximo (load01 = 1).")]
    public float maxActionsPerSecond = 8f;

    float timer;
    Queue<float> actionTimes = new Queue<float>();

    Vector3 lastLocalPos;
    Quaternion lastRot;

    void Start()
    {
        // Posición inicial en marco local de la plataforma (o mundo si no hay)
        if (platformTransform != null)
            lastLocalPos = platformTransform.InverseTransformPoint(transform.position);
        else
            lastLocalPos = transform.position;

        lastRot = transform.rotation;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= sampleInterval)
        {
            timer = 0f;
            SampleActions();
            ComputeLoad();
        }
    }

    void SampleActions()
    {
        float now = Time.time;

        // --- MOVIMIENTO RELATIVO A LA PLATAFORMA ---
        Vector3 currentLocalPos = platformTransform != null
            ? platformTransform.InverseTransformPoint(transform.position)
            : transform.position;

        float moveDelta = Vector3.Distance(currentLocalPos, lastLocalPos);
        if (moveDelta > moveDeltaMin)
            actionTimes.Enqueue(now);

        lastLocalPos = currentLocalPos;

        // --- ROTACIÓN DEL JUGADOR (no depende de la plataforma) ---
        float angleDelta = Quaternion.Angle(transform.rotation, lastRot);
        if (angleDelta > angleDeltaMin)
            actionTimes.Enqueue(now);

        lastRot = transform.rotation;

        // --- LIMPIAR VENTANA TEMPORAL ---
        while (actionTimes.Count > 0 &&
               now - actionTimes.Peek() > windowSeconds)
        {
            actionTimes.Dequeue();
        }
    }

    void ComputeLoad()
    {
        float actionsPerSecond =
            actionTimes.Count / Mathf.Max(windowSeconds, 0.001f);

        load01 = Mathf.Clamp01(actionsPerSecond / maxActionsPerSecond);
    }
}
