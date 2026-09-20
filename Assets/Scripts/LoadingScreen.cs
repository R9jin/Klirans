using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LoadingScreen — handles async scene transition with a full black overlay
/// and an animated "LOADING..." text in the bottom-right corner.
///
/// Usage: Call LoadingScreen.LoadScene("SampleScene") from anywhere.
/// The loading screen will appear immediately, load the scene in the background,
/// then hand off to the new scene once it's fully ready.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("UI References (auto-created if null)")]
    [Tooltip("Full-screen black panel. Auto-created at runtime if not assigned.")]
    public Image blackPanel;

    [Tooltip("The 'LOADING...' label at bottom-right.")]
    public Text loadingLabel;

    [Header("Visual Settings")]
    [Tooltip("Seconds for the black panel to fade in before loading starts.")]
    public float fadeInDuration = 0.35f;
    [Tooltip("Minimum seconds to show the loading screen (prevents flash for fast loads).")]
    public float minimumDisplayTime = 1.2f;

    [Header("Dot Animation")]
    [Tooltip("Seconds between each dot being added (e.g. LOADING. → LOADING.. → LOADING...)")]
    public float dotInterval = 0.42f;
    [Tooltip("Maximum dots before cycling back.")]
    public int maxDots = 3;

    // ── State ──────────────────────────────────────────────────────────────────
    private Canvas _canvas;
    private bool _isLoading;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUI();
        // Start hidden
        SetVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the loading screen and asynchronously loads the target scene.
    /// Safe to call from any MonoBehaviour.
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        if (Instance == null)
        {
            // Auto-create if not already in scene
            var go = new GameObject("LoadingScreen");
            Instance = go.AddComponent<LoadingScreen>();
        }
        if (!Instance._isLoading)
            Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneName));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        _isLoading = true;
        SetVisible(true);

        // ── 1. Fade in black panel ────────────────────────────────────────────
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(t / fadeInDuration);
            SetPanelAlpha(alpha);
            yield return null;
        }
        SetPanelAlpha(1f);

        // ── 2. Begin async load (don't activate yet) ──────────────────────────
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // ── 3. Show animated loading text while loading ───────────────────────
        float minTimer = 0f;
        float dotTimer = 0f;
        int dots = 0;

        while (!op.isDone)
        {
            minTimer += Time.unscaledDeltaTime;
            dotTimer += Time.unscaledDeltaTime;

            // Animate dots
            if (dotTimer >= dotInterval)
            {
                dotTimer = 0f;
                dots = (dots % maxDots) + 1;
                UpdateLoadingText(dots);
            }

            // Allow activation once fully loaded AND minimum display time has passed
            if (op.progress >= 0.9f && minTimer >= minimumDisplayTime)
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // Scene has now activated — loading screen will persist briefly then hide
        // (The DontDestroyOnLoad object survives the scene transition)
        SetVisible(false);
        _isLoading = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // Always on top of everything

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // ── Black Panel ───────────────────────────────────────────────────────
        if (blackPanel == null)
        {
            var panelGo = new GameObject("BlackPanel");
            panelGo.transform.SetParent(transform, false);
            blackPanel = panelGo.AddComponent<Image>();
            blackPanel.color = Color.black;

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Loading Label — bottom-right ──────────────────────────────────────
        if (loadingLabel == null)
        {
            var labelGo = new GameObject("LoadingLabel");
            labelGo.transform.SetParent(transform, false);
            loadingLabel = labelGo.AddComponent<Text>();

            // Font — load Typewriter_Bold; fall back to LegacyRuntime
            var font = Resources.Load<Font>("Fonts/Typewriter_Bold");
#if UNITY_EDITOR
            if (font == null)
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            loadingLabel.font = font;
            loadingLabel.fontSize = 22;
            loadingLabel.fontStyle = FontStyle.Bold;
            loadingLabel.color = new Color(0.75f, 0.72f, 0.68f, 1f); // warm aged-paper off-white
            loadingLabel.alignment = TextAnchor.LowerRight;
            loadingLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            loadingLabel.verticalOverflow = VerticalWrapMode.Overflow;
            loadingLabel.text = "LOADING";

            // Add subtle shadow / outline for legibility on solid black
            var shadow = labelGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Bottom-right anchor, small inset from edge
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-42f, 32f); // 42px from right, 32px from bottom
            rt.sizeDelta = new Vector2(320f, 40f);
        }
    }

    private void SetVisible(bool visible)
    {
        if (blackPanel != null) blackPanel.gameObject.SetActive(visible);
        if (loadingLabel != null) loadingLabel.gameObject.SetActive(visible);
    }

    private void SetPanelAlpha(float alpha)
    {
        if (blackPanel != null)
        {
            var c = blackPanel.color;
            c.a = alpha;
            blackPanel.color = c;
        }
    }

    private void UpdateLoadingText(int dots)
    {
        if (loadingLabel == null) return;
        loadingLabel.text = "LOADING" + new string('.', dots);
    }
}
