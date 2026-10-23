using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Tooltip("Drag your PauseMenuPanel here from the Hierarchy")]
    public GameObject pauseMenuUI;

    private bool _wasCursorVisibleBeforePause = false;
    private CursorLockMode _cursorLockBeforePause = CursorLockMode.Locked;
    private GameObject _controlsModalPanel;

    private void Start()
    {
        GameIsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        EnsurePauseMenuUIBuilt();

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Do not open Pause Menu on Escape if a puzzle or modal is closing on this frame,
            // or if a cutscene or Game Over is playing.
            if (!GameIsPaused && IsAnyModalOrPuzzleOpen)
            {
                return;
            }

            if (GameIsPaused)
            {
                if (_controlsModalPanel != null && _controlsModalPanel.activeSelf)
                {
                    _controlsModalPanel.SetActive(false);
                    return;
                }
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    /// <summary>
    /// True if any interactive puzzle, minigame, inspection modal, inventory, or dialogue is currently open.
    /// </summary>
    public static bool IsAnyActivePuzzleOrModalOpen
    {
        get
        {
            if (ItemInspectionModal.IsOpen) return true;
            if (GuidanceWordPuzzle.Instance != null && GuidanceWordPuzzle.Instance.IsOpen) return true;
            if (CashierBalancePuzzle.Instance != null && CashierBalancePuzzle.Instance.IsOpen) return true;
            if (RegistrarDocumentSortPuzzle.Instance != null && RegistrarDocumentSortPuzzle.Instance.IsOpen) return true;
            if (GuidanceSafePuzzle.Instance != null && GuidanceSafePuzzle.Instance.IsOpen) return true;
            if (LobbyKeypadInteract.Instance != null && LobbyKeypadInteract.Instance.IsOpen) return true;
            if (LockpickingMinigame.IsMinigameActive) return true;
            if (HamAndCheeseVendorUI.Instance != null && HamAndCheeseVendorUI.Instance.IsOpen) return true;
            if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) return true;
            if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return true;
            return false;
        }
    }

    /// <summary>
    /// Force-exits all currently open puzzles, minigames, inspection modals, vendor menus, inventory, and dialogues.
    /// Called immediately when the player is currently being or about to be jumpscared (especially by The Proctor)
    /// or when Game Over triggers.
    /// </summary>
    public static void ForceCloseAllModalsAndPuzzles()
    {
        if (GuidanceWordPuzzle.Instance != null && GuidanceWordPuzzle.Instance.IsOpen)
        {
            GuidanceWordPuzzle.Instance.ClosePuzzle();
        }
        if (CashierBalancePuzzle.Instance != null && CashierBalancePuzzle.Instance.IsOpen)
        {
            CashierBalancePuzzle.Instance.ClosePuzzle();
        }
        if (RegistrarDocumentSortPuzzle.Instance != null && RegistrarDocumentSortPuzzle.Instance.IsOpen)
        {
            RegistrarDocumentSortPuzzle.Instance.ClosePuzzle();
        }
        if (GuidanceSafePuzzle.Instance != null && GuidanceSafePuzzle.Instance.IsOpen)
        {
            GuidanceSafePuzzle.Instance.CloseSafeUI();
        }
        if (LobbyKeypadInteract.Instance != null && LobbyKeypadInteract.Instance.IsOpen)
        {
            LobbyKeypadInteract.Instance.CloseKeypadUI();
        }
        if (LockpickingMinigame.IsMinigameActive && LockpickingMinigame.Instance != null)
        {
            LockpickingMinigame.Instance.ForceClose();
        }
        if (ItemInspectionViewer3D.IsOpen && ItemInspectionViewer3D.Instance != null)
        {
            ItemInspectionViewer3D.Instance.Close();
        }
        if (ItemInspectionModal.IsOpen && ItemInspectionModal.Instance != null)
        {
            ItemInspectionModal.Instance.Close();
        }
        if (HamAndCheeseVendorUI.Instance != null && HamAndCheeseVendorUI.Instance.IsOpen)
        {
            HamAndCheeseVendorUI.Instance.CloseUI();
        }
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
        {
            InventoryScreen.Instance.Toggle();
        }
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive)
        {
            NPCDialogueSystem.Instance.ForceCloseImmediate();
        }
    }

    public static bool IsAnyModalOrPuzzleOpen
    {
        get
        {
            if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return true;
            if (GameOverUI.IsCutsceneActive) return true;
            if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver) return true;
            if (IsAnyActivePuzzleOrModalOpen) return true;
            return false;
        }
    }

    public void Resume()
    {
        EnsurePauseMenuUIBuilt();
        if (_controlsModalPanel != null) _controlsModalPanel.SetActive(false);
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameIsPaused = false;

        // Restore cursor state depending on whether a modal/inventory is open
        if (IsAnyModalOrPuzzleOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log("[PauseMenu] Game Resumed.");
    }

    public void Pause()
    {
        EnsurePauseMenuUIBuilt();

        _wasCursorVisibleBeforePause = Cursor.visible;
        _cursorLockBeforePause = Cursor.lockState;

        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        GameIsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[PauseMenu] Game Paused.");
    }

    public void RestartMission()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameIsPaused = false;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void OpenControlsGuide()
    {
        EnsurePauseMenuUIBuilt();
        if (_controlsModalPanel != null)
        {
            _controlsModalPanel.SetActive(!_controlsModalPanel.activeSelf);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameIsPaused = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameIsPaused = false;

        Debug.Log("[PauseMenu] Quitting Game...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (GameIsPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameIsPaused = false;
        }
    }

    private void EnsurePauseMenuUIBuilt()
    {
        // Guarantee EventSystem exists so buttons can be clicked
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(esGO);
        }

        // Ensure the parent Canvas has a GraphicRaycaster and high sortingOrder so clicks always register
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            if (parentCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            parentCanvas.sortingOrder = Mathf.Max(parentCanvas.sortingOrder, 950);
        }

        if (pauseMenuUI == null)
        {
            Transform found = transform.Find("PauseMenuPanel");
            if (found != null) pauseMenuUI = found.gameObject;
        }

        if (pauseMenuUI == null) return;

        // Check if already upgraded to full themed layout
        if (pauseMenuUI.transform.Find("PauseCardContainer") != null)
        {
            // Ensure listeners are wired at runtime
            WireRuntimeButtons();
            return;
        }

        BuildThemedPauseLayout();
    }

    private void BuildThemedPauseLayout()
    {
        // Clear old prototype children
        for (int i = pauseMenuUI.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(pauseMenuUI.transform.GetChild(i).gameObject);
        }

        // Fullscreen dark crimson-black horror vignette backdrop
        Image backdropImg = pauseMenuUI.GetComponent<Image>();
        if (backdropImg == null) backdropImg = pauseMenuUI.AddComponent<Image>();
        backdropImg.color = new Color(0.02f, 0.02f, 0.03f, 0.88f);
        backdropImg.raycastTarget = true;

        RectTransform rootRT = pauseMenuUI.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Font horrorFont = Resources.Load<Font>("watch people die");
#if UNITY_EDITOR
        if (horrorFont == null)
            horrorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
#endif
        Font monoFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Center Dossier Card Container
        GameObject cardGO = new GameObject("PauseCardContainer", typeof(RectTransform), typeof(Image), typeof(Outline));
        cardGO.transform.SetParent(pauseMenuUI.transform, false);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(520f, 560f);

        Image cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.06f, 0.06f, 0.08f, 0.96f);

        Outline cardOutline = cardGO.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.65f, 0.12f, 0.12f, 0.80f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        // Top Red Accent Bar
        GameObject topBar = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(cardGO.transform, false);
        RectTransform topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0f, 1f);
        topBarRT.anchorMax = new Vector2(1f, 1f);
        topBarRT.pivot = new Vector2(0.5f, 1f);
        topBarRT.sizeDelta = new Vector2(0f, 6f);
        topBar.GetComponent<Image>().color = new Color(0.75f, 0.12f, 0.12f, 1f);

        // University Subtitle
        CreateLabel(cardGO.transform, "SubHeader", "PAMANTASAN NG CABUYAO // STUDENT CLEARANCE PROTOCOL",
            monoFont, 12, FontStyle.Bold, new Color(0.75f, 0.68f, 0.55f, 0.9f),
            new Vector2(0f, 232f), new Vector2(460f, 24f));

        // Main Title ("PAUSED")
        CreateLabel(cardGO.transform, "TitleText", "SYSTEM PAUSED",
            horrorFont != null ? horrorFont : monoFont, 42, FontStyle.Bold, new Color(0.92f, 0.18f, 0.18f, 1f),
            new Vector2(0f, 188f), new Vector2(460f, 54f));

        // Mode Status Indicator
        string modeDesc = GameModeManager.IsFreeRoamMode
            ? "ACTIVE MODE: CAMPUS TOUR / FREE ROAM"
            : "ACTIVE MODE: NIGHTMARE CLEARANCE SHIFT";
        CreateLabel(cardGO.transform, "ModeStatus", modeDesc,
            monoFont, 13, FontStyle.Italic, new Color(0.65f, 0.72f, 0.78f, 0.9f),
            new Vector2(0f, 142f), new Vector2(460f, 24f));

        // Divider Line
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        div.transform.SetParent(cardGO.transform, false);
        RectTransform divRT = div.GetComponent<RectTransform>();
        divRT.anchoredPosition = new Vector2(0f, 118f);
        divRT.sizeDelta = new Vector2(420f, 1.5f);
        div.GetComponent<Image>().color = new Color(0.65f, 0.15f, 0.15f, 0.5f);

        // Buttons Stack
        CreateMenuButton(cardGO.transform, "Btn_Resume", "RESUME CLEARANCE  [ESC]",
            monoFont, new Vector2(0f, 62f), new Color(0.14f, 0.24f, 0.16f, 0.95f), Resume);

        CreateMenuButton(cardGO.transform, "Btn_Controls", "CONTROLS & SURVIVAL GUIDE",
            monoFont, new Vector2(0f, -2f), new Color(0.14f, 0.16f, 0.22f, 0.95f), OpenControlsGuide);

        CreateMenuButton(cardGO.transform, "Btn_Restart", "RESTART SHIFT",
            monoFont, new Vector2(0f, -66f), new Color(0.22f, 0.16f, 0.12f, 0.95f), RestartMission);

        CreateMenuButton(cardGO.transform, "Btn_MainMenu", "RETURN TO MAIN MENU",
            monoFont, new Vector2(0f, -130f), new Color(0.22f, 0.12f, 0.12f, 0.95f), LoadMainMenu);

        CreateMenuButton(cardGO.transform, "Btn_Quit", "QUIT TO DESKTOP",
            monoFont, new Vector2(0f, -194f), new Color(0.16f, 0.10f, 0.10f, 0.95f), QuitGame);

        // Footer Hint
        CreateLabel(cardGO.transform, "FooterHint", "Press [ESC] at any time to resume",
            monoFont, 12, FontStyle.Normal, new Color(0.55f, 0.55f, 0.58f, 0.8f),
            new Vector2(0f, -252f), new Vector2(440f, 22f));

        // Controls Guide Sub-Modal
        BuildControlsModal(pauseMenuUI.transform, monoFont);
    }

    private void WireRuntimeButtons()
    {
        Transform card = pauseMenuUI.transform.Find("PauseCardContainer");
        if (card == null) return;

        BindBtn(card, "Btn_Resume", Resume);
        BindBtn(card, "Btn_Controls", OpenControlsGuide);
        BindBtn(card, "Btn_Restart", RestartMission);
        BindBtn(card, "Btn_MainMenu", LoadMainMenu);
        BindBtn(card, "Btn_Quit", QuitGame);

        Transform ctrlModal = pauseMenuUI.transform.Find("ControlsGuideModal");
        if (ctrlModal != null)
        {
            _controlsModalPanel = ctrlModal.gameObject;
            BindBtn(ctrlModal, "Btn_CloseControls", () => _controlsModalPanel.SetActive(false));
        }
    }

    private void BindBtn(Transform parent, string name, UnityEngine.Events.UnityAction action)
    {
        Transform t = parent.Find(name);
        if (t == null) return;
        Button b = t.GetComponent<Button>();
        if (b != null)
        {
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }
    }

    private void BuildControlsModal(Transform parent, Font font)
    {
        _controlsModalPanel = new GameObject("ControlsGuideModal", typeof(RectTransform), typeof(Image), typeof(Outline));
        _controlsModalPanel.transform.SetParent(parent, false);
        RectTransform rt = _controlsModalPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640f, 520f);

        _controlsModalPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.98f);
        Outline ol = _controlsModalPanel.GetComponent<Outline>();
        ol.effectColor = new Color(0.85f, 0.68f, 0.25f, 0.9f);
        ol.effectDistance = new Vector2(2f, -2f);

        CreateLabel(_controlsModalPanel.transform, "Header", "STUDENT SURVIVAL & CONTROLS GUIDE",
            font, 20, FontStyle.Bold, new Color(0.95f, 0.82f, 0.35f, 1f),
            new Vector2(0f, 215f), new Vector2(580f, 36f));

        string guideBody =
            "<b>[W, A, S, D]</b> — Move / Navigate Corridors\n" +
            "<b>[LEFT SHIFT]</b> — Sprint (Consumes Stamina & Generates Noise)\n" +
            "<b>[C / LEFT CTRL]</b> — Crouch (Silent Movement)\n" +
            "<b>[F]</b> — Toggle Flashlight ON / OFF\n" +
            "<b>[E]</b> — Interact with Doors, NPCs, Pickups, Lockers & Puzzles\n" +
            "<b>[1 - 6] / [SCROLL]</b> — Select Hotbar Item Slot\n" +
            "<b>[V]</b> — Inspect Selected Item in 3D (Rotate to Find Hidden Clues!)\n" +
            "<b>[G]</b> — Drop Selected Item\n\n" +
            "<color=#FF6666><b>SURVIVAL TIPS:</b></color>\n" +
            "• Inspect quest items with <b>[V]</b> to discover safe combinations, cable tags & codes.\n" +
            "• Hide inside hallway <b>Lockers [E]</b> when The Proctor or Stalker Guard is hunting.\n" +
            "• Keep your flashlight aimed at the <b>Weeping Student</b> during blackouts to freeze it!";

        GameObject bodyGO = CreateLabel(_controlsModalPanel.transform, "BodyText", guideBody,
            font, 15, FontStyle.Normal, new Color(0.90f, 0.90f, 0.88f, 1f),
            new Vector2(0f, 15f), new Vector2(560f, 340f));
        Text bodyTxt = bodyGO.GetComponent<Text>();
        bodyTxt.alignment = TextAnchor.MiddleLeft;
        bodyTxt.lineSpacing = 1.25f;

        CreateMenuButton(_controlsModalPanel.transform, "Btn_CloseControls", "BACK TO PAUSE MENU",
            font, new Vector2(0f, -205f), new Color(0.24f, 0.14f, 0.14f, 0.95f),
            () => _controlsModalPanel.SetActive(false));

        _controlsModalPanel.SetActive(false);
    }

    private GameObject CreateLabel(Transform parent, string name, string text, Font font, int size, FontStyle style, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        Text txt = go.GetComponent<Text>();
        txt.font = font;
        txt.text = text;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = true;
        txt.raycastTarget = false;
        return go;
    }

    private Button CreateMenuButton(Transform parent, string name, string label, Font font, Vector2 pos, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(380f, 48f);

        Image img = btnGO.GetComponent<Image>();
        img.color = bgColor;

        Outline ol = btnGO.GetComponent<Outline>();
        ol.effectColor = new Color(0.75f, 0.25f, 0.25f, 0.55f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGO.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.8f, 0.7f, 0.7f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        CreateLabel(btnGO.transform, "Label", label, font, 16, FontStyle.Bold,
            new Color(0.95f, 0.92f, 0.88f, 1f), Vector2.zero, new Vector2(360f, 44f));

        return btn;
    }
}
