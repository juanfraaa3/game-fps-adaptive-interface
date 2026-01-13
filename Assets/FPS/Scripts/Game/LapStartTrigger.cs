using UnityEngine;

public class LapStartTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var logger = other.GetComponentInParent<MultitaskMetricsLogger>();
        if (logger != null)
        {
            logger.OpenLap();
        }
    }
}
