using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the pre-built "restroom droplets" clip occasionally with random
/// silence gaps between plays. The clip itself contains the drip sequence;
/// after it finishes, the emitter waits a random number of seconds before
/// playing again, so drips feel present but not constant.
/// Fully 3-D spatial: loud at the sinks, inaudible down the hallway.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RestroomDripAudio : MonoBehaviour
{
    [Header("Clip")]
    [Tooltip("The restroom droplets AudioClip - the full pre-built drip sequence.")]
    public AudioClip dripClip;

    [Header("Volume")]
    [Tooltip("Volume at the sinks. Audible but will not drown out footsteps or BGM.")]
    [Range(0f, 1f)]
    public float volume = 0.55f;

    [Header("Playback Gaps")]
    [Tooltip("Minimum seconds of silence between clip plays.")]
    public float gapMin = 18f;

    [Tooltip("Maximum seconds of silence between clip plays.")]
    public float gapMax = 40f;

    [Header("3-D Audio Range")]
    [Tooltip("Full volume within this distance from the sink.")]
    public float minDistance = 1.0f;

    [Tooltip("Completely inaudible beyond this distance.")]
    public float maxDistance = 12.0f;

    // -------------------------------------------------
    private AudioSource _src;

    private void Awake()
    {
        _src = GetComponent<AudioSource>();

        _src.clip        = dripClip;
        _src.loop        = false;       // play once then go silent
        _src.playOnAwake = false;

        // Full 3-D spatial - logarithmic rolloff with distance
        _src.spatialBlend = 1f;
        _src.rolloffMode  = AudioRolloffMode.Logarithmic;
        _src.minDistance  = minDistance;
        _src.maxDistance  = maxDistance;

        _src.volume       = volume;
        _src.pitch        = 1f;
        _src.dopplerLevel = 0f;
        _src.spread       = 30f;
    }

    private void Start()
    {
        if (dripClip == null) return;
        // Stagger first play so restrooms don't start at the same time
        float firstDelay = Random.Range(2f, gapMax * 0.5f);
        StartCoroutine(PlayLoop(firstDelay));
    }

    private IEnumerator PlayLoop(float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            // Play the clip once
            _src.pitch = Random.Range(0.97f, 1.03f); // tiny variation, not noticeable
            _src.Play();

            // Wait for the clip to finish
            yield return new WaitForSeconds(dripClip.length);

            // Then wait a random silence gap before next play
            float gap = Random.Range(gapMin, gapMax);
            yield return new WaitForSeconds(gap);
        }
    }
}
