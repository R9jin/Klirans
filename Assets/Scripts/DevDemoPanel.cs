using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// DevDemoPanel — Comprehensive, full-power developer and demo control console for KLIRANS.
/// 
/// Capabilities:
/// - Quick Toggle: F1, F3, or ~ (Tilde/Backquote), or via Pause Menu button.
/// - Tab 1: [⚡ THREATS & BLACKOUT]
///     * Live Blackout Trigger / Immediate Power Restore / Toggle Blackouts Disabled.
///     * Enemy Filter Modes: [All / Dynamic], [Only Proctor], [Only Weeping], [Only Guard Stalker], [Peaceful / None].
///     * Quick Spawn & Clean Despawn buttons for every threat entity.
///     * Live Night Threat Level (Signatures 0-6 difficulty scaling).
/// - Tab 2: [👁 3D GIZMOS & HUD]
///     * Toggles for 3D Vision Cones, Floor Hearing Radii, LOS Rays, NavMesh Paths.
///     * Overhead 3D World Billboard state badges.
///     * Real-time top-left telemetry HUD readout.
/// - Tab 3: [⚙ AI PARAMETERS & LIVE SLIDERS]
///     * Weeping Student: Chase Speed, Step Interval, Silhouette Radius, Require Flashlight, Endo Teleport Mode.
///     * The Proctor: Speed, Sprint Hearing, Walk Hearing, Catch Distance.
///     * Security Guard: Patrol Speed, Chase Speed, Vision Distance, Whistle Cadence.
/// - Tab 4: [🏃 PLAYER & CHEATS]
///     * God Mode (Immune to all catches & jumpscares).
///     * Infinite Stamina & Super Flashlight.
///     * Player Speed Multiplier (1.0x - 3.5x).
///     * Anxiety Slider & Instant Calm.
///     * +99 Coins, +10 Lockpicks.
///     * Assemble 4/4 Fragments, Grant 6/6 Signatures, Unlock Main Gate, Trigger Win Ending.
/// - Tab 5: [🗺 WARP & TELEPORT]
///     * 1-Click Teleport to 1F Lobby, Exit Gate, 104 Guidance, 105 ComLab, Registrar, Breaker Box,
///       2F Corridor, 2F Records, 3F Corridor, 3F Library, and warp directly to any active enemy.
/// </summary>
public class DevDemoPanel : MonoBehaviour
{
    private static DevDemoPanel _instance;
    public static DevDemoPanel Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<DevDemoPanel>();
                if (_instance == null)
                {
                    var go = new GameObject("[DevDemoPanel]");
                    _instance = go.AddComponent<DevDemoPanel>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public enum EnemyFilter
    {
        All = 0,
        OnlyProctor = 1,
        OnlyWeeping = 2,
        OnlyGuardStalker = 3,
        Peaceful = 4
    }

    // ── Global Static Cheat & Dev States ────────────────────────────────────
    public static bool IsOpen { get; private set; } = false;
    public static bool IsDevSpawning { get; set; } = false;

    private static bool _isGodModeActive = false;
    public static bool IsGodModeActive
    {
        get => GameModeManager.IsFreeRoamMode && _isGodModeActive;
        set => _isGodModeActive = value;
    }

    private static bool _isInfiniteStaminaActive = false;
    public static bool IsInfiniteStaminaActive
    {
        get => GameModeManager.IsFreeRoamMode && _isInfiniteStaminaActive;
        set => _isInfiniteStaminaActive = value;
    }

    private static bool _isSuperFlashlightActive = false;
    public static bool IsSuperFlashlightActive
    {
        get => GameModeManager.IsFreeRoamMode && _isSuperFlashlightActive;
        set => _isSuperFlashlightActive = value;
    }

    private static float _playerSpeedMultiplier = 1.0f;
    public static float PlayerSpeedMultiplier
    {
        get => GameModeManager.IsFreeRoamMode ? _playerSpeedMultiplier : 1.0f;
        set => _playerSpeedMultiplier = value;
    }

    private static bool _isGateUnlocked = false;
    public static bool IsGateUnlocked
    {
        get => GameModeManager.IsFreeRoamMode && _isGateUnlocked;
        set => _isGateUnlocked = value;
    }

    private static EnemyFilter _currentEnemyFilter = EnemyFilter.All;
    public static EnemyFilter CurrentEnemyFilter
    {
        get => GameModeManager.IsFreeRoamMode ? _currentEnemyFilter : EnemyFilter.All;
        set => _currentEnemyFilter = value;
    }

    // ── Visualizer Toggles ──────────────────────────────────────────────────
    private static bool _isVisualizerEnabled = true;
    public static bool IsVisualizerEnabled
    {
        get => GameModeManager.IsFreeRoamMode && _isVisualizerEnabled;
        set => _isVisualizerEnabled = value;
    }
    public static bool ShowVisionCones = true;
    public static bool ShowHearingRadii = true;
    public static bool ShowLOSRays = true;
    public static bool ShowNavMeshPaths = true;
    public static bool ShowWorldBadges = true;
    public static bool ShowScreenHUD = true;

    // ── UI Runtime Configuration ────────────────────────────────────────────
    [Header("Hotkeys")]
    public KeyCode toggleKey1 = KeyCode.F1;
    public KeyCode toggleKey2 = KeyCode.F3;
    public KeyCode toggleKey3 = KeyCode.BackQuote; // ~ key

    [Header("Simulation")]
    [Tooltip("If true, the game runs live in the background while the panel is open. If false, time is paused.")]
    public bool liveSimulationWhileOpen = true;

    private int _selectedTab = 0;
    private readonly string[] _tabNames = new string[]
    {
        "⚡ THREATS & BLACKOUT",
        "👁 3D GIZMOS & HUD",
        "⚙ AI PARAMETERS",
        "🏃 PLAYER & CHEATS",
        "🗺 WARP & TELEPORT"
    };

    private Rect _windowRect = new Rect(40, 40, 720, 640);
    private Vector2 _scrollPos = Vector2.zero;
    private bool _stylesInitialized = false;

    private GUIStyle _windowStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _tabButtonStyle;
    private GUIStyle _tabButtonActiveStyle;
    private GUIStyle _sectionHeaderStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _subLabelStyle;
    private GUIStyle _actionButtonStyle;
    private GUIStyle _dangerButtonStyle;
    private GUIStyle _activeToggleStyle;
    private GUIStyle _badgeStyle;

    private Texture2D _panelBgTex;
    private Texture2D _btnBgTex;
    private Texture2D _activeBtnBgTex;
    private Texture2D _dangerBtnBgTex;

    // Cached References
    private PlayerMovement _playerMovement;
    private Camera _playerCam;
    private DevDemoVisualizer _visualizer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            var go = new GameObject("[DevDemoPanel]");
            _instance = go.AddComponent<DevDemoPanel>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureVisualizer();
    }

