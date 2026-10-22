using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Sprint,
    Crouch,
    Interact,
    Flashlight,
    DropItem,
    InspectItem,
    InventoryBag,
    TutorialHelp
}

/// <summary>
/// Unified, persistent Settings & Keymapping Manager for KLIRANS.
/// Provides a 4-tab conventional Settings modal (Display/Video, Audio, Controls, Keymapping)
/// styled with authentic paper UI assets, accessible from both Main Menu and in-game Pause Menu.
/// Also enforces solid collision + NavMesh carving on Lobby_RoundTable in SampleScene.
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }
    public static bool IsSettingsOpen { get; private set; }

    public event Action OnSettingsChanged;

    // ── Display & Video Settings ─────────────────────────────────────────────
    public int ResolutionIndex { get; private set; } = -1;
    public int FullscreenModeIndex { get; private set; } = 0; // 0=FullScreenWindow, 1=Exclusive, 2=Windowed
    public bool VSyncEnabled { get; private set; } = true;
    public int TargetFPSIndex { get; private set; } = 1; // 0=30, 1=60, 2=120, 3=144, 4=Unlimited
    public int QualityLevelIndex { get; private set; } = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra
    public int ShadowQualityIndex { get; private set; } = 2; // 0=Off, 1=Hard, 2=Soft
    public float Brightness { get; private set; } = 1.0f; // 0.5 .. 1.5
    public float CameraFOV { get; private set; } = 60f;   // 50 .. 105

    // ── Audio Settings ───────────────────────────────────────────────────────
    public float MasterVolume { get; private set; } = 1.0f;
    public float MusicVolume { get; private set; } = 0.80f;
    public float SFXVolume { get; private set; } = 0.90f;
    public float VoiceVolume { get; private set; } = 1.0f;
    public bool MuteAll { get; private set; } = false;

    // ── Controls Settings ────────────────────────────────────────────────────
    public float MouseSensitivity { get; private set; } = 1.8f;
    public bool InvertMouseY { get; private set; } = false;
    public bool HeadBobEnabled { get; private set; } = true;
    public bool CrouchToggleMode { get; private set; } = false; // false = Hold, true = Toggle
    public bool ShowControlsHUD { get; private set; } = true;

    // ── Keybindings ──────────────────────────────────────────────────────────
    private readonly Dictionary<GameAction, KeyCode> _keyBindings = new Dictionary<GameAction, KeyCode>();

    private static readonly Dictionary<GameAction, KeyCode> DefaultBindings = new Dictionary<GameAction, KeyCode>
    {
        { GameAction.MoveForward,  KeyCode.W },
        { GameAction.MoveBackward, KeyCode.S },
        { GameAction.MoveLeft,     KeyCode.A },
        { GameAction.MoveRight,    KeyCode.D },
        { GameAction.Sprint,       KeyCode.LeftShift },
        { GameAction.Crouch,       KeyCode.LeftControl },
        { GameAction.Interact,     KeyCode.E },
        { GameAction.Flashlight,   KeyCode.F },
        { GameAction.DropItem,     KeyCode.G },
        { GameAction.InspectItem,  KeyCode.V },
        { GameAction.InventoryBag, KeyCode.Tab },
        { GameAction.TutorialHelp, KeyCode.H }
    };

    private struct ResolutionOption
    {
        public int width;
        public int height;
        public string label;
    }

    private List<ResolutionOption> _resolutions = new List<ResolutionOption>();
    private static readonly int[] FpsValues = { 30, 60, 120, 144, -1 };
    private static readonly string[] FpsLabels = { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "UNLIMITED" };
    private static readonly string[] FullscreenLabels = { "BORDERLESS FULLSCREEN", "EXCLUSIVE FULLSCREEN", "WINDOWED" };
    private static readonly string[] QualityLabels = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
    private static readonly string[] ShadowLabels = { "OFF", "HARD SHADOWS", "SOFT SHADOWS" };

    // ── Runtime UI References ────────────────────────────────────────────────
    private GameObject _canvasGO;
    private GameObject _modalRoot;
    private Image _gammaOverlayImage;
    private int _activeTab = 0; // 0=Display, 1=Audio, 2=Controls, 3=Keymapping
    private GameObject[] _tabPages = new GameObject[4];
    private Image[] _tabButtonBgs = new Image[4];
    private Text[] _tabButtonTexts = new Text[4];
    private Action _onCloseCallback;

    // Rebinding state
    private bool _isWaitingForKey = false;
    private GameAction _rebindingAction;
    private Text _rebindingButtonText;
    private Image _rebindingButtonBg;
    private readonly Dictionary<GameAction, Text> _keybindLabelMap = new Dictionary<GameAction, Text>();
    private readonly Dictionary<GameAction, Image> _keybindBgMap = new Dictionary<GameAction, Image>();

    // Fonts & Paper Sprites
    private Font _horrorFont;
    private Font _typewriterFont;
    private Sprite _paperBgSprite;
    private Sprite _bannerPaperSprite;
    private Sprite _buttonSprite;
    private Sprite _stampSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameSettingsManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameSettingsManager>();
        }
    }

    public static GameSettingsManager EnsureInstance()
    {
        if (Instance == null) AutoInitialize();
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

        BuildResolutionList();
        LoadAssets();
        LoadSettings();
        BuildSettingsCanvas();
        ApplyAllSettings();

        SceneManager.sceneLoaded += OnSceneLoaded;
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
        IsSettingsOpen = false;
        _isWaitingForKey = false;
        if (_modalRoot != null) _modalRoot.SetActive(false);

        ApplyAllSettings();
        EnforceLobbyRoundTableCollision();
    }

    // ── Lobby Round Table Solid Collision & NavMesh Carving Enforcer ─────────

    public static void EnforceLobbyRoundTableCollision()
    {
        GameObject roundTable = GameObject.Find("Lobby_RoundTable");
        if (roundTable == null) return;

        // Calculate combined world bounds of all renderers on Lobby_RoundTable
        Renderer[] renderers = roundTable.GetComponentsInChildren<Renderer>(true);
        Bounds worldBounds = new Bounds(roundTable.transform.position, new Vector3(1.80f, 0.78f, 1.80f));
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!hasBounds)
            {
                worldBounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }
        }

        // Ensure existing colliders on Lobby_RoundTable are solid (non-trigger)
        Collider[] existingCols = roundTable.GetComponentsInChildren<Collider>(true);
        foreach (var col in existingCols)
        {
            if (col == null) continue;
            col.isTrigger = false;
            col.enabled = true;
        }

        // Create or update a dedicated world-aligned child blocker with a CapsuleCollider that stays
        // strictly BELOW the tabletop surface (y = 2.35..3.02) so it blocks the player's CharacterController
        // without EVER covering or obstructing the Flashlight sitting on top of the table (y = 3.12)!
        Transform blockerTrans = roundTable.transform.Find("Lobby_RoundTable_SolidBlocker");
        GameObject blockerGO;
        if (blockerTrans == null)
        {
            blockerGO = new GameObject("Lobby_RoundTable_SolidBlocker");
            blockerGO.transform.SetParent(roundTable.transform, false);
        }
        else
        {
            blockerGO = blockerTrans.gameObject;
        }

        float radiusWorld = Mathf.Clamp(Mathf.Max(worldBounds.extents.x, worldBounds.extents.z) * 0.90f, 0.72f, 0.84f);
        Vector3 tableCenterWorld = new Vector3(worldBounds.center.x, 2.68f, worldBounds.center.z);
        blockerGO.transform.position = tableCenterWorld;
        blockerGO.transform.rotation = Quaternion.identity;

        Vector3 parentScale = roundTable.transform.lossyScale;
        blockerGO.transform.localScale = new Vector3(
            Mathf.Abs(parentScale.x) > 0.001f ? 1f / parentScale.x : 1f,
            Mathf.Abs(parentScale.y) > 0.001f ? 1f / parentScale.y : 1f,
            Mathf.Abs(parentScale.z) > 0.001f ? 1f / parentScale.z : 1f
        );

        // Put on Ignore Raycast layer so it never intercepts interaction raycasts
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0) blockerGO.layer = ignoreRaycastLayer;

        var capsule = blockerGO.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = blockerGO.AddComponent<CapsuleCollider>();
        capsule.isTrigger = false;
        capsule.direction = 1; // Y-axis
        capsule.center = Vector3.zero;
        capsule.radius = radiusWorld;
        capsule.height = 0.68f; // spans y = 2.34 to y = 3.02 (strictly below the 3.095 tabletop and 3.12 Flashlight!)

        var obstacle = blockerGO.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = blockerGO.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = Vector3.zero;
        obstacle.radius = radiusWorld + 0.18f; // extra margin so NPCs steer cleanly around table
        obstacle.height = 1.20f;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingMoveThreshold = 0.05f;
        obstacle.carvingTimeToStationary = 0f;

        // Also ensure the Flashlight pickup on the Lobby_RoundTable has a generous, unobstructed interaction collider
        var allPickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Exclude);
        foreach (var p in allPickups)
        {
            if (p == null) continue;
            bool isFlashlight = (p.itemData != null && p.itemData.itemName.ToLower().Contains("flashlight")) ||
                                p.gameObject.name.ToLower().Contains("flashlight");
            if (isFlashlight)
            {
                p.gameObject.layer = LayerMask.NameToLayer("Default");
                var box = p.GetComponent<BoxCollider>();
                if (box == null) box = p.gameObject.AddComponent<BoxCollider>();
                box.enabled = true;
                box.isTrigger = false;
                box.size = new Vector3(
                    Mathf.Max(box.size.x, 0.38f),
                    Mathf.Max(box.size.y, 0.24f),
                    Mathf.Max(box.size.z, 0.38f)
                );
            }
        }
    }

    // ── Static Keybinding Helpers ────────────────────────────────────────────

    public static KeyCode GetBinding(GameAction action)
    {
        EnsureInstance();
        if (Instance._keyBindings.TryGetValue(action, out KeyCode code))
            return code;
        return DefaultBindings[action];
    }

    public static bool GetKey(GameAction action)
    {
        KeyCode k = GetBinding(action);
        if (Input.GetKey(k)) return true;
        // Secondary ergonomic fallbacks
        if (action == GameAction.Crouch && k == KeyCode.LeftControl && Input.GetKey(KeyCode.C)) return true;
        if (action == GameAction.Sprint && k == KeyCode.LeftShift && Input.GetKey(KeyCode.RightShift)) return true;
        return false;
    }

    public static bool GetKeyDown(GameAction action)
    {
        KeyCode k = GetBinding(action);
        if (Input.GetKeyDown(k)) return true;
        if (action == GameAction.Crouch && k == KeyCode.LeftControl && Input.GetKeyDown(KeyCode.C)) return true;
        if (action == GameAction.DropItem && k == KeyCode.G && Input.GetKeyDown(KeyCode.Q)) return true;
        if (action == GameAction.TutorialHelp && k == KeyCode.H && Input.GetKeyDown(KeyCode.H)) return true;
        return false;
    }

    public static bool GetKeyUp(GameAction action)
    {
        KeyCode k = GetBinding(action);
        return Input.GetKeyUp(k);
    }

    private static float _smoothedMoveH = 0f;
    private static float _smoothedMoveV = 0f;
    private static int _lastMoveAxisFrame = -1;

    public static void GetMoveAxes(out float horizontal, out float vertical)
    {
        bool usingDefaultWASD =
            GetBinding(GameAction.MoveForward) == KeyCode.W &&
            GetBinding(GameAction.MoveBackward) == KeyCode.S &&
            GetBinding(GameAction.MoveLeft) == KeyCode.A &&
            GetBinding(GameAction.MoveRight) == KeyCode.D;

        if (usingDefaultWASD)
        {
            // Use Unity's native smoothed Input Manager axes for exact original acceleration & deceleration feel
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");
            _smoothedMoveH = horizontal;
            _smoothedMoveV = vertical;
            return;
        }

        if (_lastMoveAxisFrame != Time.frameCount)
        {
            _lastMoveAxisFrame = Time.frameCount;

            float rawH = 0f;
            float rawV = 0f;
            if (GetKey(GameAction.MoveForward) || Input.GetKey(KeyCode.UpArrow)) rawV += 1f;
            if (GetKey(GameAction.MoveBackward) || Input.GetKey(KeyCode.DownArrow)) rawV -= 1f;
            if (GetKey(GameAction.MoveRight) || Input.GetKey(KeyCode.RightArrow)) rawH += 1f;
            if (GetKey(GameAction.MoveLeft) || Input.GetKey(KeyCode.LeftArrow)) rawH -= 1f;

            // Smooth acceleration & smooth deceleration matching Unity Input Manager (gravity = 3, sensitivity = 3)
            float dt = Time.deltaTime;
            float rateH = Mathf.Abs(rawH) > 0.01f ? 3.5f : 3.0f;
            float rateV = Mathf.Abs(rawV) > 0.01f ? 3.5f : 3.0f;
            _smoothedMoveH = Mathf.MoveTowards(_smoothedMoveH, rawH, rateH * dt);
            _smoothedMoveV = Mathf.MoveTowards(_smoothedMoveV, rawV, rateV * dt);
        }

        horizontal = _smoothedMoveH;
        vertical = _smoothedMoveV;
    }

    public static string FormatKeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift: return "L-SHIFT";
            case KeyCode.RightShift: return "R-SHIFT";
            case KeyCode.LeftControl: return "L-CTRL";
            case KeyCode.RightControl: return "R-CTRL";
            case KeyCode.LeftAlt: return "L-ALT";
            case KeyCode.RightAlt: return "R-ALT";
            case KeyCode.Return: return "ENTER";
            case KeyCode.Escape: return "ESC";
            case KeyCode.Mouse0: return "LMB";
            case KeyCode.Mouse1: return "RMB";
            case KeyCode.Mouse2: return "MMB";
            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            default: return key.ToString().ToUpperInvariant();
        }
    }

    // ── Load & Save Settings ─────────────────────────────────────────────────

    private void BuildResolutionList()
    {
        _resolutions.Clear();
        int[,] common =
        {
            { 1280, 720 },
            { 1366, 768 },
            { 1600, 900 },
            { 1920, 1080 },
            { 2560, 1440 },
            { 3840, 2160 }
        };

        HashSet<string> added = new HashSet<string>();
        for (int i = 0; i < common.GetLength(0); i++)
        {
            int w = common[i, 0];
            int h = common[i, 1];
            string key = $"{w}x{h}";
            added.Add(key);
            _resolutions.Add(new ResolutionOption { width = w, height = h, label = $"{w} x {h}" });
        }

        foreach (var r in Screen.resolutions)
        {
            if (r.width < 1024 || r.height < 600) continue;
            string key = $"{r.width}x{r.height}";
            if (!added.Contains(key))
            {
                added.Add(key);
                _resolutions.Add(new ResolutionOption { width = r.width, height = r.height, label = $"{r.width} x {r.height}" });
            }
        }

        _resolutions.Sort((a, b) => (a.width * a.height).CompareTo(b.width * b.height));

        // Find closest match to current screen resolution
        ResolutionIndex = 0;
        for (int i = 0; i < _resolutions.Count; i++)
        {
            if (_resolutions[i].width == Screen.width && _resolutions[i].height == Screen.height)
            {
                ResolutionIndex = i;
                break;
            }
            if (_resolutions[i].width == 1920 && _resolutions[i].height == 1080)
            {
                ResolutionIndex = i;
            }
        }
    }

    public void LoadSettings()
    {
        ResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt("Settings_ResIdx", ResolutionIndex), 0, Mathf.Max(0, _resolutions.Count - 1));
        FullscreenModeIndex = Mathf.Clamp(PlayerPrefs.GetInt("Settings_FullscreenMode", 0), 0, 2);
        VSyncEnabled = PlayerPrefs.GetInt("Settings_VSync", 1) == 1;
        TargetFPSIndex = Mathf.Clamp(PlayerPrefs.GetInt("Settings_FPSIdx", 1), 0, FpsValues.Length - 1);
        QualityLevelIndex = Mathf.Clamp(PlayerPrefs.GetInt("Settings_QualityIdx", 2), 0, 3);
        ShadowQualityIndex = Mathf.Clamp(PlayerPrefs.GetInt("Settings_ShadowIdx", 2), 0, 2);
        Brightness = Mathf.Clamp(PlayerPrefs.GetFloat("Settings_Brightness", 1.0f), 0.5f, 1.5f);
        CameraFOV = Mathf.Clamp(PlayerPrefs.GetFloat("CameraFOV", 60f), 50f, 105f);

        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 1.0f));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_MusicVol", 0.80f));
        SFXVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_SFXVol", 0.90f));
        VoiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_VoiceVol", 1.0f));
        MuteAll = PlayerPrefs.GetInt("Settings_MuteAll", 0) == 1;

        MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("MouseSensitivity", 1.8f), 0.2f, 5.0f);
        InvertMouseY = PlayerPrefs.GetInt("Settings_InvertY", 0) == 1;
        HeadBobEnabled = PlayerPrefs.GetInt("Settings_HeadBob", 1) == 1;
        CrouchToggleMode = PlayerPrefs.GetInt("Settings_CrouchToggle", 0) == 1;
        ShowControlsHUD = PlayerPrefs.GetInt("Settings_ShowControlsHUD", 1) == 1;

        _keyBindings.Clear();
        foreach (var kv in DefaultBindings)
        {
            string prefKey = "Keybind_" + kv.Key;
            if (PlayerPrefs.HasKey(prefKey))
            {
                int raw = PlayerPrefs.GetInt(prefKey, (int)kv.Value);
                _keyBindings[kv.Key] = (KeyCode)raw;
            }
            else
            {
                _keyBindings[kv.Key] = kv.Value;
            }
        }
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetInt("Settings_ResIdx", ResolutionIndex);
        PlayerPrefs.SetInt("Settings_FullscreenMode", FullscreenModeIndex);
        PlayerPrefs.SetInt("Settings_VSync", VSyncEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Settings_FPSIdx", TargetFPSIndex);
        PlayerPrefs.SetInt("Settings_QualityIdx", QualityLevelIndex);
        PlayerPrefs.SetInt("Settings_ShadowIdx", ShadowQualityIndex);
        PlayerPrefs.SetFloat("Settings_Brightness", Brightness);
        PlayerPrefs.SetFloat("CameraFOV", CameraFOV);

        PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
        PlayerPrefs.SetFloat("Settings_MusicVol", MusicVolume);
        PlayerPrefs.SetFloat("Settings_SFXVol", SFXVolume);
        PlayerPrefs.SetFloat("Settings_VoiceVol", VoiceVolume);
        PlayerPrefs.SetInt("Settings_MuteAll", MuteAll ? 1 : 0);

        PlayerPrefs.SetFloat("MouseSensitivity", MouseSensitivity);
        PlayerPrefs.SetInt("Settings_InvertY", InvertMouseY ? 1 : 0);
        PlayerPrefs.SetInt("Settings_HeadBob", HeadBobEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Settings_CrouchToggle", CrouchToggleMode ? 1 : 0);
        PlayerPrefs.SetInt("Settings_ShowControlsHUD", ShowControlsHUD ? 1 : 0);

        foreach (var kv in _keyBindings)
        {
            PlayerPrefs.SetInt("Keybind_" + kv.Key, (int)kv.Value);
        }

        PlayerPrefs.Save();
    }

    public void ApplyAllSettings()
    {
        // 1. Display / Video
        FullScreenMode fsMode = FullScreenMode.FullScreenWindow;
        if (FullscreenModeIndex == 1) fsMode = FullScreenMode.ExclusiveFullScreen;
        else if (FullscreenModeIndex == 2) fsMode = FullScreenMode.Windowed;

        if (_resolutions.Count > 0)
        {
            int idx = Mathf.Clamp(ResolutionIndex, 0, _resolutions.Count - 1);
            var res = _resolutions[idx];
            if (!Application.isEditor)
            {
                Screen.SetResolution(res.width, res.height, fsMode);
            }
        }

        QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = FpsValues[Mathf.Clamp(TargetFPSIndex, 0, FpsValues.Length - 1)];

        int maxQ = QualitySettings.names != null ? QualitySettings.names.Length - 1 : 0;
        if (maxQ >= 0)
        {
            int mappedQ = Mathf.Clamp(QualityLevelIndex, 0, maxQ);
            QualitySettings.SetQualityLevel(mappedQ, false);
        }

        if (ShadowQualityIndex == 0) QualitySettings.shadows = ShadowQuality.Disable;
        else if (ShadowQualityIndex == 1) QualitySettings.shadows = ShadowQuality.HardOnly;
        else QualitySettings.shadows = ShadowQuality.All;

        // Brightness / Gamma Overlay
        UpdateBrightnessOverlay();

        // 2. Audio
        AudioListener.volume = MuteAll ? 0f : MasterVolume;

        // 3. Player Movement & Camera
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.lookSpeed = MouseSensitivity;
            pm.enableHeadBobbing = HeadBobEnabled;
            pm.normalFOV = CameraFOV;
            if (pm.playerCamera != null && !pm.isDialogueCameraOverride && !pm.isJumpscareCameraOverride)
            {
                pm.playerCamera.fieldOfView = CameraFOV;
            }
        }

        // 4. Equip Controller dropKey sync
        if (InventoryEquipController.Instance != null)
        {
            InventoryEquipController.Instance.dropKey = GetBinding(GameAction.DropItem);
        }

        OnSettingsChanged?.Invoke();
    }

    private void UpdateBrightnessOverlay()
    {
        if (_gammaOverlayImage == null) return;

        // Brightness 1.0 = neutral transparent
        // Brightness > 1.0 = subtle warm lift
        // Brightness < 1.0 = subtle dark tint
        if (Mathf.Abs(Brightness - 1.0f) < 0.02f)
        {
            _gammaOverlayImage.color = Color.clear;
        }
        else if (Brightness > 1.0f)
        {
            float t = (Brightness - 1.0f) / 0.5f; // 0..1
            _gammaOverlayImage.color = new Color(0.88f, 0.88f, 0.94f, Mathf.Clamp(t * 0.14f, 0f, 0.16f));
        }
        else
        {
            float t = (1.0f - Brightness) / 0.5f; // 0..1
            _gammaOverlayImage.color = new Color(0f, 0f, 0f, Mathf.Clamp(t * 0.45f, 0f, 0.50f));
        }
    }

    // ── Open / Close Modal API ───────────────────────────────────────────────

    public static void OpenSettingsModal(Action onClose = null)
    {
        EnsureInstance();
        Instance.ShowModal(onClose);
    }

    public static void CloseSettingsModal()
    {
        if (Instance != null) Instance.HideModal();
    }

    private void ShowModal(Action onClose)
    {
        _onCloseCallback = onClose;
        _isWaitingForKey = false;
        if (_modalRoot == null) BuildSettingsCanvas();

        RefreshAllUIControls();
        SwitchTab(_activeTab);

        _modalRoot.SetActive(true);
        IsSettingsOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideModal()
    {
        _isWaitingForKey = false;
        SaveSettings();
        ApplyAllSettings();

        if (_modalRoot != null) _modalRoot.SetActive(false);
        IsSettingsOpen = false;

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
    }

    // ── Update Loop (Interactive Key Rebinding & ESC Handling) ───────────────

    private void Update()
    {
        if (!IsSettingsOpen) return;

        if (_isWaitingForKey)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelKeyRebind();
                return;
            }

            foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
            {
                if (kcode == KeyCode.None || kcode == KeyCode.Escape || kcode == KeyCode.Mouse0) continue;
                if (Input.GetKeyDown(kcode))
                {
                    CompleteKeyRebind(kcode);
                    return;
                }
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HideModal();
        }
    }

    private void StartKeyRebind(GameAction action)
    {
        _isWaitingForKey = true;
        _rebindingAction = action;

        RefreshKeybindButtons();
        if (_keybindLabelMap.TryGetValue(action, out Text lbl) && lbl != null)
        {
            lbl.text = "PRESS KEY...";
            lbl.color = new Color(0.95f, 0.85f, 0.35f, 1f);
        }
        if (_keybindBgMap.TryGetValue(action, out Image bg) && bg != null)
        {
            bg.color = new Color(0.55f, 0.12f, 0.12f, 0.98f);
        }
    }

    private void CompleteKeyRebind(KeyCode newKey)
    {
        _keyBindings[_rebindingAction] = newKey;
        _isWaitingForKey = false;
        SaveSettings();
        ApplyAllSettings();
        RefreshKeybindButtons();
    }

    private void CancelKeyRebind()
    {
        _isWaitingForKey = false;
        RefreshKeybindButtons();
    }

    public void ResetDefaults()
    {
        FullscreenModeIndex = 0;
        VSyncEnabled = true;
        TargetFPSIndex = 1;
        QualityLevelIndex = 2;
        ShadowQualityIndex = 2;
        Brightness = 1.0f;
        CameraFOV = 60f;

        MasterVolume = 1.0f;
        MusicVolume = 0.80f;
        SFXVolume = 0.90f;
        VoiceVolume = 1.0f;
        MuteAll = false;

        MouseSensitivity = 1.8f;
        InvertMouseY = false;
        HeadBobEnabled = true;
        CrouchToggleMode = false;
        ShowControlsHUD = true;

        _keyBindings.Clear();
        foreach (var kv in DefaultBindings)
            _keyBindings[kv.Key] = kv.Value;

        SaveSettings();
        ApplyAllSettings();
        RefreshAllUIControls();
    }

    // ── Asset Loading ────────────────────────────────────────────────────────

    private void LoadAssets()
    {
        _horrorFont = LoadFont("Main Menu/watch people die", "Assets/Main Menu/watch people die.ttf");
        _typewriterFont = LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");

        _paperBgSprite = LoadSprite("Assets/Textures/UI/Blank_paper.png");
        _bannerPaperSprite = LoadSprite("Assets/Synty/ScrambledPaper_HUD.png");
        _buttonSprite = LoadSprite("Assets/Textures/UI/KeypadButton_Normal.png");
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

    // ── Procedural Paper UI Builder ──────────────────────────────────────────

    private Text _resValText, _fsValText, _vsyncValText, _fpsValText, _qualValText, _shadowValText, _brightValText, _fovValText;
    private Slider _brightSlider, _fovSlider;

    private Text _masterVolText, _musicVolText, _sfxVolText, _voiceVolText, _muteValText;
    private Slider _masterSlider, _musicSlider, _sfxSlider, _voiceSlider;

    private Text _sensValText, _invertYValText, _headBobValText, _crouchModeValText, _hudPromptValText;
    private Slider _sensSlider;

    private void BuildSettingsCanvas()
    {
        if (_canvasGO != null) Destroy(_canvasGO);

        _canvasGO = new GameObject("GameSettings_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasGO.transform.SetParent(transform, false);

        var canvas = _canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 960;

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Subtle Non-Raycast Gamma/Brightness Overlay
        var gammaGO = new GameObject("BrightnessGammaOverlay", typeof(RectTransform), typeof(Image));
        gammaGO.transform.SetParent(_canvasGO.transform, false);
        StretchFull(gammaGO.GetComponent<RectTransform>());
        _gammaOverlayImage = gammaGO.GetComponent<Image>();
        _gammaOverlayImage.color = Color.clear;
        _gammaOverlayImage.raycastTarget = false;

        // Modal Root
        _modalRoot = new GameObject("SettingsModalRoot", typeof(RectTransform), typeof(Image));
        _modalRoot.transform.SetParent(_canvasGO.transform, false);
        StretchFull(_modalRoot.GetComponent<RectTransform>());
        var backdrop = _modalRoot.GetComponent<Image>();
        backdrop.color = new Color(0.01f, 0.01f, 0.02f, 0.86f);
        backdrop.raycastTarget = true;

        // Main Paper Folder Sheet
        var sheetGO = new GameObject("SettingsPaperSheet", typeof(RectTransform), typeof(Image), typeof(Shadow));
        sheetGO.transform.SetParent(_modalRoot.transform, false);
        var sheetRT = sheetGO.GetComponent<RectTransform>();
        sheetRT.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRT.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRT.pivot = new Vector2(0.5f, 0.5f);
        sheetRT.sizeDelta = new Vector2(1280f, 840f);

        var sheetImg = sheetGO.GetComponent<Image>();
        if (_paperBgSprite != null) sheetImg.sprite = _paperBgSprite;
        sheetImg.color = new Color(0.95f, 0.91f, 0.83f, 0.99f);

        var shadow = sheetGO.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(10f, -12f);

        // Decorative Stamp Watermark
        if (_stampSprite != null)
        {
            var wmGO = new GameObject("WatermarkStamp", typeof(RectTransform), typeof(Image));
            wmGO.transform.SetParent(sheetGO.transform, false);
            var wmRT = wmGO.GetComponent<RectTransform>();
            wmRT.anchorMin = new Vector2(0.76f, 0.14f);
            wmRT.anchorMax = new Vector2(0.94f, 0.34f);
            wmRT.sizeDelta = Vector2.zero;
            wmRT.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var wmImg = wmGO.GetComponent<Image>();
            wmImg.sprite = _stampSprite;
            wmImg.preserveAspect = true;
            wmImg.color = new Color(0.68f, 0.14f, 0.14f, 0.11f);
            wmImg.raycastTarget = false;
        }

        // Top Header Title & Subtitle
        var headerTitle = MakeText("HeaderTitle", sheetGO.transform, "SYSTEM SETTINGS // KONFIGURASYON",
            _horrorFont, 34, new Color(0.66f, 0.06f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetAnchors(headerTitle.rectTransform, new Vector2(0.08f, 0.88f), new Vector2(0.74f, 0.95f));

        var headerSub = MakeText("HeaderSub", sheetGO.transform, "DISPLAY • VIDEO • AUDIO • CONTROLS • KEYMAPPING",
            _typewriterFont, 14, new Color(0.22f, 0.17f, 0.13f, 0.90f), TextAnchor.MiddleLeft);
        SetAnchors(headerSub.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.74f, 0.88f));

        // Top-Right Close [X] Button
        CreateStyledButton(sheetGO.transform, "BtnCloseTop", "X  CLOSE [ESC]",
            new Vector2(0.78f, 0.86f), new Vector2(0.93f, 0.94f),
            new Color(0.58f, 0.10f, 0.10f, 0.96f), new Color(0.98f, 0.95f, 0.88f, 1f), 16,
            () => HideModal());

        // 4 Category Tab Buttons (Display/Video, Audio, Controls, Keymapping)
        string[] tabNames =
        {
            "1. DISPLAY & VIDEO",
            "2. AUDIO & SOUND",
            "3. CONTROLS",
            "4. KEYMAPPING"
        };

        for (int i = 0; i < 4; i++)
        {
            int tabIdx = i;
            float xMin = 0.08f + i * 0.215f;
            float xMax = xMin + 0.205f;
            var btn = CreateStyledButton(sheetGO.transform, $"TabBtn_{i}", tabNames[i],
                new Vector2(xMin, 0.76f), new Vector2(xMax, 0.83f),
                new Color(0.18f, 0.14f, 0.11f, 0.95f), new Color(0.95f, 0.91f, 0.84f, 1f), 16,
                () => SwitchTab(tabIdx));
            _tabButtonBgs[i] = btn.GetComponent<Image>();
            _tabButtonTexts[i] = btn.GetComponentInChildren<Text>();
        }

        // Divider Line under tabs
        var divGO = new GameObject("TabDivider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(sheetGO.transform, false);
        SetAnchors(divGO.GetComponent<RectTransform>(), new Vector2(0.08f, 0.745f), new Vector2(0.93f, 0.749f));
        divGO.GetComponent<Image>().color = new Color(0.45f, 0.12f, 0.12f, 0.65f);

        // Build the 4 Tab Content Pages
        for (int i = 0; i < 4; i++)
        {
            var pageGO = new GameObject($"TabPage_{i}", typeof(RectTransform));
            pageGO.transform.SetParent(sheetGO.transform, false);
            SetAnchors(pageGO.GetComponent<RectTransform>(), new Vector2(0.08f, 0.15f), new Vector2(0.93f, 0.73f));
            _tabPages[i] = pageGO;
        }

        BuildDisplayVideoTab(_tabPages[0].transform);
        BuildAudioTab(_tabPages[1].transform);
        BuildControlsTab(_tabPages[2].transform);
        BuildKeymappingTab(_tabPages[3].transform);

        // Bottom Action Bar (Reset Defaults + Apply & Save)
        CreateStyledButton(sheetGO.transform, "BtnResetDefaults", "RESET TO DEFAULTS",
            new Vector2(0.08f, 0.055f), new Vector2(0.32f, 0.125f),
            new Color(0.28f, 0.22f, 0.18f, 0.95f), new Color(0.96f, 0.92f, 0.84f, 1f), 16,
            () => ResetDefaults());

        CreateStyledButton(sheetGO.transform, "BtnApplyClose", "APPLY & RETURN [ESC]",
            new Vector2(0.66f, 0.055f), new Vector2(0.93f, 0.125f),
            new Color(0.62f, 0.09f, 0.09f, 0.98f), new Color(0.98f, 0.95f, 0.88f, 1f), 18,
            () => HideModal());

        _modalRoot.SetActive(false);
    }

    private void SwitchTab(int index)
    {
        _activeTab = Mathf.Clamp(index, 0, 3);
        _isWaitingForKey = false;

        for (int i = 0; i < 4; i++)
        {
            bool active = (i == _activeTab);
            if (_tabPages[i] != null) _tabPages[i].SetActive(active);
            if (_tabButtonBgs[i] != null)
            {
                _tabButtonBgs[i].color = active
                    ? new Color(0.64f, 0.09f, 0.09f, 0.98f)
                    : new Color(0.18f, 0.14f, 0.11f, 0.92f);
            }
            if (_tabButtonTexts[i] != null)
            {
                _tabButtonTexts[i].color = active
                    ? new Color(1f, 0.96f, 0.88f, 1f)
                    : new Color(0.84f, 0.79f, 0.72f, 0.95f);
            }
        }

        RefreshAllUIControls();
    }

    // ── TAB 0: DISPLAY & VIDEO ───────────────────────────────────────────────

    private void BuildDisplayVideoTab(Transform parent)
    {
        // 8 rows neatly arranged
        CreateCycleRow(parent, 0, "SCREEN RESOLUTION",
            () => {
                if (_resolutions.Count == 0) return;
                ResolutionIndex = (ResolutionIndex - 1 + _resolutions.Count) % _resolutions.Count;
                SaveAndApplyUI();
            },
            () => {
                if (_resolutions.Count == 0) return;
                ResolutionIndex = (ResolutionIndex + 1) % _resolutions.Count;
                SaveAndApplyUI();
            },
            out _resValText);

        CreateCycleRow(parent, 1, "DISPLAY MODE",
            () => { FullscreenModeIndex = (FullscreenModeIndex + 2) % 3; SaveAndApplyUI(); },
            () => { FullscreenModeIndex = (FullscreenModeIndex + 1) % 3; SaveAndApplyUI(); },
            out _fsValText);

        CreateCycleRow(parent, 2, "VERTICAL SYNC (V-SYNC)",
            () => { VSyncEnabled = !VSyncEnabled; SaveAndApplyUI(); },
            () => { VSyncEnabled = !VSyncEnabled; SaveAndApplyUI(); },
            out _vsyncValText);

        CreateCycleRow(parent, 3, "FRAME RATE LIMIT (FPS)",
            () => { TargetFPSIndex = (TargetFPSIndex - 1 + FpsLabels.Length) % FpsLabels.Length; SaveAndApplyUI(); },
            () => { TargetFPSIndex = (TargetFPSIndex + 1) % FpsLabels.Length; SaveAndApplyUI(); },
            out _fpsValText);

        CreateCycleRow(parent, 4, "GRAPHICS QUALITY PRESET",
            () => { QualityLevelIndex = (QualityLevelIndex + 3) % 4; SaveAndApplyUI(); },
            () => { QualityLevelIndex = (QualityLevelIndex + 1) % 4; SaveAndApplyUI(); },
            out _qualValText);

        CreateCycleRow(parent, 5, "REALTIME SHADOWS",
            () => { ShadowQualityIndex = (ShadowQualityIndex + 2) % 3; SaveAndApplyUI(); },
            () => { ShadowQualityIndex = (ShadowQualityIndex + 1) % 3; SaveAndApplyUI(); },
            out _shadowValText);

        CreateSliderRow(parent, 6, "BRIGHTNESS / GAMMA", 0.5f, 1.5f, Brightness,
            val => { Brightness = val; SaveAndApplyUI(); },
            out _brightSlider, out _brightValText);

        CreateSliderRow(parent, 7, "CAMERA FIELD OF VIEW (FOV)", 50f, 105f, CameraFOV,
            val => { CameraFOV = val; SaveAndApplyUI(); },
            out _fovSlider, out _fovValText);
    }

    // ── TAB 1: AUDIO ─────────────────────────────────────────────────────────

    private void BuildAudioTab(Transform parent)
    {
        CreateSliderRow(parent, 0, "MASTER VOLUME", 0f, 1f, MasterVolume,
            val => { MasterVolume = val; SaveAndApplyUI(); },
            out _masterSlider, out _masterVolText);

        CreateSliderRow(parent, 1, "MUSIC / BGM VOLUME", 0f, 1f, MusicVolume,
            val => { MusicVolume = val; SaveAndApplyUI(); },
            out _musicSlider, out _musicVolText);

        CreateSliderRow(parent, 2, "SFX & AMBIENCE VOLUME", 0f, 1f, SFXVolume,
            val => { SFXVolume = val; SaveAndApplyUI(); },
            out _sfxSlider, out _sfxVolText);

        CreateSliderRow(parent, 3, "VOICE & CUTSCENE VOLUME", 0f, 1f, VoiceVolume,
            val => { VoiceVolume = val; SaveAndApplyUI(); },
            out _voiceSlider, out _voiceVolText);

        CreateCycleRow(parent, 4, "MUTE ALL AUDIO",
            () => { MuteAll = !MuteAll; SaveAndApplyUI(); },
            () => { MuteAll = !MuteAll; SaveAndApplyUI(); },
            out _muteValText);
    }

    // ── TAB 2: CONTROLS ──────────────────────────────────────────────────────

    private void BuildControlsTab(Transform parent)
    {
        CreateSliderRow(parent, 0, "MOUSE LOOK SENSITIVITY", 0.2f, 5.0f, MouseSensitivity,
            val => { MouseSensitivity = val; SaveAndApplyUI(); },
            out _sensSlider, out _sensValText);

        CreateCycleRow(parent, 1, "INVERT MOUSE Y-AXIS",
            () => { InvertMouseY = !InvertMouseY; SaveAndApplyUI(); },
            () => { InvertMouseY = !InvertMouseY; SaveAndApplyUI(); },
            out _invertYValText);

        CreateCycleRow(parent, 2, "CAMERA HEAD-BOBBING",
            () => { HeadBobEnabled = !HeadBobEnabled; SaveAndApplyUI(); },
            () => { HeadBobEnabled = !HeadBobEnabled; SaveAndApplyUI(); },
            out _headBobValText);

        CreateCycleRow(parent, 3, "CROUCH INPUT MODE",
            () => { CrouchToggleMode = !CrouchToggleMode; SaveAndApplyUI(); },
            () => { CrouchToggleMode = !CrouchToggleMode; SaveAndApplyUI(); },
            out _crouchModeValText);

        CreateCycleRow(parent, 4, "SHOW ON-SCREEN ITEM CONTROLS HUD",
            () => { ShowControlsHUD = !ShowControlsHUD; SaveAndApplyUI(); },
            () => { ShowControlsHUD = !ShowControlsHUD; SaveAndApplyUI(); },
            out _hudPromptValText);
    }

    // ── TAB 3: KEYMAPPING ────────────────────────────────────────────────────

    private void BuildKeymappingTab(Transform parent)
    {
        var hint = MakeText("KeybindHint", parent,
            "CLICK ANY ACTION BELOW TO REBIND ITS KEY  •  PRESS [ESC] TO CANCEL REBINDING",
            _typewriterFont, 14, new Color(0.55f, 0.10f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(hint.rectTransform, new Vector2(0.0f, 0.91f), new Vector2(1.0f, 0.99f));

        (GameAction action, string label)[] entries =
        {
            (GameAction.MoveForward,  "MOVE FORWARD"),
            (GameAction.MoveBackward, "MOVE BACKWARD"),
            (GameAction.MoveLeft,     "STRAFE LEFT"),
            (GameAction.MoveRight,    "STRAFE RIGHT"),
            (GameAction.Sprint,       "SPRINT / RUN"),
            (GameAction.Crouch,       "CROUCH / STEALTH"),
            (GameAction.Interact,     "INTERACT / PICK UP"),
            (GameAction.Flashlight,   "TOGGLE FLASHLIGHT"),
            (GameAction.DropItem,     "DROP HELD ITEM"),
            (GameAction.InspectItem,  "INSPECT ITEM"),
            (GameAction.TutorialHelp, "OPEN TUTORIAL / HELP")
        };

        _keybindLabelMap.Clear();
        _keybindBgMap.Clear();

        for (int i = 0; i < entries.Length; i++)
        {
            int col = i / 6; // 0 = left column, 1 = right column
            int row = i % 6;

            float xMin = col == 0 ? 0.01f : 0.515f;
            float xMax = col == 0 ? 0.485f : 0.99f;
            float yTop = 0.87f - row * 0.142f;
            float yBot = yTop - 0.118f;

            var rowGO = new GameObject($"KeybindRow_{entries[i].action}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(parent, false);
            SetAnchors(rowGO.GetComponent<RectTransform>(), new Vector2(xMin, yBot), new Vector2(xMax, yTop));
            rowGO.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.08f, 0.12f);

            var actionLbl = MakeText("ActionLabel", rowGO.transform, entries[i].label,
                _typewriterFont, 15, new Color(0.12f, 0.09f, 0.07f, 0.98f), TextAnchor.MiddleLeft);
            SetAnchors(actionLbl.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.60f, 1f));

            GameAction captured = entries[i].action;
            var keyBtn = CreateStyledButton(rowGO.transform, $"BtnKey_{captured}",
                FormatKeyName(GetBinding(captured)),
                new Vector2(0.62f, 0.12f), new Vector2(0.97f, 0.88f),
                new Color(0.18f, 0.14f, 0.11f, 0.96f), new Color(0.98f, 0.93f, 0.82f, 1f), 15,
                () => StartKeyRebind(captured));

            _keybindBgMap[captured] = keyBtn.GetComponent<Image>();
            _keybindLabelMap[captured] = keyBtn.GetComponentInChildren<Text>();
        }
    }

    // ── Row & Widget Helpers ─────────────────────────────────────────────────

    private void CreateCycleRow(Transform parent, int rowIndex, string label, Action onPrev, Action onNext, out Text valueText)
    {
        float yTop = 0.96f - rowIndex * 0.118f;
        float yBot = yTop - 0.102f;

        var rowGO = new GameObject($"Row_{label}", typeof(RectTransform), typeof(Image));
        rowGO.transform.SetParent(parent, false);
        SetAnchors(rowGO.GetComponent<RectTransform>(), new Vector2(0.01f, yBot), new Vector2(0.99f, yTop));
        rowGO.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.08f, (rowIndex % 2 == 0) ? 0.10f : 0.05f);

        var lbl = MakeText("Label", rowGO.transform, label, _typewriterFont, 16,
            new Color(0.10f, 0.08f, 0.06f, 0.98f), TextAnchor.MiddleLeft);
        SetAnchors(lbl.rectTransform, new Vector2(0.025f, 0f), new Vector2(0.50f, 1f));

        CreateStyledButton(rowGO.transform, "BtnPrev", "<",
            new Vector2(0.52f, 0.12f), new Vector2(0.58f, 0.88f),
            new Color(0.20f, 0.15f, 0.12f, 0.95f), new Color(0.96f, 0.92f, 0.84f, 1f), 18, onPrev);

        valueText = MakeText("ValueText", rowGO.transform, "", _typewriterFont, 16,
            new Color(0.62f, 0.08f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(valueText.rectTransform, new Vector2(0.59f, 0f), new Vector2(0.91f, 1f));

        CreateStyledButton(rowGO.transform, "BtnNext", ">",
            new Vector2(0.92f, 0.12f), new Vector2(0.98f, 0.88f),
            new Color(0.20f, 0.15f, 0.12f, 0.95f), new Color(0.96f, 0.92f, 0.84f, 1f), 18, onNext);
    }

    private void CreateSliderRow(Transform parent, int rowIndex, string label, float min, float max, float initial,
        Action<float> onChanged, out Slider slider, out Text valueText)
    {
        float yTop = 0.96f - rowIndex * 0.118f;
        float yBot = yTop - 0.102f;

        var rowGO = new GameObject($"SliderRow_{label}", typeof(RectTransform), typeof(Image));
        rowGO.transform.SetParent(parent, false);
        SetAnchors(rowGO.GetComponent<RectTransform>(), new Vector2(0.01f, yBot), new Vector2(0.99f, yTop));
        rowGO.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.08f, (rowIndex % 2 == 0) ? 0.10f : 0.05f);

        var lbl = MakeText("Label", rowGO.transform, label, _typewriterFont, 16,
            new Color(0.10f, 0.08f, 0.06f, 0.98f), TextAnchor.MiddleLeft);
        SetAnchors(lbl.rectTransform, new Vector2(0.025f, 0f), new Vector2(0.50f, 1f));

        var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(rowGO.transform, false);
        SetAnchors(sliderGO.GetComponent<RectTransform>(), new Vector2(0.52f, 0.22f), new Vector2(0.85f, 0.78f));

        slider = sliderGO.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = initial;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderGO.transform, false);
        SetAnchors(bg.GetComponent<RectTransform>(), new Vector2(0f, 0.28f), new Vector2(1f, 0.72f));
        bg.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.15f, 0.95f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        SetAnchors(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.28f), new Vector2(1f, 0.72f));

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        StretchFull(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = new Color(0.68f, 0.12f, 0.12f, 1f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        StretchFull(handleArea.GetComponent<RectTransform>());

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hrt = handle.GetComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(18f, 0f);
        var hImg = handle.GetComponent<Image>();
        hImg.color = new Color(0.96f, 0.92f, 0.84f, 1f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = hrt;
        slider.targetGraphic = hImg;
        slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

        valueText = MakeText("ValueText", rowGO.transform, "", _typewriterFont, 16,
            new Color(0.62f, 0.08f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetAnchors(valueText.rectTransform, new Vector2(0.86f, 0f), new Vector2(0.98f, 1f));
    }

    private void SaveAndApplyUI()
    {
        SaveSettings();
        ApplyAllSettings();
        RefreshAllUIControls();
    }

    private void RefreshAllUIControls()
    {
        if (_resValText != null && _resolutions.Count > 0)
            _resValText.text = _resolutions[Mathf.Clamp(ResolutionIndex, 0, _resolutions.Count - 1)].label;
        if (_fsValText != null)
            _fsValText.text = FullscreenLabels[Mathf.Clamp(FullscreenModeIndex, 0, FullscreenLabels.Length - 1)];
        if (_vsyncValText != null)
            _vsyncValText.text = VSyncEnabled ? "ENABLED (ON)" : "DISABLED (OFF)";
        if (_fpsValText != null)
            _fpsValText.text = FpsLabels[Mathf.Clamp(TargetFPSIndex, 0, FpsLabels.Length - 1)];
        if (_qualValText != null)
            _qualValText.text = QualityLabels[Mathf.Clamp(QualityLevelIndex, 0, QualityLabels.Length - 1)];
        if (_shadowValText != null)
            _shadowValText.text = ShadowLabels[Mathf.Clamp(ShadowQualityIndex, 0, ShadowLabels.Length - 1)];

        if (_brightSlider != null) _brightSlider.SetValueWithoutNotify(Brightness);
        if (_brightValText != null) _brightValText.text = Mathf.RoundToInt(Brightness * 100f) + "%";

        if (_fovSlider != null) _fovSlider.SetValueWithoutNotify(CameraFOV);
        if (_fovValText != null) _fovValText.text = Mathf.RoundToInt(CameraFOV) + "°";

        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(MasterVolume);
        if (_masterVolText != null) _masterVolText.text = Mathf.RoundToInt(MasterVolume * 100f) + "%";

        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(MusicVolume);
        if (_musicVolText != null) _musicVolText.text = Mathf.RoundToInt(MusicVolume * 100f) + "%";

        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(SFXVolume);
        if (_sfxVolText != null) _sfxVolText.text = Mathf.RoundToInt(SFXVolume * 100f) + "%";

        if (_voiceSlider != null) _voiceSlider.SetValueWithoutNotify(VoiceVolume);
        if (_voiceVolText != null) _voiceVolText.text = Mathf.RoundToInt(VoiceVolume * 100f) + "%";

        if (_muteValText != null) _muteValText.text = MuteAll ? "MUTED (ON)" : "UNMUTED (OFF)";

        if (_sensSlider != null) _sensSlider.SetValueWithoutNotify(MouseSensitivity);
        if (_sensValText != null) _sensValText.text = MouseSensitivity.ToString("F1");

        if (_invertYValText != null) _invertYValText.text = InvertMouseY ? "INVERTED (ON)" : "NORMAL (OFF)";
        if (_headBobValText != null) _headBobValText.text = HeadBobEnabled ? "ENABLED (ON)" : "DISABLED (OFF)";
        if (_crouchModeValText != null) _crouchModeValText.text = CrouchToggleMode ? "TOGGLE [PRESS]" : "HOLD [DEFAULT]";
        if (_hudPromptValText != null) _hudPromptValText.text = ShowControlsHUD ? "VISIBLE (ON)" : "HIDDEN (OFF)";

        RefreshKeybindButtons();
    }

    private void RefreshKeybindButtons()
    {
        foreach (var kv in _keybindLabelMap)
        {
            if (kv.Value != null)
            {
                kv.Value.text = FormatKeyName(GetBinding(kv.Key));
                kv.Value.color = new Color(0.98f, 0.93f, 0.82f, 1f);
            }
        }
        foreach (var kv in _keybindBgMap)
        {
            if (kv.Value != null)
            {
                kv.Value.color = new Color(0.18f, 0.14f, 0.11f, 0.96f);
            }
        }
    }

    private Button CreateStyledButton(Transform parent, string name, string label,
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

        var txt = MakeText("Text", go.transform, label, _typewriterFont, fontSize, textColor, TextAnchor.MiddleCenter);
        txt.raycastTarget = false;

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
