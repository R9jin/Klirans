using UnityEngine;

/// <summary>
/// Flashlight Controller
/// Managed via 6-slot Inventory Hotbar Equipment (Keys 1-6).
/// When Flashlight is equipped in hand:
///   - Displays single HeldFlashlight model (no duplicates)
///   - Left Click (Mouse0) toggles the flashlight spotlight ON and OFF.
/// Unequipping stows the flashlight model and turns off the light.
/// </summary>
public class FlashlightController : MonoBehaviour
{
    public static FlashlightController Instance { get; private set; }

    [Tooltip("The InventoryItem that represents the flashlight.")]
    public InventoryItem flashlightItemData;

    [Tooltip("The Unity Light component to toggle (e.g., Spotlight).")]
    public Light flashlightLight;

    [Header("Peripheral Spill Light (Dual Beam)")]
    [Tooltip("Secondary wide-angle flood light that illuminates immediate footing without blinding.")]
    public Light flashlightSpillLight;
    public bool enableSpillLight = true;
    public float spillIntensity = 0.65f;
    public float spillRange = 8.5f;
    public float spillSpotAngle = 110.0f;

    [Header("High-Power Flashlight Settings")]
    [Tooltip("Main spotlight intensity for high-contrast survival horror navigation.")]
    public float powerfulIntensity = 4.8f;

    [Tooltip("Main spotlight throw range (metres).")]
    public float powerfulRange = 38.0f;

    [Tooltip("Outer spot cone angle.")]
    public float outerSpotAngle = 68.0f;

    [Tooltip("Inner spot cone angle for the bright core beam.")]
    public float innerSpotAngle = 30.0f;

    [Tooltip("Clean, modern high-CRI flashlight beam color.")]
    public Color beamColor = new Color(0.96f, 0.98f, 1.0f, 1.0f);

    [Tooltip("Enable soft shadow casting for realistic stalking & ambient horror.")]
    public bool enableSoftShadows = true;

    [Header("Held Item View")]
    [Tooltip("The 3D flashlight model shown in view when equipped (child of Main Camera).")]
    public GameObject heldFlashlightModel;

    [Header("Audio (Optional)")]
    public AudioClip clickSound;

    private bool isEquipped = false;
    private bool isLightOn = false;
    private AudioSource audioSource;

