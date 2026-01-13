using UnityEngine;

public class SectionSegmentController : MonoBehaviour
{
    public GameObject[] metricsObjects;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        foreach (var obj in metricsObjects)
            obj.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        foreach (var obj in metricsObjects)
            obj.SetActive(false);
    }
}
