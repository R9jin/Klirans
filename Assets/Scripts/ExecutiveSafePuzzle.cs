using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExecutiveSafePuzzle — Heavy Rotary Tumbler Executive Vault Safe located in Room 103
/// (Office of the Executive Vice President).
///
/// Features:
/// - Heavy Sargent & Greenleaf 3-wheel mechanical rotary vault safe securing the Official University Seal Stamp.
/// - Brushed steel door plate with rich dark mahogany and brass executive borders.
/// - 3 smooth rotary dial wheels with chrome bezels, numbers 0-9.
/// - Correct combination: 7 - 3 - 2 (discovered on the Faculty Records Key Tag in 3D inspection).
/// - Dynamic mechanical ratchet click and heavy vault multi-bolt unlatching sound effects.
/// - Full mouse and keyboard controls (A/D to select tumbler, W/S or ▲/▼ to rotate, 1-3 direct select).
/// - Active dial selection indicator and status LED (Red when locked, Emerald Green when opened).
/// - Unlocking safe grants the ONE AND ONLY Official University Seal Stamp directly to the player's inventory,
///   reducing anxiety by 35, triggering 3D item inspection, and enabling EVP to sign the 6th and final clearance!
/// </summary>
public class ExecutiveSafePuzzle : MonoBehaviour, IInteractable
{
    public static ExecutiveSafePuzzle Instance { get; private set; }

    [Header("Safe State")]
    public bool isSafeUnlocked = false;
    private int[] currentTumblers = new int[3] { 0, 0, 0 };
    private readonly int[] correctCombination = new int[3] { 7, 3, 2 };
    private int selectedTumblerIndex = 0;

    [Header("Reward Item")]
    public InventoryItem sealRewardItem;

    [Header("Audio")]
    public AudioClip dialClickAudio;
    public AudioClip boltUnlockAudio;

    // Generated audio fallbacks
    private AudioClip _synthClickClip;
    private AudioClip _synthUnlockClip;

    // Visual Sprites
    private Sprite _safeDoorSprite;
    private Sprite _safeDialSprite;

    // UI References
    private GameObject _safeCanvasGO;
    private CanvasGroup _canvasGroup;
    private Image _safeDoorImage;
    private Image _statusLedImage;
    private Text _statusBannerText;
    private Text[] _digitTexts = new Text[3];
    private RectTransform[] _dialWheelRTs = new RectTransform[3];
    private Image[] _selectionRings = new Image[3];
    private float[] _currentDialAngles = new float[3] { 0f, 0f, 0f };
    private float[] _targetDialAngles = new float[3] { 0f, 0f, 0f };

    // Victory seal display
    private GameObject _unlockedChamberGO;

    private AudioSource _audioSource;
    private bool _isUIOpen = false;
    public bool IsOpen => _isUIOpen;
    private int _openedFrame = -1;

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

        LoadPuzzleSprites();
        BuildSynthesizedAudioClips();

#if UNITY_EDITOR
        if (dialClickAudio == null)
            dialClickAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (boltUnlockAudio == null)
            boltUnlockAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
        if (sealRewardItem == null)
            sealRewardItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Executive_Seal_Stamp.asset");
#endif
        if (sealRewardItem == null)
            sealRewardItem = Resources.Load<InventoryItem>("Executive_Seal_Stamp");