    public bool IsEquipped => isEquipped;
    public bool IsLightOn => isLightOn && flashlightLight != null && flashlightLight.enabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.0f; // 2D crisp click in player's ear
        }

        if (clickSound == null)
        {
            clickSound = CreateTactileClickClip();
        }
    }

    private void Start()
    {
        AutoAssignReferences();
        ConfigureFlashlightLights();
        StripRogueComponents();
        SetEquippedState(false);
    }

    private void StripRogueComponents()
    {
        if (heldFlashlightModel != null)
        {
            foreach (var p in heldFlashlightModel.GetComponentsInChildren<PickupItem>(true))
            {
                if (p != null) Destroy(p);
            }
            foreach (var col in heldFlashlightModel.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) Destroy(col);
            }
            foreach (var hl in heldFlashlightModel.GetComponentsInChildren<ItemVisualHighlight>(true))
            {
                if (hl != null)
                {
                    hl.OnPickedUp();
                    Destroy(hl);
                }
            }
            foreach (var l in heldFlashlightModel.GetComponentsInChildren<Light>(true))
            {
                if (l != null && l != flashlightLight && l != flashlightSpillLight)
                {
                    l.enabled = false;
                    Destroy(l.gameObject);
                }
            }
            Transform glintL = heldFlashlightModel.transform.Find("ItemGlintLight");
            if (glintL != null) Destroy(glintL.gameObject);
            Transform glintS = heldFlashlightModel.transform.Find("ItemGlintSparkle");
            if (glintS != null) Destroy(glintS.gameObject);
        }

        // Also strip any rogue ItemGlintLight / ItemVisualHighlight anywhere under the player/camera hierarchy
        Camera mainCam = Camera.main ?? GetComponentInChildren<Camera>();
        if (mainCam != null)
        {
            foreach (var hl in mainCam.GetComponentsInChildren<ItemVisualHighlight>(true))
            {
                if (hl != null)
                {
                    hl.OnPickedUp();
                    Destroy(hl);
                }
            }
            foreach (var l in mainCam.GetComponentsInChildren<Light>(true))
            {
                if (l != null && l != flashlightLight && l != flashlightSpillLight &&
                    (l.gameObject.name.Contains("Glint") || l.type == LightType.Point))
                {
                    l.enabled = false;
                    Destroy(l.gameObject);
                }
            }
        }
    }

    public void AutoAssignReferences()
    {
        Camera mainCam = Camera.main ?? GetComponentInChildren<Camera>();
        if (mainCam != null)
        {
            if (flashlightLight == null)
            {
                Transform flTransform = mainCam.transform.Find("FlashlightLight");
                if (flTransform != null)
                {
                    flashlightLight = flTransform.GetComponent<Light>();
                }
                else
                {
                    var l = mainCam.GetComponentInChildren<Light>(true);
                    if (l != null) flashlightLight = l;
                }
            }

            if (flashlightLight != null && flashlightSpillLight == null && enableSpillLight)
            {
                Transform spillT = flashlightLight.transform.Find("FlashlightSpillLight");
                if (spillT != null)
                {
                    flashlightSpillLight = spillT.GetComponent<Light>();
                }
                else
                {
                    GameObject spillGo = new GameObject("FlashlightSpillLight");
                    spillGo.transform.SetParent(flashlightLight.transform, false);
                    spillGo.transform.localPosition = Vector3.zero;
                    spillGo.transform.localRotation = Quaternion.identity;
                    flashlightSpillLight = spillGo.AddComponent<Light>();
                }
            }

            if (heldFlashlightModel == null)
            {
                Transform t = mainCam.transform.Find("HeldFlashlight");
                if (t != null) heldFlashlightModel = t.gameObject;
            }
        }
    }

    /// <summary>
    /// Configures the core high-power spotlight and peripheral spill fill light.
    /// </summary>
    public void ConfigureFlashlightLights()
    {
        if (flashlightLight != null)
        {
            flashlightLight.type = LightType.Spot;
            flashlightLight.intensity = powerfulIntensity;
            flashlightLight.range = powerfulRange;
            flashlightLight.spotAngle = outerSpotAngle;
            flashlightLight.innerSpotAngle = innerSpotAngle;
            flashlightLight.color = beamColor;
            flashlightLight.shadows = enableSoftShadows ? LightShadows.Soft : LightShadows.None;
            flashlightLight.shadowBias = 0.04f;
            flashlightLight.shadowNormalBias = 0.4f;
        }

        if (flashlightSpillLight != null)
        {
            flashlightSpillLight.type = LightType.Spot;
            flashlightSpillLight.intensity = spillIntensity;
            flashlightSpillLight.range = spillRange;
            flashlightSpillLight.spotAngle = spillSpotAngle;
            flashlightSpillLight.innerSpotAngle = 55.0f;
            flashlightSpillLight.color = new Color(beamColor.r * 0.94f, beamColor.g * 0.96f, beamColor.b, 1.0f);
            flashlightSpillLight.shadows = LightShadows.None; // Peripheral fill doesn't need shadow overhead
        }
    }

    public bool HasFlashlightInInventory()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.Slots == null) return false;
        inv.EnsureSlotsInitialized();
        foreach (var slot in inv.Slots)
        {
            if (slot != null && !slot.IsEmpty && slot.item != null)
            {
                if (slot.item == flashlightItemData || slot.item.itemName.ToLower().Contains("flashlight"))
                    return true;
            }
        }
        return false;
    }

    public static readonly Vector3 OffhandPosition = new Vector3(-0.30f, -0.27f, 0.28f);
    public static readonly Quaternion OffhandRotation = Quaternion.Euler(-1.5f, 185f, 4.0f);

    private void Update()
    {
        bool hasFlashlight = HasFlashlightInInventory();

        // Synchronize left-hand flashlight model visibility
        if (heldFlashlightModel != null)
        {
            bool shouldBeVisible = hasFlashlight && !LockerHideManager.IsPlayerHidden;
            if (heldFlashlightModel.activeSelf != shouldBeVisible)
            {
                heldFlashlightModel.SetActive(shouldBeVisible);
            }
            // Keep left-hand position locked in offhand (Minecraft shield / RE style)
            var sway = heldFlashlightModel.GetComponent<ViewmodelSway>();
            if (sway != null)
            {
                sway.SetBaseline(OffhandPosition, OffhandRotation);
            }
            else
            {
                heldFlashlightModel.transform.localPosition = OffhandPosition;
                heldFlashlightModel.transform.localRotation = OffhandRotation;
            }
        }

        if (!hasFlashlight)
        {
            isEquipped = false;
            if (isLightOn)
            {
                isLightOn = false;
                if (flashlightLight != null) flashlightLight.enabled = false;
                if (flashlightSpillLight != null) flashlightSpillLight.enabled = false;
            }
            return;
        }

        isEquipped = true;

        if (PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen) return;
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        // Left Click (Mouse 0) or [F] toggles the flashlight ON/OFF at any time!
        if (Input.GetMouseButtonDown(0) || GameSettingsManager.GetKeyDown(GameAction.Flashlight))
        {
            ToggleLight();
        }
    }

    /// <summary>
    /// Called by InventoryEquipController when Flashlight slot is equipped/unequipped.
    /// </summary>
    public void SetEquippedState(bool equipped)
    {
        AutoAssignReferences();
        StripRogueComponents();

        bool hasFlashlight = HasFlashlightInInventory();
        isEquipped = hasFlashlight;

        if (heldFlashlightModel != null)
        {
            heldFlashlightModel.SetActive(hasFlashlight && !LockerHideManager.IsPlayerHidden);
            var sway = heldFlashlightModel.GetComponent<ViewmodelSway>();
            if (sway != null)
            {
                sway.SetBaseline(OffhandPosition, OffhandRotation);
            }
            else
            {
                heldFlashlightModel.transform.localPosition = OffhandPosition;
                heldFlashlightModel.transform.localRotation = OffhandRotation;
            }
        }

        if (!hasFlashlight)
        {
            isLightOn = false;
            if (flashlightLight != null) flashlightLight.enabled = false;
            if (flashlightSpillLight != null) flashlightSpillLight.enabled = false;
        }
    }

    /// <summary>
    /// Toggles light on/off when player Left Clicks while holding flashlight in offhand.
    /// </summary>
    public void ToggleLight()
    {
        if (!HasFlashlightInInventory()) return;

        isLightOn = !isLightOn;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = isLightOn;
        }

        if (flashlightSpillLight != null)
        {
            flashlightSpillLight.enabled = isLightOn;
        }

        PlayClickSound();
    }

    private void PlayClickSound()
    {
        if (clickSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clickSound, 0.75f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clickSound, transform.position, 0.75f);
            }
        }
    }

    /// <summary>
    /// Generates a crisp, tactile switch click sound procedurally so flashlight toggles always feel punchy.
    /// </summary>
    private AudioClip CreateTactileClickClip()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.045f); // 45ms click
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float decay = Mathf.Exp(-t * 90.0f);
            // Sharp dual-tone mechanical click (1800Hz & 3200Hz) with slight noise pulse
            float wave = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.4f;
            float noise = (Random.value * 2f - 1f) * 0.25f;
            samples[i] = (wave + noise) * decay;
        }

        AudioClip clip = AudioClip.Create("FlashlightClick_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
