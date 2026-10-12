using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ItemInspectionModal — Authentic survival horror document & notebook reading modal.
/// Renders an aged notebook/parchment window on HudCanvas displaying item text, notes, clues, and university records.
/// </summary>
public class ItemInspectionModal : MonoBehaviour
{
    private static ItemInspectionModal _instance;
    public static ItemInspectionModal Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ItemInspectionModal>();
                if (_instance == null)
                {
                    var hud = GameObject.Find("HudCanvas");
                    var go = new GameObject("ItemInspectionController");
                    if (hud != null) go.transform.SetParent(hud.transform, false);
                    _instance = go.AddComponent<ItemInspectionModal>();
                }
            }
            return _instance;
        }
    }

    public static bool IsOpen => _instance != null && _instance._isOpen;

    // ── UI References ─────────────────────────────────────────────────────────
    private GameObject _overlayRoot;
    private CanvasGroup _canvasGroup;
    private Text _titleText;
    private Text _bodyText;
    private Image _iconImage;
    private ScrollRect _scrollRect;
    private AudioSource _audioSource;

    private bool _isOpen = false;
    private bool _wasInventoryOpenBefore = false;
    private int _openedFrame = -1;
    private CursorLockMode _previousLockMode;
    private bool _previousCursorVisible;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;

        BuildUI();
        SetVisible(false, instant: true);
    }

    private void Update()
    {
        if (!_isOpen) return;

        // Ignore close keys on the same frame the modal was opened to prevent instant close
        if (Time.frameCount == _openedFrame) return;

        // Close on E, Space, Return, Tab, or Escape
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the inspection modal for a specific InventoryItem.
    /// </summary>
    public static void Inspect(InventoryItem item)
    {
        if (item == null) return;

        string title = !string.IsNullOrEmpty(item.inspectionTitle) ? item.inspectionTitle : item.itemName;
        string content = !string.IsNullOrEmpty(item.inspectionText) ? item.inspectionText : item.description;

        Instance.OpenModal(title, content, item.icon);
    }

    public void OpenModal(string title, string content, Sprite icon = null)
    {
        if (_overlayRoot == null) BuildUI();

        _openedFrame = Time.frameCount;
        _wasInventoryOpenBefore = (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen);
        _previousLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_titleText != null) _titleText.text = title.ToUpper();
        if (_bodyText != null) _bodyText.text = content;

        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.gameObject.SetActive(icon != null);
        }

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f; // Scroll to top
        }

        PlayPaperSound();
        SetVisible(true);
        _isOpen = true;

        // Freeze player movement while reading
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.SetControlsEnabled(false);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        PlayPaperSound();
        SetVisible(false);

        // Always restore player controls (unless game is paused or player is hidden inside a locker)
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null && !PauseMenu.GameIsPaused && !LockerHideManager.IsPlayerHidden)
        {
            pm.SetControlsEnabled(true);
        }

        // Restore cursor state based on whether inventory is still open
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

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        // Load authentic typewriter font and horror heading font
        Font typewriterFont = Resources.Load<Font>("Fonts/Typewriter_Bold");
        if (typewriterFont == null) typewriterFont = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
        if (typewriterFont == null)
            typewriterFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
        if (typewriterFont == null) typewriterFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Font horrorHeadingFont = Resources.Load<Font>("Fonts/watch people die");
#if UNITY_EDITOR
        if (horrorHeadingFont == null)
            horrorHeadingFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
