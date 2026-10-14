using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GuidanceSafePuzzle — Rotary tumbler wall safe hidden behind the certificate frame
/// in Room 207 (Guidance and Counseling Office).
///
/// Features:
/// - 3 mechanical rotary tumblers (000 to 999).
/// - Correct combination: 4 - 1 - 9 (discovered on the Counseling Audio Cassette Tape in 3D inspection).
/// - Interactive dial UI with sound effects for tumbler clicks and heavy metal bolt opening.
/// - Unlocking safe grants Guidance Evaluation Seal, reduces student anxiety by 35,
///   and satisfies Guidance Counselor clearance standing.
/// </summary>
public class GuidanceSafePuzzle : MonoBehaviour, IInteractable
{
    public static GuidanceSafePuzzle Instance { get; private set; }

    [Header("Safe State")]
    public bool isSafeUnlocked = false;
    private int[] currentTumblers = new int[3] { 0, 0, 0 };
    private readonly int[] correctCombination = new int[3] { 4, 1, 9 };

    [Header("Audio")]
    public AudioClip dialClickAudio;
    public AudioClip boltUnlockAudio;

    // UI References
    private GameObject _safeCanvasGO;
    private CanvasGroup _canvasGroup;
    private Text[] _digitTexts = new Text[3];
    private AudioSource _audioSource;
    private bool _isUIOpen = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        if (dialClickAudio == null)
            dialClickAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (boltUnlockAudio == null)
            boltUnlockAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
#endif

        BuildSafeUI();
        SetUIVisible(false);
    }

    private void BuildSafeUI()
    {
        var hudCanvas = GameObject.Find("HudCanvas");
        Transform parentTransform = hudCanvas != null ? hudCanvas.transform : transform;

        _safeCanvasGO = new GameObject("GuidanceSafe_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _safeCanvasGO.transform.SetParent(parentTransform, false);
        var canvas = _safeCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;

        _canvasGroup = _safeCanvasGO.AddComponent<CanvasGroup>();

        // Dark modal backdrop
        var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bg.transform.SetParent(_safeCanvasGO.transform, false);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.88f);
        bg.GetComponent<Button>().onClick.AddListener(CloseSafeUI);

        // Safe Dial Panel
        var panel = new GameObject("SafeDialBox", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_safeCanvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(460f, 320f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        // Header Title
        var titleGO = new GameObject("SafeTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(panel.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.75f);
        trt.anchorMax = new Vector2(1f, 1f);
        var titleTxt = titleGO.GetComponent<Text>();
        titleTxt.text = "GUIDANCE WALL SAFE — COMBINATION";
        titleTxt.fontSize = 20;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = new Color(0.85f, 0.75f, 0.35f, 1f);
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 3 Tumblers
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var tumblerBox = new GameObject($"Tumbler_{i}", typeof(RectTransform), typeof(Image));
            tumblerBox.transform.SetParent(panel.transform, false);
            var tr = tumblerBox.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.2f + i * 0.28f, 0.32f);
            tr.anchorMax = new Vector2(0.36f + i * 0.28f, 0.68f);
            tumblerBox.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.18f, 1f);

            // Digit Text
            var digitGO = new GameObject("DigitText", typeof(RectTransform), typeof(Text));
            digitGO.transform.SetParent(tumblerBox.transform, false);
            var drt = digitGO.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            _digitTexts[i] = digitGO.GetComponent<Text>();
            _digitTexts[i].text = currentTumblers[i].ToString();
            _digitTexts[i].fontSize = 44;
            _digitTexts[i].fontStyle = FontStyle.Bold;
            _digitTexts[i].alignment = TextAnchor.MiddleCenter;
            _digitTexts[i].color = new Color(0.95f, 0.95f, 0.92f, 1f);
            _digitTexts[i].font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Up Button
            CreateDialButton(tumblerBox.transform, "▲", new Vector2(0.5f, 1.25f), () => ChangeTumbler(index, 1));
            // Down Button
            CreateDialButton(tumblerBox.transform, "▼", new Vector2(0.5f, -0.25f), () => ChangeTumbler(index, -1));
        }

        // Close Hint
        var hintGO = new GameObject("CloseHint", typeof(RectTransform), typeof(Text));
        hintGO.transform.SetParent(panel.transform, false);
        var hrt = hintGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.05f);
        hrt.anchorMax = new Vector2(1f, 0.18f);
        var hintTxt = hintGO.GetComponent<Text>();
        hintTxt.text = "[ESC / CLICK OUTSIDE] Isara ang Safe";
        hintTxt.fontSize = 14;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(0.6f, 0.6f, 0.65f, 0.8f);
        hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void CreateDialButton(Transform parent, string label, Vector2 anchorPos, UnityEngine.Events.UnityAction action)
    {
        var btnGO = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.sizeDelta = new Vector2(48f, 32f);
        btnGO.GetComponent<Image>().color = new Color(0.24f, 0.25f, 0.3f, 1f);

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        var t = txtGO.GetComponent<Text>();
        t.text = label;
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        btnGO.GetComponent<Button>().onClick.AddListener(action);
    }

    private void ChangeTumbler(int index, int delta)
    {
        currentTumblers[index] = (currentTumblers[index] + delta + 10) % 10;
        if (_digitTexts[index] != null)
        {
            _digitTexts[index].text = currentTumblers[index].ToString();
        }

        PlayAudio(dialClickAudio, 0.7f, 1.1f + index * 0.1f);

        CheckCombination();
    }

    private void CheckCombination()
    {
        for (int i = 0; i < 3; i++)
        {
            if (currentTumblers[i] != correctCombination[i]) return;
        }

        // Correct combination entered (4 - 1 - 9)
        StartCoroutine(SafeUnlockedSequence());
    }

    private IEnumerator SafeUnlockedSequence()
    {
        isSafeUnlocked = true;
        PlayAudio(boltUnlockAudio, 0.95f, 1.0f);

        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(-35f);
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Guidance Safe Na-unlock! (4-1-9)",
                "Nakuha ang opisyal na Guidance Recommendation Seal mula sa wall safe! Bumalik kay Ma'am sa Room 207."
            );
        }

        yield return new WaitForSeconds(1.2f);
        CloseSafeUI();
    }

    private void Update()
    {
        if (!_isUIOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab))
        {
            CloseSafeUI();
        }
    }

    public string GetPrompt()
    {
        if (isSafeUnlocked)
        {
            return "[Guidance Safe] (Bukas Na - Nakuha ang Recommendation Seal)";
        }

        bool knowsCode = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("CassetteSafeCode");
        return knowsCode
            ? "[E] Buksan ang Guidance Safe gamit ang Code 4-1-9"
            : "[E] Suriin ang Guidance Wall Safe (Naka-lock - 3 Tumblers)";
    }

    public void Interact()
    {
        if (isSafeUnlocked) return;
        OpenSafeUI();
    }

    public void OpenSafeUI()
    {
        _isUIOpen = true;
        SetUIVisible(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.SetControlsEnabled(false);
    }

    public void CloseSafeUI()
    {
        _isUIOpen = false;
        SetUIVisible(false);

        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null && !PauseMenu.GameIsPaused && !LockerHideManager.IsPlayerHidden)
        {
            pm.SetControlsEnabled(true);
        }

        bool isInventoryOpen = (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen);
        if (!isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetUIVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    private void PlayAudio(AudioClip clip, float vol, float pitch)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, vol);
        }
    }
}
