using System.Collections.Generic;
using UnityEngine;

public class JetpackTrajectoryVisualizer : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public LineRenderer line;

    [Header("Sampling")]
    public float sampleInterval = 0.08f;
    public int maxPoints = 80;

    private float timer;
    private List<Vector3> points = new List<Vector3>();
    private bool isActive = false;

    void Awake()
    {
        line.positionCount = 0;
        line.enabled = false;
    }

    void Update()
    {
        if (!isActive) return;

        timer += Time.deltaTime;
        if (timer >= sampleInterval)
        {
            timer = 0f;
            AddPoint(playerTransform.position);
        }
    }

    void AddPoint(Vector3 pos)
    {
        points.Add(pos);

        if (points.Count > maxPoints)
            points.RemoveAt(0);

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    public void Activate()
    {
        points.Clear();
        line.positionCount = 0;
        line.enabled = false;
        isActive = true;
    }

    public void Deactivate()
    {
        isActive = false;
        line.enabled = false;
        points.Clear();
        line.positionCount = 0;
    }

    // ✅ NUEVO: para copiar/pegar trayectorias
    public Vector3[] GetPoints()
    {
        return points.ToArray();
    }

    public void SetPoints(Vector3[] newPoints, bool enableLine = true)
    {
        points.Clear();
        points.AddRange(newPoints);

        line.positionCount = points.Count;
        if (points.Count > 0)
            line.SetPositions(points.ToArray());

        line.enabled = enableLine;
        isActive = false; // importante: esta línea NO debería seguir capturando si es "prev"
    }

    public void Clear(bool disableLine = true)
    {
        points.Clear();
        line.positionCount = 0;
        timer = 0f;

        if (disableLine) line.enabled = false;
    }
}