        BuildSafeUI();
        SetUIVisible(false);
    }

    private void LoadPuzzleSprites()
    {
        _safeDoorSprite = LoadSprite("Assets/Textures/PuzzleTextures/Safe_Door_Plate.png");
        _safeDialSprite = LoadSprite("Assets/Textures/PuzzleTextures/Safe_Dial_Wheel.png");
    }

    private Sprite LoadSprite(string relativePath)
    {
        Sprite spr = null;
#if UNITY_EDITOR
        spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
        if (spr != null) return spr;

        Texture2D texEd = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        if (texEd != null)
        {
            return Sprite.Create(texEd, new Rect(0, 0, texEd.width, texEd.height), new Vector2(0.5f, 0.5f));
        }
#endif
        string fullPath = Path.Combine(Application.dataPath, "..", relativePath).Replace('\\', '/');
        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = Path.GetFileNameWithoutExtension(relativePath);
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ExecutiveSafePuzzle] Failed to load sprite from {fullPath}: {ex.Message}");
            }
        }
        return spr;
    }

    private void BuildSynthesizedAudioClips()
    {
        // 1. Sharp mechanical ratchet click
        int clickSamples = 1600;
        _synthClickClip = AudioClip.Create("SynthDialClick_Exec", clickSamples, 1, 44100, false);
        float[] cData = new float[clickSamples];
        for (int i = 0; i < clickSamples; i++)
        {
            float t = (float)i / clickSamples;
            float env = Mathf.Exp(-t * 22f);
            float tone = Mathf.Sin(2f * Mathf.PI * 1850f * (float)i / 44100f);
            float noise = (Random.value * 2f - 1f) * 0.4f;
            cData[i] = (tone * 0.6f + noise) * env * 0.7f;
        }
        _synthClickClip.SetData(cData, 0);

        // 2. Heavy vault multi-bolt unlatch thud
        int unlockSamples = 22000;
        _synthUnlockClip = AudioClip.Create("SynthVaultUnlock_Exec", unlockSamples, 1, 44100, false);
        float[] uData = new float[unlockSamples];
        for (int i = 0; i < unlockSamples; i++)
        {
            float t = (float)i / unlockSamples;
            float env = Mathf.Exp(-t * 6.5f);
            float sub = Mathf.Sin(2f * Mathf.PI * 85f * (float)i / 44100f);
            float clang = Mathf.Sin(2f * Mathf.PI * 420f * (float)i / 44100f) * Mathf.Exp(-t * 12f);
            float rattle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 15f) * 0.35f;
            uData[i] = (sub * 0.65f + clang * 0.45f + rattle) * env * 0.85f;
        }
        _synthUnlockClip.SetData(uData, 0);
    }

    private void BuildSafeUI()
    {
        if (_safeCanvasGO != null)
        {
            Destroy(_safeCanvasGO);
            _safeCanvasGO = null;
        }

        var stale = GameObject.Find("ExecutiveSafe_UI");
        if (stale != null) Destroy(stale);

        _safeCanvasGO = new GameObject("ExecutiveSafe_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _safeCanvasGO.transform.SetParent(null, false);
        var canvas = _safeCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        var scaler = _safeCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _safeCanvasGO.AddComponent<CanvasGroup>();

        // Dark modal backdrop
        var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bg.transform.SetParent(_safeCanvasGO.transform, false);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.90f);
        bg.GetComponent<Button>().onClick.AddListener(CloseSafeUI);

        // Heavy Reinforced Steel Safe Door Panel
        var panel = new GameObject("SafeDialBox", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_safeCanvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(700f, 490f);

        _safeDoorImage = panel.GetComponent<Image>();
        if (_safeDoorSprite != null)
        {
            _safeDoorImage.sprite = _safeDoorSprite;
            _safeDoorImage.color = new Color(0.92f, 0.88f, 0.80f, 1f); // Warm executive brass tint
        }
        else
        {
            _safeDoorImage.color = new Color(0.14f, 0.12f, 0.10f, 0.98f);
        }

        // Vault Model Plaque / Subheader
        var titleGO = new GameObject("SafeTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(panel.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -42f);
        trt.sizeDelta = new Vector2(620f, 50f);
        var titleTxt = titleGO.GetComponent<Text>();
        titleTxt.text = "SARGENT & GREENLEAF // EXECUTIVE VAULT SAFE\n<size=12><color=#D4AF37>OFFICE OF THE EXECUTIVE VICE PRESIDENT — ROOM 103</color></size>";
        titleTxt.fontSize = 16;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = new Color(0.98f, 0.85f, 0.45f, 1f);
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Status LED Indicator Jewel
        var ledGO = new GameObject("StatusLED", typeof(RectTransform), typeof(Image));
        ledGO.transform.SetParent(panel.transform, false);
        var ledRT = ledGO.GetComponent<RectTransform>();
        ledRT.anchorMin = new Vector2(1f, 1f);
        ledRT.anchorMax = new Vector2(1f, 1f);
        ledRT.anchoredPosition = new Vector2(-48f, -42f);
        ledRT.sizeDelta = new Vector2(20f, 20f);
        _statusLedImage = ledGO.GetComponent<Image>();
        _statusLedImage.color = new Color(0.90f, 0.18f, 0.18f, 1f); // Red locked

        // Status Banner Text
        var bannerGO = new GameObject("StatusBanner", typeof(RectTransform), typeof(Text));
        bannerGO.transform.SetParent(panel.transform, false);
        var brt = bannerGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 1f);
        brt.anchorMax = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -86f);
        brt.sizeDelta = new Vector2(580f, 30f);
        _statusBannerText = bannerGO.GetComponent<Text>();
        _statusBannerText.text = "ENTER 3-TUMBLER MASTER FACULTY COMBINATION (CODE: 7-3-2)";
        _statusBannerText.fontSize = 13;
        _statusBannerText.fontStyle = FontStyle.Bold;
        _statusBannerText.alignment = TextAnchor.MiddleCenter;
        _statusBannerText.color = new Color(0.85f, 0.82f, 0.72f, 1f);
        _statusBannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 3 Rotary Tumblers Container
        float[] xPositions = new float[] { -175f, 0f, 175f };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float xPos = xPositions[i];

            // Outer Tumbler Housing
            var tumblerBox = new GameObject($"Tumbler_{i}", typeof(RectTransform));
            tumblerBox.transform.SetParent(panel.transform, false);
            var tr = tumblerBox.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(xPos, -15f);
            tr.sizeDelta = new Vector2(140f, 210f);

            // Selection Glow Ring (Gold border when active)
            var ringGO = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image));
            ringGO.transform.SetParent(tumblerBox.transform, false);
            var ringRT = ringGO.GetComponent<RectTransform>();
            ringRT.anchorMin = new Vector2(0.5f, 0.5f);
            ringRT.anchorMax = new Vector2(0.5f, 0.5f);
            ringRT.anchoredPosition = Vector2.zero;
            ringRT.sizeDelta = new Vector2(136f, 136f);
            _selectionRings[i] = ringGO.GetComponent<Image>();
            _selectionRings[i].color = (i == selectedTumblerIndex) ? new Color(1f, 0.82f, 0.25f, 0.75f) : new Color(0.2f, 0.2f, 0.2f, 0.25f);

            // Rotating Dial Wheel
            var wheelGO = new GameObject("DialWheel", typeof(RectTransform), typeof(Image));
            wheelGO.transform.SetParent(tumblerBox.transform, false);
            _dialWheelRTs[i] = wheelGO.GetComponent<RectTransform>();
            _dialWheelRTs[i].anchorMin = new Vector2(0.5f, 0.5f);
            _dialWheelRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            _dialWheelRTs[i].anchoredPosition = Vector2.zero;
            _dialWheelRTs[i].sizeDelta = new Vector2(126f, 126f);

            var wheelImg = wheelGO.GetComponent<Image>();
            if (_safeDialSprite != null)
            {
                wheelImg.sprite = _safeDialSprite;
                wheelImg.color = Color.white;
            }
            else
            {
                wheelImg.color = new Color(0.22f, 0.24f, 0.28f, 1f);
            }

            // Top Alignment Marker (Red pointer tick)
            var pointerGO = new GameObject("TopMarker", typeof(RectTransform), typeof(Image));
            pointerGO.transform.SetParent(tumblerBox.transform, false);
            var ptrRT = pointerGO.GetComponent<RectTransform>();
            ptrRT.anchorMin = new Vector2(0.5f, 0.5f);
            ptrRT.anchorMax = new Vector2(0.5f, 0.5f);
            ptrRT.anchoredPosition = new Vector2(0f, 68f);
            ptrRT.sizeDelta = new Vector2(6f, 12f);
            pointerGO.GetComponent<Image>().color = new Color(0.95f, 0.25f, 0.20f, 1f);

            // Center Brass Knob Hub with Digit Display
            var hubGO = new GameObject("CenterHub", typeof(RectTransform), typeof(Image));
            hubGO.transform.SetParent(tumblerBox.transform, false);
            var hubRT = hubGO.GetComponent<RectTransform>();
            hubRT.anchorMin = new Vector2(0.5f, 0.5f);
            hubRT.anchorMax = new Vector2(0.5f, 0.5f);
            hubRT.anchoredPosition = Vector2.zero;
            hubRT.sizeDelta = new Vector2(58f, 58f);
            var hubImg = hubGO.GetComponent<Image>();
            hubImg.color = new Color(0.14f, 0.12f, 0.10f, 0.95f);

            // Digit Text
            var digitGO = new GameObject("DigitText", typeof(RectTransform), typeof(Text));
            digitGO.transform.SetParent(hubGO.transform, false);
            var drt = digitGO.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            _digitTexts[i] = digitGO.GetComponent<Text>();
            _digitTexts[i].text = currentTumblers[i].ToString();
            _digitTexts[i].fontSize = 34;
            _digitTexts[i].fontStyle = FontStyle.Bold;
            _digitTexts[i].alignment = TextAnchor.MiddleCenter;
            _digitTexts[i].color = new Color(0.98f, 0.90f, 0.55f, 1f); // Polished gold digit
            _digitTexts[i].font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Up Button (▲)
            CreateDialButton(tumblerBox.transform, "▲", new Vector2(0f, 88f), () =>
            {
                SelectTumbler(index);
                ChangeTumbler(index, 1);
            });

            // Down Button (▼)
            CreateDialButton(tumblerBox.transform, "▼", new Vector2(0f, -88f), () =>
            {
                SelectTumbler(index);
                ChangeTumbler(index, -1);
            });
        }

        // Unlocked Chamber Visual Container (Initially hidden)
        _unlockedChamberGO = new GameObject("UnlockedChamber", typeof(RectTransform), typeof(Image));
        _unlockedChamberGO.transform.SetParent(panel.transform, false);
        var chRT = _unlockedChamberGO.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(0.08f, 0.18f);
        chRT.anchorMax = new Vector2(0.92f, 0.76f);
        chRT.sizeDelta = Vector2.zero;
        _unlockedChamberGO.GetComponent<Image>().color = new Color(0.06f, 0.14f, 0.08f, 0.96f);

        var chamberTextGO = new GameObject("ChamberText", typeof(RectTransform), typeof(Text));
        chamberTextGO.transform.SetParent(_unlockedChamberGO.transform, false);
        var ctRT = chamberTextGO.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero;
        ctRT.anchorMax = Vector2.one;
        ctRT.sizeDelta = Vector2.zero;
        var ctxt = chamberTextGO.GetComponent<Text>();
        ctxt.text = "<size=22><color=#55FF88>★ COMBINATION 7-3-2 ACCEPTED ★</color></size>\n\n<size=17><color=#FFEE77>OFFICIAL UNIVERSITY SEAL STAMP ACQUIRED</color></size>\n<size=13><color=#C0C0C0>Executive dry seal released from secure chamber for final clearance.</color></size>";
        ctxt.fontSize = 18;
        ctxt.fontStyle = FontStyle.Bold;
        ctxt.alignment = TextAnchor.MiddleCenter;
        ctxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _unlockedChamberGO.SetActive(false);

        // Control Hotkeys Hint at bottom
        var hintGO = new GameObject("ControlsHint", typeof(RectTransform), typeof(Text));
        hintGO.transform.SetParent(panel.transform, false);
        var hrt = hintGO.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0f);
        hrt.anchorMax = new Vector2(1f, 0.14f);
        var hintTxt = hintGO.GetComponent<Text>();
        hintTxt.text = "[A / D / 1-3] Select Tumbler  •  [W / S / ▲ / ▼] Rotate  •  [ESC / E] Close Safe";
        hintTxt.fontSize = 13;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(0.70f, 0.68f, 0.62f, 0.85f);
        hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void CreateDialButton(Transform parent, string label, Vector2 offsetPos, UnityEngine.Events.UnityAction action)
    {
        var btnGO = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offsetPos;
        rt.sizeDelta = new Vector2(44f, 26f);
        btnGO.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.25f, 0.95f);

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        var t = txtGO.GetComponent<Text>();
        t.text = label;
        t.fontSize = 15;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.95f, 0.90f, 0.70f, 1f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        btnGO.GetComponent<Button>().onClick.AddListener(action);
    }

    private void SelectTumbler(int index)
    {
        selectedTumblerIndex = Mathf.Clamp(index, 0, 2);
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_selectionRings[i] != null)
            {
                _selectionRings[i].color = (i == selectedTumblerIndex)
                    ? new Color(1f, 0.82f, 0.25f, 0.85f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.25f);
            }
        }
    }

    private void ChangeTumbler(int index, int delta)
    {
        if (isSafeUnlocked) return;

        currentTumblers[index] = (currentTumblers[index] + delta + 10) % 10;
        _targetDialAngles[index] += delta * 36f;

        if (_digitTexts[index] != null)
        {
            _digitTexts[index].text = currentTumblers[index].ToString();
        }

        AudioClip clip = dialClickAudio != null ? dialClickAudio : _synthClickClip;
        PlayAudio(clip, 0.65f, 0.95f + index * 0.08f + (Random.value * 0.06f));

        CheckCombination();
    }

    private void CheckCombination()
    {
        for (int i = 0; i < 3; i++)
        {
            if (currentTumblers[i] != correctCombination[i]) return;
        }

        // Correct combination entered (7 - 3 - 2)
        StartCoroutine(SafeUnlockedSequence());
    }

    private IEnumerator SafeUnlockedSequence()
    {
        isSafeUnlocked = true;

        if (_statusLedImage != null)
        {
            _statusLedImage.color = new Color(0.20f, 1.0f, 0.40f, 1f); // Vibrant Emerald Green
        }

        if (_statusBannerText != null)
        {
            _statusBannerText.text = "<color=#44FF88>★ SHEAR BOLTS RETRACTED — ACCESS GRANTED ★</color>";
        }

        AudioClip unlockClip = boltUnlockAudio != null ? boltUnlockAudio : _synthUnlockClip;
        PlayAudio(unlockClip, 0.95f, 1.0f);

        if (_unlockedChamberGO != null)
        {
            _unlockedChamberGO.SetActive(true);
        }

        // Award the ONE AND ONLY Executive Seal Stamp to player!
        if (sealRewardItem != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(sealRewardItem, 1);
            ItemInspectionViewer3D.Inspect(sealRewardItem);
        }

        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(-35f);
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Nakuha ang Executive Seal Stamp!",
                "Nakuha ang Official University Seal Stamp mula sa Executive Safe! Dalhin ito kay EVP sa Room 103 para sa final clearance endorsement."
            );
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.TriggerAutosave("Executive Safe Unlocked");
        }

        yield return new WaitForSeconds(1.8f);
        CloseSafeUI();
    }

    private void Update()
    {
        if (!_isUIOpen) return;

        // Smooth dial rotation animation
        for (int i = 0; i < 3; i++)
        {
            if (_dialWheelRTs[i] != null)
            {
                _currentDialAngles[i] = Mathf.MoveTowards(_currentDialAngles[i], _targetDialAngles[i], Time.deltaTime * 360f);
                _dialWheelRTs[i].localRotation = Quaternion.Euler(0f, 0f, _currentDialAngles[i]);
            }
        }

        if (isSafeUnlocked) return;

        // CRITICAL FIX: Do NOT close on the exact frame the safe was opened by pressing [E]!
        if (Time.frameCount <= _openedFrame + 5) return;

        // 1. Close safe
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E))
        {
            CloseSafeUI();
            return;
        }

        // 2. Mouse scroll wheel support for dialing
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.04f)
        {
            ChangeTumbler(selectedTumblerIndex, 1);
        }
        else if (scroll < -0.04f)
        {
            ChangeTumbler(selectedTumblerIndex, -1);
        }

        // 2. Select Tumbler (A/D or Left/Right or 1/2/3)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SelectTumbler((selectedTumblerIndex - 1 + 3) % 3);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            SelectTumbler((selectedTumblerIndex + 1) % 3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectTumbler(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectTumbler(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SelectTumbler(2);
        }

        // 3. Rotate Selected Tumbler (W/S or Up/Down)
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            ChangeTumbler(selectedTumblerIndex, 1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            ChangeTumbler(selectedTumblerIndex, -1);
        }
    }

    public string GetPrompt()
    {
        if (isSafeUnlocked)
        {
            return "[Executive Safe] (Bukas Na — Nakuha ang Official University Seal Stamp)";
        }

        bool knowsCode = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("LockerKeyCode");
        return knowsCode
            ? "[E] Buksan ang Executive Safe gamit ang Code 7-3-2"
            : "[E] Suriin ang Executive Vault Safe (Naka-lock — 3 Tumblers)";
    }

    public void Interact()
    {
        if (isSafeUnlocked) return;
        OpenSafeUI();
    }

    public void OpenSafeUI()
    {
        EnsureUIBuilt();
        _isUIOpen = true;
        _openedFrame = Time.frameCount;
        SetUIVisible(true);
        UpdateSelectionVisuals();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.SetControlsEnabled(false);
    }

    private void EnsureUIBuilt()
    {
        if (_safeCanvasGO == null)
        {
            BuildSafeUI();
        }
        else if (_safeCanvasGO.transform.parent != null)
        {
            _safeCanvasGO.transform.SetParent(null, false);
            var canvas = _safeCanvasGO.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
            }
        }
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
