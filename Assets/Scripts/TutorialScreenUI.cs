using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Paged "How to Play / Tutorial" Screen ("HELP // GABAY SA PAGLALARO").
/// Modeled after conventional paged visual tutorial screens using KLIRANS's authentic paper UI assets.
/// Pages:
///   Page 1: Basic Controls & Movement (WASD, Sprint, Crouch, Interact, Bag, Pause/Help)
///   Page 2: Flashlight, Hotbar & How to Drop Items ([LMB/F] Toggle, [1-6] Equip, [G/Q] Drop, [V] Inspect)
///   Page 3: How to Win (4 Torn Fragments -> Print Form -> 6 Signatures in Order -> Registrar -> Main Gate Escape)
///   Page 4: Surviving Blackouts & Entities (Blackouts, Proctor & Weeping Student, Lockers, Circuit Breaker, Sandwiches)
/// Automatically opens after the Opening Cutscene concludes, and can be opened anytime via [H]/[F1], Pause Menu, or Main Menu.
/// </summary>
public class TutorialScreenUI : MonoBehaviour
{
    public static TutorialScreenUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private const int TotalPages = 4;
    private int _currentPage = 0;
    private bool _hasShownPreGameTutorialThisScene = false;
    private bool _wasPausedBeforeTutorial = false;
    private Action _onCloseCallback;

    // UI References
    private GameObject _canvasGO;
    private GameObject _modalRoot;
    private GameObject[] _pages = new GameObject[TotalPages];
    private Image[] _pageDots = new Image[TotalPages];
    private Text _pageTitleText;
    private Text _pageCounterText;
    private Button _prevBtn;
    private Button _nextBtn;
    private Text _closeBtnText;

    // Dynamic texts that reflect current keybindings
    private Text[] _dynamicKeyTexts;

