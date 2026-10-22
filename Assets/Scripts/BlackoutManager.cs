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
    private static BlackoutManager _instance;
    public static BlackoutManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<BlackoutManager>();
            return _instance;
        }
        private set { _instance = value; }
    }

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

    [Header("Dynamic Scaling with Clearance Signatures")]
    [Tooltip("If true, blackouts become progressively more frequent as the player collects more signatures on their clearance paper.")]
    public bool scaleFrequencyWithSignatures = true;

    [Tooltip("Minimum allowed interval between blackouts at maximum signatures (prevents excessive spam).")]
    public float minIntervalCap = 38f;

    [Tooltip("Maximum allowed interval between blackouts at maximum signatures.")]
    public float maxIntervalCap = 60f;

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
    [Tooltip("Ambient color during blackout (total pitch black).")]
    public Color blackoutAmbientColor = Color.black;
    [Tooltip("Ambient intensity during blackout (0 for pitch black).")]
    public float blackoutAmbientIntensity = 0f;

    // ── Flashlight Tuning ─────────────────────────────────────────────────────
    [Header("Flashlight Tuning for Blackout Atmosphere")]
    [Tooltip("Intensity of the flashlight light during and outside of blackouts.")]
    public float flashlightIntensity = 4.8f;
    [Tooltip("Range of the flashlight beam.")]
    public float flashlightRange = 38f;
    [Tooltip("Outer spot angle of the flashlight.")]
    public float flashlightSpotAngle = 68f;
    [Tooltip("Inner spot angle (crisp core beam).")]
    public float flashlightInnerAngle = 30f;
    [Tooltip("Flashlight color — cool white for modern tactical flashlight feel.")]
    public Color flashlightColor = new Color(0.96f, 0.98f, 1.0f, 1.0f);

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

    // Wandering student NPCs that are despawned during blackouts
    private List<GameObject> _cachedWanderingNPCs = new List<GameObject>();

    // Names of lights to KEEP on during blackouts (ONLY player flashlight - everything else shuts down!)
    private static readonly HashSet<string> _protectedLightNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FlashlightLight",
        "FlashlightSpillLight"
    };

    private Coroutine _voltageLoopCoroutine;
    private float _currentGridBaseMultiplier = 0.28f;

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

        // Cache wandering student NPCs for blackout despawning
        CacheWanderingNPCs();

        // Start campus brownout voltage & erratic flicker simulation
        _voltageLoopCoroutine = StartCoroutine(CampusGridVoltageLoop());

        // Kick off the random blackout schedule
        if (!disableBlackouts && !GameModeManager.IsFreeRoamMode)
            _scheduleCoroutine = StartCoroutine(BlackoutSchedule());
    }

    private void Update()
    {
        // Debug F7 shortcut: instant blackout trigger (Dev mode capability only in Free Roam)
        if (GameModeManager.IsFreeRoamMode && Input.GetKeyDown(debugTriggerKey) && !IsBlackoutActive)
        {
            TriggerBlackout(forceDev: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Immediately triggers a blackout (usable by other systems / enemy AI / dev mode).
    /// </summary>
    public void TriggerBlackout(bool forceDev = false)
    {
        if (GameModeManager.IsFreeRoamMode && !forceDev) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
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
        while (OpeningCutsceneDirector.IsAnyCutscenePlaying) yield return new WaitForSeconds(0.5f);

        // Short startup grace period so the player isn't immediately hit on load
        yield return new WaitForSeconds(UnityEngine.Random.Range(30f, 60f));

        while (true)
        {
            float waitTime = CalculateNextInterval();
            yield return new WaitForSeconds(waitTime);

            float duration = UnityEngine.Random.Range(blackoutDurationMin, blackoutDurationMax);
            yield return BlackoutSequence(duration);
        }
    }

    /// <summary>
    /// Computes the wait time until the next blackout based on how many signatures
    /// have been stamped on the clearance slip (0 to 6).
    /// As the player progresses through their clearance requirements, blackouts gradually
    /// become more frequent, creating rising tension without excessive spamming.
    /// </summary>
    public float CalculateNextInterval()
    {
        int signatures = 0;
        if (ClearanceManager.Instance != null)
        {
            signatures = Mathf.Clamp(ClearanceManager.Instance.SignatureCount, 0, 6);
        }

        if (CampusThreatDirector.Instance != null)
        {
            var p = CampusThreatDirector.Instance.CurrentProfile;
            float wait = UnityEngine.Random.Range(p.blackoutIntervalMin, p.blackoutIntervalMax);
            Debug.Log($"[BlackoutManager] Threat Level {signatures}/6 -> Next blackout scheduled in {wait:F1}s (Range: {p.blackoutIntervalMin:F0}s - {p.blackoutIntervalMax:F0}s)");
            return wait;
        }

        if (!scaleFrequencyWithSignatures || signatures <= 0)
        {
            return UnityEngine.Random.Range(intervalMin, intervalMax);
        }

        // Progression curve: 0/6 -> [intervalMin, intervalMax] (e.g. 90-180s)
        //                    6/6 -> [minIntervalCap, maxIntervalCap] (e.g. 38-60s)
        float factor = signatures / 6.0f;
        float scaledMin = Mathf.Lerp(intervalMin, minIntervalCap, factor);
        float scaledMax = Mathf.Lerp(intervalMax, maxIntervalCap, factor);

        float waitTime = UnityEngine.Random.Range(scaledMin, scaledMax);
        Debug.Log($"[BlackoutManager] Clearance Signatures: {signatures}/6 -> Next blackout scheduled in {waitTime:F1}s (Range: {scaledMin:F0}s - {scaledMax:F0}s)");
        return waitTime;
    }

    /// <summary>
    /// Restores power to the building, triggering the flickering light surge and ambient recovery.
    /// Called when The Proctor either catches the player or when the blackout chase timer expires.
    /// </summary>
    public void RestorePower()
    {
        // Always despawn any active horror entities the instant power restoration is triggered
        TheProctorAI.DespawnActiveProctor();
        WeepingStudentAI.DespawnActiveWeepingStudent();
        TheProctorManager.Instance?.CleanupProctor();

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
        SetAllLightIntensityMultiplier(0.1f);
        yield return null;
        SetAllLightIntensityMultiplier(0.3f);
        yield return null;
        SetAllLightIntensityMultiplier(0.02f);
        yield return null;

        // ── PHASE 2: Power cut — play diegetic sound ──────────────────────────
        if (powerDownClip != null)
            _audioSource.PlayOneShot(powerDownClip, powerDownVolume);

        // Despawn wandering student NPCs during blackout sequence
        DespawnWanderingNPCs();

        // Lock all doors during blackout sequence
        DoorInteract.LockAllDoorsForBlackout();

        // Kill ALL campus lights
        SetAllLightsEnabled(false);

        // Extinguish main directional Sun light & outdoor courtyard lights
        if (_mainSunLight != null)
        {
            _mainSunLight.intensity = 0f;
            _mainSunLight.enabled = false;
        }
        var outdoorSun = GameObject.Find("Outdoor_Gate_Illumination");
        if (outdoorSun != null)
        {
            var l = outdoorSun.GetComponent<Light>();
            if (l != null) { l.intensity = 0f; l.enabled = false; }
        }

        // True pitch-black environment — only player flashlight illuminates
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.skybox = null;

        // ── PHASE 3: Darkness hold ────────────────────────────────────────────
        if (TheProctorManager.Instance == null)
        {
            float holdTime = Mathf.Max(1f, duration - 5f);
            yield return new WaitForSeconds(holdTime);
            yield return PowerRestorationRoutine();
        }
    }

    private IEnumerator PowerRestorationRoutine()
    {
        // Immediately despawn any active entities as the generator hum kicks in
        TheProctorAI.DespawnActiveProctor();
        WeepingStudentAI.DespawnActiveWeepingStudent();
        TheProctorManager.Instance?.CleanupProctor();

        // ── PHASE 4: Power restoration wind-up ───────────────────────────────
        if (powerUpClip != null)
            _audioSource.PlayOneShot(powerUpClip, powerUpVolume);

        // Dramatic flicker: lights stutter twice before coming fully back
        yield return new WaitForSeconds(0.40f);
        SetAllLightsEnabled(true);
        SetAllLightIntensityMultiplier(0.12f);
        yield return new WaitForSeconds(0.08f);
        SetAllLightsEnabled(false);
        yield return new WaitForSeconds(0.25f);

        SetAllLightsEnabled(true);
        SetAllLightIntensityMultiplier(0.30f);
        yield return new WaitForSeconds(0.1f);
        SetAllLightsEnabled(false);
        yield return new WaitForSeconds(0.18f);

        // Breaker slams shut — restore lights based on grid state (brownout vs stabilized)
        TheProctorAI.DespawnActiveProctor();
        WeepingStudentAI.DespawnActiveWeepingStudent();
        TheProctorManager.Instance?.CleanupProctor();

        SetAllLightsEnabled(true);
        bool stabilized = CircuitBreakerPuzzle.Instance != null && CircuitBreakerPuzzle.Instance.isPowerRestored;
        float finalMultiplier = stabilized ? 0.85f : 0.28f;
        SetAllLightIntensityMultiplier(finalMultiplier);

        // Restore directional Sun intensity
        if (_mainSunLight != null)
        {
            _mainSunLight.enabled = true;
            _mainSunLight.intensity = _originalSunIntensity;
        }
        var outdoorSun = GameObject.Find("Outdoor_Gate_Illumination");
        if (outdoorSun != null)
        {
            var l = outdoorSun.GetComponent<Light>();
            if (l != null) { l.enabled = true; l.intensity = 1.0f; }
        }

        // Restore ambient and skybox
        RenderSettings.skybox = _originalSkybox;
        RenderSettings.ambientMode = _originalAmbientMode;
        RenderSettings.ambientLight = _originalAmbientColor;
        RenderSettings.ambientIntensity = _originalAmbientIntensity;

        // Restore wandering student NPCs to the hallways
        RestoreWanderingNPCs();

        // Unlock all doors locked by the blackout
        DoorInteract.UnlockAllDoorsFromBlackout();

        // Notify listeners
        IsBlackoutActive = false;
        OnBlackoutEnd?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Grid Voltage & Brownout Simulation
    // ─────────────────────────────────────────────────────────────────────────

    public void OnBreakerGridStabilized()
    {
        Debug.Log("[BlackoutManager] Grid stabilized by 15A Ceramic Fuse! Elevating campus lighting from brownout (28%) to stable (85%).");
        _currentGridBaseMultiplier = 0.85f;
        if (!IsBlackoutActive)
        {
            SetAllLightIntensityMultiplier(0.85f);
        }
    }

    private IEnumerator CampusGridVoltageLoop()
    {
        while (true)
        {
            if (IsBlackoutActive)
            {
                yield return new WaitForSeconds(0.4f);
                continue;
            }

            bool stabilized = CircuitBreakerPuzzle.Instance != null && CircuitBreakerPuzzle.Instance.isPowerRestored;
            float targetMultiplier = stabilized ? 0.85f : 0.28f;
            _currentGridBaseMultiplier = targetMultiplier;

            if (!stabilized)
            {
                // Brownout state: lights are super low and aggressively blinking/flickering
                float holdTime = UnityEngine.Random.Range(0.25f, 0.95f);
                SetAllLightIntensityMultiplier(targetMultiplier);
                yield return new WaitForSeconds(holdTime);

                if (IsBlackoutActive) continue;

                // Rapid erratic blink dip
                float dipVal = UnityEngine.Random.Range(0.02f, 0.10f);
                SetAllLightIntensityMultiplier(dipVal);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.03f, 0.08f));

                if (IsBlackoutActive) continue;

                // 30% chance of erratic double-twitch
                if (UnityEngine.Random.value < 0.30f)
                {
                    SetAllLightIntensityMultiplier(targetMultiplier * 0.6f);
                    yield return new WaitForSeconds(0.03f);
                    SetAllLightIntensityMultiplier(dipVal * 0.4f);
                    yield return new WaitForSeconds(0.04f);
                }

                SetAllLightIntensityMultiplier(targetMultiplier);
            }
            else
            {
                // Stabilized state: lights are brightened just a bit, slow subtle breathing flicker
                SetAllLightIntensityMultiplier(targetMultiplier);
                float stableWait = UnityEngine.Random.Range(10.0f, 22.0f);
                yield return new WaitForSeconds(stableWait);

                if (IsBlackoutActive) continue;

                // Rare gentle dip (slow breathing hum)
                float dip = targetMultiplier * 0.78f;
                SetAllLightIntensityMultiplier(dip);
                yield return new WaitForSeconds(0.12f);
                SetAllLightIntensityMultiplier(targetMultiplier);
            }
        }
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
            // Only protect player flashlight!
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
            if (enabled)
            {
                if (ls.originalEnabled)
                    ls.light.enabled = true;
            }
            else
            {
                ls.light.enabled = false;
                ls.light.intensity = 0f;
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
        // Preserve soft shadows set by FlashlightController — only disable during actual blackout events
        if (FlashlightController.Instance != null && FlashlightController.Instance.enableSoftShadows)
            _flashlightLight.shadows = LightShadows.Soft;

        Debug.Log($"[BlackoutManager] Flashlight tuned — intensity: {flashlightIntensity}, range: {flashlightRange}, spot: {flashlightSpotAngle}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wandering NPC Blackout Control
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheWanderingNPCs()
    {
        _cachedWanderingNPCs.Clear();
        var parent = GameObject.Find("ClearanceNPCs");
        if (parent != null)
        {
            var ais = parent.GetComponentsInChildren<ProctorAI>(true);
            foreach (var ai in ais)
            {
                if (ai != null && !_cachedWanderingNPCs.Contains(ai.gameObject))
                {
                    _cachedWanderingNPCs.Add(ai.gameObject);
                }
            }
        }

        // Fallback: search scene for any ProctorAI not named TheProctor
        if (_cachedWanderingNPCs.Count == 0)
        {
            var allAIs = FindObjectsByType<ProctorAI>(FindObjectsInactive.Include);
            foreach (var ai in allAIs)
            {
                if (ai != null && !ai.name.Contains("TheProctor") && !_cachedWanderingNPCs.Contains(ai.gameObject))
                {
                    _cachedWanderingNPCs.Add(ai.gameObject);
                }
            }
        }
        Debug.Log($"[BlackoutManager] Cached {_cachedWanderingNPCs.Count} wandering student NPCs for blackout despawning.");
    }

    private void DespawnWanderingNPCs()
    {
        if (_cachedWanderingNPCs.Count == 0) CacheWanderingNPCs();
        int count = 0;
        foreach (var npc in _cachedWanderingNPCs)
        {
            if (npc != null && npc.activeSelf)
            {
                npc.SetActive(false);
                count++;
            }
        }
        Debug.Log($"[BlackoutManager] Despawned {count} wandering student NPCs during blackout sequence.");
    }

    private void RestoreWanderingNPCs()
    {
        if (_cachedWanderingNPCs.Count == 0) CacheWanderingNPCs();
        int count = 0;
        foreach (var npc in _cachedWanderingNPCs)
        {
            if (npc != null)
            {
                npc.SetActive(true);
                var agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && !agent.isOnNavMesh)
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(npc.transform.position, out UnityEngine.AI.NavMeshHit hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                }
                count++;
            }
        }
        Debug.Log($"[BlackoutManager] Restored {count} wandering student NPCs after blackout ended.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cleanup on Destroy
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        // Restore ambient, skybox, sun and wandering NPCs if destroyed mid-blackout
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
            RestoreWanderingNPCs();
        }
    }
}
