using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("Music Settings")]
    public AudioClip zoneMusic;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (zoneMusic != null)
            {
                MusicManager.Instance.PlayMusic(zoneMusic);
            }
        }
    }
}
