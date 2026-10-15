using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OpeningCutsceneDirector — Cinematic opening cutscene for KLIRANS.
///
/// Narrative Scenario:
/// The student was exhausted from days of clearance requirements and fell fast asleep at an armchair desk.
/// They wake up groggily to find it's past 7:00 PM—the university has locked down, the power grid fails,
/// and it's the FINAL NIGHT DEADLINE to complete all 6 department clearance stamps before midnight...
/// or remain trapped with the entities roaming the dark.
///
/// Features:
/// - Procedural eyelid blinking animation (top/bottom eyelids fluttering open with blur/vignette).
/// - 3D Camera tilt & lift from desk to standing view.
/// - Atmospheric Taglish subtitles and inner dialogue.
/// - Fluorescent light buzz, ticking clock, sudden gasp, power surge blackout, and stalker audio cues.
/// - Bottom-right responsive Skip button: "[SPACE] / [ESC] Laktawan (Skip)".
/// - Seamless zero-cut transition into player control.
/// </summary>
public class OpeningCutsceneDirector : MonoBehaviour
{
    public static OpeningCutsceneDirector Instance { get; private set; }
    public static bool IsCutsceneActive { get; private set; } = false;

    [Header("Configuration")]
    [Tooltip("Play the cutscene on game launch.")]
    public bool playOnStart = true;

    [Tooltip("Force play cutscene even if previously played this session.")]
    public bool forcePlayEveryLaunch = true;

    [Header("Audio Clips")]
    public AudioClip tapeHissAmbience;
    public AudioClip tickingClockClip;
    public AudioClip groggyBreathClip;
    public AudioClip gaspClip;
    public AudioClip powerOutageClip;
    public AudioClip doorSlamClip;
    public AudioClip monsterCueClip;
    public AudioClip objectiveChimeClip;

    [Header("Debug")]
    [Tooltip("Press this key in playmode to replay the cutscene at any time for testing.")]
    public KeyCode replayCutsceneKey = KeyCode.F7;

    // UI Elements
    private GameObject _canvasGO;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private RectTransform _eyelidTop;
    private RectTransform _eyelidBottom;
    private Image _blackBackdrop;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;

    private GameObject _subtitleBox;
    private Text _subtitleText;
    private Text _speakerTagText;

    private GameObject _timeCardBox;
    private Text _timeCardClockText;
    private Text _timeCardWarningText;

    private GameObject _clearanceSlipPreviewBox;
    private Text _clearanceTitleText;
    private Text _clearanceStampsText;

    private GameObject _skipPromptRoot;
    private Text _skipPromptText;
    private Button _skipButton;

    // Audio sources
    private AudioSource _ambienceSource;
    private AudioSource _voiceSource;
    private AudioSource _sfxSource;

    // Runtime state
    private Coroutine _cutsceneRoutine;
    private PlayerMovement _playerMovement;
    private Camera _playerCamera;
    private Vector3 _originalCamLocalPos;
    private Quaternion _originalCamLocalRot;
    private bool _hasSkipped = false;

    public event Action OnCutsceneFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Debug.Log("[OpeningCutsceneDirector] Awake initialized.");

