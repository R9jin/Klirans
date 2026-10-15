using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// WinCutsceneDirector — Cinematic victory ending cutscene for KLIRANS.
///
/// Narrative Scenario:
/// Having collected all official department signatures and validated the clearance slip,
/// the student reaches the main campus exit gate.
/// The heavy iron chains snap open, and the student bolts through the threshold into the night air.
/// The gates slam shut behind them, sealing the nightmare inside.
/// Looking back at the pitch-black university building one last time, the student gazes down at
/// their official, validated clearance slip—stamped and cleared before midnight.
///
/// Features:
/// - Cinematic 2.35:1 widescreen letterboxing.
/// - Dynamic 3D camera choreography: gate breach, outdoor sprint, turnaround to face the school, and slip inspection.
/// - Atmospheric sound design: gate creaks, heavy running steps, relieved panting, door slam, and victory chime.
/// - Holographic Clearance Slip preview card with 6 official seals and "CANDIDATE FOR GRADUATION" stamp.
/// - Taglish subtitles and inner dialogue.
/// - Polished Victory Screen with stats and interactive buttons: Play Again, Main Menu, and Quit.
/// - Bottom-right responsive Skip button: "[SPACE] / [ESC] Laktawan (Skip)".
/// - Debug replay key (F8) in editor and development builds.
/// </summary>
public class WinCutsceneDirector : MonoBehaviour
{
    public static WinCutsceneDirector Instance { get; private set; }
    public static bool IsCutsceneActive { get; private set; } = false;

    [Header("Configuration")]
    [Tooltip("Scene to load when clicking Play Again.")]
    public string gameplaySceneName = "SampleScene";

    [Tooltip("Scene to load when clicking Main Menu.")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Audio Clips")]
    public AudioClip gateOpenClip;
    public AudioClip gateSlamClip;
    public AudioClip runningFootstepsClip;
    public AudioClip relievedBreathingClip;
    public AudioClip gaspingClip;
    public AudioClip victoryChimeClip;
    public AudioClip victoryMusicClip;
    public AudioClip buttonClickClip;

    [Header("Debug")]
    [Tooltip("Press this key in playmode to trigger the win cutscene at any time for testing.")]
    public KeyCode debugTriggerKey = KeyCode.F8;

    // UI Elements
    private GameObject _canvasGO;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private Image _blackBackdrop;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;

    private GameObject _subtitleBox;
    private Text _subtitleText;
    private Text _speakerTagText;

    private GameObject _clearanceCardBox;
    private Text _clearanceTitleText;
    private Text _clearanceStampsText;
    private Text _clearanceStatusStampText;

    private GameObject _victoryScreenBox;
    private Text _victoryTitleText;
    private Text _victorySubtitleText;
    private Text _victoryStatsText;
    private Button _playAgainButton;
    private Button _mainMenuButton;
    private Button _quitButton;

    private GameObject _skipPromptRoot;
    private Text _skipPromptText;
    private Button _skipButton;

    // Audio sources
    private AudioSource _ambienceSource;
    private AudioSource _voiceSource;
    private AudioSource _sfxSource;
    private AudioSource _musicSource;

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
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupAudioSources();
        LoadDefaultAudio();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(debugTriggerKey) && !IsCutsceneActive)
        {
            PlayWinCutscene();
            return;
        }
#endif

        if (!IsCutsceneActive || _hasSkipped) return;

