using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// BlackoutManager — Diegetic campus-wide power blackout system.
/// 
/// At random intervals the entire building's power cuts out:
///   - All room lights, hallway lights, ceiling fixtures are shut off.
///   - Ambient light drops to near-zero pitch black.
///   - The FNAF power outage sound plays diegetically (terrifying wind-down).
///   - Player must use their flashlight to navigate.
/// 
/// Power is restored after a dramatic delay:
///   - The reversed FNAF power surge sound charges up.
///   - Lights flicker and stutter back to life with an electrical pop.
///   - Ambient light is restored to original values.
/// 
/// Exposes IsBlackoutActive and events for future enemy/entity AI hooks.
/// Press F7 in Editor/Play mode to trigger an instant test blackout.
/// </summary>
public class BlackoutManager : MonoBehaviour
{
    public static BlackoutManager Instance { get; private set; }

    // ── Events for enemy AI hooks ─────────────────────────────────────────────
    /// <summary>Fired when the power cuts out (start of blackout).</summary>
    public event Action OnBlackoutStart;
    /// <summary>Fired when the power is fully restored.</summary>
    public event Action OnBlackoutEnd;

    // ── Public State ──────────────────────────────────────────────────────────
    /// <summary>True while the blackout is active (lights off, pitch black).</summary>
    public bool IsBlackoutActive { get; private set; }

