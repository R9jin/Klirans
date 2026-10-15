using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CampusThreatDirector — Centralized AI Aggression & Blackout Director (FNAF-style Night Scaling).
/// 
/// Lore & Philosophy:
/// - In KLIRANS, collecting clearance signatures on your Clearance Slip disturbs the supernatural entities bound to the campus.
/// - Progresses organically from 0 to 6 signatures:
///     * 0 Signatures: "Night 1" — Exploration & gathering paper fragments. Long blackout cooldowns (150-210s), slow, forgiving stalkers.
///     * 1-2 Signatures: "Night 1.5 - Night 2" — The campus awakens. Mild tension, moderate blackouts (100-150s).
///     * 3-4 Signatures: "Night 3" (The quintessential FNAF 1 sweet spot!) — Aggressive & tense, frequent blackouts (65-90s),
///       Proctor & Weeping Student are fast and alert, forcing locker hiding & flashlight discipline, but completely fair!
///     * 5-6 Signatures: "Night Climax" — High stakes leading to graduation/Registrar escape (40-60s blackout intervals).
/// 
/// Controlled Components:
/// 1. Blackouts (BlackoutManager & TheProctorManager): Frequency, cooldown intervals, and chase durations.
/// 2. The Proctor AI (TheProctorAI): Patrol speed, pursuit chase speed, acoustic hearing radii, and hunt duration.
/// 3. Weeping Student AI (WeepingStudentAI): Unobserved charge speed, silhouette freeze radius, and corridor spawn distance.
/// 4. Security Guard Stalker AI (SecurityGuardStalkerAI): Endgame patrol/chase speeds, vision range, and whistle frequency.
/// 5. Atmospheric Feedback: Eerie dread sound pulse and an on-screen threat notification upon every signature collected.
/// </summary>
public class CampusThreatDirector : MonoBehaviour
{
    private static CampusThreatDirector _instance;
    public static CampusThreatDirector Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CampusThreatDirector>();
                if (_instance == null)
                {
                    var gm = GameObject.Find("GameManager") ?? new GameObject("CampusThreatDirector");
                    _instance = gm.GetComponent<CampusThreatDirector>() ?? gm.AddComponent<CampusThreatDirector>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [System.Serializable]
    public class ThreatProfile
    {
        public string stageName;
        [TextArea(2, 3)]
        public string atmosphericPrompt;

        [Header("Blackout Pacing (Seconds)")]
        [Tooltip("Minimum wait time between blackouts.")]
        public float blackoutIntervalMin;
        [Tooltip("Maximum wait time between blackouts.")]
        public float blackoutIntervalMax;
        [Tooltip("Minimum duration of the blackout stalker encounter.")]
        public float blackoutDurationMin;
        [Tooltip("Maximum duration of the blackout stalker encounter.")]
        public float blackoutDurationMax;

        [Header("The Proctor AI (Blind Acoustic Stalker)")]
        [Tooltip("Speed when patrolling the hallway corridor.")]
        public float proctorPatrolSpeed;
        [Tooltip("Speed when aggressively chasing the player on foot.")]
        public float proctorChaseSpeed;
        [Tooltip("Acoustic range to hear un-crouched walking (meters).")]
        public float proctorWalkHearing;
        [Tooltip("Acoustic range to hear sprinting floor vibrations (meters).")]
        public float proctorSprintHearing;
        [Tooltip("How long The Proctor actively hunts toward the sound origin (seconds).")]
        public float proctorHuntDuration;

        [Header("Weeping Student AI (Quantum Stalker)")]
        [Tooltip("Pursuit rush speed when player is NOT looking.")]
        public float weepingChaseSpeed;
        [Tooltip("Close-range silhouette freeze radius even in shadow (meters).")]
        public float weepingSilhouetteRadius;
        [Tooltip("Distance along hallway where Weeping Student spawns from player.")]
        public float weepingSpawnDistance;

        [Header("Security Guard SG-2 Dela Cruz (Endgame Stalker)")]
        public float guardPatrolSpeed;
        public float guardChaseSpeed;
        public float guardVisionDistance;
        public float guardWhistleIntervalMin;
        public float guardWhistleIntervalMax;
    }

    [Header("Difficulty Curve (Indices 0 to 6 = Signatures 0 to 6)")]
    public ThreatProfile[] threatProfiles;

    [Header("Audio & Audio Feedback")]
    [Tooltip("Dread stinger / heartbeat played when difficulty escalates on signing.")]
    public AudioClip threatEscalationStinger;

    [Header("Debug")]
    [Tooltip("Press F6 in play mode to simulate signing the next clearance and ramping difficulty.")]
    public KeyCode debugAdvanceThreatKey = KeyCode.F6;
    [Tooltip("Press F5 in play mode to step back difficulty for comparison.")]
    public KeyCode debugDecreaseThreatKey = KeyCode.F5;

    // Runtime state
    private int _currentSignatures = 0;
    private AudioSource _audioSource;
    private GameObject _bannerRoot;
    private Text _bannerStageText;
    private Text _bannerDescText;
    private CanvasGroup _bannerCanvasGroup;
    private Coroutine _bannerCoroutine;

    public int CurrentSignatures => _currentSignatures;
    public ThreatProfile CurrentProfile => GetProfileForSignatures(_currentSignatures);

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D UI audio
        }

