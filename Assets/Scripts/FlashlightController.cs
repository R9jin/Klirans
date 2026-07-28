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

    [Header("Held Item View")]
    [Tooltip("The 3D flashlight model shown in view when equipped (child of Main Camera).")]
    public GameObject heldFlashlightModel;

    [Header("Audio (Optional)")]
    public AudioClip clickSound;

    private bool isEquipped = false;
    private bool isLightOn = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        AutoAssignReferences();
        SetEquippedState(false);
    }

    public void AutoAssignReferences()
    {
        Camera mainCam = Camera.main ?? GetComponentInChildren<Camera>();
        if (mainCam != null)
        {
            if (flashlightLight == null)
            {
                var l = mainCam.GetComponentInChildren<Light>(true);
                if (l != null) flashlightLight = l;
            }

            if (heldFlashlightModel == null)
            {
                Transform t = mainCam.transform.Find("HeldFlashlight");
                if (t != null) heldFlashlightModel = t.gameObject;
            }
        }
    }

    private void Update()
    {
        // Only listen for Left Click toggle when the flashlight is actively equipped
        if (isEquipped)
        {
            if (Input.GetMouseButtonDown(0))
            {
                ToggleLight();
            }
        }
    }

    /// <summary>
    /// Called by InventoryEquipController when Flashlight slot is equipped/unequipped.
    /// </summary>
    public void SetEquippedState(bool equipped)
    {
        AutoAssignReferences();
        isEquipped = equipped;

        if (heldFlashlightModel != null)
        {
            heldFlashlightModel.SetActive(equipped);
            if (equipped)
            {
                // Strip rogue PickupItem scripts or Colliders on viewmodel so raycasts/interaction never hit it
                foreach (var p in heldFlashlightModel.GetComponentsInChildren<PickupItem>(true))
                {
                    if (p != null) Destroy(p);
                }
                foreach (var col in heldFlashlightModel.GetComponentsInChildren<Collider>(true))
                {
                    if (col != null) Destroy(col);
                }
            }
        }

        if (!equipped)
        {
            // Turn off light when stowed/unequipped
            isLightOn = false;
            if (flashlightLight != null)
            {
                flashlightLight.enabled = false;
            }
        }
        else
        {
            // Turn on light when initially equipped
            isLightOn = true;
            if (flashlightLight != null)
            {
                flashlightLight.enabled = true;
            }
        }
    }

    /// <summary>
    /// Toggles light on/off when player Left Clicks while holding flashlight.
    /// </summary>
    public void ToggleLight()
    {
        if (!isEquipped) return;

        isLightOn = !isLightOn;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = isLightOn;
        }

        if (clickSound != null)
        {
            AudioSource.PlayClipAtPoint(clickSound, transform.position, 0.8f);
        }
    }

    public bool IsLightOn() => isLightOn;
    public bool IsEquipped() => isEquipped;
}
