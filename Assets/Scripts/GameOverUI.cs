using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the Game Over screen when Anxiety reaches 100%.
/// Displays retro survival-horror title, flavor text, and Retry/Menu buttons.
/// Halts player movement, disables Proctor AI, unlocks cursor.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("UI Root (Auto-constructed if null)")]
    public GameObject gameOverPanel;
    public CanvasGroup canvasGroup;

    [Header("Text References")]
    public Text titleText;
    public Text subtitleText;

    [Header("Buttons")]
    public Button retryButton;
    public Button menuButton;

    [Header("Audio")]
    public AudioClip deathSound;

    private AudioSource _audioSource;
    private bool _hasTriggered = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0.85f;

#if UNITY_EDITOR
        if (deathSound == null)
        {
            deathSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");
        }
#endif
    }

    private void Start()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver += HandleGameOver;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnGameOver -= HandleGameOver;
        }
    }

    private void HandleGameOver()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        StartCoroutine(ShowGameOverRoutine());
    }

    private IEnumerator ShowGameOverRoutine()
    {
        // 1. Freeze player controls
        var playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.SetControlsEnabled(false);
        }

        // 2. Despawn any active Proctor
        TheProctorAI.DespawnActiveProctor();

        // 3. Play death sting
        if (deathSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(deathSound, 0.8f);
        }

        // 4. Construct UI if needed
        EnsureUIConstructed();

        // 5. Unlock mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // 6. Smooth fade in
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            float duration = 1.6f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }

    private void OnRetryClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void EnsureUIConstructed()
    {
        if (gameOverPanel != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null)
        {
            var canvasGO = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGO.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999;
            hud = canvasGO;
        }

        Font horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (loadedFont != null) horrorFont = loadedFont;
#endif

        // Root fullscreen panel
        gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        gameOverPanel.transform.SetParent(hud.transform, false);
        var rootRT = gameOverPanel.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        var bgImg = gameOverPanel.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.01f, 0.01f, 0.96f); // Deep pitch charcoal with blood tinge

        canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();

        // Title Text
        var titleGO = new GameObject("GameOverTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(gameOverPanel.transform, false);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.65f);
        titleRT.anchorMax = new Vector2(0.5f, 0.65f);
        titleRT.sizeDelta = new Vector2(700f, 90f);

        titleText = titleGO.GetComponent<Text>();
        titleText.text = "ANXIETY OVERWHELMED";
        titleText.font = horrorFont;
        titleText.fontSize = 44;
        titleText.color = new Color(0.88f, 0.12f, 0.12f, 1f); // Horror crimson
        titleText.alignment = TextAnchor.MiddleCenter;

        // Subtitle Text
        var subGO = new GameObject("GameOverSubtitle", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(gameOverPanel.transform, false);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0.53f);
        subRT.anchorMax = new Vector2(0.5f, 0.53f);
        subRT.sizeDelta = new Vector2(650f, 60f);

        subtitleText = subGO.GetComponent<Text>();
        subtitleText.text = "The Proctor claimed you into the silence.\nYour clearance will never be granted.";
        subtitleText.font = horrorFont;
        subtitleText.fontSize = 18;
        subtitleText.color = new Color(0.75f, 0.70f, 0.70f, 0.85f);
        subtitleText.alignment = TextAnchor.MiddleCenter;

        // Button Container
        var btnContainer = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnContainer.transform.SetParent(gameOverPanel.transform, false);
        var btnRT = btnContainer.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.35f);
        btnRT.anchorMax = new Vector2(0.5f, 0.35f);
        btnRT.sizeDelta = new Vector2(440f, 55f);

        var hlg = btnContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Retry Button
        retryButton = CreateButton(btnContainer.transform, "RetryButton", "WAKE UP", horrorFont, OnRetryClicked);

        // Main Menu Button
        menuButton = CreateButton(btnContainer.transform, "MenuButton", "MAIN MENU", horrorFont, OnMainMenuClicked);
    }

    private Button CreateButton(Transform parent, string goName, string text, Font font, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.18f, 0.05f, 0.05f, 0.90f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.05f, 0.05f, 0.90f);
        colors.highlightedColor = new Color(0.65f, 0.12f, 0.12f, 1.0f);
        colors.pressedColor = new Color(0.95f, 0.20f, 0.20f, 1.0f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(btnGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        var txt = textGO.GetComponent<Text>();
        txt.text = text;
        txt.font = font;
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }
}
