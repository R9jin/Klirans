using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// First-Person Psychological Collapse & Autopsy Game Over Director:
/// When the player's Anxiety reaches 100%, instead of an abrupt menu pop-up:
/// 1. All enemy chases, active jumpscares, QTEs, and open puzzles/modals are immediately aborted and closed.
/// 2. The player's controls lock as they suffer an acute panic-induced collapse in the hallway.
/// 3. First-person camera sways, stumbles, drops to its knees, and falls sideways onto the cold floor.
/// 4. Heavy gasping, thud impact, and a decelerating heartbeat flatline into an eerie drone.
/// 5. A clean, custom-rendered "INCIDENT AUTOPSY & PSYCHOLOGICAL COLLAPSE REPORT" is slammed onto the screen
///    with dynamic stats (Signatures Collected, Time Survived, Cause of Collapse) and a crisp red rubber stamp.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private static GameOverUI _instance;
    public static GameOverUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameOverUI>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    /// <summary>
    /// True while the Game Over collapse sequence or report screen is active.
    /// Suppresses normal HUD overlays, enemy chases, jumpscares, and interaction prompts.
    /// </summary>
    public static bool IsCutsceneActive { get; private set; }

    [Header("Developer Debug")]
    [Tooltip("Press F9 at any time during Play Mode to test the Game Over Collapse Cutscene.")]
    public KeyCode debugTriggerKey = KeyCode.F9;

    [Header("Legacy / Optional References")]
    public GameObject gameOverPanel;
    public CanvasGroup canvasGroup;
    public Text titleText;
    public Text subtitleText;
    public Button retryButton;
    public Button menuButton;
    public AudioClip deathSound;

    [Header("Audio Clips (Auto-Loaded)")]
    public AudioClip fastHeartbeatClip;
    public AudioClip slowHeartbeatClip;
    public AudioClip gaspingClip;
    public AudioClip breathingClip;
    public AudioClip bodyThudClip;
    public AudioClip stampClip;
    public AudioClip eerieDroneClip;
    public AudioClip buttonClickClip;

    [Header("UI Assets (Auto-Loaded)")]
    public Sprite documentSheetSprite;
    public Sprite buttonSprite;
    private Font horrorFont;
    private Font typewriterFont;

    // ── Runtime Cutscene & UI State ───────────────────────────────────────────
    private bool _hasTriggered = false;
    private bool _hasSkipped = false;
    private float _gameStartTime;

    private GameObject _canvasGO;
    private Image _vignetteImg;
    private Image _fadeToBlackImg;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;
    private Text _thoughtsText;
    private GameObject _skipPromptRoot;

    // Autopsy Report Document Elements
    private GameObject _autopsySheetBox;
    private Text _autopsyTitleText;
    private Text _autopsyBodyText;
    private Text _autopsyStatsText;
    private GameObject _stampBox;
    private Text _stampText;
    private Image _solidBackdropImg;

    // Audio Sources
    private AudioSource _sfxSource;
    private AudioSource _heartbeatSource;
    private AudioSource _breathingSource;
    private AudioSource _droneSource;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        IsCutsceneActive = false;
        _gameStartTime = Time.time;

        LoadDefaultAssets();
        SetupAudioSources();

        // Hide any legacy static panel so our cinematic canvas handles everything cleanly
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private bool _isSubscribed = false;

    private void Start()
    {
        TrySubscribe();
        BuildCutsceneUI();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed) return;
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver -= HandleGameOver;
            AnxietyManager.Instance.OnGameOver += HandleGameOver;
            _isSubscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver -= HandleGameOver;
        }
        if (Instance == this)
        {
            IsCutsceneActive = false;
        }
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            TrySubscribe();
        }

        // Developer shortcut: Press F9 to preview/test the Game Over Cutscene
        if (!_hasTriggered && Input.GetKeyDown(debugTriggerKey))
        {
            Debug.Log("[GameOverUI] F9 pressed — Triggering Game Over Collapse Cutscene.");
            HandleGameOver();
        }

        // Allow skipping the physical collapse animation to jump straight to the Autopsy Report
        if (_hasTriggered && !_hasSkipped && _autopsySheetBox != null && !_autopsySheetBox.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SkipToReport();
            }
        }

        // When Game Over report is visible, constantly enforce mouse cursor unlock
        if (_hasTriggered && _autopsySheetBox != null && _autopsySheetBox.activeSelf)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }

    private void SetupAudioSources()
    {
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.spatialBlend = 0f;
        _sfxSource.playOnAwake = false;

        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.loop = true;
        _heartbeatSource.playOnAwake = false;

        _breathingSource = gameObject.AddComponent<AudioSource>();
        _breathingSource.spatialBlend = 0f;
        _breathingSource.loop = false;
        _breathingSource.playOnAwake = false;

        _droneSource = gameObject.AddComponent<AudioSource>();
        _droneSource.spatialBlend = 0f;
        _droneSource.loop = true;
        _droneSource.volume = 0f;
        _droneSource.playOnAwake = false;
    }

    private void LoadDefaultAssets()
    {
#if UNITY_EDITOR
        if (fastHeartbeatClip == null)
            fastHeartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heartbeat.mp3");
        if (slowHeartbeatClip == null)
            slowHeartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/slow heartbeat.mp3");
        if (gaspingClip == null)
            gaspingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/gasping for air.mp3");
        if (breathingClip == null)
            breathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/breathing.mp3");
        if (bodyThudClip == null)
            bodyThudClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/closing locker & cabinet.mp3");
        if (stampClip == null)
            stampClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/stamp.mp3");
        if (eerieDroneClip == null)
            eerieDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/horror background sound 2.mp3");
        if (buttonClickClip == null)
            buttonClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/clicking.mp3");

        if (buttonSprite == null)
            buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/KeypadButton_Normal.png");

        horrorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/HelpMe.ttf");
        typewriterFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/CourierPrime-Bold.ttf");
#endif
        if (fastHeartbeatClip == null)
            fastHeartbeatClip = Resources.Load<AudioClip>("Sounds/fast heartbeat");
        if (slowHeartbeatClip == null)
            slowHeartbeatClip = Resources.Load<AudioClip>("Sounds/slow heartbeat");
        if (gaspingClip == null)
            gaspingClip = Resources.Load<AudioClip>("Sounds/gasping for air");
        if (breathingClip == null)
            breathingClip = Resources.Load<AudioClip>("Sounds/breathing");
        if (bodyThudClip == null)
            bodyThudClip = Resources.Load<AudioClip>("Sounds/closing locker & cabinet");
        if (stampClip == null)
            stampClip = Resources.Load<AudioClip>("Sounds/stamp");
        if (eerieDroneClip == null)
            eerieDroneClip = Resources.Load<AudioClip>("Sounds/horror background sound 2");
        if (buttonClickClip == null)
            buttonClickClip = Resources.Load<AudioClip>("Sounds/clicking");

        if (buttonSprite == null)
            buttonSprite = Resources.Load<Sprite>("PlayerAssets/KeypadButton_Normal");

        if (horrorFont == null)
            horrorFont = Resources.Load<Font>("Fonts/HelpMe");
        if (typewriterFont == null)
            typewriterFont = Resources.Load<Font>("Fonts/CourierPrime-Bold");

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (horrorFont == null) horrorFont = fallback;
        if (typewriterFont == null) typewriterFont = fallback;

        // Always generate a dedicated, clean procedural parchment report sprite
        // so baked-in text from GuidancePaperSheet.png NEVER overlaps the Game Over report!
        documentSheetSprite = CreateCleanAutopsyPaperSprite();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PROCEDURAL UI CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    private void BuildCutsceneUI()
    {
        if (_canvasGO != null) Destroy(_canvasGO);

        _canvasGO = new GameObject("GameOverCutsceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasGO.transform.SetParent(transform, false);

        if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(esGO);
        }

        var canvas = _canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000; // Above all HUDs, puzzles, jumpscares, and interaction prompts

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        // 1. Blood / Asphyxiation Vignette Overlay
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

        // Solid dark full-screen backing image behind autopsy sheet so 3D world & underlying UI are 100% blocked
        var solidBackGO = new GameObject("SolidBackdrop", typeof(RectTransform), typeof(Image));
        solidBackGO.transform.SetParent(_canvasGO.transform, false);
        var sbRT = solidBackGO.GetComponent<RectTransform>();
        sbRT.anchorMin = Vector2.zero;
        sbRT.anchorMax = Vector2.one;
        sbRT.sizeDelta = Vector2.zero;
        _solidBackdropImg = solidBackGO.GetComponent<Image>();
        _solidBackdropImg.color = new Color(0.03f, 0.02f, 0.02f, 1.0f);
        _solidBackdropImg.raycastTarget = true;
        solidBackGO.SetActive(false);

        // 5. Official Autopsy / Psychological Report Paper Sheet (starts INACTIVE)
        _autopsySheetBox = new GameObject("AutopsyPaperSheet", typeof(RectTransform), typeof(Image));
        _autopsySheetBox.transform.SetParent(_canvasGO.transform, false);
        var sheetRT = _autopsySheetBox.GetComponent<RectTransform>();
        sheetRT.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRT.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRT.sizeDelta = new Vector2(760f, 600f);
        sheetRT.localRotation = Quaternion.Euler(0f, 0f, -0.5f);

        var sheetImg = _autopsySheetBox.GetComponent<Image>();
        sheetImg.sprite = documentSheetSprite != null ? documentSheetSprite : CreateCleanAutopsyPaperSprite();
        sheetImg.color = Color.white;

        // Document Header
        var titleGO = new GameObject("DocumentTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_autopsySheetBox.transform, false);
        var tRT = titleGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 1f);
        tRT.anchorMax = new Vector2(0.5f, 1f);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0f, -26f);
        tRT.sizeDelta = new Vector2(680f, 96f);

        _autopsyTitleText = titleGO.GetComponent<Text>();
        _autopsyTitleText.font = typewriterFont;
        _autopsyTitleText.fontSize = 15;
        _autopsyTitleText.fontStyle = FontStyle.Bold;
        _autopsyTitleText.color = new Color(0.14f, 0.10f, 0.09f, 1f);
        _autopsyTitleText.alignment = TextAnchor.UpperCenter;
        _autopsyTitleText.lineSpacing = 1.15f;
        _autopsyTitleText.raycastTarget = false;
        _autopsyTitleText.text =
            "REPUBLIKA NG PILIPINAS  •  PAMANTASAN NG CABUYAO\n" +
            "OFFICE OF STUDENT WELFARE & CAMPUS SECURITY\n" +
            "<size=18><color=#8B1111>INCIDENT AUTOPSY & PSYCHOLOGICAL COLLAPSE REPORT</color></size>\n" +
            "<size=13><color=#4A3C35>CASE FILE: CASE-PNC-BCH-1994-0824  •  CLASSIFIED</color></size>";

        // Section I Header Label
        var sec1GO = new GameObject("Section1Header", typeof(RectTransform), typeof(Text));
        sec1GO.transform.SetParent(_autopsySheetBox.transform, false);
        var sec1RT = sec1GO.GetComponent<RectTransform>();
        sec1RT.anchorMin = new Vector2(0.5f, 1f);
        sec1RT.anchorMax = new Vector2(0.5f, 1f);
        sec1RT.pivot = new Vector2(0.5f, 1f);
        sec1RT.anchoredPosition = new Vector2(0f, -134f);
        sec1RT.sizeDelta = new Vector2(676f, 24f);

        var sec1Text = sec1GO.GetComponent<Text>();
        sec1Text.font = typewriterFont;
        sec1Text.fontSize = 13;
        sec1Text.fontStyle = FontStyle.Bold;
        sec1Text.color = new Color(0.22f, 0.16f, 0.14f, 1f);
        sec1Text.alignment = TextAnchor.MiddleLeft;
        sec1Text.text = "  SECTION I: SUBJECT EVALUATION & CLINICAL FINDINGS";
        sec1Text.raycastTarget = false;

        // Document Body
        var bodyGO = new GameObject("DocumentBody", typeof(RectTransform), typeof(Text));
        bodyGO.transform.SetParent(_autopsySheetBox.transform, false);
        var bRT = bodyGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.5f, 1f);
        bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -166f);
        bRT.sizeDelta = new Vector2(660f, 195f);

        _autopsyBodyText = bodyGO.GetComponent<Text>();
        _autopsyBodyText.font = typewriterFont;
        _autopsyBodyText.fontSize = 14;
        _autopsyBodyText.fontStyle = FontStyle.Normal;
        _autopsyBodyText.color = new Color(0.16f, 0.12f, 0.12f, 1f);
        _autopsyBodyText.alignment = TextAnchor.UpperLeft;
        _autopsyBodyText.lineSpacing = 1.18f;
        _autopsyBodyText.raycastTarget = false;

        // Section II Header Label
        var sec2GO = new GameObject("Section2Header", typeof(RectTransform), typeof(Text));
        sec2GO.transform.SetParent(_autopsySheetBox.transform, false);
        var sec2RT = sec2GO.GetComponent<RectTransform>();
        sec2RT.anchorMin = new Vector2(0.5f, 1f);
        sec2RT.anchorMax = new Vector2(0.5f, 1f);
        sec2RT.pivot = new Vector2(0.5f, 1f);
        sec2RT.anchoredPosition = new Vector2(0f, -372f);
        sec2RT.sizeDelta = new Vector2(676f, 24f);

        var sec2Text = sec2GO.GetComponent<Text>();
        sec2Text.font = typewriterFont;
        sec2Text.fontSize = 13;
        sec2Text.fontStyle = FontStyle.Bold;
        sec2Text.color = new Color(0.22f, 0.16f, 0.14f, 1f);
        sec2Text.alignment = TextAnchor.MiddleLeft;
        sec2Text.text = "  SECTION II: CLEARANCE PROGRESS & SURVIVAL RECORD";
        sec2Text.raycastTarget = false;

        // Stats Block (Left aligned inside Section II box so the stamp can sit cleanly on the right)
        var statsGO = new GameObject("DocumentStats", typeof(RectTransform), typeof(Text));
        statsGO.transform.SetParent(_autopsySheetBox.transform, false);
        var sRT = statsGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.5f, 1f);
        sRT.anchorMax = new Vector2(0.5f, 1f);
        sRT.pivot = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -404f);
        sRT.sizeDelta = new Vector2(660f, 92f);

        _autopsyStatsText = statsGO.GetComponent<Text>();
        _autopsyStatsText.font = typewriterFont;
        _autopsyStatsText.fontSize = 14;
        _autopsyStatsText.fontStyle = FontStyle.Bold;
        _autopsyStatsText.color = new Color(0.18f, 0.11f, 0.11f, 1f);
        _autopsyStatsText.alignment = TextAnchor.UpperLeft;
        _autopsyStatsText.lineSpacing = 1.28f;
        _autopsyStatsText.raycastTarget = false;

        // Red Rubber Stamp [CLEARANCE TERMINATED] — positioned in lower-right of the report, free of text overlap
        _stampBox = new GameObject("FailedStamp", typeof(RectTransform), typeof(Image));
        _stampBox.transform.SetParent(_autopsySheetBox.transform, false);
        var stampRT = _stampBox.GetComponent<RectTransform>();
        stampRT.anchorMin = new Vector2(0.75f, 0.25f);
        stampRT.anchorMax = new Vector2(0.75f, 0.25f);
        stampRT.pivot = new Vector2(0.5f, 0.5f);
        stampRT.anchoredPosition = Vector2.zero;
        stampRT.sizeDelta = new Vector2(280f, 72f);
        stampRT.localRotation = Quaternion.Euler(0f, 0f, -11f);

        var stampImg = _stampBox.GetComponent<Image>();
        stampImg.sprite = CreateRubberStampBorderSprite();
        stampImg.color = Color.white;
        stampImg.raycastTarget = false;

        var stampTextGO = new GameObject("StampText", typeof(RectTransform), typeof(Text));
        stampTextGO.transform.SetParent(_stampBox.transform, false);
        var stRT = stampTextGO.GetComponent<RectTransform>();
        stRT.anchorMin = Vector2.zero;
        stRT.anchorMax = Vector2.one;
        stRT.sizeDelta = Vector2.zero;

        _stampText = stampTextGO.GetComponent<Text>();
        _stampText.font = typewriterFont;
        _stampText.fontSize = 17;
        _stampText.fontStyle = FontStyle.Bold;
        _stampText.alignment = TextAnchor.MiddleCenter;
        _stampText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _stampText.verticalOverflow = VerticalWrapMode.Overflow;
        _stampText.lineSpacing = 1.05f;
        _stampText.color = new Color(0.76f, 0.08f, 0.08f, 0.94f);
        _stampText.raycastTarget = false;
        _stampText.text = "CLEARANCE TERMINATED\n<size=12>FAILED TO ESCAPE BCH</size>";

        // Buttons Container
        var btnContainer = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnContainer.transform.SetParent(_autopsySheetBox.transform, false);
        var btnRT = btnContainer.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0f);
        btnRT.anchorMax = new Vector2(0.5f, 0f);
        btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 26f);
        btnRT.sizeDelta = new Vector2(640f, 48f);

        var hlg = btnContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 32f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        retryButton = CreateStyledButton(btnContainer.transform, "RetryButton", "SUMUBOK MULI  [WAKE UP]", typewriterFont, OnRetryClicked);
        menuButton = CreateStyledButton(btnContainer.transform, "MenuButton", "PANGUNAHING MENU  [EXIT]", typewriterFont, OnMainMenuClicked);

        // Skip prompt pill (only visible during fainting sequence)
        _skipPromptRoot = new GameObject("SkipPrompt", typeof(RectTransform), typeof(Text));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var skipRT = _skipPromptRoot.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(0.5f, 0f);
        skipRT.anchorMax = new Vector2(0.5f, 0f);
        skipRT.pivot = new Vector2(0.5f, 0f);
        skipRT.anchoredPosition = new Vector2(0f, 16f);
        skipRT.sizeDelta = new Vector2(400f, 25f);

        var skipText = _skipPromptRoot.GetComponent<Text>();
        skipText.font = typewriterFont;
        skipText.fontSize = 13;
        skipText.color = new Color(0.65f, 0.65f, 0.65f, 0.5f);
        skipText.alignment = TextAnchor.MiddleCenter;
        skipText.text = "Press [SPACE] to skip to Incident Report";

        _autopsySheetBox.SetActive(false);
        _stampBox.SetActive(false);
        _canvasGO.SetActive(false);
    }

    private Button CreateStyledButton(Transform parent, string name, string label, Font font, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var img = btnGO.GetComponent<Image>();
        if (buttonSprite != null) img.sprite = buttonSprite;
        img.color = new Color(0.18f, 0.14f, 0.12f, 0.96f);
        img.raycastTarget = true;

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.14f, 0.12f, 0.96f);
        colors.highlightedColor = new Color(0.55f, 0.10f, 0.10f, 1f);
        colors.pressedColor = new Color(0.35f, 0.05f, 0.05f, 1f);
        btn.colors = colors;

        btn.onClick.AddListener(() =>
        {
            if (_sfxSource != null && buttonClickClip != null)
                _sfxSource.PlayOneShot(buttonClickClip, 0.85f);
            onClick?.Invoke();
        });

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(btnGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        var txt = txtGO.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 15;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(0.95f, 0.90f, 0.85f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        txt.text = label;

        return btn;
    }

    /// <summary>
    /// Creates a clean, high-resolution aged official paper sprite with subtle section boxes and borders,
    /// containing ZERO baked-in text so there is never any text overlap with the dynamic report fields.
    /// </summary>
    private Sprite CreateCleanAutopsyPaperSprite()
    {
        int w = 760;
        int h = 600;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color basePaper = new Color(0.925f, 0.898f, 0.835f, 1f);
        Color borderDark = new Color(0.28f, 0.23f, 0.18f, 0.92f);
        Color sectionHeaderBg = new Color(0.855f, 0.820f, 0.750f, 1f);
        Color sectionBoxBg = new Color(0.895f, 0.868f, 0.805f, 1f);
        Color boxBorder = new Color(0.55f, 0.49f, 0.42f, 0.75f);

        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Subtle aged vignette toward edges + fine paper grain
                float nx = (x / (float)w) * 2f - 1f;
                float ny = (y / (float)h) * 2f - 1f;
                float edgeDist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                float edgeDarken = Mathf.Clamp01((edgeDist - 0.75f) * 0.22f);
                float grain = (Mathf.PerlinNoise(x * 0.18f, y * 0.18f) - 0.5f) * 0.018f;

                Color c = new Color(
                    Mathf.Clamp01(basePaper.r - edgeDarken + grain),
                    Mathf.Clamp01(basePaper.g - edgeDarken + grain),
                    Mathf.Clamp01(basePaper.b - edgeDarken + grain),
                    1f
                );

                // Outer double frame border (14px and 19px from edges)
                bool outerBorder = (x >= 12 && x <= 15 && y >= 12 && y <= h - 13) ||
                                   (x >= w - 16 && x <= w - 13 && y >= 12 && y <= h - 13) ||
                                   (y >= 12 && y <= 15 && x >= 12 && x <= w - 13) ||
                                   (y >= h - 16 && y <= h - 13 && x >= 12 && x <= w - 13);

                bool innerBorder = (x == 19 && y >= 19 && y <= h - 20) ||
                                   (x == w - 20 && y >= 19 && y <= h - 20) ||
                                   (y == 19 && x >= 19 && x <= w - 20) ||
                                   (y == h - 20 && x >= 19 && x <= w - 20);

                if (outerBorder || innerBorder)
                {
                    pixels[y * w + x] = borderDark;
                    continue;
                }

                // Header divider double line (at y = h - 124 and y = h - 127)
                if (x >= 34 && x <= w - 35 && (y == h - 124 || y == h - 125 || y == h - 128))
                {
                    pixels[y * w + x] = borderDark;
                    continue;
                }

                // Section I Header Bar (top offset 134..158 -> y in [h-158 .. h-134])
                int s1Top = h - 134;
                int s1Bot = h - 158;
                if (x >= 40 && x <= w - 41 && y >= s1Bot && y <= s1Top)
                {
                    bool barBorder = (x == 40 || x == w - 41 || y == s1Bot || y == s1Top);
                    pixels[y * w + x] = barBorder ? boxBorder : sectionHeaderBg;
                    continue;
                }

                // Section I Body Box (top offset 162..362 -> y in [h-362 .. h-162])
                int b1Top = h - 162;
                int b1Bot = h - 362;
                if (x >= 40 && x <= w - 41 && y >= b1Bot && y <= b1Top)
                {
                    bool bBorder = (x == 40 || x == w - 41 || y == b1Bot || y == b1Top);
                    pixels[y * w + x] = bBorder ? boxBorder : sectionBoxBg;
                    continue;
                }

                // Section II Header Bar (top offset 372..396 -> y in [h-396 .. h-372])
                int s2Top = h - 372;
                int s2Bot = h - 396;
                if (x >= 40 && x <= w - 41 && y >= s2Bot && y <= s2Top)
                {
                    bool barBorder = (x == 40 || x == w - 41 || y == s2Bot || y == s2Top);
                    pixels[y * w + x] = barBorder ? boxBorder : sectionHeaderBg;
                    continue;
                }

                // Section II Stats Box (top offset 400..502 -> y in [h-502 .. h-400])
                int b2Top = h - 400;
                int b2Bot = h - 502;
                if (x >= 40 && x <= w - 41 && y >= b2Bot && y <= b2Top)
                {
                    bool bBorder = (x == 40 || x == w - 41 || y == b2Bot || y == b2Top);
                    pixels[y * w + x] = bBorder ? boxBorder : sectionBoxBg;
                    continue;
                }

                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Creates a crisp red rubber-stamp border sprite with a translucent ink wash inside.
    /// </summary>
    private Sprite CreateRubberStampBorderSprite()
    {
        int w = 280;
        int h = 72;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color inkBorder = new Color(0.76f, 0.08f, 0.08f, 0.88f);
        Color inkFill = new Color(0.76f, 0.08f, 0.08f, 0.10f);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool outer = (x >= 2 && x <= 5 && y >= 2 && y <= h - 3) ||
                             (x >= w - 6 && x <= w - 3 && y >= 2 && y <= h - 3) ||
                             (y >= 2 && y <= 5 && x >= 2 && x <= w - 3) ||
                             (y >= h - 6 && y <= h - 3 && x >= 2 && x <= w - 3);

                bool inner = (x == 9 && y >= 9 && y <= h - 10) ||
                             (x == w - 10 && y >= 9 && y <= h - 10) ||
                             (y == 9 && x >= 9 && x <= w - 10) ||
                             (y == h - 10 && x >= 9 && x <= w - 10);

                if (outer || inner)
                {
                    // Slight distressed ink texture
                    float noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    float alpha = noise > 0.18f ? inkBorder.a : inkBorder.a * 0.45f;
                    pixels[y * w + x] = new Color(inkBorder.r, inkBorder.g, inkBorder.b, alpha);
                }
                else if (x > 9 && x < w - 10 && y > 9 && y < h - 10)
                {
                    pixels[y * w + x] = inkFill;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Texture2D CreateVignetteTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(Mathf.Pow(dist, 1.8f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CUTSCENE CHOREOGRAPHY
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Immediately stops all enemies, active jumpscares, QTEs, puzzles, modals, and interaction prompts.
    /// </summary>
    private void SilenceAllThreatsAndPrompts()
    {
        // 1. Force-close any open puzzle, minigame, inspection modal, inventory, or dialogue
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        // Extra defensive sweep: unconditionally hide any remaining puzzle panels or backdrops
        var allGuidance = FindObjectsByType<GuidanceWordPuzzle>(FindObjectsInactive.Include);
        for (int i = 0; i < allGuidance.Length; i++)
        {
            if (allGuidance[i] != null)
            {
                allGuidance[i].ClosePuzzle();
                if (allGuidance[i].puzzlePanel != null) allGuidance[i].puzzlePanel.SetActive(false);
                if (allGuidance[i].backdropOverlay != null) allGuidance[i].backdropOverlay.SetActive(false);
            }
        }
        var allCashier = FindObjectsByType<CashierBalancePuzzle>(FindObjectsInactive.Include);
        for (int i = 0; i < allCashier.Length; i++)
        {
            if (allCashier[i] != null)
            {
                allCashier[i].ClosePuzzle();
                if (allCashier[i].puzzlePanel != null) allCashier[i].puzzlePanel.SetActive(false);
                if (allCashier[i].backdropOverlay != null) allCashier[i].backdropOverlay.SetActive(false);
            }
        }
        var allRegistrar = FindObjectsByType<RegistrarDocumentSortPuzzle>(FindObjectsInactive.Include);
        for (int i = 0; i < allRegistrar.Length; i++)
        {
            if (allRegistrar[i] != null)
            {
                allRegistrar[i].ClosePuzzle();
                if (allRegistrar[i].puzzlePanel != null) allRegistrar[i].puzzlePanel.SetActive(false);
                if (allRegistrar[i].backdropOverlay != null) allRegistrar[i].backdropOverlay.SetActive(false);
            }
        }

        // 2. Abort any active Proctor struggle QTE or wandering NPC jumpscare
        ProctorJumpscareQTE.AbortActiveQTE();
        if (NPCJumpscareManager.Instance != null)
        {
            NPCJumpscareManager.Instance.AbortActiveJumpscare();
        }

        // 3. Despawn all active stalker/blackout entities immediately
        TheProctorAI.DespawnActiveProctor();
        WeepingStudentAI.DespawnActiveWeepingStudent();
        SecurityGuardStalkerAI.DespawnActiveGuardStalker();
        if (TheProctorManager.Instance != null)
        {
            TheProctorManager.Instance.CleanupProctor();
        }

        // 4. Freeze and disable all wandering student ProctorAI so they NEVER walk, chase or scare during/after Game Over
        var wanderingNPCs = FindObjectsByType<ProctorAI>(FindObjectsInactive.Include);
        for (int i = 0; i < wanderingNPCs.Length; i++)
        {
            if (wanderingNPCs[i] != null)
            {
                wanderingNPCs[i].StopChase();
                if (wanderingNPCs[i].TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent) && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                wanderingNPCs[i].enabled = false;
            }
        }

        // 5. Hide any "[E] Pick Up / Talk" prompt text on screen
        var playerInteract = FindFirstObjectByType<PlayerInteract>();
        if (playerInteract != null)
        {
            if (playerInteract.promptText != null)
            {
                playerInteract.promptText.gameObject.SetActive(false);
            }
            playerInteract.enabled = false;
        }
    }

    public void HandleGameOver()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;
        IsCutsceneActive = true;

        Time.timeScale = 1f; // Keep timeScale at 1 so physical collapse coroutine runs smoothly!

        // Immediately silence all enemy jumpscares, chases, QTEs, puzzles, and interaction prompts
        SilenceAllThreatsAndPrompts();

        if (_canvasGO == null) BuildCutsceneUI();
        _canvasGO.SetActive(true);

        StartCoroutine(CutsceneChoreographyRoutine());
    }

    private IEnumerator CutsceneChoreographyRoutine()
    {
        SilenceAllThreatsAndPrompts();

        // 1. Lock Player Movement & Camera Controls
        var playerMove = FindFirstObjectByType<PlayerMovement>();
        Camera mainCam = Camera.main;
        if (playerMove != null)
        {
            playerMove.isJumpscareCameraOverride = true;
            playerMove.SetControlsEnabled(false);
            if (playerMove.playerCamera != null) mainCam = playerMove.playerCamera;
        }

        // Turn off flashlight upon collapse or make it flicker out
        var flashlight = FlashlightController.Instance;

        // 2. Start Panic Audio (Racing Heartbeat + Gasping for Air)
        if (_heartbeatSource != null && fastHeartbeatClip != null)
        {
            _heartbeatSource.clip = fastHeartbeatClip;
            _heartbeatSource.pitch = 1.35f;
            _heartbeatSource.volume = 0.95f;
            _heartbeatSource.Play();
        }

        if (_breathingSource != null && gaspingClip != null)
        {
            _breathingSource.clip = gaspingClip;
            _breathingSource.volume = 0.90f;
            _breathingSource.Play();
        }

        if (_droneSource != null && eerieDroneClip != null)
        {
            _droneSource.clip = eerieDroneClip;
            _droneSource.volume = 0.15f;
            _droneSource.Play();
        }

        // Record initial camera transform
        Transform camT = mainCam != null ? mainCam.transform : null;
        Vector3 startLocalPos = camT != null ? camT.localPosition : new Vector3(0f, 1.52f, 0f);
        Quaternion startLocalRot = camT != null ? camT.localRotation : Quaternion.identity;
        float startFOV = mainCam != null ? mainCam.fieldOfView : 60f;

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 1: VERTIGO & CHEST-CLUTCHING HYPERVENTILATION (0.0s -> 1.8s)
        // Camera sways violently, FOV constricts/expands, crimson vignette pulses
        // ═════════════════════════════════════════════════════════════════════
        float p1Dur = 1.8f;
        float elapsed = 0f;

        while (elapsed < p1Dur && !_hasSkipped)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / p1Dur;

            // Letterbox bars slowly creep in
            float barH = Mathf.Lerp(0f, 55f, t);
            if (_letterboxTop != null) _letterboxTop.sizeDelta = new Vector2(0f, barH);
            if (_letterboxBottom != null) _letterboxBottom.sizeDelta = new Vector2(0f, barH);

            // Crimson tunnel-vision vignette
            if (_vignetteImg != null)
            {
                float pulse = 0.55f + Mathf.Sin(elapsed * 14f) * 0.20f;
                _vignetteImg.color = new Color(0.22f, 0.01f, 0.01f, Mathf.Clamp01(t * pulse));
            }

            if (camT != null)
            {
                // Staggering / dizzy head roll and pitch
                float swayX = Mathf.Sin(elapsed * 6.5f) * 8f * t;
                float swayZ = Mathf.Cos(elapsed * 5.0f) * 14f * t;
                camT.localRotation = startLocalRot * Quaternion.Euler(swayX + (t * 12f), 0f, swayZ);

                // Stumbling camera bob
                float dropY = Mathf.Lerp(0f, -0.25f, t * t);
                camT.localPosition = startLocalPos + new Vector3(Mathf.Sin(elapsed * 9f) * 0.06f, dropY, 0f);
            }

            if (mainCam != null)
            {
                mainCam.fieldOfView = startFOV + Mathf.Sin(elapsed * 12f) * 6f * t;
            }

            if (_droneSource != null)
            {
                _droneSource.volume = Mathf.Lerp(0.15f, 0.45f, t);
            }

            yield return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 2: DROPPING TO KNEES & HITTING THE FLOOR (1.8s -> 3.4s)
        // First-person camera drops to knee height, then slams sideways onto the floor tiles
        // ═════════════════════════════════════════════════════════════════════
        if (!_hasSkipped)
        {
            float p2Dur = 1.6f;
            elapsed = 0f;

            Vector3 p2StartPos = camT != null ? camT.localPosition : startLocalPos;
            Quaternion p2StartRot = camT != null ? camT.localRotation : startLocalRot;

            // Target floor position: head resting sideways just above the floor (y = 0.18m)
            Vector3 floorPos = new Vector3(startLocalPos.x + 0.15f, 0.18f, startLocalPos.z + 0.10f);
            // Target floor rotation: tilted 78 degrees sideways on the floor, looking slightly down the hallway
            Quaternion floorRot = Quaternion.Euler(14f, startLocalRot.eulerAngles.y + 18f, 76f);

            bool playedKneeThud = false;
            bool playedBodyCollapseThud = false;

            while (elapsed < p2Dur && !_hasSkipped)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / p2Dur);

                // Two-stage fall curve (knees at t=0.45, full floor impact at t=0.85)
                float curve = t * t * (3f - 2f * t);

                if (t >= 0.42f && !playedKneeThud)
                {
                    playedKneeThud = true;
                    if (_sfxSource != null && bodyThudClip != null)
                    {
                        _sfxSource.pitch = 0.85f;
                        _sfxSource.PlayOneShot(bodyThudClip, 0.65f);
                    }
                }

                if (t >= 0.82f && !playedBodyCollapseThud)
                {
                    playedBodyCollapseThud = true;
                    if (_sfxSource != null && bodyThudClip != null)
                    {
                        _sfxSource.pitch = 0.62f;
                        _sfxSource.PlayOneShot(bodyThudClip, 1.0f);
                    }

                    // Switch heartbeat from racing to dying slow thumps
                    if (_heartbeatSource != null && slowHeartbeatClip != null)
                    {
                        _heartbeatSource.clip = slowHeartbeatClip;
                        _heartbeatSource.pitch = 0.85f;
                        _heartbeatSource.volume = 0.80f;
                        _heartbeatSource.Play();
                    }

                    // Flicker flashlight off as it hits the floor
                    if (flashlight != null && flashlight.IsLightOn)
                    {
                        flashlight.ToggleLight();
                    }
                }

                if (camT != null)
                {
                    camT.localPosition = Vector3.Lerp(p2StartPos, floorPos, curve);
                    camT.localRotation = Quaternion.Slerp(p2StartRot, floorRot, curve);

                    // Impact jolt at t ~ 0.85
                    if (t > 0.80f && t < 0.95f)
                    {
                        camT.localPosition += new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(-0.03f, 0.03f), 0f);
                    }
                }

                // Heavy eyelid blink effect
                if (_fadeToBlackImg != null)
                {
                    float blink = Mathf.Sin(t * Mathf.PI * 2.5f) * 0.5f + 0.35f;
                    _fadeToBlackImg.color = new Color(0.02f, 0.0f, 0.0f, Mathf.Clamp01(blink * t));
                }

                if (_letterboxTop != null) _letterboxTop.sizeDelta = new Vector2(0f, Mathf.Lerp(55f, 110f, t));
                if (_letterboxBottom != null) _letterboxBottom.sizeDelta = new Vector2(0f, Mathf.Lerp(55f, 110f, t));

                yield return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // PHASE 3: DYING CONSCIOUSNESS & SUFFOCATING THOUGHTS (3.4s -> 6.8s)
        // Eyelids close to pitch black while final haunting thoughts flash
        // ═════════════════════════════════════════════════════════════════════
        if (!_hasSkipped)
        {
            if (_breathingSource != null && breathingClip != null)
            {
                _breathingSource.clip = breathingClip;
                _breathingSource.pitch = 0.72f;
                _breathingSource.volume = 0.65f;
                _breathingSource.Play();
            }

            // Fade to complete darkness over 1.2s
            float fadeDur = 1.2f;
            elapsed = 0f;
            float startBlackA = _fadeToBlackImg != null ? _fadeToBlackImg.color.a : 0.5f;

            while (elapsed < fadeDur && !_hasSkipped)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                if (_fadeToBlackImg != null)
                {
                    _fadeToBlackImg.color = new Color(0.01f, 0.01f, 0.01f, Mathf.Lerp(startBlackA, 0.96f, t));
                }
                yield return null;
            }

            // Flash haunting final thoughts in typewriter text
            string[] thoughts = new string[]
            {
                "\"Hindi ako makahinga... ang sikip ng dibdib ko...\"",
                "\"Ang clearance ko... kailangan ko pang makalabas ng BCH...\"",
                "\"Tulong... may nakatingin sa akin sa dilim...\""
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
        SilenceAllThreatsAndPrompts();

        if (_canvasGO != null) _canvasGO.SetActive(true);
        if (_solidBackdropImg != null) _solidBackdropImg.gameObject.SetActive(true);

        if (_fadeToBlackImg != null)
        {
            _fadeToBlackImg.color = new Color(0.02f, 0.02f, 0.02f, 1.0f);
            _fadeToBlackImg.raycastTarget = true;
        }
        if (_thoughtsText != null) _thoughtsText.color = new Color(1f, 1f, 1f, 0f);
        if (_skipPromptRoot != null) _skipPromptRoot.SetActive(false);

        if (_letterboxTop != null) _letterboxTop.sizeDelta = new Vector2(0f, 56f);
        if (_letterboxBottom != null) _letterboxBottom.sizeDelta = new Vector2(0f, 56f);

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

        // Completely freeze the game world now that the collapse cutscene is finished and report is visible!
        Time.timeScale = 0f;

        StartCoroutine(StampSlamRoutine());

        // Unlock mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator StampSlamRoutine()
    {
        yield return new WaitForSecondsRealtime(0.35f);

        if (_stampBox != null)
        {
            _stampBox.SetActive(true);
            var rt = _stampBox.GetComponent<RectTransform>();
            Vector3 originalScale = Vector3.one;
            rt.localScale = originalScale * 2.2f;

            if (_sfxSource != null && stampClip != null)
            {
                _sfxSource.pitch = 0.9f;
                _sfxSource.PlayOneShot(stampClip, 1.0f);
            }

            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.Lerp(originalScale * 2.2f, originalScale, t / 0.18f);
                yield return null;
            }
            rt.localScale = originalScale;
        }
    }

    private void OnRetryClicked()
    {
        Time.timeScale = 1f;
        IsCutsceneActive = false;
        _hasTriggered = false;
        if (_canvasGO != null) Destroy(_canvasGO);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        IsCutsceneActive = false;
        _hasTriggered = false;
        if (_canvasGO != null) Destroy(_canvasGO);
        SceneManager.LoadScene("MainMenu");
    }
}