        InitializeDefaultProfilesIfEmpty();
        BuildThreatBannerUI();
    }

    private void Start()
    {
        // Hook into ClearanceManager
        ClearanceManager.OnSignatureCollected += HandleSignatureCollected;

        // Query initial signature count
        if (ClearanceManager.Instance != null)
        {
            _currentSignatures = Mathf.Clamp(ClearanceManager.Instance.SignatureCount, 0, 6);
        }

        // Apply initial stage (Night 1 baseline)
        ApplyThreatProfile(_currentSignatures, false);
    }

    private void OnDestroy()
    {
        ClearanceManager.OnSignatureCollected -= HandleSignatureCollected;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(debugAdvanceThreatKey))
        {
            int next = Mathf.Clamp(_currentSignatures + 1, 0, 6);
            Debug.Log($"[CampusThreatDirector] Debug F6: Advancing threat level from {_currentSignatures} -> {next}");
            SetThreatLevel(next, true);
        }
        else if (Input.GetKeyDown(debugDecreaseThreatKey))
        {
            int prev = Mathf.Clamp(_currentSignatures - 1, 0, 6);
            Debug.Log($"[CampusThreatDirector] Debug F5: Decreasing threat level from {_currentSignatures} -> {prev}");
            SetThreatLevel(prev, true);
        }
