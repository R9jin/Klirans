using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PauseMenu — ESC pauses the game and shows a styled panel matching the main menu.
/// Builds all UI entirely in code — no prefab required.
/// Style: "watch people die" horror font, dark navy/charcoal panel, crimson red accents.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    // ── References (auto-built or inspector-assigned) ─────────────────────────
    [Header("UI Panels")]
    public GameObject pauseMenuUI;
    private GameObject _pausePanelRoot;
    private Canvas     _pauseCanvas;

    // ── Cached player refs ────────────────────────────────────────────────────
    private PlayerMovement _playerMovement;

    // ── Fonts ─────────────────────────────────────────────────────────────────
    private Font _horrorFont;  // "watch people die"
    private Font _bodyFont;    // LegacyRuntime fallback

    // ── Colour palette (matches main menu) ────────────────────────────────────
    private static readonly Color ColPanel      = new Color(0.04f, 0.04f, 0.06f, 0.94f);   // near-black navy
    private static readonly Color ColOverlay    = new Color(0f,    0f,    0f,    0.55f);    // dim backdrop
    private static readonly Color ColTitle      = new Color(0.96f, 0.96f, 0.96f, 1f);      // bright off-white
    private static readonly Color ColSubtitle   = new Color(0.85f, 0.22f, 0.22f, 1f);      // crimson red
    private static readonly Color ColBtnText    = new Color(0.88f, 0.88f, 0.88f, 1f);      // light grey
    private static readonly Color ColBtnHover   = new Color(1f,    0.25f, 0.25f, 1f);      // red hover
    private static readonly Color ColLabel      = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color ColSliderFill = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color ColSliderBg   = new Color(0.20f, 0.20f, 0.25f, 1f);

    // ── Volume / FOV ──────────────────────────────────────────────────────────
    private Slider _volumeSlider;
    private Text   _volumeValueText;
    private Slider _fovSlider;
    private Text   _fovValueText;
    private Slider _sensitivitySlider;
    private Text   _sensitivityValueText;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        LoadFonts();
        if (pauseMenuUI != null)
        {
            _pausePanelRoot = pauseMenuUI;
        }
        else
        {
            BuildPauseUI();
            pauseMenuUI = _pausePanelRoot;
        }
        SetPaused(false);
    }

    private void Start()
    {
        GameIsPaused = false;
        Time.timeScale = 1f;

        _playerMovement = FindAnyObjectByType<PlayerMovement>();

        // Init sliders from saved prefs
        float vol  = PlayerPrefs.GetFloat("MasterVolume",    1.0f);
        float fov  = PlayerPrefs.GetFloat("CameraFOV",       60f);
        float sens = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);

        AudioListener.volume = vol;

        if (_volumeSlider      != null) { _volumeSlider.value      = vol;  }
        if (_fovSlider         != null) { _fovSlider.value         = fov;  }
        if (_sensitivitySlider != null) { _sensitivitySlider.value  = sens; }

        UpdateVolumeText(vol);
        UpdateFOVText(fov);
        UpdateSensText(sens);
        ApplyFOV(fov);
    }

    private void Update()
    {
        // Don't allow pausing during dialogue or jumpscare
        bool dialogueOpen = NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive;
        if (dialogueOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pause Logic
    // ─────────────────────────────────────────────────────────────────────────

    public void TogglePause()
    {
        if (GameIsPaused) Resume(); else Pause();
    }

    public void Resume()
    {
        SetPaused(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Pause()
    {
        SetPaused(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("[PauseMenu] Quit");
        Application.Quit();
    }

    private void SetPaused(bool paused)
    {
        if (_pausePanelRoot != null) _pausePanelRoot.SetActive(paused);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Construction
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadFonts()
    {
        _horrorFont = UnityEditor_LoadFont("Assets/Main Menu/watch people die.ttf");
        if (_horrorFont == null) _horrorFont = Resources.Load<Font>("Fonts/Typewriter_Bold");
        if (_horrorFont == null) _horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Font UnityEditor_LoadFont(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(path);
#else
        return null;
#endif
    }

    private void BuildPauseUI()
    {
        // ── Root canvas on this GameObject ────────────────────────────────────
        _pauseCanvas = gameObject.GetComponent<Canvas>();
        if (_pauseCanvas == null) _pauseCanvas = gameObject.AddComponent<Canvas>();
        _pauseCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _pauseCanvas.sortingOrder = 500; // above HUD but below loading screen

        var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // ── Dark fullscreen overlay ───────────────────────────────────────────
        var overlay = MakeImage("PauseOverlay", transform, ColOverlay);
        StretchFull(overlay.rectTransform);
        overlay.raycastTarget = true; // blocks clicks behind

        // ── Left-side panel (same layout as main menu — left 44% of screen) ──
        _pausePanelRoot = new GameObject("PausePanel");
        _pausePanelRoot.transform.SetParent(transform, false);
        var panelRT = _pausePanelRoot.AddComponent<RectTransform>();
        panelRT.anchorMin      = new Vector2(0f, 0f);
        panelRT.anchorMax      = new Vector2(0.44f, 1f);
        panelRT.offsetMin      = Vector2.zero;
        panelRT.offsetMax      = Vector2.zero;

        var panelImg = _pausePanelRoot.AddComponent<Image>();
        panelImg.color = ColPanel;
        panelImg.raycastTarget = true;

        // ── Left vignette gradient (decorative, matches main menu) ────────────
        var gradSprite = Resources.Load<Sprite>("MenuLeftGradient");
        if (gradSprite == null)
        {
            var gradGuids = UnityEditor_FindAsset("MenuLeftGradient");
            if (gradGuids != null) gradSprite = UnityEditor_LoadSprite(gradGuids);
        }
        if (gradSprite != null)
        {
            var grad = MakeImage("LeftGrad", transform, Color.white);
            grad.sprite = gradSprite;
            grad.raycastTarget = false;
            var grt = grad.rectTransform;
            grt.anchorMin = new Vector2(0f, 0f);
            grt.anchorMax = new Vector2(0.44f, 1f);
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;
        }

        // ── Title: "PAUSED" ───────────────────────────────────────────────────
        var titleBox = new GameObject("TitleBox");
        titleBox.transform.SetParent(_pausePanelRoot.transform, false);
        var titleBoxRT = titleBox.AddComponent<RectTransform>();
        titleBoxRT.anchorMin = new Vector2(0f, 0.62f);
        titleBoxRT.anchorMax = new Vector2(1f, 0.95f);
        titleBoxRT.offsetMin = new Vector2(48f, 0f);
        titleBoxRT.offsetMax = new Vector2(-12f, 0f);

        MakeText("GameTitleText", titleBox.transform, "PAUSED", _horrorFont, 88, ColTitle, TextAnchor.LowerLeft);
        MakeText("SubtitleText",  titleBox.transform, "GAME PAUSED  |  KLIRANS",
                 _horrorFont, 18, ColSubtitle, TextAnchor.UpperLeft);

        // ── Buttons panel ─────────────────────────────────────────────────────
        var btnPanel = new GameObject("ButtonsPanel");
        btnPanel.transform.SetParent(_pausePanelRoot.transform, false);
        var btnPanelRT = btnPanel.AddComponent<RectTransform>();
        btnPanelRT.anchorMin = new Vector2(0f, 0.06f);
        btnPanelRT.anchorMax = new Vector2(1f, 0.58f);
        btnPanelRT.offsetMin = new Vector2(48f, 0f);
        btnPanelRT.offsetMax = new Vector2(-12f, 0f);

        var vlg = btnPanel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.UpperLeft;
        vlg.spacing              = 0f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.padding              = new RectOffset(0, 0, 0, 0);

        MakeMenuButton(btnPanel.transform, "Resume",       () => Resume());
        MakeMenuButton(btnPanel.transform, "Main Menu",    () => LoadMenu());
        MakeMenuButton(btnPanel.transform, "Quit",         () => QuitGame());

        // ── Settings strip at the bottom of the panel ─────────────────────────
        BuildSettingsStrip(_pausePanelRoot.transform);
    }

    private void BuildSettingsStrip(Transform parent)
    {
        var strip = new GameObject("SettingsStrip");
        strip.transform.SetParent(parent, false);
        var rt = strip.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.01f);
        rt.anchorMax = new Vector2(1f, 0.20f);
        rt.offsetMin = new Vector2(48f, 0f);
        rt.offsetMax = new Vector2(-20f, 0f);

        var vlg = strip.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 6f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;

        // Volume
        var volRow = MakeSliderRow(strip.transform, "VOLUME",
            0f, 1f, PlayerPrefs.GetFloat("MasterVolume", 1f),
            out _volumeSlider, out _volumeValueText);
        _volumeSlider.onValueChanged.AddListener(val => {
            AudioListener.volume = val;
            PlayerPrefs.SetFloat("MasterVolume", val);
            PlayerPrefs.Save();
            UpdateVolumeText(val);
        });

        // FOV
        var fovRow = MakeSliderRow(strip.transform, "FOV",
            50f, 110f, PlayerPrefs.GetFloat("CameraFOV", 60f),
            out _fovSlider, out _fovValueText);
        _fovSlider.onValueChanged.AddListener(val => {
            ApplyFOV(val);
            PlayerPrefs.SetFloat("CameraFOV", val);
            PlayerPrefs.Save();
            UpdateFOVText(val);
        });

        // Sensitivity
        var sensRow = MakeSliderRow(strip.transform, "SENSITIVITY",
            0.5f, 5f, PlayerPrefs.GetFloat("MouseSensitivity", 2f),
            out _sensitivitySlider, out _sensitivityValueText);
        _sensitivitySlider.onValueChanged.AddListener(val => {
            if (_playerMovement != null) _playerMovement.lookSpeed = val;
            PlayerPrefs.SetFloat("MouseSensitivity", val);
            PlayerPrefs.Save();
            UpdateSensText(val);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Widget Factories
    // ─────────────────────────────────────────────────────────────────────────

    private void MakeMenuButton(Transform parent, string label, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 62f;
        le.flexibleWidth   = 1f;

        var btn = go.AddComponent<Button>();
        var img = go.AddComponent<Image>();
        img.color = Color.clear; // transparent — text-only like main menu

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(go.transform, false);
        var txt = txtObj.AddComponent<Text>();
        txt.text       = label;
        txt.font       = _horrorFont;
        txt.fontSize   = 38;
        txt.color      = ColBtnText;
        txt.alignment  = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;

        var txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        // Colour tint on hover / click
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = ColBtnHover;
        cols.pressedColor     = new Color(0.7f, 0.1f, 0.1f, 1f);
        cols.fadeDuration     = 0.1f;
        btn.colors = cols;
        btn.targetGraphic = txt;

        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private GameObject MakeSliderRow(Transform parent, string labelText,
        float min, float max, float value,
        out Slider slider, out Text valText)
    {
        var row = new GameObject($"Row_{labelText}");
        row.transform.SetParent(parent, false);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;
        le.flexibleWidth   = 1f;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 10f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = true;

        // Label
        var lbl = MakeText("Label", row.transform, labelText, _bodyFont, 15, ColLabel, TextAnchor.MiddleLeft);
        var lblLE = lbl.gameObject.AddComponent<LayoutElement>();
        lblLE.preferredWidth = 130f;

        // Slider
        slider = MakeSlider(row.transform, min, max, value);
        var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
        sliderLE.preferredWidth  = 160f;
        sliderLE.flexibleWidth   = 1f;

        // Value text
        valText = MakeText("ValText", row.transform, "", _bodyFont, 14, ColLabel, TextAnchor.MiddleCenter);
        var valLE = valText.gameObject.AddComponent<LayoutElement>();
        valLE.preferredWidth = 48f;

        return row;
    }

    private Slider MakeSlider(Transform parent, float min, float max, float value)
    {
        var go = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value    = value;

        // Background
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = ColSliderBg;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = new Vector2(5f, 0f);
        faRT.offsetMax = new Vector2(-15f, 0f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = ColSliderFill;
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = new Vector2(10f, 0f);

        // Handle slide area
        var handleSlide = new GameObject("Handle Slide Area");
        handleSlide.transform.SetParent(go.transform, false);
        var hsRT = handleSlide.AddComponent<RectTransform>();
        hsRT.anchorMin = new Vector2(0f, 0f);
        hsRT.anchorMax = new Vector2(1f, 1f);
        hsRT.offsetMin = new Vector2(10f, 0f);
        hsRT.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleSlide.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRT = handle.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20f, 0f);

        slider.fillRect   = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction  = Slider.Direction.LeftToRight;

        return slider;
    }

    private Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Text MakeText(string name, Transform parent, string content, Font font, int size, Color color, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = font;
        txt.fontSize  = size;
        txt.color     = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;
        StretchFull(go.GetComponent<RectTransform>());
        return txt;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyFOV(float fov)
    {
        if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerMovement != null)
        {
            _playerMovement.normalFOV = fov;
            if (_playerMovement.playerCamera != null)
                _playerMovement.playerCamera.fieldOfView = fov;
        }
    }

    private void UpdateVolumeText(float v)
    {
        if (_volumeValueText != null) _volumeValueText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private void UpdateFOVText(float v)
    {
        if (_fovValueText != null) _fovValueText.text = Mathf.RoundToInt(v) + "°";
    }

    private void UpdateSensText(float v)
    {
        if (_sensitivityValueText != null) _sensitivityValueText.text = v.ToString("F1");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor asset helpers (compile-safe for builds)
    // ─────────────────────────────────────────────────────────────────────────

    private string UnityEditor_FindAsset(string name)
    {
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets(name);
        return guids.Length > 0 ? UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]) : null;
#else
        return null;
#endif
    }

    private Sprite UnityEditor_LoadSprite(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }
}
