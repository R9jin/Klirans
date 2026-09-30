using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the ANXIETY meter on the player HUD.
/// Automatically connects to AnxietyManager and creates/binds UI on HudCanvas.
/// Styled in the retro survival-horror aesthetic with dark borders, crimson blood fill,
/// and trauma pulse feedback on anxiety spikes.
/// </summary>
public class AnxietyUI : MonoBehaviour
{
    public static AnxietyUI Instance { get; private set; }

    [Header("UI References (Optional - Auto-Built if null)")]
    public CanvasGroup rootCanvasGroup;
    public Image barBackground;
    public Image barFill;
    public Text labelText;
    public Text valueText;

    [Header("Styling")]
    public Color normalFillColor = new Color(0.78f, 0.12f, 0.12f, 0.95f);    // Deep dried blood crimson
    public Color highAnxietyColor = new Color(0.95f, 0.05f, 0.05f, 1.0f);     // Burning alarm crimson
    public Color panicPulseColor = new Color(1.0f, 0.85f, 0.85f, 1.0f);      // Flash white-red
    public Color labelColor = new Color(0.88f, 0.82f, 0.82f, 1.0f);          // Distressed parchment off-white

    [Header("Animation Settings")]
    public float smoothFillSpeed = 4f;
    public float pulseDuration = 0.5f;

    private float _targetFill = 0f;
    private Coroutine _pulseCoroutine;
    private RectTransform _meterRect;
    private Vector2 _originalMeterPos;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnsureUIConstructed();

        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
            UpdateDisplay(AnxietyManager.Instance.CurrentAnxiety, AnxietyManager.Instance.maxAnxiety, instant: true);
        }
    }

    private void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;
        }
    }

    private void Update()
    {
        if (barFill != null)
        {
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, _targetFill, Time.deltaTime * smoothFillSpeed);
        }
    }

    private void HandleAnxietyChanged(float current, float max)
    {
        UpdateDisplay(current, max, instant: false);
    }

    public void UpdateDisplay(float current, float max, bool instant)
    {
        _targetFill = Mathf.Clamp01(current / Mathf.Max(1f, max));
        if (instant && barFill != null)
        {
            barFill.fillAmount = _targetFill;
        }

        int percentage = Mathf.RoundToInt(_targetFill * 100f);

        if (valueText != null)
        {
            valueText.text = $"{percentage}%";
        }

        if (barFill != null)
        {
            Color targetCol = (_targetFill >= 0.70f) ? highAnxietyColor : normalFillColor;
            barFill.color = targetCol;
        }

        if (!instant && current > 0f)
        {
            TriggerPulse();
        }
    }

    public void TriggerPulse()
    {
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float elapsed = 0f;
        Color baseCol = (_targetFill >= 0.70f) ? highAnxietyColor : normalFillColor;

        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pulseDuration;

            // Flash bar color
            if (barFill != null)
            {
                barFill.color = Color.Lerp(panicPulseColor, baseCol, t);
            }

            // Subtle shudder of the meter frame
            if (_meterRect != null)
            {
                float shake = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 3f;
                _meterRect.anchoredPosition = _originalMeterPos + new Vector2(shake, 0f);
            }

            yield return null;
        }

        if (barFill != null) barFill.color = baseCol;
        if (_meterRect != null) _meterRect.anchoredPosition = _originalMeterPos;
    }

    /// <summary>
    /// Programmatically builds the Anxiety HUD frame on HudCanvas if not assigned via Inspector.
    /// </summary>
    private void EnsureUIConstructed()
    {
        if (barFill != null && labelText != null)
        {
            _meterRect = barFill.transform.parent as RectTransform;
            if (_meterRect != null) _originalMeterPos = _meterRect.anchoredPosition;
            return;
        }

        var hud = GameObject.Find("HudCanvas");
        if (hud == null)
        {
            Debug.LogWarning("[AnxietyUI] HudCanvas not found in scene. Meter will not be visible.");
            return;
        }

        // Font
        Font horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (loadedFont != null) horrorFont = loadedFont;
#endif

        // Root container - Positioned at Bottom-Left, above Stamina bar
        var rootGO = new GameObject("AnxietyMeter", typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(hud.transform, false);
        _meterRect = rootGO.GetComponent<RectTransform>();
        _meterRect.anchorMin = new Vector2(0f, 0f);
        _meterRect.anchorMax = new Vector2(0f, 0f);
        _meterRect.pivot = new Vector2(0f, 0f);
        _meterRect.anchoredPosition = new Vector2(36f, 76f); // Sits comfortably above bottom hotbar/stamina
        _meterRect.sizeDelta = new Vector2(240f, 32f);
        _originalMeterPos = _meterRect.anchoredPosition;

        rootCanvasGroup = rootGO.GetComponent<CanvasGroup>();

        // Label: "ANXIETY"
        var labelGO = new GameObject("AnxietyLabel", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(rootGO.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, 18f);
        labelRT.sizeDelta = new Vector2(0f, 20f);

        labelText = labelGO.GetComponent<Text>();
        labelText.text = "ANXIETY";
        labelText.font = horrorFont;
        labelText.fontSize = 15;
        labelText.color = labelColor;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Value text: "[ 0% ]"
        var valGO = new GameObject("AnxietyValue", typeof(RectTransform), typeof(Text));
        valGO.transform.SetParent(rootGO.transform, false);
        var valRT = valGO.GetComponent<RectTransform>();
        valRT.anchorMin = new Vector2(1f, 1f);
        valRT.anchorMax = new Vector2(1f, 1f);
        valRT.pivot = new Vector2(1f, 1f);
        valRT.anchoredPosition = new Vector2(0f, 18f);
        valRT.sizeDelta = new Vector2(80f, 20f);

        valueText = valGO.GetComponent<Text>();
        valueText.text = "0%";
        valueText.font = horrorFont;
        valueText.fontSize = 15;
        valueText.color = labelColor;
        valueText.alignment = TextAnchor.MiddleRight;

        // Background Border Frame (Dark charcoal frame)
        var frameGO = new GameObject("BarBorder", typeof(RectTransform), typeof(Image));
        frameGO.transform.SetParent(rootGO.transform, false);
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.sizeDelta = Vector2.zero;
        var frameImg = frameGO.GetComponent<Image>();
        frameImg.color = new Color(0.12f, 0.12f, 0.14f, 0.90f);

        // Inner Background track (near-black cavity)
        var trackGO = new GameObject("BarTrack", typeof(RectTransform), typeof(Image));
        trackGO.transform.SetParent(frameGO.transform, false);
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = Vector2.zero;
        trackRT.anchorMax = Vector2.one;
        trackRT.offsetMin = new Vector2(2f, 2f);
        trackRT.offsetMax = new Vector2(-2f, -2f);
        barBackground = trackGO.GetComponent<Image>();
        barBackground.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);

        // Fill bar (Horizontal image fill)
        var fillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(trackGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        barFill = fillGO.GetComponent<Image>();
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 0f;
        barFill.color = normalFillColor;
    }
}
