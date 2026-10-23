using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SaveManager — Robust, change-resilient Save, Load, QuickSave, and Autosave system for "KLIRANS".
/// 
/// Designed specifically to remain 100% resilient to map/level geometry changes:
/// - Stores semantic progression state (signatures, quest milestones, puzzle states, items).
/// - Ground-checks and NavMesh-samples player coordinates on load so level adjustments never spawn the player in the void.
/// - Dynamic item resolution matches assets by filename and display name.
/// - Hotkeys: [F5] Quick Save, [F8] Quick Load (also [F6]).
/// - Autosaves on major milestones: clearance signatures, cracked safes, restored power, printed forms.
/// - Full integration with Pause Menu (Save Game & Load Game buttons).
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    var go = new GameObject("SaveManager");
                    _instance = go.AddComponent<SaveManager>();
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Hotkeys")]
    public KeyCode quickSaveKey = KeyCode.F5;
    public KeyCode quickLoadKey = KeyCode.F8;
    public KeyCode alternateQuickLoadKey = KeyCode.F6;

    [Header("Autosave Settings")]
    public bool enableAutosave = true;
    public float autosaveCooldown = 4.0f;
    private float _lastAutosaveTime = -999f;

    [Header("Audio Feedback")]
    public AudioClip saveAudio;
    public AudioClip loadAudio;

    // Tracking picked up items and unlocked doors during gameplay
    private static readonly HashSet<string> _pickedUpWorldItems = new HashSet<string>();
    private static readonly HashSet<string> _unlockedDoors = new HashSet<string>();
    private static Dictionary<string, InventoryItem> _itemCache;

    // Toast Notification UI
    private GameObject _toastCanvasGO;
    private RectTransform _toastPanelRT;
    private CanvasGroup _toastCanvasGroup;
    private Text _toastText;
    private Coroutine _toastCoroutine;
    private AudioSource _audioSource;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake = false;
        }

        LoadAudioFeedback();
        InitializeItemCache();
        BuildToastUI();
    }

    private void Start()
    {
        InitializeItemCache();
    }

    private void LoadAudioFeedback()
    {
#if UNITY_EDITOR
        if (saveAudio == null)
            saveAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/clicking.mp3");
        if (loadAudio == null)
            loadAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
#endif
        if (saveAudio == null) saveAudio = Resources.Load<AudioClip>("Sounds/clicking");
        if (loadAudio == null) loadAudio = Resources.Load<AudioClip>("Sounds/door_closing");
    }

    public static void RegisterPickedUpItem(string pickupName)
    {
        if (!string.IsNullOrEmpty(pickupName))
        {
            _pickedUpWorldItems.Add(pickupName);
        }
    }

    public static void RegisterUnlockedDoor(string doorName)
    {
        if (!string.IsNullOrEmpty(doorName))
        {
            _unlockedDoors.Add(doorName);
        }
    }

    public static void InitializeItemCache()
    {
        if (_itemCache != null && _itemCache.Count > 0) return;
        _itemCache = new Dictionary<string, InventoryItem>(StringComparer.OrdinalIgnoreCase);

        // Scan all Resources
        var fromResources = Resources.LoadAll<InventoryItem>("");
        foreach (var item in fromResources)
        {
            if (item == null) continue;
            if (!_itemCache.ContainsKey(item.name)) _itemCache[item.name] = item;
            if (!_itemCache.ContainsKey(item.itemName)) _itemCache[item.itemName] = item;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InventoryItem", new[] { "Assets/Items" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
            if (item == null) continue;
            if (!_itemCache.ContainsKey(item.name)) _itemCache[item.name] = item;
            if (!_itemCache.ContainsKey(item.itemName)) _itemCache[item.itemName] = item;
        }
#endif
    }

    private void Update()
    {
        // Don't process save/load hotkeys during Game Over cutscene
        if (GameOverUI.IsCutsceneActive) return;

        // F5: Quick Save
        if (Input.GetKeyDown(quickSaveKey))
        {
            SaveGame("QuickSave");
        }

        // F8 or F6: Quick Load
        if (Input.GetKeyDown(quickLoadKey) || Input.GetKeyDown(alternateQuickLoadKey))
        {
            LoadGame();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SAVE / LOAD LOGIC
    // ═════════════════════════════════════════════════════════════════════════

    private string GetSaveDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public bool HasSaveFile(string slotName = "QuickSave")
    {
        string path = Path.Combine(GetSaveDirectory(), slotName + ".json");
        return File.Exists(path);
    }

    public string GetLatestSaveSlot()
    {
        string dir = GetSaveDirectory();
        string[] files = Directory.GetFiles(dir, "*.json");
        if (files == null || files.Length == 0) return null;

        string latestFile = null;
        DateTime latestTime = DateTime.MinValue;

        foreach (var f in files)
        {
            DateTime writeTime = File.GetLastWriteTime(f);
            if (writeTime > latestTime)
            {
                latestTime = writeTime;
                latestFile = Path.GetFileNameWithoutExtension(f);
            }
        }

        return latestFile;
    }

    public void TriggerAutosave(string milestoneName = "")
    {
        if (!enableAutosave) return;
        if (Time.time - _lastAutosaveTime < autosaveCooldown) return;
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;

        _lastAutosaveTime = Time.time;
        SaveGame("Autosave", $"AUTOSAVED: {milestoneName}");
    }

    public bool SaveGame(string slotName = "QuickSave", string customNotification = null)
    {
        try
        {
            InitializeItemCache();

            SaveData data = new SaveData();
            data.saveVersion = "1.0";
            data.saveSlot = slotName;
            data.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.playTime = Time.time;

            // 1. Player State
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            var pm = playerGO != null ? playerGO.GetComponent<PlayerMovement>() : FindAnyObjectByType<PlayerMovement>();
            if (playerGO != null)
            {
                Vector3 p = playerGO.transform.position;
                data.posX = p.x;
                data.posY = p.y;
                data.posZ = p.z;
                data.rotY = playerGO.transform.eulerAngles.y;

                if (pm != null && pm.playerCamera != null)
                {
                    data.cameraPitch = pm.playerCamera.transform.localEulerAngles.x;
                }
                if (pm != null && pm.staminaSystem != null)
                {
                    data.stamina = pm.staminaSystem.currentStamina;
                }
            }

            // Anxiety
            if (AnxietyManager.Instance != null)
            {
                data.anxiety = AnxietyManager.Instance.CurrentAnxiety;
            }

            // Flashlight
            if (FlashlightController.Instance != null)
            {
                data.isFlashlightOn = FlashlightController.Instance.IsLightOn;
            }

            // 2. Clearance Progression
            if (ClearanceManager.Instance != null)
            {
                var sigs = ClearanceManager.Instance.GetCollectedSignatures();
                foreach (var s in sigs) data.signatures.Add(s);
                data.isSlipSubmitted = ClearanceManager.Instance.IsSlipSubmitted;
                data.guidanceTapeDelivered = ClearanceManager.Instance.GuidanceTapeDelivered;
                data.registrarChasedAndCalmed = ClearanceManager.Instance.RegistrarChasedAndCalmed;
                data.cashierFeePaid = ClearanceManager.Instance.CashierFeePaid;
            }

            // Objective
            if (ObjectiveHUD.Instance != null)
            {
                data.objectiveTitle = ObjectiveHUD.Instance.CurrentTitle;
                data.objectiveDetail = ObjectiveHUD.Instance.CurrentDetail;
            }

            // 3. Inventory Hotbar Slots
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.EnsureSlotsInitialized();
                var slots = InventoryManager.Instance.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && !slots[i].IsEmpty && slots[i].item != null)
                    {
                        SavedSlotData sData = new SavedSlotData
                        {
                            slotIndex = i,
                            assetName = slots[i].item.name,
                            itemName = slots[i].item.itemName,
                            quantity = slots[i].quantity
                        };
                        data.inventorySlots.Add(sData);
                    }
                }
            }

            if (InventoryEquipController.Instance != null)
            {
                data.selectedHotbarSlot = InventoryEquipController.Instance.GetActiveSlotIndex();
            }

            // 4. Puzzle States
            if (GuidanceSafePuzzle.Instance != null)
                data.guidanceSafeUnlocked = GuidanceSafePuzzle.Instance.isSafeUnlocked;

            if (ExecutiveSafePuzzle.Instance != null)
                data.executiveSafeUnlocked = ExecutiveSafePuzzle.Instance.isSafeUnlocked;

            if (CircuitBreakerPuzzle.Instance != null)
            {
                data.circuitFuseInstalled = CircuitBreakerPuzzle.Instance.isFuseInstalled;
                data.circuitPowerRestored = CircuitBreakerPuzzle.Instance.isPowerRestored;
            }

            if (ComLabTerminalPuzzle.Instance != null)
            {
                data.comLabCableConnected = ComLabTerminalPuzzle.Instance.isCableConnected;
                data.comLabHoldCleared = ComLabTerminalPuzzle.Instance.isHoldCleared;
            }

            if (LobbyKeypadInteract.Instance != null)
                data.lobbyKeypadUnlocked = LobbyKeypadInteract.Instance.isGateUnlocked;

            if (CashierBalancePuzzle.Instance != null)
                data.cashierPuzzleSolved = CashierBalancePuzzle.Instance.IsSolved;

            if (RegistrarDocumentSortPuzzle.Instance != null)
                data.registrarPuzzleSolved = RegistrarDocumentSortPuzzle.Instance.IsSolved;

            if (GuidanceWordPuzzle.Instance != null)
                data.guidanceWordPuzzleSolved = GuidanceWordPuzzle.Instance.IsSolved;

            // 5. Picked Up World Items & Doors
            foreach (var item in _pickedUpWorldItems) data.pickedUpItemNames.Add(item);
            foreach (var door in _unlockedDoors) data.unlockedDoorNames.Add(door);

            // Serialize & Write JSON
            string json = JsonUtility.ToJson(data, true);
            string filePath = Path.Combine(GetSaveDirectory(), slotName + ".json");
            File.WriteAllText(filePath, json, Encoding.UTF8);

            Debug.Log($"[SaveManager] Game saved successfully to: {filePath}");

            // Audio & Toast Feedback
            if (_audioSource != null && saveAudio != null)
            {
                _audioSource.pitch = 1.05f;
                _audioSource.PlayOneShot(saveAudio, 0.85f);
            }

            string notif = customNotification ?? $"GAME SAVED ({slotName.ToUpper()})";
            ShowToast(notif, isSuccess: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to save game: {ex}");
            ShowToast("SAVE FAILED!", isSuccess: false);
            return false;
        }
    }

    public bool LoadGame(string slotName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(slotName))
            {
                slotName = GetLatestSaveSlot() ?? "QuickSave";
            }

            string filePath = Path.Combine(GetSaveDirectory(), slotName + ".json");
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveManager] Save file not found: {filePath}");
                ShowToast($"NO SAVE FOUND ({slotName})", isSuccess: false);
                return false;
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogError("[SaveManager] Failed to parse save data JSON.");
                ShowToast("CORRUPT SAVE FILE", isSuccess: false);
                return false;
            }

            InitializeItemCache();

            // Force close all open puzzles, minigames, inspection modals, or dialogue
            PauseMenu.ForceCloseAllModalsAndPuzzles();

            // 1. Restore Player Safe Position (with NavMesh / Ground Check)
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            var pm = playerGO != null ? playerGO.GetComponent<PlayerMovement>() : FindAnyObjectByType<PlayerMovement>();
            if (playerGO != null)
            {
                var cc = playerGO.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                Vector3 targetPos = new Vector3(data.posX, data.posY, data.posZ);

                // Sample NavMesh for walkable ground to ensure resilience against geometry changes
                if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var navHit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    targetPos = navHit.position + Vector3.up * 0.08f;
                }
                else if (Physics.Raycast(targetPos + Vector3.up * 1.5f, Vector3.down, out var groundHit, 5.0f))
                {
                    targetPos = groundHit.point + Vector3.up * 0.08f;
                }

                playerGO.transform.position = targetPos;
                playerGO.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);

                if (pm != null)
                {
                    if (pm.playerCamera != null)
                    {
                        pm.playerCamera.transform.localRotation = Quaternion.Euler(data.cameraPitch, 0f, 0f);
                    }
                    if (pm.staminaSystem != null)
                    {
                        pm.staminaSystem.currentStamina = Mathf.Clamp(data.stamina, 5f, pm.staminaSystem.maxStamina);
                    }
                    pm.SetControlsEnabled(true);
                    pm.isJumpscareCameraOverride = false;
                }

                if (cc != null) cc.enabled = true;
            }

            // 2. Restore Anxiety
            if (AnxietyManager.Instance != null)
            {
                AnxietyManager.Instance.SetAnxiety(data.anxiety);
            }

            // 3. Restore Flashlight
            if (FlashlightController.Instance != null)
            {
                if (FlashlightController.Instance.IsLightOn != data.isFlashlightOn)
                {
                    FlashlightController.Instance.ToggleLight();
                }
            }

            // 4. Restore Clearance Progression
            if (ClearanceManager.Instance != null)
            {
                ClearanceManager.Instance.RestoreSignatures(data.signatures, data.isSlipSubmitted);
                ClearanceManager.Instance.GuidanceTapeDelivered = data.guidanceTapeDelivered;
                ClearanceManager.Instance.RegistrarChasedAndCalmed = data.registrarChasedAndCalmed;
                ClearanceManager.Instance.CashierFeePaid = data.cashierFeePaid;
            }

            // 5. Restore Objective HUD
            if (ObjectiveHUD.Instance != null)
            {
                if (!string.IsNullOrEmpty(data.objectiveTitle))
                {
                    ObjectiveHUD.Instance.SetObjective(data.objectiveTitle, data.objectiveDetail);
                }
                else if (ClearanceManager.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjectiveForSignature(ClearanceManager.Instance.SignatureCount);
                }
            }

            // 6. Restore Inventory Hotbar
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.EnsureSlotsInitialized();
                var slots = InventoryManager.Instance.Slots;

                // Clear current slots
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i]?.Clear();
                }

                // Restore saved items
                if (data.inventorySlots != null)
                {
                    foreach (var sData in data.inventorySlots)
                    {
                        if (sData.slotIndex >= 0 && sData.slotIndex < slots.Count)
                        {
                            InventoryItem resolvedItem = ResolveInventoryItem(sData.assetName, sData.itemName);
                            if (resolvedItem != null)
                            {
                                slots[sData.slotIndex].item = resolvedItem;
                                slots[sData.slotIndex].quantity = Mathf.Max(1, sData.quantity);
                            }
                        }
                    }
                }

                InventoryManager.Instance.TriggerInventoryChanged();

                if (InventoryEquipController.Instance != null && data.selectedHotbarSlot >= 0)
                {
                    InventoryEquipController.Instance.SelectSlot(data.selectedHotbarSlot);
                }
            }

            // 7. Restore Puzzles
            if (GuidanceSafePuzzle.Instance != null)
            {
                GuidanceSafePuzzle.Instance.isSafeUnlocked = data.guidanceSafeUnlocked;
            }

            if (ExecutiveSafePuzzle.Instance != null)
            {
                ExecutiveSafePuzzle.Instance.isSafeUnlocked = data.executiveSafeUnlocked;
            }

            if (CircuitBreakerPuzzle.Instance != null && data.circuitPowerRestored)
            {
                CircuitBreakerPuzzle.Instance.RestorePowerInstant();
            }

            if (ComLabTerminalPuzzle.Instance != null)
            {
                ComLabTerminalPuzzle.Instance.RestoreTerminalState(data.comLabCableConnected, data.comLabHoldCleared);
            }

            if (LobbyKeypadInteract.Instance != null && data.lobbyKeypadUnlocked)
            {
                LobbyKeypadInteract.Instance.UnlockInstant();
            }

            if (CashierBalancePuzzle.Instance != null && data.cashierPuzzleSolved)
            {
                CashierBalancePuzzle.Instance.RestoreSolvedState();
            }

            if (RegistrarDocumentSortPuzzle.Instance != null && data.registrarPuzzleSolved)
            {
                RegistrarDocumentSortPuzzle.Instance.RestoreSolvedState();
            }

            if (GuidanceWordPuzzle.Instance != null && data.guidanceWordPuzzleSolved)
            {
                GuidanceWordPuzzle.Instance.RestoreSolvedState();
            }

            // 8. Remove Picked Up World Items
            _pickedUpWorldItems.Clear();
            if (data.pickedUpItemNames != null)
            {
                foreach (var name in data.pickedUpItemNames)
                {
                    _pickedUpWorldItems.Add(name);
                }

                var pickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
                foreach (var p in pickups)
                {
                    if (p != null && _pickedUpWorldItems.Contains(p.gameObject.name))
                    {
                        Destroy(p.gameObject);
                    }
                }
            }

            // 9. Unlock Saved Doors
            _unlockedDoors.Clear();
            if (data.unlockedDoorNames != null)
            {
                foreach (var name in data.unlockedDoorNames)
                {
                    _unlockedDoors.Add(name);
                }

                var doors = FindObjectsByType<DoorInteract>(FindObjectsInactive.Include);
                foreach (var d in doors)
                {
                    if (d != null && _unlockedDoors.Contains(d.gameObject.name))
                    {
                        d.isLocked = false;
                    }
                }
            }

            Debug.Log($"[SaveManager] Game loaded successfully from: {filePath}");

            // Audio & Toast Feedback
            if (_audioSource != null && loadAudio != null)
            {
                _audioSource.pitch = 0.95f;
                _audioSource.PlayOneShot(loadAudio, 0.85f);
            }

            ShowToast($"GAME LOADED ({slotName.ToUpper()})", isSuccess: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to load game: {ex}");
            ShowToast("LOAD FAILED!", isSuccess: false);
            return false;
        }
    }

    private InventoryItem ResolveInventoryItem(string assetName, string itemName)
    {
        if (_itemCache == null) InitializeItemCache();

        if (!string.IsNullOrEmpty(assetName) && _itemCache.TryGetValue(assetName, out var itemA))
            return itemA;

        if (!string.IsNullOrEmpty(itemName) && _itemCache.TryGetValue(itemName, out var itemB))
            return itemB;

        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PROCEDURAL TOAST NOTIFICATION UI
    // ═════════════════════════════════════════════════════════════════════════

    private void BuildToastUI()
    {
        if (_toastCanvasGO != null) Destroy(_toastCanvasGO);

        _toastCanvasGO = new GameObject("SaveManager_ToastCanvas");
        if (Application.isPlaying) DontDestroyOnLoad(_toastCanvasGO);

        var canvas = _toastCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2500;

        var scaler = _toastCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("ToastPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGO.transform.SetParent(_toastCanvasGO.transform, false);

        _toastPanelRT = panelGO.GetComponent<RectTransform>();
        _toastPanelRT.anchorMin = new Vector2(0.5f, 0.94f);
        _toastPanelRT.anchorMax = new Vector2(0.5f, 0.94f);
        _toastPanelRT.pivot = new Vector2(0.5f, 0.5f);
        _toastPanelRT.sizeDelta = new Vector2(420f, 48f);

        _toastCanvasGroup = panelGO.GetComponent<CanvasGroup>();
        _toastCanvasGroup.alpha = 0f;
        _toastCanvasGroup.blocksRaycasts = false;
        _toastCanvasGroup.interactable = false;

        var bgImg = panelGO.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);
        bgImg.raycastTarget = false;

        var outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.20f, 0.15f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Toast Text
        var txtGO = new GameObject("ToastText", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(panelGO.transform, false);

        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10f, 2f);
        txtRT.offsetMax = new Vector2(-10f, -2f);

        _toastText = txtGO.GetComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _toastText.font = font;
        _toastText.fontSize = 18;
        _toastText.fontStyle = FontStyle.Bold;
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.color = new Color(0.95f, 0.92f, 0.85f, 1f);
        _toastText.raycastTarget = false;
    }

    public void ShowToast(string message, bool isSuccess = true)
    {
        if (_toastText == null || _toastCanvasGroup == null) BuildToastUI();
        if (_toastText == null || _toastCanvasGroup == null) return;

        _toastText.text = isSuccess ? $"<color=#55FF88>●</color> {message}" : $"<color=#FF5555>▲</color> {message}";
        if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
        _toastCoroutine = StartCoroutine(ToastAnimationRoutine());
    }

    private IEnumerator ToastAnimationRoutine()
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < 0.20f)
        {
            elapsed += Time.unscaledDeltaTime;
            _toastCanvasGroup.alpha = Mathf.Clamp01(elapsed / 0.20f);
            yield return null;
        }
        _toastCanvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(1.8f);

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            _toastCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / 0.35f));
            yield return null;
        }
        _toastCanvasGroup.alpha = 0f;
        _toastCoroutine = null;
    }
}

