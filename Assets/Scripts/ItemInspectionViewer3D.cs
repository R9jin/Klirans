using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ItemInspectionViewer3D — Resident Evil Remake-style 3D Item Inspection System for KLIRANS.
///
/// Features:
/// - Isolated 3D inspection studio on dedicated layer (Layer 30) with 5-point studio lighting
///   and per-instance PBR studio material boost so inspected items are brightly and clearly lit
///   like Resident Evil remakes while having ZERO light bleed into the dark horror environment.
/// - Root ScreenSpaceOverlay Canvas (sortingOrder 600) so it always renders cleanly above
///   InventoryCanvas, SlotCanvas, and HudCanvas without sub-canvas clipping.
/// - Responsive 360-degree click-and-drag rotation (LMB or RMB drag + WASD/Arrow keys)
///   with smooth momentum damping.
/// - Physical 3D clue hotspot markers attached to the clue face of every inspected model so
///   players can visually spot and rotate toward hidden clues, then press [SPACE], [E], or Click
///   to examine ("Suriin") the clue.
/// - Anti-spam debounced soft tactile audio (replaces the loud printer sound and prevents [V] spam).
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
            var _ = Instance;
        }
    }

    public static bool IsOpen => _instance != null && _instance._isOpen;

    // Dedicated layer so studio lights & camera ONLY affect the inspected 3D object
    private const int InspectionStudioLayer = 30;

    // ── Inspection Studio Objects ────────────────────────────────────────────
    private GameObject _studioRoot;
    private Camera _studioCamera;
    private Light _frontCamLight;
    private Light _keyLight;
    private Light _fillLight;
    private Light _underLight;
    private Light _rimLight;
    private Transform _modelPivot;
    private RenderTexture _renderTexture;
    private GameObject _currentInspectedInstance;
    private readonly List<Material> _instantiatedStudioMaterials = new List<Material>();
    private readonly List<Transform> _clueHotspotRings = new List<Transform>();

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

    // ── Audio Clips & Anti-Spam Timers ───────────────────────────────────────
    private AudioClip _discoveryChimeAudio;
    private AudioClip _softHandleAudio;
    private float _lastToggleTime = -10f;
    private float _lastSoundTime = -10f;

    // ── Rotation, Drag & Zoom State ──────────────────────────────────────────
    private bool _isOpen = false;
    private InventoryItem _activeItem;
    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _currentRotation = Quaternion.identity;
    private Vector2 _rotationVelocity = Vector2.zero;
    private float _targetDistance = 1.38f;
    private float _currentDistance = 1.38f;
    private float _defaultDistance = 1.38f;
    private const float MinDistance = 0.68f;
    private const float MaxDistance = 2.50f;
    private const float PixelDragSensitivity = 0.42f;
    private const float AxisDragSensitivity = 3.8f;
    private const float KeyboardRotateSpeed = 115f;
    private const float DampingRate = 16f;

    private bool _isDragging = false;
    private Vector3 _lastMousePos;
    private float _currentDragDistance = 0f;

    // ── Clue Detection State ─────────────────────────────────────────────────
    private readonly List<InspectableClueMarker> _activeClueMarkers = new List<InspectableClueMarker>();
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
        _audioSource.ignoreListenerPause = true;

