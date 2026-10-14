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
    public float weightDyingBallast = 0.42f;
    [Range(0f, 1f)] [Tooltip("Chance of PowerSurgeDrop (long blackout pause).")]
    public float weightPowerSurgeDrop = 0.20f;
    [Range(0f, 1f)] [Tooltip("Chance of DeadZone (burned out, always off).")]
    public float weightDeadZone = 0.13f;
    [Range(0f, 1f)] [Tooltip("Chance of StableLow (dim but steady — no flicker).")]
    public float weightStableLow = 0.25f;

    [Header("Base Light Intensity (when ON)")]
    [Tooltip("PointLight base intensity when fully lit. Set by batch calibration script; override here per-fixture.")]
    public float basePointIntensity = 0.60f;
    [Tooltip("UpSpotLight base intensity when fully lit.")]
    public float baseSpotIntensity = 0.20f;

    [Header("Stable Low Intensity Multiplier")]
    [Tooltip("StableLow pattern sets lights to this fraction of their base intensity.")]
    [Range(0.05f, 0.6f)]
    public float stableLowMultiplier = 0.35f;

    [Header("Emissive Material Sync")]
    [Tooltip("MeshRenderers whose _EmissionColor tracks the light state. Auto-filled from children.")]
    public MeshRenderer[] emissiveRenderers;

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
                // If emission is black, synthesize a warm neon white from the light color
                if (_baseEmissionColor == Color.black)
                {
                    _baseEmissionColor = new Color(0.92f, 0.95f, 1.0f, 1.0f) * 2.5f; // HDR warm white
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
        yield return new WaitForSeconds(Random.Range(0f, 4f));

        while (true)
        {
            // Calm period between bursts
            float calmDuration = Random.Range(2.5f, 9.0f);
            SetLightFraction(1.0f);
            yield return new WaitForSeconds(calmDuration);

            // Burst of micro-stutters
            int stutters = Random.Range(3, 10);
            for (int i = 0; i < stutters; i++)
            {
                // Random dip depth — sometimes just a flicker, sometimes near-off
                float dipFactor = Random.value < 0.3f ? Random.Range(0.0f, 0.08f) : Random.Range(0.05f, 0.45f);
                SetLightFraction(dipFactor);
                yield return new WaitForSeconds(Random.Range(0.018f, 0.085f));

                // Snap back ON
                SetLightFraction(1.0f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.07f));
            }

            // Occasional deep recovery pause (light fully off momentarily)
            if (Random.value < 0.35f)
            {
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.08f, 0.40f));
                SetLightFraction(1.0f);
            }
        }
    }

    // ── PowerSurgeDrop ────────────────────────────────────────────────────────
    // Stable light → rapid warning flicker → full blackout for 1–4 seconds → restart.

    private IEnumerator PowerSurgeDropRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, 6f));

        while (true)
        {
            // Stable phase
            SetLightFraction(1.0f);
            yield return new WaitForSeconds(Random.Range(5f, 18f));

            // Warning flicker (2–5 rapid blinks)
            int warnings = Random.Range(2, 6);
            for (int i = 0; i < warnings; i++)
            {
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                SetLightFraction(1.0f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }

            // BLACKOUT
            SetLightFraction(0f);
            yield return new WaitForSeconds(Random.Range(1.0f, 4.5f));

            // Struggling restart flickers
            int restarts = Random.Range(2, 5);
            for (int i = 0; i < restarts; i++)
            {
                SetLightFraction(Random.Range(0.15f, 0.55f));
                yield return new WaitForSeconds(Random.Range(0.06f, 0.20f));
                SetLightFraction(0f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
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

        Color emission = _baseEmissionColor * fraction;

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
