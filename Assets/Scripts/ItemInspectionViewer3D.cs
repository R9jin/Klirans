using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ItemInspectionViewer3D — Resident Evil-style 3D Item Inspection System for KLIRANS.
///
/// Features:
/// - Isolated 3D inspection studio with dedicated camera and 3-point cinematic lighting (Key, Fill, Cyan Rim).
/// - High-resolution RenderTexture displayed in fullscreen horror UI.
/// - Free 360-degree rotation (Pitch, Yaw, Roll) via Left Mouse Drag with smooth momentum damping.
/// - Smooth zoom in/out via Mouse Scroll Wheel with clamped view distance.
/// - Reset orientation and zoom via [R] key.
/// - Dynamic angle-based clue detection: reveals secret notes, engraved codes, and hidden details
///   when the player rotates the item to face the inspection camera.
/// - Interactive clue examination via [SPACE] or click, triggering audio cues and updating ObjectiveHUD.
/// - Full Taglish lore, item category badges, and retro survival horror typography.
/// </summary>
public class ItemInspectionViewer3D : MonoBehaviour
{
    private static ItemInspectionViewer3D _instance;
    public static ItemInspectionViewer3D Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ItemInspectionViewer3D>();
                if (_instance == null)
                {
                    var go = new GameObject("ItemInspectionViewer3D_Controller");
                    _instance = go.AddComponent<ItemInspectionViewer3D>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (_instance == null)
        {
            var inst = Instance;
        }
    }

    public static bool IsOpen => _instance != null && _instance._isOpen;

    // ── Inspection Studio Objects ────────────────────────────────────────────
    private GameObject _studioRoot;
    private Camera _studioCamera;
    private Light _keyLight;
    private Light _fillLight;
    private Light _rimLight;
    private Transform _modelPivot;
    private RenderTexture _renderTexture;
    private GameObject _currentInspectedInstance;

    // ── UI Elements ──────────────────────────────────────────────────────────
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RawImage _viewportImage;
    private Text _itemNameText;
    private Text _itemCategoryText;
    private Text _itemDescriptionText;
    private GameObject _cluePromptRoot;
    private Text _cluePromptText;
    private GameObject _clueCardRoot;
    private Text _clueCardTitleText;
    private Text _clueCardDescText;
    private Text _controlsLegendText;
    private AudioSource _audioSource;

    // ── Audio Clips ──────────────────────────────────────────────────────────
    private AudioClip _discoveryChimeAudio;
    private AudioClip _paperRustleAudio;

    // ── Rotation & Zoom State ────────────────────────────────────────────────
    private bool _isOpen = false;
    private InventoryItem _activeItem;
    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _currentRotation = Quaternion.identity;
    private Vector2 _rotationVelocity = Vector2.zero;
    private float _targetDistance = 1.45f;
    private float _currentDistance = 1.45f;
    private float _defaultDistance = 1.45f;
    private const float MinDistance = 0.75f;
    private const float MaxDistance = 2.6f;
    private const float RotateSensitivity = 3.6f;
    private const float DampingRate = 14f;

    // ── Clue Detection State ─────────────────────────────────────────────────
    private List<InspectableClueMarker> _activeClueMarkers = new List<InspectableClueMarker>();
    private InspectableClueMarker _currentlyFacingClue = null;
    private int _openedFrame = -1;
    private bool _wasInventoryOpenBefore = false;

    // Active clue discovery state
    public static readonly HashSet<string> DiscoveredClueIDs = new HashSet<string>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAudioResources();
        BuildStudio();
        BuildUI();
        SetVisible(false, instant: true);
    }

    private void LoadAudioResources()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;

#if UNITY_EDITOR
        _discoveryChimeAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
        _paperRustleAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/printer_operation.wav");
