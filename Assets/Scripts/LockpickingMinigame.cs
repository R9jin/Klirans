using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lockpicking Minigame Manager
/// 1:1 implementation based on the reference screenshots:
/// - 5 vertical pin tumbler channels with green target zones & red fail zones.
/// - Modeled hook pick with handle, long shaft, and angled tip.
/// - Non-breakable pick (unlimited attempts).
/// - Controls: A/D (Move), W / RClick (Hit Pin), S / LClick (Lock Pin), R (Reset), N (New Combo), L / ESC (Leave).
/// </summary>
public class LockpickingMinigame : MonoBehaviour
{
    private static LockpickingMinigame _instance;
    public static LockpickingMinigame Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<LockpickingMinigame>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LockpickingMinigame");
                    _instance = go.AddComponent<LockpickingMinigame>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class PinData
    {
        public bool isLocked;
        public float currentHeight; // 0.0 to 1.0
        public float velocity;
        public float greenMin; // e.g. 0.45
        public float greenMax; // e.g. 0.75

        // UI elements for this pin
        public RectTransform containerRect;
        public Image pinCapImage;
        public Image greenSegmentImage;
        public Image redTopSegmentImage;
        public Image redBottomSegmentImage;
    }

    [Header("Minigame Settings")]
    public int totalPins = 5;
    public float difficultyTimer = 60f;
    public string difficultyName = "Easy";

    [Header("Item Reference")]
    public InventoryItem lockpinItemData;

    // Internal state
    private DoorInteract targetDoor;
    private bool isMinigameActive = false;
    private int selectedPinIndex = 0;
    private float timeLeft = 60f;
    private List<PinData> pins = new List<PinData>();
    private Dictionary<DoorInteract, List<Vector2>> doorCombinations = new Dictionary<DoorInteract, List<Vector2>>();

