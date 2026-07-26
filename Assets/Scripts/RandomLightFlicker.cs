using System.Collections;
using UnityEngine;

/// <summary>
/// Simulates realistic random flickering for fluorescent ceiling lights.
/// Controls both the Light component intensity and the MeshRenderer emissive material.
/// </summary>
public class RandomLightFlicker : MonoBehaviour
{
    [Header("Light References")]
    [Tooltip("The Light component to flicker. If null, will search in children.")]
    public Light targetLight;

    [Tooltip("The MeshRenderer with the emissive light material. If null, will search on this GameObject.")]
    public MeshRenderer targetRenderer;

    [Header("Flicker Timing")]
    [Tooltip("Minimum time (in seconds) between flicker events.")]
    public float minTimeBetweenFlickers = 2.0f;

    [Tooltip("Maximum time (in seconds) between flicker events.")]
    public float maxTimeBetweenFlickers = 8.0f;

    [Header("Flicker Properties")]
    [Tooltip("Probability (0 to 1) that this light is a flickering light. Set to 1.0 for guaranteed flickering.")]
    [Range(0f, 1f)]
    public float flickerProbability = 0.35f;

    [Tooltip("Base light intensity when fully ON.")]
    public float normalIntensity = 8.0f;

    [Tooltip("Minimum intensity multiplier during a flicker dip.")]
    public float minFlickerMultiplier = 0.05f;

    private Material instancedMaterial;
    private Color originalEmissionColor = new Color(3f, 3f, 3f, 1f);
    private bool isFlickeringActive = false;

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        if (targetLight != null)
        {
            normalIntensity = targetLight.intensity > 0 ? targetLight.intensity : 8.0f;
        }

        if (targetRenderer != null && targetRenderer.sharedMaterial != null)
        {
            // Create instance so individual lights flicker their emission independently
            instancedMaterial = targetRenderer.material;
            if (instancedMaterial.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = instancedMaterial.GetColor("_EmissionColor");
                if (originalEmissionColor == Color.black)
                {
                    originalEmissionColor = new Color(3f, 3f, 3f, 1f);
                }
            }
        }
    }

    private void Start()
    {
        // Decide if this specific light fixture is one of the flickering ones
        if (Random.value <= flickerProbability)
        {
            isFlickeringActive = true;
            StartCoroutine(FlickerRoutine());
        }
    }

    private IEnumerator FlickerRoutine()
    {
        while (isFlickeringActive)
        {
            // Wait for next random interval
            float waitTime = Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
            yield return new WaitForSeconds(waitTime);

            // Trigger a flicker sequence (rapid bursts of dimming/blinking)
            int numBlinks = Random.Range(2, 6);
            for (int i = 0; i < numBlinks; i++)
            {
                float dimFactor = Random.Range(minFlickerMultiplier, 0.3f);
                SetLightState(normalIntensity * dimFactor, dimFactor);

                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));

                // Quick pulse back up or micro-gap
                if (Random.value > 0.4f)
                {
                    SetLightState(normalIntensity, 1.0f);
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
                }
            }

            // Restore full brightness after flicker burst
            SetLightState(normalIntensity, 1.0f);
        }
    }

    private void SetLightState(float intensity, float emissionMultiplier)
    {
        if (targetLight != null)
        {
            targetLight.intensity = intensity;
            targetLight.enabled = intensity > 0.1f;
        }

        if (instancedMaterial != null && instancedMaterial.HasProperty("_EmissionColor"))
        {
            Color currentEmission = originalEmissionColor * emissionMultiplier;
            instancedMaterial.SetColor("_EmissionColor", currentEmission);
            if (emissionMultiplier <= 0.05f)
            {
                instancedMaterial.DisableKeyword("_EMISSION");
            }
            else
            {
                instancedMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    private void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }
    }
}
