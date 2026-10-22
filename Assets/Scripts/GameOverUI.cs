using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameOverUI & Anxiety GameOver Cutscene Director — KLIRANS.
/// Triggers when the player's Anxiety reaches 100% (or via F9 debug key in Playmode).
///
/// Complete 4-Phase Cinematic Horror Sequence:
/// 1. PANIC SURGE: Muffled rapid heartbeat, heavy gasping for air, frantic camera FOV breathing & jitter, pulsating tunnel-vision vignette.
/// 2. PHYSICAL COLLAPSE: Legs buckle, camera plunges to floor (Y ~0.22m, Roll ~40°), impact thud, fading heartbeat.
/// 3. VOID & FINAL BREATH: Total blackout, Tagalog internal monologue text ("Hindi na ako makahinga..."), heartbeat flatlines.
/// 4. OFFICIAL AUTOPSY & PSYCHOLOGICAL REPORT: Authentic university incident report paper UI, live clearance stats,
///    red rubber stamp slam [CLEARANCE TERMINATED // FAILED], Retry and Main Menu buttons.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }
    public static bool IsCutsceneActive { get; private set; } = false;

    [Header("Debug Testing")]
    [Tooltip("Press this key in playmode to trigger the anxiety game over cutscene immediately.")]
    public KeyCode debugTriggerKey = KeyCode.F9;

    [Header("UI Root Elements")]
    public GameObject gameOverPanel;
    public CanvasGroup canvasGroup;

    [Header("Text References (Legacy & Expanded)")]
    public Text titleText;
    public Text subtitleText;

    [Header("Buttons")]
    public Button retryButton;
    public Button menuButton;

    [Header("Audio Clips")]
    public AudioClip deathSound;
    public AudioClip fastHeartbeatClip;
    public AudioClip slowHeartbeatClip;
    public AudioClip gaspingClip;
    public AudioClip breathingClip;
    public AudioClip bodyThudClip;
    public AudioClip stampClip;
    public AudioClip eerieDroneClip;
    public AudioClip buttonClickClip;

    [Header("Paper UI Sprites")]
    public Sprite documentSheetSprite;
    public Sprite buttonSprite;

    // Runtime Audio Sources
    private AudioSource _heartbeatSource;
    private AudioSource _breathingSource;
    private AudioSource _sfxSource;
    private AudioSource _ambienceSource;

    // UI Structure
    private GameObject _canvasGO;
    private Canvas _canvas;
    private Image _vignetteImg;
    private Image _fadeToBlackImg;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;
    private Text _thoughtsText;
    private GameObject _autopsySheetBox;
    private Text _autopsyTitleText;
    private Text _autopsyBodyText;
    private Text _autopsyStatsText;
    private GameObject _stampBox;
    private Text _stampText;
    private Button _skipButton;

    // Player references
    private PlayerMovement _playerMovement;
    private Camera _playerCamera;
    private Vector3 _originalCamLocalPos;
    private Quaternion _originalCamLocalRot;
    private float _originalCamFOV = 60f;

    private bool _hasTriggered = false;
    private bool _hasSkipped = false;
    private float _gameStartTime = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _gameStartTime = Time.time;
        SetupAudioSources();
        LoadDefaultAssets();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void Start()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver += HandleGameOver;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver -= HandleGameOver;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(debugTriggerKey) && !IsCutsceneActive)
        {
            HandleGameOver();
            return;
        }
