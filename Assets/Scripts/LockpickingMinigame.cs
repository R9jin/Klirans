using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LockpickingMinigame — Fully polished Skyrim/Fallout/Dying Light style 5-pin tumbler lockpicking minigame.
/// 
/// Visuals & Features:
/// - Realistic gunmetal steel lock housing cutaway with brass brand plaque, corner bolt studs, and machined borehole chambers.
/// - Machined stainless steel upper driver pins with lathe grooves and specular gleam.
/// - Polished golden brass lower key pins with beveled tips that ride the pick.
/// - Dynamic steel coil springs that physically compress/expand as pins are pushed upward.
/// - Luminous neon-emerald alignment shear line target zones.
/// - Status LEDs above each channel (Red when unset, Glowing Green when locked at shear line).
/// - Textured spring steel lockpick with rubber diamond knurling handle, chrome ferrule collar, and precision hook tip.
/// - L-shaped torque tension wrench inserted at the bottom keyway plug.
/// - Physical pick lift/tilt animation when pushing pins.
/// - Smooth cylinder plug rotation (90°) upon solving all 5 pins, accompanied by a heavy mechanical unbolt sound!
/// - Diegetic mechanical audio (pick slide clicks, spring push friction, latch lock clacks, unlatch thud).
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
        public float greenMin;      // e.g. 0.40
        public float greenMax;      // e.g. 0.70

        // Visual UI components
        public RectTransform containerRect;
        public Image springImage;
        public Image driverPinImage;
        public Image keyPinImage;
        public Image greenZoneImage;
        public Image ledIndicatorImage;
    }

    [Header("Minigame Settings")]
    public int totalPins = 5;
    public float difficultyTimer = 60f;
    public string difficultyName = "Standard Commercial Pin Tumbler";

    [Header("Item Reference")]
    public InventoryItem lockpinItemData;

    // Runtime state
    private DoorInteract targetDoor;
    private bool isMinigameActive = false;
    private bool isVictoryAnimating = false;
    public bool IsActive => isMinigameActive;
    public static bool IsMinigameActive => _instance != null && _instance.isMinigameActive;
    private int selectedPinIndex = 0;
    private float timeLeft = 60f;
    private List<PinData> pins = new List<PinData>();
    private Dictionary<DoorInteract, List<Vector2>> doorCombinations = new Dictionary<DoorInteract, List<Vector2>>();

    // UI Roots & Visual Transforms
    public GameObject minigameUIRoot;
    private Canvas createdCanvas;
    private RectTransform lockBlockTransform;
    private RectTransform pickVisualTransform;
    private RectTransform tensionWrenchTransform;
    private RectTransform cylinderPlugTransform;
    private Text timerText;
    private Text headerText;
    private Text statusBannerText;

    // Pick dynamic tilt
    private float _pickTiltAngle = 0f;

    // Audio
    private AudioSource _audioSource;
    private AudioClip _slideClickClip;
    private AudioClip _springPushClip;
    private AudioClip _latchLockClip;
    private AudioClip _pinDropClip;
    private AudioClip _unboltClip;

    // Loaded Texture Sprites
    private Sprite _lockHousingSprite;
    private Sprite _driverPinSprite;
    private Sprite _keyPinSprite;
    private Sprite _springSprite;
    private Sprite _lockpickSprite;
    private Sprite _tensionWrenchSprite;

    private int _openedFrame = -1;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupAudio();
        LoadSprites();
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

    private void SetupAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake = false;
        }

        // Generate or load crisp mechanical sound clips
        _slideClickClip = CreateSynthClick("PickSlideClick", 2800f, 1800f, 0.045f, 0.40f);
        _springPushClip = CreateSynthClick("SpringPushClip", 1200f, 650f, 0.065f, 0.55f);
        _latchLockClip = CreateSynthClick("LatchLockClip", 3400f, 2200f, 0.090f, 0.85f);
        _pinDropClip = CreateSynthClick("PinDropClip", 650f, 400f, 0.075f, 0.45f);

        // Load heavy unbolt audio if available
