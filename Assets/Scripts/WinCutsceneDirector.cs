using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// WinCutsceneDirector — Cinematic victory ending cutscene for KLIRANS.
/// Features authentic paper UI graphics (ScrambledPaper_HUD, GuidancePaperSheet, signed6,
/// PaidClearedStamp, ArchivedStamp, KeypadButton_Normal) and 3D outdoor gate escape choreography.
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

    [Header("Paper UI Sprites")]
    public Sprite dialoguePaperSprite;
    public Sprite blankPaperSprite;
    public Sprite signedSlipSprite;
    public Sprite documentSheetSprite;
    public Sprite paidClearedStampSprite;
    public Sprite archivedStampSprite;
    public Sprite buttonPaperSprite;

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
    private Text _contextTagText;
    private Coroutine _typewriterRoutine;

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
    private CharacterController _characterController;
    private Camera _playerCamera;
    private Vector3 _originalCamLocalPos;
    private Quaternion _originalCamLocalRot;
    private Transform _securityGateTransform;
    private Vector3 _securityGateClosedPos;
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
        LoadDefaultAssets();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void Start()
    {
        EnsureGateBarrier();
    }

    private void EnsureGateBarrier()
    {
        if (_securityGateTransform == null)
        {
            var gateGO = GameObject.Find("06_Security_Grille_Gate");
            if (gateGO != null)
            {
                _securityGateTransform = gateGO.transform;
                _securityGateClosedPos = _securityGateTransform.position;
            }
        }

        if (_securityGateTransform != null)
        {
            // Ensure the exit gate has a solid barrier collider so the player cannot slip through without winning!
            var col = _securityGateTransform.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = _securityGateTransform.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(2.8f, 3.2f, 0.4f);
                col.center = new Vector3(0f, 1.6f, 0f);
            }
            col.isTrigger = false;
            col.enabled = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameModeManager.IsFreeRoamMode && Input.GetKeyDown(debugTriggerKey) && !IsCutsceneActive)
        {
            PlayWinCutscene();
            return;
        }