        SetupAudioSources();
        LoadDefaultAudio();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void Start()
    {
        FindPlayerReferences();

        if (playOnStart)
        {
            StartCoroutine(DelayedStartRoutine());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator DelayedStartRoutine()
    {
        // Wait for player movement & other managers to complete Start()
        yield return new WaitForEndOfFrame();
        PlayCutscene();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(replayCutsceneKey) && !IsCutsceneActive)
        {
            PlayCutscene();
            return;
        }
#endif

        if (!IsCutsceneActive || _hasSkipped) return;

        // Skip input detection: Space, Escape, Return, or left click on skip prompt
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
        {
            SkipCutscene();
        }
    }

    private void FindPlayerReferences()
    {
        if (_playerMovement == null)
            _playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (_playerMovement != null && _playerMovement.playerCamera != null)
        {
            _playerCamera = _playerMovement.playerCamera;
            _originalCamLocalPos = _playerCamera.transform.localPosition;
            _originalCamLocalRot = _playerCamera.transform.localRotation;
        }
        else if (Camera.main != null)
        {
            _playerCamera = Camera.main;
            _originalCamLocalPos = _playerCamera.transform.localPosition;
            _originalCamLocalRot = _playerCamera.transform.localRotation;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Begins playing the opening nightmare cutscene from the start.
    /// </summary>
    public void PlayCutscene()
    {
        if (IsCutsceneActive) return;

        FindPlayerReferences();

        IsCutsceneActive = true;
        _hasSkipped = false;
        Debug.Log("[OpeningCutsceneDirector] >>> STARTING OPENING NIGHTMARE CUTSCENE <<<");

        // Hide regular gameplay HUD overlays during the cutscene
        HideGameplayHUD(true);

        // Disable player controls during cutscene
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true; // locks camera sway & breathing
        }

        // Unlock cursor for skip button click or keep accessible
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetUIVisible(true);

        if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
        _cutsceneRoutine = StartCoroutine(CutsceneSequence());
    }

    /// <summary>
    /// Skips the cutscene immediately and smoothly transitions to gameplay.
    /// </summary>
    public void SkipCutscene()
    {
        if (!IsCutsceneActive || _hasSkipped) return;
        _hasSkipped = true;

        if (_cutsceneRoutine != null)
        {
            StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = null;
        }

        // Stop all cutscene sound loops
        if (_ambienceSource != null) _ambienceSource.Stop();
        if (_voiceSource != null) _voiceSource.Stop();
        if (_sfxSource != null) _sfxSource.Stop();

        ConcludeCutscene();
        StartCoroutine(FastSkipTransition());
    }

    private IEnumerator FastSkipTransition()
    {
        // Fade black backdrop out into gameplay
        float fade = 0.30f;
        Color c = _blackBackdrop != null ? _blackBackdrop.color : Color.black;
        while (fade > 0f)
        {
            fade -= Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(fade / 0.30f);
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        SetUIVisible(false);
    }

    private void ConcludeCutscene()
    {
        IsCutsceneActive = false;

        // Reset camera transform
        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos;
            _playerCamera.transform.localRotation = _originalCamLocalRot;
        }

        // Restore player movement & input
        if (_playerMovement != null)
        {
            _playerMovement.isDialogueCameraOverride = false;
            _playerMovement.SyncPitch(0f);
            _playerMovement.SetControlsEnabled(true);
        }

        // Restore HUD overlays
        HideGameplayHUD(false);

        // Lock cursor back to survival horror FPS mode
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Play objective chime and display the opening survival goal!
        if (_sfxSource != null && objectiveChimeClip != null)
        {
            _sfxSource.PlayOneShot(objectiveChimeClip, 0.9f);
        }

        if (ObjectiveHUD.Instance != null)
        {
            bool hasFlashlight = InventoryManager.Instance != null && InventoryManager.Instance.HasItemNamed("Flashlight");
            if (!hasFlashlight)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "KUNIN ANG FLASHLIGHT SA MESA // PICK UP FLASHLIGHT",
                    "Napakadilim! Kunin ang umiilaw na flashlight sa bilog na mesa sa harap mo upang magkaroon ng liwanag! [Pindutin ang E]"
                );
            }
            else
            {
                ObjectiveHUD.Instance.SetObjective(
                    "SURVIVE THE CLEARANCE NIGHTMARE",
                    "Nakatulog ka sa classroom at sarado na ang buong campus! Hanapin ang 4 na piraso ng clearance slip at kumpletuhin ang 6 na signatories bago mag-hatinggabi!"
                );
            }
        }

        OnCutsceneFinished?.Invoke();
        Debug.Log("[OpeningCutsceneDirector] Cutscene finished — player controls fully engaged!");
    }

    // ── Cutscene Animation Choreography ──────────────────────────────────────

    private IEnumerator CutsceneSequence()
    {
        // ── BEAT 0: Initial Pitch Darkness & Slumber ────────────────────────
        _blackBackdrop.color = Color.black;
        SetEyelidsOpenAmount(0.0f); // 0% = completely closed
        SetLetterboxVisible(true);
        HideSubtitles();
        _timeCardBox.SetActive(false);
        _clearanceSlipPreviewBox.SetActive(false);

        // Lower camera to desk-rest posture
        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos + new Vector3(0f, -0.35f, 0.1f);
            _playerCamera.transform.localRotation = Quaternion.Euler(46f, 0f, -3f); // Slumped resting head
        }

        // Play eerie tape hiss + faint clock ticks
        if (_ambienceSource != null && tapeHissAmbience != null)
        {
            _ambienceSource.clip = tapeHissAmbience;
            _ambienceSource.volume = 0.55f;
            _ambienceSource.Play();
        }