#endif
    }

    // ── Signature Event Handler ───────────────────────────────────────────────

    private void HandleSignatureCollected(int signatureIndex, int totalCollected)
    {
        SetThreatLevel(totalCollected, true);
    }

    /// <summary>
    /// Updates the active threat level to the specified signature count (0 to 6).
    /// </summary>
    public void SetThreatLevel(int signatures, bool showBannerAndStinger)
    {
        _currentSignatures = Mathf.Clamp(signatures, 0, 6);
        ApplyThreatProfile(_currentSignatures, showBannerAndStinger);
    }

    // ── Apply Threat Profile across Subsystems ────────────────────────────────

    private void ApplyThreatProfile(int signatures, bool showBannerAndStinger)
    {
        ThreatProfile profile = GetProfileForSignatures(signatures);

        Debug.Log($"[CampusThreatDirector] >>> APPLIED THREAT LEVEL {signatures}/6 [{profile.stageName}] <<<\n" +
                  $"Blackout: {profile.blackoutIntervalMin:F0}-{profile.blackoutIntervalMax:F0}s (Duration: {profile.blackoutDurationMin:F0}-{profile.blackoutDurationMax:F0}s) | " +
                  $"Proctor: Patrol {profile.proctorPatrolSpeed:F1}m/s, Chase {profile.proctorChaseSpeed:F1}m/s, Hearing {profile.proctorWalkHearing:F1}/{profile.proctorSprintHearing:F1}m | " +
                  $"Weeping: Chase {profile.weepingChaseSpeed:F1}m/s, Silhouette {profile.weepingSilhouetteRadius:F1}m");

        // 1. Update BlackoutManager
        var bm = BlackoutManager.Instance ?? FindAnyObjectByType<BlackoutManager>();
        if (bm != null)
        {
            bm.intervalMin = profile.blackoutIntervalMin;
            bm.intervalMax = profile.blackoutIntervalMax;
            bm.blackoutDurationMin = profile.blackoutDurationMin;
            bm.blackoutDurationMax = profile.blackoutDurationMax;
            bm.minIntervalCap = profile.blackoutIntervalMin;
            bm.maxIntervalCap = profile.blackoutIntervalMax;
        }

        // 2. Update TheProctorManager
        var pm = TheProctorManager.Instance ?? FindAnyObjectByType<TheProctorManager>();
        if (pm != null)
        {
            pm.blackoutChaseDuration = (profile.blackoutDurationMin + profile.blackoutDurationMax) * 0.5f;
            pm.spawnDistance = profile.weepingSpawnDistance;
        }

        // 3. Update currently active Proctor AI (if stalking in corridor right now)
        if (TheProctorAI.ActiveProctor != null)
        {
            ApplyStatsToProctor(TheProctorAI.ActiveProctor);
        }

        // 4. Update currently active Weeping Student AI (if stalking in corridor right now)
        if (WeepingStudentAI.ActiveWeepingStudent != null)
        {
            ApplyStatsToWeeping(WeepingStudentAI.ActiveWeepingStudent);
        }

        // 5. Update endgame Security Guard Stalker (if active)
        if (SecurityGuardStalkerAI.ActiveStalker != null)
        {
            ApplyStatsToGuard(SecurityGuardStalkerAI.ActiveStalker);
        }

        // 6. Audio stinger & atmospheric on-screen prompt
        if (showBannerAndStinger)
        {
            PlayDreadStinger();
            ShowThreatNotification(profile.stageName, profile.atmosphericPrompt);
        }
    }

    // ── Public AI Configuration Bridges ──────────────────────────────────────

    public void ApplyStatsToProctor(TheProctorAI proctor)
    {
        if (proctor == null) return;
        ThreatProfile p = CurrentProfile;
        proctor.ApplyThreatStats(
            p.proctorPatrolSpeed,
            p.proctorChaseSpeed,
            p.proctorWalkHearing,
            p.proctorSprintHearing,
            p.proctorHuntDuration
        );
    }

    public void ApplyStatsToWeeping(WeepingStudentAI weeping)
    {
        if (weeping == null) return;
        ThreatProfile p = CurrentProfile;
        weeping.ApplyThreatStats(
            p.weepingChaseSpeed,
            p.weepingSilhouetteRadius
        );
    }

    public void ApplyStatsToGuard(SecurityGuardStalkerAI guard)
    {
        if (guard == null) return;
        ThreatProfile p = CurrentProfile;
        guard.ApplyThreatStats(
            p.guardPatrolSpeed,
            p.guardChaseSpeed,
            p.guardVisionDistance,
            p.guardWhistleIntervalMin,
            p.guardWhistleIntervalMax
        );
    }

    public ThreatProfile GetProfileForSignatures(int count)
    {
        if (threatProfiles == null || threatProfiles.Length == 0)
        {
            InitializeDefaultProfilesIfEmpty();
        }
        int clamped = Mathf.Clamp(count, 0, threatProfiles.Length - 1);
        return threatProfiles[clamped];
    }

    // ── Audio Feedback ────────────────────────────────────────────────────────

    private void PlayDreadStinger()
    {
        if (_audioSource == null) return;

        if (threatEscalationStinger != null)
        {
            _audioSource.PlayOneShot(threatEscalationStinger, 0.85f);
        }
        else
        {
            // Procedurally synthesize a sinister low-frequency sub-bass dread pulse
            AudioClip pulse = CreateProceduralDreadPulse();
            _audioSource.PlayOneShot(pulse, 0.90f);
        }
    }

    private AudioClip CreateProceduralDreadPulse()
    {
        int sampleRate = 44100;
        float duration = 1.6f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 52Hz ominous sub-bass with slow binaural detune and sinister decay
            float freq = Mathf.Lerp(62f, 44f, t / duration);
            float sub = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
            float detune = Mathf.Sin(2f * Mathf.PI * (freq * 1.5f + 1.2f) * t) * 0.25f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.05f;

            float env = Mathf.Sin(Mathf.Clamp01(t / 0.15f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 1.8f);
            samples[i] = (sub + detune + noise) * env;
        }

        AudioClip clip = AudioClip.Create("ThreatEscalation_DreadPulse", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ── On-Screen Threat Notification UI ──────────────────────────────────────

    private void BuildThreatBannerUI()
    {
        Canvas hudCanvas = null;
        var canvasGo = GameObject.Find("HudCanvas");
        if (canvasGo != null) hudCanvas = canvasGo.GetComponent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();

        if (hudCanvas == null) return;

        // Check if banner already exists
        Transform existing = hudCanvas.transform.Find("ThreatBannerPanel");
        if (existing != null)
        {
            _bannerRoot = existing.gameObject;
            _bannerCanvasGroup = _bannerRoot.GetComponent<CanvasGroup>();
            _bannerStageText = existing.Find("StageText")?.GetComponent<Text>();
            _bannerDescText = existing.Find("DescText")?.GetComponent<Text>();
            return;
        }

        // Create sleek retro-horror banner
        _bannerRoot = new GameObject("ThreatBannerPanel");
        _bannerRoot.transform.SetParent(hudCanvas.transform, false);

        RectTransform rt = _bannerRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -38f);
        rt.sizeDelta = new Vector2(620f, 64f);

        Image bg = _bannerRoot.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.02f, 0.02f, 0.88f); // Dark charcoal red backing

        // Outline
        Outline outline = _bannerRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.12f, 0.12f, 0.65f); // Dried crimson border
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        _bannerCanvasGroup = _bannerRoot.AddComponent<CanvasGroup>();
        _bannerCanvasGroup.alpha = 0f;
        _bannerCanvasGroup.blocksRaycasts = false;
        _bannerCanvasGroup.interactable = false;

        Font font = Resources.Load<Font>("Fonts/Typewriter_Bold") 
                 ?? Resources.Load<Font>("Typewriter_Bold")
                 ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Stage Header Text
        GameObject stageGO = new GameObject("StageText");
        stageGO.transform.SetParent(_bannerRoot.transform, false);
        RectTransform stageRT = stageGO.AddComponent<RectTransform>();
        stageRT.anchorMin = new Vector2(0f, 0.5f);
        stageRT.anchorMax = new Vector2(1f, 1f);
        stageRT.anchoredPosition = new Vector2(0f, -4f);
        stageRT.sizeDelta = new Vector2(-24f, 24f);

        _bannerStageText = stageGO.AddComponent<Text>();
        _bannerStageText.font = font;
        _bannerStageText.fontSize = 14;
        _bannerStageText.fontStyle = FontStyle.Bold;
        _bannerStageText.alignment = TextAnchor.MiddleCenter;
        _bannerStageText.color = new Color(1.0f, 0.32f, 0.32f, 1f); // Vibrant warning crimson

        // 2. Descriptive / Taglish Prompt Text
        GameObject descGO = new GameObject("DescText");
        descGO.transform.SetParent(_bannerRoot.transform, false);
        RectTransform descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.5f);
        descRT.anchoredPosition = new Vector2(0f, 4f);
        descRT.sizeDelta = new Vector2(-24f, 24f);

        _bannerDescText = descGO.AddComponent<Text>();
        _bannerDescText.font = font;
        _bannerDescText.fontSize = 12;
        _bannerDescText.fontStyle = FontStyle.Normal;
        _bannerDescText.alignment = TextAnchor.MiddleCenter;
        _bannerDescText.color = new Color(0.92f, 0.90f, 0.86f, 0.95f); // Off-white typewriter ink
    }

    public void ShowThreatNotification(string stageTitle, string description)
    {
        if (_bannerRoot == null) BuildThreatBannerUI();
        if (_bannerRoot == null || _bannerCanvasGroup == null) return;

        if (_bannerStageText != null) _bannerStageText.text = stageTitle.ToUpper();
        if (_bannerDescText != null) _bannerDescText.text = description;

        if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
        _bannerCoroutine = StartCoroutine(AnimateBannerRoutine());
    }

    private IEnumerator AnimateBannerRoutine()
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            _bannerCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.4f);
            yield return null;
        }
        _bannerCanvasGroup.alpha = 1f;

        // Display for 4.5 seconds
        yield return new WaitForSeconds(4.5f);

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            _bannerCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.8f);
            yield return null;
        }
        _bannerCanvasGroup.alpha = 0f;
        _bannerCoroutine = null;
    }

    // ── Default FNAF Night 3 Difficulty Curve Initialization ──────────────────

    private void InitializeDefaultProfilesIfEmpty()
    {
        if (threatProfiles != null && threatProfiles.Length == 7) return;

        threatProfiles = new ThreatProfile[7];

        // 0 Signatures: "Shift Start / Night 1" (Tutorial & Gathering Pieces)
        threatProfiles[0] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Shift Start (0/6)",
            atmosphericPrompt = "Tahimik pa ang mga pasilyo. Bihira ang brownout. Hanapin ang mga punit na clearance.",
            blackoutIntervalMin = 150f,
            blackoutIntervalMax = 210f,
            blackoutDurationMin = 16f,
            blackoutDurationMax = 18f,
            proctorPatrolSpeed = 2.3f,
            proctorChaseSpeed = 4.5f, // Player walk = 4.0, run = 6.5. Very safe!
            proctorWalkHearing = 8.0f,
            proctorSprintHearing = 18.0f,
            proctorHuntDuration = 4.0f,
            weepingChaseSpeed = 5.5f,
            weepingSilhouetteRadius = 4.8f,
            weepingSpawnDistance = 20.0f,
            guardPatrolSpeed = 2.0f,
            guardChaseSpeed = 3.4f,
            guardVisionDistance = 16.0f,
            guardWhistleIntervalMin = 28.0f,
            guardWhistleIntervalMax = 42.0f
        };

        // 1 Signature: "Night 1.5" (First Clearance Stamped)
        threatProfiles[1] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Night 1.5 (1/6)",
            atmosphericPrompt = "Unang pirma nakuha. Nagsisimulang kumurap ang mga fluorescent tube sa pasilyo.",
            blackoutIntervalMin = 125f,
            blackoutIntervalMax = 175f,
            blackoutDurationMin = 18f,
            blackoutDurationMax = 20f,
            proctorPatrolSpeed = 2.5f,
            proctorChaseSpeed = 4.7f,
            proctorWalkHearing = 9.5f,
            proctorSprintHearing = 20.0f,
            proctorHuntDuration = 4.5f,
            weepingChaseSpeed = 6.2f,
            weepingSilhouetteRadius = 4.4f,
            weepingSpawnDistance = 18.5f,
            guardPatrolSpeed = 2.1f,
            guardChaseSpeed = 3.5f,
            guardVisionDistance = 17.0f,
            guardWhistleIntervalMin = 26.0f,
            guardWhistleIntervalMax = 38.0f
        };

        // 2 Signatures: "Night 2" (Threat Materialized)
        threatProfiles[2] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Night 2 (2/6)",
            atmosphericPrompt = "Pangalawang pirma nakuha. Lumalakas ang mga yabag sa dilim. Mag-ingat sa pagtakbo.",
            blackoutIntervalMin = 100f,
            blackoutIntervalMax = 140f,
            blackoutDurationMin = 20f,
            blackoutDurationMax = 22f,
            proctorPatrolSpeed = 2.7f,
            proctorChaseSpeed = 5.0f,
            proctorWalkHearing = 11.0f,
            proctorSprintHearing = 22.0f,
            proctorHuntDuration = 5.0f,
            weepingChaseSpeed = 7.0f,
            weepingSilhouetteRadius = 4.0f,
            weepingSpawnDistance = 17.0f,
            guardPatrolSpeed = 2.2f,
            guardChaseSpeed = 3.6f,
            guardVisionDistance = 18.0f,
            guardWhistleIntervalMin = 24.0f,
            guardWhistleIntervalMax = 35.0f
        };

        // 3 Signatures: "Night 2.5" (Midway Tension)
        threatProfiles[3] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Night 2.5 (3/6)",
            atmosphericPrompt = "Pangatlong pirma nakuha. Humihina ang kuryente. Mas lumalapit ang mga entity.",
            blackoutIntervalMin = 80f,
            blackoutIntervalMax = 110f,
            blackoutDurationMin = 22f,
            blackoutDurationMax = 24f,
            proctorPatrolSpeed = 3.0f,
            proctorChaseSpeed = 5.2f,
            proctorWalkHearing = 12.5f,
            proctorSprintHearing = 24.5f,
            proctorHuntDuration = 5.5f,
            weepingChaseSpeed = 7.6f,
            weepingSilhouetteRadius = 3.7f,
            weepingSpawnDistance = 15.5f,
            guardPatrolSpeed = 2.3f,
            guardChaseSpeed = 3.7f,
            guardVisionDistance = 19.0f,
            guardWhistleIntervalMin = 22.0f,
            guardWhistleIntervalMax = 32.0f
        };

        // 4 Signatures: "Night 3" (The FNAF 1 Sweet Spot!)
        threatProfiles[4] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Night 3 (4/6)",
            atmosphericPrompt = "NIGHT 3 INTENSITY: Madalas ang brownout! Mabilis at agresibo ang mga entity sa pasilyo.",
            blackoutIntervalMin = 65f,
            blackoutIntervalMax = 90f,
            blackoutDurationMin = 24f,
            blackoutDurationMax = 26f,
            proctorPatrolSpeed = 3.2f,
            proctorChaseSpeed = 5.4f, // Fair: sprint is 6.5, so burst sprint + lockers save you!
            proctorWalkHearing = 13.5f,
            proctorSprintHearing = 26.5f,
            proctorHuntDuration = 6.0f,
            weepingChaseSpeed = 8.2f, // Fast surge when unobserved, frozen solid on eye contact!
            weepingSilhouetteRadius = 3.5f,
            weepingSpawnDistance = 14.5f,
            guardPatrolSpeed = 2.4f,
            guardChaseSpeed = 3.8f,
            guardVisionDistance = 20.0f,
            guardWhistleIntervalMin = 18.0f,
            guardWhistleIntervalMax = 28.0f
        };

        // 5 Signatures: "Night 3.5" (High Tension Penultimate Stage)
        threatProfiles[5] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Night 3.5 (5/6)",
            atmosphericPrompt = "Panlimang pirma nakuha. Bumabagsak ang buong power grid. Isa na lang ang kulang!",
            blackoutIntervalMin = 50f,
            blackoutIntervalMax = 75f,
            blackoutDurationMin = 26f,
            blackoutDurationMax = 28f,
            proctorPatrolSpeed = 3.4f,
            proctorChaseSpeed = 5.6f,
            proctorWalkHearing = 15.0f,
            proctorSprintHearing = 28.5f,
            proctorHuntDuration = 6.5f,
            weepingChaseSpeed = 8.7f,
            weepingSilhouetteRadius = 3.3f,
            weepingSpawnDistance = 13.5f,
            guardPatrolSpeed = 2.5f,
            guardChaseSpeed = 3.9f,
            guardVisionDistance = 21.0f,
            guardWhistleIntervalMin = 16.0f,
            guardWhistleIntervalMax = 25.0f
        };

        // 6 Signatures: "Night Climax" (All Clearances Signed — Submit to Registrar)
        threatProfiles[6] = new ThreatProfile
        {
            stageName = "Campus Threat Level: Full Lockdown (6/6)",
            atmosphericPrompt = "KUMPLETO NA ANG CLEARANCE! Sukdulan ang banta. Isuko agad ang papel sa Registrar!",
            blackoutIntervalMin = 40f,
            blackoutIntervalMax = 60f,
            blackoutDurationMin = 28f,
            blackoutDurationMax = 30f,
            proctorPatrolSpeed = 3.6f,
            proctorChaseSpeed = 5.8f, // Max 5.8 m/s: still below player 6.5 m/s sprint!
            proctorWalkHearing = 16.0f,
            proctorSprintHearing = 30.0f,
            proctorHuntDuration = 7.0f,
            weepingChaseSpeed = 9.2f,
            weepingSilhouetteRadius = 3.1f,
            weepingSpawnDistance = 12.5f,
            guardPatrolSpeed = 2.6f,
            guardChaseSpeed = 4.0f,
            guardVisionDistance = 22.0f,
            guardWhistleIntervalMin = 14.0f,
            guardWhistleIntervalMax = 22.0f
        };
    }
}
