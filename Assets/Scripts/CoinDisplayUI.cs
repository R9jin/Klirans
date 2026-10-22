using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CoinDisplayUI — Renders the player's coin wallet in the top-right corner of the HUD.
/// Automatically creates and connects its UI components on HudCanvas.
/// Features smooth bounce feedback and "+₱1" popup floaters upon collecting coins.
/// </summary>
public class CoinDisplayUI : MonoBehaviour
{
    public static CoinDisplayUI Instance { get; private set; }

    private RectTransform _rootRect;
    private Text _coinText;
    private Text _plusFloaterText;
    private RectTransform _coinIconRect;
    private Coroutine _bounceCoroutine;
    private Coroutine _floaterCoroutine;

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

        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            UpdateDisplay(CoinManager.Instance.CurrentCoins, instant: true);
        }
    }

    private void OnDestroy()
    {
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    private void HandleCoinsChanged(int newCount)
    {
        UpdateDisplay(newCount, instant: false);
    }

    public void ShowFloater(string text)
    {
        if (_plusFloaterText == null) return;
        if (_floaterCoroutine != null) StopCoroutine(_floaterCoroutine);
        _floaterCoroutine = StartCoroutine(AnimateFloater(text));
    }

    private IEnumerator AnimateFloater(string text)
    {
        _plusFloaterText.text = text;
        _plusFloaterText.gameObject.SetActive(true);

        RectTransform rt = _plusFloaterText.rectTransform;
        Vector2 startPos = new Vector2(-10f, -48f);
        Vector2 endPos = new Vector2(-10f, -18f);

        float duration = 1.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
            Color c = _plusFloaterText.color;
            c.a = Mathf.Clamp01(1f - (t * t));
            _plusFloaterText.color = c;

            yield return null;
        }

        _plusFloaterText.gameObject.SetActive(false);
    }

    private void UpdateDisplay(int amount, bool instant)
    {
        if (_coinText != null)
        {
            _coinText.text = $"₱ {amount}";
        }

        if (!instant && _coinIconRect != null)
        {
            if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
            _bounceCoroutine = StartCoroutine(BounceCoinAnimation());
        }
    }

    private IEnumerator BounceCoinAnimation()
    {
        float elapsed = 0f;
        float duration = 0.28f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Elastic pop scale up to 1.35 then settle to 1.0
            float scale = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.35f;
            if (_coinIconRect != null) _coinIconRect.localScale = Vector3.one * scale;
            yield return null;
        }

        if (_coinIconRect != null) _coinIconRect.localScale = Vector3.one;
    }

    private void EnsureUIConstructed()
    {
        if (_rootRect != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null)
        {
            Debug.LogWarning("[CoinDisplayUI] HudCanvas not found in scene. Coin HUD will not be created.");
            return;
        }

        // Font selection
        Font displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (loadedFont != null) displayFont = loadedFont;
#endif

        // Root container - Positioned Top-Right
        var rootGO = new GameObject("CoinWalletHUD", typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(hud.transform, false);
        _rootRect = rootGO.GetComponent<RectTransform>();
        _rootRect.anchorMin = new Vector2(1f, 1f);
        _rootRect.anchorMax = new Vector2(1f, 1f);
        _rootRect.pivot = new Vector2(1f, 1f);
        _rootRect.anchoredPosition = new Vector2(-28f, -28f);
        _rootRect.sizeDelta = new Vector2(160f, 44f);

        // Dark translucent pill backdrop
        var bgGO = new GameObject("PillBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(rootGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.06f, 0.05f, 0.05f, 0.75f);

        // Subtle gold outline
        var outlineGO = new GameObject("PillBorder", typeof(RectTransform), typeof(Outline));
        outlineGO.transform.SetParent(bgGO.transform, false);
        var outComponent = bgGO.AddComponent<Outline>();
        outComponent.effectColor = new Color(0.85f, 0.70f, 0.20f, 0.45f);
        outComponent.effectDistance = new Vector2(1f, -1f);

        // Coin Icon (Shiny Gold Disc)
        var iconGO = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(rootGO.transform, false);
        _coinIconRect = iconGO.GetComponent<RectTransform>();
        _coinIconRect.anchorMin = new Vector2(0f, 0.5f);
        _coinIconRect.anchorMax = new Vector2(0f, 0.5f);
        _coinIconRect.pivot = new Vector2(0.5f, 0.5f);
        _coinIconRect.anchoredPosition = new Vector2(24f, 0f);
        _coinIconRect.sizeDelta = new Vector2(28f, 28f);

        var iconImg = iconGO.GetComponent<Image>();
        iconImg.color = new Color(1.0f, 0.82f, 0.20f, 1f); // Vibrant Philippine gold coin

        // Coin symbol inner text ("₱")
        var symGO = new GameObject("PesoSymbol", typeof(RectTransform), typeof(Text));
        symGO.transform.SetParent(iconGO.transform, false);
        var symRT = symGO.GetComponent<RectTransform>();
        symRT.anchorMin = Vector2.zero;
        symRT.anchorMax = Vector2.one;
        symRT.sizeDelta = Vector2.zero;
        var symText = symGO.GetComponent<Text>();
        symText.text = "₱";
        symText.font = displayFont;
        symText.fontSize = 18;
        symText.color = new Color(0.25f, 0.16f, 0.02f, 1f);
        symText.alignment = TextAnchor.MiddleCenter;

        // Coin Count Text ("₱ 0")
        var textGO = new GameObject("CoinCountText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(rootGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.pivot = new Vector2(0f, 0.5f);
        textRT.anchoredPosition = new Vector2(46f, 0f);
        textRT.sizeDelta = new Vector2(-50f, 0f);

        _coinText = textGO.GetComponent<Text>();
        _coinText.text = "₱ 0";
        _coinText.font = displayFont;
        _coinText.fontSize = 20;
        _coinText.fontStyle = FontStyle.Bold;
        _coinText.color = new Color(1.0f, 0.92f, 0.70f, 1f); // Warm coin gleam
        _coinText.alignment = TextAnchor.MiddleLeft;

        var textShadow = textGO.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        textShadow.effectDistance = new Vector2(1f, -1f);

        // Floater Popup Text (e.g. "+₱1")
        var floaterGO = new GameObject("PlusFloater", typeof(RectTransform), typeof(Text));
        floaterGO.transform.SetParent(rootGO.transform, false);
        var fRT = floaterGO.GetComponent<RectTransform>();
        fRT.anchorMin = new Vector2(1f, 0f);
        fRT.anchorMax = new Vector2(1f, 0f);
        fRT.pivot = new Vector2(1f, 1f);
        fRT.anchoredPosition = new Vector2(-10f, -48f);
        fRT.sizeDelta = new Vector2(120f, 30f);

        _plusFloaterText = floaterGO.GetComponent<Text>();
        _plusFloaterText.text = "+₱1";
        _plusFloaterText.font = displayFont;
        _plusFloaterText.fontSize = 18;
        _plusFloaterText.fontStyle = FontStyle.Bold;
        _plusFloaterText.color = new Color(0.35f, 1.0f, 0.45f, 1f); // Vibrant positive green
        _plusFloaterText.alignment = TextAnchor.MiddleRight;
        floaterGO.SetActive(false);

        Debug.Log("[CoinDisplayUI] UI constructed successfully on HudCanvas.");
    }
}