    private void Start()
    {
        FindPlayerReferences();
        EnsureVisualizer();
    }

    private void Update()
    {
        // Dev / Demo mode capabilities only exist and appear in Free Roam mode
        if (!GameModeManager.IsFreeRoamMode)
        {
            if (IsOpen) ClosePanel();
            return;
        }

        // Toggle panel hotkeys (F1, F3, ~)
        if (Input.GetKeyDown(toggleKey1) || Input.GetKeyDown(toggleKey2) || Input.GetKeyDown(toggleKey3))
        {
            TogglePanel();
        }

        // Close on Escape if open
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
        }

        // Keep player references updated across scene transitions
        if (_playerMovement == null || _playerCam == null)
        {
            FindPlayerReferences();
        }

        // Apply Super Flashlight live
        ApplySuperFlashlightState();
    }

    public static void TogglePanel()
    {
        if (IsOpen) ClosePanel();
        else OpenPanel();
    }

    public static void OpenPanel()
    {
        if (!GameModeManager.IsFreeRoamMode)
        {
            Debug.Log("[DevDemoPanel] Dev / Demo Mode is only available in Free Roam mode.");
            return;
        }

        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Instance != null && !Instance.liveSimulationWhileOpen)
        {
            Time.timeScale = 0f;
        }
        Debug.Log("[DevDemoPanel] Opened Dev / Demo Mode Panel.");
    }

    public static void ClosePanel()
    {
        IsOpen = false;
        if (Instance != null && !Instance.liveSimulationWhileOpen)
        {
            Time.timeScale = 1f;
        }

        // Only lock cursor back if not paused in PauseMenu or active UI
        if (!PauseMenu.GameIsPaused && !TutorialScreenUI.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        Debug.Log("[DevDemoPanel] Closed Dev / Demo Mode Panel.");
    }

    private void EnsureVisualizer()
    {
        if (_visualizer == null)
        {
            _visualizer = GetComponent<DevDemoVisualizer>();
            if (_visualizer == null)
            {
                _visualizer = gameObject.AddComponent<DevDemoVisualizer>();
            }
        }
    }

    private void FindPlayerReferences()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _playerMovement = p.GetComponent<PlayerMovement>();
            if (_playerMovement != null) _playerCam = _playerMovement.playerCamera;
        }
        if (_playerCam == null) _playerCam = Camera.main;
    }

    private void ApplySuperFlashlightState()
    {
        var fl = FlashlightController.Instance;
        if (fl != null && fl.flashlightLight != null)
        {
            if (IsSuperFlashlightActive)
            {
                fl.flashlightLight.range = 80.0f;
                fl.flashlightLight.intensity = 10.0f;
                fl.flashlightLight.spotAngle = 95.0f;
            }
            else
            {
                fl.flashlightLight.range = fl.powerfulRange;
                fl.flashlightLight.intensity = fl.powerfulIntensity;
                fl.flashlightLight.spotAngle = fl.outerSpotAngle;
            }
        }
    }

    // ── OnGUI Interface ─────────────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _panelBgTex = MakeSolidTex(2, 2, new Color(0.06f, 0.08f, 0.10f, 0.95f));
        _btnBgTex = MakeSolidTex(2, 2, new Color(0.14f, 0.18f, 0.22f, 0.90f));
        _activeBtnBgTex = MakeSolidTex(2, 2, new Color(0.12f, 0.45f, 0.40f, 0.95f));
        _dangerBtnBgTex = MakeSolidTex(2, 2, new Color(0.55f, 0.15f, 0.15f, 0.95f));

        _windowStyle = new GUIStyle(GUI.skin.window);
        _windowStyle.normal.background = _panelBgTex;
        _windowStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
        _windowStyle.fontSize = 14;
        _windowStyle.fontStyle = FontStyle.Bold;
        _windowStyle.padding = new RectOffset(16, 16, 24, 16);

        _headerStyle = new GUIStyle(GUI.skin.label);
        _headerStyle.fontSize = 18;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.normal.textColor = new Color(0.35f, 0.90f, 0.80f);
        _headerStyle.alignment = TextAnchor.MiddleLeft;

        _tabButtonStyle = new GUIStyle(GUI.skin.button);
        _tabButtonStyle.normal.background = _btnBgTex;
        _tabButtonStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        _tabButtonStyle.fontSize = 11;
        _tabButtonStyle.fontStyle = FontStyle.Bold;
        _tabButtonStyle.padding = new RectOffset(8, 8, 8, 8);

        _tabButtonActiveStyle = new GUIStyle(_tabButtonStyle);
        _tabButtonActiveStyle.normal.background = _activeBtnBgTex;
        _tabButtonActiveStyle.normal.textColor = Color.white;

        _sectionHeaderStyle = new GUIStyle(GUI.skin.label);
        _sectionHeaderStyle.fontSize = 13;
        _sectionHeaderStyle.fontStyle = FontStyle.Bold;
        _sectionHeaderStyle.normal.textColor = new Color(1f, 0.82f, 0.35f);
        _sectionHeaderStyle.margin = new RectOffset(0, 0, 10, 4);

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.fontSize = 12;
        _labelStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f);

        _subLabelStyle = new GUIStyle(GUI.skin.label);
        _subLabelStyle.fontSize = 11;
        _subLabelStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f);

        _actionButtonStyle = new GUIStyle(GUI.skin.button);
        _actionButtonStyle.normal.background = _btnBgTex;
        _actionButtonStyle.normal.textColor = Color.white;
        _actionButtonStyle.fontSize = 12;
        _actionButtonStyle.fontStyle = FontStyle.Bold;
        _actionButtonStyle.padding = new RectOffset(10, 10, 6, 6);

        _dangerButtonStyle = new GUIStyle(_actionButtonStyle);
        _dangerButtonStyle.normal.background = _dangerBtnBgTex;
        _dangerButtonStyle.normal.textColor = new Color(1f, 0.9f, 0.9f);

        _activeToggleStyle = new GUIStyle(_actionButtonStyle);
        _activeToggleStyle.normal.background = _activeBtnBgTex;

        _badgeStyle = new GUIStyle(GUI.skin.box);
        _badgeStyle.fontSize = 11;
        _badgeStyle.normal.textColor = new Color(0.35f, 0.90f, 0.80f);
        _badgeStyle.normal.background = _btnBgTex;
        _badgeStyle.alignment = TextAnchor.MiddleCenter;

        _stylesInitialized = true;
    }

    private Texture2D MakeSolidTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void OnGUI()
    {
        // Dev / Demo HUD and Window strictly exist in Free Roam mode
        if (!GameModeManager.IsFreeRoamMode) return;

        InitStyles();

        // Subtle watermark helper pill shown ONLY in Free Roam mode when panel is closed
        if (!IsOpen)
        {
            if (!PauseMenu.GameIsPaused && !TutorialScreenUI.IsOpen && !ItemInspectionModal.IsOpen)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.80f);
                GUI.Label(new Rect(18, Screen.height - 34, 380, 24), " <color=#55FFAA>● FREE ROAM</color> | Press <color=#FFDD44>[F1]</color> for Dev / Demo Menu ", _badgeStyle);
                GUI.color = Color.white;
            }
            return;
        }

        // Screen center clamp for window
        _windowRect.width = Mathf.Min(740, Screen.width - 40);
        _windowRect.height = Mathf.Min(680, Screen.height - 40);

        _windowRect = GUI.Window(99901, _windowRect, DrawWindowContents, "KLIRANS // FULL-POWER DEMO & DEV SUITE", _windowStyle);
    }

    private void DrawWindowContents(int windowID)
    {
        // Top Header & Close Button
        GUILayout.BeginHorizontal();
        GUILayout.Label("🎮 DEMO CONTROLLER & ENGINE DEBUGGER", _headerStyle);
        GUILayout.FlexibleSpace();

        // Simulation Mode Toggle
        bool newSim = GUILayout.Toggle(liveSimulationWhileOpen, "Live Simulation", GUILayout.Width(110));
        if (newSim != liveSimulationWhileOpen)
        {
            liveSimulationWhileOpen = newSim;
            Time.timeScale = liveSimulationWhileOpen ? 1f : 0f;
        }

        if (GUILayout.Button("✕ CLOSE", _dangerButtonStyle, GUILayout.Width(75), GUILayout.Height(26)))
        {
            ClosePanel();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Tab Navigation Strip
        GUILayout.BeginHorizontal();
        for (int i = 0; i < _tabNames.Length; i++)
        {
            GUIStyle btnStyle = (_selectedTab == i) ? _tabButtonActiveStyle : _tabButtonStyle;
            if (GUILayout.Button(_tabNames[i], btnStyle, GUILayout.Height(30)))
            {
                _selectedTab = i;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Tab Body in ScrollView
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        switch (_selectedTab)
        {
            case 0: DrawThreatsAndBlackoutTab(); break;
            case 1: DrawVisualizersAndGizmosTab(); break;
            case 2: DrawAiParametersTab(); break;
            case 3: DrawPlayerAndCheatsTab(); break;
            case 4: DrawWarpAndTeleportTab(); break;
        }

        GUILayout.EndScrollView();

        // Bottom status footer
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Hotkeys: [{toggleKey1}] / [{toggleKey2}] / [~]   |   Player: {(_playerMovement != null ? "Active" : "None")}   |   TimeScale: {Time.timeScale:F1}x", _subLabelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset All Cheats", _actionButtonStyle, GUILayout.Width(130)))
        {
            ResetAllCheats();
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 10000, 30));
    }

    // ── TAB 1: THREATS & BLACKOUT ───────────────────────────────────────────

    private void DrawThreatsAndBlackoutTab()
    {
        GUILayout.Label("CAMPUS POWER & BLACKOUT CONTROL", _sectionHeaderStyle);

        bool isBlackout = (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive);
        GUILayout.BeginHorizontal();
        string powerStatus = isBlackout ? "<color=#FF4444>● BLACKOUT ACTIVE (CORRIDORS IN DARKNESS)</color>" : "<color=#44FF88>● POWER NORMAL (GRID STABLE)</color>";
        GUILayout.Label($"Grid State: {powerStatus}", _labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (!isBlackout)
        {
            if (GUILayout.Button("⚡ TRIGGER BLACKOUT NOW", _dangerButtonStyle, GUILayout.Height(32)))
            {
                if (BlackoutManager.Instance != null) BlackoutManager.Instance.TriggerBlackout(forceDev: true);
            }
        }
        else
        {
            if (GUILayout.Button("💡 RESTORE POWER IMMEDIATELY", _actionButtonStyle, GUILayout.Height(32)))
            {
                if (BlackoutManager.Instance != null) BlackoutManager.Instance.RestorePower();
            }
        }

        if (BlackoutManager.Instance != null)
        {
            bool newDisabled = GUILayout.Toggle(BlackoutManager.Instance.disableBlackouts, " Disable Random Blackouts", GUILayout.Width(200));
            if (newDisabled != BlackoutManager.Instance.disableBlackouts)
            {
                BlackoutManager.Instance.disableBlackouts = newDisabled;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("ENEMY FILTER & BLACKOUT SELECTION MODE", _sectionHeaderStyle);
        GUILayout.Label("Select which entity is permitted to stalk the hallways during blackouts:", _subLabelStyle);

        GUILayout.BeginHorizontal();
        EnemyFilter[] filters = (EnemyFilter[])Enum.GetValues(typeof(EnemyFilter));
        foreach (var f in filters)
        {
            GUIStyle st = (CurrentEnemyFilter == f) ? _tabButtonActiveStyle : _actionButtonStyle;
            string label = f switch
            {
                EnemyFilter.All => "ALL / DYNAMIC",
                EnemyFilter.OnlyProctor => "ONLY PROCTOR",
                EnemyFilter.OnlyWeeping => "ONLY WEEPING",
                EnemyFilter.OnlyGuardStalker => "ONLY GUARD",
                EnemyFilter.Peaceful => "PEACEFUL (NONE)",
                _ => f.ToString()
            };

            if (GUILayout.Button(label, st, GUILayout.Height(28)))
            {
                CurrentEnemyFilter = f;
                Debug.Log($"[DevDemoPanel] Enemy filter set to: {f}");
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("ACTIVE ENTITY QUICK SPAWN / DISMISS", _sectionHeaderStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Proctor", _actionButtonStyle, GUILayout.Height(28)))
        {
            IsDevSpawning = true;
            TheProctorManager.Instance?.SpawnProctorDebug();
            IsDevSpawning = false;
        }
        if (GUILayout.Button("Spawn Weeping Student", _actionButtonStyle, GUILayout.Height(28)))
        {
            IsDevSpawning = true;
            TheProctorManager.Instance?.SpawnWeepingStudentDebug();
            IsDevSpawning = false;
        }
        if (GUILayout.Button("Spawn Guard Stalker", _actionButtonStyle, GUILayout.Height(28)))
        {
            IsDevSpawning = true;
            SecurityGuardEndgameManager.Instance?.TriggerEndgame();
            IsDevSpawning = false;
        }
        if (GUILayout.Button("Dismiss All Enemies", _dangerButtonStyle, GUILayout.Height(28)))
        {
            DespawnAllEnemies();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("CAMPUS NIGHT PROGRESSION (THREAT SCALING)", _sectionHeaderStyle);

        int sigCount = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
        GUILayout.Label($"Current Clearance Progress: <b>{sigCount} / 6 Signatures</b> (Night Level {Mathf.Min(6, sigCount + 1)})", _labelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("- Lower Threat (Revoke 1)", _actionButtonStyle, GUILayout.Width(200)))
        {
            // Lower threat
            if (CampusThreatDirector.Instance != null && sigCount > 0)
            {
                CampusThreatDirector.Instance.SetThreatLevel(Mathf.Max(0, sigCount - 1), true);
            }
        }
        if (GUILayout.Button("+ Raise Threat (Night 6 Max)", _actionButtonStyle, GUILayout.Width(200)))
        {
            CampusThreatDirector.Instance?.SetThreatLevel(6, true);
        }
        GUILayout.EndHorizontal();
    }

    // ── TAB 2: VISUALIZERS & 3D GIZMOS ──────────────────────────────────────

    private void DrawVisualizersAndGizmosTab()
    {
        GUILayout.Label("3D IN-GAME ENGINE GIZMOS (DIRECT IN GAME VIEW)", _sectionHeaderStyle);
        GUILayout.Label("Visualizes spatial detection systems, lines of sight, radii, and AI paths in real-time:", _subLabelStyle);

        IsVisualizerEnabled = GUILayout.Toggle(IsVisualizerEnabled, "<b>Master 3D Visualizer Active</b>", _labelStyle);

        GUILayout.Space(8);
        GUILayout.BeginVertical("box");
        ShowVisionCones = GUILayout.Toggle(ShowVisionCones, " Vision Cones (Proctor, Weeping Student, Guard)", _labelStyle);
        ShowHearingRadii = GUILayout.Toggle(ShowHearingRadii, " Detection & Hearing Radii (Sprint, Walk, Catch Floor Discs)", _labelStyle);
        ShowLOSRays = GUILayout.Toggle(ShowLOSRays, " Camera Eye-Contact & LOS Rays (Cyan = Freezing gaze, Red = Blocked)", _labelStyle);
        ShowNavMeshPaths = GUILayout.Toggle(ShowNavMeshPaths, " NavMesh Path Lines & Stepping Trajectories", _labelStyle);
        ShowWorldBadges = GUILayout.Toggle(ShowWorldBadges, " Overhead 3D World Billboard Status Badges", _labelStyle);
        ShowScreenHUD = GUILayout.Toggle(ShowScreenHUD, " Top-Left Telemetry Screen HUD Readout", _labelStyle);
        GUILayout.EndVertical();

        GUILayout.Space(14);
        GUILayout.Label("ACTIVE ENTITY REAL-TIME RADAR READOUT", _sectionHeaderStyle);

        var weeping = WeepingStudentAI.ActiveWeepingStudent;
        var proctor = TheProctorAI.ActiveProctor;
        var guard = SecurityGuardStalkerAI.ActiveStalker;

        if (weeping == null && proctor == null && guard == null)
        {
            GUILayout.Label("<color=#888888>No hostile stalker entities currently active in campus hallways.</color>", _labelStyle);
        }

        if (weeping != null)
        {
            float dist = _playerMovement != null ? Vector3.Distance(weeping.transform.position, _playerMovement.transform.position) : 0f;
            string stateStr = weeping.IsObserved ? "<color=#00FFFF>[FROZEN - IN PLAYER SIGHT]</color>" : "<color=#FF3333>[ENDO TELEPORTING - LOOKING AWAY]</color>";
            GUILayout.Label($"<b>Weeping Student</b>: {stateStr} | Dist: {dist:F1}m | Floor: {weeping.assignedFloor} | Mode: {(weeping.endoTeleportMode ? "FNAF Endo" : "Smooth")}", _labelStyle);
        }

        if (proctor != null)
        {
            float dist = _playerMovement != null ? Vector3.Distance(proctor.transform.position, _playerMovement.transform.position) : 0f;
            GUILayout.Label($"<b>The Proctor</b>: <color=#FFAA00>[{proctor.currentState}]</color> | Dist: {dist:F1}m | Floor: {proctor.assignedFloor} | Speed: {proctor.chaseSpeed:F1}m/s", _labelStyle);
        }

        if (guard != null)
        {
            float dist = _playerMovement != null ? Vector3.Distance(guard.transform.position, _playerMovement.transform.position) : 0f;
            GUILayout.Label($"<b>Security Guard Stalker</b>: <color=#FFAA00>[{guard.currentState}]</color> | Dist: {dist:F1}m | Vision: {guard.visionMaxDistance:F1}m", _labelStyle);
        }
    }

    // ── TAB 3: AI PARAMETERS & LIVE SLIDERS ─────────────────────────────────

    private void DrawAiParametersTab()
    {
        GUILayout.Label("WEEPING STUDENT // FNAF SB ENDO CONTROLS", _sectionHeaderStyle);

        var weeping = WeepingStudentAI.ActiveWeepingStudent;

        float chaseSpd = weeping != null ? weeping.chaseSpeed : 8.8f;
        float stepInt = weeping != null ? weeping.teleportStepInterval : 0.28f;
        float silRad = weeping != null ? weeping.silhouetteRadius : 3.8f;
        bool endoMode = weeping != null ? weeping.endoTeleportMode : true;
        bool reqFlash = weeping != null ? weeping.requireFlashlightToFreeze : false;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Chase Rush Speed: {chaseSpd:F1} m/s", _labelStyle, GUILayout.Width(220));
        chaseSpd = GUILayout.HorizontalSlider(chaseSpd, 2.0f, 25.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Endo Step Interval: {stepInt:F2} s", _labelStyle, GUILayout.Width(220));
        stepInt = GUILayout.HorizontalSlider(stepInt, 0.08f, 1.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Silhouette Freeze Radius: {silRad:F1} m", _labelStyle, GUILayout.Width(220));
        silRad = GUILayout.HorizontalSlider(silRad, 1.0f, 10.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        endoMode = GUILayout.Toggle(endoMode, " FNAF Security Breach Endo Teleport Mode (Zero Walk Anim)", _labelStyle);
        reqFlash = GUILayout.Toggle(reqFlash, " Require Flashlight Beam in Pitch Dark", _labelStyle);
        GUILayout.EndHorizontal();

        if (weeping != null)
        {
            weeping.chaseSpeed = chaseSpd;
            weeping.teleportStepInterval = stepInt;
            weeping.silhouetteRadius = silRad;
            weeping.endoTeleportMode = endoMode;
            weeping.requireFlashlightToFreeze = reqFlash;
        }

        GUILayout.Space(14);
        GUILayout.Label("THE PROCTOR // ACOUSTIC BLIND PURSUER", _sectionHeaderStyle);

        var proctor = TheProctorAI.ActiveProctor;
        float procSpd = proctor != null ? proctor.chaseSpeed : 5.6f;
        float sprintHear = proctor != null ? proctor.sprintHearingRange : 28.0f;
        float walkHear = proctor != null ? proctor.baseHearingRange : 14.0f;
        float procCatch = proctor != null ? proctor.catchDistance : 1.4f;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Pursuit Speed: {procSpd:F1} m/s", _labelStyle, GUILayout.Width(220));
        procSpd = GUILayout.HorizontalSlider(procSpd, 2.0f, 14.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Sprint Hearing Reach: {sprintHear:F1} m", _labelStyle, GUILayout.Width(220));
        sprintHear = GUILayout.HorizontalSlider(sprintHear, 5.0f, 40.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Walk Hearing Reach: {walkHear:F1} m", _labelStyle, GUILayout.Width(220));
        walkHear = GUILayout.HorizontalSlider(walkHear, 1.0f, 20.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Catch Contact Range: {procCatch:F2} m", _labelStyle, GUILayout.Width(220));
        procCatch = GUILayout.HorizontalSlider(procCatch, 0.6f, 3.0f);
        GUILayout.EndHorizontal();

        if (proctor != null)
        {
            proctor.chaseSpeed = procSpd;
            proctor.sprintHearingRange = sprintHear;
            proctor.baseHearingRange = walkHear;
            proctor.catchDistance = procCatch;
        }

        GUILayout.Space(14);
        GUILayout.Label("SECURITY GUARD STALKER // MR. X CLIMAX", _sectionHeaderStyle);

        var guard = SecurityGuardStalkerAI.ActiveStalker;
        float gPatrol = guard != null ? guard.patrolSpeed : 2.1f;
        float gChase = guard != null ? guard.chaseSpeed : 4.2f;
        float gVision = guard != null ? guard.visionMaxDistance : 22.0f;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Patrol March Speed: {gPatrol:F1} m/s", _labelStyle, GUILayout.Width(220));
        gPatrol = GUILayout.HorizontalSlider(gPatrol, 1.0f, 5.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Chase Sprint Speed: {gChase:F1} m/s", _labelStyle, GUILayout.Width(220));
        gChase = GUILayout.HorizontalSlider(gChase, 2.0f, 8.0f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Spotlight Vision Reach: {gVision:F1} m", _labelStyle, GUILayout.Width(220));
        gVision = GUILayout.HorizontalSlider(gVision, 5.0f, 35.0f);
        GUILayout.EndHorizontal();

        if (guard != null)
        {
            guard.patrolSpeed = gPatrol;
            guard.chaseSpeed = gChase;
            guard.visionMaxDistance = gVision;
        }
    }

    // ── TAB 4: PLAYER & CHEATS ──────────────────────────────────────────────

    private void DrawPlayerAndCheatsTab()
    {
        GUILayout.Label("INVULNERABILITY & SURVIVAL CHEATS", _sectionHeaderStyle);

        GUILayout.BeginHorizontal();
        GUIStyle godStyle = IsGodModeActive ? _activeToggleStyle : _actionButtonStyle;
        if (GUILayout.Button(IsGodModeActive ? "🛡 GOD MODE: [ON] (IMMUNE TO JUMPSCARES)" : "🛡 GOD MODE: [OFF]", godStyle, GUILayout.Height(30)))
        {
            IsGodModeActive = !IsGodModeActive;
            Debug.Log($"[DevDemoPanel] God Mode: {IsGodModeActive}");
        }

        GUIStyle stamStyle = IsInfiniteStaminaActive ? _activeToggleStyle : _actionButtonStyle;
        if (GUILayout.Button(IsInfiniteStaminaActive ? "⚡ INFINITE STAMINA: [ON]" : "⚡ INFINITE STAMINA: [OFF]", stamStyle, GUILayout.Height(30)))
        {
            IsInfiniteStaminaActive = !IsInfiniteStaminaActive;
        }

        GUIStyle flashStyle = IsSuperFlashlightActive ? _activeToggleStyle : _actionButtonStyle;
        if (GUILayout.Button(IsSuperFlashlightActive ? "🔦 SUPER FLASHLIGHT: [ON]" : "🔦 SUPER FLASHLIGHT: [OFF]", flashStyle, GUILayout.Height(30)))
        {
            IsSuperFlashlightActive = !IsSuperFlashlightActive;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Player Speed Multiplier
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Player Movement Multiplier: <b>{PlayerSpeedMultiplier:F2}x</b>", _labelStyle, GUILayout.Width(240));
        PlayerSpeedMultiplier = GUILayout.HorizontalSlider(PlayerSpeedMultiplier, 1.0f, 3.5f);
        if (GUILayout.Button("1.0x", _actionButtonStyle, GUILayout.Width(45))) PlayerSpeedMultiplier = 1.0f;
        if (GUILayout.Button("2.0x", _actionButtonStyle, GUILayout.Width(45))) PlayerSpeedMultiplier = 2.0f;
        GUILayout.EndHorizontal();

        // Anxiety Slider
        var anxiety = AnxietyManager.Instance;
        float curAnx = anxiety != null ? anxiety.CurrentAnxiety : 0f;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Anxiety Level: <b>{curAnx:F1}%</b>", _labelStyle, GUILayout.Width(240));
        float newAnx = GUILayout.HorizontalSlider(curAnx, 0f, 100f);
        if (anxiety != null && Mathf.Abs(newAnx - curAnx) > 0.5f)
        {
            anxiety.SetAnxiety(newAnx);
        }
        if (GUILayout.Button("Calm (0%)", _actionButtonStyle, GUILayout.Width(80)))
        {
            anxiety?.ResetAnxiety();
        }
        if (GUILayout.Button("Max (100%)", _actionButtonStyle, GUILayout.Width(90)))
        {
            GameOverUI.Instance?.HandleGameOver();
            ClosePanel();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("ITEMS & CURRENCY INJECTION", _sectionHeaderStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+99 Pesos Coins", _actionButtonStyle, GUILayout.Height(28)))
        {
            CoinManager.Instance?.AddCoins(99);
        }
        if (GUILayout.Button("+10 Lockpins", _actionButtonStyle, GUILayout.Height(28)))
        {
            AddLockpicksCheat(10);
        }
        if (GUILayout.Button("Refill Stamina 100%", _actionButtonStyle, GUILayout.Height(28)))
        {
            var stam = FindAnyObjectByType<StaminaSystem>();
            if (stam != null) stam.currentStamina = stam.maxStamina;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("GAME PROGRESSION SHORTCUTS", _sectionHeaderStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Assemble 4/4 Fragments", _actionButtonStyle, GUILayout.Height(30)))
        {
            AssembleFragmentsCheat();
        }
        if (GUILayout.Button("Grant All 6 Signatures (6/6)", _actionButtonStyle, GUILayout.Height(30)))
        {
            GrantAllSignaturesCheat();
        }
        if (GUILayout.Button("Unlock Main Campus Gate", _actionButtonStyle, GUILayout.Height(30)))
        {
            UnlockCampusGateCheat();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("🎬 TRIGGER WIN ESCAPE CUTSCENE NOW", _dangerButtonStyle, GUILayout.Height(32)))
        {
            WinCutsceneDirector.Instance?.PlayWinCutscene();
            ClosePanel();
        }
        GUILayout.Space(4);
        if (GUILayout.Button("💀 TRIGGER ANXIETY GAME OVER CUTSCENE NOW", _dangerButtonStyle, GUILayout.Height(32)))
        {
            GameOverUI.Instance?.HandleGameOver();
            ClosePanel();
        }
    }

    // ── TAB 5: WARP & TELEPORT ──────────────────────────────────────────────

    private void DrawWarpAndTeleportTab()
    {
        GUILayout.Label("1-CLICK LANDMARK TELEPORTATION", _sectionHeaderStyle);
        GUILayout.Label("Instantly teleports the student to key campus rooms and navigation nodes:", _subLabelStyle);

        GUILayout.Label("<b>1st Floor (Ground Level)</b>", _subLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1F Main Entrance Gate", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-77.8f, 2.35f, 16.5f));
        if (GUILayout.Button("1F Security Lobby", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-84.0f, 2.35f, 18.0f));
        if (GUILayout.Button("1F Guidance (Room 104)", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-88.5f, 2.35f, -4.5f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1F ComLab 1", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-78.5f, 2.35f, 29.0f));
        if (GUILayout.Button("1F Registrar Office", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-80.0f, 2.35f, 42.0f));
        if (GUILayout.Button("1F Breaker Box Panel", _actionButtonStyle, GUILayout.Height(28))) WarpToBreaker();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("<b>2nd Floor (Administrative & Records)</b>", _subLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("2F Main Hallway Center", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-84.0f, 8.41f, 15.0f));
        if (GUILayout.Button("2F Records / Locker Room", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-88.5f, 8.41f, 35.0f));
        if (GUILayout.Button("2F Cashier / Accounting", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-80.0f, 8.41f, 42.0f));
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("<b>3rd Floor (Library & Executive)</b>", _subLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3F Main Hallway Center", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-84.0f, 14.34f, 15.0f));
        if (GUILayout.Button("3F Library (Room 308)", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-88.5f, 14.34f, -4.5f));
        if (GUILayout.Button("3F Executive VP Office", _actionButtonStyle, GUILayout.Height(28))) WarpPlayer(new Vector3(-80.0f, 14.34f, 30.0f));
        GUILayout.EndHorizontal();

        GUILayout.Space(14);
        GUILayout.Label("WARP TO ACTIVE ENTITY", _sectionHeaderStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Warp in Front of Weeping Student", _actionButtonStyle, GUILayout.Height(30)))
        {
            var weeping = WeepingStudentAI.ActiveWeepingStudent;
            if (weeping != null) WarpPlayer(weeping.transform.position + weeping.transform.forward * 2.8f);
            else Debug.LogWarning("[DevDemoPanel] No Weeping Student active in scene.");
        }
        if (GUILayout.Button("Warp in Front of The Proctor", _actionButtonStyle, GUILayout.Height(30)))
        {
            var proctor = TheProctorAI.ActiveProctor;
            if (proctor != null) WarpPlayer(proctor.transform.position + proctor.transform.forward * 3.5f);
            else Debug.LogWarning("[DevDemoPanel] No Proctor active in scene.");
        }
        if (GUILayout.Button("Warp in Front of Guard Stalker", _actionButtonStyle, GUILayout.Height(30)))
        {
            var guard = SecurityGuardStalkerAI.ActiveStalker;
            if (guard != null) WarpPlayer(guard.transform.position + guard.transform.forward * 3.0f);
            else Debug.LogWarning("[DevDemoPanel] No Guard Stalker active in scene.");
        }
        GUILayout.EndHorizontal();
    }

    // ── CHEAT IMPLEMENTATION HELPERS ────────────────────────────────────────

    public void WarpPlayer(Vector3 targetPos)
    {
        if (_playerMovement == null) FindPlayerReferences();
        if (_playerMovement == null) return;

        CharacterController cc = _playerMovement.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        _playerMovement.transform.position = targetPos;

        if (cc != null) cc.enabled = true;
        Debug.Log($"[DevDemoPanel] Warped player to {targetPos}");
    }

    private void WarpToBreaker()
    {
        var breaker = FindAnyObjectByType<CircuitBreakerPuzzle>();
        if (breaker != null)
        {
            Vector3 target = breaker.transform.position + breaker.transform.forward * 1.2f;
            WarpPlayer(target);
        }
        else
        {
            WarpPlayer(new Vector3(-84.0f, 2.35f, 28.0f));
        }
    }

    private void AddLockpicksCheat(int count)
    {
        InventoryItem lockpinAsset = null;
#if UNITY_EDITOR
        lockpinAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Lockpin.asset");
#endif
        if (lockpinAsset == null) lockpinAsset = Resources.Load<InventoryItem>("Lockpin");

        if (lockpinAsset != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(lockpinAsset, count);
            Debug.Log($"[DevDemoPanel] Added {count} Lockpin(s) to inventory.");
        }
        else
        {
            Debug.LogWarning("[DevDemoPanel] Lockpin asset could not be loaded.");
        }
    }

    private void AssembleFragmentsCheat()
    {
        if (FragmentManager.Instance != null)
        {
            // Pick up all 4 fragments immediately
            FragmentManager.Instance.CollectFragment("Top", null);
            FragmentManager.Instance.CollectFragment("Bottom", null);
            FragmentManager.Instance.CollectFragment("Left", null);
            FragmentManager.Instance.CollectFragment("Right", null);
            Debug.Log("[DevDemoPanel] Assembled all 4 fragments into Blank Clearance Slip!");
        }
    }

    private void GrantAllSignaturesCheat()
    {
        if (ClearanceManager.Instance != null)
        {
            for (int i = 0; i < 6; i++)
            {
                ClearanceManager.Instance.GrantSignature(i);
            }
            Debug.Log("[DevDemoPanel] Granted all 6 clearance signatures (6/6)!");
        }
    }

    private void UnlockCampusGateCheat()
    {
        IsGateUnlocked = true;
        if (ClearanceManager.Instance != null)
        {
            GrantAllSignaturesCheat();
            ClearanceManager.Instance.SubmitSlipToRegistrar();
        }

        // Open 3D Iron Gate immediately if present in scene
        var gateGO = GameObject.Find("06_Security_Grille_Gate");
        if (gateGO != null)
        {
            var col = gateGO.GetComponent<Collider>();
            if (col != null) col.enabled = false; // Player can pass through immediately
            gateGO.transform.position += new Vector3(0f, 0f, 2.35f);
        }
        Debug.Log("[DevDemoPanel] Campus main gate unlocked and opened!");
    }

    private void DespawnAllEnemies()
    {
        TheProctorAI.DespawnActiveProctor();
        WeepingStudentAI.DespawnActiveWeepingStudent();
        SecurityGuardStalkerAI.DespawnActiveGuardStalker();
        Debug.Log("[DevDemoPanel] All active hostile enemies dismissed.");
    }

    private void ResetAllCheats()
    {
        IsGodModeActive = false;
        IsInfiniteStaminaActive = false;
        IsSuperFlashlightActive = false;
        PlayerSpeedMultiplier = 1.0f;
        CurrentEnemyFilter = EnemyFilter.All;
        IsGateUnlocked = false;
        Time.timeScale = 1.0f;
        ApplySuperFlashlightState();
        Debug.Log("[DevDemoPanel] Reset all cheats to defaults.");
    }
}
