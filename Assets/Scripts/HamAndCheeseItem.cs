using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HamAndCheeseItem — Manages consumption effects and on-screen comforting feedback
/// when the player eats a freshly toasted Ham & Cheese sandwich.
/// Relieves -25 to -35 anxiety, restores stamina, and plays comforting sound & text feedback.
/// </summary>
public class HamAndCheeseItem : MonoBehaviour
{
    private static HamAndCheeseItem _instance;
    public static HamAndCheeseItem Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("HamAndCheeseController");
                _instance = go.AddComponent<HamAndCheeseItem>();
                if (Application.isPlaying) DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Item Asset Reference")]
    public InventoryItem sandwichItemAsset;

    private AudioSource _audioSource;
    private GameObject _bannerGO;
    private Text _bannerText;
    private CanvasGroup _bannerCanvasGroup;
    private Coroutine _bannerCoroutine;

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

        LoadItemAsset();
    }

    private void LoadItemAsset()
    {
#if UNITY_EDITOR
        if (sandwichItemAsset == null)
        {
            sandwichItemAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/HamAndCheeseSandwich.asset");
        }
#endif
    }

    /// <summary>
    /// Eats a Ham & Cheese sandwich immediately, reducing anxiety by amount (25-35),
    /// boosting stamina, and displaying comforting on-screen message.
    /// </summary>
    public static void Consume(float anxietyRelief = 30f, float staminaBoost = 25f)
    {
        Instance.ExecuteConsume(anxietyRelief, staminaBoost);
    }

    public void ExecuteConsume(float anxietyRelief, float staminaBoost)
    {
        // 1. Relieve Anxiety
        if (AnxietyManager.Instance != null)
        {
            float actualRelief = Mathf.Abs(anxietyRelief);
            AnxietyManager.Instance.ChangeAnxiety(-actualRelief);
            Debug.Log($"[HamAndCheese] Ate sandwich! Anxiety relieved by -{actualRelief:F0}. Current: {AnxietyManager.Instance.CurrentAnxiety:F1}");
        }

        // 2. Restore Stamina
        var stamina = FindAnyObjectByType<StaminaSystem>();
        if (stamina != null && staminaBoost > 0f)
        {
            stamina.currentStamina = Mathf.Min(stamina.maxStamina, stamina.currentStamina + staminaBoost);
        }

        // 3. Audio: Play eating bite & soothing chime
        PlayEatingSound();

        // 4. Show comforting on-screen toast notification
        ShowComfortBanner($"Kinain mo ang mainit-init na Ham & Cheese... Gumaan ang pakiramdam mo. (-{anxietyRelief:F0} Anxiety)");
    }

    private void PlayEatingSound()
    {
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

        // Generate a crisp, comforting munch/bite procedurally or play chime
        AudioClip chime = null;
#if UNITY_EDITOR
        chime = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
        if (chime != null)
        {
            _audioSource.pitch = 0.95f;
            _audioSource.PlayOneShot(chime, 0.85f);
        }
    }

    private void ShowComfortBanner(string message)
    {
        EnsureBannerUI();

        if (_bannerText != null) _bannerText.text = message;
        if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
        _bannerCoroutine = StartCoroutine(AnimateBanner());
    }

    private IEnumerator AnimateBanner()
    {
        if (_bannerGO == null || _bannerCanvasGroup == null) yield break;

        _bannerGO.SetActive(true);
        _bannerCanvasGroup.alpha = 0f;

        // Fade in
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            _bannerCanvasGroup.alpha = Mathf.Clamp01(elapsed / 0.35f);
            yield return null;
        }

        yield return new WaitForSeconds(3.2f);

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.6f)
        {
            elapsed += Time.deltaTime;
            _bannerCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.6f);
            yield return null;
        }

        _bannerGO.SetActive(false);
    }

    private void EnsureBannerUI()
    {
        if (_bannerGO != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        Font horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (loadedFont != null) horrorFont = loadedFont;
#endif

        _bannerGO = new GameObject("ComfortBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _bannerGO.transform.SetParent(hud.transform, false);

        var rt = _bannerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.82f);
        rt.anchorMax = new Vector2(0.5f, 0.82f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 48f);

        var bg = _bannerGO.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.05f, 0.88f);

        var outline = _bannerGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.75f, 0.25f, 0.65f); // Warm toasted cheese outline
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        _bannerCanvasGroup = _bannerGO.GetComponent<CanvasGroup>();

        var textGO = new GameObject("BannerText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(_bannerGO.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = new Vector2(-24f, 0f);

        _bannerText = textGO.GetComponent<Text>();
        _bannerText.font = horrorFont;
        _bannerText.fontSize = 17;
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerText.color = new Color(1.0f, 0.94f, 0.80f, 1f);
        _bannerText.alignment = TextAnchor.MiddleCenter;

        _bannerGO.SetActive(false);
    }
}