    // ── Timing ────────────────────────────────────────────────────────────────
    [Header("Blackout Timing")]
    [Tooltip("Minimum seconds between blackouts.")]
    public float intervalMin = 90f;
    [Tooltip("Maximum seconds between blackouts.")]
    public float intervalMax = 180f;
    [Tooltip("Minimum duration the blackout lasts before power restoration starts.")]
    public float blackoutDurationMin = 18f;
    [Tooltip("Maximum duration the blackout lasts before power restoration starts.")]
    public float blackoutDurationMax = 24f;

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("FNAF Power Outage sound (power cuts out). Leave null to auto-load from Resources.")]
    public AudioClip powerDownClip;
    [Tooltip("Reversed FNAF power surge sound (power comes back). Leave null to auto-load from Resources.")]
    public AudioClip powerUpClip;
    [Range(0f, 1f)]
    public float powerDownVolume = 0.88f;
    [Range(0f, 1f)]
    public float powerUpVolume = 0.80f;

    // ── Ambient Light ─────────────────────────────────────────────────────────
    [Header("Ambient Darkness")]
    [Tooltip("Ambient color during blackout (pitch black).")]
    public Color blackoutAmbientColor = new Color(0.002f, 0.002f, 0.004f, 1f);
    [Tooltip("Ambient intensity during blackout (0 = pitch black).")]
    public float blackoutAmbientIntensity = 0f;

    // ── Flashlight Tuning ─────────────────────────────────────────────────────
    [Header("Flashlight Tuning for Blackout Atmosphere")]
    [Tooltip("Intensity of the flashlight light during and outside of blackouts.")]
    public float flashlightIntensity = 2.0f;
    [Tooltip("Range of the flashlight beam.")]
    public float flashlightRange = 25f;
    [Tooltip("Outer spot angle of the flashlight.")]
    public float flashlightSpotAngle = 56f;
    [Tooltip("Inner spot angle (crisp core beam).")]
    public float flashlightInnerAngle = 26f;
    [Tooltip("Flashlight color — warm halogen tint to contrast the cold building darkness.")]
    public Color flashlightColor = new Color(1.0f, 0.97f, 0.92f, 1.0f);

    // ── Debug ─────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [Tooltip("Press F7 to trigger an immediate blackout for testing.")]
    public KeyCode debugTriggerKey = KeyCode.F7;
    public bool disableBlackouts = false;

    // ── Private state ─────────────────────────────────────────────────────────
    private struct LightState
    {
        public Light light;
        public float originalIntensity;
        public bool originalEnabled;
    }

    private List<LightState> _cachedLights = new List<LightState>();
    private Color _originalAmbientColor;
    private float _originalAmbientIntensity;
    private AmbientMode _originalAmbientMode;
    private Material _originalSkybox;
    private Light _mainSunLight;
    private float _originalSunIntensity;

    private AudioSource _audioSource;
    private Light _flashlightLight;
    private Coroutine _blackoutCoroutine;
    private Coroutine _scheduleCoroutine;

    // Names of lights to KEEP on during blackouts (emergency / player flashlight / main pipeline sun)
    private static readonly HashSet<string> _protectedLightNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FlashlightLight",
        "StatusLED",
        "ExitSign_RedLight",
        "PaperGlintLight",
        "Outdoor_Gate_Illumination",
        "Directional Light"
    };

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_EDITOR
        // Disable async shader compilation placeholder (prevents cyan/dummy shader artifacts in Editor)
        UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif

        // Audio source — 2D ambient so it fills the whole building equally
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // pure 2D
        _audioSource.loop = false;
        _audioSource.volume = 1f;
        _audioSource.priority = 0; // highest priority — never gets stolen

        // Load audio from Resources if not assigned
        if (powerDownClip == null)
            powerDownClip = Resources.Load<AudioClip>("Sounds/FNAF_ Power Outage - Gaming Sound Effect (HD)");
        if (powerUpClip == null)
            powerUpClip = Resources.Load<AudioClip>("Sounds/FNAF_Power_On_Short");
    }

    private void Start()
    {
        // Cache ambient and skybox settings
        _originalAmbientColor = RenderSettings.ambientLight;
        _originalAmbientIntensity = RenderSettings.ambientIntensity;
        _originalAmbientMode = RenderSettings.ambientMode;
        _originalSkybox = RenderSettings.skybox;

        // Cache main sun directional light
        _mainSunLight = RenderSettings.sun;
        if (_mainSunLight == null)
        {
            var sunGO = GameObject.Find("Outdoor_Gate_Illumination") ?? GameObject.Find("Directional Light");
            if (sunGO != null) _mainSunLight = sunGO.GetComponent<Light>();
        }
        if (_mainSunLight != null)
        {
            _originalSunIntensity = _mainSunLight.intensity;
        }

        // Tune the flashlight now (and keep it tuned)
        TuneFlashlight();

        // Cache all scene lights (excluding protected ones)
        CacheLights();

        // Kick off the random blackout schedule
        if (!disableBlackouts)
            _scheduleCoroutine = StartCoroutine(BlackoutSchedule());
    }

    private void Update()
    {
        // Debug: instant blackout trigger
        if (Input.GetKeyDown(debugTriggerKey) && !IsBlackoutActive)
        {
            TriggerBlackout();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Immediately triggers a blackout (usable by other systems / enemy AI).
    /// </summary>
    public void TriggerBlackout()
    {
        if (IsBlackoutActive) return;
        if (_blackoutCoroutine != null) StopCoroutine(_blackoutCoroutine);
        float duration = UnityEngine.Random.Range(blackoutDurationMin, blackoutDurationMax);
        _blackoutCoroutine = StartCoroutine(BlackoutSequence(duration));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core Coroutines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator BlackoutSchedule()
    {
        // Short startup grace period so the player isn't immediately hit on load
        yield return new WaitForSeconds(UnityEngine.Random.Range(30f, 60f));

        while (true)
        {
            float waitTime = UnityEngine.Random.Range(intervalMin, intervalMax);
            yield return new WaitForSeconds(waitTime);

            float duration = UnityEngine.Random.Range(blackoutDurationMin, blackoutDurationMax);
            yield return BlackoutSequence(duration);
        }
    }

    /// <summary>
    /// Restores power to the building, triggering the flickering light surge and ambient recovery.
    /// Called when The Proctor either catches the player or when the blackout chase timer expires.
    /// </summary>
    public void RestorePower()
    {
        if (!IsBlackoutActive) return;
        if (_blackoutCoroutine != null) StopCoroutine(_blackoutCoroutine);
        _blackoutCoroutine = StartCoroutine(PowerRestorationRoutine());
    }

    private IEnumerator BlackoutSequence(float duration)
    {
        IsBlackoutActive = true;
        OnBlackoutStart?.Invoke();

        // ── PHASE 1: Voltage stutter (2 frames) ──────────────────────────────
        // Flicker all lights down and back twice to simulate voltage drop
        SetAllLightIntensityMultiplier(0.3f);
        yield return null;
        SetAllLightIntensityMultiplier(1.0f);
        yield return null;
        SetAllLightIntensityMultiplier(0.1f);
        yield return null;

        // ── PHASE 2: Power cut — play diegetic sound ──────────────────────────
        if (powerDownClip != null)
            _audioSource.PlayOneShot(powerDownClip, powerDownVolume);

        // Kill all non-protected lights
        SetAllLightsEnabled(false);

        // Keep directional Sun light enabled in URP pipeline but set intensity to 0 (true blackness)
        if (_mainSunLight != null)
        {
            _mainSunLight.intensity = 0f;
        }

        // Drop ambient and clear skybox to pitch black so no blue-white sky leaks into indoor textures
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.skybox = null;

        // ── PHASE 3: Darkness hold ────────────────────────────────────────────
        // If TheProctorManager is active, it controls when RestorePower() is invoked (chase timer / catch).
        // Otherwise, wait for duration as fallback.
        if (TheProctorManager.Instance == null)
        {
            float holdTime = Mathf.Max(1f, duration - 5f);
            yield return new WaitForSeconds(holdTime);
            yield return PowerRestorationRoutine();
        }
    }

    private IEnumerator PowerRestorationRoutine()
    {
        // ── PHASE 4: Power restoration wind-up ───────────────────────────────
        // Play the reversed power-up sound as the building power charges back up
        if (powerUpClip != null)
            _audioSource.PlayOneShot(powerUpClip, powerUpVolume);

        // Dramatic flicker: lights stutter twice before coming fully back
        yield return new WaitForSeconds(1.5f);
        // First flicker attempt
        SetAllLightsEnabled(true);
        SetAllLightIntensityMultiplier(0.15f);
        yield return new WaitForSeconds(0.08f);
        SetAllLightsEnabled(false);
        yield return new WaitForSeconds(0.25f);
        // Second flicker attempt
        SetAllLightsEnabled(true);
        SetAllLightIntensityMultiplier(0.4f);
        yield return new WaitForSeconds(0.1f);
        SetAllLightsEnabled(false);
        yield return new WaitForSeconds(0.18f);
        // Breaker slams shut — lights pop back at full intensity
        SetAllLightsEnabled(true);
        SetAllLightIntensityMultiplier(1.0f);

        // Restore directional Sun intensity
        if (_mainSunLight != null)
        {
            _mainSunLight.intensity = _originalSunIntensity;
        }

        // Restore ambient and skybox
        RenderSettings.skybox = _originalSkybox;
        RenderSettings.ambientMode = _originalAmbientMode;
        RenderSettings.ambientLight = _originalAmbientColor;
        RenderSettings.ambientIntensity = _originalAmbientIntensity;

        // Notify listeners
        IsBlackoutActive = false;
        OnBlackoutEnd?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Light Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheLights()
    {
        _cachedLights.Clear();
        var allLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (var l in allLights)
        {
            if (l == null) continue;
            // Skip protected lights (flashlight, status LEDs, exit signs, etc.)
            if (_protectedLightNames.Contains(l.gameObject.name)) continue;
            _cachedLights.Add(new LightState
            {
                light = l,
                originalIntensity = l.intensity,
                originalEnabled = l.enabled,
            });
        }
        Debug.Log($"[BlackoutManager] Cached {_cachedLights.Count} scene lights for blackout control.");
    }

    private void SetAllLightsEnabled(bool enabled)
    {
        for (int i = 0; i < _cachedLights.Count; i++)
        {
            var ls = _cachedLights[i];
            if (ls.light == null) continue;
            // Restore only lights that were originally enabled (don't turn on lights that were off before)
            if (enabled)
            {
                if (ls.originalEnabled)
                    ls.light.enabled = true;
            }
            else
            {
                ls.light.enabled = false;
            }
        }
    }

    private void SetAllLightIntensityMultiplier(float multiplier)
    {
        for (int i = 0; i < _cachedLights.Count; i++)
        {
            var ls = _cachedLights[i];
            if (ls.light == null) continue;
            if (ls.light.enabled)
                ls.light.intensity = ls.originalIntensity * multiplier;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Flashlight Tuning
    // ─────────────────────────────────────────────────────────────────────────

    private void TuneFlashlight()
    {
        // Try to find the flashlight via FlashlightController first
        if (FlashlightController.Instance != null && FlashlightController.Instance.flashlightLight != null)
        {
            _flashlightLight = FlashlightController.Instance.flashlightLight;
        }
        else
        {
            // Fallback: find by name
            var go = GameObject.Find("FlashlightLight");
            if (go != null) _flashlightLight = go.GetComponent<Light>();
        }

        if (_flashlightLight == null)
        {
            Debug.LogWarning("[BlackoutManager] Could not find FlashlightLight — skipping flashlight tuning.");
            return;
        }

        _flashlightLight.intensity    = flashlightIntensity;
        _flashlightLight.range        = flashlightRange;
        _flashlightLight.spotAngle    = flashlightSpotAngle;
        _flashlightLight.innerSpotAngle = flashlightInnerAngle;
        _flashlightLight.color        = flashlightColor;
        _flashlightLight.shadows      = LightShadows.None;

        Debug.Log($"[BlackoutManager] Flashlight tuned — intensity: {flashlightIntensity}, range: {flashlightRange}, spot: {flashlightSpotAngle}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cleanup on Destroy
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        // Restore ambient, skybox, sun if we get destroyed mid-blackout
        if (IsBlackoutActive)
        {
            SetAllLightsEnabled(true);
            SetAllLightIntensityMultiplier(1.0f);
            if (_originalSkybox != null)
                RenderSettings.skybox = _originalSkybox;
            if (_mainSunLight != null)
                _mainSunLight.intensity = _originalSunIntensity;
            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientLight = _originalAmbientColor;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
        }
    }
}
