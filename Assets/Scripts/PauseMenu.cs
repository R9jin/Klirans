using UnityEngine;

using UnityEngine.SceneManagement;

using UnityEngine.UI;

using UnityEngine.EventSystems;



/// <summary>

/// Controls the in-game Pause Menu for KLIRANS.

/// Replicates the authentic KLIRANS Main Menu 1:1 with left-aligned horror typography,

/// dark crimson-black vignette backdrop (MenuLeftGradient), and retro PSX hover feedback.

/// </summary>

public class PauseMenu : MonoBehaviour

{

    public static PauseMenu Instance { get; private set; }

    public static bool GameIsPaused = false;



    [Tooltip("Drag your PauseMenuPanel here from the Hierarchy")]

    public GameObject pauseMenuUI;



    [Header("Sub-Panels")]

    [SerializeField] private GameObject _mainButtonsPanel;

    [SerializeField] private GameObject _settingsPanel;

    [SerializeField] private GameObject _controlsPanel;



    [Header("Settings Sliders")]

    [SerializeField] private Slider _volumeSlider;

    [SerializeField] private Text _volumeValueText;

    [SerializeField] private Slider _fovSlider;

    [SerializeField] private Text _fovValueText;

    [SerializeField] private Slider _sensitivitySlider;

    [SerializeField] private Text _sensitivityValueText;



    private bool _wasCursorVisibleBeforePause = false;

    private CursorLockMode _cursorLockBeforePause = CursorLockMode.Locked;



    private void Awake()

    {

        if (Instance != null && Instance != this)

        {

            if (GetComponent<Canvas>() == null && pauseMenuUI == null)

            {

                Destroy(gameObject);

                return;

            }

            Destroy(Instance.gameObject);

        }

        Instance = this;

        EnsurePauseMenuUIBuilt();

    }



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

        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        if (_controlsPanel != null) _controlsPanel.SetActive(false);

