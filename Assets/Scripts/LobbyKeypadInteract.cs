using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LobbyKeypadInteract — Emergency magnetic lockdown override keypad at the lobby entrance exit.
///
/// Features:
/// - Controls the electromagnetic lock on the main exit doors.
/// - Requires 4-digit code: 9 - 4 - 2 - 1 (discovered in the Security Guard Incident Logbook in 3D inspection).
/// - When unlocked, disengages the magnetic door seal and unlocks the campus exit for the finale!
/// </summary>
public class LobbyKeypadInteract : MonoBehaviour, IInteractable
{
    public static LobbyKeypadInteract Instance { get; private set; }

    [Header("Keypad State")]
    public bool isGateUnlocked = false;
    private string currentEnteredCode = "";
    private const string CorrectCode = "9421";

    [Header("Visual References")]
    [Tooltip("Status LED Light on the keypad (Red when locked, Green when unlocked).")]
    public Light keypadLight;

    [Tooltip("The main exit door object to unlock or open.")]
    public GameObject exitDoorObject;

    [Header("Audio")]
    public AudioClip beepAudio;
    public AudioClip unlockAudio;
    public AudioClip errorAudio;

    // UI
    private GameObject _keypadCanvasGO;
    private CanvasGroup _canvasGroup;
    private Text _displayCodeText;
    private AudioSource _audioSource;
    private bool _isUIOpen = false;
    public bool IsOpen => _isUIOpen;

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
        if (beepAudio == null)
            beepAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (unlockAudio == null)
            unlockAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
        if (errorAudio == null)
            errorAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
#endif

