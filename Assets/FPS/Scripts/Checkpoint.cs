using UnityEngine;
using Unity.FPS.Game;


public class Checkpoint : MonoBehaviour
{
    [Header("Adaptive Activation (Optional)")]
    public bool activateAdaptiveMusic = false;
    public MultitaskPitchController pitchController;
    [Header("Disable Any Adaptive System (Optional)")]
    public bool disableAdaptiveSystem = false;
    public Behaviour adaptiveSystemToDisable;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CheckpointManager.Instance.SetCheckpoint(transform.position, transform.rotation);

            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.RegisterCheckpoint(gameObject.name);
            }

            //Debug.Log("Checkpoint activado en: " + transform.position);
            // NUEVO BLOQUE
            if (activateAdaptiveMusic && pitchController != null)
            {
                pitchController.ActivateAdaptiveMusic();
                Debug.Log("Adaptive Music Activated from checkpoint.");
            }
            if (disableAdaptiveSystem && adaptiveSystemToDisable != null)
            {
                adaptiveSystemToDisable.enabled = false;
                Debug.Log("Adaptive system disabled from checkpoint.");
            }

        }
    }

}