    // UI Root & Components
    public GameObject minigameUIRoot;
    private Canvas createdCanvas;
    private RectTransform pickVisualTransform;
    private Text timerText;
    private Text headerText;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureUIInitialized();
    }

    private void Start()
    {
        EnsureUIInitialized();
        if (minigameUIRoot != null)
        {
            minigameUIRoot.SetActive(false);
        }
    }

    private void EnsureUIInitialized()
    {
        if (minigameUIRoot == null)
        {
            CreateRuntimeUI();
            if (minigameUIRoot != null)
            {
                minigameUIRoot.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!isMinigameActive) return;

        // Timer update
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
        {
            timeLeft = 0;
            EndLockpicking(false, "Time ran out.");
            return;
        }

        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(timeLeft).ToString() + "s";
        }

        // 1. Leave Lockpicking (L / ESC)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.L))
        {
            EndLockpicking(false, "Lockpicking left.");
            return;
        }

        // 2. Move Pick Left / Right (A / D or Left / Right Arrows)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            selectedPinIndex = Mathf.Clamp(selectedPinIndex - 1, 0, totalPins - 1);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            selectedPinIndex = Mathf.Clamp(selectedPinIndex + 1, 0, totalPins - 1);
        }

        // 3. Hit Pin (Right Click / W)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.W))
        {
            HitActivePin();
        }

        // 4. Lock Pin (Left Click / S)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.S))
        {
            TryLockActivePin();
        }

        // Update Pin Animations & Physics
        UpdatePinPhysics();

        // Render UI
        UpdateUIRender();
    }

    public void StartLockpicking(DoorInteract door)
    {
        if (door == null || !door.isLocked) return;

        if (!HasLockpin(door.requiredLockpinItem))
        {
            Debug.Log("[LockpickingMinigame] Cannot start lockpicking - no Lockpin in inventory!");
            return;
        }

        EnsureUIInitialized();

        targetDoor = door;
        isMinigameActive = true;
        selectedPinIndex = 0;
        timeLeft = difficultyTimer;

        // Pause Player Movement
        SetPlayerMovementActive(false);

        if (minigameUIRoot != null)
        {
            minigameUIRoot.SetActive(true);
        }

        InitializeDoorCombination(door);
    }

    private void InitializeDoorCombination(DoorInteract door)
    {
        // Generate or retrieve persistent lock combination for this door
        if (!doorCombinations.ContainsKey(door) || doorCombinations[door].Count != totalPins)
        {
            List<Vector2> combo = new List<Vector2>();
            for (int i = 0; i < totalPins; i++)
            {
                float size = Random.Range(0.25f, 0.35f);
                float min = Random.Range(0.25f, 0.6f);
                combo.Add(new Vector2(min, Mathf.Clamp01(min + size)));
            }
            doorCombinations[door] = combo;
        }

        List<Vector2> targets = doorCombinations[door];
        for (int i = 0; i < pins.Count && i < targets.Count; i++)
        {
            pins[i].greenMin = targets[i].x;
            pins[i].greenMax = targets[i].y;
        }
    }

    private void HitActivePin()
    {
        if (selectedPinIndex < 0 || selectedPinIndex >= pins.Count) return;
        PinData pin = pins[selectedPinIndex];
        if (pin.isLocked) return;

        // Push pin upward with velocity
        pin.velocity = 2.2f;
    }

    private void TryLockActivePin()
    {
        if (selectedPinIndex < 0 || selectedPinIndex >= pins.Count) return;
        PinData pin = pins[selectedPinIndex];
        if (pin.isLocked) return;

        // Check if current height is inside the green target zone
        if (pin.currentHeight >= pin.greenMin && pin.currentHeight <= pin.greenMax)
        {
            // Successfully locked pin!
            pin.isLocked = true;
            pin.currentHeight = 0.9f; // Lock at top
            pin.velocity = 0f;

            // Check if all pins are locked
            CheckVictoryCondition();
        }
        else
        {
            // Missed green zone -> pin drops down
            pin.velocity = -1.5f;
        }
    }

    private void CheckVictoryCondition()
    {
        bool allLocked = true;
        foreach (var p in pins)
        {
            if (!p.isLocked)
            {
                allLocked = false;
                break;
            }
        }

        if (allLocked)
        {
            EndLockpicking(true, "Lock Picked Successfully!");
        }
    }

    private void ResetPuzzle()
    {
        foreach (var p in pins)
        {
            p.isLocked = false;
            p.currentHeight = 0f;
            p.velocity = 0f;
        }
        selectedPinIndex = 0;
    }

    private void GenerateNewCombination()
    {
        ResetPuzzle();
        for (int i = 0; i < pins.Count; i++)
        {
            float size = Random.Range(0.25f, 0.35f);
            float min = Random.Range(0.25f, 0.6f);
            pins[i].greenMin = min;
            pins[i].greenMax = Mathf.Clamp01(min + size);
        }
    }

    private void UpdatePinPhysics()
    {
        for (int i = 0; i < pins.Count; i++)
        {
            PinData pin = pins[i];
            if (pin.isLocked) continue;

            // Apply gravity & velocity
            if (pin.currentHeight > 0f || pin.velocity > 0f)
            {
                pin.currentHeight += pin.velocity * Time.deltaTime;
                pin.velocity -= 4.0f * Time.deltaTime; // Gravity

                if (pin.currentHeight <= 0f)
                {
                    pin.currentHeight = 0f;
                    pin.velocity = 0f;
                }
                else if (pin.currentHeight >= 1.0f)
                {
                    pin.currentHeight = 1.0f;
                    pin.velocity = -0.5f; // Bounce at top
                }
            }
        }
    }

    private void EndLockpicking(bool success, string message)
    {
        isMinigameActive = false;

        if (minigameUIRoot != null)
        {
            minigameUIRoot.SetActive(false);
        }

        SetPlayerMovementActive(true);

        if (success && targetDoor != null)
        {
            targetDoor.UnlockDoor();
            Debug.Log("[LockpickingMinigame] Door unlocked!");
        }

        targetDoor = null;
    }

    private void SetPlayerMovementActive(bool active)
    {
        PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.SetControlsEnabled(active);
            if (!active && pm.footstepAudioSource != null && pm.footstepAudioSource.isPlaying)
            {
                pm.footstepAudioSource.Pause();
            }
            pm.enabled = active;
        }

        Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !active;
    }

    public static bool HasLockpin(InventoryItem requiredItem = null)
    {
        return GetLockpinCount(requiredItem) > 0;
    }

    public static int GetLockpinCount(InventoryItem requiredItem = null)
    {
        InventoryManager inventory = InventoryManager.Instance ?? Object.FindAnyObjectByType<InventoryManager>();
        if (inventory == null) return 0;

        inventory.EnsureSlotsInitialized();
        int count = 0;
        foreach (var slot in inventory.Slots)
        {
            if (!slot.IsEmpty && slot.item != null)
            {
                if (requiredItem != null && slot.item == requiredItem)
                {
                    count += slot.quantity;
                }
                else if (slot.item.itemName.ToLower().Contains("lockpin") || slot.item.itemName.ToLower().Contains("lock pin") || slot.item.itemName.ToLower().Contains("lockpick"))
                {
                    count += slot.quantity;
                }
            }
        }
        return count;
    }

    private void UpdateUIRender()
    {
        // 1. Move Hook Pick under selected pin channel
        if (pickVisualTransform != null && pins.Count > 0)
        {
            float channelSpacing = 55f;
            float startX = -110f;
            float targetX = startX + (selectedPinIndex * channelSpacing);

            Vector2 pos = pickVisualTransform.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * 25f);
            pickVisualTransform.anchoredPosition = pos;
        }

        // 2. Render Pin Positions & Green/Red Target Zones
        for (int i = 0; i < pins.Count; i++)
        {
            PinData pin = pins[i];
            if (pin.containerRect == null) continue;

            float channelHeight = 160f;
            float pinY = pin.currentHeight * (channelHeight - 40f);

            // Update pin cap position
            if (pin.pinCapImage != null)
            {
                RectTransform capRect = pin.pinCapImage.rectTransform;
                capRect.anchoredPosition = new Vector2(0, pinY);

                if (pin.isLocked)
                {
                    pin.pinCapImage.color = new Color(0.2f, 0.9f, 0.2f, 1f); // Green when locked
                }
                else
                {
                    pin.pinCapImage.color = new Color(0.7f, 0.55f, 0.15f, 1f); // Brass/Gold
                }
            }

            // Update Green & Red segment heights
            if (pin.greenSegmentImage != null && pin.redBottomSegmentImage != null && pin.redTopSegmentImage != null)
            {
                float gMinY = pin.greenMin * channelHeight;
                float gMaxY = pin.greenMax * channelHeight;
                float gHeight = gMaxY - gMinY;

                // Green zone
                RectTransform gRect = pin.greenSegmentImage.rectTransform;
                gRect.anchoredPosition = new Vector2(0, gMinY);
                gRect.sizeDelta = new Vector2(26, gHeight);

                // Red bottom zone
                RectTransform rbRect = pin.redBottomSegmentImage.rectTransform;
                rbRect.anchoredPosition = new Vector2(0, 0);
                rbRect.sizeDelta = new Vector2(26, gMinY);

                // Red top zone
                RectTransform rtRect = pin.redTopSegmentImage.rectTransform;
                rtRect.anchoredPosition = new Vector2(0, gMaxY);
                rtRect.sizeDelta = new Vector2(26, channelHeight - gMaxY);
            }
        }
    }

    /// <summary>
    /// Constructs UI layout matching screenshots (5 Pin Channels, Left Controls Box, Lockpick with Handle & Hook).
    /// </summary>
    private void CreateRuntimeUI()
    {
        GameObject canvasObj = new GameObject("LockpickingCanvas");
        createdCanvas = canvasObj.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        minigameUIRoot = new GameObject("MinigameRoot", typeof(RectTransform));
        minigameUIRoot.transform.SetParent(canvasObj.transform, false);

        RectTransform rootRect = minigameUIRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Dark Background Overlay
        GameObject bgObj = new GameObject("Background", typeof(Image));
        bgObj.transform.SetParent(minigameUIRoot.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Centered Lock Housing Block
        GameObject lockBlockObj = new GameObject("LockHousingBlock", typeof(Image));
        lockBlockObj.transform.SetParent(minigameUIRoot.transform, false);
        Image blockImg = lockBlockObj.GetComponent<Image>();
        blockImg.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        RectTransform blockRect = lockBlockObj.GetComponent<RectTransform>();
        blockRect.anchoredPosition = new Vector2(0, 20);
        blockRect.sizeDelta = new Vector2(420, 320);

        // Subtle Outer Border Frame
        GameObject borderObj = new GameObject("BorderFrame", typeof(Image));
        borderObj.transform.SetParent(lockBlockObj.transform, false);
        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.color = new Color(0.4f, 0.4f, 0.45f, 0.4f);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(6, 6);
        borderObj.transform.SetAsFirstSibling();

        // Header Title ("LOCKPICKING")
        GameObject headerObj = new GameObject("HeaderText", typeof(Text));
        headerObj.transform.SetParent(lockBlockObj.transform, false);
        headerText = headerObj.GetComponent<Text>();
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 24;
        headerText.fontStyle = FontStyle.Bold;
        headerText.text = "LOCKPICKING";
        headerText.color = new Color(0.95f, 0.85f, 0.35f, 1f);
        headerText.alignment = TextAnchor.MiddleLeft;
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(-180, 130);
        headerRect.sizeDelta = new Vector2(200, 40);

        // Timer Text at Top Right
        GameObject timerObj = new GameObject("TimerText", typeof(Text));
        timerObj.transform.SetParent(lockBlockObj.transform, false);
        timerText = timerObj.GetComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 24;
        timerText.fontStyle = FontStyle.Bold;
        timerText.text = "60s";
        timerText.color = new Color(0.95f, 0.85f, 0.35f, 1f);
        timerText.alignment = TextAnchor.MiddleRight;
        RectTransform timerRect = timerObj.GetComponent<RectTransform>();
        timerRect.anchoredPosition = new Vector2(180, 130);
        timerRect.sizeDelta = new Vector2(100, 40);

        // Cylinder Cutout Cavity
        GameObject cylHole = new GameObject("CylinderHole", typeof(Image));
        cylHole.transform.SetParent(lockBlockObj.transform, false);
        Image holeImg = cylHole.GetComponent<Image>();
        holeImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        RectTransform holeRect = cylHole.GetComponent<RectTransform>();
        holeRect.anchoredPosition = new Vector2(0, -60);
        holeRect.sizeDelta = new Vector2(390, 150);

        // 5 Vertical Pin Channels
        pins.Clear();
        float channelSpacing = 65f;
        float startX = -130f;

        for (int i = 0; i < totalPins; i++)
        {
            PinData pin = new PinData();

            GameObject channelObj = new GameObject($"PinChannel_{i}", typeof(RectTransform));
            channelObj.transform.SetParent(lockBlockObj.transform, false);
            channelObj.AddComponent<RectMask2D>(); // Clip pin segments cleanly inside channel
            RectTransform chanRect = channelObj.GetComponent<RectTransform>();
            chanRect.anchoredPosition = new Vector2(startX + (i * channelSpacing), 35);
            chanRect.sizeDelta = new Vector2(34, 160);

            // Channel Dark Background Cutout
            GameObject chanBg = new GameObject("ChannelBg", typeof(Image));
            chanBg.transform.SetParent(channelObj.transform, false);
            Image chanBgImg = chanBg.GetComponent<Image>();
            chanBgImg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            RectTransform chanBgRect = chanBg.GetComponent<RectTransform>();
            chanBgRect.anchorMin = Vector2.zero;
            chanBgRect.anchorMax = Vector2.one;
            chanBgRect.sizeDelta = Vector2.zero;

            // Red Bottom Segment
            GameObject redBot = new GameObject("RedBottom", typeof(Image));
            redBot.transform.SetParent(channelObj.transform, false);
            Image rbImg = redBot.GetComponent<Image>();
            rbImg.color = new Color(0.85f, 0.22f, 0.2f, 1f);
            pin.redBottomSegmentImage = rbImg;

            // Green Middle Target Segment
            GameObject greenMid = new GameObject("GreenMiddle", typeof(Image));
            greenMid.transform.SetParent(channelObj.transform, false);
            Image gImg = greenMid.GetComponent<Image>();
            gImg.color = new Color(0.2f, 0.88f, 0.3f, 1f);
            pin.greenSegmentImage = gImg;

            // Red Top Segment
            GameObject redTop = new GameObject("RedTop", typeof(Image));
            redTop.transform.SetParent(channelObj.transform, false);
            Image rtImg = redTop.GetComponent<Image>();
            rtImg.color = new Color(0.85f, 0.22f, 0.2f, 1f);
            pin.redTopSegmentImage = rtImg;

            // Pin Cap (Brass bottom cylinder cap)
            GameObject pinCap = new GameObject("PinCap", typeof(Image));
            pinCap.transform.SetParent(channelObj.transform, false);
            Image capImg = pinCap.GetComponent<Image>();
            capImg.color = new Color(0.78f, 0.62f, 0.18f, 1f);
            pin.pinCapImage = capImg;
            RectTransform capRect = pinCap.GetComponent<RectTransform>();
            capRect.sizeDelta = new Vector2(34, 24);

            pin.containerRect = chanRect;
            pins.Add(pin);
        }

        // Modeled Lockpick (Handle + Shaft + Angled Hook Tip)
        GameObject pickRoot = new GameObject("LockpickVisual", typeof(RectTransform));
        pickRoot.transform.SetParent(lockBlockObj.transform, false);
        pickVisualTransform = pickRoot.GetComponent<RectTransform>();
        pickVisualTransform.anchoredPosition = new Vector2(-130f, -58f);
        pickVisualTransform.sizeDelta = new Vector2(300, 40);

        // Pick Shaft (Thin silver metal rod extending to the left from x=0)
        GameObject shaftObj = new GameObject("Shaft", typeof(Image));
        shaftObj.transform.SetParent(pickRoot.transform, false);
        Image shaftImg = shaftObj.GetComponent<Image>();
        shaftImg.color = new Color(0.88f, 0.88f, 0.86f, 1f);
        RectTransform shaftRect = shaftObj.GetComponent<RectTransform>();
        shaftRect.anchoredPosition = new Vector2(-95, 0);
        shaftRect.sizeDelta = new Vector2(190, 8);

        // Pick Handle (Dark grip on far left)
        GameObject handleObj = new GameObject("Handle", typeof(Image));
        handleObj.transform.SetParent(pickRoot.transform, false);
        Image handleImg = handleObj.GetComponent<Image>();
        handleImg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchoredPosition = new Vector2(-270, 0);
        handleRect.sizeDelta = new Vector2(160, 32);

        // Pick Hook Tip (Angled hook tip aligned at x=0 under active pin)
        GameObject hookObj = new GameObject("HookTip", typeof(Image));
        hookObj.transform.SetParent(pickRoot.transform, false);
        Image hookImg = hookObj.GetComponent<Image>();
        hookImg.color = new Color(0.88f, 0.88f, 0.86f, 1f);
        RectTransform hookRect = hookObj.GetComponent<RectTransform>();
        hookRect.anchoredPosition = new Vector2(0, 10);
        hookRect.sizeDelta = new Vector2(12, 20);
        hookRect.localRotation = Quaternion.Euler(0, 0, -25f);

        // Sleek Single-Line Control Hint Bar at Bottom Center
        GameObject hintBarObj = new GameObject("ControlHintBar", typeof(Image));
        hintBarObj.transform.SetParent(minigameUIRoot.transform, false);
        Image hintBg = hintBarObj.GetComponent<Image>();
        hintBg.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
        RectTransform hintBarRect = hintBarObj.GetComponent<RectTransform>();
        hintBarRect.anchoredPosition = new Vector2(0, -185);
        hintBarRect.sizeDelta = new Vector2(620, 38);

        GameObject hintTextObj = new GameObject("HintText", typeof(Text));
        hintTextObj.transform.SetParent(hintBarObj.transform, false);
        Text hintText = hintTextObj.GetComponent<Text>();
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 15;
        hintText.text = "[A / D] Move Pick   •   [W / RClick] Push Pin   •   [S / LClick] Lock Pin   •   [ESC] Exit";
        hintText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        hintText.alignment = TextAnchor.MiddleCenter;
        RectTransform hintTextRect = hintTextObj.GetComponent<RectTransform>();
        hintTextRect.anchorMin = Vector2.zero;
        hintTextRect.anchorMax = Vector2.one;
        hintTextRect.sizeDelta = Vector2.zero;
    }
}