        BuildKeypadUI();
        SetUIVisible(false);
    }

    private void BuildKeypadUI()
    {
        var hudCanvas = GameObject.Find("HudCanvas");
        Transform parentTransform = hudCanvas != null ? hudCanvas.transform : transform;

        _keypadCanvasGO = new GameObject("LobbyKeypad_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _keypadCanvasGO.transform.SetParent(parentTransform, false);
        var canvas = _keypadCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;

        _canvasGroup = _keypadCanvasGO.AddComponent<CanvasGroup>();

        // Backdrop
        var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bg.transform.SetParent(_keypadCanvasGO.transform, false);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.88f);
        bg.GetComponent<Button>().onClick.AddListener(CloseKeypadUI);

        // Keypad Container
        var panel = new GameObject("KeypadBox", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_keypadCanvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(380f, 480f);
        panel.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.13f, 0.98f);

        // Header Title
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(panel.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.86f);
        trt.anchorMax = new Vector2(1f, 1f);
        var titleTxt = titleGO.GetComponent<Text>();
        titleTxt.text = "EMERGENCY LOBBY OVERRIDE";
        titleTxt.fontSize = 18;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = new Color(0.88f, 0.2f, 0.2f, 1f);
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Display Screen
        var screenGO = new GameObject("DisplayScreen", typeof(RectTransform), typeof(Image));
        screenGO.transform.SetParent(panel.transform, false);
        var srt = screenGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.1f, 0.72f);
        srt.anchorMax = new Vector2(0.9f, 0.85f);
        screenGO.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 1f);

        var dispTxtGO = new GameObject("DisplayText", typeof(RectTransform), typeof(Text));
        dispTxtGO.transform.SetParent(screenGO.transform, false);
        var drt = dispTxtGO.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        _displayCodeText = dispTxtGO.GetComponent<Text>();
        _displayCodeText.text = "----";
        _displayCodeText.fontSize = 32;
        _displayCodeText.fontStyle = FontStyle.Bold;
        _displayCodeText.alignment = TextAnchor.MiddleCenter;
        _displayCodeText.color = new Color(0.2f, 0.9f, 0.3f, 1f);
        _displayCodeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Numeric Keypad Grid (1-9, C, 0, OK)
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "ENTER" };
        for (int i = 0; i < 12; i++)
        {
            string keyLabel = keys[i];
            int row = i / 3;
            int col = i % 3;

            var btnGO = new GameObject($"Key_{keyLabel}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(panel.transform, false);
            var brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.12f + col * 0.28f, 0.54f - row * 0.15f);
            brt.anchorMax = new Vector2(0.34f + col * 0.28f, 0.66f - row * 0.15f);
            btnGO.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 1f);

            var lblGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGO.transform.SetParent(btnGO.transform, false);
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            var l = lblGO.GetComponent<Text>();
            l.text = keyLabel;
            l.fontSize = 18;
            l.fontStyle = FontStyle.Bold;
            l.alignment = TextAnchor.MiddleCenter;
            l.color = Color.white;
            l.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            btnGO.GetComponent<Button>().onClick.AddListener(() => OnKeyPressed(keyLabel));
        }
    }

    private void OnKeyPressed(string key)
    {
        if (key == "CLR")
        {
            currentEnteredCode = "";
            UpdateDisplay();
            PlayAudio(beepAudio, 0.5f, 0.8f);
            return;
        }

        if (key == "ENTER")
        {
            CheckCode();
            return;
        }

        if (currentEnteredCode.Length < 4)
        {
            currentEnteredCode += key;
            UpdateDisplay();
            PlayAudio(beepAudio, 0.6f, 1.2f);
        }
    }

    private void UpdateDisplay()
    {
        if (_displayCodeText != null)
        {
            _displayCodeText.text = currentEnteredCode.PadRight(4, '-');
        }
    }

    private void CheckCode()
    {
        if (currentEnteredCode == CorrectCode)
        {
            StartCoroutine(UnlockSequence());
        }
        else
        {
            PlayAudio(errorAudio, 0.8f, 0.7f);
            currentEnteredCode = "";
            if (_displayCodeText != null)
            {
                _displayCodeText.text = "ERR!";
            }
        }
    }

    private IEnumerator UnlockSequence()
    {
        isGateUnlocked = true;
        PlayAudio(unlockAudio, 1.0f, 1.0f);

        if (_displayCodeText != null)
        {
            _displayCodeText.text = "OPEN";
            _displayCodeText.color = Color.green;
        }

        if (keypadLight != null)
        {
            keypadLight.color = Color.green;
            keypadLight.intensity = 1.8f;
        }

        if (exitDoorObject != null)
        {
            var door = exitDoorObject.GetComponent<DoorInteract>();
            if (door != null) door.isLocked = false;
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Lobby Exit Na-unlock! (9-4-2-1)",
                "Na-override ang magnetic lockdown clamps sa lobby. Bukas na ang pintuan papunta sa kalayaan!"
            );
        }

        yield return new WaitForSeconds(1.2f);
        CloseKeypadUI();
    }

    private void Update()
    {
        if (!_isUIOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab))
        {
            CloseKeypadUI();
            return;
        }

        // Support physical number keys
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                OnKeyPressed(i.ToString());
            }
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnKeyPressed("ENTER");
        if (Input.GetKeyDown(KeyCode.Backspace)) OnKeyPressed("CLR");
    }

    public string GetPrompt()
    {
        if (isGateUnlocked)
        {
            return "[Lobby Exit Keypad] (Bukas Na - Na-override ang Lockdown)";
        }

        bool knowsCode = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("GuardLogbookCode");
        return knowsCode
            ? "[E] I-override ang Lockdown gamit ang Code 9-4-2-1"
            : "[E] Emergency Lobby Keypad (Naka-lock - 4-Digit Code)";
    }

    public void Interact()
    {
        if (isGateUnlocked) return;
        OpenKeypadUI();
    }

    public void OpenKeypadUI()
    {
        _isUIOpen = true;
        currentEnteredCode = "";
        UpdateDisplay();
        SetUIVisible(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.SetControlsEnabled(false);
    }

    public void CloseKeypadUI()
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