#if UNITY_EDITOR
        _discoveryChimeAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
        if (_discoveryChimeAudio == null)
            _discoveryChimeAudio = Resources.Load<AudioClip>("Sounds/objective_chime");

        // Create a subtle, non-intrusive tactile handling sound (replaces loud printer_operation.wav)
        _softHandleAudio = CreateSoftInspectRustleClip();
    }

    /// <summary>
    /// Synthesizes a short (0.14s), quiet tactile cloth/paper handling sound so pressing [V]
    /// never plays the loud mechanical printer sound.
    /// </summary>
    private static AudioClip CreateSoftInspectRustleClip()
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * 0.14f);
        float[] samples = new float[sampleCount];
        System.Random rng = new System.Random(1994);

        float prev = 0f;
        float prev2 = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            // Smooth attack and decay envelope
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Pow(1f - t, 1.3f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            // Low-pass filter for warm foley cloth/paper turn
            prev = Mathf.Lerp(prev, white, 0.14f);
            prev2 = Mathf.Lerp(prev2, prev, 0.22f);
            samples[i] = prev2 * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("SoftInspectHandle", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ── Studio Setup (Isolated Layer + 5-Point Resident Evil Remake Lighting) ──

    private void BuildStudio()
    {
        // Position studio far off-screen AND isolate on InspectionStudioLayer (Layer 30)
        Vector3 studioPos = new Vector3(2500f, 2500f, 2500f);
        int studioLayerMask = 1 << InspectionStudioLayer;

        _studioRoot = new GameObject("__ItemInspectionStudio");
        _studioRoot.layer = InspectionStudioLayer;
        _studioRoot.transform.position = studioPos;
        DontDestroyOnLoad(_studioRoot);

        // 1. Studio Camera
        var camGO = new GameObject("InspectionCamera");
        camGO.layer = InspectionStudioLayer;
        camGO.transform.SetParent(_studioRoot.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.04f, -_defaultDistance);
        camGO.transform.localRotation = Quaternion.identity;

        _studioCamera = camGO.AddComponent<Camera>();
        _studioCamera.clearFlags = CameraClearFlags.SolidColor;
        _studioCamera.backgroundColor = new Color(0.04f, 0.045f, 0.06f, 1f); // Clean Resident Evil dark slate
        _studioCamera.cullingMask = studioLayerMask; // ONLY render InspectionStudioLayer
        _studioCamera.fieldOfView = 36f;
        _studioCamera.nearClipPlane = 0.03f;
        _studioCamera.farClipPlane = 15f;
        _studioCamera.depth = 100;
        _studioCamera.allowHDR = true;
        _studioCamera.enabled = false;

        // Create high-res 1920x1080 RenderTexture
        _renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear,
            name = "ItemInspectionRT"
        };
        _studioCamera.targetTexture = _renderTexture;

        // 2. Camera-Mounted Front Inspection Light (Illuminates whichever surface faces the player!)
        var frontLightGO = new GameObject("Studio_FrontCamLight");
        frontLightGO.layer = InspectionStudioLayer;
        frontLightGO.transform.SetParent(camGO.transform, false);
        frontLightGO.transform.localPosition = new Vector3(0f, 0.12f, 0.15f);
        _frontCamLight = frontLightGO.AddComponent<Light>();
        _frontCamLight.type = LightType.Point;
        _frontCamLight.range = 6.5f;
        _frontCamLight.color = new Color(1.0f, 0.99f, 0.96f);
        _frontCamLight.intensity = 16.0f;
        _frontCamLight.shadows = LightShadows.None;
        _frontCamLight.cullingMask = studioLayerMask;
        _frontCamLight.enabled = false;

        // 3. Key Light (Bright Warm Studio Key Light from Top-Right Front)
        var keyGO = new GameObject("Studio_KeyLight");
        keyGO.layer = InspectionStudioLayer;
        keyGO.transform.SetParent(_studioRoot.transform, false);
        keyGO.transform.localPosition = new Vector3(0.85f, 0.95f, -0.95f);
        _keyLight = keyGO.AddComponent<Light>();
        _keyLight.type = LightType.Point;
        _keyLight.range = 7.0f;
        _keyLight.color = new Color(1.0f, 0.97f, 0.92f);
        _keyLight.intensity = 20.0f;
        _keyLight.shadows = LightShadows.None;
        _keyLight.cullingMask = studioLayerMask;
        _keyLight.enabled = false;

        // 4. Fill Light (Soft Studio Fill from Left-Front)
        var fillGO = new GameObject("Studio_FillLight");
        fillGO.layer = InspectionStudioLayer;
        fillGO.transform.SetParent(_studioRoot.transform, false);
        fillGO.transform.localPosition = new Vector3(-0.95f, 0.35f, -0.85f);
        _fillLight = fillGO.AddComponent<Light>();
        _fillLight.type = LightType.Point;
        _fillLight.range = 7.0f;
        _fillLight.color = new Color(0.88f, 0.94f, 1.0f);
        _fillLight.intensity = 14.0f;
        _fillLight.shadows = LightShadows.None;
        _fillLight.cullingMask = studioLayerMask;
        _fillLight.enabled = false;

        // 5. Under-Fill Uplight (Illuminates bottom/underside details like Flashlight tailcap & Stamp base)
        var underGO = new GameObject("Studio_UnderLight");
        underGO.layer = InspectionStudioLayer;
        underGO.transform.SetParent(_studioRoot.transform, false);
        underGO.transform.localPosition = new Vector3(0.0f, -0.95f, -0.75f);
        _underLight = underGO.AddComponent<Light>();
        _underLight.type = LightType.Point;
        _underLight.range = 7.0f;
        _underLight.color = new Color(0.96f, 0.96f, 0.98f);
        _underLight.intensity = 12.0f;
        _underLight.shadows = LightShadows.None;
        _underLight.cullingMask = studioLayerMask;
        _underLight.enabled = false;

        // 6. Rim Light (Crisp RE-style edge separation light behind object)
        var rimGO = new GameObject("Studio_RimLight");
        rimGO.layer = InspectionStudioLayer;
        rimGO.transform.SetParent(_studioRoot.transform, false);
        rimGO.transform.localPosition = new Vector3(0.25f, 0.75f, 0.95f);
        _rimLight = rimGO.AddComponent<Light>();
        _rimLight.type = LightType.Point;
        _rimLight.range = 7.0f;
        _rimLight.color = new Color(0.80f, 0.90f, 1.0f);
        _rimLight.intensity = 15.0f;
        _rimLight.shadows = LightShadows.None;
        _rimLight.cullingMask = studioLayerMask;
        _rimLight.enabled = false;

        // 7. Model Pivot
        var pivotGO = new GameObject("ModelPivot");
        pivotGO.layer = InspectionStudioLayer;
        pivotGO.transform.SetParent(_studioRoot.transform, false);
        pivotGO.transform.localPosition = Vector3.zero;
        pivotGO.transform.localRotation = Quaternion.identity;
        _modelPivot = pivotGO.transform;
    }

    private void SetStudioLightsEnabled(bool enabled)
    {
        if (_frontCamLight != null) _frontCamLight.enabled = enabled;
        if (_keyLight != null) _keyLight.enabled = enabled;
        if (_fillLight != null) _fillLight.enabled = enabled;
        if (_underLight != null) _underLight.enabled = enabled;
        if (_rimLight != null) _rimLight.enabled = enabled;

        // Ensure main camera never renders the isolated studio layer
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.cullingMask &= ~(1 << InspectionStudioLayer);
        }
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        if (_canvas != null) return;

        // Root ScreenSpaceOverlay Canvas parented directly to this DontDestroyOnLoad controller
        // (Never parent under HudCanvas, which would make it a 100x100 sub-canvas!)
        var canvasGO = new GameObject("ItemInspectionViewer3D_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 600; // Always above InventoryCanvas (5), SlotCanvas (10), and HudCanvas (0)

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = canvasGO.AddComponent<CanvasGroup>();

        // Fonts
        Font typewriterFont = LoadFont("Fonts/Lucida_Typewriter_Bold", "Assets/Fonts/Lucida_Typewriter_Bold.ttf") ?? LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");
        Font horrorFont = LoadFont("Fonts/watch people die", "Assets/Main Menu/watch people die.ttf") ?? typewriterFont;

        // 1. Fullscreen Backdrop Image (Dark Tint)
        var bgGO = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.015f, 0.02f, 0.03f, 0.94f);
        bgImg.raycastTarget = true;

        // 2. Viewport RawImage (Displays RenderTexture of the well-lit 3D item)
        var viewportGO = new GameObject("ViewportImage", typeof(RectTransform), typeof(RawImage));
        viewportGO.transform.SetParent(canvasGO.transform, false);
        var vpRT = viewportGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.sizeDelta = Vector2.zero;
        _viewportImage = viewportGO.GetComponent<RawImage>();
        _viewportImage.texture = _renderTexture;
        _viewportImage.raycastTarget = false;

        // 3. Vignette Overlay (Dark edges)
        CreateVignetteOverlay(canvasGO.transform);

        // 4. Header (Top-Left: Category Pill & Item Title)
        var headerGO = new GameObject("HeaderPanel", typeof(RectTransform));
        headerGO.transform.SetParent(canvasGO.transform, false);
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(0f, 1f);
        headerRT.pivot = new Vector2(0f, 1f);
        headerRT.anchoredPosition = new Vector2(60f, -45f);
        headerRT.sizeDelta = new Vector2(680f, 130f);

        _itemCategoryText = CreateText(headerGO.transform, "CategoryText", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.88f, 0.75f, 0.38f, 0.95f), typewriterFont, new Vector2(0f, 0.75f), new Vector2(1f, 1f));

        _itemNameText = CreateText(headerGO.transform, "ItemNameText", 34, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.96f, 0.94f, 0.90f, 1f), horrorFont, new Vector2(0f, 0.1f), new Vector2(1f, 0.75f));

        // Decorative Divider Line
        var dividerGO = new GameObject("DividerLine", typeof(RectTransform), typeof(Image));
        dividerGO.transform.SetParent(headerGO.transform, false);
        var divRT = dividerGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 0f);
        divRT.anchorMax = new Vector2(0.9f, 0f);
        divRT.sizeDelta = new Vector2(0f, 2f);
        var divImg = dividerGO.GetComponent<Image>();
        divImg.color = new Color(0.85f, 0.72f, 0.35f, 0.45f);
        divImg.raycastTarget = false;

        // Top-Right Close Button [X] ISARA (ESC / V)
        var closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        closeBtnGO.transform.SetParent(canvasGO.transform, false);
        var closeRT = closeBtnGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-60f, -48f);
        closeRT.sizeDelta = new Vector2(210f, 44f);
        closeBtnGO.GetComponent<Image>().color = new Color(0.16f, 0.08f, 0.08f, 0.92f);
        CreateOutline(closeBtnGO, new Color(0.85f, 0.35f, 0.35f, 0.75f));
        var closeBtn = closeBtnGO.GetComponent<Button>();
        closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        closeBtn.onClick.AddListener(Close);
        var closeTxt = CreateText(closeBtnGO.transform, "CloseBtnText", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.96f, 0.90f, 0.88f, 1f), typewriterFont, Vector2.zero, Vector2.one);
        closeTxt.text = "[X] ISARA (ESC / V)";

        // 5. Lore & Description Box (Bottom-Left)
        var descPanelGO = new GameObject("DescriptionPanel", typeof(RectTransform), typeof(Image));
        descPanelGO.transform.SetParent(canvasGO.transform, false);
        var descRT = descPanelGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(0f, 0f);
        descRT.pivot = new Vector2(0f, 0f);
        descRT.anchoredPosition = new Vector2(60f, 95f);
        descRT.sizeDelta = new Vector2(560f, 235f);
        var descImg = descPanelGO.GetComponent<Image>();
        descImg.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);
        descImg.raycastTarget = false;

        CreateOutline(descPanelGO, new Color(0.35f, 0.38f, 0.45f, 0.5f));

        _itemDescriptionText = CreateText(descPanelGO.transform, "DescriptionText", 17, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.90f, 0.88f, 0.84f, 0.96f), typewriterFont, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.94f));
        _itemDescriptionText.supportRichText = true;

        // 6. Interactive Clue Prompt (Pulsing Gold Prompt at Bottom-Center)
        _cluePromptRoot = new GameObject("CluePromptBanner", typeof(RectTransform), typeof(Image));
        _cluePromptRoot.transform.SetParent(canvasGO.transform, false);
        var cpRT = _cluePromptRoot.GetComponent<RectTransform>();
        cpRT.anchorMin = new Vector2(0.5f, 0.16f);
        cpRT.anchorMax = new Vector2(0.5f, 0.16f);
        cpRT.pivot = new Vector2(0.5f, 0.5f);
        cpRT.sizeDelta = new Vector2(620f, 56f);
        _cluePromptRoot.GetComponent<Image>().color = new Color(0.14f, 0.10f, 0.02f, 0.95f);
        CreateOutline(_cluePromptRoot, new Color(1.0f, 0.84f, 0.2f, 0.9f));

        _cluePromptText = CreateText(_cluePromptRoot.transform, "PromptText", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(1.0f, 0.90f, 0.35f, 1f), typewriterFont, Vector2.zero, Vector2.one);
        _cluePromptText.text = "★ [SPACE / E / CLICK] SURIIN ANG NAKATAGONG CLUE";

        var clueBtn = _cluePromptRoot.AddComponent<Button>();
        clueBtn.transition = Selectable.Transition.None;
        clueBtn.navigation = new Navigation { mode = Navigation.Mode.None };
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
        ctrlRT.anchoredPosition = new Vector2(0f, 24f);
        ctrlRT.sizeDelta = new Vector2(0f, 40f);

        _controlsLegendText = CreateText(controlsGO.transform, "ControlsLegend", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.78f, 0.80f, 0.84f, 0.90f), typewriterFont, Vector2.zero, Vector2.one);
        _controlsLegendText.text = "[CLICK & DRAG / WASD] Ikot (Rotate 360°)     [SCROLL] Zoom     [R] I-reset     [SPACE / E] Suriin Clue     [ESC / V] Isara";
    }

    private void BuildClueDiscoveryCard(Transform parent, Font typewriterFont, Font horrorFont)
    {
        _clueCardRoot = new GameObject("ClueCardModal", typeof(RectTransform), typeof(Image));
        _clueCardRoot.transform.SetParent(parent, false);
        var cardRT = _clueCardRoot.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.72f, 0.54f);
        cardRT.anchorMax = new Vector2(0.72f, 0.54f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(520f, 320f);
        var cardImg = _clueCardRoot.GetComponent<Image>();
        cardImg.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);
        cardImg.raycastTarget = false;
        CreateOutline(_clueCardRoot, new Color(0.92f, 0.78f, 0.35f, 0.9f));

        // Header Tag
        var headerTag = CreateText(_clueCardRoot.transform, "ClueTag", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.95f, 0.80f, 0.28f, 1f), typewriterFont, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.96f));
        headerTag.text = "★ NATUKLASANG CLUE / SIKRETO SA GAMIT";

        // Title
        _clueCardTitleText = CreateText(_clueCardRoot.transform, "ClueTitle", 22, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(1f, 0.95f, 0.88f, 1f), horrorFont, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.85f));

        // Description / Decoded Text
        _clueCardDescText = CreateText(_clueCardRoot.transform, "ClueDesc", 16, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.92f, 0.90f, 0.84f, 0.98f), typewriterFont, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.68f));

        // Close Hint
        var hint = CreateText(_clueCardRoot.transform, "ClueHint", 13, FontStyle.Italic, TextAnchor.MiddleRight,
            new Color(0.72f, 0.72f, 0.76f, 0.85f), typewriterFont, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.14f));
        hint.text = "I-drag pa-ikot ang gamit o pindutin ang [SPACE] upang itago";

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

        Texture2D vigTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float u = (x / 31f) * 2f - 1f;
                float v = (y / 31f) * 2f - 1f;
                float dist = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.Clamp01((dist - 0.55f) / 0.75f) * 0.68f;
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

        // Guard against re-triggering while already open or rapid key-spamming
        if (_isOpen) return;
        if (Time.unscaledTime - _lastToggleTime < 0.25f) return;

        if (_canvas == null) BuildUI();

        _lastToggleTime = Time.unscaledTime;
        _activeItem = item;
        _openedFrame = Time.frameCount;
        _wasInventoryOpenBefore = (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen);

        // Clear any selected UI Button in EventSystem so pressing [SPACE] to inspect a clue
        // never triggers a background UI button's OnSubmit event!
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Unlock cursor for free click-and-drag rotation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Freeze player movement/mouse look while inspecting
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

        // Instantiate and setup 3D model in isolated studio
        SetupModelInStudio(item);

        // Reset camera and rotation with a slight 3D presentation angle
        _targetRotation = Quaternion.Euler(8f, -14f, 0f);
        _currentRotation = _targetRotation;
        if (_modelPivot != null) _modelPivot.localRotation = _currentRotation;
        _rotationVelocity = Vector2.zero;
        _isDragging = false;
        _currentDragDistance = 0f;

        _targetDistance = _defaultDistance;
        _currentDistance = _defaultDistance;
        if (_studioCamera != null)
        {
            _studioCamera.transform.localPosition = new Vector3(0f, 0.04f, -_currentDistance);
            _studioCamera.enabled = true;
        }

        SetStudioLightsEnabled(true);

        PlaySoftHandleSound(0.28f);
        SetVisible(true);
        _isOpen = true;

        Debug.Log($"[ItemInspectionViewer3D] Inspecting item in 3D: {item.itemName}");
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _isDragging = false;
        _lastToggleTime = Time.unscaledTime;

        PlaySoftHandleSound(0.18f);
        SetVisible(false);

        if (_studioCamera != null)
            _studioCamera.enabled = false;

        SetStudioLightsEnabled(false);

        // Clean up temporary studio materials
        CleanupStudioMaterials();

        // Clean up spawned inspected model
        if (_currentInspectedInstance != null)
        {
            Destroy(_currentInspectedInstance);
            _currentInspectedInstance = null;
        }
        _activeClueMarkers.Clear();
        _clueHotspotRings.Clear();
        _currentlyFacingClue = null;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

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

    // ── Model Spawning, Studio Material Lighting & 3D Clue Hotspots ──────────

    private void SetupModelInStudio(InventoryItem item)
    {
        CleanupStudioMaterials();

        if (_currentInspectedInstance != null)
        {
            Destroy(_currentInspectedInstance);
            _currentInspectedInstance = null;
        }
        _activeClueMarkers.Clear();
        _clueHotspotRings.Clear();

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
        }
        else
        {
            _currentInspectedInstance = Instantiate(prefab, _modelPivot);
            _currentInspectedInstance.transform.localPosition = Vector3.zero;
            _currentInspectedInstance.transform.localRotation = Quaternion.identity;
        }

        // Strip colliders, rigidbodies, and world lights from inspect copy
        foreach (var col in _currentInspectedInstance.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (var rb in _currentInspectedInstance.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
        foreach (var l in _currentInspectedInstance.GetComponentsInChildren<Light>(true))
            Destroy(l);
        foreach (var hl in _currentInspectedInstance.GetComponentsInChildren<ItemVisualHighlight>(true))
            Destroy(hl);

        // Rebuild and detail the 3D geometry & high-resolution textures so every inspected item
        // looks richly detailed, watertight, and properly textured on all sides
        RebuildAndDetailInspectedModel(_currentInspectedInstance, item);

        // Center visual bounds and normalize scale so all items fit the frame cleanly
        NormalizeModelBounds(_currentInspectedInstance);

        // Ensure every item has an InspectableClueMarker configured on a rotatable side/back/bottom
        EnsureAndConfigureClueMarkers(_currentInspectedInstance, item);

        // Assign all children to the isolated InspectionStudioLayer and boost materials for RE-style clarity
        SetLayerRecursively(_currentInspectedInstance, InspectionStudioLayer);
        EnhanceStudioModelMaterials(_currentInspectedInstance);
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Boosts the cloned inspection model's materials so it looks brightly and clearly lit
    /// inside the isolated inspection studio (like Resident Evil Remakes) without washing out
    /// textures or 3D shading and without affecting the dark horror environment.
    /// </summary>
    private void EnhanceStudioModelMaterials(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Material[] mats = r.materials; // Creates per-instance material copies for studio only
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                _instantiatedStudioMaterials.Add(m);

                // 1. Render both sides so thin geometry never disappears when rotated
                if (m.HasProperty("_Cull"))
                    m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

                // 2. Disable environment reflections so the dark scene skybox does not tint metallic parts black
                if (m.HasProperty("_EnvironmentReflections"))
                    m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

                // 3. Ensure texture is bound to BOTH _BaseMap (URP) and _MainTex
                Texture baseTex = null;
                if (m.HasProperty("_BaseMap")) baseTex = m.GetTexture("_BaseMap");
                if (baseTex == null && m.HasProperty("_MainTex")) baseTex = m.GetTexture("_MainTex");
                if (baseTex == null) baseTex = m.mainTexture;

                if (baseTex != null)
                {
                    m.mainTexture = baseTex;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", baseTex);
                }

                // 4. Preserve rich 3D shading and texture contrast
                if (m.HasProperty("_Metallic"))
                {
                    float met = m.GetFloat("_Metallic");
                    m.SetFloat("_Metallic", Mathf.Min(met, 0.35f));
                }
                if (m.HasProperty("_Smoothness"))
                {
                    float sm = m.GetFloat("_Smoothness");
                    m.SetFloat("_Smoothness", Mathf.Clamp(sm, 0.22f, 0.58f));
                }

                Color baseCol = Color.white;
                if (m.HasProperty("_BaseColor"))
                    baseCol = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color"))
                    baseCol = m.GetColor("_Color");

                if (baseTex != null)
                {
                    // Keep textured surfaces crisp and un-washed (pure white multiplier when dark/grey)
                    float maxChan = Mathf.Max(baseCol.r, Mathf.Max(baseCol.g, baseCol.b));
                    if (maxChan < 0.75f)
                    {
                        baseCol = Color.Lerp(baseCol, Color.white, 0.75f);
                    }
                }
                else
                {
                    // For untextured dark parts (e.g. black plastic/metal), lift slightly so 3D bevels catch studio lights
                    float maxChan = Mathf.Max(baseCol.r, Mathf.Max(baseCol.g, baseCol.b));
                    if (maxChan < 0.18f)
                    {
                        baseCol = new Color(
                            Mathf.Max(baseCol.r, 0.16f),
                            Mathf.Max(baseCol.g, 0.17f),
                            Mathf.Max(baseCol.b, 0.20f),
                            baseCol.a
                        );
                    }
                }

                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
                if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);

                // 5. Subtle ambient fill so shadowed crevices remain readable without flattening 3D normals
                m.EnableKeyword("_EMISSION");
                if (baseTex != null && m.HasProperty("_EmissionMap"))
                {
                    m.SetTexture("_EmissionMap", baseTex);
                    if (m.HasProperty("_EmissionColor"))
                        m.SetColor("_EmissionColor", new Color(0.16f, 0.16f, 0.16f, 1f));
                }
                else if (m.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", new Color(baseCol.r * 0.10f, baseCol.g * 0.10f, baseCol.b * 0.11f, 1f));
                }
            }
            r.materials = mats;
        }
    }

    private void CleanupStudioMaterials()
    {
        for (int i = 0; i < _instantiatedStudioMaterials.Count; i++)
        {
            if (_instantiatedStudioMaterials[i] != null)
                Destroy(_instantiatedStudioMaterials[i]);
        }
        _instantiatedStudioMaterials.Clear();
    }

    // ── High-Detail 3D Remodeling & Texture Binding for All Inspected Items ──

    private void RebuildAndDetailInspectedModel(GameObject root, InventoryItem item)
    {
        if (root == null || item == null) return;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (litShader == null) return;

        string id = ((item.name ?? "") + " " + (item.itemName ?? "")).ToLowerInvariant();

        // 1. First repair any serialized ChamferedBox meshes on the cloned hierarchy so none are inside-out or have edge gaps
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.sharedMesh.name.Contains("ChamferedBox"))
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 sz = b.size;
                if (sz.x > 0.001f && sz.y > 0.001f && sz.z > 0.001f)
                {
                    float bev = Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) * 0.10f;
                    mf.sharedMesh = Item3DModelFactory.CreateChamferedBoxMesh(sz.x, sz.y, sz.z, bev);
                }
            }
        }

        // 2. Dispatch specialized high-detail 3D model & texture builders
        if (id.Contains("blank_paper") || id.Contains("blank paper") || id.Contains("clearance slip"))
        {
            BuildDetailedClearanceSlip3D(root, litShader);
        }
        else if (id.Contains("handbook"))
        {
            BuildDetailedStudentHandbook3D(root, litShader);
        }
        else if (id.Contains("cassette") || id.Contains("counseling"))
        {
            BuildDetailedCassetteTape3D(root, litShader);
        }
        else if (id.Contains("fragment"))
        {
            BuildDetailedFragment3D(root, item, litShader);
        }
        else if (id.Contains("comlab") || id.Contains("clipboard"))
        {
            BuildDetailedComLabClipboard3D(root, litShader);
        }
        else if (id.Contains("voucher"))
        {
            BuildDetailedLibraryVoucher3D(root, litShader);
        }
        else if (id.Contains("printer") || id.Contains("ream") || id.Contains("bond paper"))
        {
            BuildDetailedPrinterPaperReam3D(root, litShader);
        }
        else if (id.Contains("logbook"))
        {
            BuildDetailedGuardLogbook3D(root, litShader);
        }
        else if (id.Contains("medical") || id.Contains("clinic"))
        {
            BuildDetailedMedicalSlip3D(root, litShader);
        }
        else if (id.Contains("fuse"))
        {
            BuildDetailedCeramicFuse3D(root, litShader);
        }
        else if (id.Contains("vga") || id.Contains("cable"))
        {
            BuildDetailedVGACable3D(root, litShader);
        }
        else if (id.Contains("locker") && id.Contains("key"))
        {
            BuildDetailedLockerKey3D(root, litShader);
        }
        else if (id.Contains("stamp") || id.Contains("seal"))
        {
            BuildDetailedSealStamp3D(root, litShader);
        }
        else if (id.Contains("coin") || id.Contains("peso"))
        {
            BuildDetailedPesoCoin3D(root, litShader);
        }
        else if (id.Contains("sandwich") || id.Contains("ham"))
        {
            BuildDetailedSandwich3D(root, litShader);
        }
        else if (id.Contains("flashlight"))
        {
            BuildDetailedFlashlight3D(root, litShader);
        }
    }

    private Material CreateStudioLitMaterial(Shader shader, string name, Color color, float metallic, float smoothness, Texture tex = null)
    {
        Material m = new Material(shader) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }
        _instantiatedStudioMaterials.Add(m);
        return m;
    }

    private static Texture2D LoadDiskTexture(string assetPath)
    {
#if UNITY_EDITOR
        Texture2D t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (t != null) return t;
#endif
        if (System.IO.File.Exists(assetPath))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(assetPath);
            Texture2D runtimeTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (runtimeTex.LoadImage(bytes))
            {
                runtimeTex.filterMode = FilterMode.Bilinear;
                return runtimeTex;
            }
        }
        return null;
    }

    private static void ClearModelChildren(GameObject root)
    {
        var toDestroy = new List<GameObject>();
        foreach (Transform child in root.transform)
        {
            if (child != null) toDestroy.Add(child.gameObject);
        }
        for (int i = 0; i < toDestroy.Count; i++)
        {
            DestroyImmediate(toDestroy[i]);
        }
    }

    private static GameObject AddPartMesh(Transform parent, string name, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject AddPartPrimitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ── 1. FULL CLEARANCE SLIP (Uses Assets/PlayerAssets/signed0.png by default, or signed1..6.png) ──
    private void BuildDetailedClearanceSlip3D(GameObject root, Shader shader)
    {
        ClearModelChildren(root);

        Texture frontTex = null;
        if (ClearanceManager.Instance != null)
        {
            frontTex = ClearanceManager.Instance.GetCurrentSlipTexture();
        }
        if (frontTex == null)
        {
            frontTex = LoadDiskTexture("Assets/PlayerAssets/signed0.png")
                    ?? LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_ClearanceSlip.png");
        }

        Texture2D backTex = CreateClearanceSlipBackWatermarkTexture();

        Material frontMat = CreateStudioLitMaterial(shader, "Slip_Front_Signed0", Color.white, 0.02f, 0.22f, frontTex);
        Material backMat  = CreateStudioLitMaterial(shader, "Slip_Back_Watermark", Color.white, 0.02f, 0.22f, backTex);
        Material edgeMat  = CreateStudioLitMaterial(shader, "Slip_PaperCore", new Color(0.95f, 0.93f, 0.87f, 1f), 0.0f, 0.15f);

        // Thin 3D paper core
        AddPartPrimitive(root.transform, "PaperCoreSheet", PrimitiveType.Cube,
            Vector3.zero, Quaternion.identity, new Vector3(0.268f, 0.378f, 0.0022f), edgeMat);

        // Front printed face (-Z faces camera at default rotation)
        AddPartPrimitive(root.transform, "ClearanceSlipSheet_Front", PrimitiveType.Quad,
            new Vector3(0f, 0f, -0.0014f), Quaternion.identity, new Vector3(0.27f, 0.38f, 1f), frontMat);

        // Back watermark & instructions face (+Z faces camera when rotated 180°)
        AddPartPrimitive(root.transform, "ClearanceSlipSheet_Back", PrimitiveType.Quad,
            new Vector3(0f, 0f, 0.0014f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.27f, 0.38f, 1f), backMat);
    }

    private static Texture2D CreateClearanceSlipBackWatermarkTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color paper = new Color(0.95f, 0.92f, 0.85f, 1f);
        Color watermark = new Color(0.86f, 0.82f, 0.73f, 1f);
        Color navyInk = new Color(0.14f, 0.18f, 0.32f, 1f);
        Color redStamp = new Color(0.76f, 0.16f, 0.16f, 1f);
        Vector2 center = new Vector2(w * 0.5f, h * 0.52f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = paper;
                if (x < 22 || x > w - 22 || y < 22 || y > h - 22)
                {
                    if (x == 20 || x == w - 20 || y == 20 || y == h - 20) c = navyInk;
                }

                // Subtle circular University Seal watermark in center
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (Mathf.Abs(d - 145f) < 5f || Mathf.Abs(d - 115f) < 3f || (d < 95f && ((x + y) % 12 < 3)))
                    c = watermark;

                // Back instructions & 6 department checklist table
                if (y > h - 110 && y < h - 55 && x > 55 && x < w - 55 && (y % 16 < 6))
                    c = navyInk;

                if (y > 160 && y < h - 150 && x > 60 && x < w - 60)
                {
                    if (y % 68 == 0 || x == 60 || x == w - 60 || x == 190) c = watermark;
                    if (y % 68 > 24 && y % 68 < 34 && x > 75 && x < w - 85 && (x % 11 < 6)) c = navyInk;
                }

                // Red Registrar instructions stamp at bottom
                if (y > 65 && y < 125 && x > 110 && x < w - 110)
                {
                    if (y == 66 || y == 124 || x == 111 || x == w - 111 || (y > 86 && y < 104 && (x % 10 < 6)))
                        c = redStamp;
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // ── 2. STUDENT HANDBOOK (Multi-Part Hardcover Book with Gold Emblem, Spine Ribs, Page Block & Back Warning Note) ──
    private void BuildDetailedStudentHandbook3D(GameObject root, Shader shader)
    {
        ClearModelChildren(root);

        Texture2D frontTex = CreateDetailedHandbookFrontTexture();
        Texture2D backTex  = CreateDetailedHandbookBackTexture();
        Texture2D pageTex  = CreatePageEdgesTexture();

        Material leatherMat = CreateStudioLitMaterial(shader, "Handbook_NavyLeather", new Color(0.09f, 0.14f, 0.28f, 1f), 0.12f, 0.42f);
        Material frontMat   = CreateStudioLitMaterial(shader, "Handbook_FrontArt", Color.white, 0.18f, 0.48f, frontTex);
        Material backMat    = CreateStudioLitMaterial(shader, "Handbook_BackArt", Color.white, 0.10f, 0.38f, backTex);
        Material pagesMat   = CreateStudioLitMaterial(shader, "Handbook_PageBlock", Color.white, 0.0f, 0.20f, pageTex);
        Material goldMat    = CreateStudioLitMaterial(shader, "Handbook_GoldFoil", new Color(0.95f, 0.79f, 0.28f, 1f), 0.85f, 0.72f);
        Material ribbonMat  = CreateStudioLitMaterial(shader, "Handbook_SatinRibbon", new Color(0.76f, 0.11f, 0.16f, 1f), 0.15f, 0.55f);

        // 1. Cream Multi-Sheet Page Block (inset slightly from front/back boards)
        Mesh pageBlockMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.208f, 0.294f, 0.036f, 0.002f);
        AddPartMesh(root.transform, "PageBlock", pageBlockMesh, new Vector3(0.006f, 0f, 0f), Quaternion.identity, Vector3.one, pagesMat);

        // 2. Front Hardcover Board (-Z)
        Mesh coverBoardMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.220f, 0.308f, 0.0065f, 0.002f);
        AddPartMesh(root.transform, "FrontCoverBoard", coverBoardMesh, new Vector3(0.002f, 0f, -0.020f), Quaternion.identity, Vector3.one, leatherMat);

        // Front Cover Printed & Gold-Stamped Artwork Face
        AddPartPrimitive(root.transform, "FrontCoverArt", PrimitiveType.Quad,
            new Vector3(0.002f, 0f, -0.0236f), Quaternion.identity, new Vector3(0.214f, 0.302f, 1f), frontMat);

        // Raised 3D Embossed Gold University Crest Medallion on Front Cover
        AddPartPrimitive(root.transform, "GoldCrestMedallion", PrimitiveType.Cylinder,
            new Vector3(0.002f, 0.018f, -0.0238f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.074f, 0.0018f, 0.074f), goldMat);

        // 4 Brass Corner Protectors on Front Cover
        Vector2[] corners = { new Vector2(-0.098f, 0.142f), new Vector2(0.102f, 0.142f), new Vector2(-0.098f, -0.142f), new Vector2(0.102f, -0.142f) };
        for (int i = 0; i < corners.Length; i++)
        {
            AddPartPrimitive(root.transform, $"BrassCorner_{i}", PrimitiveType.Cube,
                new Vector3(corners[i].x, corners[i].y, -0.020f), Quaternion.identity, new Vector3(0.022f, 0.022f, 0.0085f), goldMat);
        }

        // 3. Back Hardcover Board (+Z)
        AddPartMesh(root.transform, "BackCoverBoard", coverBoardMesh, new Vector3(0.002f, 0f, 0.020f), Quaternion.identity, Vector3.one, leatherMat);

        // Back Cover Artwork & Taped Handwritten Warning Note (+Z facing back)
        AddPartPrimitive(root.transform, "BackCoverWarningNote", PrimitiveType.Quad,
            new Vector3(0.002f, 0f, 0.0236f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.214f, 0.302f, 1f), backMat);

        // 4. Rounded Book Spine (-X) with 5 Raised Gold-Trimmed Spine Hubs
        Mesh spineMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.016f, 0.308f, 0.0465f, 0.004f);
        AddPartMesh(root.transform, "BookSpine", spineMesh, new Vector3(-0.106f, 0f, 0f), Quaternion.identity, Vector3.one, leatherMat);

        for (int i = 0; i < 5; i++)
        {
            float ribY = 0.11f - i * 0.055f;
            AddPartPrimitive(root.transform, $"SpineHubRib_{i}", PrimitiveType.Cube,
                new Vector3(-0.108f, ribY, 0f), Quaternion.identity, new Vector3(0.018f, 0.007f, 0.048f), goldMat);
        }

        // Headband and Tailband at top/bottom of spine
        AddPartPrimitive(root.transform, "SpineHeadband_Top", PrimitiveType.Cylinder,
            new Vector3(-0.098f, 0.147f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.018f, 0.012f), ribbonMat);
        AddPartPrimitive(root.transform, "SpineHeadband_Bottom", PrimitiveType.Cylinder,
            new Vector3(-0.098f, -0.147f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.018f, 0.012f), ribbonMat);

        // 5. Burgundy Satin Ribbon Bookmark Draping from Bottom Pages
        AddPartPrimitive(root.transform, "RibbonBookmarkTop", PrimitiveType.Cube,
            new Vector3(0.025f, -0.168f, -0.002f), Quaternion.Euler(0f, 0f, -10f), new Vector3(0.022f, 0.065f, 0.0025f), ribbonMat);
        AddPartPrimitive(root.transform, "RibbonBookmarkTail", PrimitiveType.Cube,
            new Vector3(0.036f, -0.205f, 0.001f), Quaternion.Euler(6f, 0f, -22f), new Vector3(0.022f, 0.045f, 0.0025f), ribbonMat);
    }

    private static Texture2D CreateDetailedHandbookFrontTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color navyBase = new Color(0.08f, 0.13f, 0.27f, 1f);
        Color navyGrain = new Color(0.06f, 0.10f, 0.22f, 1f);
        Color goldBright = new Color(0.96f, 0.82f, 0.32f, 1f);
        Color goldDark = new Color(0.75f, 0.58f, 0.18f, 1f);
        Color crimsonShield = new Color(0.58f, 0.10f, 0.14f, 1f);

        Vector2 sealCenter = new Vector2(w * 0.5f, h * 0.53f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Subtle pebble leatherette grain
                bool grain = ((x * 7 + y * 13) % 11 == 0) || ((x ^ y) % 9 == 0);
                Color c = grain ? navyGrain : navyBase;

                // Spine groove on left edge
                if (x > 22 && x < 30) c = navyGrain * 0.8f;

                // Double ornate gold foil border frame
                bool outerBorder = (x >= 36 && x <= w - 36 && (Mathf.Abs(x - 36) < 4 || Mathf.Abs(x - (w - 36)) < 4 || Mathf.Abs(y - 36) < 4 || Mathf.Abs(y - (h - 36)) < 4));
                bool innerBorder = (x >= 48 && x <= w - 48 && y >= 48 && y <= h - 48 && (Mathf.Abs(x - 48) < 2 || Mathf.Abs(x - (w - 48)) < 2 || Mathf.Abs(y - 48) < 2 || Mathf.Abs(y - (h - 48)) < 2));
                if (outerBorder) c = goldBright;
                else if (innerBorder) c = goldDark;

                // Gold Header Title Block ("PAMANTASAN NG LUNGSOD // STUDENT HANDBOOK")
                if (y > h - 155 && y < h - 125 && x > 85 && x < w - 85 && (x % 12 < 8)) c = goldBright;
                if (y > h - 195 && y < h - 172 && x > 120 && x < w - 120 && (x % 10 < 7)) c = goldDark;
                if (y == h - 210 && x > 95 && x < w - 95) c = goldBright;

                // Central Ornate University Seal
                float d = Vector2.Distance(new Vector2(x, y), sealCenter);
                if (d < 105f)
                {
                    if (Mathf.Abs(d - 100f) < 5f || Mathf.Abs(d - 78f) < 3.5f) c = goldBright;
                    else if (d > 78f && d < 100f)
                    {
                        float ang = Mathf.Atan2(y - sealCenter.y, x - sealCenter.x);
                        if (Mathf.Abs(Mathf.Sin(ang * 24f)) > 0.45f) c = goldDark;
                    }
                    else if (d < 74f)
                    {
                        c = crimsonShield;
                        int dx = Mathf.Abs(x - (int)sealCenter.x);
                        int dy = y - (int)sealCenter.y;
                        // Gold cross & open book motif inside shield
                        if (dx < 8 || Mathf.Abs(dy) < 8 || (dx < 38 && Mathf.Abs(dy) < 26 && (dx % 12 < 6)))
                            c = goldBright;
                    }
                }

                // Bottom Edition & Academic Year Foil Block
                if (y > 115 && y < 138 && x > 105 && x < w - 105 && (x % 11 < 7)) c = goldBright;
                if (y > 82 && y < 100 && x > 150 && x < w - 150 && (x % 9 < 6)) c = goldDark;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateDetailedHandbookBackTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color navyBase = new Color(0.08f, 0.13f, 0.27f, 1f);
        Color goldBorder = new Color(0.78f, 0.62f, 0.22f, 1f);
        Color notePaper = new Color(0.95f, 0.91f, 0.78f, 1f);
        Color blueRule = new Color(0.68f, 0.76f, 0.88f, 1f);
        Color redMargin = new Color(0.84f, 0.35f, 0.35f, 1f);
        Color darkInk = new Color(0.12f, 0.12f, 0.16f, 1f);
        Color urgentRed = new Color(0.82f, 0.10f, 0.10f, 1f);
        Color tapeTan = new Color(0.86f, 0.78f, 0.54f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = ((x * 7 + y * 13) % 11 == 0) ? navyBase * 0.88f : navyBase;

                // Gold back frame
                if (x >= 36 && x <= w - 36 && (Mathf.Abs(x - 36) < 3 || Mathf.Abs(x - (w - 36)) < 3 || Mathf.Abs(y - 36) < 3 || Mathf.Abs(y - (h - 36)) < 3))
                    c = goldBorder;

                // Taped Yellowed Notebook Warning Page in Center-Back (x: 75..435, y: 150..630)
                if (x > 75 && x < 435 && y > 150 && y < 630)
                {
                    c = notePaper;
                    if (x == 115) c = redMargin;
                    if (y % 28 == 0 && y < 590) c = blueRule;

                    // Red urgent header: "HUWAG TATALIKURAN ANG ESTATWA!"
                    if (y > 555 && y < 585 && x > 125 && x < 415 && (x % 11 < 7))
                        c = urgentRed;

                    // Handwritten survival note lines
                    if (y > 320 && y < 530 && (y % 28 > 8 && y % 28 < 18) && x > 125 && x < 410)
                    {
                        if (((x + y * 3) % 9) < 6) c = darkInk;
                    }

                    // Underlined red warning at bottom of note: "DO NOT BLINK! - DILIMAN WING"
                    if (y > 210 && y < 275 && (y % 26 > 6 && y % 26 < 18) && x > 125 && x < 405)
                    {
                        if ((x % 10) < 7) c = urgentRed;
                    }
                    if (y == 204 && x > 120 && x < 410) c = urgentRed;
                }

                // Masking tape strips holding the note at top-left and bottom-right corners
                if ((x > 55 && x < 135 && y > 595 && y < 645) || (x > 380 && x < 455 && y > 130 && y < 180))
                {
                    c = tapeTan;
                }

                // White library barcode sticker at bottom left
                if (x > 80 && x < 230 && y > 58 && y < 115)
                {
                    c = Color.white;
                    if (y > 70 && y < 105 && x > 92 && x < 218 && (x % 5 < 3))
                        c = Color.black;
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreatePageEdgesTexture()
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color cream1 = new Color(0.93f, 0.90f, 0.81f, 1f);
        Color cream2 = new Color(0.84f, 0.79f, 0.69f, 1f);
        for (int y = 0; y < h; y++)
        {
            Color rowCol = (y % 3 == 0) ? cream2 : cream1;
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, (x % 4 == 0) ? cream2 : rowCol);
            }
        }
        tex.Apply();
        return tex;
    }

    // ── 3. COUNSELING AUDIO CASSETTE TAPE (Detailed Shell, Head Shield, Twin Reels, Side A & Side B 4-1-9 Label) ──
    private void BuildDetailedCassetteTape3D(GameObject root, Shader shader)
    {
        ClearModelChildren(root);

        Texture2D labelATex = CreateDetailedCassetteLabelTexture(isSideB: false);
        Texture2D labelBTex = CreateDetailedCassetteLabelTexture(isSideB: true);

        Material shellMat   = CreateStudioLitMaterial(shader, "Cassette_CharcoalShell", new Color(0.16f, 0.17f, 0.20f, 1f), 0.25f, 0.55f);
        Material windowMat  = CreateStudioLitMaterial(shader, "Cassette_DarkWindow", new Color(0.08f, 0.09f, 0.11f, 1f), 0.15f, 0.75f);
        Material spoolMat   = CreateStudioLitMaterial(shader, "Cassette_WhiteSpool", new Color(0.95f, 0.95f, 0.96f, 1f), 0.05f, 0.45f);
        Material tapeMat    = CreateStudioLitMaterial(shader, "Cassette_MagneticTape", new Color(0.32f, 0.18f, 0.10f, 1f), 0.20f, 0.65f);
        Material chromeMat  = CreateStudioLitMaterial(shader, "Cassette_ChromeScrew", new Color(0.88f, 0.90f, 0.94f, 1f), 0.85f, 0.80f);
        Material copperMat  = CreateStudioLitMaterial(shader, "Cassette_CopperPad", new Color(0.82f, 0.52f, 0.26f, 1f), 0.65f, 0.50f);
        Material matSideA   = CreateStudioLitMaterial(shader, "Cassette_Label_SideA", Color.white, 0.05f, 0.30f, labelATex);
        Material matSideB   = CreateStudioLitMaterial(shader, "Cassette_Label_SideB", Color.white, 0.05f, 0.30f, labelBTex);

        // 1. Watertight Main Compact Cassette Shell
        Mesh shellMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.32f, 0.20f, 0.036f, 0.004f);
        AddPartMesh(root.transform, "CassetteShell", shellMesh, Vector3.zero, Quaternion.identity, Vector3.one, shellMat);

        // 2. Raised Bottom Head-Shield Trapezoid Housing (Classic Compact Cassette Bottom Bulge)
        Mesh shieldMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.22f, 0.044f, 0.044f, 0.003f);
        AddPartMesh(root.transform, "HeadShieldHousing", shieldMesh, new Vector3(0f, -0.078f, 0f), Quaternion.identity, Vector3.one, shellMat);

        // Exposed Magnetic Tape Ribbon & Copper Pressure Pad at Bottom Edge
        AddPartPrimitive(root.transform, "ExposedBottomTape", PrimitiveType.Cube,
            new Vector3(0f, -0.099f, 0f), Quaternion.identity, new Vector3(0.18f, 0.004f, 0.024f), tapeMat);
        AddPartPrimitive(root.transform, "PressurePadSpring", PrimitiveType.Cube,
            new Vector3(0f, -0.096f, 0f), Quaternion.identity, new Vector3(0.028f, 0.008f, 0.028f), copperMat);

        // Bottom Capstan & Guide Holes (2 pairs)
        float[] holeX = { -0.085f, -0.045f, 0.045f, 0.085f };
        for (int i = 0; i < holeX.Length; i++)
        {
            AddPartPrimitive(root.transform, $"CapstanHole_{i}", PrimitiveType.Cylinder,
                new Vector3(holeX[i], -0.082f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.014f, 0.023f, 0.014f), windowMat);
        }

        // 3. Side A Label (Front -Z) & Side B Label with SAFE CODE: 4-1-9 (Back +Z)
        AddPartPrimitive(root.transform, "Label_SideA", PrimitiveType.Quad,
            new Vector3(0f, 0.018f, -0.0186f), Quaternion.identity, new Vector3(0.285f, 0.132f, 1f), matSideA);

        AddPartPrimitive(root.transform, "Label_SideB_Clue", PrimitiveType.Quad,
            new Vector3(0f, 0.018f, 0.0186f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.285f, 0.132f, 1f), matSideB);

        // 4. Center Reel Window Recess & Dual Magnetic Tape Reels + 6-Toothed White Drive Hubs (Visible on BOTH Front and Back!)
        AddPartPrimitive(root.transform, "CenterWindowBar", PrimitiveType.Cube,
            new Vector3(0f, 0.012f, 0f), Quaternion.identity, new Vector3(0.195f, 0.068f, 0.0378f), windowMat);

        // Left & Right Magnetic Tape Pancakes
        AddPartPrimitive(root.transform, "LeftTapePancake", PrimitiveType.Cylinder,
            new Vector3(-0.068f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.062f, 0.0192f, 0.062f), tapeMat);
        AddPartPrimitive(root.transform, "RightTapePancake", PrimitiveType.Cylinder,
            new Vector3(0.068f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.048f, 0.0192f, 0.048f), tapeMat);

        // White 6-Toothed Drive Hubs & Hub Holes
        for (int side = -1; side <= 1; side += 2)
        {
            Mesh hubMesh = Item3DModelFactory.CreateReededCylinderMesh(0.025f, 0.040f, 6, 0.005f);
            AddPartMesh(root.transform, $"DriveHub_{side}", hubMesh,
                new Vector3(side * 0.068f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, spoolMat);

            AddPartPrimitive(root.transform, $"HubCenterHole_{side}", PrimitiveType.Cylinder,
                new Vector3(side * 0.068f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.018f, 0.0205f, 0.018f), windowMat);
        }

        // 5. 5 Metallic Corner Assembly Screws
        Vector2[] screwPos = {
            new Vector2(-0.145f, 0.086f), new Vector2(0.145f, 0.086f),
            new Vector2(-0.145f, -0.086f), new Vector2(0.145f, -0.086f),
            new Vector2(0f, 0.088f)
        };
        for (int i = 0; i < screwPos.Length; i++)
        {
            AddPartPrimitive(root.transform, $"AssemblyScrew_{i}", PrimitiveType.Cylinder,
                new Vector3(screwPos[i].x, screwPos[i].y, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.010f, 0.0195f, 0.010f), chromeMat);
        }
    }

    private static Texture2D CreateDetailedCassetteLabelTexture(bool isSideB)
    {
        int w = 512, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color creamPaper = new Color(0.95f, 0.92f, 0.84f, 1f);
        Color borderLine = new Color(0.22f, 0.22f, 0.25f, 1f);
        Color headerStripe = isSideB ? new Color(0.78f, 0.14f, 0.14f, 1f) : new Color(0.12f, 0.26f, 0.58f, 1f);
        Color accentStripe = isSideB ? new Color(0.92f, 0.42f, 0.18f, 1f) : new Color(0.22f, 0.52f, 0.82f, 1f);
        Color darkInk = new Color(0.10f, 0.10f, 0.14f, 1f);
        Color redClueInk = new Color(0.82f, 0.08f, 0.08f, 1f);
        Color white = new Color(0.98f, 0.98f, 0.98f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = creamPaper;

                // Outer label border
                if (x < 6 || x > w - 7 || y < 6 || y > h - 7) c = borderLine;

                // Colored top header band
                if (y >= h - 56 && y <= h - 10 && x >= 10 && x <= w - 10)
                {
                    c = (y < h - 46) ? accentStripe : headerStripe;
                    // "SIDE A / B" badge box on left
                    if (x > 22 && x < 78 && y > h - 42 && y < h - 16) c = white;
                    // Header text bars
                    if (x > 95 && x < w - 35 && y > h - 36 && y < h - 22 && (x % 12 < 8)) c = white;
                }

                // Ruled writing lines in top & bottom label zones
                if ((y == h - 76 || y == h - 96 || y == 36 || y == 56) && x > 24 && x < w - 24)
                    c = new Color(0.72f, 0.74f, 0.78f, 1f);

                // Simulated handwritten title on top line: "SESSION #04 - ANXIETY" (Side A) or "SAFE CODE: 4 - 1 - 9" (Side B)
                if (!isSideB)
                {
                    if (y > h - 92 && y < h - 78 && x > 45 && x < w - 65 && ((x * 3 + y) % 9 < 6))
                        c = darkInk;
                    if (y > 40 && y < 52 && x > 60 && x < w - 60 && (x % 10 < 6))
                        c = headerStripe;
                }
                else
                {
                    // Bold unmistakable red & black handwriting on Side B
                    if (y > h - 94 && y < h - 76 && x > 40 && x < w - 40 && (x % 10 < 7))
                        c = redClueInk;
                    if (y > 38 && y < 54 && x > 80 && x < w - 80 && (x % 12 < 8))
                        c = darkInk;
                }

                tex.SetPixel(x, y, c);
            }
        }

        // Draw crisp pixel-art digits "SAFE CODE: 4-1-9" on Side B and "SESSION #04" on Side A
        if (isSideB)
        {
            DrawPixelString(tex, "SAFE CODE: 4-1-9", 105, 16, 3, redClueInk);
        }
        else
        {
            DrawPixelString(tex, "SESSION 04 AUDIO", 105, 16, 3, darkInk);
        }

        tex.Apply();
        return tex;
    }

    // ── 4. Additional Item Texture & Detail Enforcers ──

    private void BuildDetailedFragment3D(GameObject root, InventoryItem item, Shader shader)
    {
        ClearModelChildren(root);
        Texture2D fullSlip = LoadDiskTexture("Assets/PlayerAssets/signed0.png");
        int fragIdx = 0;
        string n = (item.name + " " + item.itemName).ToLowerInvariant();
        if (n.Contains("left") || n.Contains("2")) fragIdx = 1;
        else if (n.Contains("right") || n.Contains("3")) fragIdx = 2;
        else if (n.Contains("bottom") || n.Contains("4")) fragIdx = 3;

        Texture2D frontTex = LoadDiskTexture($"Assets/Textures/ItemInspectTextures/Tex_Fragment_{fragIdx}.png") ?? fullSlip;
        Texture2D backTex = CreateClearanceSlipBackWatermarkTexture();

        Material frontMat = CreateStudioLitMaterial(shader, $"Frag_Front_{fragIdx}", Color.white, 0.02f, 0.20f, frontTex);
        Material backMat  = CreateStudioLitMaterial(shader, $"Frag_Back_{fragIdx}", Color.white, 0.02f, 0.20f, backTex);
        Material edgeMat  = CreateStudioLitMaterial(shader, "Frag_PaperCore", new Color(0.94f, 0.91f, 0.84f, 1f), 0f, 0.15f);

        AddPartPrimitive(root.transform, "FragCore", PrimitiveType.Cube, Vector3.zero, Quaternion.identity, new Vector3(0.18f, 0.24f, 0.002f), edgeMat);
        AddPartPrimitive(root.transform, "FragFront", PrimitiveType.Quad, new Vector3(0f, 0f, -0.0013f), Quaternion.identity, new Vector3(0.182f, 0.242f, 1f), frontMat);
        AddPartPrimitive(root.transform, "FragBack", PrimitiveType.Quad, new Vector3(0f, 0f, 0.0013f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.182f, 0.242f, 1f), backMat);
    }

    private void BuildDetailedComLabClipboard3D(GameObject root, Shader shader)
    {
        Texture2D formTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_ComLabForm.png");
        Transform sheet = root.transform.Find("PaperSheet");
        if (sheet != null && formTex != null)
        {
            sheet.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "ComLabForm_Front", Color.white, 0.02f, 0.25f, formTex);
            AddPartPrimitive(root.transform, "ComLabForm_TopFace", PrimitiveType.Quad,
                new Vector3(0f, -0.01f, -0.009f), Quaternion.identity, new Vector3(0.225f, 0.335f, 1f),
                CreateStudioLitMaterial(shader, "ComLabForm_Quad", Color.white, 0.02f, 0.25f, formTex));
        }
        Transform sticky = root.transform.Find("BackStickyNote");
        if (sticky != null)
        {
            sticky.localPosition = new Vector3(0.03f, 0.04f, 0.009f);
            sticky.localRotation = Quaternion.Euler(0f, 180f, 6f);
            sticky.localScale = new Vector3(0.11f, 0.11f, 1f);
            Texture2D stickyTex = CreateStickyNoteClueTexture("RETURN VGA CABLE", "TO SERVER ROOM!");
            sticky.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "StickyNote_Mat", Color.white, 0.02f, 0.22f, stickyTex);
        }
    }

    private void BuildDetailedLibraryVoucher3D(GameObject root, Shader shader)
    {
        ClearModelChildren(root);
        Texture2D frontTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_LibraryVoucher.png");
        Texture2D backTex  = CreateStickyNoteClueTexture("ARCHIVE STAMP", "LIB-94 OVERDUE");
        Material frontMat  = CreateStudioLitMaterial(shader, "Voucher_Front", Color.white, 0.02f, 0.20f, frontTex);
        Material backMat   = CreateStudioLitMaterial(shader, "Voucher_Back", Color.white, 0.02f, 0.20f, backTex);
        Material coreMat   = CreateStudioLitMaterial(shader, "Voucher_Core", new Color(0.92f, 0.94f, 0.88f, 1f), 0f, 0.15f);

        AddPartPrimitive(root.transform, "VoucherCore", PrimitiveType.Cube, Vector3.zero, Quaternion.identity, new Vector3(0.238f, 0.378f, 0.002f), coreMat);
        AddPartPrimitive(root.transform, "VoucherFront", PrimitiveType.Quad, new Vector3(0f, 0f, -0.0013f), Quaternion.identity, new Vector3(0.24f, 0.38f, 1f), frontMat);
        AddPartPrimitive(root.transform, "VoucherBack", PrimitiveType.Quad, new Vector3(0f, 0f, 0.0013f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.24f, 0.38f, 1f), backMat);
    }

    private void BuildDetailedPrinterPaperReam3D(GameObject root, Shader shader)
    {
        Texture2D wrapTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_PrinterPaper.png");
        Material wrapMat = CreateStudioLitMaterial(shader, "PaperReam_Wrap", Color.white, 0.05f, 0.35f, wrapTex);
        var block = root.transform.Find("PaperReamBlock");
        if (block != null)
        {
            block.GetComponent<MeshFilter>().sharedMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.24f, 0.085f, 0.34f, 0.005f);
            block.GetComponent<MeshRenderer>().sharedMaterial = wrapMat;
        }
    }

    private void BuildDetailedGuardLogbook3D(GameObject root, Shader shader)
    {
        Texture2D logTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_GuardLogbook.png");
        Material logMat = CreateStudioLitMaterial(shader, "GuardLog_Page", Color.white, 0.02f, 0.25f, logTex);
        Material pageEdgeMat = CreateStudioLitMaterial(shader, "GuardLog_Edges", Color.white, 0f, 0.20f, CreatePageEdgesTexture());
        Transform cover = root.transform.Find("BookCover");
        if (cover != null)
        {
            AddPartMesh(cover, "LogbookPageBlock", Item3DModelFactory.CreateChamferedBoxMesh(0.238f, 0.328f, 0.036f, 0.002f),
                new Vector3(0.004f, 0f, 0f), Quaternion.identity, Vector3.one, pageEdgeMat);
            Transform logSheet = cover.Find("IncidentLogPage");
            if (logSheet != null)
            {
                logSheet.localPosition = new Vector3(0f, 0f, 0.0245f);
                logSheet.GetComponent<MeshRenderer>().sharedMaterial = logMat;
            }
        }
    }

    private void BuildDetailedMedicalSlip3D(GameObject root, Shader shader)
    {
        Texture2D medTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_MedicalSlip.png");
        Material medMat = CreateStudioLitMaterial(shader, "Medical_Chart", Color.white, 0.02f, 0.25f, medTex);
        Transform board = root.transform.Find("AluminumChartBase");
        if (board != null)
        {
            Transform chart = board.Find("ClinicalChart");
            if (chart != null)
            {
                chart.localPosition = new Vector3(0f, -0.01f, -0.007f);
                chart.localRotation = Quaternion.identity;
                chart.GetComponent<MeshRenderer>().sharedMaterial = medMat;
            }
            Texture2D backCodeTex = CreateStickyNoteClueTexture("DISPENSARY CODE", "CAB-MED: 9-0-4");
            AddPartPrimitive(board, "MedicalBackSticker", PrimitiveType.Quad,
                new Vector3(0f, -0.02f, 0.007f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.18f, 0.12f, 1f),
                CreateStudioLitMaterial(shader, "Medical_BackCode", Color.white, 0.02f, 0.25f, backCodeTex));
        }
    }

    private void BuildDetailedCeramicFuse3D(GameObject root, Shader shader)
    {
        Texture2D fuseTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_CeramicFuse.png");
        Transform label = root.transform.Find("SpecsLabel");
        if (label != null && fuseTex != null)
        {
            label.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "Fuse_Label", Color.white, 0.05f, 0.35f, fuseTex);
        }
    }

    private void BuildDetailedVGACable3D(GameObject root, Shader shader)
    {
        Texture2D vgaTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_VGACable.png");
        Transform hood = root.transform.Find("ConnectorHood");
        if (hood != null)
        {
            Transform label = hood.Find("ConnectorLabel");
            if (label != null && vgaTex != null)
            {
                label.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "VGA_Label", Color.white, 0.05f, 0.35f, vgaTex);
            }
        }
    }

    private void BuildDetailedLockerKey3D(GameObject root, Shader shader)
    {
        Texture2D tagTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_KeyTag.png");
        if (tagTex != null)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.gameObject.name.ToLowerInvariant().Contains("tag"))
                {
                    r.sharedMaterial = CreateStudioLitMaterial(shader, "KeyTag_Mat", Color.white, 0.05f, 0.45f, tagTex);
                }
            }
        }
    }

    private void BuildDetailedSealStamp3D(GameObject root, Shader shader)
    {
        Texture2D sealTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_EmbossingSeal.png");
        Transform die = root.transform.Find("RubberDie");
        if (die != null && sealTex != null)
        {
            die.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "SealDie_Mat", Color.white, 0.05f, 0.35f, sealTex);
        }
    }

    private void BuildDetailedPesoCoin3D(GameObject root, Shader shader)
    {
        Texture2D coinTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_PesoCoin.png");
        Transform body = root.transform.Find("ReededCoinBody");
        if (body != null && coinTex != null)
        {
            body.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "PesoCoin_Mat", new Color(0.92f, 0.92f, 0.95f, 1f), 0.55f, 0.65f, coinTex);
        }
    }

    private void BuildDetailedSandwich3D(GameObject root, Shader shader)
    {
        Texture2D wrapTex = LoadDiskTexture("Assets/Textures/ItemInspectTextures/Tex_SandwichWrapper.png");
        Transform wrap = root.transform.Find("WaxPaperSleeve");
        if (wrap != null && wrapTex != null)
        {
            wrap.GetComponent<MeshRenderer>().sharedMaterial = CreateStudioLitMaterial(shader, "SandwichWrap_Mat", Color.white, 0.02f, 0.35f, wrapTex);
        }
    }

    private void BuildDetailedFlashlight3D(GameObject root, Shader shader)
    {
        Transform tail = root.transform.Find("Tailcap");
        if (tail != null)
        {
            Texture2D serialTex = CreateStickyNoteClueTexture("SG-2 DELA CRUZ", "SECURITY PROPERTY");
            AddPartPrimitive(tail, "TailcapSerialPlate", PrimitiveType.Quad,
                new Vector3(0f, -0.52f, 0.28f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.55f, 0.28f, 1f),
                CreateStudioLitMaterial(shader, "FlashlightSerial_Mat", Color.white, 0.35f, 0.55f, serialTex));
        }
    }

    private static Texture2D CreateStickyNoteClueTexture(string line1, string line2)
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bg = new Color(0.96f, 0.92f, 0.52f, 1f);
        Color ink = new Color(0.12f, 0.12f, 0.16f, 1f);
        Color red = new Color(0.82f, 0.12f, 0.12f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                if (x < 6 || x > w - 7 || y < 6 || y > h - 7) c = new Color(0.78f, 0.72f, 0.35f, 1f);
                if (y % 28 == 0 && y < h - 35) c = new Color(0.84f, 0.78f, 0.42f, 1f);
                tex.SetPixel(x, y, c);
            }
        }
        DrawPixelString(tex, line1, 18, 150, 2, ink);
        DrawPixelString(tex, line2, 18, 95, 2, red);
        tex.Apply();
        return tex;
    }

    private static void DrawPixelString(Texture2D tex, string text, int startX, int startY, int scale, Color col)
    {
        if (string.IsNullOrEmpty(text)) return;
        int cursorX = startX;
        for (int i = 0; i < text.Length; i++)
        {
            uint glyph = GetGlyph3x5(char.ToUpperInvariant(text[i]));
            for (int row = 0; row < 5; row++)
            {
                for (int colBit = 0; colBit < 3; colBit++)
                {
                    int bitIndex = (4 - row) * 3 + (2 - colBit);
                    if (((glyph >> bitIndex) & 1u) != 0u)
                    {
                        for (int dy = 0; dy < scale; dy++)
                        {
                            for (int dx = 0; dx < scale; dx++)
                            {
                                int px = cursorX + colBit * scale + dx;
                                int py = startY + (4 - row) * scale + dy;
                                if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                                    tex.SetPixel(px, py, col);
                            }
                        }
                    }
                }
            }
            cursorX += 4 * scale;
        }
    }

    private static uint GetGlyph3x5(char c)
    {
        switch (c)
        {
            case '0': return 0b111_101_101_101_111;
            case '1': return 0b010_110_010_010_111;
            case '2': return 0b111_001_111_100_111;
            case '3': return 0b111_001_111_001_111;
            case '4': return 0b101_101_111_001_001;
            case '5': return 0b111_100_111_001_111;
            case '6': return 0b111_100_111_101_111;
            case '7': return 0b111_001_001_010_010;
            case '8': return 0b111_101_111_101_111;
            case '9': return 0b111_101_111_001_111;
            case 'A': return 0b010_101_111_101_101;
            case 'B': return 0b110_101_110_101_110;
            case 'C': return 0b111_100_100_100_111;
            case 'D': return 0b110_101_101_101_110;
            case 'E': return 0b111_100_111_100_111;
            case 'F': return 0b111_100_111_100_100;
            case 'G': return 0b111_100_101_101_111;
            case 'H': return 0b101_101_111_101_101;
            case 'I': return 0b111_010_010_010_111;
            case 'L': return 0b100_100_100_100_111;
            case 'M': return 0b101_111_111_101_101;
            case 'N': return 0b111_101_101_101_101;
            case 'O': return 0b111_101_101_101_111;
            case 'P': return 0b111_101_111_100_100;
            case 'R': return 0b111_101_110_101_101;
            case 'S': return 0b111_100_111_001_111;
            case 'T': return 0b111_010_010_010_010;
            case 'U': return 0b101_101_101_101_111;
            case 'V': return 0b101_101_101_101_010;
            case 'Y': return 0b101_101_010_010_010;
            case '-': return 0b000_000_111_000_000;
            case ':': return 0b000_010_000_010_000;
            default:  return 0b000_000_000_000_000;
        }
    }

    /// <summary>
    /// Ensures every inspected 3D model has an InspectableClueMarker oriented so the player
    /// can click and drag the object around to discover the clue.
    /// </summary>
    private void EnsureAndConfigureClueMarkers(GameObject root, InventoryItem item)
    {
        _activeClueMarkers.AddRange(root.GetComponentsInChildren<InspectableClueMarker>(true));

        if (_activeClueMarkers.Count == 0)
        {
            var fallbackMarker = root.AddComponent<InspectableClueMarker>();
            fallbackMarker.clueID = $"Clue_{item.name}";
            fallbackMarker.clueTitle = !string.IsNullOrEmpty(item.inspectionTitle)
                ? item.inspectionTitle
                : $"Detalye sa {item.itemName}";
            fallbackMarker.clueDescription = !string.IsNullOrEmpty(item.inspectionText)
                ? item.inspectionText
                : GetInspectionLore(item);
            fallbackMarker.revealDirection = Vector3.forward; // Back side (+Z)
            fallbackMarker.detectionAngle = 55f;
            _activeClueMarkers.Add(fallbackMarker);
        }

        // Fix any marker whose revealDirection was set to (0, 0, -1) (which faces the camera at start
        // instead of the back of the object!) so rotating to the back (+Z) reveals the clue as hinted.
        foreach (var clue in _activeClueMarkers)
        {
            if (clue == null) continue;
            clue.detectionAngle = Mathf.Max(clue.detectionAngle, 52f);

            Vector3 dir = clue.revealDirection.normalized;
            if (dir.sqrMagnitude < 0.01f)
            {
                clue.revealDirection = Vector3.forward;
            }
            else if (Vector3.Dot(dir, Vector3.back) > 0.9f)
            {
                // Flip (0, 0, -1) to (0, 0, +1) so the clue is on the back side of the item
                clue.revealDirection = Vector3.forward;
            }
        }
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

        Bounds initialBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            initialBounds.Encapsulate(renderers[i].bounds);
        }

        float maxDim = Mathf.Max(initialBounds.size.x, Mathf.Max(initialBounds.size.y, initialBounds.size.z));
        if (maxDim > 0.001f)
        {
            float targetSize = 0.85f;
            float scaleFactor = targetSize / maxDim;
            root.transform.localScale = Vector3.one * scaleFactor;
        }

        Bounds scaledBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            scaledBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 centerOffset = _modelPivot.position - scaledBounds.center;
        root.transform.position += centerOffset;
    }

    // ── Input & Inspection Loop ──────────────────────────────────────────────

    private void Update()
    {
        if (!_isOpen) return;

        // Ignore close keys on the exact frame the viewer was opened
        if (Time.frameCount == _openedFrame) return;

        HandleInput();
        UpdateRotationAndZoom();
        UpdateClueDetection();
    }

    private void HandleInput()
    {
        // Close on Esc, Tab, or [V] (InspectItem keybind, debounced so it never spams)
        bool closeByInspectKey = (Time.unscaledTime - _lastToggleTime >= 0.25f) &&
                                 (GameSettingsManager.GetKeyDown(GameAction.InspectItem) || Input.GetKeyDown(KeyCode.V));

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || closeByInspectKey)
        {
            Close();
            return;
        }

        // Reset orientation on R
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetOrientation();
        }

        // Examine clue on [SPACE] or [E]
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
        {
            if (_currentlyFacingClue != null)
            {
                if (_clueCardRoot != null && _clueCardRoot.activeSelf)
                    _clueCardRoot.SetActive(false);
                else
                    ExamineCurrentClue();
            }
            else if (_clueCardRoot != null && _clueCardRoot.activeSelf)
            {
                _clueCardRoot.SetActive(false);
            }
        }

        // ── Smooth 360° Click-and-Drag Rotation (Left Mouse OR Right Mouse) ──
        bool mouseDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        bool mouseHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        bool mouseUp = Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1);

        if (mouseDown)
        {
            _isDragging = true;
            _lastMousePos = Input.mousePosition;
            _currentDragDistance = 0f;
            _rotationVelocity = Vector2.zero;
        }
        else if (mouseHeld && _isDragging)
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 pixelDelta = currentMousePos - _lastMousePos;
            _lastMousePos = currentMousePos;
            _currentDragDistance += pixelDelta.magnitude;

            float rotX = pixelDelta.x * PixelDragSensitivity;
            float rotY = pixelDelta.y * PixelDragSensitivity;

            // Fallback to raw mouse axes if cursor is constrained
            if (Mathf.Abs(rotX) < 0.001f && Mathf.Abs(rotY) < 0.001f)
            {
                rotX = Input.GetAxisRaw("Mouse X") * AxisDragSensitivity;
                rotY = Input.GetAxisRaw("Mouse Y") * AxisDragSensitivity;
            }

            if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
            {
                _rotationVelocity = new Vector2(rotX, rotY);
                ApplyRotation(rotX, rotY);
            }
        }
        else if (mouseUp && _isDragging)
        {
            _isDragging = false;

            // If the player clicked without dragging while a clue is facing the camera,
            // allow a simple click to examine the clue!
            if (_currentDragDistance < 6f && _currentlyFacingClue != null)
            {
                // Avoid double-triggering if they clicked directly on the CluePromptBanner button
                GameObject hovered = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (hovered == null || _cluePromptRoot == null || hovered != _cluePromptRoot)
                {
                    ExamineCurrentClue();
                }
            }
        }
        else
        {
            // Keyboard rotation support (WASD / Arrow Keys)
            float keyX = 0f;
            float keyY = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyX += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyY += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyY -= 1f;

            if (Mathf.Abs(keyX) > 0.01f || Mathf.Abs(keyY) > 0.01f)
            {
                float dt = Time.unscaledDeltaTime * KeyboardRotateSpeed;
                ApplyRotation(keyX * dt, keyY * dt);
            }
            else if (_rotationVelocity.sqrMagnitude > 0.001f)
            {
                // Apply smooth momentum decay when mouse drag is released
                ApplyRotation(_rotationVelocity.x, _rotationVelocity.y);
                _rotationVelocity = Vector2.Lerp(_rotationVelocity, Vector2.zero, Time.unscaledDeltaTime * 7f);
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

        Quaternion pitch = Quaternion.AngleAxis(dy, _studioCamera.transform.right);
        Quaternion yaw = Quaternion.AngleAxis(-dx, _studioCamera.transform.up);

        _targetRotation = yaw * pitch * _targetRotation;
    }

    private void UpdateRotationAndZoom()
    {
        _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, Time.unscaledDeltaTime * DampingRate);
        if (_modelPivot != null)
        {
            _modelPivot.localRotation = _currentRotation;
        }

        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.unscaledDeltaTime * 12f);
        if (_studioCamera != null)
        {
            _studioCamera.transform.localPosition = new Vector3(0f, 0.04f, -_currentDistance);
        }

        // Subtle pulse on 3D clue hotspot rings so the player spots them while rotating
        float ringScale = 0.095f + 0.012f * Mathf.Sin(Time.unscaledTime * 5f);
        for (int i = 0; i < _clueHotspotRings.Count; i++)
        {
            if (_clueHotspotRings[i] != null)
                _clueHotspotRings[i].localScale = new Vector3(ringScale, ringScale, ringScale);
        }
    }

    private void ResetOrientation()
    {
        _targetRotation = Quaternion.Euler(8f, -14f, 0f);
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
            }
            if (_cluePromptText != null)
            {
                _cluePromptText.text = $"★ [SPACE / E / CLICK] SURIIN ANG CLUE: {_currentlyFacingClue.clueTitle.ToUpper()}";
            }

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

        // Clear EventSystem selection so Space doesn't double-fire button click
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        _currentlyFacingClue.Discover();
        DiscoveredClueIDs.Add(_currentlyFacingClue.clueID);

        PlayDiscoverySound();

        if (_clueCardRoot != null)
        {
            _clueCardRoot.SetActive(true);
            if (_clueCardTitleText != null) _clueCardTitleText.text = _currentlyFacingClue.clueTitle;
            if (_clueCardDescText != null) _clueCardDescText.text = _currentlyFacingClue.clueDescription;
        }

        UpdateObjectiveForClue(_currentlyFacingClue);
    }

    private void UpdateObjectiveForClue(InspectableClueMarker clue)
    {
        if (ObjectiveHUD.Instance == null || clue == null) return;

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
                    "Ikabit ang VGA cable sa Master Terminal sa ComLab 1 para ma-clear ang terminal hold!"
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
                    "Matandang 1-Piso barya na maaaring gamitin sa campus."
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

        switch (item.itemName)
        {
            case "Locker Key":
                return "Mabigat na brass key na may nakakabit na luma at naninilaw na ivory acrylic tag. " +
                       "May napansin kang manipis na ukit ng numero sa likod ng tag na parang sinadyang itago mula sa ibang estudyante.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng key tag upang suriin ang code.</color>";

            case "Official University Seal Stamp":
            case "Executive Seal Stamp":
                return "Mabigat na stamp na gawa sa solidong mahogany wood at pinakinis na brass collar. " +
                       "Sa ilalim nito ay nakadikit ang makapal na vulcanized rubber die na may opisyal na seal ng Executive Vice President.\n\n" +
                       "<color=#D4AF37>Tip: I-drag upang tingnan ang ilalim ng stamp para sa authorization code.</color>";

            case "ComLab Clearance Form":
                return "Isang wooden clipboard na may hawak na official accountability sheet ng Computer Laboratory. " +
                       "May nakadikit na naninilaw na sticky note sa likod ng board na may sulat-kamay ng ComLab student assistant.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng clipboard para mabasa ang sticky note.</color>";

            case "Library Voucher":
                return "Perforated dot-matrix paper voucher na inilabas ng Lumang Library Archive system noong 1994. " +
                       "May purple stamp sa likod na nagbabala ukol sa mga estudyanteng hindi nakatapos ng clearance bago mag-dilim.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng papel upang suriin ang tatak.</color>";

            case "Blank Paper":
            case "Clearance Slip":
                return "Ang kinatatakutang opisyal na Clearance Form ng Pamantasan. " +
                       "Naglalaman ng anim na bakanteng kahon para sa pirma ng Library, Guidance, ComLab, Clinic, Cashier, at EVP.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot ang papel upang suriin ang watermark sa likod.</color>";

            case "Ham & Cheese Sandwich":
                return "Toasted sandwich mula sa lumang cafeteria na binalot sa mantikang wax paper. " +
                       "Mabango pa rin at malambot ang tinapay. Makatutulong ito upang pakalmahin ang matinding kaba at anxiety ng estudyante.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot upang suriin ang nakabalot na sandwich.</color>";

            case "Flashlight":
                return "Heavy-duty anodized aluminum tactical flashlight na may knurled grip. " +
                       "May nakagasgas na serial number sa takip ng baterya sa ilalim nito na nagpapatunay kung sino ang may-ari nito.\n\n" +
                       "<color=#D4AF37>Tip: I-drag upang tingnan ang tailcap sa ilalim at makita ang serial.</color>";

            case "Student Handbook":
                return "Navy blue leatherette bound handbook na may kupas na gintong selyo ng unibersidad. " +
                       "Sa pinakahuling pahina at sa likod ng pabalat, may nakasulat na desperadong babala ukol sa mga kababalaghan sa campus.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng handbook upang basahin ang babala.</color>";

            case "Printer Paper":
                return "Isang buong kaha (ream) ng 500-sheet Substance 20 bond paper. " +
                       "Naka-seal sa orihinal na asul na wrapper. Kailangan ito ng mga opisina para makapag-print ng mga opisyal na form.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng wrapper upang suriin ang label.</color>";

            case "Lockpin":
                return "Improbisadong lockpick na ginawa mula sa matigas na bobby pin. " +
                       "Binaluktot ang dulo upang magsilbing tension tool para sa mga lumang drawer at padlock.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa gilid upang suriin ang talim ng pin.</color>";

            case "VGA Monitor Cable":
                return "Makapal na asul na video display cable na may 15-pin D-sub connectors at dalawang thumbscrews. " +
                       "Kailangan ito para magka-signal ang offline computer database terminal sa ComLab.\n\n" +
                       "<color=#D4AF37>Tip: I-drag upang tingnan ang ibabaw ng connector boot para sa serial code.</color>";

            case "Ceramic Cartridge Fuse 15A":
                return "Mabigat na ceramic cartridge fuse na may makintab na brass contact caps. " +
                       "Ginagamit sa distribution breaker panel upang maibalik ang kuryente sa ComLab at Faculty offices.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng ceramic body para sa amperage rating.</color>";

            case "Counseling Audio Cassette Tape":
                return "Lumang microcassette tape mula sa guidance records na may markang 'SESSION #04 - ANXIETY'. " +
                       "May nakasulat na sikretong impormasyon sa likod ng tape label.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa Side B ng cassette para sa nakatagong safe code.</color>";

            case "Philippine 1-Peso Coin":
                return "Makintab na 1-piso Pilipino coin (BSP serye na may mukha ni José Rizal). " +
                       "May ukit ng taon at simbolo ng Bangko Sentral sa kabilang mukha nito.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng barya upang suriin ang ukit.</color>";

            case "Security Guard Incident Logbook":
                return "Opisyal na itim na logbook ni Guard Dela Cruz na may ginintuang security star badge. " +
                       "Naglalaman ng emergency lockdown override protocol para sa main entrance.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng pabalat para sa keypad code.</color>";

            case "Clinic Medical Record Slip":
                return "Opisyal na clinical record mula sa University Clinic. " +
                       "May nakatala ritong blood pressure reading at code para sa emergency medicine dispensary cabinet.\n\n" +
                       "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng chart para sa cabinet code.</color>";

            default:
                if (item.itemName.StartsWith("Fragment"))
                {
                    return "Punit na piraso ng opisyal na student clearance slip. " +
                           "May mga jagged edges at sulat ng ballpen sa likod na tumutukoy kung saan ito dapat itugma sa ibang piraso.\n\n" +
                           "<color=#D4AF37>Tip: I-drag pa-ikot sa likod ng piraso upang mabasa ang clue.</color>";
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

    private void PlaySoftHandleSound(float volume = 0.25f)
    {
        if (_audioSource == null || _softHandleAudio == null) return;
        if (Time.unscaledTime - _lastSoundTime < 0.25f) return;

        _lastSoundTime = Time.unscaledTime;
        _audioSource.Stop();
        _audioSource.pitch = Random.Range(0.96f, 1.04f);
        _audioSource.PlayOneShot(_softHandleAudio, volume);
    }

    private void PlayDiscoverySound()
    {
        if (_audioSource != null && _discoveryChimeAudio != null)
        {
            _audioSource.pitch = 1.0f;
            _audioSource.PlayOneShot(_discoveryChimeAudio, 0.75f);
        }
    }
}