        // Heavy, groggy breathing of a sleeping student
        if (_voiceSource != null && groggyBreathClip != null)
        {
            _voiceSource.PlayOneShot(groggyBreathClip, 0.75f);
        }

        yield return new WaitForSeconds(1.8f);

        // ── BEAT 1: First Groggy Blink ──────────────────────────────────────
        // Eyelids open slightly (30%), then shut again ("Ugh...")
        yield return AnimateEyelids(0.0f, 0.35f, 0.9f);
        yield return new WaitForSeconds(0.4f);
        yield return AnimateEyelids(0.35f, 0.0f, 0.6f);

        yield return new WaitForSeconds(0.8f);

        // ── BEAT 2: Waking Up & First Dialogue ──────────────────────────────
        // Eyelids flutter open wider (65%)
        yield return AnimateEyelids(0.0f, 0.70f, 1.2f);

        ShowSubtitle("STUDENT", "Ugh... ang sakit ng leeg ko... Nakatulog yata ako sa desk... Teka, anong oras na ba?");
        
        // Camera lifts up slowly from resting on desk
        yield return AnimateCameraView(
            startPos: _playerCamera != null ? _playerCamera.transform.localPosition : Vector3.zero,
            targetPos: _originalCamLocalPos,
            startRot: _playerCamera != null ? _playerCamera.transform.localRotation : Quaternion.identity,
            targetRot: Quaternion.Euler(12f, 0f, 0f),
            duration: 2.4f
        );

        yield return new WaitForSeconds(1.2f);

        // ── BEAT 3: Clock Check & Sudden Panic ──────────────────────────────
        // Fully open eyes
        yield return AnimateEyelids(0.70f, 1.0f, 0.6f);

        // Display alarming clock card
        _timeCardBox.SetActive(true);
        if (_timeCardClockText != null) _timeCardClockText.text = "19:04 (7:04 PM)";
        if (_timeCardWarningText != null) _timeCardWarningText.text = "[!] CURFEW VIOLATION // BUILDING GATES CLOSED";

        // Sharp panic gasp
        if (_voiceSource != null && gaspClip != null)
        {
            _voiceSource.PlayOneShot(gaspClip, 0.95f);
        }