#endif

        // Proximity trigger: If clearance slip is officially submitted or gate is unlocked,
        // reaching the lobby main entrance gate triggers the win escape sequence!
        if (!IsCutsceneActive && (DevDemoPanel.IsGateUnlocked || (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)))
        {
            if (_playerMovement == null) FindPlayerReferences();
            if (_playerMovement != null)
            {
                Vector3 p = _playerMovement.transform.position;
                Vector3 gateTarget = new Vector3(-75.48f, 2.35f, 14.43f);
                float distToGate = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(gateTarget.x, 0, gateTarget.z));
                float dy = Mathf.Abs(p.y - gateTarget.y);
                if (distToGate < 3.2f && dy < 2.5f)
                {
                    PlayWinCutscene();
                    return;
                }
            }
        }

        if (!IsCutsceneActive || _hasSkipped) return;

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

    private void LoadDefaultAssets()
    {
#if UNITY_EDITOR
        if (dialoguePaperSprite == null)
            dialoguePaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");
        if (blankPaperSprite == null)
            blankPaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Blank_paper.png");
        if (signedSlipSprite == null)
            signedSlipSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/signed6.png");
        if (documentSheetSprite == null)
            documentSheetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Blank_paper.png");
        if (paidClearedStampSprite == null)
            paidClearedStampSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/PaidClearedStamp.png");
        if (archivedStampSprite == null)
            archivedStampSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ArchivedStamp.png");
        if (buttonPaperSprite == null)
            buttonPaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/KeypadButton_Normal.png");

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

        if (_playerMovement != null)
        {
            _characterController = _playerMovement.GetComponent<CharacterController>();
            if (_playerMovement.playerCamera != null)
            {
                _playerCamera = _playerMovement.playerCamera;
                _originalCamLocalPos = _playerCamera.transform.localPosition;
                _originalCamLocalRot = _playerCamera.transform.localRotation;
            }
        }

        if (_playerCamera == null && Camera.main != null)
        {
            _playerCamera = Camera.main;
            _originalCamLocalPos = _playerCamera.transform.localPosition;
            _originalCamLocalRot = _playerCamera.transform.localRotation;
        }

        if (_securityGateTransform == null)
        {
            var gateGO = GameObject.Find("06_Security_Grille_Gate");
            if (gateGO != null)
            {
                _securityGateTransform = gateGO.transform;
                _securityGateClosedPos = _securityGateTransform.position;
            }
        }
    }

    /// <summary>
    /// Starts the cinematic victory cutscene.
    /// </summary>
    public void PlayWinCutscene()
    {
        if (IsCutsceneActive) return;

        // If the opening cutscene is still active when F8 is pressed, stop it cleanly first
        if (OpeningCutsceneDirector.Instance != null && OpeningCutsceneDirector.IsCutsceneActive)
        {
            OpeningCutsceneDirector.Instance.SkipCutscene();
        }

        FindPlayerReferences();

        IsCutsceneActive = true;
        _hasSkipped = false;

        // Lock player movement and disable CharacterController so scripted movement is smooth
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
        }
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }
        if (_playerCamera != null)
        {
            var heldFlashlight = _playerCamera.transform.Find("HeldFlashlight");
            if (heldFlashlight != null) heldFlashlight.gameObject.SetActive(false);
        }

        HideGameplayHUD(true);

        SetLetterboxVisible(true);
        HideSubtitles();
        if (_clearanceCardBox != null) _clearanceCardBox.SetActive(false);
        if (_victoryScreenBox != null) _victoryScreenBox.SetActive(false);
        if (_skipPromptRoot != null) _skipPromptRoot.SetActive(true);
        if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        if (_ambienceSource != null) _ambienceSource.Stop();
        if (_voiceSource != null) _voiceSource.Stop();
        if (_sfxSource != null) _sfxSource.Stop();

        // Place camera at the outdoor turnaround vantage point looking at the school gate
        if (_securityGateTransform != null)
            _securityGateTransform.position = _securityGateClosedPos;
        if (_playerMovement != null)
        {
            _playerMovement.transform.position = new Vector3(-71.8f, 2.35f, 16.50f);
            _playerMovement.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
        }
        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos;
            _playerCamera.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
        }

        StartCoroutine(FastSkipToVictoryScreen());
    }

    private IEnumerator FastSkipToVictoryScreen()
    {
        HideSubtitles();
        if (_clearanceCardBox != null) _clearanceCardBox.SetActive(false);

        float fade = 0.35f;
        Color c = _blackBackdrop != null ? _blackBackdrop.color : new Color(0f, 0f, 0f, 0.45f);
        while (fade > 0f)
        {
            fade -= Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(0f, 0.45f, 1f - (fade / 0.35f));
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        DisplayVictoryScreen();
    }

    // ── Cutscene Animation Choreography ──────────────────────────────────────

    private IEnumerator CutsceneSequence()
    {
        // ── BEAT 1: Right-Side Exit Gate Unlocking & Creaking Open ───────────
        // Pass cleanly along the right side corridor (Z = 14.43), completely avoiding the security guard table (Z = 16.50)
        Vector3 lobbyStartPos = new Vector3(-79.5f, 2.35f, 14.43f);
        Vector3 outdoorSidewalkPos = new Vector3(-70.2f, 2.35f, 14.43f);

        if (_playerMovement != null)
        {
            _playerMovement.transform.position = lobbyStartPos;
            // Face East (+X, Yaw = 90°) directly toward the Right Exit Gate and street
            _playerMovement.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }
        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos;
            _playerCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        if (_sfxSource != null && gateOpenClip != null)
        {
            _sfxSource.PlayOneShot(gateOpenClip, 0.95f);
        }

        ShowSubtitle("ESTUDYANTE (IKAW)", "Bukas na ang mga trangka ng Main Gate sa kanan! Kumpleto na ang clearance ko—heto na ang pagkakataon ko!", "PNC RIGHT EXIT GATE // 11:58 PM");

        // Locate right exit gate and disable any blocking solid colliders
        Transform rightGateTransform = null;
        var rightGateGO = GameObject.Find("RightEntrance_BarredGate");
        if (rightGateGO != null)
        {
            rightGateTransform = rightGateGO.transform;
        }
        else if (_securityGateTransform != null)
        {
            rightGateTransform = _securityGateTransform;
        }

        var solidBlocker = GameObject.Find("LobbyExit_SolidGateBlocker");
        if (solidBlocker != null)
        {
            var bCol = solidBlocker.GetComponent<Collider>();
            if (bCol != null) bCol.enabled = false;
        }

        if (rightGateTransform != null)
        {
            foreach (var col in rightGateTransform.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }

        Vector3 rightGateClosedPos = rightGateTransform != null ? rightGateTransform.position : Vector3.zero;
        Vector3 rightGateOpenPos = rightGateClosedPos + new Vector3(0f, 0f, -2.5f);

        // Slide the iron right gate open smoothly
        float gateOpenDuration = 2.4f;
        float gateElapsed = 0f;
        while (gateElapsed < gateOpenDuration)
        {
            gateElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, gateElapsed / gateOpenDuration);
            if (rightGateTransform != null)
                rightGateTransform.position = Vector3.Lerp(rightGateClosedPos, rightGateOpenPos, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);

        // ── BEAT 2: Frantic Sprint Out Through the Right Gate Threshold ───────
        ShowSubtitle("ESTUDYANTE (IKAW)", "TAKBO! LABAS NA SA CAMPUS BAGO MAG-HATINGGABI!", "PAGTAKAS // RIGHT EXIT GATE");

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

        float runDuration = 2.2f;
        float elapsedRun = 0f;
        while (elapsedRun < runDuration)
        {
            elapsedRun += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedRun / runDuration);

            if (_playerMovement != null)
            {
                _playerMovement.transform.position = Vector3.Lerp(lobbyStartPos, outdoorSidewalkPos, t);
            }

            if (_playerCamera != null)
            {
                float bob = Mathf.Sin(elapsedRun * 14f) * 0.08f;
                float roll = Mathf.Cos(elapsedRun * 7f) * 1.4f;
                _playerCamera.transform.localPosition = _originalCamLocalPos + new Vector3(0f, bob, 0f);
                _playerCamera.transform.localRotation = Quaternion.Euler(2f, 0f, roll);
            }

            yield return null;
        }

        if (_sfxSource != null && _sfxSource.isPlaying)
        {
            _sfxSource.Stop();
            _sfxSource.loop = false;
            _sfxSource.pitch = 1.0f;
        }

        // Slam the right gate shut behind the student!
        if (rightGateTransform != null)
        {
            rightGateTransform.position = rightGateClosedPos;
        }
        if (_sfxSource != null && gateSlamClip != null)
        {
            _sfxSource.PlayOneShot(gateSlamClip, 1.0f);
        }

        yield return new WaitForSeconds(0.6f);

        // ── BEAT 3: Stop on the Lit Sidewalk & Turn Back to Face the Campus ──
        if (_voiceSource != null && relievedBreathingClip != null)
        {
            _voiceSource.clip = relievedBreathingClip;
            _voiceSource.loop = true;
            _voiceSource.Play();
        }

        // Smooth 180-degree world turnaround from East (90°) to West (270°) so the camera looks back at the PNC Archway & Gate
        Quaternion startYaw = Quaternion.Euler(0f, 90f, 0f);
        Quaternion endYaw = Quaternion.Euler(0f, 270f, 0f);
        float turnDuration = 1.25f;
        float turnElapsed = 0f;
        while (turnElapsed < turnDuration)
        {
            turnElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, turnElapsed / turnDuration);
            if (_playerMovement != null)
                _playerMovement.transform.rotation = Quaternion.Slerp(startYaw, endYaw, t);
            if (_playerCamera != null)
            {
                _playerCamera.transform.localPosition = _originalCamLocalPos;
                _playerCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(2f, 0f, 0f), Quaternion.Euler(-4f, 0f, 0f), t);
            }
            yield return null;
        }

        ShowSubtitle("ESTUDYANTE (IKAW)", "N-nakalabas ako... Buhay ako! Nakatakas ako mula sa loob ng BCH Building!", "LABAS NG CAMPUS // LIGTAS");

        yield return new WaitForSeconds(3.4f);

        // ── BEAT 4: Official Signed Clearance Slip Inspection ────────────────
        if (_playerCamera != null)
        {
            Quaternion lookDownRot = Quaternion.Euler(18f, 0f, 0f);
            float lookElapsed = 0f;
            while (lookElapsed < 0.6f)
            {
                lookElapsed += Time.unscaledDeltaTime;
                _playerCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(-4f, 0f, 0f), lookDownRot, lookElapsed / 0.6f);
                yield return null;
            }
        }

        if (_clearanceCardBox != null)
        {
            _clearanceCardBox.SetActive(true);
        }

        if (_sfxSource != null && victoryChimeClip != null)
        {
            _sfxSource.PlayOneShot(victoryChimeClip, 1.0f);
        }

        if (_musicSource != null && victoryMusicClip != null)
        {
            _musicSource.clip = victoryMusicClip;
            _musicSource.volume = 0.45f;
            _musicSource.Play();
        }

        ShowSubtitle("ESTUDYANTE (IKAW)", "Kumpleto ang lahat ng anim na pirma at selyo bago mag-hatinggabi! Makakagraduate na ako sa wakas!", "CLEARANCE VALIDATED // 6/6");

        yield return new WaitForSeconds(4.4f);

        if (_clearanceCardBox != null)
        {
            _clearanceCardBox.SetActive(false);
        }

        // ── BEAT 5: Final Glance at the Campus Gate ──────────────────────────
        if (_playerCamera != null)
        {
            Quaternion lookArchRot = Quaternion.Euler(-6f, 0f, 0f);
            float lookUpElapsed = 0f;
            while (lookUpElapsed < 0.7f)
            {
                lookUpElapsed += Time.unscaledDeltaTime;
                _playerCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(18f, 0f, 0f), lookArchRot, lookUpElapsed / 0.7f);
                yield return null;
            }
        }

        ShowSubtitle("ESTUDYANTE (IKAW)", "Tapos na ang bangungot ng clearance signing. Ligtas na ako.", "11:59 PM // CLEARED");

        yield return new WaitForSeconds(3.2f);

        HideSubtitles();

        // ── BEAT 6: Dim Vignette & Present Official Paper Victory Document ───
        float fadeElapsed = 0f;
        Color c = new Color(0f, 0f, 0f, 0f);
        while (fadeElapsed < 0.8f)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(0f, 0.45f, fadeElapsed / 0.8f);
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        DisplayVictoryScreen();
    }

    private void DisplayVictoryScreen()
    {
        if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        if (_blackBackdrop != null) _blackBackdrop.color = new Color(0f, 0f, 0f, 0.48f);

        SetLetterboxVisible(false);
        HideSubtitles();
        if (_skipPromptRoot != null) _skipPromptRoot.SetActive(false);

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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnCutsceneFinished?.Invoke();
        Debug.Log("[WinCutsceneDirector] Win cutscene sequence concluded — Victory screen active!");
    }

    // ── Button Handlers ──────────────────────────────────────────────────────

    public void OnPlayAgainClicked()
    {
        PlayButtonSound();
        IsCutsceneActive = false;
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnMainMenuClicked()
    {
        PlayButtonSound();
        IsCutsceneActive = false;
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

    private void ShowSubtitle(string speaker, string dialogue, string contextTag = "KLIRANS // TALA")
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
            yield return new WaitForSecondsRealtime(0.016f);
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
        _canvasGO = new GameObject("WinCutscene_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998;

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _canvasGO.GetComponent<CanvasGroup>();

        Font horrorFont = LoadFont("Main Menu/watch people die", "Assets/Main Menu/watch people die.ttf");
        Font typewriterFont = LoadFont("Fonts/Lucida_Typewriter_Bold", "Assets/Fonts/Lucida_Typewriter_Bold.ttf") ?? LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");

        // 1. Dim/Black Backdrop
        var bgGO = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        _blackBackdrop = bgGO.GetComponent<Image>();
        _blackBackdrop.color = new Color(0f, 0f, 0f, 0f);
        _blackBackdrop.raycastTarget = false;

        // 2. Cinematic Letterbox Bars
        _letterboxTop = CreateLetterbox("Letterbox_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 75f);
        _letterboxBottom = CreateLetterbox("Letterbox_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 75f);

        // 3. Taped Paper Dialogue Box (matching NPCDialogueSystem)
        BuildSubtitleBox(horrorFont, typewriterFont);

        // 4. Signed Paper Clearance Form Preview Card (Beat 4)
        BuildClearanceCard(horrorFont, typewriterFont);

        // 5. Final Official Paper Clearance Certificate Victory Screen
        BuildVictoryScreenBox(horrorFont, typewriterFont);

        // 6. Pinned Paper Skip Note (Bottom Right)
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
        _subtitleBox = new GameObject("SubtitlePaperBox", typeof(RectTransform), typeof(Image));
        _subtitleBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _subtitleBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.13f);
        rt.anchorMax = new Vector2(0.5f, 0.13f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 180f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -0.5f);

        var bg = _subtitleBox.GetComponent<Image>();
        bg.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : blankPaperSprite;
        bg.color = new Color(0.96f, 0.92f, 0.84f, 0.98f);

        var shadow = _subtitleBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(4f, -5f);

        // Speaker Header (Left, crimson horror font)
        var tagGO = new GameObject("SpeakerTag", typeof(RectTransform), typeof(Text));
        tagGO.transform.SetParent(_subtitleBox.transform, false);
        var trt = tagGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.09f, 0.65f);
        trt.anchorMax = new Vector2(0.62f, 0.87f);
        trt.sizeDelta = Vector2.zero;
        _speakerTagText = tagGO.GetComponent<Text>();
        _speakerTagText.font = horrorFont;
        _speakerTagText.fontSize = 21;
        _speakerTagText.fontStyle = FontStyle.Bold;
        _speakerTagText.color = new Color(0.68f, 0.05f, 0.05f, 1f);
        _speakerTagText.alignment = TextAnchor.MiddleLeft;

        // Context / Location Tag (Right, faded typewriter ink)
        var ctxGO = new GameObject("ContextTag", typeof(RectTransform), typeof(Text));
        ctxGO.transform.SetParent(_subtitleBox.transform, false);
        var crt = ctxGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.58f, 0.66f);
        crt.anchorMax = new Vector2(0.91f, 0.86f);
        crt.sizeDelta = Vector2.zero;
        _contextTagText = ctxGO.GetComponent<Text>();
        _contextTagText.font = typewriterFont;
        _contextTagText.fontSize = 13;
        _contextTagText.fontStyle = FontStyle.Bold;
        _contextTagText.alignment = TextAnchor.MiddleRight;
        _contextTagText.color = new Color(0.32f, 0.23f, 0.17f, 0.90f);

        // Subtle Ink Divider Rule
        var divGO = new GameObject("InkDivider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(_subtitleBox.transform, false);
        var drt = divGO.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.09f, 0.62f);
        drt.anchorMax = new Vector2(0.91f, 0.63f);
        drt.sizeDelta = Vector2.zero;
        divGO.GetComponent<Image>().color = new Color(0.25f, 0.16f, 0.12f, 0.35f);

        // Dialogue Body Text (Dark charcoal typewriter ink)
        var subGO = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_subtitleBox.transform, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.09f, 0.14f);
        srt.anchorMax = new Vector2(0.91f, 0.59f);
        srt.sizeDelta = Vector2.zero;
        _subtitleText = subGO.GetComponent<Text>();
        _subtitleText.font = typewriterFont;
        _subtitleText.fontSize = 18;
        _subtitleText.lineSpacing = 1.18f;
        _subtitleText.color = new Color(0.07f, 0.05f, 0.04f, 1f);
        _subtitleText.alignment = TextAnchor.UpperLeft;
        _subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildClearanceCard(Font horrorFont, Font typewriterFont)
    {
        _clearanceCardBox = new GameObject("ClearanceCardPaper", typeof(RectTransform), typeof(Image));
        _clearanceCardBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _clearanceCardBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 410f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -0.8f);

        var bg = _clearanceCardBox.GetComponent<Image>();
        bg.sprite = documentSheetSprite != null ? documentSheetSprite : (blankPaperSprite != null ? blankPaperSprite : dialoguePaperSprite);
        bg.color = new Color(0.96f, 0.92f, 0.85f, 0.99f);

        var shadow = _clearanceCardBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
        shadow.effectDistance = new Vector2(6f, -7f);

        // Left: Actual Signed 6-Department Clearance Slip Graphic (signed6.png)
        if (signedSlipSprite != null)
        {
            var slipGO = new GameObject("SignedSlipGraphic", typeof(RectTransform), typeof(Image));
            slipGO.transform.SetParent(_clearanceCardBox.transform, false);
            var slrt = slipGO.GetComponent<RectTransform>();
            slrt.anchorMin = new Vector2(0.11f, 0.16f);
            slrt.anchorMax = new Vector2(0.43f, 0.84f);
            slrt.sizeDelta = Vector2.zero;
            slrt.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            var sImg = slipGO.GetComponent<Image>();
            sImg.sprite = signedSlipSprite;
            sImg.preserveAspect = true;
            sImg.color = Color.white;
        }

        // Rubber Stamp Overlay (PaidClearedStamp.png)
        if (paidClearedStampSprite != null)
        {
            var stampImgGO = new GameObject("PaidClearedStamp", typeof(RectTransform), typeof(Image));
            stampImgGO.transform.SetParent(_clearanceCardBox.transform, false);
            var sirt = stampImgGO.GetComponent<RectTransform>();
            sirt.anchorMin = new Vector2(0.68f, 0.14f);
            sirt.anchorMax = new Vector2(0.89f, 0.42f);
            sirt.sizeDelta = Vector2.zero;
            sirt.localRotation = Quaternion.Euler(0f, 0f, -14f);
            var stImg = stampImgGO.GetComponent<Image>();
            stImg.sprite = paidClearedStampSprite;
            stImg.preserveAspect = true;
            stImg.color = new Color(1f, 1f, 1f, 0.88f);
        }

        // Header Title
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_clearanceCardBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.45f, 0.72f);
        trt.anchorMax = new Vector2(0.89f, 0.86f);
        trt.sizeDelta = Vector2.zero;
        _clearanceTitleText = titleGO.GetComponent<Text>();
        _clearanceTitleText.font = horrorFont;
        _clearanceTitleText.fontSize = 20;
        _clearanceTitleText.fontStyle = FontStyle.Bold;
        _clearanceTitleText.color = new Color(0.68f, 0.05f, 0.05f, 1f);
        _clearanceTitleText.text = "PAMANTASAN NG CABUYAO // CLEARED";
        _clearanceTitleText.alignment = TextAnchor.MiddleLeft;

        // Signatures Grid Text in dark typewriter ink
        var stampsGO = new GameObject("StampsText", typeof(RectTransform), typeof(Text));
        stampsGO.transform.SetParent(_clearanceCardBox.transform, false);
        var srt = stampsGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.45f, 0.26f);
        srt.anchorMax = new Vector2(0.89f, 0.71f);
        srt.sizeDelta = Vector2.zero;
        _clearanceStampsText = stampsGO.GetComponent<Text>();
        _clearanceStampsText.font = typewriterFont;
        _clearanceStampsText.fontSize = 17;
        _clearanceStampsText.fontStyle = FontStyle.Bold;
        _clearanceStampsText.lineSpacing = 1.28f;
        _clearanceStampsText.color = new Color(0.04f, 0.04f, 0.04f, 1f);
        _clearanceStampsText.text =
            "[X] LIBRARY & ARCHIVES    — SIGNED (Rm 308)\n" +
            "[X] GUIDANCE OFFICE       — SIGNED (Rm 207)\n" +
            "[X] COMPUTER LABORATORY   — SIGNED (Rm 105)\n" +
            "[X] UNIVERSITY CLINIC     — SIGNED (Rm 102)\n" +
            "[X] CASHIER WINDOW        — SIGNED (Win 1)\n" +
            "[X] REGISTRAR / EVP       — SIGNED (Win 2)";

        // Official Verdict Line
        var stampGO = new GameObject("GraduationStamp", typeof(RectTransform), typeof(Text));
        stampGO.transform.SetParent(_clearanceCardBox.transform, false);
        var stmpRT = stampGO.GetComponent<RectTransform>();
        stmpRT.anchorMin = new Vector2(0.45f, 0.13f);
        stmpRT.anchorMax = new Vector2(0.89f, 0.25f);
        stmpRT.sizeDelta = Vector2.zero;
        _clearanceStatusStampText = stampGO.GetComponent<Text>();
        _clearanceStatusStampText.font = typewriterFont;
        _clearanceStatusStampText.fontSize = 17;
        _clearanceStatusStampText.fontStyle = FontStyle.Bold;
        _clearanceStatusStampText.color = new Color(0.62f, 0.06f, 0.06f, 1f);
        _clearanceStatusStampText.alignment = TextAnchor.MiddleLeft;
        _clearanceStatusStampText.text = "VERDICT: CANDIDATE FOR GRADUATION (6/6)";
    }

    private void BuildVictoryScreenBox(Font horrorFont, Font typewriterFont)
    {
        _victoryScreenBox = new GameObject("VictoryScreenDocument", typeof(RectTransform), typeof(Image));
        _victoryScreenBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _victoryScreenBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920f, 670f);

        var bg = _victoryScreenBox.GetComponent<Image>();
        bg.sprite = documentSheetSprite != null ? documentSheetSprite : (blankPaperSprite != null ? blankPaperSprite : dialoguePaperSprite);
        bg.color = new Color(0.96f, 0.93f, 0.86f, 0.99f);

        var shadow = _victoryScreenBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(8f, -9f);

        // Decorative Rubber Stamps in Top Corners
        if (paidClearedStampSprite != null)
        {
            var stampL = new GameObject("StampCleared", typeof(RectTransform), typeof(Image));
            stampL.transform.SetParent(_victoryScreenBox.transform, false);
            var slRT = stampL.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0.71f, 0.74f);
            slRT.anchorMax = new Vector2(0.88f, 0.89f);
            slRT.sizeDelta = Vector2.zero;
            slRT.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var slImg = stampL.GetComponent<Image>();
            slImg.sprite = paidClearedStampSprite;
            slImg.preserveAspect = true;
            slImg.color = new Color(1f, 1f, 1f, 0.90f);
            slImg.raycastTarget = false;
        }

        if (archivedStampSprite != null)
        {
            var stampR = new GameObject("StampArchived", typeof(RectTransform), typeof(Image));
            stampR.transform.SetParent(_victoryScreenBox.transform, false);
            var srRT = stampR.GetComponent<RectTransform>();
            srRT.anchorMin = new Vector2(0.12f, 0.74f);
            srRT.anchorMax = new Vector2(0.28f, 0.89f);
            srRT.sizeDelta = Vector2.zero;
            srRT.localRotation = Quaternion.Euler(0f, 0f, 9f);
            var srImg = stampR.GetComponent<Image>();
            srImg.sprite = archivedStampSprite;
            srImg.preserveAspect = true;
            srImg.color = new Color(1f, 1f, 1f, 0.85f);
            srImg.raycastTarget = false;
        }

        // Title (Crimson Horror Stamp Header)
        var titleGO = new GameObject("VictoryTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_victoryScreenBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.14f, 0.78f);
        trt.anchorMax = new Vector2(0.86f, 0.88f);
        trt.sizeDelta = Vector2.zero;
        _victoryTitleText = titleGO.GetComponent<Text>();
        _victoryTitleText.font = horrorFont;
        _victoryTitleText.fontSize = 36;
        _victoryTitleText.fontStyle = FontStyle.Bold;
        _victoryTitleText.color = new Color(0.68f, 0.05f, 0.05f, 1f);
        _victoryTitleText.alignment = TextAnchor.MiddleCenter;
        _victoryTitleText.text = "CLEARANCE GRANTED";

        // Subtitle in dark typewriter ink
        var subGO = new GameObject("VictorySubtitle", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_victoryScreenBox.transform, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.14f, 0.70f);
        srt.anchorMax = new Vector2(0.86f, 0.78f);
        srt.sizeDelta = Vector2.zero;
        _victorySubtitleText = subGO.GetComponent<Text>();
        _victorySubtitleText.font = typewriterFont;
        _victorySubtitleText.fontSize = 15;
        _victorySubtitleText.fontStyle = FontStyle.Bold;
        _victorySubtitleText.color = new Color(0.12f, 0.09f, 0.07f, 0.96f);
        _victorySubtitleText.alignment = TextAnchor.MiddleCenter;
        _victorySubtitleText.text = "Nakatakas ka sa bangungot ng pamantasan bago mag-hatinggabi.";

        // Pinned Paper Note Card for Stats + Signed Slip Preview
        var statsBox = new GameObject("StatsPaperNote", typeof(RectTransform), typeof(Image));
        statsBox.transform.SetParent(_victoryScreenBox.transform, false);
        var sbrt = statsBox.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(0.12f, 0.36f);
        sbrt.anchorMax = new Vector2(0.88f, 0.70f);
        sbrt.sizeDelta = Vector2.zero;
        var sbImg = statsBox.GetComponent<Image>();
        sbImg.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : blankPaperSprite;
        sbImg.color = new Color(0.94f, 0.89f, 0.80f, 0.98f);

        if (signedSlipSprite != null)
        {
            var miniSlip = new GameObject("MiniSignedSlip", typeof(RectTransform), typeof(Image));
            miniSlip.transform.SetParent(statsBox.transform, false);
            var msRT = miniSlip.GetComponent<RectTransform>();
            msRT.anchorMin = new Vector2(0.08f, 0.14f);
            msRT.anchorMax = new Vector2(0.32f, 0.86f);
            msRT.sizeDelta = Vector2.zero;
            msRT.localRotation = Quaternion.Euler(0f, 0f, 2.2f);
            var msImg = miniSlip.GetComponent<Image>();
            msImg.sprite = signedSlipSprite;
            msImg.preserveAspect = true;
            msImg.color = Color.white;
        }

        var statsTextGO = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
        statsTextGO.transform.SetParent(statsBox.transform, false);
        var strt = statsTextGO.GetComponent<RectTransform>();
        strt.anchorMin = new Vector2(0.35f, 0.14f);
        strt.anchorMax = new Vector2(0.91f, 0.86f);
        strt.sizeDelta = Vector2.zero;
        _victoryStatsText = statsTextGO.GetComponent<Text>();
        _victoryStatsText.font = typewriterFont;
        _victoryStatsText.fontSize = 17;
        _victoryStatsText.fontStyle = FontStyle.Bold;
        _victoryStatsText.lineSpacing = 1.35f;
        _victoryStatsText.color = new Color(0.04f, 0.04f, 0.04f, 1f);
        _victoryStatsText.alignment = TextAnchor.MiddleLeft;
        _victoryStatsText.text =
            "• CLEARANCE SIGNATURES: 6 / 6 KUMPLETO\n" +
            "• CAMPUS MAIN GATE: NABUKSAN AT NAKATAKAS\n" +
            "• ORAS NG PAGLABAS: 11:59 PM (UMABOT SA CUTOFF)\n" +
            "• HATOL NG REGISTRAR: CANDIDATE FOR GRADUATION";

        // Paper Card Buttons: Play Again, Main Menu, Quit
        _playAgainButton = CreateVictoryButton("PlayAgainButton", "MAGLARO MULI // PLAY AGAIN", new Vector2(0.5f, 0.29f), new Vector2(440f, 48f), typewriterFont, OnPlayAgainClicked);
        _mainMenuButton = CreateVictoryButton("MainMenuButton", "PANGUNAHING MENU // MAIN MENU", new Vector2(0.5f, 0.20f), new Vector2(440f, 48f), typewriterFont, OnMainMenuClicked);
        _quitButton = CreateVictoryButton("QuitButton", "LUMABAS SA LARO // QUIT", new Vector2(0.5f, 0.11f), new Vector2(440f, 48f), typewriterFont, OnQuitClicked);
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
        img.sprite = buttonPaperSprite != null ? buttonPaperSprite : dialoguePaperSprite;
        img.color = new Color(0.93f, 0.87f, 0.76f, 0.98f);

        var shadow = btnGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(3f, -4f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.0f, 0.92f, 0.82f, 1.0f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.65f, 1.0f);
        btn.colors = colors;
        if (action != null) btn.onClick.AddListener(action);

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(btnGO.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.06f, 0.10f);
        trt.anchorMax = new Vector2(0.94f, 0.90f);
        trt.sizeDelta = Vector2.zero;

        var txt = textGO.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(0.14f, 0.06f, 0.05f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;

        return btn;
    }

    private void BuildSkipPrompt(Font font)
    {
        _skipPromptRoot = new GameObject("SkipPromptPaper", typeof(RectTransform), typeof(Image), typeof(Button));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var rt = _skipPromptRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-36f, 24f);
        rt.sizeDelta = new Vector2(285f, 54f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -1.5f);

        var img = _skipPromptRoot.GetComponent<Image>();
        img.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : buttonPaperSprite;
        img.color = new Color(0.95f, 0.90f, 0.82f, 0.96f);

        var shadow = _skipPromptRoot.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
        shadow.effectDistance = new Vector2(3f, -4f);

        _skipButton = _skipPromptRoot.GetComponent<Button>();
        _skipButton.onClick.AddListener(SkipCutscene);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(_skipPromptRoot.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.08f, 0.12f);
        trt.anchorMax = new Vector2(0.92f, 0.88f);
        trt.sizeDelta = Vector2.zero;

        _skipPromptText = textGO.GetComponent<Text>();
        _skipPromptText.font = font;
        _skipPromptText.fontSize = 14;
        _skipPromptText.fontStyle = FontStyle.Bold;
        _skipPromptText.color = new Color(0.12f, 0.06f, 0.05f, 0.98f);
        _skipPromptText.alignment = TextAnchor.MiddleCenter;
        _skipPromptText.text = "[SPACE / ESC] LAKTAWAN";
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