#endif
        if (_discoveryChimeAudio == null)
            _discoveryChimeAudio = Resources.Load<AudioClip>("Sounds/objective_chime");
    }

    // ── Studio Setup ─────────────────────────────────────────────────────────

    private void BuildStudio()
    {
        // Position studio far off-screen so scene geometry and lights never interfere
        Vector3 studioPos = new Vector3(2500f, 2500f, 2500f);

        _studioRoot = new GameObject("__ItemInspectionStudio");
        _studioRoot.transform.position = studioPos;
        DontDestroyOnLoad(_studioRoot);

        // 1. Studio Camera
        var camGO = new GameObject("InspectionCamera");
        camGO.transform.SetParent(_studioRoot.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.1f, -_defaultDistance);
        camGO.transform.localRotation = Quaternion.identity;

        _studioCamera = camGO.AddComponent<Camera>();
        _studioCamera.clearFlags = CameraClearFlags.SolidColor;
        _studioCamera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f); // Deep survival horror slate
        _studioCamera.fieldOfView = 38f;
        _studioCamera.nearClipPlane = 0.05f;
        _studioCamera.farClipPlane = 30f;
        _studioCamera.depth = 100;
        _studioCamera.enabled = false; // Only render when inspecting

        // Create high-res 1920x1080 RenderTexture
        _renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear,
            name = "ItemInspectionRT"
        };
        _studioCamera.targetTexture = _renderTexture;

        // 2. Key Light (Soft Warm Directional from Top-Right Front)
        var keyGO = new GameObject("Studio_KeyLight");
        keyGO.transform.SetParent(_studioRoot.transform, false);
        keyGO.transform.localRotation = Quaternion.Euler(32f, -38f, 0f);
        _keyLight = keyGO.AddComponent<Light>();
        _keyLight.type = LightType.Directional;
        _keyLight.color = new Color(1.0f, 0.96f, 0.90f);
        _keyLight.intensity = 1.35f;

        // 3. Fill Light (Cool Directional from Bottom-Left Front)
        var fillGO = new GameObject("Studio_FillLight");
        fillGO.transform.SetParent(_studioRoot.transform, false);
        fillGO.transform.localRotation = Quaternion.Euler(-20f, 42f, 0f);
        _fillLight = fillGO.AddComponent<Light>();
        _fillLight.type = LightType.Directional;
        _fillLight.color = new Color(0.62f, 0.72f, 0.88f);
        _fillLight.intensity = 0.55f;

        // 4. Rim Light (Crisp Cyan-White Directional from Behind)
        var rimGO = new GameObject("Studio_RimLight");
        rimGO.transform.SetParent(_studioRoot.transform, false);
        rimGO.transform.localRotation = Quaternion.Euler(-145f, 25f, 0f);
        _rimLight = rimGO.AddComponent<Light>();
        _rimLight.type = LightType.Directional;
        _rimLight.color = new Color(0.65f, 0.85f, 1.0f);
        _rimLight.intensity = 1.15f;

        // 5. Model Pivot
        var pivotGO = new GameObject("ModelPivot");
        pivotGO.transform.SetParent(_studioRoot.transform, false);
        pivotGO.transform.localPosition = Vector3.zero;
        pivotGO.transform.localRotation = Quaternion.identity;
        _modelPivot = pivotGO.transform;
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        var hudCanvas = GameObject.Find("HudCanvas");
        Transform parentTransform = hudCanvas != null ? hudCanvas.transform : transform;

        // Overlay Root Canvas
        var canvasGO = new GameObject("ItemInspectionViewer3D_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(parentTransform, false);
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 250; // Above inventory and HUD

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = canvasGO.AddComponent<CanvasGroup>();

        // Fonts
        Font typewriterFont = LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");
        Font horrorFont = LoadFont("Fonts/watch people die", "Assets/Main Menu/watch people die.ttf") ?? typewriterFont;

        // 1. Fullscreen Backdrop Image (Dark Tint)
        var bgGO = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.03f, 0.92f);

        // 2. Viewport RawImage (Displays RenderTexture)
        var viewportGO = new GameObject("ViewportImage", typeof(RectTransform), typeof(RawImage));
        viewportGO.transform.SetParent(canvasGO.transform, false);
        var vpRT = viewportGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.sizeDelta = Vector2.zero;
        _viewportImage = viewportGO.GetComponent<RawImage>();
        _viewportImage.texture = _renderTexture;

        // 3. Vignette Overlay (Dark edges)
        CreateVignetteOverlay(canvasGO.transform);

        // 4. Header (Top-Left: Category Pill & Item Title)
        var headerGO = new GameObject("HeaderPanel", typeof(RectTransform));
        headerGO.transform.SetParent(canvasGO.transform, false);
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(0f, 1f);
        headerRT.pivot = new Vector2(0f, 1f);
        headerRT.anchoredPosition = new Vector2(60f, -50f);
        headerRT.sizeDelta = new Vector2(600f, 130f);

        _itemCategoryText = CreateText(headerGO.transform, "CategoryText", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.85f, 0.72f, 0.35f, 0.95f), typewriterFont, new Vector2(0f, 0.75f), new Vector2(1f, 1f));

        _itemNameText = CreateText(headerGO.transform, "ItemNameText", 34, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.96f, 0.94f, 0.90f, 1f), horrorFont, new Vector2(0f, 0.1f), new Vector2(1f, 0.75f));

        // Decorative Divider Line
        var dividerGO = new GameObject("DividerLine", typeof(RectTransform), typeof(Image));
        dividerGO.transform.SetParent(headerGO.transform, false);
        var divRT = dividerGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 0f);
        divRT.anchorMax = new Vector2(0.9f, 0f);
        divRT.sizeDelta = new Vector2(0f, 2f);
        dividerGO.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 0.45f);

        // 5. Lore & Description Box (Bottom-Left)
        var descPanelGO = new GameObject("DescriptionPanel", typeof(RectTransform), typeof(Image));
        descPanelGO.transform.SetParent(canvasGO.transform, false);
        var descRT = descPanelGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(0f, 0f);
        descRT.pivot = new Vector2(0f, 0f);
        descRT.anchoredPosition = new Vector2(60f, 100f);
        descRT.sizeDelta = new Vector2(540f, 220f);
        descPanelGO.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.85f);

        // Thin Border around Description
        CreateOutline(descPanelGO, new Color(0.35f, 0.38f, 0.45f, 0.5f));

        _itemDescriptionText = CreateText(descPanelGO.transform, "DescriptionText", 17, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.88f, 0.86f, 0.82f, 0.95f), typewriterFont, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.94f));

        // 6. Interactive Clue Prompt (Pulsing Gold Prompt at Center-Right)
        _cluePromptRoot = new GameObject("CluePromptBanner", typeof(RectTransform), typeof(Image));
        _cluePromptRoot.transform.SetParent(canvasGO.transform, false);
        var cpRT = _cluePromptRoot.GetComponent<RectTransform>();
        cpRT.anchorMin = new Vector2(0.5f, 0.25f);
        cpRT.anchorMax = new Vector2(0.5f, 0.25f);
        cpRT.pivot = new Vector2(0.5f, 0.5f);
        cpRT.sizeDelta = new Vector2(560f, 52f);
        _cluePromptRoot.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.02f, 0.92f);
        CreateOutline(_cluePromptRoot, new Color(1.0f, 0.84f, 0.2f, 0.85f));

        _cluePromptText = CreateText(_cluePromptRoot.transform, "PromptText", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(1.0f, 0.88f, 0.35f, 1f), typewriterFont, Vector2.zero, Vector2.one);
        _cluePromptText.text = "★ [SPACE] SURIIN ANG NAKATAGONG DETALYE";

        // Add clickable button to the clue prompt
        var clueBtn = _cluePromptRoot.AddComponent<Button>();
        clueBtn.transition = Selectable.Transition.None;
        clueBtn.onClick.AddListener(ExamineCurrentClue);

        _cluePromptRoot.SetActive(false);

        // 7. Discovered Clue Card (Revealed Secret Modal)
        BuildClueDiscoveryCard(canvasGO.transform, typewriterFont, horrorFont);

        // 8. Bottom Controls Legend Bar
        var controlsGO = new GameObject("ControlsBar", typeof(RectTransform));
        controlsGO.transform.SetParent(canvasGO.transform, false);
        var ctrlRT = controlsGO.GetComponent<RectTransform>();
        ctrlRT.anchorMin = new Vector2(0f, 0f);
        ctrlRT.anchorMax = new Vector2(1f, 0f);
        ctrlRT.pivot = new Vector2(0.5f, 0f);
        ctrlRT.anchoredPosition = new Vector2(0f, 25f);
        ctrlRT.sizeDelta = new Vector2(0f, 40f);

        _controlsLegendText = CreateText(controlsGO.transform, "ControlsLegend", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.72f, 0.74f, 0.78f, 0.85f), typewriterFont, Vector2.zero, Vector2.one);
        _controlsLegendText.text = "[LMB DRAG] Ikot (Rotate)     [SCROLL] Zoom     [R] I-reset     [SPACE] Suriin Clue     [ESC / TAB] Isara";
    }

    private void BuildClueDiscoveryCard(Transform parent, Font typewriterFont, Font horrorFont)
    {
        _clueCardRoot = new GameObject("ClueCardModal", typeof(RectTransform), typeof(Image));
        _clueCardRoot.transform.SetParent(parent, false);
        var cardRT = _clueCardRoot.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.68f, 0.5f);
        cardRT.anchorMax = new Vector2(0.68f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(520f, 320f);
        _clueCardRoot.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.04f, 0.95f);
        CreateOutline(_clueCardRoot, new Color(0.92f, 0.78f, 0.35f, 0.9f));

        // Header Tag
        var headerTag = CreateText(_clueCardRoot.transform, "ClueTag", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.85f, 0.72f, 0.25f, 1f), typewriterFont, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.96f));
        headerTag.text = "★ NATUKLASANG CLUE / SIKRETO";

        // Title
        _clueCardTitleText = CreateText(_clueCardRoot.transform, "ClueTitle", 22, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(1f, 0.95f, 0.88f, 1f), horrorFont, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.85f));

        // Description / Decoded Text
        _clueCardDescText = CreateText(_clueCardRoot.transform, "ClueDesc", 16, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.9f, 0.88f, 0.82f, 0.95f), typewriterFont, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.68f));

        // Close Hint
        var hint = CreateText(_clueCardRoot.transform, "ClueHint", 13, FontStyle.Italic, TextAnchor.MiddleRight,
            new Color(0.65f, 0.65f, 0.7f, 0.75f), typewriterFont, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.14f));
        hint.text = "I-rotate ang gamit o pindutin ang [SPACE] upang itago";

        _clueCardRoot.SetActive(false);
    }

    private void CreateVignetteOverlay(Transform parent)
    {
        var vigGO = new GameObject("VignetteOverlay", typeof(RectTransform), typeof(Image));
        vigGO.transform.SetParent(parent, false);
        var vigRT = vigGO.GetComponent<RectTransform>();
        vigRT.anchorMin = Vector2.zero;
        vigRT.anchorMax = Vector2.one;
        vigRT.sizeDelta = Vector2.zero;
        var vigImg = vigGO.GetComponent<Image>();
        vigImg.raycastTarget = false;

        // Subtle gradient border texture
        Texture2D vigTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float u = (x / 31f) * 2f - 1f;
                float v = (y / 31f) * 2f - 1f;
                float dist = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.Clamp01((dist - 0.5f) / 0.7f) * 0.75f;
                vigTex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        vigTex.Apply();
        vigImg.sprite = Sprite.Create(vigTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }

    private void CreateOutline(GameObject target, Color color)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private Text CreateText(Transform parent, string goName, int fontSize, FontStyle fontStyle,
        TextAnchor alignment, Color color, Font font, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var txt = go.GetComponent<Text>();
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.alignment = alignment;
        txt.color = color;
        txt.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        return txt;
    }

    private Font LoadFont(string resourcePath, string editorAssetPath)
    {
        Font font = Resources.Load<Font>(resourcePath);
#if UNITY_EDITOR
        if (font == null) font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);
#endif
        return font;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the 3D inspection studio for the given InventoryItem.
    /// </summary>
    public static void Inspect(InventoryItem item)
    {
        if (item == null) return;
        Instance.OpenInspection(item);
    }

    public void OpenInspection(InventoryItem item)
    {
        if (item == null) return;
        _activeItem = item;
        _openedFrame = Time.frameCount;
        _wasInventoryOpenBefore = (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen);

        // Unlock cursor for free drag
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Freeze player movement/mouse look
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.SetControlsEnabled(false);

        // Populate UI Info
        string categoryName = GetCategoryName(item);
        if (_itemCategoryText != null) _itemCategoryText.text = $"[ {categoryName.ToUpper()} ]";
        if (_itemNameText != null) _itemNameText.text = item.itemName.ToUpper();
        if (_itemDescriptionText != null) _itemDescriptionText.text = GetInspectionLore(item);

        // Hide any previously open clue cards
        if (_clueCardRoot != null) _clueCardRoot.SetActive(false);
        if (_cluePromptRoot != null) _cluePromptRoot.SetActive(false);
        _currentlyFacingClue = null;

        // Instantiate and setup 3D model in studio
        SetupModelInStudio(item);

        // Reset camera and rotation
        _targetRotation = Quaternion.identity;
        _currentRotation = Quaternion.identity;
        _rotationVelocity = Vector2.zero;
        _targetDistance = _defaultDistance;
        _currentDistance = _defaultDistance;
        if (_studioCamera != null)
        {
            _studioCamera.transform.localPosition = new Vector3(0f, 0.05f, -_currentDistance);
            _studioCamera.enabled = true;
        }

        PlayPaperSound();
        SetVisible(true);
        _isOpen = true;

        Debug.Log($"[ItemInspectionViewer3D] Inspecting item in 3D: {item.itemName}");
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        PlayPaperSound();
        SetVisible(false);

        if (_studioCamera != null)
            _studioCamera.enabled = false;

        // Clean up spawned inspected model
        if (_currentInspectedInstance != null)
        {
            Destroy(_currentInspectedInstance);
            _currentInspectedInstance = null;
        }
        _activeClueMarkers.Clear();
        _currentlyFacingClue = null;

        // Restore player movement
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null && !PauseMenu.GameIsPaused && !LockerHideManager.IsPlayerHidden)
        {
            pm.SetControlsEnabled(true);
        }

        // Restore cursor state
        bool isInventoryCurrentlyOpen = (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen);
        if (!isInventoryCurrentlyOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ── Model Spawning & Normalization ───────────────────────────────────────

    private void SetupModelInStudio(InventoryItem item)
    {
        if (_currentInspectedInstance != null)
        {
            Destroy(_currentInspectedInstance);
            _currentInspectedInstance = null;
        }
        _activeClueMarkers.Clear();

        GameObject prefab = item.itemPrefab;

        // Fallback: search for prefab by naming convention if unlinked
        if (prefab == null)
        {
            prefab = FindFallbackPrefab(item);
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[ItemInspectionViewer3D] No 3D prefab found for item: {item.itemName}. Creating placeholder box.");
            _currentInspectedInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _currentInspectedInstance.transform.SetParent(_modelPivot, false);
            return;
        }

        // Instantiate copy inside model pivot
        _currentInspectedInstance = Instantiate(prefab, _modelPivot);
        _currentInspectedInstance.transform.localPosition = Vector3.zero;
        _currentInspectedInstance.transform.localRotation = Quaternion.identity;

        // Strip colliders, rigidbodies, and lights from inspect copy
        foreach (var col in _currentInspectedInstance.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (var rb in _currentInspectedInstance.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
        foreach (var l in _currentInspectedInstance.GetComponentsInChildren<Light>(true))
            Destroy(l);

        // Collect all InspectableClueMarker components
        _activeClueMarkers.AddRange(_currentInspectedInstance.GetComponentsInChildren<InspectableClueMarker>(true));

        // Center visual bounds and normalize scale so all items fit the frame cleanly
        NormalizeModelBounds(_currentInspectedInstance);
    }

    private GameObject FindFallbackPrefab(InventoryItem item)
    {
        string safeName = item.itemName.Replace(" ", "").Replace("_", "");
        string[] candidates = {
            $"Assets/Prefabs/ItemModels/Model_{safeName}.prefab",
            $"Assets/Prefabs/ItemModels/Model_{item.itemName.Replace(' ', '_')}.prefab",
            $"Assets/Prefabs/ItemModels/Model_{item.name}.prefab"
        };
#if UNITY_EDITOR
        foreach (var path in candidates)
        {
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null) return p;
        }
#endif
        return null;
    }

    private void NormalizeModelBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Offset to center
        Vector3 centerOffset = root.transform.position - bounds.center;
        root.transform.position += centerOffset;

        // Normalize scale to fit ~0.8 units
        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDim > 0.001f)
        {
            float targetSize = 0.85f;
            float scaleFactor = targetSize / maxDim;
            root.transform.localScale = Vector3.one * scaleFactor;
        }
    }

    // ── Input & Inspection Loop ──────────────────────────────────────────────

    private void Update()
    {
        if (!_isOpen) return;

        // Ignore close keys on open frame
        if (Time.frameCount == _openedFrame) return;

        HandleInput();
        UpdateRotationAndZoom();
        UpdateClueDetection();
    }

    private void HandleInput()
    {
        // Close on Esc, Tab, E, or Right Click
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1))
        {
            Close();
            return;
        }

        // Reset orientation on R
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetOrientation();
        }

        // Examine clue on Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_currentlyFacingClue != null)
            {
                ExamineCurrentClue();
            }
            else if (_clueCardRoot != null && _clueCardRoot.activeSelf)
            {
                _clueCardRoot.SetActive(false);
            }
        }

        // Drag Rotation with Left Mouse Button
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * RotateSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * RotateSensitivity;

            _rotationVelocity = new Vector2(mouseX, mouseY);

            ApplyRotation(mouseX, mouseY);
        }
        else
        {
            // Apply momentum decay when released
            if (_rotationVelocity.sqrMagnitude > 0.001f)
            {
                ApplyRotation(_rotationVelocity.x, _rotationVelocity.y);
                _rotationVelocity = Vector2.Lerp(_rotationVelocity, Vector2.zero, Time.unscaledDeltaTime * 6f);
            }
        }

        // Zoom with Scroll Wheel
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetDistance = Mathf.Clamp(_targetDistance - scroll * 0.18f, MinDistance, MaxDistance);
        }
    }

    private void ApplyRotation(float dx, float dy)
    {
        if (_studioCamera == null) return;

        // Rotate around Camera Up and Right vectors
        Quaternion pitch = Quaternion.AngleAxis(-dy, _studioCamera.transform.right);
        Quaternion yaw = Quaternion.AngleAxis(-dx, _studioCamera.transform.up);

        _targetRotation = yaw * pitch * _targetRotation;
    }

    private void UpdateRotationAndZoom()
    {
        // Smoothly interpolate rotation
        _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, Time.unscaledDeltaTime * DampingRate);
        if (_modelPivot != null)
        {
            _modelPivot.localRotation = _currentRotation;
        }

        // Smoothly interpolate zoom distance
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.unscaledDeltaTime * 12f);
        if (_studioCamera != null)
        {
            _studioCamera.transform.localPosition = new Vector3(0f, 0.05f, -_currentDistance);
        }
    }

    private void ResetOrientation()
    {
        _targetRotation = Quaternion.identity;
        _targetDistance = _defaultDistance;
        _rotationVelocity = Vector2.zero;
        if (_clueCardRoot != null) _clueCardRoot.SetActive(false);
    }

    // ── Clue Angle Detection ─────────────────────────────────────────────────

    private void UpdateClueDetection()
    {
        if (_activeClueMarkers.Count == 0 || _studioCamera == null || _modelPivot == null)
        {
            if (_cluePromptRoot != null && _cluePromptRoot.activeSelf)
                _cluePromptRoot.SetActive(false);
            _currentlyFacingClue = null;
            return;
        }

        InspectableClueMarker bestFacingClue = null;

        foreach (var clue in _activeClueMarkers)
        {
            if (clue == null) continue;

            if (clue.IsFacingCamera(_studioCamera.transform, _modelPivot))
            {
                bestFacingClue = clue;
                break;
            }
        }

        _currentlyFacingClue = bestFacingClue;

        if (_currentlyFacingClue != null)
        {
            if (_cluePromptRoot != null && !_cluePromptRoot.activeSelf)
            {
                _cluePromptRoot.SetActive(true);
                if (_cluePromptText != null)
                {
                    _cluePromptText.text = $"★ [SPACE] SURIIN ANG CLUE: {_currentlyFacingClue.clueTitle.ToUpper()}";
                }
            }

            // Pulsing opacity effect
            if (_cluePromptRoot != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                var img = _cluePromptRoot.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    c.a = pulse;
                    img.color = c;
                }
            }
        }
        else
        {
            if (_cluePromptRoot != null && _cluePromptRoot.activeSelf)
            {
                _cluePromptRoot.SetActive(false);
            }
        }
    }

    private void ExamineCurrentClue()
    {
        if (_currentlyFacingClue == null) return;

        // Mark clue discovered
        _currentlyFacingClue.Discover();
        DiscoveredClueIDs.Add(_currentlyFacingClue.clueID);

        // Play chime sound
        PlayDiscoverySound();

        // Show discovered clue card modal
        if (_clueCardRoot != null)
        {
            _clueCardRoot.SetActive(true);
            if (_clueCardTitleText != null) _clueCardTitleText.text = _currentlyFacingClue.clueTitle;
            if (_clueCardDescText != null) _clueCardDescText.text = _currentlyFacingClue.clueDescription;
        }

        // Trigger puzzle & objective updates
        UpdateObjectiveForClue(_currentlyFacingClue);
    }

    private void UpdateObjectiveForClue(InspectableClueMarker clue)
    {
        if (ObjectiveHUD.Instance == null) return;

        switch (clue.clueID)
        {
            case "LockerKeyCode":
                ObjectiveHUD.Instance.SetObjective(
                    "Records Locker Code (CAB-04 // 7-3-2)",
                    "Na-decode ang code sa key tag: Gamitin ang 7-3-2 sa 2nd Floor Faculty Records Locker!"
                );
                break;

            case "ExecutiveSealCode":
                ObjectiveHUD.Instance.SetObjective(
                    "Executive Seal Na-decode (EVP-AUTH-1994)",
                    "Ang official seal para sa Room 103 Executive Vice President clearance endorsement."
                );
                break;

            case "ComLabStickyNote":
                ObjectiveHUD.Instance.SetObjective(
                    "ComLab In-Charge Note",
                    "Ibalik ang monitor cable sa Server Room bago pirmahan ni Sir ang ComLab clearance."
                );
                break;

            case "HandbookNotes":
                ObjectiveHUD.Instance.SetObjective(
                    "Survival Babala: Diliman Statue",
                    "'Huwag tatalikuran ang estatwa sa Diliman Wing. Titigan mo habang umaatras!'"
                );
                break;

            case "FlashlightSerial":
                ObjectiveHUD.Instance.SetObjective(
                    "Security Guard Flashlight (SG-2)",
                    "Serial SG-2 DELA CRUZ — pag-aari ng security guard sa lobby entrance."
                );
                break;

            case "ClearanceSlipWatermark":
                ObjectiveHUD.Instance.SetObjective(
                    "6 Clearance Signature Departments",
                    "Kumpletuhin ang 6 na selyo: Library, Guidance, ComLab, Clinic, Cashier, at EVP."
                );
                break;

            case "VGACableSerial":
                ObjectiveHUD.Instance.SetObjective(
                    "ComLab Cable Serial (CCS-SRV-TERMINAL-01)",
                    "Ikabit ang VGA cable sa Master Terminal sa Room 105 para ma-clear ang terminal hold!"
                );
                break;

            case "CeramicFuseRating":
                ObjectiveHUD.Instance.SetObjective(
                    "15A Fuse Rating Na-decode",
                    "Ilagay ang 15A fuse sa Circuit Slot 2 ng Main Breaker Box para ibalik ang kuryente sa ComLab!"
                );
                break;

            case "CassetteSafeCode":
                ObjectiveHUD.Instance.SetObjective(
                    "Guidance Safe Combination (4-1-9)",
                    "Gamitin ang code 4-1-9 sa Wall Safe sa likod ng certificate frame sa Room 207!"
                );
                break;

            case "GuardLogbookCode":
                ObjectiveHUD.Instance.SetObjective(
                    "Emergency Lobby Override (9-4-2-1)",
                    "Lihim na keypad code para buksan ang magnetic lockdown relay sa main entrance exit!"
                );
                break;

            case "ClinicMedCabinetCode":
                ObjectiveHUD.Instance.SetObjective(
                    "Clinic Dispensary Code (CAB-MED: 9-0-4)",
                    "Gamitin ang 9-0-4 para buksan ang medicine cabinet sa clinic para sa first aid."
                );
                break;

            case "PesoCoinInfo":
                ObjectiveHUD.Instance.SetObjective(
                    "1-Piso Barya (BSP 1995)",
                    "Gamitin sa Cafeteria Vending Machine o Emergency Payphone para kumuha ng supplies."
                );
                break;
        }
    }

    // ── Helper Lore & Descriptions ───────────────────────────────────────────

    private string GetCategoryName(InventoryItem item)
    {
        switch (item.itemType)
        {
            case InventoryItem.ItemType.KeyItem: return "Key Item";
            case InventoryItem.ItemType.Document: return "Official Document";
            case InventoryItem.ItemType.Consumable: return "Survival Consumable";
            case InventoryItem.ItemType.Tool: return "Survival Tool";
            default: return "Inventory Item";
        }
    }

    private string GetInspectionLore(InventoryItem item)
    {
        string baseDesc = !string.IsNullOrEmpty(item.description) ? item.description : "";

        // Taglish horror flavor and inspect hints for each item
        switch (item.itemName)
        {
            case "Locker Key":
                return "Mabigat na brass key na may nakakabit na luma at naninilaw na ivory acrylic tag. " +
                       "May napansin kang manipis na ukit ng numero sa likod ng tag na parang sinadyang itago mula sa ibang estudyante.\n\n" +
                       "<color=#D4AF37>Tip: I-rotate ang likod ng key tag upang suriin ang code.</color>";

            case "Official University Seal Stamp":
            case "Executive Seal Stamp":
                return "Mabigat na stamp na gawa sa solidong mahogany wood at pinakinis na brass collar. " +
                       "Sa ilalim nito ay nakadikit ang makapal na vulcanized rubber die na may opisyal na seal ng Executive Vice President.\n\n" +
                       "<color=#D4AF37>Tip: Tingnan ang ilalim ng stamp para sa authorization code.</color>";

            case "ComLab Clearance Form":
                return "Isang wooden clipboard na may hawak na official accountability sheet ng Computer Laboratory. " +
                       "May nakadikit na naninilaw na sticky note sa likod ng board na may sulat-kamay ng ComLab student assistant.\n\n" +
                       "<color=#D4AF37>Tip: Silipin ang likod ng clipboard para mabasa ang sticky note.</color>";

            case "Library Voucher":
                return "Perforated dot-matrix paper voucher na inilabas ng Lumang Library Archive system noong 1994. " +
                       "May purple stamp sa likod na nagbabala ukol sa mga estudyanteng hindi nakatapos ng clearance bago mag-dilim.";

            case "Blank Paper":
            case "Clearance Slip":
                return "Ang kinatatakutang opisyal na Clearance Form ng Pamantasan. " +
                       "Naglalaman ng anim na bakanteng kahon para sa pirma ng Library, Guidance, ComLab, Clinic, Cashier, at EVP.";

            case "Ham & Cheese Sandwich":
                return "Toasted sandwich mula sa lumang cafeteria na binalot sa mantikang wax paper. " +
                       "Mabango pa rin at malambot ang tinapay. Makatutulong ito upang pakalmahin ang matinding kaba at anxiety ng estudyante.";

            case "Flashlight":
                return "Heavy-duty anodized aluminum tactical flashlight na may knurled grip. " +
                       "May nakagasgas na serial number sa takip ng baterya sa ilalim nito na nagpapatunay kung sino ang may-ari nito.\n\n" +
                       "<color=#D4AF37>Tip: I-rotate ang tailcap sa ilalim upang makita ang serial.</color>";

            case "Student Handbook":
                return "Navy blue leatherette bound handbook na may kupas na gintong selyo ng unibersidad. " +
                       "Sa pinakahuling pahina at sa likod ng pabalat, may nakasulat na desperadong babala ukol sa mga kababalaghan sa campus.";

            case "Printer Paper":
                return "Isang buong kaha (ream) ng 500-sheet Substance 20 bond paper. " +
                       "Naka-seal sa orihinal na asul na wrapper. Kailangan ito ng mga opisina para makapag-print ng mga opisyal na form.";

            case "Lockpin":
                return "Improbisadong lockpick na ginawa mula sa matigas na bobby pin. " +
                       "Binaluktot ang dulo upang magsilbing tension tool para sa mga lumang drawer at padlock.";

            case "VGA Monitor Cable":
                return "Makapal na asul na video display cable na may 15-pin D-sub connectors at dalawang thumbscrews. " +
                       "Kailangan ito para magka-signal ang offline computer database terminal sa ComLab.\n\n" +
                       "<color=#D4AF37>Tip: I-rotate ang connector boot upang suriin ang serial code.</color>";

            case "Ceramic Cartridge Fuse 15A":
                return "Mabigat na ceramic cartridge fuse na may makintab na brass contact caps. " +
                       "Ginagamit sa distribution breaker panel upang maibalik ang kuryente sa ComLab at Faculty offices.\n\n" +
                       "<color=#D4AF37>Tip: Suriin ang gitna ng ceramic body para sa amperage rating.</color>";

            case "Counseling Audio Cassette Tape":
                return "Lumang microcassette tape mula sa guidance records na may markang 'SESSION #04 - ANXIETY'. " +
                       "May nakasulat na sikretong impormasyon sa likod ng tape label.\n\n" +
                       "<color=#D4AF37>Tip: Tumingin sa Side B ng cassette para sa nakatagong safe code.</color>";

            case "Philippine 1-Peso Coin":
                return "Makintab na 1-piso Pilipino coin (BSP serye na may mukha ni José Rizal). " +
                       "Pwedeng ihulog sa vintage vending machine sa cafeteria o emergency hallway payphone.";

            case "Security Guard Incident Logbook":
                return "Opisyal na itim na logbook ni Guard Dela Cruz na may ginintuang security star badge. " +
                       "Naglalaman ng emergency lockdown override protocol para sa main entrance.\n\n" +
                       "<color=#D4AF37>Tip: Buklatin ang likod ng pabalat para sa keypad code.</color>";

            case "Clinic Medical Record Slip":
                return "Opisyal na clinical record mula sa University Clinic. " +
                       "May nakatala ritong blood pressure reading at code para sa emergency medicine dispensary cabinet.\n\n" +
                       "<color=#D4AF37>Tip: Silipin ang ibabang bahagi ng chart para sa cabinet code.</color>";

            default:
                if (item.itemName.StartsWith("Fragment"))
                {
                    return "Punit na piraso ng opisyal na student clearance slip. " +
                           "May mga jagged edges at marks ng ballpen sa likod na tumutukoy kung saan ito dapat itugma sa ibang piraso.";
                }
                return baseDesc;
        }
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
        if (_canvas != null)
        {
            _canvas.enabled = visible;
        }
    }

    private void PlayPaperSound()
    {
        if (_audioSource != null && _paperRustleAudio != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(_paperRustleAudio, 0.45f);
        }
    }

    private void PlayDiscoverySound()
    {
        if (_audioSource != null && _discoveryChimeAudio != null)
        {
            _audioSource.pitch = 1.0f;
            _audioSource.PlayOneShot(_discoveryChimeAudio, 0.85f);
        }
    }
}