#endif
        if (horrorHeadingFont == null) horrorHeadingFont = typewriterFont;

        _overlayRoot = new GameObject("ItemInspectionOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        _overlayRoot.transform.SetParent(hud.transform, false);

        var rootRT = _overlayRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        var bgImg = _overlayRoot.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.03f, 0.85f); // Deep dark atmosphere backdrop

        // Click outside the paper to close
        var backdropBtn = _overlayRoot.GetComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Close);

        _canvasGroup = _overlayRoot.GetComponent<CanvasGroup>();

        // ── Parchment / Notebook Container ───────────────────────────────────
        var paperGO = new GameObject("NotebookPage", typeof(RectTransform), typeof(Image), typeof(Outline));
        paperGO.transform.SetParent(_overlayRoot.transform, false);

        var paperRT = paperGO.GetComponent<RectTransform>();
        paperRT.anchorMin = new Vector2(0.5f, 0.5f);
        paperRT.anchorMax = new Vector2(0.5f, 0.5f);
        paperRT.pivot = new Vector2(0.5f, 0.5f);
        paperRT.sizeDelta = new Vector2(740f, 560f);

        var paperImg = paperGO.GetComponent<Image>();
        paperImg.color = new Color(0.96f, 0.93f, 0.86f, 0.98f); // Aged cream student paper

        var paperOutline = paperGO.GetComponent<Outline>();
        paperOutline.effectColor = new Color(0.20f, 0.16f, 0.12f, 0.90f);
        paperOutline.effectDistance = new Vector2(3f, -3f);

        // Red student notebook margin line (classic school notebook detail)
        var marginLine = new GameObject("NotebookMarginRule", typeof(RectTransform), typeof(Image));
        marginLine.transform.SetParent(paperGO.transform, false);
        var marginRT = marginLine.GetComponent<RectTransform>();
        marginRT.anchorMin = new Vector2(0f, 0f);
        marginRT.anchorMax = new Vector2(0f, 1f);
        marginRT.pivot = new Vector2(0f, 0.5f);
        marginRT.anchoredPosition = new Vector2(38f, 0f);
        marginRT.sizeDelta = new Vector2(2f, -30f);
        marginLine.GetComponent<Image>().color = new Color(0.85f, 0.28f, 0.28f, 0.35f);

        // Header Banner
        var headerGO = new GameObject("HeaderBanner", typeof(RectTransform));
        headerGO.transform.SetParent(paperGO.transform, false);
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -22f);
        headerRT.sizeDelta = new Vector2(-70f, 65f);

        // Icon Image
        var iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(headerGO.transform, false);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0f, 0f);
        iconRT.sizeDelta = new Vector2(48f, 48f);
        _iconImage = iconGO.GetComponent<Image>();
        _iconImage.preserveAspect = true;

        // Title Text
        var titleGO = new GameObject("InspectionTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(headerGO.transform, false);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(58f, 0f);
        titleRT.offsetMax = Vector2.zero;

        _titleText = titleGO.GetComponent<Text>();
        _titleText.font = typewriterFont;
        _titleText.fontSize = 20;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.color = new Color(0.12f, 0.08f, 0.06f, 1f);
        _titleText.alignment = TextAnchor.MiddleLeft;

        // Horizontal Separator Line
        var sepGO = new GameObject("Separator", typeof(RectTransform), typeof(Image));
        sepGO.transform.SetParent(paperGO.transform, false);
        var sepRT = sepGO.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0f, 1f);
        sepRT.anchorMax = new Vector2(1f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -90f);
        sepRT.sizeDelta = new Vector2(-70f, 2f);
        sepGO.GetComponent<Image>().color = new Color(0.35f, 0.30f, 0.25f, 0.5f);

        // ── Scrollable Body Text (Uses RectMask2D — guarantees text is never clipped by failed stencil buffer!) ──
        var scrollGO = new GameObject("TextScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGO.transform.SetParent(paperGO.transform, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(52f, 68f);
        scrollRT.offsetMax = new Vector2(-36f, -100f);

        _scrollRect = scrollGO.GetComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 32f;

        // Content
        var contentGO = new GameObject("ScrollContent", typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 400f);

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(0, 16, 4, 12);

        _scrollRect.content = contentRT;

        // Body Text
        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(Text));
        bodyGO.transform.SetParent(contentGO.transform, false);

        _bodyText = bodyGO.GetComponent<Text>();
        _bodyText.font = typewriterFont;
        _bodyText.fontSize = 17;
        _bodyText.lineSpacing = 1.30f;
        _bodyText.color = new Color(0.12f, 0.10f, 0.08f, 0.95f);
        _bodyText.alignment = TextAnchor.UpperLeft;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        _bodyText.supportRichText = true;

        // ── Footer Close Button ───────────────────────────────────────────────
        var btnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(paperGO.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0f);
        btnRT.anchorMax = new Vector2(0.5f, 0f);
        btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 16f);
        btnRT.sizeDelta = new Vector2(240f, 38f);

        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color(0.18f, 0.14f, 0.10f, 0.95f);

        var btnOutline = btnGO.GetComponent<Outline>();
        btnOutline.effectColor = new Color(0.85f, 0.70f, 0.35f, 0.6f);

        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(Close);

        var btnTextGO = new GameObject("BtnText", typeof(RectTransform), typeof(Text));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;

        var btnText = btnTextGO.GetComponent<Text>();
        btnText.font = typewriterFont;
        btnText.fontSize = 15;
        btnText.fontStyle = FontStyle.Bold;
        btnText.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.text = "[E] Close Note";
    }

    private static AudioClip _cachedRustleClip;

    private void PlayPaperSound()
    {
        if (_audioSource == null) return;
        if (_cachedRustleClip == null)
        {
#if UNITY_EDITOR
            _cachedRustleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
            if (_cachedRustleClip == null)
                _cachedRustleClip = Resources.Load<AudioClip>("Sounds/objective_chime");
        }

        if (_cachedRustleClip != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(_cachedRustleClip, 0.65f);
        }
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        if (_overlayRoot == null) return;
        _overlayRoot.SetActive(visible);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
