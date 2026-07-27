using System.Collections;
using UnityEngine;

/// <summary>
/// Simulates realistic random flickering for fluorescent ceiling lights.
/// Controls Light component intensity and safely syncs emissive materials if enabled.
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
    public float normalIntensity = 3.5f;

    [Tooltip("Minimum intensity multiplier during a flicker dip.")]
    public float minFlickerMultiplier = 0.05f;

    private Material instancedMaterial;
    private Color originalEmissionColor = Color.black;
    private bool hasOriginalEmission = false;
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
            normalIntensity = targetLight.intensity > 0 ? targetLight.intensity : 3.5f;
        }

        if (targetRenderer != null && targetRenderer.sharedMaterial != null)
        {
            instancedMaterial = targetRenderer.material;
            if (instancedMaterial.HasProperty("_EmissionColor") && instancedMaterial.IsKeywordEnabled("_EMISSION"))
            {
                originalEmissionColor = instancedMaterial.GetColor("_EmissionColor");
                hasOriginalEmission = originalEmissionColor != Color.black;
            }
        }
    }

    private void Start()
    {
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
            float waitTime = Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
            yield return new WaitForSeconds(waitTime);

            int numBlinks = Random.Range(2, 6);
            for (int i = 0; i < numBlinks; i++)
            {
                float dimFactor = Random.Range(minFlickerMultiplier, 0.3f);
                SetLightState(normalIntensity * dimFactor, dimFactor);

                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));

                if (Random.value > 0.4f)
                {
                    SetLightState(normalIntensity, 1.0f);
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
                }
            }

            SetLightState(normalIntensity, 1.0f);
        }
    }

    private void SetLightState(float intensity, float emissionMultiplier)
    {
        if (targetLight != null)
        {
            targetLight.intensity = intensity;
            targetLight.enabled = intensity > 0.05f;
        }

        if (hasOriginalEmission && instancedMaterial != null && instancedMaterial.HasProperty("_EmissionColor"))
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
