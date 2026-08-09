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

    private MaterialPropertyBlock propBlock;
    private Color originalEmissionColor = Color.black;
    private bool hasOriginalEmission = false;
    private bool isFlickeringActive = false;

    private float nextFlickerTime;
    private bool isCurrentlyBlinking;
    private int blinksRemaining;
    private float nextBlinkChangeTime;
    private bool isLightDip;

    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

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
            propBlock = new MaterialPropertyBlock();
            Material mat = targetRenderer.sharedMaterial;
            if (mat.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = mat.GetColor("_EmissionColor");
                hasOriginalEmission = originalEmissionColor != Color.black;
            }
        }
    }

    private void Start()
    {
        if (Random.value <= flickerProbability)
        {
            isFlickeringActive = true;
            ScheduleNextFlicker();
        }
    }

    private void Update()
    {
        if (!isFlickeringActive) return;

        if (!isCurrentlyBlinking)
        {
            if (Time.time >= nextFlickerTime)
            {
                isCurrentlyBlinking = true;
                blinksRemaining = Random.Range(2, 6);
                StartNextBlinkSequence();
            }
        }
        else
        {
            if (Time.time >= nextBlinkChangeTime)
            {
                if (isLightDip)
                {
                    if (Random.value > 0.4f)
                    {
                        SetLightState(normalIntensity, 1.0f);
                        isLightDip = false;
                        nextBlinkChangeTime = Time.time + Random.Range(0.02f, 0.08f);
                    }
                    else
                    {
                        ProceedToNextBlink();
                    }
                }
                else
                {
                    ProceedToNextBlink();
                }
            }
        }
    }

    private void ProceedToNextBlink()
    {
        blinksRemaining--;
        if (blinksRemaining <= 0)
        {
            SetLightState(normalIntensity, 1.0f);
            isCurrentlyBlinking = false;
            ScheduleNextFlicker();
        }
        else
        {
            StartNextBlinkSequence();
        }
    }

    private void StartNextBlinkSequence()
    {
        float dimFactor = Random.Range(minFlickerMultiplier, 0.3f);
        SetLightState(normalIntensity * dimFactor, dimFactor);
        isLightDip = true;
        nextBlinkChangeTime = Time.time + Random.Range(0.03f, 0.12f);
    }

    private void ScheduleNextFlicker()
    {
        nextFlickerTime = Time.time + Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
    }

    private void SetLightState(float intensity, float emissionMultiplier)
    {
        if (targetLight != null)
        {
            targetLight.intensity = Mathf.Max(intensity, 0.5f);
            targetLight.enabled = true;
        }

        if (hasOriginalEmission && propBlock != null && targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(propBlock);
            Color currentEmission = originalEmissionColor * emissionMultiplier;
            propBlock.SetColor(EmissionColorProp, currentEmission);
            targetRenderer.SetPropertyBlock(propBlock);
        }
    }
}
