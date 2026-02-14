using UnityEngine;

public class AxisDebugger : MonoBehaviour
{
    void Update()
    {
        // Muestra cualquier axis que se esté moviendo "harto"
        for (int axisIndex = 1; axisIndex <= 10; axisIndex++)
        {
            float v = Input.GetAxis("Axis " + axisIndex);
            if (Mathf.Abs(v) > 0.35f)
            {
                Debug.Log($"Axis {axisIndex} = {v:0.00}");
            }
        }
    }
}