    // Assets
    private Font _horrorFont;
    private Font _typewriterFont;
    private Sprite _paperBgSprite;
    private Sprite _cardPaperSprite;
    private Sprite _buttonSprite;
    private Sprite _flashlightSprite;
    private Sprite _unsignedSlipSprite;
    private Sprite _signedSlipSprite;
    private Sprite _stampSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstance();
    }

    public static TutorialScreenUI EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("TutorialScreenUI");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TutorialScreenUI>();
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAssets();
        BuildTutorialUI();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;
        _hasShownPreGameTutorialThisScene = false;
        if (_modalRoot != null) _modalRoot.SetActive(false);
    }

    private void Update()
    {
        // Allow toggling the Tutorial anytime during gameplay with [H] or [F1]
        if (!IsOpen)
        {
            if (SceneManager.GetActiveScene().name == "SampleScene" &&
                !OpeningCutsceneDirector.IsCutsceneActive &&
                !WinCutsceneDirector.IsCutsceneActive &&
                !GuardChaseCutsceneDirector.IsCutsceneActive &&
                !PauseMenu.GameIsPaused &&
                !GameSettingsManager.IsSettingsOpen &&
                !PauseMenu.IsAnyModalOrPuzzleOpen)
            {
                if (GameSettingsManager.GetKeyDown(GameAction.TutorialHelp))
                {
                    Open(0, null);
                }
            }
            return;
        }

        // Navigation shortcuts while Tutorial is open
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NextPage();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PrevPage();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            Close();
        }
    }

    // ── Public Open / Close API ──────────────────────────────────────────────

    public static void OpenTutorial(int startPage = 0, Action onClose = null)
    {
        EnsureInstance();
        Instance.Open(startPage, onClose);
    }

    public static void CloseTutorial()
    {
        if (Instance != null && IsOpen)
        {
            Instance.Close();
        }
    }

    public void Open(int startPage = 0, Action onClose = null)
    {
        if (_modalRoot == null) BuildTutorialUI();

        _onCloseCallback = onClose;
        _wasPausedBeforeTutorial = PauseMenu.GameIsPaused;

        // Rebuild dynamic text labels so any custom keybindings are reflected
        RefreshDynamicKeybindTexts();

        _modalRoot.SetActive(true);
        IsOpen = true;

        SetPage(Mathf.Clamp(startPage, 0, TotalPages - 1));

        if (SceneManager.GetActiveScene().name == "SampleScene")
        {
            Time.timeScale = 0f;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (_modalRoot != null) _modalRoot.SetActive(false);
        IsOpen = false;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "SampleScene")
        {
            if (_wasPausedBeforeTutorial)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
    }

    public void NextPage()
    {
        if (_currentPage < TotalPages - 1)
            SetPage(_currentPage + 1);
        else
            SetPage(0);
    }

    public void PrevPage()
    {
        if (_currentPage > 0)
            SetPage(_currentPage - 1);
        else
            SetPage(TotalPages - 1);
    }

    public void SetPage(int pageIndex)
    {
        _currentPage = Mathf.Clamp(pageIndex, 0, TotalPages - 1);

        string[] pageTitles =
        {
            "PAGE 1: MGA KONTROL AT PAGGALAW // BASIC CONTROLS",
            "PAGE 2: FLASHLIGHT, HOTBAR AT PAGBAGSAK NG GAMIT // ITEM & DROP CONTROLS",
            "PAGE 3: PAANO MANALO AT MAKATAKAS // HOW TO COMPLETE YOUR CLEARANCE",
            "PAGE 4: BLACKOUT AT MGA NILALANG SA DILIM // SURVIVING CAMPUS THREATS"
        };

        if (_pageTitleText != null)
            _pageTitleText.text = pageTitles[_currentPage];

        if (_pageCounterText != null)
            _pageCounterText.text = $"PAGE {_currentPage + 1} / {TotalPages}   •   USE [<] [>] OR [A / D] TO FLIP PAGES";

        for (int i = 0; i < TotalPages; i++)
        {
            if (_pages[i] != null) _pages[i].SetActive(i == _currentPage);
            if (_pageDots[i] != null)
            {
                _pageDots[i].color = (i == _currentPage)
                    ? new Color(0.68f, 0.08f, 0.08f, 1f)
                    : new Color(0.25f, 0.20f, 0.16f, 0.45f);
                _pageDots[i].rectTransform.localScale = (i == _currentPage)
                    ? new Vector3(1.25f, 1.25f, 1f)
                    : Vector3.one;
            }
        }

        if (_closeBtnText != null)
        {
            bool isInGame = SceneManager.GetActiveScene().name == "SampleScene" && !_wasPausedBeforeTutorial;
            _closeBtnText.text = isInGame ? "MAGLARO NA // START GAME [SPACE]" : "ISARA // CLOSE [ESC]";
        }
    }

    // ── Asset Loading ────────────────────────────────────────────────────────

    private void LoadAssets()
    {
        _horrorFont = LoadFont("Main Menu/watch people die", "Assets/Main Menu/watch people die.ttf");
        _typewriterFont = LoadFont("Fonts/Lucida_Typewriter_Bold", "Assets/Fonts/Lucida_Typewriter_Bold.ttf") ?? LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");

        _paperBgSprite = LoadSprite("Assets/Textures/UI/Blank_paper.png");
        _cardPaperSprite = LoadSprite("Assets/Synty/ScrambledPaper_HUD.png");
        _buttonSprite = LoadSprite("Assets/Textures/UI/KeypadButton_Normal.png");
        _flashlightSprite = LoadSprite("Assets/Textures/Icons/FlashLight.png");
        _unsignedSlipSprite = LoadSprite("Assets/Clearance Variations/Blank.png");
        _signedSlipSprite = LoadSprite("Assets/Clearance Variations/signed6.png");
        _stampSprite = LoadSprite("Assets/Textures/UI/PaidClearedStamp.png");
    }

    private Font LoadFont(string resourcePath, string editorAssetPath)
    {
        Font f = Resources.Load<Font>(resourcePath);
#if UNITY_EDITOR
        if (f == null && !string.IsNullOrEmpty(editorAssetPath))
            f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);
#endif
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    private Sprite LoadSprite(string editorAssetPath)
    {
#if UNITY_EDITOR
        Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
        if (sp != null) return sp;
        Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(editorAssetPath);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
#endif
        return null;
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private Text _p1MoveKeys, _p1SprintKey, _p1CrouchKey, _p1InteractKey, _p1BagKey, _p1HelpKey;
    private Text _p2PickupKey, _p2ToggleKey, _p2DropKey;

    private void BuildTutorialUI()
    {
        if (_canvasGO != null) Destroy(_canvasGO);

        _canvasGO = new GameObject("TutorialScreen_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasGO.transform.SetParent(transform, false);

        var canvas = _canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 955;

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _modalRoot = new GameObject("TutorialModalRoot", typeof(RectTransform), typeof(Image));
        _modalRoot.transform.SetParent(_canvasGO.transform, false);
        StretchFull(_modalRoot.GetComponent<RectTransform>());
        var backdrop = _modalRoot.GetComponent<Image>();
        backdrop.color = new Color(0.01f, 0.01f, 0.02f, 0.86f);
        backdrop.raycastTarget = true;

        // Main Paper Sheet Container (modeled after the reference HELP screen)
        var sheetGO = new GameObject("TutorialPaperSheet", typeof(RectTransform), typeof(Image), typeof(Shadow));
        sheetGO.transform.SetParent(_modalRoot.transform, false);
        var sheetRT = sheetGO.GetComponent<RectTransform>();
        sheetRT.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRT.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRT.pivot = new Vector2(0.5f, 0.5f);
        sheetRT.sizeDelta = new Vector2(1380f, 860f);

        var sheetImg = sheetGO.GetComponent<Image>();
        if (_paperBgSprite != null) sheetImg.sprite = _paperBgSprite;
        sheetImg.color = new Color(0.95f, 0.91f, 0.83f, 0.99f);

        var sheetShadow = sheetGO.GetComponent<Shadow>();
        sheetShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sheetShadow.effectDistance = new Vector2(10f, -12f);

        // Top Banner Ribbon ("HELP")
        var bannerGO = new GameObject("HelpHeaderBanner", typeof(RectTransform), typeof(Image), typeof(Shadow));
        bannerGO.transform.SetParent(sheetGO.transform, false);
        SetAnchors(bannerGO.GetComponent<RectTransform>(), new Vector2(0.32f, 0.89f), new Vector2(0.68f, 0.975f));
        var bannerImg = bannerGO.GetComponent<Image>();
        if (_cardPaperSprite != null) bannerImg.sprite = _cardPaperSprite;
        bannerImg.color = new Color(0.84f, 0.72f, 0.53f, 0.98f);
        var bSh = bannerGO.GetComponent<Shadow>();
        bSh.effectColor = new Color(0f, 0f, 0f, 0.55f);
        bSh.effectDistance = new Vector2(3f, -4f);

        var bannerTitle = MakeText("BannerTitle", bannerGO.transform, "HELP // GABAY",
            _horrorFont, 36, new Color(0.20f, 0.12f, 0.08f, 1f), TextAnchor.MiddleCenter);
        StretchFull(bannerTitle.rectTransform);

        // Top-Right Close [X] Button
        CreateButton(sheetGO.transform, "BtnCloseX", "X",
            new Vector2(0.90f, 0.895f), new Vector2(0.95f, 0.965f),
            new Color(0.62f, 0.10f, 0.10f, 0.96f), new Color(0.98f, 0.95f, 0.88f, 1f), 22,
            () => Close());

        // Page Subtitle Bar
        _pageTitleText = MakeText("PageSubtitle", sheetGO.transform, "",
            _horrorFont, 24, new Color(0.65f, 0.07f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(_pageTitleText.rectTransform, new Vector2(0.10f, 0.815f), new Vector2(0.90f, 0.88f));

        // Left & Right Navigation Arrow Buttons (< and >) on the sides of the sheet
        _prevBtn = CreateButton(sheetGO.transform, "BtnPrevPage", "<",
            new Vector2(0.025f, 0.42f), new Vector2(0.075f, 0.56f),
            new Color(0.22f, 0.16f, 0.12f, 0.96f), new Color(0.98f, 0.93f, 0.82f, 1f), 32,
            () => PrevPage());

        _nextBtn = CreateButton(sheetGO.transform, "BtnNextPage", ">",
            new Vector2(0.925f, 0.42f), new Vector2(0.975f, 0.56f),
            new Color(0.22f, 0.16f, 0.12f, 0.96f), new Color(0.98f, 0.93f, 0.82f, 1f), 32,
            () => NextPage());

        // Build the 4 Pages inside the content area (0.09..0.91 x, 0.19..0.80 y)
        for (int i = 0; i < TotalPages; i++)
        {
            var pGO = new GameObject($"TutorialPage_{i + 1}", typeof(RectTransform));
            pGO.transform.SetParent(sheetGO.transform, false);
            SetAnchors(pGO.GetComponent<RectTransform>(), new Vector2(0.085f, 0.185f), new Vector2(0.915f, 0.805f));
            _pages[i] = pGO;
        }

        BuildPage1_Controls(_pages[0].transform);
        BuildPage2_FlashlightAndDrop(_pages[1].transform);
        BuildPage3_HowToWin(_pages[2].transform);
        BuildPage4_SurvivingBlackouts(_pages[3].transform);

        // Bottom Page Dots Indicator (● ○ ○ ○)
        for (int i = 0; i < TotalPages; i++)
        {
            int idx = i;
            float centerX = 0.455f + i * 0.030f;
            var dotBtn = CreateButton(sheetGO.transform, $"PageDot_{i}", "",
                new Vector2(centerX - 0.009f, 0.132f), new Vector2(centerX + 0.009f, 0.158f),
                new Color(0.25f, 0.20f, 0.16f, 0.45f), Color.clear, 10,
                () => SetPage(idx));
            _pageDots[i] = dotBtn.GetComponent<Image>();
        }

        // Page Counter Text
        _pageCounterText = MakeText("PageCounter", sheetGO.transform, "PAGE 1 / 4",
            _typewriterFont, 14, new Color(0.22f, 0.16f, 0.12f, 0.92f), TextAnchor.MiddleLeft);
        SetAnchors(_pageCounterText.rectTransform, new Vector2(0.09f, 0.06f), new Vector2(0.55f, 0.12f));

        // Bottom-Right Start / Resume Button
        var startBtn = CreateButton(sheetGO.transform, "BtnStartResume", "MAGLARO NA // START GAME [SPACE]",
            new Vector2(0.60f, 0.055f), new Vector2(0.915f, 0.125f),
            new Color(0.62f, 0.09f, 0.09f, 0.98f), new Color(0.98f, 0.95f, 0.88f, 1f), 17,
            () => Close());
        _closeBtnText = startBtn.GetComponentInChildren<Text>();

        _modalRoot.SetActive(false);
    }

    // ── PAGE 1: BASIC CONTROLS & MOVEMENT ────────────────────────────────────

    private void BuildPage1_Controls(Transform parent)
    {
        // 6 Visual Cards in a 3x2 grid
        CreateControlCard(parent, new Vector2(0.00f, 0.52f), new Vector2(0.32f, 0.98f),
            "1. MOVE / PAGLAKAD", "W  A  S  D",
            "Walk through classrooms, hallways, and stairs across the 1st, 2nd, and 3rd floors.",
            out _p1MoveKeys);

        CreateControlCard(parent, new Vector2(0.34f, 0.52f), new Vector2(0.66f, 0.98f),
            "2. SPRINT / PAGTAKBO", "L-SHIFT",
            "Hold to run faster when escaping threats. Watch your Stamina bar at the bottom!",
            out _p1SprintKey);

        CreateControlCard(parent, new Vector2(0.68f, 0.52f), new Vector2(1.00f, 0.98f),
            "3. CROUCH / PAGYUKO", "L-CTRL / C",
            "Crouch to lower your footsteps and move stealthily in dark corridors.",
            out _p1CrouchKey);

        CreateControlCard(parent, new Vector2(0.00f, 0.02f), new Vector2(0.32f, 0.48f),
            "4. INTERACT / GAMITIN", "E",
            "Pick up items, talk to Department NPCs, hide inside Lockers, and operate terminals.",
            out _p1InteractKey);

        CreateControlCard(parent, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.48f),
            "5. 6-SLOT HOTBAR", "1 - 6 / SCROLL",
            "You have 6 inventory slots. Press [1]-[6] or scroll the Mouse Wheel to equip held items.",
            out _p1BagKey);

        CreateControlCard(parent, new Vector2(0.68f, 0.02f), new Vector2(1.00f, 0.48f),
            "6. HELP & SETTINGS", "H / ESC",
            "Press [H] anytime to reopen this Tutorial, or [ESC] to adjust Video, Audio & Keybinds.",
            out _p1HelpKey);
    }

    // ── PAGE 2: FLASHLIGHT, HOTBAR & DROPPING ITEMS ──────────────────────────

    private void BuildPage2_FlashlightAndDrop(Transform parent)
    {
        // 3 Large Vertical Illustrated Cards (matching the 3-card layout in reference screenshot!)
        var card1 = CreateTallCard(parent, new Vector2(0.00f, 0.02f), new Vector2(0.32f, 0.98f),
            "EQUIP & HOTBAR SLOTS",
            "Pick up the Flashlight on the Lobby Round Table with [E].\n\n" +
            "Press keys [1] to [6] or use the Mouse Scroll Wheel to equip or stow items on your 6-slot hotbar.",
            _flashlightSprite, "E  /  1 - 6", out _p2PickupKey);

        var card2 = CreateTallCard(parent, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.98f),
            "FLASHLIGHT & INSPECT",
            "While holding the Flashlight in your hand:\n" +
            "• Press LEFT CLICK [LMB] or [F] to toggle the Flashlight beam ON / OFF.\n" +
            "• Press [V] to inspect any held item or document in 3D at any time.",
            _flashlightSprite, "LMB / F  •  V", out _p2ToggleKey);

        var card3 = CreateTallCard(parent, new Vector2(0.68f, 0.02f), new Vector2(1.00f, 0.98f),
            "HOW TO DROP ITEMS [G]",
            "Want to drop the Flashlight or any held item?\n\n" +
            "• Select its slot [1-6] so it is in your hand, then press [G] (or [Q]) to drop it onto the floor!\n" +
            "• You only have 6 slots—drop items you don't need to free up a slot.",
            _unsignedSlipSprite, "PRESS [G] / [Q]", out _p2DropKey);
    }

    // ── PAGE 3: HOW TO WIN (THE 6 CLEARANCE SIGNATURES) ──────────────────────

    private void BuildPage3_HowToWin(Transform parent)
    {
        Text dummy;
        CreateTallCard(parent, new Vector2(0.00f, 0.02f), new Vector2(0.32f, 0.98f),
            "STEP 1: PRINT CLEARANCE",
            "1. Search the 1st Floor (Guard House, Registrar, CR, Guidance) for the 4 Torn Clearance Fragments.\n\n" +
            "2. Bring the 4 Fragments + VGA Cable + Bond Paper to the 1F Computer Lab Terminal to print your Blank Clearance Slip!",
            _unsignedSlipSprite, "4 FRAGMENTS -> PRINT", out dummy);

        CreateTallCard(parent, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.98f),
            "STEP 2: 6 SIGNATURES",
            "Complete each office's task & get signed IN ORDER:\n" +
            "1. Library (3F) — Return Archive Book\n" +
            "2. Guidance (2F) — Safe & Word Puzzle\n" +
            "3. CCS Dean (2F) — ComLab Terminal\n" +
            "4. Registrar (1F) — Clinic + Sort Puzzle\n" +
            "5. Cashier (1F) — Balance + 50 PHP\n" +
            "6. EVP Office (1F) — Fuse + Dry Seal",
            _signedSlipSprite, "SIGN 1 -> 6 IN ORDER", out dummy);

        CreateTallCard(parent, new Vector2(0.68f, 0.02f), new Vector2(1.00f, 0.98f),
            "STEP 3: VALIDATE & ESCAPE",
            "1. Once all 6 signatures are complete, return to the Registrar Window (1F) for final validation!\n\n" +
            "2. Run to the Main Lobby Entrance Gate and press [E] before 12:00 Midnight to escape and WIN!",
            _stampSprite, "MAIN GATE ESCAPE [E]", out dummy);
    }

    // ── PAGE 4: SURVIVING BLACKOUTS & ENTITIES ───────────────────────────────

    private void BuildPage4_SurvivingBlackouts(Transform parent)
    {
        Text dummy;
        CreateTallCard(parent, new Vector2(0.00f, 0.02f), new Vector2(0.32f, 0.98f),
            "CAMPUS BLACKOUTS",
            "The campus power grid periodically fails!\n\n" +
            "During a Blackout, either THE PROCTOR or THE WEEPING STUDENT spawns and hunts in the dark.\n\n" +
            "Find and flip the glowing Wall Circuit Breaker to restore power and banish them!",
            _flashlightSprite, "FLIP BREAKER [E]", out dummy);

        CreateTallCard(parent, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.98f),
            "LOCKERS & STEALTH",
            "When your screen glitches or you hear heavy footsteps / crying:\n\n" +
            "• Turn OFF your Flashlight [LMB / F] so you aren't spotted from afar.\n" +
            "• Press [E] on any Metal Locker in the hallway to hide inside until the threat leaves!",
            _stampSprite, "HIDE IN LOCKER [E]", out dummy);

        CreateTallCard(parent, new Vector2(0.68f, 0.02f), new Vector2(1.00f, 0.98f),
            "COINS & CANTEEN FOOD",
            "Being chased raises your Anxiety and drains Stamina.\n\n" +
            "• Pick up 5-Peso Coins scattered on desks and tables.\n" +
            "• Buy Ham & Cheese Sandwiches at the Canteen and press [LMB / F] while holding one to restore Stamina & calm Anxiety!",
            _unsignedSlipSprite, "EAT SANDWICH [LMB/F]", out dummy);
    }

    // ── Card Builders ────────────────────────────────────────────────────────

    private void CreateControlCard(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string title, string keyBadgeText, string description, out Text badgeTextOut)
    {
        var cardGO = new GameObject($"Card_{title}", typeof(RectTransform), typeof(Image), typeof(Shadow));
        cardGO.transform.SetParent(parent, false);
        SetAnchors(cardGO.GetComponent<RectTransform>(), anchorMin, anchorMax);

        var bg = cardGO.GetComponent<Image>();
        if (_cardPaperSprite != null) bg.sprite = _cardPaperSprite;
        bg.color = new Color(0.90f, 0.84f, 0.72f, 0.96f);

        var sh = cardGO.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sh.effectDistance = new Vector2(4f, -5f);

        var titleTxt = MakeText("Title", cardGO.transform, title,
            _horrorFont, 21, new Color(0.64f, 0.07f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(titleTxt.rectTransform, new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.94f));

        // Key Badge Box
        var badgeGO = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image), typeof(Shadow));
        badgeGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(badgeGO.GetComponent<RectTransform>(), new Vector2(0.16f, 0.44f), new Vector2(0.84f, 0.71f));
        var badgeImg = badgeGO.GetComponent<Image>();
        if (_buttonSprite != null) badgeImg.sprite = _buttonSprite;
        badgeImg.color = new Color(0.17f, 0.13f, 0.10f, 0.96f);
        var bSh = badgeGO.GetComponent<Shadow>();
        bSh.effectColor = new Color(0f, 0f, 0f, 0.5f);
        bSh.effectDistance = new Vector2(2f, -3f);

        badgeTextOut = MakeText("KeyText", badgeGO.transform, keyBadgeText,
            _typewriterFont, 20, new Color(0.98f, 0.92f, 0.76f, 1f), TextAnchor.MiddleCenter);
        StretchFull(badgeTextOut.rectTransform);

        var descTxt = MakeText("Desc", cardGO.transform, description,
            _typewriterFont, 14, new Color(0.12f, 0.09f, 0.07f, 0.96f), TextAnchor.UpperCenter);
        SetAnchors(descTxt.rectTransform, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.40f));
    }

    private GameObject CreateTallCard(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        string title, string body, Sprite illustration, string badgeLabel, out Text badgeTextOut)
    {
        var cardGO = new GameObject($"TallCard_{title}", typeof(RectTransform), typeof(Image), typeof(Shadow));
        cardGO.transform.SetParent(parent, false);
        SetAnchors(cardGO.GetComponent<RectTransform>(), anchorMin, anchorMax);

        var bg = cardGO.GetComponent<Image>();
        if (_cardPaperSprite != null) bg.sprite = _cardPaperSprite;
        bg.color = new Color(0.90f, 0.84f, 0.72f, 0.96f);

        var sh = cardGO.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.48f);
        sh.effectDistance = new Vector2(4f, -6f);

        // Header
        var titleTxt = MakeText("Title", cardGO.transform, title,
            _horrorFont, 23, new Color(0.64f, 0.07f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(titleTxt.rectTransform, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f));

        // Illustration Box (top-middle like reference screenshot)
        var illusFrame = new GameObject("IllustrationFrame", typeof(RectTransform), typeof(Image));
        illusFrame.transform.SetParent(cardGO.transform, false);
        SetAnchors(illusFrame.GetComponent<RectTransform>(), new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.83f));
        illusFrame.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.09f, 0.88f);

        if (illustration != null)
        {
            var iconGO = new GameObject("IllustrationSprite", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(illusFrame.transform, false);
            SetAnchors(iconGO.GetComponent<RectTransform>(), new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f));
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = illustration;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }

        // Key / Action Badge Strip
        var badgeGO = new GameObject("ActionBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(badgeGO.GetComponent<RectTransform>(), new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.54f));
        badgeGO.GetComponent<Image>().color = new Color(0.58f, 0.10f, 0.10f, 0.95f);

        badgeTextOut = MakeText("BadgeText", badgeGO.transform, badgeLabel,
            _typewriterFont, 15, new Color(0.98f, 0.94f, 0.84f, 1f), TextAnchor.MiddleCenter);
        StretchFull(badgeTextOut.rectTransform);

        // Body Text
        var bodyTxt = MakeText("BodyText", cardGO.transform, body,
            _typewriterFont, 14, new Color(0.10f, 0.08f, 0.06f, 0.98f), TextAnchor.UpperLeft);
        bodyTxt.lineSpacing = 1.18f;
        SetAnchors(bodyTxt.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.43f));

        return cardGO;
    }

    private void RefreshDynamicKeybindTexts()
    {
        string moveStr = $"{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.MoveForward))} " +
                         $"{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.MoveLeft))} " +
                         $"{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.MoveBackward))} " +
                         $"{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.MoveRight))}";
        string sprintStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Sprint));
        string crouchStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Crouch)) + " / C";
        string interactStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Interact));
        string bagStr = "1 - 6 / SCROLL";
        string helpStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.TutorialHelp)) + " / ESC";
        string flashStr = "LMB / " + GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Flashlight));
        string dropStr = $"PRESS [{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.DropItem))}] / [Q]";

        if (_p1MoveKeys != null) _p1MoveKeys.text = moveStr;
        if (_p1SprintKey != null) _p1SprintKey.text = sprintStr;
        if (_p1CrouchKey != null) _p1CrouchKey.text = crouchStr;
        if (_p1InteractKey != null) _p1InteractKey.text = interactStr;
        if (_p1BagKey != null) _p1BagKey.text = bagStr;
        if (_p1HelpKey != null) _p1HelpKey.text = helpStr;

        if (_p2PickupKey != null) _p2PickupKey.text = $"[{interactStr}] PICK UP  •  [1-6] SLOT";
        if (_p2ToggleKey != null) _p2ToggleKey.text = $"{flashStr}  •  [{GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.InspectItem))}] INSPECT";
        if (_p2DropKey != null) _p2DropKey.text = dropStr;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor, int fontSize, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        go.transform.SetParent(parent, false);
        SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);

        var img = go.GetComponent<Image>();
        if (_buttonSprite != null) img.sprite = _buttonSprite;
        img.color = bgColor;

        var sh = go.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.65f);
        sh.effectDistance = new Vector2(2f, -3f);

        if (!string.IsNullOrEmpty(label))
        {
            var txt = MakeText("Text", go.transform, label, _typewriterFont, fontSize, textColor, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
        }

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    private Text MakeText(string name, Transform parent, string content, Font font, int size, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        var txt = go.GetComponent<Text>();
        txt.text = content;
        txt.font = font;
        txt.fontSize = size;
        txt.fontStyle = FontStyle.Bold;
        txt.color = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