#if UNITY_EDITOR
        _unboltClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
#endif
        if (_unboltClip == null)
        {
            _unboltClip = Resources.Load<AudioClip>("Sounds/door_closing");
        }
        if (_unboltClip == null)
        {
            _unboltClip = CreateSynthClick("HeavyUnboltClunk", 450f, 180f, 0.25f, 1.0f);
        }
    }

    private AudioClip CreateSynthClick(string clipName, float freq1, float freq2, float duration, float volume)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * (1f / (duration * 0.35f))); // Exponential decay
            float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.65f + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.35f;
            data[i] = wave * env * volume;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void LoadSprites()
    {
        _lockHousingSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Housing_Cutaway.png");
        _driverPinSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Pin_Driver.png");
        _keyPinSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Pin_Key.png");
        _springSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Pin_Spring.png");
        _lockpickSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Pick_Tool.png");
        _tensionWrenchSprite = LoadSprite("Assets/Textures/PuzzleTextures/Lock_Tension_Wrench.png");
    }

    private Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        var texEd = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texEd != null)
        {
            return Sprite.Create(texEd, new Rect(0, 0, texEd.width, texEd.height), new Vector2(0.5f, 0.5f));
        }
#endif
        string fullPath = Path.Combine(Application.dataPath, "..", path).Replace('\\', '/');
        string targetPath = File.Exists(fullPath) ? fullPath : (File.Exists(path) ? path : null);
        if (targetPath != null)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(targetPath);
                Texture2D texRun = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texRun.LoadImage(bytes))
                {
                    texRun.name = Path.GetFileNameWithoutExtension(path);
                    return Sprite.Create(texRun, new Rect(0, 0, texRun.width, texRun.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LockpickingMinigame] Failed to load sprite from {targetPath}: {ex.Message}");
            }
        }
        return null;
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
        if (!isMinigameActive || isVictoryAnimating) return;

        // Prevent instant close on the same frame after pressing E to enter
        if (Time.frameCount <= _openedFrame + 2) return;

        // Timer update
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
        {
            timeLeft = 0;
            EndLockpicking(false, "Time expired.");
            return;
        }

        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(timeLeft).ToString() + "s";
        }

        // 1. Leave Lockpicking (ESC / Q / L)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.L))
        {
            EndLockpicking(false, "Lockpicking left.");
            return;
        }

        // 2. Move Pick Left / Right (A / D, Arrow Keys, or Mouse Scroll)
        float mouseScroll = Input.GetAxis("Mouse ScrollWheel");
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || mouseScroll > 0.05f)
        {
            int prev = selectedPinIndex;
            selectedPinIndex = Mathf.Clamp(selectedPinIndex - 1, 0, totalPins - 1);
            if (selectedPinIndex != prev && _audioSource != null && _slideClickClip != null)
            {
                _audioSource.PlayOneShot(_slideClickClip, 0.50f);
            }
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || mouseScroll < -0.05f)
        {
            int prev = selectedPinIndex;
            selectedPinIndex = Mathf.Clamp(selectedPinIndex + 1, 0, totalPins - 1);
            if (selectedPinIndex != prev && _audioSource != null && _slideClickClip != null)
            {
                _audioSource.PlayOneShot(_slideClickClip, 0.50f);
            }
        }

        // 3. Lift Pin (Left Click / W / Up Arrow) - hold or press
        if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            LiftActivePin();
        }

        // 4. Set Pin at Shear Line (Right Click / Space / S / Enter / E / Down Arrow)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            TryLockActivePin();
        }

        // Smoothly return pick tilt angle to 0
        _pickTiltAngle = Mathf.MoveTowards(_pickTiltAngle, 0f, Time.deltaTime * 65f);

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
            Debug.Log("[LockpickingMinigame] Cannot start lockpicking — no Lockpin in inventory!");
            return;
        }

        EnsureUIInitialized();

        targetDoor = door;
        isMinigameActive = true;
        isVictoryAnimating = false;
        _openedFrame = Time.frameCount;
        selectedPinIndex = 0;
        timeLeft = difficultyTimer;
        _pickTiltAngle = 0f;

        if (statusBannerText != null)
        {
            statusBannerText.text = "ALIGN ALL 5 TUMBLER PINS AT THE SHEAR LINE";
            statusBannerText.color = new Color(0.92f, 0.88f, 0.75f, 1f);
        }

        if (cylinderPlugTransform != null)
        {
            cylinderPlugTransform.localRotation = Quaternion.identity;
        }

        ResetPuzzle();
        SetPlayerMovementActive(false);

        if (minigameUIRoot != null)
        {
            minigameUIRoot.SetActive(true);
        }

        InitializeDoorCombination(door);
    }

    private void InitializeDoorCombination(DoorInteract door)
    {
        if (!doorCombinations.ContainsKey(door) || doorCombinations[door].Count != totalPins)
        {
            List<Vector2> combo = new List<Vector2>();
            for (int i = 0; i < totalPins; i++)
            {
                // Precision shear line target window (0.11 to 0.16 height)
                float size = UnityEngine.Random.Range(0.05f, 0.08f); // Precision: narrow green zone
                float min = UnityEngine.Random.Range(0.28f, 0.70f);
                combo.Add(new Vector2(min, Mathf.Clamp01(min + size)));
            }
            doorCombinations[door] = combo;
        }

        List<Vector2> targets = doorCombinations[door];
        for (int i = 0; i < pins.Count && i < targets.Count; i++)
        {
            pins[i].greenMin = targets[i].x;
            pins[i].greenMax = targets[i].y;
            pins[i].isLocked = false;
            pins[i].currentHeight = 0f;
            pins[i].velocity = 0f;
        }
    }

    private void LiftActivePin()
    {
        if (selectedPinIndex < 0 || selectedPinIndex >= pins.Count) return;
        PinData pin = pins[selectedPinIndex];
        if (pin.isLocked) return;

        // Controllable featherable pin lift against spring tension
        pin.velocity = Mathf.MoveTowards(pin.velocity, 0.9f, Time.deltaTime * 6.0f); // Precision: lower lift cap

        // Pick physical lifting tilt
        _pickTiltAngle = Mathf.MoveTowards(_pickTiltAngle, 14.0f, Time.deltaTime * 60f);

        // Check alignment with tight precision margin
        bool inShear = pin.currentHeight >= (pin.greenMin - 0.008f) && pin.currentHeight <= (pin.greenMax + 0.008f);
        if (inShear && statusBannerText != null)
        {
            statusBannerText.text = $"PIN {selectedPinIndex + 1} ALIGNED AT SHEAR LINE! PRESS [SPACE / E / RIGHT CLICK] TO SET";
            statusBannerText.color = new Color(0.25f, 1f, 0.5f, 1f);
        }
    }

    private void TryLockActivePin()
    {
        if (selectedPinIndex < 0 || selectedPinIndex >= pins.Count) return;
        PinData pin = pins[selectedPinIndex];
        if (pin.isLocked) return;

        // Subtle torque wrench flex on lock attempt
        if (tensionWrenchTransform != null)
        {
            tensionWrenchTransform.localRotation = Quaternion.Euler(0, 0, -5f);
        }

        // Check if current height is inside the green target zone (precise 0.008 margin — requires skill)
        if (pin.currentHeight >= (pin.greenMin - 0.008f) && pin.currentHeight <= (pin.greenMax + 0.008f))
        {
            // Successfully locked pin at shear line!
            pin.isLocked = true;
            pin.currentHeight = 0.92f; // Lock solidly at shear line
            pin.velocity = 0f;

            if (_audioSource != null && _latchLockClip != null)
            {
                _audioSource.PlayOneShot(_latchLockClip, 0.90f);
            }

            if (statusBannerText != null)
            {
                statusBannerText.text = $"PIN {selectedPinIndex + 1} SET INTO PLACE!";
                statusBannerText.color = new Color(0.40f, 1.0f, 0.40f, 1f);
            }

            // Automatically advance to the next unlocked pin
            for (int step = 1; step < totalPins; step++)
            {
                int next = (selectedPinIndex + step) % totalPins;
                if (!pins[next].isLocked)
                {
                    selectedPinIndex = next;
                    break;
                }
            }

            CheckVictoryCondition();
        }
        else
        {
            // Missed green zone -> pin bounces down and tension wrench slips!
            pin.velocity = -2.2f;
            if (_audioSource != null && _pinDropClip != null)
            {
                _audioSource.pitch = 0.85f;
                _audioSource.PlayOneShot(_pinDropClip, 0.90f);
            }

            // Anti-brute force: Slipped tension releases a previously set pin!
            bool droppedPrev = false;
            for (int i = pins.Count - 1; i >= 0; i--)
            {
                if (i != selectedPinIndex && pins[i].isLocked)
                {
                    pins[i].isLocked = false;
                    pins[i].velocity = -1.8f;
                    droppedPrev = true;
                    break;
                }
            }

            if (statusBannerText != null)
            {
                statusBannerText.text = droppedPrev
                    ? "TENSION SLIPPED! A PREVIOUS PIN DROPPED — FEATHER LIFT WITH PRECISION"
                    : "NOT ALIGNED AT SHEAR LINE — FEATHER [W] OR MOUSE TO HOVER IN GREEN ZONE";
                statusBannerText.color = new Color(1.0f, 0.30f, 0.30f, 1f);
            }
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
            StartCoroutine(RotatePlugAndUnlockRoutine());
        }
    }

    private IEnumerator RotatePlugAndUnlockRoutine()
    {
        isVictoryAnimating = true;

        if (statusBannerText != null)
        {
            statusBannerText.text = "<color=#44FF88>★ SHEAR LINE ALIGNED — CYLINDER UNLATCHED ★</color>";
        }

        if (_audioSource != null && _unboltClip != null)
        {
            _audioSource.PlayOneShot(_unboltClip, 0.95f);
        }

        // Smoothly rotate the cylinder plug and tension wrench by 90 degrees
        float elapsed = 0f;
        float duration = 0.55f;
        Quaternion startRot = (cylinderPlugTransform != null) ? cylinderPlugTransform.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(0, 0, -90f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (cylinderPlugTransform != null)
            {
                cylinderPlugTransform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            }
            if (tensionWrenchTransform != null)
            {
                tensionWrenchTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, -90f, t));
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.40f);

        EndLockpicking(true, "Lock Picked Successfully!");
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

    private void UpdatePinPhysics()
    {
        for (int i = 0; i < pins.Count; i++)
        {
            PinData pin = pins[i];
            if (pin.isLocked) continue;

            if (pin.currentHeight > 0f || pin.velocity != 0f)
            {
                pin.currentHeight += pin.velocity * Time.deltaTime;
                // Smooth spring gravity decay
                pin.velocity -= 5.0f * Time.deltaTime; // Stronger gravity — requires precision

                if (pin.currentHeight <= 0f)
                {
                    pin.currentHeight = 0f;
                    pin.velocity = 0f;
                }
                else if (pin.currentHeight >= 1.0f)
                {
                    pin.currentHeight = 1.0f;
                    pin.velocity = -0.4f; // Soft bounce at top limit
                }
            }
        }

        // Rebound tension wrench
        if (tensionWrenchTransform != null && !isVictoryAnimating)
        {
            tensionWrenchTransform.localRotation = Quaternion.RotateTowards(tensionWrenchTransform.localRotation, Quaternion.identity, Time.deltaTime * 30f);
        }
    }

    public void ForceClose()
    {
        if (!isMinigameActive && !isVictoryAnimating) return;
        StopAllCoroutines();
        EndLockpicking(false, "Force closed.");
    }

    private void EndLockpicking(bool success, string message)
    {
        isMinigameActive = false;
        isVictoryAnimating = false;

        if (minigameUIRoot != null)
        {
            minigameUIRoot.SetActive(false);
        }

        SetPlayerMovementActive(true);

        if (success && targetDoor != null)
        {
            targetDoor.UnlockDoor();
            Debug.Log("[LockpickingMinigame] Target door unlocked successfully!");
        }
        else if (!success && message == "Time expired.")
        {
            if (AnxietyManager.Instance != null) AnxietyManager.Instance.AddAnxiety(4f);
            var jsMgr = NPCJumpscareManager.Instance ?? Object.FindAnyObjectByType<NPCJumpscareManager>();
            if (jsMgr != null) jsMgr.PunishPlayerError("Lockpicking Time Expired", 0.45f);
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
        float channelSpacing = 82f;
        float startX = -164f;

        // 1. Move Hook Pick under selected pin channel with dynamic tilt
        if (pickVisualTransform != null && pins.Count > 0)
        {
            float targetX = startX + (selectedPinIndex * channelSpacing) + 20f;

            Vector2 pos = pickVisualTransform.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * 24f);
            pickVisualTransform.anchoredPosition = pos;

            // Physical hook lift tilt
            pickVisualTransform.localRotation = Quaternion.Euler(0, 0, _pickTiltAngle);
        }

        // 2. Render Pin Heights, Springs, Drivers, Key Pins, and LEDs
        for (int i = 0; i < pins.Count; i++)
        {
            PinData pin = pins[i];
            if (pin.containerRect == null) continue;

            float channelHeight = 180f;
            float pinY = pin.currentHeight * (channelHeight - 48f);

            // Lower Key Pin (Machined brass)
            if (pin.keyPinImage != null)
            {
                RectTransform keyRect = pin.keyPinImage.rectTransform;
                keyRect.anchoredPosition = new Vector2(0, pinY);

                if (pin.isLocked)
                {
                    pin.keyPinImage.color = new Color(0.40f, 1.0f, 0.40f, 1f); // Vibrant green when locked
                }
                else
                {
                    pin.keyPinImage.color = Color.white; // True textured brass
                }
            }

            // Upper Driver Pin (Stainless steel, sits on top of key pin)
            if (pin.driverPinImage != null)
            {
                RectTransform driverRect = pin.driverPinImage.rectTransform;
                driverRect.anchoredPosition = new Vector2(0, pinY + 36f);
            }

            // Top Spring (Compresses as driver pin moves up)
            if (pin.springImage != null)
            {
                RectTransform springRect = pin.springImage.rectTransform;
                float remainingSpace = Mathf.Max(20f, (channelHeight + 20f) - (pinY + 36f + 48f));
                springRect.anchoredPosition = new Vector2(0, channelHeight + 20f);
                springRect.sizeDelta = new Vector2(24, remainingSpace);
            }

            // Channel Status LED at top of channel
            if (pin.ledIndicatorImage != null)
            {
                pin.ledIndicatorImage.color = pin.isLocked
                    ? new Color(0.20f, 1.0f, 0.45f, 1f)  // Luminous Green
                    : new Color(0.85f, 0.22f, 0.22f, 0.90f); // Unset Red
            }

            // Green Target Zone height
            if (pin.greenZoneImage != null)
            {
                float gMinY = pin.greenMin * channelHeight;
                float gMaxY = pin.greenMax * channelHeight;
                float gHeight = Mathf.Max(16f, gMaxY - gMinY);

                RectTransform gRect = pin.greenZoneImage.rectTransform;
                gRect.anchoredPosition = new Vector2(0, gMinY);
                gRect.sizeDelta = new Vector2(32, gHeight);
            }
        }
    }

    /// <summary>
    /// Constructs UI layout matching high-detail mechanical cutaway specifications.
    /// </summary>
    private void CreateRuntimeUI()
    {
        GameObject canvasObj = new GameObject("LockpickingCanvas");
        createdCanvas = canvasObj.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        minigameUIRoot = new GameObject("MinigameRoot", typeof(RectTransform));
        minigameUIRoot.transform.SetParent(canvasObj.transform, false);

        RectTransform rootRect = minigameUIRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Dark Atmospheric Gunmetal Blur Backdrop
        GameObject bgObj = new GameObject("Background", typeof(Image));
        bgObj.transform.SetParent(minigameUIRoot.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Realistic Textured Lock Housing Cutaway Block
        GameObject lockBlockObj = new GameObject("LockHousingBlock", typeof(Image));
        lockBlockObj.transform.SetParent(minigameUIRoot.transform, false);
        Image blockImg = lockBlockObj.GetComponent<Image>();
        if (_lockHousingSprite != null)
        {
            blockImg.sprite = _lockHousingSprite;
            blockImg.color = Color.white;
        }
        else
        {
            blockImg.color = new Color(0.18f, 0.20f, 0.24f, 0.98f);
        }
        lockBlockTransform = lockBlockObj.GetComponent<RectTransform>();
        lockBlockTransform.anchoredPosition = new Vector2(0, 30);
        lockBlockTransform.sizeDelta = new Vector2(640, 460);

        // Header Title Text
        GameObject headerObj = new GameObject("HeaderText", typeof(Text));
        headerObj.transform.SetParent(lockBlockObj.transform, false);
        headerText = headerObj.GetComponent<Text>();
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 20;
        headerText.fontStyle = FontStyle.Bold;
        headerText.text = "PIN TUMBLER MECHANISM";
        headerText.color = new Color(0.92f, 0.88f, 0.78f, 1f);
        headerText.alignment = TextAnchor.MiddleLeft;
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(-280, 195);
        headerRect.sizeDelta = new Vector2(340, 36);

        // Timer Text at Top Right
        GameObject timerObj = new GameObject("TimerText", typeof(Text));
        timerObj.transform.SetParent(lockBlockObj.transform, false);
        timerText = timerObj.GetComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 24;
        timerText.fontStyle = FontStyle.Bold;
        timerText.text = "60s";
        timerText.color = new Color(0.95f, 0.88f, 0.65f, 1f);
        timerText.alignment = TextAnchor.MiddleRight;
        RectTransform timerRect = timerObj.GetComponent<RectTransform>();
        timerRect.anchoredPosition = new Vector2(280, 195);
        timerRect.sizeDelta = new Vector2(100, 36);

        // Rotating Cylinder Plug Chamber at Bottom
        GameObject plugObj = new GameObject("CylinderPlug", typeof(RectTransform));
        plugObj.transform.SetParent(lockBlockObj.transform, false);
        cylinderPlugTransform = plugObj.GetComponent<RectTransform>();
        cylinderPlugTransform.anchoredPosition = new Vector2(0, -115);
        cylinderPlugTransform.sizeDelta = new Vector2(520, 120);

        // 5 Vertical Pin Channels
        pins.Clear();
        float channelSpacing = 82f;
        float startX = -164f;

        for (int i = 0; i < totalPins; i++)
        {
            PinData pin = new PinData();

            GameObject channelObj = new GameObject($"PinChannel_{i}", typeof(RectTransform));
            channelObj.transform.SetParent(lockBlockObj.transform, false);
            RectTransform chanRect = channelObj.GetComponent<RectTransform>();
            chanRect.anchoredPosition = new Vector2(startX + (i * channelSpacing), 45);
            chanRect.sizeDelta = new Vector2(36, 210);

            // Channel Background Borehole (Dark recessed steel)
            GameObject chanBg = new GameObject("ChannelBg", typeof(Image));
            chanBg.transform.SetParent(channelObj.transform, false);
            Image chanBgImg = chanBg.GetComponent<Image>();
            chanBgImg.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
            RectTransform chanBgRect = chanBg.GetComponent<RectTransform>();
            chanBgRect.anchorMin = Vector2.zero;
            chanBgRect.anchorMax = Vector2.one;
            chanBgRect.sizeDelta = Vector2.zero;

            // Channel Status LED Indicator at Top
            GameObject ledObj = new GameObject("ChannelLED", typeof(Image));
            ledObj.transform.SetParent(channelObj.transform, false);
            Image ledImg = ledObj.GetComponent<Image>();
            ledImg.color = new Color(0.85f, 0.22f, 0.22f, 0.90f);
            pin.ledIndicatorImage = ledImg;
            RectTransform ledRect = ledObj.GetComponent<RectTransform>();
            ledRect.anchorMin = new Vector2(0.5f, 1f);
            ledRect.anchorMax = new Vector2(0.5f, 1f);
            ledRect.pivot = new Vector2(0.5f, 1f);
            ledRect.anchoredPosition = new Vector2(0, 18);
            ledRect.sizeDelta = new Vector2(10, 10);

            // Top Spring (Expands/Compresses)
            GameObject springObj = new GameObject("PinSpring", typeof(Image));
            springObj.transform.SetParent(channelObj.transform, false);
            Image springImg = springObj.GetComponent<Image>();
            if (_springSprite != null)
            {
                springImg.sprite = _springSprite;
                springImg.color = Color.white;
            }
            else
            {
                springImg.color = new Color(0.70f, 0.75f, 0.80f, 0.85f);
            }
            pin.springImage = springImg;
            RectTransform springRect = springObj.GetComponent<RectTransform>();
            springRect.anchorMin = new Vector2(0.5f, 0f);
            springRect.anchorMax = new Vector2(0.5f, 0f);
            springRect.pivot = new Vector2(0.5f, 1f);

            // Luminous Green Target Zone (Shear line slot)
            GameObject greenZoneObj = new GameObject("GreenZone", typeof(Image));
            greenZoneObj.transform.SetParent(channelObj.transform, false);
            Image gImg = greenZoneObj.GetComponent<Image>();
            gImg.color = new Color(0.18f, 0.88f, 0.42f, 0.70f);
            pin.greenZoneImage = gImg;
            RectTransform gRect = greenZoneObj.GetComponent<RectTransform>();
            gRect.anchorMin = new Vector2(0.5f, 0f);
            gRect.anchorMax = new Vector2(0.5f, 0f);
            gRect.pivot = new Vector2(0.5f, 0f);

            // Upper Driver Pin (Stainless steel)
            GameObject driverObj = new GameObject("DriverPin", typeof(Image));
            driverObj.transform.SetParent(channelObj.transform, false);
            Image driverImg = driverObj.GetComponent<Image>();
            if (_driverPinSprite != null)
            {
                driverImg.sprite = _driverPinSprite;
                driverImg.color = Color.white;
            }
            else
            {
                driverImg.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            }
            pin.driverPinImage = driverImg;
            RectTransform driverRect = driverObj.GetComponent<RectTransform>();
            driverRect.anchorMin = new Vector2(0.5f, 0f);
            driverRect.anchorMax = new Vector2(0.5f, 0f);
            driverRect.pivot = new Vector2(0.5f, 0f);
            driverRect.sizeDelta = new Vector2(30, 46);

            // Lower Key Pin (Polished solid brass with conical tip)
            GameObject keyPinObj = new GameObject("KeyPin", typeof(Image));
            keyPinObj.transform.SetParent(channelObj.transform, false);
            Image keyImg = keyPinObj.GetComponent<Image>();
            if (_keyPinSprite != null)
            {
                keyImg.sprite = _keyPinSprite;
                keyImg.color = Color.white;
            }
            else
            {
                keyImg.color = new Color(0.85f, 0.68f, 0.25f, 1f);
            }
            pin.keyPinImage = keyImg;
            RectTransform keyRect = keyPinObj.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0.5f, 0f);
            keyRect.anchorMax = new Vector2(0.5f, 0f);
            keyRect.pivot = new Vector2(0.5f, 0f);
            keyRect.sizeDelta = new Vector2(30, 46);

            pin.containerRect = chanRect;
            pins.Add(pin);
        }

        // Tension Wrench at bottom keyway plug
        GameObject wrenchObj = new GameObject("TensionWrench", typeof(Image));
        wrenchObj.transform.SetParent(cylinderPlugTransform, false);
        Image wrenchImg = wrenchObj.GetComponent<Image>();
        if (_tensionWrenchSprite != null)
        {
            wrenchImg.sprite = _tensionWrenchSprite;
            wrenchImg.color = Color.white;
        }
        else
        {
            wrenchImg.color = new Color(0.75f, 0.80f, 0.88f, 1f);
        }
        tensionWrenchTransform = wrenchObj.GetComponent<RectTransform>();
        tensionWrenchTransform.anchoredPosition = new Vector2(40, -15);
        tensionWrenchTransform.sizeDelta = new Vector2(240, 75);

        // Modeled Textured Spring Steel Lockpick Tool
        GameObject pickRoot = new GameObject("LockpickVisual", typeof(Image));
        pickRoot.transform.SetParent(lockBlockObj.transform, false);
        Image pickImg = pickRoot.GetComponent<Image>();
        if (_lockpickSprite != null)
        {
            pickImg.sprite = _lockpickSprite;
            pickImg.color = Color.white;
        }
        else
        {
            pickImg.color = new Color(0.80f, 0.82f, 0.88f, 1f);
        }
        pickVisualTransform = pickRoot.GetComponent<RectTransform>();
        pickVisualTransform.pivot = new Vector2(0.95f, 0.5f); // Pivot at the tip
        pickVisualTransform.anchoredPosition = new Vector2(-164f, -75f);
        pickVisualTransform.sizeDelta = new Vector2(330, 52);

        // Status Banner Text across the lock
        GameObject statusObj = new GameObject("StatusBanner", typeof(Text));
        statusObj.transform.SetParent(lockBlockObj.transform, false);
        statusBannerText = statusObj.GetComponent<Text>();
        statusBannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusBannerText.fontSize = 15;
        statusBannerText.fontStyle = FontStyle.Bold;
        statusBannerText.text = "ALIGN ALL 5 TUMBLER PINS AT THE SHEAR LINE";
        statusBannerText.color = new Color(0.92f, 0.88f, 0.75f, 1f);
        statusBannerText.alignment = TextAnchor.MiddleCenter;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, -170);
        statusRect.sizeDelta = new Vector2(580, 32);

        // Metallic Control Hint Bar at Screen Bottom
        GameObject hintBarObj = new GameObject("ControlHintBar", typeof(Image));
        hintBarObj.transform.SetParent(minigameUIRoot.transform, false);
        Image hintBg = hintBarObj.GetComponent<Image>();
        hintBg.color = new Color(0.10f, 0.12f, 0.15f, 0.95f);
        RectTransform hintBarRect = hintBarObj.GetComponent<RectTransform>();
        hintBarRect.anchoredPosition = new Vector2(0, -240);
        hintBarRect.sizeDelta = new Vector2(720, 42);

        GameObject hintTextObj = new GameObject("HintText", typeof(Text));
        hintTextObj.transform.SetParent(hintBarObj.transform, false);
        Text hintText = hintTextObj.GetComponent<Text>();
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 15;
        hintText.text = "[A / D / Scroll] Select Pin   •   [W / Left Click] Lift Pin   •   [Space / Right Click / E] Set Pin   •   [ESC] Exit";
        hintText.color = new Color(0.92f, 0.90f, 0.84f, 1f);
        hintText.alignment = TextAnchor.MiddleCenter;
        RectTransform hintTextRect = hintTextObj.GetComponent<RectTransform>();
        hintTextRect.anchorMin = Vector2.zero;
        hintTextRect.anchorMax = Vector2.one;
        hintTextRect.sizeDelta = Vector2.zero;
    }
}
