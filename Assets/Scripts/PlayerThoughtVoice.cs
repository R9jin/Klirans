using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerThoughtVoice — Diegetic spontaneous player internal monologue / written thought system.
/// Displays authentic colloquial Tagalog thought subtitles at the bottom of the screen reacting dynamically
/// to key campus events (waking up, electrical brownouts, blackouts, anomalies, runaway staff, high anxiety).
/// </summary>
public class PlayerThoughtVoice : MonoBehaviour
{
    public static PlayerThoughtVoice Instance { get; private set; }

    [Header("UI Visuals")]
    public Canvas thoughtCanvas;
    public GameObject thoughtBox;
    public Text thoughtText;
    public CanvasGroup thoughtGroup;

    private Coroutine _thoughtCoroutine;
    private readonly Queue<ThoughtEntry> _thoughtQueue = new Queue<ThoughtEntry>();
    private bool _isDisplaying = false;

    // Track event one-shots so thoughts feel spontaneous rather than spammy
    private bool _hasSpokenOpening = false;
    private bool _hasSpokenFirstBlackout = false;
    private bool _hasSpokenFirstCry = false;
    private bool _hasSpokenHighAnxiety = false;
    private float _lastAnxietyThoughtTime = 0f;

    private struct ThoughtEntry
    {
        public string text;
        public float duration;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureThoughtUI();
    }

    private void Start()
    {
        // Hook into BlackoutManager events
        if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart += HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd += HandleBlackoutEnd;
        }

