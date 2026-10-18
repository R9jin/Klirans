using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GuardChaseCutsceneDirector — Cinematic anomaly sequence for the Security Guard's transformation and corridor pursuit.
/// 
/// LORE & SEQUENCE:
/// 1. Triggered upon final clearance submission in Room 104 (or via Debug Key F9/F6).
/// 2. BEAT 1 (The Lockdown Pulse): Sudden electrical blackout surge and static cut. Letterbox bars close in. Red emergency beacon starts pulsating in the lobby booth.
/// 3. BEAT 2 (The Booth in Red): Cinematic establishing shot of the Ground Floor Lobby Security Station. The camera glides toward the window with an eerie Dutch angle while corrupted PA broadcast alerts sound.
/// 4. BEAT 3 (The Anomaly Rises): Medium close-up on the Security Guard with dramatic vertigo zoom punch. He stands up with jerky, mechanical rigidity. His head snaps unnaturally to stare directly at the camera lens with micro-twitches.
/// 5. BEAT 4 (The Whistle & Halogen Ignition): Blinding halogen flashlight snaps on with a full-screen white glare flare. The guard raises his whistle and sounds a room-shaking haunted whistle blast while the camera vibrates violently.
/// 6. BEAT 5 (The Corridor Sweep): Camera cuts to the main hallway corridor. The Stalker Guard emerges through the archway with heavy combat boots and sweeping flashlight beam. Corrupted system orders lockdown.
/// 7. BEAT 6 (Hand-off to Player): Camera returns smoothly to the player in first-person view. Player character gasps in terror with an in-character dialogue realization. Persistent red emergency strobe illuminates the lobby escape route. Stalker AI begins active hunt. Objective HUD updates with high-priority escape objective.
/// 
/// AESTHETICS:
/// - Uses authentic paper textures (ScrambledPaper_HUD.png, KeypadButton_Normal.png) with aged masking tape strips and typewriter ribbon sound effects.
/// - Typewriter horror fonts (watch people die, Typewriter_Bold).
/// - Full skip support via [SPACE] / [ESC] / [ENTER].
/// </summary>
public class GuardChaseCutsceneDirector : MonoBehaviour
{
    private static GuardChaseCutsceneDirector _instance;
    public static GuardChaseCutsceneDirector Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GuardChaseCutsceneDirector>();
                if (_instance == null)
                {
                    GameObject go = GameObject.Find("GameManager") ?? new GameObject("GuardChaseCutsceneDirector");
                    _instance = go.GetComponent<GuardChaseCutsceneDirector>() ?? go.AddComponent<GuardChaseCutsceneDirector>();
                }
            }
            return _instance;
        }
    }

    public static bool IsCutsceneActive { get; private set; } = false;

    [Header("Debug")]
    [Tooltip("Press this key in playmode to instantly trigger the Guard Chase Cutscene for testing.")]
    public KeyCode debugTriggerKey = KeyCode.F6;

    [Header("Paper UI Sprites")]
    public Sprite dialoguePaperSprite;
    public Sprite buttonPaperSprite;
    public Sprite vignetteSprite;

    [Header("Audio Clips")]
    public AudioClip powerOutageClip;
    public AudioClip anomalyDroneClip;
    public AudioClip hauntedWhistleClip;
    public AudioClip alertStingClip;
    public AudioClip jumpscareHitClip;
    public AudioClip runningFootstepsClip;
    public AudioClip crazyMumblesClip;
    public AudioClip staticClip;
    public AudioClip heartbeatClip;
    public AudioClip studentGaspClip;
    public AudioClip typewriterTickClip;
    public AudioClip buttonClickClip;

    public event Action OnCutsceneFinished;

    // UI Elements
    private GameObject _canvasGO;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private Image _blackBackdrop;
    private Image _glitchOverlay;
    private Image _whiteFlashOverlay;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;

    private GameObject _subtitleBox;
    private Text _subtitleText;
    private Text _speakerTagText;
    private Text _contextTagText;
    private Coroutine _typewriterRoutine;

    private GameObject _skipPromptRoot;
    private Text _skipPromptText;
    private Button _skipButton;

    // Audio Sources
    private AudioSource _ambienceSource;
    private AudioSource _alarmSource;
    private AudioSource _voiceSource;
    private AudioSource _whistleSource;
    private AudioSource _sfxSource;
    private AudioSource _heartbeatSource;
    private AudioSource _typewriterAudioSource;

    // Cinematic Camera & Lighting
    private GameObject _cutsceneCameraGO;
    private Camera _cutsceneCamera;
    private AudioListener _cutsceneListener;
    private AudioListener _playerListener;
    private Light _redEmergencyBeacon;
    private Light _boothSpotlight;
    private Light _guardCutsceneHalogen;

    // Runtime state
    private Coroutine _cutsceneRoutine;
    private PlayerMovement _playerMovement;
    private Camera _playerCamera;
    private bool _hasSkipped = false;
    private Transform _guardHeadBone;

    private void Awake()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            IsCutsceneActive = false;
            return;
        }

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupAudioSources();
        LoadDefaultAssets();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        CleanupCutsceneLights(true);
    }

    private void Update()
    {
        if (GameModeManager.IsFreeRoamMode) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(debugTriggerKey) && !IsCutsceneActive)
        {
            Debug.Log("[GuardChaseCutsceneDirector] Debug trigger F6 pressed — launching Guard Chase Cutscene!");
            PlayChaseCutscene();
            return;
        }