// ═════════════════════════════════════════════════════════════════════════
// SAVE DATA SERIALIZATION CONTRACTS
// ═════════════════════════════════════════════════════════════════════════

[Serializable]
public class SavedSlotData
{
    public int slotIndex;
    public string assetName;
    public string itemName;
    public int quantity;
}

[Serializable]
public class SaveData
{
    public string saveVersion = "1.0";
    public string saveSlot = "QuickSave";
    public string timestamp = "";
    public float playTime = 0f;

    // Player State
    public float posX, posY, posZ;
    public float rotY;
    public float cameraPitch;
    public float anxiety;
    public float stamina;
    public bool isFlashlightOn;

    // Progression State
    public List<int> signatures = new List<int>();
    public bool isSlipSubmitted;
    public bool guidanceTapeDelivered;
    public bool registrarChasedAndCalmed;
    public bool cashierFeePaid;
    public string objectiveTitle = "";
    public string objectiveDetail = "";

    // Inventory State
    public List<SavedSlotData> inventorySlots = new List<SavedSlotData>();
    public int selectedHotbarSlot;

    // Puzzle & World State
    public bool guidanceSafeUnlocked;
    public bool executiveSafeUnlocked;
    public bool circuitFuseInstalled;
    public bool circuitPowerRestored;
    public bool comLabCableConnected;
    public bool comLabHoldCleared;
    public bool lobbyKeypadUnlocked;
    public bool cashierPuzzleSolved;
    public bool registrarPuzzleSolved;
    public bool guidanceWordPuzzleSolved;

    // World Pickups & Doors
    public List<string> pickedUpItemNames = new List<string>();
    public List<string> unlockedDoorNames = new List<string>();
}