#endif

        if (!IsCutsceneActive || _hasSkipped) return;

        // Skip to autopsy paper report on Space, Enter, or Escape
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
        {
            SkipToReport();
        }
    }

    private void SetupAudioSources()
    {
        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.playOnAwake = false;
        _heartbeatSource.loop = true;
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.volume = 0.90f;

        _breathingSource = gameObject.AddComponent<AudioSource>();
        _breathingSource.playOnAwake = false;
        _breathingSource.loop = false;
        _breathingSource.spatialBlend = 0f;
        _breathingSource.volume = 0.85f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 0.95f;

        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.playOnAwake = false;
        _ambienceSource.loop = true;
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.volume = 0.50f;
    }

    private void LoadDefaultAssets()
    {
#if UNITY_EDITOR
        if (deathSound == null)
            deathSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");
        if (fastHeartbeatClip == null)
            fastHeartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");
        if (slowHeartbeatClip == null)
            slowHeartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/heartbeat.mp3");
        if (gaspingClip == null)
            gaspingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");
        if (breathingClip == null)
            breathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");
        if (bodyThudClip == null)
            bodyThudClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (stampClip == null)
            stampClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/xeroxSoundEffect_Print.wav");
        if (eerieDroneClip == null)
            eerieDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/dysopian background sound.mp3");
        if (buttonClickClip == null)
            buttonClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/opening door pt.2.mp3");

        if (documentSheetSprite == null)
            documentSheetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/GuidancePaperSheet.png");
        if (documentSheetSprite == null)
            documentSheetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");
        if (buttonSprite == null)
            buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/KeypadButton_Normal.png");
#endif
    }

    private Font GetLegibleFont()
    {
        Font f = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
        if (f == null) f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lucida_Typewriter_Bold.ttf");
#endif
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    private Font GetHorrorFont()
    {
        Font f = Resources.Load<Font>("Fonts/watch people die");
#if UNITY_EDITOR
        if (f == null) f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
        if (f == null) f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/watch people die.ttf");
#endif
        if (f == null) f = GetLegibleFont();
        return f;
    }

    // ── UI Construction ────────────────────────────────────────────────────────
    private void BuildCutsceneUI()
    {
        if (_canvasGO != null) return;

        var typewriterFont = GetLegibleFont();
        var horrorFont = GetHorrorFont();

        _canvasGO = new GameObject("AnxietyGameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // Topmost presentation

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = _canvasGO.AddComponent<CanvasGroup>();
        gameOverPanel = _canvasGO;

        // 1. Fullscreen Pulsating Vignette
        var vigGO = new GameObject("VignetteOverlay", typeof(RectTransform), typeof(Image));
        vigGO.transform.SetParent(_canvasGO.transform, false);
        var vigRT = vigGO.GetComponent<RectTransform>();
        vigRT.anchorMin = Vector2.zero;
        vigRT.anchorMax = Vector2.one;
        vigRT.sizeDelta = Vector2.zero;

        _vignetteImg = vigGO.GetComponent<Image>();
        _vignetteImg.sprite = Sprite.Create(CreateVignetteTexture(), new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
        _vignetteImg.color = new Color(0.12f, 0.01f, 0.01f, 0f);
        _vignetteImg.raycastTarget = false;

        // 2. Fullscreen Fade to Pitch Black
        var blackGO = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
        blackGO.transform.SetParent(_canvasGO.transform, false);
        var blackRT = blackGO.GetComponent<RectTransform>();
        blackRT.anchorMin = Vector2.zero;
        blackRT.anchorMax = Vector2.one;
        blackRT.sizeDelta = Vector2.zero;

        _fadeToBlackImg = blackGO.GetComponent<Image>();
        _fadeToBlackImg.color = new Color(0.01f, 0.01f, 0.01f, 0f);
        _fadeToBlackImg.raycastTarget = false;

        // 3. Cinematic Letterbox Bars
        var barColor = new Color(0.02f, 0.01f, 0.01f, 1f);
        var topBarGO = new GameObject("LetterboxTop", typeof(RectTransform), typeof(Image));
        topBarGO.transform.SetParent(_canvasGO.transform, false);
        _letterboxTop = topBarGO.GetComponent<RectTransform>();
        _letterboxTop.anchorMin = new Vector2(0f, 1f);
        _letterboxTop.anchorMax = new Vector2(1f, 1f);
        _letterboxTop.pivot = new Vector2(0.5f, 1f);
        _letterboxTop.sizeDelta = new Vector2(0f, 0f);
        topBarGO.GetComponent<Image>().color = barColor;

        var bottomBarGO = new GameObject("LetterboxBottom", typeof(RectTransform), typeof(Image));
        bottomBarGO.transform.SetParent(_canvasGO.transform, false);
        _letterboxBottom = bottomBarGO.GetComponent<RectTransform>();
        _letterboxBottom.anchorMin = new Vector2(0f, 0f);
        _letterboxBottom.anchorMax = new Vector2(1f, 0f);
        _letterboxBottom.pivot = new Vector2(0.5f, 0f);
        _letterboxBottom.sizeDelta = new Vector2(0f, 0f);
        bottomBarGO.GetComponent<Image>().color = barColor;

        // 4. Final Monologue / Suffocating Thoughts Text
        var thoughtGO = new GameObject("SuffocatingThoughtsText", typeof(RectTransform), typeof(Text));
        thoughtGO.transform.SetParent(_canvasGO.transform, false);
        var thoughtRT = thoughtGO.GetComponent<RectTransform>();
        thoughtRT.anchorMin = new Vector2(0.5f, 0.5f);
        thoughtRT.anchorMax = new Vector2(0.5f, 0.5f);
        thoughtRT.sizeDelta = new Vector2(900f, 180f);

        _thoughtsText = thoughtGO.GetComponent<Text>();
        _thoughtsText.font = typewriterFont;
        _thoughtsText.fontSize = 24;
        _thoughtsText.color = new Color(0.85f, 0.80f, 0.80f, 0f);
        _thoughtsText.alignment = TextAnchor.MiddleCenter;
        _thoughtsText.fontStyle = FontStyle.Bold;

        // 5. Official Autopsy / Psychological Report Paper Sheet
        _autopsySheetBox = new GameObject("AutopsyPaperSheet", typeof(RectTransform), typeof(Image));
        _autopsySheetBox.transform.SetParent(_canvasGO.transform, false);
        var sheetRT = _autopsySheetBox.GetComponent<RectTransform>();
        sheetRT.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRT.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRT.sizeDelta = new Vector2(760f, 620f);
        sheetRT.localRotation = Quaternion.Euler(0f, 0f, -0.6f);

        var sheetImg = _autopsySheetBox.GetComponent<Image>();
        sheetImg.sprite = documentSheetSprite;
        sheetImg.color = new Color(0.95f, 0.93f, 0.88f, 1f);

        // Document Header
        var titleGO = new GameObject("DocumentTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_autopsySheetBox.transform, false);
        var tRT = titleGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 1f);
        tRT.anchorMax = new Vector2(0.5f, 1f);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0f, -28f);
        tRT.sizeDelta = new Vector2(700f, 100f);

        _autopsyTitleText = titleGO.GetComponent<Text>();
        _autopsyTitleText.font = typewriterFont;
        _autopsyTitleText.fontSize = 17;
        _autopsyTitleText.fontStyle = FontStyle.Bold;
        _autopsyTitleText.color = new Color(0.12f, 0.08f, 0.08f, 1f);
        _autopsyTitleText.alignment = TextAnchor.UpperCenter;
        _autopsyTitleText.text = "REPUBLIKA NG PILIPINAS  •  PAMANTASAN NG CABUYAO\nOFFICE OF STUDENT WELFARE & CAMPUS SECURITY\n<size=19><color=#881111>INCIDENT AUTOPSY & PSYCHOLOGICAL COLLAPSE REPORT</color></size>\nCASE FILE: CASE-PNC-BCH-1994-0824  •  CLASSIFIED";

        // Document Body
        var bodyGO = new GameObject("DocumentBody", typeof(RectTransform), typeof(Text));
        bodyGO.transform.SetParent(_autopsySheetBox.transform, false);
        var bRT = bodyGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.5f, 1f);
        bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -135f);
        bRT.sizeDelta = new Vector2(680f, 210f);

        _autopsyBodyText = bodyGO.GetComponent<Text>();
        _autopsyBodyText.font = typewriterFont;
        _autopsyBodyText.fontSize = 14;
        _autopsyBodyText.fontStyle = FontStyle.Normal;
        _autopsyBodyText.color = new Color(0.18f, 0.14f, 0.14f, 1f);
        _autopsyBodyText.alignment = TextAnchor.UpperLeft;
        _autopsyBodyText.lineSpacing = 1.15f;

        // Stats Block
        var statsGO = new GameObject("DocumentStats", typeof(RectTransform), typeof(Text));
        statsGO.transform.SetParent(_autopsySheetBox.transform, false);
        var sRT = statsGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.5f, 1f);
        sRT.anchorMax = new Vector2(0.5f, 1f);
        sRT.pivot = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -345f);
        sRT.sizeDelta = new Vector2(680f, 110f);

        _autopsyStatsText = statsGO.GetComponent<Text>();
        _autopsyStatsText.font = typewriterFont;
        _autopsyStatsText.fontSize = 14;
        _autopsyStatsText.fontStyle = FontStyle.Bold;
        _autopsyStatsText.color = new Color(0.20f, 0.10f, 0.10f, 1f);
        _autopsyStatsText.alignment = TextAnchor.UpperLeft;
        _autopsyStatsText.lineSpacing = 1.2f;

        // Red Rubber Stamp [CLEARANCE TERMINATED]
        _stampBox = new GameObject("FailedStamp", typeof(RectTransform), typeof(Image));
        _stampBox.transform.SetParent(_autopsySheetBox.transform, false);
        var stampRT = _stampBox.GetComponent<RectTransform>();
        stampRT.anchorMin = new Vector2(0.70f, 0.48f);
        stampRT.anchorMax = new Vector2(0.70f, 0.48f);
        stampRT.sizeDelta = new Vector2(340f, 75f);
        stampRT.localRotation = Quaternion.Euler(0f, 0f, -14f);

        var stampImg = _stampBox.GetComponent<Image>();
        stampImg.color = new Color(0.75f, 0.08f, 0.08f, 0.18f); // Faint stamp ink background

        var stampTextGO = new GameObject("StampText", typeof(RectTransform), typeof(Text), typeof(Outline));
        stampTextGO.transform.SetParent(_stampBox.transform, false);
        var stRT = stampTextGO.GetComponent<RectTransform>();
        stRT.anchorMin = Vector2.zero;
        stRT.anchorMax = Vector2.one;
        stRT.sizeDelta = Vector2.zero;

        _stampText = stampTextGO.GetComponent<Text>();
        _stampText.font = horrorFont;
        _stampText.fontSize = 28;
        _stampText.fontStyle = FontStyle.Bold;
        _stampText.alignment = TextAnchor.MiddleCenter;
        _stampText.color = new Color(0.85f, 0.08f, 0.08f, 1f);
        _stampText.text = "[ CLEARANCE TERMINATED ]\n<size=16>FAILED TO ESCAPE BCH BUILDING</size>";

        var outline = stampTextGO.GetComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.02f, 0.02f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Buttons Container
        var btnContainer = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnContainer.transform.SetParent(_autopsySheetBox.transform, false);
        var btnRT = btnContainer.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0f);
        btnRT.anchorMax = new Vector2(0.5f, 0f);
        btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 32f);
        btnRT.sizeDelta = new Vector2(620f, 52f);

        var hlg = btnContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        retryButton = CreateStyledButton(btnContainer.transform, "RetryButton", "SUMUBOK MULI  [WAKE UP]", typewriterFont, OnRetryClicked);
        menuButton = CreateStyledButton(btnContainer.transform, "MenuButton", "PANGUNAHING MENU  [EXIT]", typewriterFont, OnMainMenuClicked);

        // Skip prompt pill
        var skipGO = new GameObject("SkipPrompt", typeof(RectTransform), typeof(Text));
        skipGO.transform.SetParent(_canvasGO.transform, false);
        var skipRT = skipGO.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(0.5f, 0f);
        skipRT.anchorMax = new Vector2(0.5f, 0f);
        skipRT.pivot = new Vector2(0.5f, 0f);
        skipRT.anchoredPosition = new Vector2(0f, 16f);
        skipRT.sizeDelta = new Vector2(400f, 25f);

        var skipTxt = skipGO.GetComponent<Text>();
        skipTxt.font = typewriterFont;
        skipTxt.fontSize = 12;
        skipTxt.color = new Color(0.6f, 0.6f, 0.6f, 0.65f);
        skipTxt.alignment = TextAnchor.MiddleCenter;
        skipTxt.text = "[SPACE / ENTER / ESC]  Laktawan ang Cutscene";
    }

    private Button CreateStyledButton(Transform parent, string goName, string text, Font font, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var img = btnGO.GetComponent<Image>();
        img.sprite = buttonSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.18f, 0.15f, 0.15f, 0.95f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.15f, 0.15f, 0.95f);
        colors.highlightedColor = new Color(0.65f, 0.12f, 0.12f, 1.0f);
        colors.pressedColor = new Color(0.95f, 0.20f, 0.20f, 1.0f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (_sfxSource != null && buttonClickClip != null) _sfxSource.PlayOneShot(buttonClickClip, 0.9f);
            onClick?.Invoke();
        });

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(btnGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        var txt = textGO.GetComponent<Text>();
        txt.text = text;
        txt.font = font;
        txt.fontSize = 15;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private Texture2D CreateVignetteTexture()
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float maxDist = center.magnitude;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.SmoothStep(0.25f, 1.0f, dist);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private void SetUIVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(visible);
        }
    }

    // ── Cutscene Execution ─────────────────────────────────────────────────────
    public void HandleGameOver()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;
        IsCutsceneActive = true;

        if (_canvasGO == null)
        {
            SetupAudioSources();
            LoadDefaultAssets();
            BuildCutsceneUI();
        }

        StartCoroutine(CutsceneChoreographyRoutine());
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
            _originalCamFOV = _playerCamera.fieldOfView;
        }
    }

    private IEnumerator CutsceneChoreographyRoutine()
    {
        FindPlayerReferences();

        // 1. Lock controls, close dialogs, despawn active enemies
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isJumpscareCameraOverride = true;
        }

        TheProctorAI.DespawnActiveProctor();
        var dialogueSystem = NPCDialogueSystem.Instance ?? FindAnyObjectByType<NPCDialogueSystem>();
        if (dialogueSystem != null) dialogueSystem.EndDialogue();

        SetUIVisible(true);
        if (_autopsySheetBox != null) _autopsySheetBox.SetActive(false);
        if (_stampBox != null) _stampBox.SetActive(false);

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 1: PANIC ATTACK SURGE (~3.4 seconds)
        // ═════════════════════════════════════════════════════════════════════
        if (_heartbeatSource != null && fastHeartbeatClip != null)
        {
            _heartbeatSource.clip = fastHeartbeatClip;
            _heartbeatSource.volume = 1.0f;
            _heartbeatSource.Play();
        }

        if (_breathingSource != null && gaspingClip != null)
        {
            _breathingSource.clip = gaspingClip;
            _breathingSource.volume = 0.95f;
            _breathingSource.Play();
        }

        float phase1Duration = 3.4f;
        float elapsed = 0f;

        while (elapsed < phase1Duration && !_hasSkipped)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase1Duration;

            // Pulsating vignette darkening
            float pulse = Mathf.Sin(elapsed * 12.0f) * 0.25f + 0.75f;
            if (_vignetteImg != null)
            {
                _vignetteImg.color = new Color(0.15f, 0.01f, 0.01f, Mathf.Clamp01(progress * pulse * 0.92f));
            }

            // Camera erratic shaking and rapid breathing FOV zoom
            if (_playerCamera != null)
            {
                float fovOsc = Mathf.Sin(elapsed * 9.0f) * 6.5f;
                _playerCamera.fieldOfView = _originalCamFOV + fovOsc;

                float pitchShake = (Mathf.PerlinNoise(Time.time * 24f, 0f) - 0.5f) * 5.0f * progress;
                float rollShake = (Mathf.PerlinNoise(0f, Time.time * 24f) - 0.5f) * 7.0f * progress;
                _playerCamera.transform.localRotation = _originalCamLocalRot * Quaternion.Euler(pitchShake, 0f, rollShake);
            }

            // Animate cinematic letterbox bars expanding
            if (_letterboxTop != null && _letterboxBottom != null)
            {
                float barHeight = Mathf.Lerp(0f, 95f, progress);
                _letterboxTop.sizeDelta = new Vector2(0f, barHeight);
                _letterboxBottom.sizeDelta = new Vector2(0f, barHeight);
            }

            yield return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 2: PHYSICAL COLLAPSE (~2.2 seconds)
        // ═════════════════════════════════════════════════════════════════════
        if (!_hasSkipped)
        {
            if (_breathingSource != null && breathingClip != null)
            {
                _breathingSource.clip = breathingClip;
                _breathingSource.volume = 0.75f;
                _breathingSource.Play();
            }

            if (_sfxSource != null && deathSound != null)
            {
                _sfxSource.PlayOneShot(deathSound, 0.85f);
            }

            float collapseDuration = 2.0f;
            elapsed = 0f;
            Vector3 camStartPos = _playerCamera != null ? _playerCamera.transform.localPosition : Vector3.zero;
            Vector3 camFloorPos = new Vector3(camStartPos.x, 0.22f, camStartPos.z);
            Quaternion camStartRot = _playerCamera != null ? _playerCamera.transform.localRotation : Quaternion.identity;
            Quaternion camFloorRot = camStartRot * Quaternion.Euler(18f, 0f, 42f);

            bool playedThud = false;

            while (elapsed < collapseDuration && !_hasSkipped)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / collapseDuration;
                float dropCurve = t * t; // Acceleration downwards

                if (_playerCamera != null)
                {
                    _playerCamera.transform.localPosition = Vector3.Lerp(camStartPos, camFloorPos, dropCurve);
                    _playerCamera.transform.localRotation = Quaternion.Slerp(camStartRot, camFloorRot, dropCurve);
                }

                // Impact thud when hitting the ground
                if (t >= 0.75f && !playedThud)
                {
                    playedThud = true;
                    if (_sfxSource != null && bodyThudClip != null)
                    {
                        _sfxSource.pitch = 0.8f;
                        _sfxSource.PlayOneShot(bodyThudClip, 1.0f);
                    }
                }

                // Fade to black beginning to swallow vision
                if (_fadeToBlackImg != null)
                {
                    _fadeToBlackImg.color = new Color(0.01f, 0.01f, 0.01f, Mathf.Clamp01(t * 0.85f));
                }

                yield return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 3: THE VOID & FINAL THOUGHTS (~3.8 seconds)
        // ═════════════════════════════════════════════════════════════════════
        if (!_hasSkipped)
        {
            if (_fadeToBlackImg != null) _fadeToBlackImg.color = new Color(0.01f, 0.01f, 0.01f, 1f);

            // Transition heartbeat from rapid to slow, fading into silence
            if (_heartbeatSource != null && slowHeartbeatClip != null)
            {
                _heartbeatSource.clip = slowHeartbeatClip;
                _heartbeatSource.volume = 0.6f;
                _heartbeatSource.Play();
            }

            if (_ambienceSource != null && eerieDroneClip != null)
            {
                _ambienceSource.clip = eerieDroneClip;
                _ambienceSource.Play();
            }

            // Tagalog final thought strings
            string[] thoughts = new string[]
            {
                "Hindi na ako makahinga...",
                "Sobrang sikip sa dibdib... Dumidilim na ang paningin ko...",
                "Bumigay na ang aking katawan... Hindi ko na matatapos ang clearance..."
            };

            for (int i = 0; i < thoughts.Length && !_hasSkipped; i++)
            {
                if (_thoughtsText != null)
                {
                    _thoughtsText.text = thoughts[i];
                    // Fade text in
                    float t = 0f;
                    while (t < 0.4f && !_hasSkipped)
                    {
                        t += Time.deltaTime;
                        _thoughtsText.color = new Color(0.90f, 0.85f, 0.85f, t / 0.4f);
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.9f);

                    // Fade text out
                    t = 0f;
                    while (t < 0.35f && !_hasSkipped)
                    {
                        t += Time.deltaTime;
                        _thoughtsText.color = new Color(0.90f, 0.85f, 0.85f, 1f - (t / 0.35f));
                        yield return null;
                    }
                }
            }

            // Fade heartbeat out into flatline
            float hbFade = 1.0f;
            while (hbFade > 0f && !_hasSkipped)
            {
                hbFade -= Time.deltaTime * 0.8f;
                if (_heartbeatSource != null) _heartbeatSource.volume = Mathf.Clamp01(hbFade * 0.5f);
                yield return null;
            }
            if (_heartbeatSource != null) _heartbeatSource.Stop();
        }

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 4: OFFICIAL AUTOPSY & PSYCHOLOGICAL REPORT
        // ═════════════════════════════════════════════════════════════════════
        ShowAutopsyReport();
    }

    private void SkipToReport()
    {
        if (_hasSkipped) return;
        _hasSkipped = true;

        StopAllCoroutines();

        if (_heartbeatSource != null) _heartbeatSource.Stop();
        if (_breathingSource != null) _breathingSource.Stop();

        ShowAutopsyReport();
    }

    private void ShowAutopsyReport()
    {
        SetUIVisible(true);

        if (_fadeToBlackImg != null) _fadeToBlackImg.color = new Color(0.02f, 0.02f, 0.02f, 0.95f);
        if (_thoughtsText != null) _thoughtsText.color = new Color(1f, 1f, 1f, 0f);

        if (_letterboxTop != null) _letterboxTop.sizeDelta = new Vector2(0f, 75f);
        if (_letterboxBottom != null) _letterboxBottom.sizeDelta = new Vector2(0f, 75f);

        // Populate dynamic autopsy and clearance stats
        int sigCount = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
        float timeSurvived = Time.time - _gameStartTime;
        int minutes = Mathf.FloorToInt(timeSurvived / 60f);
        int seconds = Mathf.FloorToInt(timeSurvived % 60f);

        if (_autopsyBodyText != null)
        {
            _autopsyBodyText.text =
                "<b>SUBJECT / ESTUDYANTE:</b> Student ID #1994-0421 (Unidentified Undergraduate)\n" +
                "<b>LOKASYON NG INSIDENTE:</b> BCH Building Hallway & Department Wings\n" +
                "<b>KATAYUAN (STATUS):</b> <color=#991111><b>UNCONSCIOUS // SEVERE PSYCHOSOMATIC SHOCK</b></color>\n" +
                "<b>DIAGNOSIS NG DAHILAN:</b> Acute Panic-Induced Asphyxiation and Hallucinatory Cardiac Shock. " +
                "Subject collapsed unconscious on the hallway floor during the emergency campus blackout. " +
                "Auditory distress indicators show subject suffered debilitating claustrophobia and paranoia.\n" +
                "<b>DISPOSISYON:</b> Clearance application cancelled. Student was unable to secure campus exit.";
        }

        if (_autopsyStatsText != null)
        {
            _autopsyStatsText.text =
                $"<b>MGA NAKUHANG PIRMA:</b>  <color=#991111>{sigCount} / 6 Signatures Acquired</color>\n" +
                $"<b>ORAS NA NAKATAGAL:</b>      {minutes:00} minuto at {seconds:00} segundo\n" +
                $"<b>ANXIETY GAUGE RECORD:</b>  <color=#991111>100% MAXIMUM (CRITICAL COLLAPSE)</color>";
        }

        if (_autopsySheetBox != null)
        {
            _autopsySheetBox.SetActive(true);
        }

        StartCoroutine(StampSlamRoutine());

        // Unlock mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator StampSlamRoutine()
    {
        yield return new WaitForSeconds(0.4f);

        if (_stampBox != null)
        {
            _stampBox.SetActive(true);
            var rt = _stampBox.GetComponent<RectTransform>();
            Vector3 originalScale = rt.localScale;
            rt.localScale = originalScale * 2.2f;

            if (_sfxSource != null && stampClip != null)
            {
                _sfxSource.pitch = 0.9f;
                _sfxSource.PlayOneShot(stampClip, 1.0f);
            }

            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.Lerp(originalScale * 2.2f, originalScale, t / 0.18f);
                yield return null;
            }
            rt.localScale = originalScale;
        }
    }

    private void OnRetryClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
