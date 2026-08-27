using UnityEngine;
using System.Collections;

public class RandomAmbience : MonoBehaviour
{
    [Tooltip("The ambience sound clips to play. A random one is chosen each time.")]
    public AudioClip[] ambienceClips;
    
    [Tooltip("Volume of the ambience sound.")]
    [Range(0f, 1f)]
    public float volume = 0.25f; 
    
    [Tooltip("Minimum time (in seconds) between ambience triggers.")]
    public float minDelay = 20f;
    
    [Tooltip("Maximum time (in seconds) between ambience triggers.")]
    public float maxDelay = 45f; 

    private AudioSource audioSource;

    private void Start()
    {
        // Setup a 2D AudioSource for global ambience
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; 
        audioSource.playOnAwake = false;
        
        StartCoroutine(PlayAmbienceRoutine());
    }

    private void PlayRandomClip()
    {
        if (ambienceClips != null && ambienceClips.Length > 0)
        {
            AudioClip clipToPlay = ambienceClips[Random.Range(0, ambienceClips.Length)];
            if (clipToPlay != null)
                audioSource.PlayOneShot(clipToPlay, volume);
        }
    }

    private IEnumerator PlayAmbienceRoutine()
    {
        // Play one immediately so the player hears ambience right away
        PlayRandomClip();

        while (true)
        {
            float waitTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(waitTime);

            PlayRandomClip();
        }
    }
}
