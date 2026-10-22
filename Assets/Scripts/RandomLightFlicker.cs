using System.Collections;
using UnityEngine;

/// <summary>
/// Realistic fluorescent ceiling light flickering — KLIRANS Horror Lighting System.
/// 
/// Features:
///   - Four behavior patterns: DyingBallast (erratic stutter), PowerSurgeDrop (long blackout),
///     DeadZone (permanently burned out), StableLow (moody steady low glow).
///   - Controls ALL child Light components on the fixture simultaneously.
///   - True ON/OFF: lights can reach intensity 0 and be fully disabled during blackout pauses.
///   - Neon tube mesh emission synced via MaterialPropertyBlock (_EmissionColor) — no material instances.
///   - Weighted random pattern assignment at runtime (override per-fixture in Inspector).
/// </summary>
public class RandomLightFlicker : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Public Enums & Pattern Settings
    // ─────────────────────────────────────────────────────────────────────────

    public enum FlickerPattern
    {
        /// <summary>Assigned randomly at Start() using weighted probability.</summary>
        Auto,
        /// <summary>Rapid micro-stutters, occasional dim dips. Classic dying fluorescent ballast.</summary>
        DyingBallast,
        /// <summary>Stable for seconds then blinks rapidly before a 1-4s full blackout.</summary>
        PowerSurgeDrop,
        /// <summary>Burned out — stays fully off forever. Pure dead zone.</summary>
        DeadZone,
        /// <summary>No flicker. Just low-intensity stable ambient glow.</summary>
        StableLow,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Behavior Pattern")]
    [Tooltip("Auto = randomly assigned at Start. Override to force a specific pattern on this fixture.")]
    public FlickerPattern pattern = FlickerPattern.Auto;

    [Header("Auto Pattern Weights (only used when pattern = Auto)")]
    [Range(0f, 1f)] [Tooltip("Chance of DyingBallast (erratic stutter).")]
    public float weightDyingBallast = 0.15f;
    [Range(0f, 1f)] [Tooltip("Chance of PowerSurgeDrop (long blackout pause).")]
    public float weightPowerSurgeDrop = 0.30f;
    [Range(0f, 1f)] [Tooltip("Chance of DeadZone (burned out, always off).")]
    public float weightDeadZone = 0.55f;
    [Range(0f, 1f)] [Tooltip("Chance of StableLow (dim but steady — no flicker).")]
    public float weightStableLow = 0.00f;

    [Header("Base Light Intensity (when ON)")]
    [Tooltip("PointLight base intensity when fully lit. Set by batch calibration script; override here per-fixture.")]
    public float basePointIntensity = 0.20f;
    [Tooltip("UpSpotLight base intensity when fully lit.")]
    public float baseSpotIntensity = 0.06f;

    [Header("Stable Low Intensity Multiplier")]
    [Tooltip("StableLow pattern sets lights to this fraction of their base intensity.")]
    [Range(0.05f, 0.6f)]
    public float stableLowMultiplier = 0.25f;

    [Header("Emissive Material Sync")]
    [Tooltip("MeshRenderers whose _EmissionColor tracks the light state. Auto-filled from children.")]
    public MeshRenderer[] emissiveRenderers;

    [Tooltip("Emission brightness multiplier for fluorescent tube mesh.")]
    [Range(0.05f, 1.5f)]
    public float emissionMultiplier = 0.40f;

    [Header("Debug")]
    public bool showDebugLog = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Light[] _lights;
    private float[] _baseLightIntensities;
    private Color _baseEmissionColor;
    private bool _hasEmission;
    private MaterialPropertyBlock _propBlock;
    private Coroutine _behaviorCoroutine;
    private FlickerPattern _activePattern;

    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Gather all child Light components
        _lights = GetComponentsInChildren<Light>(true);

        // Compute base intensities from the live light values (before calibration may override them)
        _baseLightIntensities = new float[_lights.Length];
        for (int i = 0; i < _lights.Length; i++)
        {
            _baseLightIntensities[i] = _lights[i].intensity > 0.01f ? _lights[i].intensity : basePointIntensity;
        }

        // Auto-collect emissive renderers from children
        if (emissiveRenderers == null || emissiveRenderers.Length == 0)
        {
            emissiveRenderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        // Grab original emission color from the first valid renderer
        _propBlock = new MaterialPropertyBlock();
        _hasEmission = false;
        foreach (var r in emissiveRenderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            if (r.sharedMaterial.HasProperty(EmissionColorProp))
            {
                _baseEmissionColor = r.sharedMaterial.GetColor(EmissionColorProp);
                // If emission is black, synthesize a dim sickly fluorescent glow
                if (_baseEmissionColor == Color.black || _baseEmissionColor.maxColorComponent < 0.05f)
                {
                    _baseEmissionColor = new Color(0.85f, 0.90f, 0.82f, 1.0f) * 0.75f;
                }
                _hasEmission = true;
                break;
            }
        }
    }

    private void Start()
    {
        _activePattern = ResolvePattern();

        if (showDebugLog)
            Debug.Log($"[RLF] {gameObject.name} assigned pattern: {_activePattern}");

        _behaviorCoroutine = StartCoroutine(RunPattern(_activePattern));
    }

    private void OnDestroy()
    {
        if (_behaviorCoroutine != null) StopCoroutine(_behaviorCoroutine);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pattern Resolution
    // ─────────────────────────────────────────────────────────────────────────

    private FlickerPattern ResolvePattern()
    {
        if (pattern != FlickerPattern.Auto) return pattern;

        float total = weightDyingBallast + weightPowerSurgeDrop + weightDeadZone + weightStableLow;
        if (total <= 0f) return FlickerPattern.StableLow;

        float roll = Random.value * total;
        float acc = 0f;

        acc += weightDyingBallast;
        if (roll < acc) return FlickerPattern.DyingBallast;
        acc += weightPowerSurgeDrop;
        if (roll < acc) return FlickerPattern.PowerSurgeDrop;
        acc += weightDeadZone;
        if (roll < acc) return FlickerPattern.DeadZone;

        return FlickerPattern.StableLow;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pattern Coroutines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunPattern(FlickerPattern p)
    {
        switch (p)
        {
            case FlickerPattern.DyingBallast:
                yield return StartCoroutine(DyingBallastRoutine());
                break;
            case FlickerPattern.PowerSurgeDrop:
                yield return StartCoroutine(PowerSurgeDropRoutine());
                break;
            case FlickerPattern.DeadZone:
                yield return StartCoroutine(DeadZoneRoutine());
                break;
            default:
                yield return StartCoroutine(StableLowRoutine());
                break;
        }
    }

    // ── DyingBallast ──────────────────────────────────────────────────────────
    // Rapid micro-stutters and occasional deep dips. Feels like a failing ballast
    // that keeps trying to warm up the tube but can't hold voltage.

    private IEnumerator DyingBallastRoutine()
    {
        // Initial delay: stagger fixture start times across the level
        yield return new WaitForSeconds(Random.Range(0f, 3f));

        while (true)
        {
            // Calm period between bursts (short and nervous)
            float calmDuration = Random.Range(0.6f, 2.2f);
            SetLightFraction(1.0f);
            yield return new WaitForSeconds(calmDuration);

            // Burst of micro-stutters
            int stutters = Random.Range(4, 12);
            for (int i = 0; i < stutters; i++)
            {
                // Random dip depth — deep drops into pitch darkness
                float dipFactor = Random.value < 0.45f ? 0f : Random.Range(0.02f, 0.30f);
                SetLightFraction(dipFactor);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));

                // Snap back ON briefly
                SetLightFraction(Random.Range(0.5f, 1.0f));
                yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
            }

            // Occasional deep recovery pause (light fully off momentarily)
            if (Random.value < 0.50f)
            {
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.20f, 0.85f));
                SetLightFraction(1.0f);
            }
        }
    }

    // ── PowerSurgeDrop ────────────────────────────────────────────────────────
    // Stable light → rapid warning flicker → full blackout for 1–4 seconds → restart.

    private IEnumerator PowerSurgeDropRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, 4f));

        while (true)
        {
            // Stable phase (short so lights constantly drop out and blackout)
            float stableMin = 1.6f;
            float stableMax = 4.5f;
            if (CampusThreatDirector.Instance != null)
            {
                float t = CampusThreatDirector.Instance.CurrentSignatures / 6f;
                stableMin = Mathf.Lerp(1.6f, 0.7f, t);
                stableMax = Mathf.Lerp(4.5f, 2.0f, t);
            }
            SetLightFraction(1.0f);
            yield return new WaitForSeconds(Random.Range(stableMin, stableMax));

            // Warning flicker (rapid erratic stuttering)
            int warnings = Random.Range(3, 7);
            for (int i = 0; i < warnings; i++)
            {
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
                SetLightFraction(Random.Range(0.4f, 1.0f));
                yield return new WaitForSeconds(Random.Range(0.02f, 0.07f));
            }

            // BLACKOUT: long dark pause where flashlight is vital
            SetLightFraction(0f);
            yield return new WaitForSeconds(Random.Range(1.8f, 5.0f));

            // Struggling restart flickers
            int restarts = Random.Range(3, 6);
            for (int i = 0; i < restarts; i++)
            {
                SetLightFraction(Random.Range(0.10f, 0.45f));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.16f));
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.10f));
            }

            // Finally stabilize
            SetLightFraction(1.0f);
        }
    }

    // ── DeadZone ──────────────────────────────────────────────────────────────
    // Permanently off. Burned-out tube. Creates permanent pitch-dark zones in hallways.

    private IEnumerator DeadZoneRoutine()
    {
        // One final death flicker
        yield return new WaitForSeconds(Random.Range(0f, 2f));
        int deathFlickers = Random.Range(1, 4);
        for (int i = 0; i < deathFlickers; i++)
        {
            SetLightFraction(Random.Range(0.05f, 0.4f));
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            SetLightFraction(0f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.20f));
        }
        SetLightFraction(0f); // Forever off
    }

    // ── StableLow ─────────────────────────────────────────────────────────────
    // No flicker. Just a dim, steady low-intensity glow. Creates moody fill without event.

    private IEnumerator StableLowRoutine()
    {
        SetLightFraction(stableLowMultiplier);
        yield break; // Set once, done. No loop needed.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core Light State Setter
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all child lights and emissive tube material to the given fraction (0 = off, 1 = full base intensity).
    /// fraction below 0.01 → lights disabled entirely (true blackout).
    /// </summary>
    public void SetLightFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        bool lightsOn = fraction >= 0.01f;

        for (int i = 0; i < _lights.Length; i++)
        {
            if (_lights[i] == null) continue;
            _lights[i].enabled = lightsOn;
            _lights[i].intensity = lightsOn ? _baseLightIntensities[i] * fraction : 0f;
        }

        SetEmission(fraction);
    }

    /// <summary>
    /// Call this after the batch calibration script lowers base intensities so the flicker still
    /// scales correctly relative to the new calibrated base.
    /// </summary>
    public void SetBaseIntensities(float pointIntensity, float spotIntensity)
    {
        basePointIntensity = pointIntensity;
        baseSpotIntensity = spotIntensity;

        for (int i = 0; i < _lights.Length; i++)
        {
            if (_lights[i] == null) continue;
            if (_lights[i].name == "PointLight")
                _baseLightIntensities[i] = pointIntensity;
            else if (_lights[i].name == "UpSpotLight" || _lights[i].name == "RoomLight")
                _baseLightIntensities[i] = spotIntensity;
            else
                _baseLightIntensities[i] = pointIntensity; // default fallback
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Emissive Sync via MaterialPropertyBlock
    // ─────────────────────────────────────────────────────────────────────────

    private void SetEmission(float fraction)
    {
        if (!_hasEmission || _propBlock == null) return;

        Color emission = _baseEmissionColor * (fraction * emissionMultiplier);

        foreach (var r in emissiveRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(EmissionColorProp, emission);
            r.SetPropertyBlock(_propBlock);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public Utilities
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the currently active resolved pattern.</summary>
    public FlickerPattern ActivePattern => _activePattern;
}