        // Skip input detection: Space, Escape, Return, or button click
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
        {
            SkipCutscene();
        }
    }

    private void SetupAudioSources()
    {
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.playOnAwake = false;
        _ambienceSource.loop = true;
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.volume = 0.45f;

        _voiceSource = gameObject.AddComponent<AudioSource>();
        _voiceSource.playOnAwake = false;
        _voiceSource.loop = false;
        _voiceSource.spatialBlend = 0f;
        _voiceSource.volume = 0.85f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 0.90f;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.volume = 0.50f;
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (gateOpenClip == null)
            gateOpenClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
        if (gateSlamClip == null)
            gateSlamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (runningFootstepsClip == null)
            runningFootstepsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
        if (relievedBreathingClip == null)
            relievedBreathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");
        if (gaspingClip == null)
            gaspingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");
        if (victoryChimeClip == null)
            victoryChimeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
        if (victoryMusicClip == null)
            victoryMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/main-menu-music.mp3");
#endif
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

    /// <summary>
    /// Starts the cinematic victory cutscene.
    /// </summary>
    public void PlayWinCutscene()
    {
        if (IsCutsceneActive) return;

        FindPlayerReferences();

        IsCutsceneActive = true;
        _hasSkipped = false;

        // Lock player movement and camera controls
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
        }

        // Hide regular gameplay HUD overlays
        HideGameplayHUD(true);

        // Reset UI components
        SetLetterboxVisible(true);
        HideSubtitles();
        if (_clearanceCardBox != null) _clearanceCardBox.SetActive(false);
        if (_victoryScreenBox != null) _victoryScreenBox.SetActive(false);
        if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);

        SetUIVisible(true);

        if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
        _cutsceneRoutine = StartCoroutine(CutsceneSequence());
    }

    /// <summary>
    /// Skips directly to the victory summary screen.
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

        if (_ambienceSource != null) _ambienceSource.Stop();
        if (_voiceSource != null) _voiceSource.Stop();
        if (_sfxSource != null) _sfxSource.Stop();

        StartCoroutine(FastSkipToVictoryScreen());
    }

    private IEnumerator FastSkipToVictoryScreen()
    {
        HideSubtitles();
        if (_clearanceCardBox != null) _clearanceCardBox.SetActive(false);

        // Fade backdrop to solid black
        float fade = 0.35f;
        Color c = _blackBackdrop != null ? _blackBackdrop.color : Color.black;
        while (fade > 0f)
        {
            fade -= Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(1f - (fade / 0.35f));
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        DisplayVictoryScreen();
    }

    // ── Cutscene Animation Choreography ──────────────────────────────────────

    private IEnumerator CutsceneSequence()
    {
        // ── BEAT 1: Gate Unlocking & Creaking Open ───────────────────────────
        if (_playerCamera != null)
        {
            // Position camera facing the gate
            _playerCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // Play heavy iron door creaking open
        if (_sfxSource != null && gateOpenClip != null)
        {
            _sfxSource.PlayOneShot(gateOpenClip, 0.95f);
        }

        ShowSubtitle("STUDENT", "Bukas na ang mga trangka ng gate! Heto na ang pagkakataon ko...!");

        yield return new WaitForSeconds(2.8f);

        // ── BEAT 2: Frantic Sprint Out Through the Threshold ─────────────────
        ShowSubtitle("STUDENT", "TAKBO! HUWAG KANG LILINGON!");

        if (_voiceSource != null && gaspingClip != null)
        {
            _voiceSource.PlayOneShot(gaspingClip, 0.90f);
        }

        if (_sfxSource != null && runningFootstepsClip != null)
        {
            _sfxSource.clip = runningFootstepsClip;
            _sfxSource.loop = true;
            _sfxSource.pitch = 1.25f;
            _sfxSource.Play();
        }

        // Camera rushes forward to the outside sidewalk while bobbing
        Vector3 playerStartPos = (_playerMovement != null) ? _playerMovement.transform.position : new Vector3(-75.5f, 2.35f, 16.5f);
        Vector3 targetOutdoorPos = new Vector3(-68.5f, playerStartPos.y, 16.5f);

        float runDuration = 2.2f;
        float elapsedRun = 0f;
        while (elapsedRun < runDuration)
        {
            elapsedRun += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedRun / runDuration);

            if (_playerMovement != null)
            {
                _playerMovement.transform.position = Vector3.Lerp(playerStartPos, targetOutdoorPos, t);
            }

            // Head bobbing during sprint
            if (_playerCamera != null)
            {
                float bob = Mathf.Sin(elapsedRun * 14f) * 0.08f;
                _playerCamera.transform.localPosition = _originalCamLocalPos + new Vector3(0f, bob, 0f);
            }

            yield return null;
        }

        if (_sfxSource != null && _sfxSource.isPlaying)
        {
            _sfxSource.Stop();
            _sfxSource.loop = false;
            _sfxSource.pitch = 1.0f;
        }

        // Violent gate slam behind the player sealing the campus!
        if (_sfxSource != null && gateSlamClip != null)
        {
            _sfxSource.PlayOneShot(gateSlamClip, 1.0f);
        }

        yield return new WaitForSeconds(0.8f);

        // ── BEAT 3: Stop Outside & Look Back at the Dark Campus ──────────────
        if (_voiceSource != null && relievedBreathingClip != null)
        {
            _voiceSource.clip = relievedBreathingClip;
            _voiceSource.loop = true;
            _voiceSource.Play();
        }

        // Turn camera 180 degrees to look back at the looming building
        if (_playerCamera != null)
        {
            Quaternion startRot = _playerCamera.transform.localRotation;
            Quaternion backRot = Quaternion.Euler(-4f, 180f, 0f);

            float turnDuration = 1.2f;
            float turnElapsed = 0f;
            while (turnElapsed < turnDuration)
            {
                turnElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, turnElapsed / turnDuration);
                _playerCamera.transform.localRotation = Quaternion.Slerp(startRot, backRot, t);
                yield return null;
            }
        }

        ShowSubtitle("STUDENT", "N-nakalabas ako... Buhay ako... Nakatakas ako sa loob ng campus!");

        yield return new WaitForSeconds(3.5f);

        // ── BEAT 4: Clearance Slip Reveal Card ───────────────────────────────
        // Glance down at hands
        if (_playerCamera != null)
        {
            Quaternion lookDownRot = Quaternion.Euler(28f, 180f, 0f);
            float lookElapsed = 0f;
            while (lookElapsed < 0.6f)
            {
                lookElapsed += Time.unscaledDeltaTime;
                _playerCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(-4f, 180f, 0f), lookDownRot, lookElapsed / 0.6f);
                yield return null;
            }
        }

        // Display Clearance Slip Holographic Card
        if (_clearanceCardBox != null)
        {
            _clearanceCardBox.SetActive(true);
        }

        if (_sfxSource != null && victoryChimeClip != null)
        {
            _sfxSource.PlayOneShot(victoryChimeClip, 1.0f);
        }

        // Play victory music in background
        if (_musicSource != null && victoryMusicClip != null)
        {
            _musicSource.clip = victoryMusicClip;
            _musicSource.volume = 0.45f;
            _musicSource.Play();
        }

        ShowSubtitle("STUDENT", "Kumpleto ang lahat ng anim na selyo bago mag-hatinggabi! Makakagraduate na ako!");

        yield return new WaitForSeconds(4.5f);

        if (_clearanceCardBox != null)
        {
            _clearanceCardBox.SetActive(false);
        }

        // ── BEAT 5: Final Night Sky Resolution ───────────────────────────────
        // Look up at the midnight starry sky
        if (_playerCamera != null)
        {
            Quaternion lookUpRot = Quaternion.Euler(-26f, 180f, 0f);
            float lookUpElapsed = 0f;
            while (lookUpElapsed < 0.8f)
            {
                lookUpElapsed += Time.unscaledDeltaTime;
                _playerCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(28f, 180f, 0f), lookUpRot, lookUpElapsed / 0.8f);
                yield return null;
            }
        }

        ShowSubtitle("STUDENT", "Wala na... Tapos na ang bangungot. Ligtas na ako sa wakas.");

        yield return new WaitForSeconds(3.5f);

        HideSubtitles();

        // ── BEAT 6: Fade to Cinematic Black & Show Victory Card ──────────────
        float fadeElapsed = 0f;
        Color c = _blackBackdrop != null ? _blackBackdrop.color : Color.black;
        while (fadeElapsed < 1.0f)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(fadeElapsed / 1.0f);
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        DisplayVictoryScreen();
    }

    private void DisplayVictoryScreen()
    {
        if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        if (_blackBackdrop != null) _blackBackdrop.color = Color.black;

        SetLetterboxVisible(false);
        HideSubtitles();
        if (_skipPromptRoot != null) _skipPromptRoot.SetActive(false);

        // Ensure victory music continues
        if (_musicSource != null && !_musicSource.isPlaying && victoryMusicClip != null)
        {
            _musicSource.clip = victoryMusicClip;
            _musicSource.volume = 0.50f;
            _musicSource.Play();
        }

        if (_victoryScreenBox != null)
        {
            _victoryScreenBox.SetActive(true);
        }

        // Unlock cursor for victory buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnCutsceneFinished?.Invoke();
        Debug.Log("[WinCutsceneDirector] Win cutscene sequence concluded — Victory screen active!");
    }

    // ── Button Handlers ──────────────────────────────────────────────────────

    public void OnPlayAgainClicked()
    {
        PlayButtonSound();
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnMainMenuClicked()
    {
        PlayButtonSound();
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnQuitClicked()
    {
        PlayButtonSound();
        Application.Quit();
    }

    private void PlayButtonSound()
    {
        if (_sfxSource != null && buttonClickClip != null)
        {
            _sfxSource.PlayOneShot(buttonClickClip, 0.85f);
        }
    }

    // ── UI Helper Methods ────────────────────────────────────────────────────

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
        _canvasGO = new GameObject("WinCutscene_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998; // Highest priority overlay

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
        _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);
        _blackBackdrop.raycastTarget = false;

        // 2. Cinematic Letterbox Bars (2.35:1 aspect)
        _letterboxTop = CreateLetterbox("Letterbox_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 85f);
        _letterboxBottom = CreateLetterbox("Letterbox_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 85f);

        // 3. Subtitles Box
        BuildSubtitleBox(retroFont, secondaryFont);

        // 4. Hologram Clearance Slip Preview Card
        BuildClearanceCard(retroFont, secondaryFont);

        // 5. Final Victory Screen Box
        BuildVictoryScreenBox(retroFont, secondaryFont);

        // 6. Skip Prompt (Bottom Right)
        BuildSkipPrompt(retroFont);
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

    private void BuildSubtitleBox(Font primaryFont, Font secondaryFont)
    {
        _subtitleBox = new GameObject("SubtitleBox", typeof(RectTransform), typeof(Image));
        _subtitleBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _subtitleBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.12f);
        rt.anchorMax = new Vector2(0.5f, 0.12f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1100f, 85f);

        var bg = _subtitleBox.GetComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.03f, 0.85f);

        var outline = _subtitleBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.7f, 0.4f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        var tagGO = new GameObject("SpeakerTag", typeof(RectTransform), typeof(Text));
        tagGO.transform.SetParent(_subtitleBox.transform, false);
        var trt = tagGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.02f, 0.60f);
        trt.anchorMax = new Vector2(0.35f, 0.95f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        _speakerTagText = tagGO.GetComponent<Text>();
        _speakerTagText.font = primaryFont;
        _speakerTagText.fontSize = 17;
        _speakerTagText.fontStyle = FontStyle.Bold;
        _speakerTagText.color = new Color(0.35f, 0.95f, 0.55f);
        _speakerTagText.alignment = TextAnchor.MiddleLeft;

        var subGO = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_subtitleBox.transform, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.02f, 0.05f);
        srt.anchorMax = new Vector2(0.98f, 0.60f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        _subtitleText = subGO.GetComponent<Text>();
        _subtitleText.font = secondaryFont;
        _subtitleText.fontSize = 17;
        _subtitleText.color = new Color(0.95f, 0.95f, 0.95f);
        _subtitleText.alignment = TextAnchor.MiddleLeft;
    }

    private void BuildClearanceCard(Font primaryFont, Font secondaryFont)
    {
        _clearanceCardBox = new GameObject("ClearanceCardBox", typeof(RectTransform), typeof(Image));
        _clearanceCardBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _clearanceCardBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.52f);
        rt.anchorMax = new Vector2(0.5f, 0.52f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(740f, 380f);

        var bg = _clearanceCardBox.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.06f, 0.94f);

        var outline = _clearanceCardBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.85f, 0.45f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header Title
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_clearanceCardBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.04f, 0.84f);
        trt.anchorMax = new Vector2(0.96f, 0.98f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        _clearanceTitleText = titleGO.GetComponent<Text>();
        _clearanceTitleText.font = primaryFont;
        _clearanceTitleText.fontSize = 20;
        _clearanceTitleText.fontStyle = FontStyle.Bold;
        _clearanceTitleText.color = new Color(0.35f, 0.95f, 0.55f);
        _clearanceTitleText.text = "PAMANTASAN NG CABUYAO // STUDENT CLEARANCE FORM";
        _clearanceTitleText.alignment = TextAnchor.MiddleCenter;

        // Signatures Grid Text
        var stampsGO = new GameObject("StampsText", typeof(RectTransform), typeof(Text));
        stampsGO.transform.SetParent(_clearanceCardBox.transform, false);
        var srt = stampsGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.06f, 0.22f);
        srt.anchorMax = new Vector2(0.94f, 0.82f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        _clearanceStampsText = stampsGO.GetComponent<Text>();
        _clearanceStampsText.font = secondaryFont;
        _clearanceStampsText.fontSize = 16;
        _clearanceStampsText.lineSpacing = 1.35f;
        _clearanceStampsText.color = new Color(0.90f, 0.95f, 0.90f);
        _clearanceStampsText.text =
            "[✓] LIBRARY & ARCHIVES          — SEALED & SIGNED (Rm 308)\n" +
            "[✓] GUIDANCE & COUNSELING       — SEALED & SIGNED (Rm 204)\n" +
            "[✓] COMPUTER LABORATORY        — SEALED & SIGNED (Rm 105)\n" +
            "[✓] UNIVERSITY CLINIC          — SEALED & SIGNED (Rm 102)\n" +
            "[✓] CASHIER & ACCOUNTING       — SEALED & SIGNED (Window 1)\n" +
            "[✓] REGISTRAR / EXEC. VP       — SEALED & SIGNED (Window 2)";

        // Golden Graduation Stamp
        var stampGO = new GameObject("GraduationStamp", typeof(RectTransform), typeof(Text));
        stampGO.transform.SetParent(_clearanceCardBox.transform, false);
        var stmpRT = stampGO.GetComponent<RectTransform>();
        stmpRT.anchorMin = new Vector2(0.04f, 0.04f);
        stmpRT.anchorMax = new Vector2(0.96f, 0.20f);
        stmpRT.offsetMin = Vector2.zero;
        stmpRT.offsetMax = Vector2.zero;
        _clearanceStatusStampText = stampGO.GetComponent<Text>();
        _clearanceStatusStampText.font = primaryFont;
        _clearanceStatusStampText.fontSize = 19;
        _clearanceStatusStampText.fontStyle = FontStyle.Bold;
        _clearanceStatusStampText.color = new Color(1.0f, 0.82f, 0.25f);
        _clearanceStatusStampText.alignment = TextAnchor.MiddleCenter;
        _clearanceStatusStampText.text = "★ OFFICIAL VERDICT: CLEARED // CANDIDATE FOR GRADUATION ★";
    }

    private void BuildVictoryScreenBox(Font primaryFont, Font secondaryFont)
    {
        _victoryScreenBox = new GameObject("VictoryScreenBox", typeof(RectTransform), typeof(Image));
        _victoryScreenBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _victoryScreenBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 620f);

        var bg = _victoryScreenBox.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.05f, 0.96f);

        var outline = _victoryScreenBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.85f, 0.45f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Title
        var titleGO = new GameObject("VictoryTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_victoryScreenBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.05f, 0.84f);
        trt.anchorMax = new Vector2(0.95f, 0.97f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        _victoryTitleText = titleGO.GetComponent<Text>();
        _victoryTitleText.font = primaryFont;
        _victoryTitleText.fontSize = 38;
        _victoryTitleText.fontStyle = FontStyle.Bold;
        _victoryTitleText.color = new Color(0.35f, 0.95f, 0.55f);
        _victoryTitleText.alignment = TextAnchor.MiddleCenter;
        _victoryTitleText.text = "CLEARANCE GRANTED";

        // Subtitle
        var subGO = new GameObject("VictorySubtitle", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_victoryScreenBox.transform, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.05f, 0.74f);
        srt.anchorMax = new Vector2(0.95f, 0.83f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        _victorySubtitleText = subGO.GetComponent<Text>();
        _victorySubtitleText.font = secondaryFont;
        _victorySubtitleText.fontSize = 18;
        _victorySubtitleText.color = new Color(0.85f, 0.92f, 0.85f);
        _victorySubtitleText.alignment = TextAnchor.MiddleCenter;
        _victorySubtitleText.text = "Nakatakas ka sa bangungot ng pamantasan bago mag-hatinggabi.";

        // Stats card box
        var statsBox = new GameObject("StatsBox", typeof(RectTransform), typeof(Image));
        statsBox.transform.SetParent(_victoryScreenBox.transform, false);
        var sbrt = statsBox.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(0.08f, 0.38f);
        sbrt.anchorMax = new Vector2(0.92f, 0.71f);
        sbrt.offsetMin = Vector2.zero;
        sbrt.offsetMax = Vector2.zero;
        statsBox.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.03f, 0.85f);

        var statsTextGO = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
        statsTextGO.transform.SetParent(statsBox.transform, false);
        var strt = statsTextGO.GetComponent<RectTransform>();
        strt.anchorMin = new Vector2(0.06f, 0.05f);
        strt.anchorMax = new Vector2(0.94f, 0.95f);
        strt.offsetMin = Vector2.zero;
        strt.offsetMax = Vector2.zero;
        _victoryStatsText = statsTextGO.GetComponent<Text>();
        _victoryStatsText.font = secondaryFont;
        _victoryStatsText.fontSize = 17;
        _victoryStatsText.lineSpacing = 1.4f;
        _victoryStatsText.color = new Color(0.90f, 0.95f, 0.90f);
        _victoryStatsText.text =
            "• STATUS: ALL 6 DEPARTMENT SIGNATURES SECURED (6/6)\n" +
            "• CAMPUS MAIN GATE: BREACHED & ESCAPED\n" +
            "• DEADLINE STATUS: SUBMITTED BEFORE MIDNIGHT\n" +
            "• FINAL VERDICT: CANDIDATE FOR GRADUATION";

        // Buttons: Play Again, Main Menu, Quit
        _playAgainButton = CreateVictoryButton("PlayAgainButton", "MAGLARO MULI // PLAY AGAIN", new Vector2(0.5f, 0.27f), new Vector2(480f, 48f), primaryFont, OnPlayAgainClicked);
        _mainMenuButton = CreateVictoryButton("MainMenuButton", "PANGUNAHING MENU // MAIN MENU", new Vector2(0.5f, 0.17f), new Vector2(480f, 48f), primaryFont, OnMainMenuClicked);
        _quitButton = CreateVictoryButton("QuitButton", "LUMABAS SA LARO // QUIT", new Vector2(0.5f, 0.07f), new Vector2(480f, 48f), primaryFont, OnQuitClicked);
    }

    private Button CreateVictoryButton(string name, string label, Vector2 anchor, Vector2 size, Font font, UnityEngine.Events.UnityAction action)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(_victoryScreenBox.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.12f, 0.24f, 0.16f, 0.95f);

        var outline = btnGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.95f, 0.55f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1.0f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        btn.colors = colors;
        if (action != null) btn.onClick.AddListener(action);

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(btnGO.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var txt = textGO.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 17;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(0.95f, 1.0f, 0.95f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;

        return btn;
    }

    private void BuildSkipPrompt(Font font)
    {
        _skipPromptRoot = new GameObject("SkipPromptRoot", typeof(RectTransform), typeof(Image), typeof(Button));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var rt = _skipPromptRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 22f);
        rt.sizeDelta = new Vector2(320f, 40f);

        var img = _skipPromptRoot.GetComponent<Image>();
        img.color = new Color(0.04f, 0.04f, 0.05f, 0.75f);

        var outline = _skipPromptRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.95f, 0.55f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        _skipButton = _skipPromptRoot.GetComponent<Button>();
        _skipButton.onClick.AddListener(SkipCutscene);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(_skipPromptRoot.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        _skipPromptText = textGO.GetComponent<Text>();
        _skipPromptText.font = font;
        _skipPromptText.fontSize = 15;
        _skipPromptText.color = new Color(0.75f, 0.95f, 0.80f);
        _skipPromptText.alignment = TextAnchor.MiddleCenter;
        _skipPromptText.text = "[SPACE] / [ESC] Laktawan (Skip)";
    }

    private Font LoadFont(string resourcesPath, string assetPath)
    {
        Font font = Resources.Load<Font>(resourcesPath);
#if UNITY_EDITOR
        if (font == null)
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(assetPath);
#endif
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }
}