        if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);

    }



    private void Update()

    {

        if (Input.GetKeyDown(KeyCode.Escape))

        {

            if (GameSettingsManager.IsSettingsOpen)

            {

                return;

            }



            // Do not open Pause Menu on Escape if a puzzle or modal is closing on this frame,

            // or if a cutscene or Game Over is playing.

            if (!GameIsPaused && IsAnyModalOrPuzzleOpen)

            {

                return;

            }



            if (GameIsPaused)

            {

                if (_settingsPanel != null && _settingsPanel.activeSelf)

                {

                    CloseSettingsPanel();

                    return;

                }

                if (_controlsPanel != null && _controlsPanel.activeSelf)

                {

                    CloseControlsPanel();

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

            if (ExecutiveSafePuzzle.Instance != null && ExecutiveSafePuzzle.Instance.IsOpen) return true;

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

        // 1. Guidance Scrambled Letter Core Values Puzzle (Signature 1)

        var guidancePuzzles = Object.FindObjectsByType<GuidanceWordPuzzle>(FindObjectsInactive.Include);

        for (int i = 0; i < guidancePuzzles.Length; i++)

        {

            if (guidancePuzzles[i] != null)

            {

                guidancePuzzles[i].ClosePuzzle();

                if (guidancePuzzles[i].puzzlePanel != null) guidancePuzzles[i].puzzlePanel.SetActive(false);

                if (guidancePuzzles[i].backdropOverlay != null) guidancePuzzles[i].backdropOverlay.SetActive(false);

            }

        }

        if (GuidanceWordPuzzle.Instance != null)

        {

            GuidanceWordPuzzle.Instance.ClosePuzzle();

            if (GuidanceWordPuzzle.Instance.puzzlePanel != null) GuidanceWordPuzzle.Instance.puzzlePanel.SetActive(false);

            if (GuidanceWordPuzzle.Instance.backdropOverlay != null) GuidanceWordPuzzle.Instance.backdropOverlay.SetActive(false);

        }



        // 2. Cashier Balance / Ledger Settlement Puzzle (Signature 5)

        var cashierPuzzles = Object.FindObjectsByType<CashierBalancePuzzle>(FindObjectsInactive.Include);

        for (int i = 0; i < cashierPuzzles.Length; i++)

        {

            if (cashierPuzzles[i] != null)

            {

                cashierPuzzles[i].ClosePuzzle();

                if (cashierPuzzles[i].puzzlePanel != null) cashierPuzzles[i].puzzlePanel.SetActive(false);

                if (cashierPuzzles[i].backdropOverlay != null) cashierPuzzles[i].backdropOverlay.SetActive(false);

            }

        }

        if (CashierBalancePuzzle.Instance != null)

        {

            CashierBalancePuzzle.Instance.ClosePuzzle();

            if (CashierBalancePuzzle.Instance.puzzlePanel != null) CashierBalancePuzzle.Instance.puzzlePanel.SetActive(false);

            if (CashierBalancePuzzle.Instance.backdropOverlay != null) CashierBalancePuzzle.Instance.backdropOverlay.SetActive(false);

        }



        // 3. Registrar Document Sorting Puzzle (Signature 4)

        var registrarPuzzles = Object.FindObjectsByType<RegistrarDocumentSortPuzzle>(FindObjectsInactive.Include);

        for (int i = 0; i < registrarPuzzles.Length; i++)

        {

            if (registrarPuzzles[i] != null)

            {

                registrarPuzzles[i].ClosePuzzle();

                if (registrarPuzzles[i].puzzlePanel != null) registrarPuzzles[i].puzzlePanel.SetActive(false);

                if (registrarPuzzles[i].backdropOverlay != null) registrarPuzzles[i].backdropOverlay.SetActive(false);

            }

        }

        if (RegistrarDocumentSortPuzzle.Instance != null)

        {

            RegistrarDocumentSortPuzzle.Instance.ClosePuzzle();

            if (RegistrarDocumentSortPuzzle.Instance.puzzlePanel != null) RegistrarDocumentSortPuzzle.Instance.puzzlePanel.SetActive(false);

            if (RegistrarDocumentSortPuzzle.Instance.backdropOverlay != null) RegistrarDocumentSortPuzzle.Instance.backdropOverlay.SetActive(false);

        }



        // 4. Guidance Safe Lock Dial & Executive Safe Lock Dial

        if (GuidanceSafePuzzle.Instance != null)

        {

            GuidanceSafePuzzle.Instance.CloseSafeUI();

        }

        if (ExecutiveSafePuzzle.Instance != null)

        {

            ExecutiveSafePuzzle.Instance.CloseSafeUI();

        }



        // 5. Lobby Keypad Interact

        if (LobbyKeypadInteract.Instance != null)

        {

            LobbyKeypadInteract.Instance.CloseKeypadUI();

        }



        // 6. Lockpicking Minigame

        if (LockpickingMinigame.Instance != null)

        {

            LockpickingMinigame.Instance.ForceClose();

        }



        // 7. 3D & 2D Item Inspection Modals

        if (ItemInspectionViewer3D.Instance != null)

        {

            ItemInspectionViewer3D.Instance.Close();

        }

        if (ItemInspectionModal.Instance != null)

        {

            ItemInspectionModal.Instance.Close();

        }



        // 8. Ham & Cheese Canteen Vendor

        if (HamAndCheeseVendorUI.Instance != null)

        {

            HamAndCheeseVendorUI.Instance.CloseUI();

        }



        // 9. Player Inventory Screen (Limit 6 items)

        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)

        {

            InventoryScreen.Instance.Toggle();

        }



        // 10. NPC Tagalog Dialogue System

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

        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        if (_controlsPanel != null) _controlsPanel.SetActive(false);

        if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);

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



        if (pauseMenuUI != null)

        {

            Canvas parentCanvas = pauseMenuUI.GetComponentInParent<Canvas>();

            if (parentCanvas != null)

            {

                parentCanvas.gameObject.SetActive(true);

                parentCanvas.sortingOrder = Mathf.Max(parentCanvas.sortingOrder, 950);

            }

            pauseMenuUI.SetActive(true);

            pauseMenuUI.transform.SetAsLastSibling();

        }



        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        if (_controlsPanel != null) _controlsPanel.SetActive(false);

        if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);



        RefreshSliderValues();



        Time.timeScale = 0f;

        AudioListener.pause = true;

        GameIsPaused = true;



        Cursor.lockState = CursorLockMode.None;

        Cursor.visible = true;



        Debug.Log("[PauseMenu] Game Paused.");

    }



    public void OpenSettingsPanel()

    {

        EnsurePauseMenuUIBuilt();

        if (_controlsPanel != null) _controlsPanel.SetActive(false);

        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);



        GameSettingsManager.OpenSettingsModal(() =>

        {

            if (GameIsPaused && pauseMenuUI != null)

            {

                pauseMenuUI.SetActive(true);

                if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);

                if (_controlsPanel != null) _controlsPanel.SetActive(false);

            }

        });

    }



    public void CloseSettingsPanel()

    {

        GameSettingsManager.CloseSettingsModal();

        if (GameIsPaused && pauseMenuUI != null)

        {

            pauseMenuUI.SetActive(true);

            if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);

            if (_controlsPanel != null) _controlsPanel.SetActive(false);

        }

    }



    public void OpenControlsPanel()

    {

        EnsurePauseMenuUIBuilt();

        if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(false);

        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        if (_controlsPanel != null) _controlsPanel.SetActive(true);

    }



    public void CloseControlsPanel()

    {

        if (_controlsPanel != null) _controlsPanel.SetActive(false);

        if (_mainButtonsPanel != null) _mainButtonsPanel.SetActive(true);

    }



    // Backwards-compatible aliases

    public void OpenSettingsModal() => OpenSettingsPanel();

    public void OpenControlsGuide() => OpenControlsPanel();



    public void RestartMission()

    {

        Time.timeScale = 1f;

        AudioListener.pause = false;

        GameIsPaused = false;



        Scene currentScene = SceneManager.GetActiveScene();

        SceneManager.LoadScene(currentScene.name);

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



    private void RefreshSliderValues()

    {

        float vol = PlayerPrefs.GetFloat("MasterVolume", 1.0f);

        if (_volumeSlider != null) _volumeSlider.value = vol;

        UpdateVolumeText(vol);



        float fov = PlayerPrefs.GetFloat("CameraFOV", 60f);

        if (_fovSlider != null) _fovSlider.value = fov;

        UpdateFOVText(fov);



        float sens = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);

        if (_sensitivitySlider != null) _sensitivitySlider.value = sens;

        UpdateSensitivityText(sens);

    }



    public void OnVolumeChanged(float val)

    {

        AudioListener.volume = val;

        PlayerPrefs.SetFloat("MasterVolume", val);

        PlayerPrefs.Save();

        UpdateVolumeText(val);

        if (GameSettingsManager.Instance != null)

        {

            GameSettingsManager.Instance.LoadSettings();

            GameSettingsManager.Instance.ApplyAllSettings();

        }

    }



    public void OnFOVChanged(float val)

    {

        PlayerPrefs.SetFloat("CameraFOV", val);

        PlayerPrefs.Save();

        UpdateFOVText(val);



        if (GameSettingsManager.Instance != null)

        {

            GameSettingsManager.Instance.LoadSettings();

            GameSettingsManager.Instance.ApplyAllSettings();

        }

        else

        {

            var cam = Camera.main;

            if (cam != null) cam.fieldOfView = val;

        }

    }



    public void OnSensitivityChanged(float val)

    {

        PlayerPrefs.SetFloat("MouseSensitivity", val);

        PlayerPrefs.Save();

        UpdateSensitivityText(val);



        if (GameSettingsManager.Instance != null)

        {

            GameSettingsManager.Instance.LoadSettings();

            GameSettingsManager.Instance.ApplyAllSettings();

        }

        else

        {

            var pm = FindAnyObjectByType<PlayerMovement>();

            if (pm != null) pm.lookSpeed = val;

        }

    }



    private void UpdateVolumeText(float val)

    {

        if (_volumeValueText != null)

            _volumeValueText.text = Mathf.RoundToInt(val * 100f) + "%";

    }



    private void UpdateFOVText(float val)

    {

        if (_fovValueText != null)

            _fovValueText.text = Mathf.RoundToInt(val) + "°";

    }



    private void UpdateSensitivityText(float val)

    {

        if (_sensitivityValueText != null)

            _sensitivityValueText.text = val.ToString("F1");

    }



    private void EnsurePauseMenuUIBuilt()

    {

        // Guarantee EventSystem exists so buttons can be clicked

        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)

        {

            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            DontDestroyOnLoad(esGO);

        }



        // 1. Locate or create parent Canvas

        Canvas parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas == null)

        {

            var canvasGO = GameObject.Find("PauseCanvas");

            if (canvasGO != null) parentCanvas = canvasGO.GetComponent<Canvas>();

        }



        if (parentCanvas == null)

        {

            var canvasGO = new GameObject("PauseCanvas");

            parentCanvas = canvasGO.AddComponent<Canvas>();

            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            parentCanvas.sortingOrder = 950;

            var scaler = canvasGO.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

        }



        parentCanvas.gameObject.SetActive(true);

        if (parentCanvas.GetComponent<GraphicRaycaster>() == null)

        {

            parentCanvas.gameObject.AddComponent<GraphicRaycaster>();

        }

        parentCanvas.sortingOrder = Mathf.Max(parentCanvas.sortingOrder, 950);



        // 2. Locate or create pauseMenuUI panel

        if (pauseMenuUI == null)

        {

            Transform found = parentCanvas.transform.Find("PauseMenuPanel");

            if (found != null) pauseMenuUI = found.gameObject;

            else

            {

                var foundGlobal = GameObject.Find("PauseMenuPanel");

                if (foundGlobal != null) pauseMenuUI = foundGlobal;

            }

        }



        if (pauseMenuUI == null)

        {

            var panelGO = new GameObject("PauseMenuPanel", typeof(RectTransform));

            panelGO.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRT = panelGO.GetComponent<RectTransform>();

            panelRT.anchorMin = Vector2.zero;

            panelRT.anchorMax = Vector2.one;

            panelRT.offsetMin = Vector2.zero;

            panelRT.offsetMax = Vector2.zero;

            pauseMenuUI = panelGO;

        }



        // Clean out any obsolete centered PauseCardContainer or ContentPanel

        Transform oldCard = pauseMenuUI.transform.Find("PauseCardContainer");

        if (oldCard != null)

        {

            if (Application.isPlaying) Destroy(oldCard.gameObject);

            else DestroyImmediate(oldCard.gameObject);

        }

        Transform oldContent = pauseMenuUI.transform.Find("ContentPanel");

        if (oldContent != null)

        {

            if (Application.isPlaying) Destroy(oldContent.gameObject);

            else DestroyImmediate(oldContent.gameObject);

        }



        // Locate MainMenu_UI

        Transform menuUITrans = pauseMenuUI.transform.Find("MainMenu_UI");

        if (menuUITrans == null)

        {

            BuildMainMenuPauseLayout();

            menuUITrans = pauseMenuUI.transform.Find("MainMenu_UI");

        }



        WireRuntimeComponents(menuUITrans);

    }



    private void WireRuntimeComponents(Transform menuUITrans)

    {

        if (menuUITrans == null) return;



        // Subtitle text check

        Transform subT = menuUITrans.Find("TitleBox/SubtitleText");

        if (subT != null)

        {

            Text subTxt = subT.GetComponent<Text>();

            if (subTxt != null) subTxt.text = "CLEARANCE PAUSED  |  CAMPUS HORROR";

        }



        // Panels

        Transform btnPanelT = menuUITrans.Find("MainButtonsPanel");

        if (btnPanelT != null) _mainButtonsPanel = btnPanelT.gameObject;



        Transform setPanelT = menuUITrans.Find("SettingsPanel");

        if (setPanelT != null) _settingsPanel = setPanelT.gameObject;



        Transform ctrlPanelT = menuUITrans.Find("ControlsPanel");

        if (ctrlPanelT != null) _controlsPanel = ctrlPanelT.gameObject;



        // Wire Main Buttons

        if (btnPanelT != null)

        {

            BindBtn(btnPanelT, "Btn_Resume", Resume);
            BindBtn(btnPanelT, "Btn_SaveGame", () => {
                if (SaveManager.Instance != null) SaveManager.Instance.SaveGame("QuickSave");
            });
            BindBtn(btnPanelT, "Btn_LoadGame", () => {
                if (SaveManager.Instance != null) {
                    SaveManager.Instance.LoadGame();
                    Resume();
                }
            });

            BindBtn(btnPanelT, "Btn_Restart", RestartMission);

            BindBtn(btnPanelT, "Btn_Settings", OpenSettingsPanel);

            BindBtn(btnPanelT, "Btn_Controls", OpenControlsPanel);

            BindBtn(btnPanelT, "Btn_MainMenu", LoadMainMenu);

            BindBtn(btnPanelT, "Btn_Quit", QuitGame);

        }



        // Wire Settings Sliders & Back

        if (setPanelT != null)

        {

            Transform rowVol = setPanelT.Find("Row_MASTERVOLUME");

            if (rowVol != null)

            {

                _volumeSlider = rowVol.Find("Slider")?.GetComponent<Slider>();

                _volumeValueText = rowVol.Find("ValText")?.GetComponent<Text>();

                if (_volumeSlider != null)

                {

                    _volumeSlider.minValue = 0f;

                    _volumeSlider.maxValue = 1f;

                    _volumeSlider.onValueChanged.RemoveAllListeners();

                    _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

                }

            }



            Transform rowFOV = setPanelT.Find("Row_CAMERAFOV");

            if (rowFOV != null)

            {

                _fovSlider = rowFOV.Find("Slider")?.GetComponent<Slider>();

                _fovValueText = rowFOV.Find("ValText")?.GetComponent<Text>();

                if (_fovSlider != null)

                {

                    _fovSlider.minValue = 50f;

                    _fovSlider.maxValue = 110f;

                    _fovSlider.onValueChanged.RemoveAllListeners();

                    _fovSlider.onValueChanged.AddListener(OnFOVChanged);

                }

            }



            Transform rowSens = setPanelT.Find("Row_SENSITIVITY");

            if (rowSens != null)

            {

                _sensitivitySlider = rowSens.Find("Slider")?.GetComponent<Slider>();

                _sensitivityValueText = rowSens.Find("ValText")?.GetComponent<Text>();

                if (_sensitivitySlider != null)

                {

                    _sensitivitySlider.minValue = 0.5f;

                    _sensitivitySlider.maxValue = 5.0f;

                    _sensitivitySlider.onValueChanged.RemoveAllListeners();

                    _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

                }

            }



            BindBtn(setPanelT, "Btn_BackSettings", CloseSettingsPanel);

        }



        // Wire Controls Back

        if (ctrlPanelT != null)

        {

            BindBtn(ctrlPanelT, "Btn_BackControls", CloseControlsPanel);

            BindBtn(ctrlPanelT, "Btn_BackCredits", CloseControlsPanel);

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



    private void BuildMainMenuPauseLayout()

    {

        for (int i = pauseMenuUI.transform.childCount - 1; i >= 0; i--)

        {

            var child = pauseMenuUI.transform.GetChild(i).gameObject;

            if (Application.isPlaying) Destroy(child);

            else DestroyImmediate(child);

        }



        Image backdropImg = pauseMenuUI.GetComponent<Image>();

        if (backdropImg == null) backdropImg = pauseMenuUI.AddComponent<Image>();

        backdropImg.color = new Color(0.02f, 0.02f, 0.03f, 0.75f);

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



        Sprite gradientSprite = Resources.Load<Sprite>("MenuLeftGradient");

#if UNITY_EDITOR

        if (gradientSprite == null)

            gradientSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/MenuLeftGradient.png");

#endif



        // Left Vignette Gradient

        GameObject vigGO = new GameObject("LeftVignetteGradient", typeof(RectTransform), typeof(Image));

        vigGO.transform.SetParent(pauseMenuUI.transform, false);

        RectTransform vigRT = vigGO.GetComponent<RectTransform>();

        vigRT.anchorMin = new Vector2(0f, 0f);

        vigRT.anchorMax = new Vector2(0.52f, 1f);

        vigRT.offsetMin = Vector2.zero;

        vigRT.offsetMax = Vector2.zero;

        Image vigImg = vigGO.GetComponent<Image>();

        if (gradientSprite != null) vigImg.sprite = gradientSprite;

        vigImg.color = Color.white;

        vigImg.raycastTarget = false;



        // MainMenu_UI Root

        GameObject uiGO = new GameObject("MainMenu_UI", typeof(RectTransform));

        uiGO.transform.SetParent(pauseMenuUI.transform, false);

        RectTransform uiRT = uiGO.GetComponent<RectTransform>();

        uiRT.anchorMin = new Vector2(0.03f, 0.00f);

        uiRT.anchorMax = new Vector2(0.44f, 1.00f);

        uiRT.offsetMin = Vector2.zero;

        uiRT.offsetMax = Vector2.zero;



        // Title Box

        GameObject titleBox = new GameObject("TitleBox", typeof(RectTransform), typeof(VerticalLayoutGroup));

        titleBox.transform.SetParent(uiGO.transform, false);

        RectTransform tbRT = titleBox.GetComponent<RectTransform>();

        tbRT.anchorMin = new Vector2(0f, 0.60f);

        tbRT.anchorMax = new Vector2(1f, 0.95f);

        tbRT.offsetMin = Vector2.zero;

        tbRT.offsetMax = Vector2.zero;

        var vlgTitle = titleBox.GetComponent<VerticalLayoutGroup>();

        vlgTitle.spacing = 4f;

        vlgTitle.padding = new RectOffset(12, 12, 8, 8);

        vlgTitle.childControlWidth = true;

        vlgTitle.childControlHeight = true;

        vlgTitle.childForceExpandWidth = true;

        vlgTitle.childForceExpandHeight = true;



        CreateMainLabel(titleBox.transform, "GameTitleText", "KLIRANS", horrorFont, 96, TextAnchor.LowerLeft,

            new Color(0.96f, 0.96f, 0.96f, 1f), new Vector2(3f, -3f));

        CreateMainLabel(titleBox.transform, "SubtitleText", "CLEARANCE PAUSED  |  CAMPUS HORROR", horrorFont, 20, TextAnchor.UpperLeft,

            new Color(0.85f, 0.25f, 0.25f, 1f), new Vector2(1.5f, -1.5f));



        // Buttons Panel

        GameObject btnPanel = new GameObject("MainButtonsPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));

        btnPanel.transform.SetParent(uiGO.transform, false);

        RectTransform bpRT = btnPanel.GetComponent<RectTransform>();

        bpRT.anchorMin = new Vector2(0f, 0.06f);

        bpRT.anchorMax = new Vector2(1f, 0.58f);

        bpRT.offsetMin = Vector2.zero;

        bpRT.offsetMax = Vector2.zero;

        var vlgBtn = btnPanel.GetComponent<VerticalLayoutGroup>();

        vlgBtn.spacing = 8f;

        vlgBtn.childControlWidth = true;

        vlgBtn.childControlHeight = true;

        vlgBtn.childForceExpandWidth = true;

        vlgBtn.childForceExpandHeight = true;



        CreateMainMenuButton(btnPanel.transform, "Btn_Resume", "Resume", horrorFont, Resume);
        CreateMainMenuButton(btnPanel.transform, "Btn_SaveGame", "Save Game (F5)", horrorFont, () => {
            if (SaveManager.Instance != null) SaveManager.Instance.SaveGame("QuickSave");
        });
        CreateMainMenuButton(btnPanel.transform, "Btn_LoadGame", "Load Game (F8)", horrorFont, () => {
            if (SaveManager.Instance != null) {
                SaveManager.Instance.LoadGame();
                Resume();
            }
        });

        CreateMainMenuButton(btnPanel.transform, "Btn_Restart", "Restart Shift", horrorFont, RestartMission);

        CreateMainMenuButton(btnPanel.transform, "Btn_Settings", "Settings", horrorFont, OpenSettingsPanel);

        CreateMainMenuButton(btnPanel.transform, "Btn_Controls", "Controls & Survival", horrorFont, OpenControlsPanel);

        CreateMainMenuButton(btnPanel.transform, "Btn_MainMenu", "Main Menu", horrorFont, LoadMainMenu);

        CreateMainMenuButton(btnPanel.transform, "Btn_Quit", "Quit", horrorFont, QuitGame);



        _mainButtonsPanel = btnPanel;

    }



    private GameObject CreateMainLabel(Transform parent, string name, string text, Font font, int size, TextAnchor align, Color color, Vector2 outlineDist)

    {

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));

        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();

        t.font = font;

        t.text = text;

        t.fontSize = size;

        t.alignment = align;

        t.color = color;

        t.raycastTarget = false;



        Outline ol = go.GetComponent<Outline>();

        ol.effectColor = new Color(0f, 0f, 0f, 0.95f);

        ol.effectDistance = outlineDist;

        return go;

    }



    private Button CreateMainMenuButton(Transform parent, string name, string label, Font font, UnityEngine.Events.UnityAction onClick)

    {

        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonHover));

        btnGO.transform.SetParent(parent, false);



        Image img = btnGO.GetComponent<Image>();

        img.color = new Color(0f, 0f, 0f, 0f); // Transparent

        img.raycastTarget = true;



        Button btn = btnGO.GetComponent<Button>();

        btn.transition = Selectable.Transition.ColorTint;

        btn.onClick.AddListener(onClick);



        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));

        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform tRT = textGO.GetComponent<RectTransform>();

        tRT.anchorMin = Vector2.zero;

        tRT.anchorMax = Vector2.one;

        tRT.offsetMin = Vector2.zero;

        tRT.offsetMax = Vector2.zero;



        Text txt = textGO.GetComponent<Text>();

        txt.font = font;

        txt.text = label;

        txt.fontSize = 38;

        txt.alignment = TextAnchor.MiddleLeft;

        txt.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        txt.raycastTarget = false;



        Outline ol = textGO.GetComponent<Outline>();

        ol.effectColor = new Color(0f, 0f, 0f, 0.95f);

        ol.effectDistance = new Vector2(2f, -2f);



        MenuButtonHover hover = btnGO.GetComponent<MenuButtonHover>();

        hover.targetText = txt;

        hover.SetLabel(label);

        hover.prefixOnHover = "> ";

        hover.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);

        hover.hoverColor = new Color(1f, 0.20f, 0.20f, 1f);



        AudioClip chime = Resources.Load<AudioClip>("objective_chime");

#if UNITY_EDITOR

        if (chime == null)

            chime = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");

#endif

        hover.hoverSound = chime;

        hover.clickSound = chime;



        return btn;

    }

}

