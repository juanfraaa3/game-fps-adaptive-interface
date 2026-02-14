using UnityEngine;
using System.Collections.Generic;

public class AdaptiveBehaviourManager : MonoBehaviour
{
    [Header("MASTER SWITCH")]
    public bool adaptiveEnabled = true;

    [Header("Drag ALL adaptive scripts here")]
    public List<MonoBehaviour> adaptiveScripts = new List<MonoBehaviour>();

    void Start()
    {
        ApplyState();
    }

    public void ApplyState()
    {
        foreach (var script in adaptiveScripts)
        {
            if (script != null)
                script.enabled = adaptiveEnabled;
        }

        Debug.Log("Adaptive Mode: " + adaptiveEnabled);
    }

    public void ToggleAdaptive()
    {
        adaptiveEnabled = !adaptiveEnabled;
        ApplyState();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleAdaptive();
        }
    }
}