#endif

        if (!IsCutsceneActive || _hasSkipped) return;

        // Skip input handling
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
        {
            SkipCutscene();
        }
    }

    // ── Asset Loading & Audio Setup ──────────────────────────────────────────

    private void SetupAudioSources()
    {
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.playOnAwake = false;
        _ambienceSource.loop = true;
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.volume = 0.40f;

        _alarmSource = gameObject.AddComponent<AudioSource>();
        _alarmSource.playOnAwake = false;
        _alarmSource.loop = true;
        _alarmSource.spatialBlend = 0f;
        _alarmSource.volume = 0.65f;

        _voiceSource = gameObject.AddComponent<AudioSource>();
        _voiceSource.playOnAwake = false;
        _voiceSource.loop = false;
        _voiceSource.spatialBlend = 0f;
        _voiceSource.volume = 0.90f;

        _whistleSource = gameObject.AddComponent<AudioSource>();
        _whistleSource.playOnAwake = false;
        _whistleSource.loop = false;
        _whistleSource.spatialBlend = 0f;
        _whistleSource.volume = 1.0f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 0.90f;

        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.playOnAwake = false;
        _heartbeatSource.loop = true;
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.volume = 0f;

        _typewriterAudioSource = gameObject.AddComponent<AudioSource>();
        _typewriterAudioSource.playOnAwake = false;
        _typewriterAudioSource.loop = false;
        _typewriterAudioSource.spatialBlend = 0f;
        _typewriterAudioSource.volume = 0.18f;
    }

    private void LoadDefaultAssets()
    {
#if UNITY_EDITOR
        if (dialoguePaperSprite == null)
            dialoguePaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");
        if (buttonPaperSprite == null)
            buttonPaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/KeypadButton_Normal.png");
        if (vignetteSprite == null)
            vignetteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Vignette_UI.png");

        if (powerOutageClip == null)
            powerOutageClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/FNAF_ Power Outage - Gaming Sound Effect (HD).mp3");
        if (anomalyDroneClip == null)
            anomalyDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/dysopian background sound.mp3");
        if (hauntedWhistleClip == null)
            hauntedWhistleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fnx_sound-whistle-of-the-haunted-realm-287347.mp3");
        if (alertStingClip == null)
            alertStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
        if (jumpscareHitClip == null)
            jumpscareHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/theProctorJumpscare.mp3");
        if (runningFootstepsClip == null)
            runningFootstepsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
        if (crazyMumblesClip == null)
            crazyMumblesClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
        if (staticClip == null)
            staticClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");
        if (heartbeatClip == null)
            heartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3") ??
                            UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/heartbeat.mp3");
        if (studentGaspClip == null)
            studentGaspClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3") ??
                              UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");
        if (typewriterTickClip == null)
            typewriterTickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
        if (buttonClickClip == null)
            buttonClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
    }

    private void FindPlayerReferences()
    {
        if (_playerMovement == null)
            _playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (_playerMovement != null && _playerMovement.playerCamera != null)
        {
            _playerCamera = _playerMovement.playerCamera;
        }
        else if (Camera.main != null)
        {
            _playerCamera = Camera.main;
        }

        if (_playerCamera != null)
        {
            _playerListener = _playerCamera.GetComponent<AudioListener>();
        }
    }

    // ── Public Trigger API ───────────────────────────────────────────────────

    /// <summary>
    /// Launches the cinematic Security Guard Anomaly Chase Cutscene.
    /// </summary>
    public void PlayChaseCutscene()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (IsCutsceneActive) return;

        // If dialogue is active, close it cleanly
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive)
        {
            NPCDialogueSystem.Instance.EndDialogue();
        }

        FindPlayerReferences();

        IsCutsceneActive = true;
        _hasSkipped = false;
        Debug.Log("[GuardChaseCutsceneDirector] >>> STARTING ANOMALOUS SECURITY GUARD CHASE CUTSCENE <<<");

        // Lock player controls
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
        }

        HideGameplayHUD(true);
        SetLetterboxVisible(true);
        HideSubtitles();

        if (_skipPromptRoot != null) _skipPromptRoot.SetActive(true);
        if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);
        if (_glitchOverlay != null) _glitchOverlay.color = new Color(0.85f, 0f, 0f, 0f);
        if (_whiteFlashOverlay != null) _whiteFlashOverlay.color = new Color(1f, 1f, 1f, 0f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetUIVisible(true);

        // Setup Cinematic Camera
        SetupCinematicCamera();

        if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
        _cutsceneRoutine = StartCoroutine(CutsceneSequence());
    }

    /// <summary>
    /// Skips the cutscene and transitions immediately to active Stalker gameplay.
    /// </summary>
    public void SkipCutscene()
    {
        if (!IsCutsceneActive || _hasSkipped) return;
        _hasSkipped = true;
        Debug.Log("[GuardChaseCutsceneDirector] Cutscene skipped by player!");

        if (_cutsceneRoutine != null)
        {
            StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = null;
        }
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        StopAllAudio();
        CleanupCutsceneLights(false);
        TearDownCinematicCamera();

        // Complete the endgame spawn & handoff
        CompleteEndgameHandoff();
    }

    private void StopAllAudio()
    {
        if (_ambienceSource != null) _ambienceSource.Stop();
        if (_alarmSource != null) _alarmSource.Stop();
        if (_voiceSource != null) _voiceSource.Stop();
        if (_whistleSource != null) _whistleSource.Stop();
        if (_sfxSource != null) _sfxSource.Stop();
        if (_heartbeatSource != null) _heartbeatSource.Stop();
        if (_typewriterAudioSource != null) _typewriterAudioSource.Stop();
    }

    // ── Cinematic Camera & Dynamic Lighting ──────────────────────────────────

    private void SetupCinematicCamera()
    {
        if (_cutsceneCameraGO == null)
        {
            _cutsceneCameraGO = new GameObject("GuardChase_CinematicCamera");
            _cutsceneCamera = _cutsceneCameraGO.AddComponent<Camera>();
            _cutsceneCamera.nearClipPlane = 0.1f;
            _cutsceneCamera.farClipPlane = 200f;
            _cutsceneCamera.fieldOfView = 58f;
            _cutsceneListener = _cutsceneCameraGO.AddComponent<AudioListener>();
        }

        _cutsceneCamera.depth = _playerCamera != null ? _playerCamera.depth + 10f : 10f;
        _cutsceneCamera.enabled = true;

        if (_playerListener != null) _playerListener.enabled = false;
        if (_cutsceneListener != null) _cutsceneListener.enabled = true;
    }

    private void TearDownCinematicCamera()
    {
        if (_cutsceneCamera != null) _cutsceneCamera.enabled = false;
        if (_cutsceneListener != null) _cutsceneListener.enabled = false;
        if (_playerListener != null) _playerListener.enabled = true;
        if (_playerCamera != null) _playerCamera.enabled = true;
    }

    private void EnsureCutsceneLights()
    {
        if (_redEmergencyBeacon == null)
        {
            GameObject beaconGO = new GameObject("Cutscene_EmergencyRedBeacon");
            beaconGO.transform.position = new Vector3(-76.2f, 3.25f, 16.0f);
            _redEmergencyBeacon = beaconGO.AddComponent<Light>();
            _redEmergencyBeacon.type = LightType.Point;
            _redEmergencyBeacon.color = new Color(1.0f, 0.06f, 0.06f, 1.0f);
            _redEmergencyBeacon.range = 14.0f;
            _redEmergencyBeacon.intensity = 0f;
            _redEmergencyBeacon.shadows = LightShadows.Soft;
        }

        if (_boothSpotlight == null)
        {
            GameObject spotGO = new GameObject("Cutscene_BoothSpotlight");
            spotGO.transform.position = new Vector3(-76.2f, 3.1f, 16.0f);
            spotGO.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            _boothSpotlight = spotGO.AddComponent<Light>();
            _boothSpotlight.type = LightType.Spot;
            _boothSpotlight.color = new Color(0.90f, 0.15f, 0.15f, 1.0f);
            _boothSpotlight.range = 9.0f;
            _boothSpotlight.spotAngle = 80f;
            _boothSpotlight.intensity = 0f;
        }
    }

    private void CleanupCutsceneLights(bool destroyBeacon = false)
    {
        if (_boothSpotlight != null) Destroy(_boothSpotlight.gameObject);
        if (_guardCutsceneHalogen != null) Destroy(_guardCutsceneHalogen.gameObject);

        // Keep the red beacon persistent during active stalker gameplay for atmospheric lockdown lighting
        if (destroyBeacon && _redEmergencyBeacon != null)
        {
            Destroy(_redEmergencyBeacon.gameObject);
        }
        else if (_redEmergencyBeacon != null)
        {
            // Transition to steady subtle emergency pulse in lobby
            _redEmergencyBeacon.intensity = 2.4f;
        }
    }

    // ── Cutscene Choreography Sequence ───────────────────────────────────────

    private IEnumerator CutsceneSequence()
    {
        EnsureCutsceneLights();

        // Reference to the sitting booth guard
        GameObject boothGuard = GameObject.Find("Security_Guard_NPC");
        if (boothGuard == null)
        {
            var interact = FindAnyObjectByType<SecurityGuardInteract>(FindObjectsInactive.Include);
            if (interact != null) boothGuard = interact.gameObject;
        }
        if (boothGuard != null)
        {
            boothGuard.SetActive(true);
            FindGuardHeadBone(boothGuard.transform);
        }

        Vector3 guardPos = boothGuard != null ? boothGuard.transform.position : new Vector3(-75.75f, 2.36f, 15.95f);

        // ── BEAT 1: Electrical Outage & Lockdown Glitch Surge (1.2s) ─────────
        if (_sfxSource != null && powerOutageClip != null)
        {
            _sfxSource.PlayOneShot(powerOutageClip, 0.95f);
        }
        if (_alarmSource != null && staticClip != null)
        {
            _alarmSource.clip = staticClip;
            _alarmSource.volume = 0.55f;
            _alarmSource.Play();
        }

        // Screen black flash with glitch crimson flashes and letterbox entry
        float blackoutElapsed = 0f;
        while (blackoutElapsed < 1.0f)
        {
            blackoutElapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.PingPong(blackoutElapsed * 8f, 1f);
            if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(1.0f, 0.75f, pulse));
            if (_glitchOverlay != null) _glitchOverlay.color = new Color(0.9f, 0.05f, 0.05f, pulse * 0.40f);
            yield return null;
        }

        if (_alarmSource != null && anomalyDroneClip != null)
        {
            _alarmSource.clip = anomalyDroneClip;
            _alarmSource.volume = 0.50f;
            _alarmSource.Play();
        }

        // ── BEAT 2: Establishing Shot — The Booth in Crimson Red (2.8s) ──────
        // Camera in the lobby looking at the booth window with subtle eerie Dutch roll (-3.5 deg)
        Vector3 cam1Start = new Vector3(-78.6f, 2.75f, 16.5f);
        Vector3 cam1End = new Vector3(-77.2f, 2.65f, 16.2f);
        Quaternion cam1RotStart = Quaternion.Euler(5f, 98f, -3.5f);
        Quaternion cam1RotEnd = Quaternion.Euler(4f, 100f, -1.5f);

        _cutsceneCamera.fieldOfView = 58f;
        _cutsceneCamera.transform.position = cam1Start;
        _cutsceneCamera.transform.rotation = cam1RotStart;

        ShowSubtitle("PNC PUBLIC ADDRESS SYSTEM", "BABALA: NA-CORRUPT ANG DATABASE NG REGISTRAR. HINDI AWTORISADO ANG PAG-ALIS. ISINARA ANG BUONG PANGUNAHING PASILIDAD.", "LOBBY CHECKPOINT // RED PROTOCOL");

        float dollyElapsed = 0f;
        float dollyDuration = 2.8f;
        while (dollyElapsed < dollyDuration)
        {
            dollyElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, dollyElapsed / dollyDuration);

            _cutsceneCamera.transform.position = Vector3.Lerp(cam1Start, cam1End, t);
            _cutsceneCamera.transform.rotation = Quaternion.Slerp(cam1RotStart, cam1RotEnd, t);

            // Pulse red emergency lighting
            float beaconIntensity = 2.6f + Mathf.Sin(Time.time * 8f) * 1.9f;
            if (_redEmergencyBeacon != null) _redEmergencyBeacon.intensity = beaconIntensity;
            if (_boothSpotlight != null) _boothSpotlight.intensity = beaconIntensity * 0.75f;

            // Fade black backdrop
            if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.75f, 0f, t * 2f));
            if (_glitchOverlay != null) _glitchOverlay.color = new Color(0.9f, 0.05f, 0.05f, Mathf.Sin(Time.time * 12f) * 0.08f);

            yield return null;
        }

        // ── BEAT 3: The Anomaly Rises with Vertigo Zoom (2.8s) ────────────────
        // Medium close-up on the Security Guard inside the booth
        Vector3 cam2Pos = new Vector3(-76.7f, 2.60f, 16.15f);
        Quaternion cam2Rot = Quaternion.Euler(2f, 102f, 0f);

        _cutsceneCamera.transform.position = cam2Pos;
        _cutsceneCamera.transform.rotation = cam2Rot;

        if (_sfxSource != null && alertStingClip != null)
        {
            _sfxSource.PlayOneShot(alertStingClip, 0.90f);
        }
        if (_voiceSource != null && crazyMumblesClip != null)
        {
            _voiceSource.PlayOneShot(crazyMumblesClip, 0.90f);
        }

        // Start tension heartbeat
        if (_heartbeatSource != null && heartbeatClip != null)
        {
            _heartbeatSource.clip = heartbeatClip;
            _heartbeatSource.volume = 0.55f;
            _heartbeatSource.pitch = 1.05f;
            _heartbeatSource.Play();
        }

        // Animate the guard rising unnaturally
        var sitController = boothGuard != null ? boothGuard.GetComponent<NPCSitController>() : null;
        if (sitController != null)
        {
            sitController.standUpDuration = 0.35f;
            sitController.StandUp();
        }

        ShowSubtitle("SG-2 DELA CRUZ [ANOMALOUS OVERRIDE]", "WALANG AALIS... HINDI PA TAPOS ANG CLEARANCE MO... DITO KA NA HABANGBUHAY!", "LOBBY CHECKPOINT // ANOMALY ENTITY");

        float riseElapsed = 0f;
        float riseDuration = 2.8f;
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(riseElapsed / riseDuration);

            // Vertigo zoom punch from 58 deg down to 43 deg
            _cutsceneCamera.fieldOfView = Mathf.Lerp(58f, 43f, Mathf.SmoothStep(0f, 1f, t));

            // Anomaly camera twitch/jitter
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * 0.022f;
            _cutsceneCamera.transform.position = cam2Pos + jitter;

            // Direct guard head tracking to snap to the camera lens
            if (_guardHeadBone != null)
            {
                Vector3 toCam = (_cutsceneCamera.transform.position - _guardHeadBone.position).normalized;
                Quaternion lookCam = Quaternion.LookRotation(toCam, Vector3.up);
                _guardHeadBone.rotation = Quaternion.Slerp(_guardHeadBone.rotation, lookCam, Time.unscaledDeltaTime * 12f);
            }

            // Rapid strobe red light
            if (_redEmergencyBeacon != null)
                _redEmergencyBeacon.intensity = 3.2f + Mathf.Sin(Time.time * 16f) * 2.4f;

            yield return null;
        }

        // ── BEAT 4: Flashlight Ignition & Haunted Whistle Blast (2.4s) ───────
        // Mount bright halogen spotlight on the guard pointing toward the camera
        if (_guardCutsceneHalogen == null && boothGuard != null)
        {
            GameObject halGO = new GameObject("GuardCutsceneHalogen");
            halGO.transform.SetParent(boothGuard.transform, false);
            halGO.transform.localPosition = new Vector3(0.25f, 1.45f, 0.40f);
            halGO.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            _guardCutsceneHalogen = halGO.AddComponent<Light>();
            _guardCutsceneHalogen.type = LightType.Spot;
            _guardCutsceneHalogen.color = new Color(0.95f, 0.98f, 1.0f);
            _guardCutsceneHalogen.range = 26.0f;
            _guardCutsceneHalogen.spotAngle = 65.0f;
            _guardCutsceneHalogen.intensity = 4.2f;
            _guardCutsceneHalogen.shadows = LightShadows.Hard;
        }

        // Full-screen white glare flash
        if (_whiteFlashOverlay != null)
        {
            _whiteFlashOverlay.color = new Color(1f, 1f, 1f, 0.85f);
        }

        // Accelerate heartbeat
        if (_heartbeatSource != null)
        {
            _heartbeatSource.pitch = 1.30f;
            _heartbeatSource.volume = 0.75f;
        }

        // Blast the whistle!
        if (_whistleSource != null && hauntedWhistleClip != null)
        {
            _whistleSource.pitch = 0.98f;
            _whistleSource.PlayOneShot(hauntedWhistleClip, 1.0f);
        }

        if (_sfxSource != null && jumpscareHitClip != null)
        {
            _sfxSource.PlayOneShot(jumpscareHitClip, 0.80f);
        }

        ShowSubtitle("SG-2 DELA CRUZ", "*PINATUNOG ANG KANYANG MILITAR NA PITO HABANG DUMIDILAT ANG MGA MATA SA DILIM*", "HAUNTED WHISTLE // SOUNDWAVE");

        float blastElapsed = 0f;
        while (blastElapsed < 2.2f)
        {
            blastElapsed += Time.unscaledDeltaTime;

            // Fade white glare flash
            if (_whiteFlashOverlay != null)
            {
                float flashA = Mathf.Lerp(0.85f, 0f, blastElapsed / 0.7f);
                _whiteFlashOverlay.color = new Color(1f, 1f, 1f, flashA);
            }

            // High intensity camera shockwave vibration decaying over time
            float shake = Mathf.Lerp(0.07f, 0.005f, blastElapsed / 2.2f);
            _cutsceneCamera.transform.position = cam2Pos + UnityEngine.Random.insideUnitSphere * shake;

            yield return null;
        }

        // ── BEAT 5: The Corridor Sweep — Marching into the Hallway (3.4s) ────
        // Place camera in central hallway corridor looking South towards the lobby archway
        Vector3 hallwayCamPos = new Vector3(-84.0f, 2.65f, 25.0f);
        Quaternion hallwayCamRot = Quaternion.Euler(2f, 180f, 0f);

        _cutsceneCamera.fieldOfView = 58f;
        _cutsceneCamera.transform.position = hallwayCamPos;
        _cutsceneCamera.transform.rotation = hallwayCamRot;

        // Clone/Spawn the active Stalker Guard now so we can animate him walking into the corridor
        var stalkerInstance = EnsureStalkerSpawnedForCutscene();

        Vector3 marchStart = new Vector3(-81.5f, 2.36f, 16.5f);
        Vector3 marchEnd = new Vector3(-84.0f, 2.36f, 19.5f);

        if (stalkerInstance != null)
        {
            stalkerInstance.SetActive(true);
            var nav = stalkerInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null) nav.enabled = false; // Scripted march during cutscene
            stalkerInstance.transform.position = marchStart;
            stalkerInstance.transform.rotation = Quaternion.Euler(0f, 270f, 0f);

            var stalkerAnim = stalkerInstance.GetComponentInChildren<Animator>();
            if (stalkerAnim != null) stalkerAnim.SetFloat("Speed", 1.0f);
        }

        // Now safe to hide the booth guard
        if (boothGuard != null) boothGuard.SetActive(false);

        if (_sfxSource != null && runningFootstepsClip != null)
        {
            _sfxSource.clip = runningFootstepsClip;
            _sfxSource.loop = true;
            _sfxSource.pitch = 0.90f;
            _sfxSource.Play();
        }

        ShowSubtitle("CAMPUS SECURITY [STALKER OVERRIDE]", "IKANDADO ANG LAHAT NG PINTO... COMMENCING PERIMETER HUNT.", "CENTRAL CORRIDOR // MR. X PROTOCOL");

        Light stalkerFlashlight = stalkerInstance != null ? stalkerInstance.GetComponentInChildren<Light>() : null;

        float marchElapsed = 0f;
        float marchDuration = 3.2f;
        while (marchElapsed < marchDuration)
        {
            marchElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(marchElapsed / marchDuration);

            if (stalkerInstance != null)
            {
                stalkerInstance.transform.position = Vector3.Lerp(marchStart, marchEnd, t);
                // Turn from entering lobby (270°) to marching down hallway (0° / North)
                Quaternion startRot = Quaternion.Euler(0f, 270f, 0f);
                Quaternion endRot = Quaternion.Euler(0f, 0f, 0f);
                stalkerInstance.transform.rotation = Quaternion.Slerp(startRot, endRot, t);

                // Sweep flashlight beam across lockers and walls
                if (stalkerFlashlight != null)
                {
                    float sweep = Mathf.Sin(Time.time * 3.5f) * 18.0f;
                    stalkerFlashlight.transform.localRotation = Quaternion.Euler(15f, sweep, 0f);
                }
            }

            // Hallway camera subtle slow creep with handheld breath bob
            float bob = Mathf.Sin(marchElapsed * 4f) * 0.015f;
            _cutsceneCamera.transform.position = hallwayCamPos + new Vector3(0f, bob, -0.3f * t);

            yield return null;
        }

        if (_sfxSource != null && _sfxSource.isPlaying)
        {
            _sfxSource.Stop();
            _sfxSource.loop = false;
        }

        if (_heartbeatSource != null && _heartbeatSource.isPlaying)
        {
            _heartbeatSource.Stop();
        }

        // ── BEAT 6: Hand-off to Active Gameplay & Player Monologue (1.2s) ───
        float fadeOut = 0.40f;
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOut)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            if (_blackBackdrop != null)
                _blackBackdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.95f, fadeElapsed / fadeOut));
            yield return null;
        }

        TearDownCinematicCamera();
        CleanupCutsceneLights(false);
        HideSubtitles();
        SetLetterboxVisible(false);

        // Complete the endgame spawn & handoff
        CompleteEndgameHandoff();

        // Fade in back to player view
        float fadeIn = 0.45f;
        float inElapsed = 0f;
        while (inElapsed < fadeIn)
        {
            inElapsed += Time.unscaledDeltaTime;
            if (_blackBackdrop != null)
                _blackBackdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.95f, 0f, inElapsed / fadeIn));
            yield return null;
        }

        // Player panic gasp and spoken realization
        if (_voiceSource != null && studentGaspClip != null)
        {
            _voiceSource.PlayOneShot(studentGaspClip, 0.85f);
        }

        // Display brief in-character realization prompt on paper dialogue
        ShowSubtitle(
            "ESTUDYANTE (IKAW)",
            "Diyos ko... si Kuya Guard... nilamon siya ng anomaly! Papunta siya rito sa pasilyo! Kailangan kong makalabas sa Main Gate!",
            "PAGTAKAS // EMERGENCY EVASION"
        );

        // Keep player realization visible for 3.5 seconds
        yield return new WaitForSeconds(3.5f);
        HideSubtitles();

        SetUIVisible(false);
    }

    private void FindGuardHeadBone(Transform root)
    {
        _guardHeadBone = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head"))
            {
                _guardHeadBone = t;
                break;
            }
        }
    }

    private GameObject EnsureStalkerSpawnedForCutscene()
    {
        var existingAI = FindAnyObjectByType<SecurityGuardStalkerAI>(FindObjectsInactive.Include);
        if (existingAI != null) return existingAI.gameObject;

        Vector3 spawnPos = new Vector3(-84.0f, 2.36f, 18.0f);
        return SecurityGuardEndgameManager.CreateStalkerInstance(spawnPos);
    }

    /// <summary>
    /// Restores player movement and hands control over to active Stalker AI.
    /// </summary>
    private void CompleteEndgameHandoff()
    {
        IsCutsceneActive = false;

        // Restore player camera and controls
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(true);
            _playerMovement.isDialogueCameraOverride = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        HideGameplayHUD(false);

        // Activate stalker in SecurityGuardEndgameManager
        if (SecurityGuardEndgameManager.Instance != null)
        {
            SecurityGuardEndgameManager.Instance.CompleteEndgameActivation();
        }

        // Update Objective HUD
        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "ESCAPE THE ANOMALOUS SECURITY GUARD!",
                "The possessed Security Guard is hunting you through the hallways! Evade his flashlight, hide in lockers if needed, and reach the Main Entrance Gate!"
            );
        }

        OnCutsceneFinished?.Invoke();
        Debug.Log("[GuardChaseCutsceneDirector] Cutscene finished! Stalker AI is actively hunting the player.");
    }

    // ── UI Helper Methods ────────────────────────────────────────────────────

    private void ShowSubtitle(string speaker, string dialogue, string contextTag = "LOBBY CHECKPOINT // RED PROTOCOL")
    {
        if (_subtitleBox == null) return;
        _subtitleBox.SetActive(true);
        if (_speakerTagText != null) _speakerTagText.text = speaker;
        if (_contextTagText != null) _contextTagText.text = contextTag;

        if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
        _typewriterRoutine = StartCoroutine(TypewriterReveal(dialogue));
    }

    private IEnumerator TypewriterReveal(string fullText)
    {
        if (_subtitleText == null) yield break;
        _subtitleText.text = "";
        for (int i = 0; i < fullText.Length; i++)
        {
            _subtitleText.text = fullText.Substring(0, i + 1);

            // Play typewriter key mechanical click on alternating characters
            if (i % 2 == 0 && _typewriterAudioSource != null && typewriterTickClip != null)
            {
                _typewriterAudioSource.pitch = UnityEngine.Random.Range(1.8f, 2.2f);
                _typewriterAudioSource.PlayOneShot(typewriterTickClip, 0.12f);
            }

            yield return new WaitForSecondsRealtime(0.018f);
        }
        _subtitleText.text = fullText;
    }

    private void HideSubtitles()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        if (_subtitleBox != null) _subtitleBox.SetActive(false);
    }

    private void SetLetterboxVisible(bool visible)
    {
        if (_letterboxTop != null) _letterboxTop.gameObject.SetActive(visible);
        if (_letterboxBottom != null) _letterboxBottom.gameObject.SetActive(visible);
    }

    private void SetUIVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    private void HideGameplayHUD(bool hide)
    {
        if (ObjectiveHUD.Instance != null && ObjectiveHUD.Instance.canvasGroup != null)
        {
            ObjectiveHUD.Instance.canvasGroup.alpha = hide ? 0f : 1f;
        }

        var slotCanvas = GameObject.Find("SlotCanvas");
        if (slotCanvas != null)
        {
            var cg = slotCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = slotCanvas.AddComponent<CanvasGroup>();
            cg.alpha = hide ? 0f : 1f;
        }

        var hudCanvas = GameObject.Find("HudCanvas");
        if (hudCanvas != null)
        {
            var cg = hudCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = hudCanvas.AddComponent<CanvasGroup>();
            cg.alpha = hide ? 0f : 1f;
        }
    }

    // ── Dynamic Paper UI Builder ─────────────────────────────────────────────

    private void BuildCutsceneUI()
    {
        _canvasGO = new GameObject("GuardChaseCutscene_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 997;

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _canvasGO.GetComponent<CanvasGroup>();

        Font horrorFont = LoadFont("Main Menu/watch people die", "Assets/Main Menu/watch people die.ttf");
        Font typewriterFont = LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");

        // 1. Black Backdrop & Glitch Flash Overlay
        var bgGO = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        _blackBackdrop = bgGO.GetComponent<Image>();
        _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);
        _blackBackdrop.raycastTarget = false;

        var glitchGO = new GameObject("GlitchOverlay", typeof(RectTransform), typeof(Image));
        glitchGO.transform.SetParent(_canvasGO.transform, false);
        var glRT = glitchGO.GetComponent<RectTransform>();
        glRT.anchorMin = Vector2.zero;
        glRT.anchorMax = Vector2.one;
        glRT.sizeDelta = Vector2.zero;
        _glitchOverlay = glitchGO.GetComponent<Image>();
        _glitchOverlay.color = new Color(0.9f, 0.05f, 0.05f, 0f);
        _glitchOverlay.raycastTarget = false;

        var flashGO = new GameObject("WhiteFlashOverlay", typeof(RectTransform), typeof(Image));
        flashGO.transform.SetParent(_canvasGO.transform, false);
        var flRT = flashGO.GetComponent<RectTransform>();
        flRT.anchorMin = Vector2.zero;
        flRT.anchorMax = Vector2.one;
        flRT.sizeDelta = Vector2.zero;
        _whiteFlashOverlay = flashGO.GetComponent<Image>();
        _whiteFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
        _whiteFlashOverlay.raycastTarget = false;

        // 2. Cinematic Letterbox Bars
        _letterboxTop = CreateLetterbox("Letterbox_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 75f);
        _letterboxBottom = CreateLetterbox("Letterbox_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 75f);

        // 3. Taped Paper Dialogue Box
        BuildSubtitleBox(horrorFont, typewriterFont);

        // 4. Pinned Paper Skip Note (Bottom Right)
        BuildSkipPrompt(typewriterFont);
    }

    private RectTransform CreateLetterbox(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(0f, height);
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    private void BuildSubtitleBox(Font horrorFont, Font typewriterFont)
    {
        _subtitleBox = new GameObject("SubtitlePaperBox", typeof(RectTransform), typeof(Image), typeof(Shadow));
        _subtitleBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _subtitleBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.12f);
        rt.anchorMax = new Vector2(0.5f, 0.12f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 170f);

        var bg = _subtitleBox.GetComponent<Image>();
        if (dialoguePaperSprite != null)
        {
            bg.sprite = dialoguePaperSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.93f, 0.89f, 0.81f, 0.98f);
        }
        else
        {
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
        }

        var shadow = _subtitleBox.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(3f, -4f);

        // Aged Masking Tape Strips on Corners
        CreateTapeStrip("TapeStrip_Left", _subtitleBox.transform, new Vector2(-400f, 82f), -4.5f);
        CreateTapeStrip("TapeStrip_Right", _subtitleBox.transform, new Vector2(400f, 82f), 5.0f);

        // Speaker Tag Banner
        var speakerGO = new GameObject("SpeakerTag", typeof(RectTransform), typeof(Text));
        speakerGO.transform.SetParent(_subtitleBox.transform, false);
        var spRT = speakerGO.GetComponent<RectTransform>();
        spRT.anchorMin = new Vector2(0f, 1f);
        spRT.anchorMax = new Vector2(0f, 1f);
        spRT.pivot = new Vector2(0f, 1f);
        spRT.anchoredPosition = new Vector3(28f, -12f, 0f);
        spRT.sizeDelta = new Vector2(520f, 32f);

        _speakerTagText = speakerGO.GetComponent<Text>();
        _speakerTagText.font = horrorFont != null ? horrorFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _speakerTagText.fontSize = 24;
        _speakerTagText.fontStyle = FontStyle.Bold;
        _speakerTagText.color = new Color(0.72f, 0.08f, 0.08f, 1f);
        _speakerTagText.text = "CAMPUS SECURITY [ANOMALOUS OVERRIDE]";

        // Context / Location Stamp
        var ctxGO = new GameObject("ContextTag", typeof(RectTransform), typeof(Text));
        ctxGO.transform.SetParent(_subtitleBox.transform, false);
        var ctxRT = ctxGO.GetComponent<RectTransform>();
        ctxRT.anchorMin = new Vector2(1f, 1f);
        ctxRT.anchorMax = new Vector2(1f, 1f);
        ctxRT.pivot = new Vector2(1f, 1f);
        ctxRT.anchoredPosition = new Vector3(-28f, -14f, 0f);
        ctxRT.sizeDelta = new Vector2(340f, 26f);

        _contextTagText = ctxGO.GetComponent<Text>();
        _contextTagText.font = typewriterFont != null ? typewriterFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _contextTagText.fontSize = 15;
        _contextTagText.fontStyle = FontStyle.Bold;
        _contextTagText.alignment = TextAnchor.MiddleRight;
        _contextTagText.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        _contextTagText.text = "LOBBY CHECKPOINT // RED PROTOCOL";

        // Body Text
        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(Text));
        bodyGO.transform.SetParent(_subtitleBox.transform, false);
        var bRT = bodyGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot = new Vector2(0.5f, 0.5f);
        bRT.offsetMin = new Vector2(28f, 16f);
        bRT.offsetMax = new Vector2(-28f, -48f);

        _subtitleText = bodyGO.GetComponent<Text>();
        _subtitleText.font = typewriterFont != null ? typewriterFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _subtitleText.fontSize = 19;
        _subtitleText.fontStyle = FontStyle.Normal;
        _subtitleText.lineSpacing = 1.22f;
        _subtitleText.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        _subtitleText.text = "";
    }

    private void CreateTapeStrip(string name, Transform parent, Vector2 pos, float zRot)
    {
        var tapeGO = new GameObject(name, typeof(RectTransform), typeof(Image));
        tapeGO.transform.SetParent(parent, false);
        var rt = tapeGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(70f, 22f);
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, zRot);

        var img = tapeGO.GetComponent<Image>();
        img.color = new Color(0.92f, 0.88f, 0.74f, 0.80f); // Translucent masking tape
        img.raycastTarget = false;
    }

    private void BuildSkipPrompt(Font typewriterFont)
    {
        _skipPromptRoot = new GameObject("SkipPromptRoot", typeof(RectTransform), typeof(Image), typeof(Button));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var rt = _skipPromptRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 85f);
        rt.sizeDelta = new Vector2(260f, 44f);

        var img = _skipPromptRoot.GetComponent<Image>();
        if (buttonPaperSprite != null)
        {
            img.sprite = buttonPaperSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.90f, 0.86f, 0.78f, 0.95f);
        }
        else
        {
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        }

        _skipButton = _skipPromptRoot.GetComponent<Button>();
        _skipButton.onClick.AddListener(SkipCutscene);

        var txtGO = new GameObject("SkipText", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(_skipPromptRoot.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        _skipPromptText = txtGO.GetComponent<Text>();
        _skipPromptText.font = typewriterFont != null ? typewriterFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _skipPromptText.fontSize = 15;
        _skipPromptText.fontStyle = FontStyle.Bold;
        _skipPromptText.alignment = TextAnchor.MiddleCenter;
        _skipPromptText.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        _skipPromptText.text = "[SPACE] / [ESC] Laktawan (Skip)";
    }

    private Font LoadFont(string resourcesPath, string editorPath)
    {
        Font f = Resources.Load<Font>(resourcesPath);
#if UNITY_EDITOR
        if (f == null)
        {
            f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(editorPath);
        }
#endif
        return f;
    }
}