        // Spontaneous opening monologue after cutscene or spawn
        StartCoroutine(OpeningMonologueDelayed());
    }

    private void OnDestroy()
    {
        if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart -= HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd -= HandleBlackoutEnd;
        }
    }

    private void Update()
    {
        // Periodic anxiety check
        CheckAnxietyThoughts();
    }

    private IEnumerator OpeningMonologueDelayed()
    {
        while (OpeningCutsceneDirector.IsAnyCutscenePlaying)
        {
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(2.8f);

        if (!_hasSpokenOpening)
        {
            _hasSpokenOpening = true;
            ShowThought("Kailangan kong ma-clear ang clearance bago mag-hatinggabi... Bakit patay-sindi ang mga ilaw sa buong gusali?", 5.0f);
        }
    }

    private void HandleBlackoutStart()
    {
        if (!_hasSpokenFirstBlackout)
        {
            _hasSpokenFirstBlackout = true;
            ShowThought("Naku, nawalan ng kuryente! Ang dilim sa buong pasilyo... kailangan kong gamitin ang flashlight.", 5.0f);
        }
        else if (Random.value < 0.55f)
        {
            string[] blackoutThoughts = new string[]
            {
                "Nawalan na naman ng ilaw... May gumagalaw sa dilim, sigurado ako.",
                "Brownout na naman... Hindi maganda 'to. Kailangan kong bilisan bago may lumapit.",
                "Patay ang kuryente. 'Wag kang mag-ingay... baka marinig ka nila."
            };
            ShowThought(blackoutThoughts[Random.Range(0, blackoutThoughts.Length)], 4.5f);
        }
    }

    private void HandleBlackoutEnd()
    {
        if (Random.value < 0.65f)
        {
            string[] restoreThoughts = new string[]
            {
                "Bumalik na ang kuryente... pero parang lalong lumalamig ang paligid.",
                "Salamat sa Diyos, may ilaw na ulit... pero kailangan ko pa ring mag-ingat.",
                "Umayos sandali ang ilaw. Kailangan ko nang tapusin ang mga clearance bago muling magdilim."
            };
            ShowThought(restoreThoughts[Random.Range(0, restoreThoughts.Length)], 4.2f);
        }
    }

    private void CheckAnxietyThoughts()
    {
        if (AnxietyManager.Instance == null) return;

        float anxiety = AnxietyManager.Instance.CurrentAnxiety / Mathf.Max(1f, AnxietyManager.Instance.maxAnxiety);
        if (anxiety > 0.70f && Time.time > _lastAnxietyThoughtTime + 45f)
        {
            _lastAnxietyThoughtTime = Time.time;
            string[] panicThoughts = new string[]
            {
                "Bumibilis ang tibok ng puso ko... hindi ako makahinga... kailangan kong kumalma.",
                "Sobrang lakas ng kabog ng dibdib ko... may nakatingin sa akin, nararamdaman ko.",
                "Huminga ka nang malalim... 'wag kang magpa-panic... kaya mo 'to."
            };
            ShowThought(panicThoughts[Random.Range(0, panicThoughts.Length)], 4.8f);
        }
    }

    public void OnHeardWeeping()
    {
        if (!_hasSpokenFirstCry)
        {
            _hasSpokenFirstCry = true;
            ShowThought("May umiiyak ba sa sulok...? Bakit tumigil nang tingnan ko...?", 4.8f);
        }
        else if (Random.value < 0.4f)
        {
            ShowThought("Nandiyan na naman 'yung umiiyak na estudyante... 'wag mong tatanggalin ang tingin mo sa kanya.", 4.5f);
        }
    }

    public void OnRegistrarFled()
    {
        ShowThought("Teka! Huwag kang tumakbo! Kailangan ko lang ng pirma mo!", 4.5f);
    }

    public void OnAllSignaturesCollected()
    {
        ShowThought("Kumpleto na ang anim na pirma! Kailangan ko nang bumalik sa Registrar sa Room 104 bago mag-alas dose!", 5.5f);
    }

    /// <summary>
    /// Displays an internal monologue thought on screen.
    /// </summary>
    public void ShowThought(string tagalogThought, float duration = 4.5f)
    {
        if (string.IsNullOrEmpty(tagalogThought)) return;

        EnsureThoughtUI();

        _thoughtQueue.Enqueue(new ThoughtEntry { text = tagalogThought, duration = duration });

        if (!_isDisplaying)
        {
            StartCoroutine(ProcessThoughtQueue());
        }
    }

    private IEnumerator ProcessThoughtQueue()
    {
        _isDisplaying = true;

        while (_thoughtQueue.Count > 0)
        {
            var entry = _thoughtQueue.Dequeue();

            if (thoughtText != null)
            {
                thoughtText.text = $"<color=#FFCC00><b>(ISIPAN):</b></color> <i>\"{entry.text}\"</i>";
            }

            if (thoughtBox != null) thoughtBox.SetActive(true);

            // Fade in
            float fadeDur = 0.25f;
            float elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (thoughtGroup != null) thoughtGroup.alpha = Mathf.Clamp01(elapsed / fadeDur);
                yield return null;
            }

            if (thoughtGroup != null) thoughtGroup.alpha = 1f;

            // Hold display
            yield return new WaitForSeconds(entry.duration);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (thoughtGroup != null) thoughtGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDur));
                yield return null;
            }

            if (thoughtGroup != null) thoughtGroup.alpha = 0f;
            if (thoughtBox != null) thoughtBox.SetActive(false);

            yield return new WaitForSeconds(0.4f);
        }

        _isDisplaying = false;
    }

    private void EnsureThoughtUI()
    {
        if (thoughtBox != null && thoughtText != null && thoughtGroup != null) return;

        // Find or create ThoughtCanvas
        var existingCanvas = GameObject.Find("PlayerThoughtCanvas");
        if (existingCanvas != null)
        {
            thoughtCanvas = existingCanvas.GetComponent<Canvas>();
        }
        else
        {
            var canvasGO = new GameObject("PlayerThoughtCanvas");
            thoughtCanvas = canvasGO.AddComponent<Canvas>();
            thoughtCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            thoughtCanvas.sortingOrder = 95;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Thought Box Ribbon at Bottom Screen
        var boxGO = new GameObject("ThoughtBox", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        boxGO.transform.SetParent(thoughtCanvas.transform, false);
        thoughtBox = boxGO;

        var rt = boxGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 85f);
        rt.sizeDelta = new Vector2(1100f, 64f);

        var bgImg = boxGO.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.05f, 0.88f);
        bgImg.raycastTarget = false;

        var outline = boxGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.70f, 0.30f, 0.35f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        thoughtGroup = boxGO.GetComponent<CanvasGroup>();
        thoughtGroup.alpha = 0f;
        thoughtGroup.blocksRaycasts = false;
        thoughtGroup.interactable = false;

        // Thought Text
        var textGO = new GameObject("ThoughtText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(boxGO.transform, false);

        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.03f, 0f);
        trt.anchorMax = new Vector2(0.97f, 1f);
        trt.sizeDelta = Vector2.zero;

        thoughtText = textGO.GetComponent<Text>();
        Font boldFont = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
        if (boldFont == null)
            boldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lucida_Typewriter_Bold.ttf");
#endif
        if (boldFont == null) boldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (boldFont != null) thoughtText.font = boldFont;
        thoughtText.fontSize = 17;
        thoughtText.fontStyle = FontStyle.Normal;
        thoughtText.color = new Color(0.98f, 0.96f, 0.90f, 1f);
        thoughtText.alignment = TextAnchor.MiddleCenter;
        thoughtText.horizontalOverflow = HorizontalWrapMode.Wrap;
        thoughtText.verticalOverflow = VerticalWrapMode.Overflow;
        thoughtText.raycastTarget = false;

        var textShadow = textGO.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        textShadow.effectDistance = new Vector2(1.5f, -1.5f);

        thoughtBox.SetActive(false);
    }
}
