using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGMController : MonoBehaviour
{
    private static BGMController instance;

    void Awake()
    {
        // Enforce Singleton pattern so the BGM continues seamlessly when switching scenes
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            AudioSource source = GetComponent<AudioSource>();
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else
        {
            // If another BGMController exists, destroy this duplicate
            Destroy(gameObject);
        }
    }
}
