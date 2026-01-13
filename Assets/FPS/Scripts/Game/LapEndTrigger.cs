using UnityEngine;

public class LapEndTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var logger = other.GetComponentInParent<MultitaskMetricsLogger>();
        if (logger != null)
        {
            logger.CloseLap(false);
        }
    }
}