        // Camera snaps up in shock
        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(12f, 0f, 0f),
            targetRot: Quaternion.Euler(-4f, 0f, 0f),
            duration: 0.35f
        );

        ShowSubtitle("STUDENT", "GAGO! 7:00 PM NA?! Bakit sarado na ang buong building?! Nakatulog ako sa klase?!");

        yield return new WaitForSeconds(3.2f);
        _timeCardBox.SetActive(false);

        // ── BEAT 4: The Nightmare Stakes (Clearance Slip Deadline) ──────────
        // Camera glances down to student's hands/desk
        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos + new Vector3(0f, -0.05f, 0.05f),
            startRot: Quaternion.Euler(-4f, 0f, 0f),
            targetRot: Quaternion.Euler(34f, 0f, 0f),
            duration: 0.8f
        );

        // Display Clearance Slip Hologram Card
        _clearanceSlipPreviewBox.SetActive(true);

        ShowSubtitle("STUDENT", "Patay ako... Huling araw na pala ngayon ng clearance signing! Kapag hindi ko nakumpleto ang 6 na selyo bago mag-hatinggabi, hindi ako makakagraduate!");

        yield return new WaitForSeconds(4.2f);

        ShowSubtitle("STUDENT", "Kailangan kong puntahan ang Library, Guidance, ComLab, Clinic, Cashier, at EVP... Pero bakit parang walang tao rito?!");

        yield return new WaitForSeconds(3.5f);
        _clearanceSlipPreviewBox.SetActive(false);

        // ── BEAT 5: Power Surge Blackout & Horror Threat Emerges ────────────
        // Sudden violent electrical brownout
        if (_sfxSource != null && powerOutageClip != null)
        {
            _sfxSource.PlayOneShot(powerOutageClip, 0.95f);
        }

        // Rapid flickering of blackout screen
        for (int i = 0; i < 4; i++)
        {
            _blackBackdrop.color = new Color(0f, 0f, 0f, 0.85f);
            yield return new WaitForSeconds(0.08f);
            _blackBackdrop.color = new Color(0f, 0f, 0f, 0.2f);
            yield return new WaitForSeconds(0.06f);
        }
        _blackBackdrop.color = new Color(0f, 0f, 0f, 0.5f); // Dim ambient night

        // Distant heavy door slamming shut down hallway
        if (_sfxSource != null && doorSlamClip != null)
        {
            _sfxSource.PlayOneShot(doorSlamClip, 0.85f);
        }

        yield return new WaitForSeconds(0.8f);

        // Stalker breath / cry in the distance
        if (_sfxSource != null && monsterCueClip != null)
        {
            _sfxSource.PlayOneShot(monsterCueClip, 0.90f);
        }

        // Camera jerks in panic (looking left then right down the dark hall)
        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(34f, 0f, 0f),
            targetRot: Quaternion.Euler(0f, -28f, 0f), // Look left
            duration: 0.5f
        );
        yield return new WaitForSeconds(0.3f);

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(0f, -28f, 0f),
            targetRot: Quaternion.Euler(0f, 32f, 0f), // Look right
            duration: 0.6f
        );
        yield return new WaitForSeconds(0.3f);

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(0f, 32f, 0f),
            targetRot: Quaternion.Euler(0f, 0f, 0f), // Back to center
            duration: 0.5f
        );

        ShowSubtitle("STUDENT", "S-sino 'yun?! Bakit nakapatay ang mga ilaw sa hallway?! Hello?! May tao pa ba d'yan?!");

        yield return new WaitForSeconds(3.5f);

        ShowSubtitle("STUDENT", "Hindi 'to normal... Naka-lockdown ang buong campus, at may kung anong gumagala sa dilim. Kailangan kong kumilos... Bago nila ako maabutan!");

        yield return new WaitForSeconds(4.0f);

        // ── BEAT 6: Smooth Transition to Gameplay ───────────────────────────
        HideSubtitles();

        // Fade backdrop completely to clear game view
        float t = 0f;
        Color bgC = _blackBackdrop.color;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            bgC.a = Mathf.Lerp(0.5f, 0f, t / 0.6f);
            _blackBackdrop.color = bgC;
            yield return null;
        }

        SetLetterboxVisible(false);
        ConcludeCutscene();
        SetUIVisible(false);
    }

    // ── Procedural Camera & Eyelid Animation Helpers ─────────────────────────

    private IEnumerator AnimateEyelids(float startOpen, float targetOpen, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float currentOpen = Mathf.Lerp(startOpen, targetOpen, t);
            SetEyelidsOpenAmount(currentOpen);
            yield return null;
        }
        SetEyelidsOpenAmount(targetOpen);
    }

    private void SetEyelidsOpenAmount(float openFactor)
    {
        // openFactor: 0.0 = completely closed, 1.0 = completely open
        float closedHeight = Screen.height * 0.55f;
        float currentHeight = Mathf.Lerp(closedHeight, 0f, Mathf.Clamp01(openFactor));

        if (_eyelidTop != null)
        {
            _eyelidTop.sizeDelta = new Vector2(0f, currentHeight);
        }
        if (_eyelidBottom != null)
        {
            _eyelidBottom.sizeDelta = new Vector2(0f, currentHeight);
        }
    }

    private IEnumerator AnimateCameraView(Vector3 startPos, Vector3 targetPos, Quaternion startRot, Quaternion targetRot, float duration)
    {
        if (_playerCamera == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _playerCamera.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            _playerCamera.transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _playerCamera.transform.localPosition = targetPos;
        _playerCamera.transform.localRotation = targetRot;
    }

    private void ShowSubtitle(string speaker, string dialogue)
    {
        if (_subtitleBox == null) return;
        _subtitleBox.SetActive(true);
        if (_speakerTagText != null) _speakerTagText.text = speaker;
        if (_subtitleText != null) _subtitleText.text = dialogue;
    }

    private void HideSubtitles()
    {
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

    // ── Dynamic UI Builder ───────────────────────────────────────────────────

    private void BuildCutsceneUI()
    {
        _canvasGO = new GameObject("OpeningCutscene_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 990; // High priority above HUD

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _canvasGO.GetComponent<CanvasGroup>();

        Font retroFont = LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");
        Font secondaryFont = LoadFont("Fonts/Lucida_Typewriter_Bold", "Assets/Fonts/Lucida_Typewriter_Bold.ttf");

        // 1. Black Backdrop
        var bgGO = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        _blackBackdrop = bgGO.GetComponent<Image>();
        _blackBackdrop.color = Color.black;
        _blackBackdrop.raycastTarget = false;

        // 2. Eyelids (Top & Bottom)
        _eyelidTop = CreateEyelidBar("Eyelid_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _eyelidBottom = CreateEyelidBar("Eyelid_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

        // 3. Cinematic Letterbox Bars (2.35:1 aspect)
        _letterboxTop = CreateLetterbox("Letterbox_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 80f);
        _letterboxBottom = CreateLetterbox("Letterbox_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 80f);

        // 4. Time Card (Clock & Curfew Overlay)
        BuildTimeCard(retroFont, secondaryFont);

        // 5. Clearance Slip Warning Card
        BuildClearanceCard(retroFont, secondaryFont);

        // 6. Subtitles Panel (Bottom Center)
        BuildSubtitleBox(retroFont, secondaryFont);

        // 7. Skip Prompt (Bottom Right)
        BuildSkipPrompt(retroFont);
    }

    private RectTransform CreateEyelidBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(0f, 600f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.01f, 0.01f, 0.02f, 1f);
        img.raycastTarget = false;
        return rt;
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

    private void BuildTimeCard(Font primaryFont, Font secondaryFont)
    {
        _timeCardBox = new GameObject("TimeCardBox", typeof(RectTransform), typeof(Image));
        _timeCardBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _timeCardBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.76f);
        rt.anchorMax = new Vector2(0.5f, 0.76f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 95f);

        var bg = _timeCardBox.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.05f, 0.88f);

        var outline = _timeCardBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.15f, 0.15f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var clockGO = new GameObject("ClockText", typeof(RectTransform), typeof(Text));
        clockGO.transform.SetParent(_timeCardBox.transform, false);
        var crt = clockGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0.45f);
        crt.anchorMax = new Vector2(1f, 0.95f);
        crt.sizeDelta = Vector2.zero;
        _timeCardClockText = clockGO.GetComponent<Text>();
        _timeCardClockText.font = primaryFont;
        _timeCardClockText.fontSize = 28;
        _timeCardClockText.fontStyle = FontStyle.Bold;
        _timeCardClockText.alignment = TextAnchor.MiddleCenter;
        _timeCardClockText.color = new Color(0.95f, 0.25f, 0.25f, 1f);

        var warnGO = new GameObject("WarningText", typeof(RectTransform), typeof(Text));
        warnGO.transform.SetParent(_timeCardBox.transform, false);
        var wrt = warnGO.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0f, 0.05f);
        wrt.anchorMax = new Vector2(1f, 0.45f);
        wrt.sizeDelta = Vector2.zero;
        _timeCardWarningText = warnGO.GetComponent<Text>();
        _timeCardWarningText.font = secondaryFont;
        _timeCardWarningText.fontSize = 15;
        _timeCardWarningText.alignment = TextAnchor.MiddleCenter;
        _timeCardWarningText.color = new Color(0.85f, 0.85f, 0.85f, 0.95f);
    }

    private void BuildClearanceCard(Font primaryFont, Font secondaryFont)
    {
        _clearanceSlipPreviewBox = new GameObject("ClearancePreviewBox", typeof(RectTransform), typeof(Image));
        _clearanceSlipPreviewBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _clearanceSlipPreviewBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.58f);
        rt.anchorMax = new Vector2(0.5f, 0.58f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640f, 130f);

        var bg = _clearanceSlipPreviewBox.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.05f, 0.05f, 0.92f);

        var outline = _clearanceSlipPreviewBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.2f, 0.2f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var titleGO = new GameObject("ClearanceTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_clearanceSlipPreviewBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.04f, 0.55f);
        trt.anchorMax = new Vector2(0.96f, 0.95f);
        trt.sizeDelta = Vector2.zero;
        _clearanceTitleText = titleGO.GetComponent<Text>();
        _clearanceTitleText.font = primaryFont;
        _clearanceTitleText.fontSize = 18;
        _clearanceTitleText.fontStyle = FontStyle.Bold;
        _clearanceTitleText.alignment = TextAnchor.MiddleCenter;
        _clearanceTitleText.color = new Color(1f, 0.88f, 0.80f, 1f);
        _clearanceTitleText.text = "OFFICIAL UNIVERSITY CLEARANCE // FINAL DEADLINE: TONIGHT 11:59 PM";

        var stampGO = new GameObject("ClearanceStamps", typeof(RectTransform), typeof(Text));
        stampGO.transform.SetParent(_clearanceSlipPreviewBox.transform, false);
        var srt = stampGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.04f, 0.05f);
        srt.anchorMax = new Vector2(0.96f, 0.52f);
        srt.sizeDelta = Vector2.zero;
        _clearanceStampsText = stampGO.GetComponent<Text>();
        _clearanceStampsText.font = secondaryFont;
        _clearanceStampsText.fontSize = 15;
        _clearanceStampsText.alignment = TextAnchor.MiddleCenter;
        _clearanceStampsText.color = new Color(0.92f, 0.32f, 0.32f, 0.98f);
        _clearanceStampsText.text = "[!] 6 MISSING STAMPS: Library  •  Guidance  •  ComLab  •  Clinic  •  Cashier  •  EVP";
    }

    private void BuildSubtitleBox(Font primaryFont, Font secondaryFont)
    {
        _subtitleBox = new GameObject("SubtitleBox", typeof(RectTransform), typeof(Image));
        _subtitleBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _subtitleBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.12f);
        rt.anchorMax = new Vector2(0.5f, 0.12f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(980f, 90f);

        var bg = _subtitleBox.GetComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.03f, 0.85f);

        var outline = _subtitleBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        var spkGO = new GameObject("SpeakerTag", typeof(RectTransform), typeof(Text));
        spkGO.transform.SetParent(_subtitleBox.transform, false);
        var srt = spkGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.03f, 0.65f);
        srt.anchorMax = new Vector2(0.97f, 0.95f);
        srt.sizeDelta = Vector2.zero;
        _speakerTagText = spkGO.GetComponent<Text>();
        _speakerTagText.font = primaryFont;
        _speakerTagText.fontSize = 14;
        _speakerTagText.fontStyle = FontStyle.Bold;
        _speakerTagText.alignment = TextAnchor.MiddleLeft;
        _speakerTagText.color = new Color(0.85f, 0.75f, 0.45f, 1f);

        var subGO = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_subtitleBox.transform, false);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.03f, 0.08f);
        subRT.anchorMax = new Vector2(0.97f, 0.65f);
        subRT.sizeDelta = Vector2.zero;
        _subtitleText = subGO.GetComponent<Text>();
        _subtitleText.font = secondaryFont;
        _subtitleText.fontSize = 17;
        _subtitleText.alignment = TextAnchor.UpperLeft;
        _subtitleText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        _subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildSkipPrompt(Font font)
    {
        _skipPromptRoot = new GameObject("SkipPromptPill", typeof(RectTransform), typeof(Image), typeof(Button));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var rt = _skipPromptRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-45f, 35f);
        rt.sizeDelta = new Vector2(250f, 44f);

        var img = _skipPromptRoot.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.10f, 0.88f);

        var outline = _skipPromptRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.4f, 0.45f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        _skipButton = _skipPromptRoot.GetComponent<Button>();
        _skipButton.onClick.AddListener(SkipCutscene);

        var txtGO = new GameObject("SkipText", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(_skipPromptRoot.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        _skipPromptText = txtGO.GetComponent<Text>();
        _skipPromptText.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _skipPromptText.fontSize = 14;
        _skipPromptText.fontStyle = FontStyle.Bold;
        _skipPromptText.alignment = TextAnchor.MiddleCenter;
        _skipPromptText.color = new Color(0.92f, 0.92f, 0.94f, 0.95f);
        _skipPromptText.text = "[SPACE] / [ESC]  LAKTAWAN";
    }

    // ── Audio Setup ──────────────────────────────────────────────────────────

    private void SetupAudioSources()
    {
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.loop = true;
        _ambienceSource.playOnAwake = false;

        _voiceSource = gameObject.AddComponent<AudioSource>();
        _voiceSource.spatialBlend = 0f;
        _voiceSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.spatialBlend = 0f;
        _sfxSource.playOnAwake = false;
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (tapeHissAmbience == null)
            tapeHissAmbience = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs tape hiss.mp3");

        if (groggyBreathClip == null)
            groggyBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");

        if (gaspClip == null)
            gaspClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");

        if (powerOutageClip == null)
            powerOutageClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/FNAF_ Power Outage - Gaming Sound Effect (HD).mp3");

        if (doorSlamClip == null)
            doorSlamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/universfield-creepy-creaking-door-402155.mp3");

        if (monsterCueClip == null)
            monsterCueClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");

        if (objectiveChimeClip == null)
            objectiveChimeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
    }

    private Font LoadFont(string resourcePath, string editorAssetPath)
    {
        Font font = Resources.Load<Font>(resourcePath);
#if UNITY_EDITOR
        if (font == null) font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);
#endif
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }
}
