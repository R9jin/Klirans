using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Tooltip("The InventoryItem that represents the flashlight.")]
    public InventoryItem flashlightItemData;

    [Tooltip("The Unity Light component to toggle (e.g., a Spotlight).")]
    public Light flashlightLight;

    [Header("Held Item View")]
    [Tooltip("The 3D flashlight model shown in the player's view when equipped (child of Main Camera).")]
    public GameObject heldFlashlightModel;

    private bool isFlashlightOn = false;

    void Start()
    {
        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
        }

        // Make sure the held model starts hidden
        if (heldFlashlightModel != null)
        {
            heldFlashlightModel.SetActive(false);
        }
    }

    void Update()
    {
        // Toggle the flashlight if 'F' is pressed
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }
        
        // Ensure flashlight turns off if we lose the item
        if (isFlashlightOn && !HasFlashlight())
        {
            isFlashlightOn = false;
            if (flashlightLight != null)
                flashlightLight.enabled = false;
            if (heldFlashlightModel != null)
                heldFlashlightModel.SetActive(false);
        }
    }

    private void ToggleFlashlight()
    {
        if (HasFlashlight())
        {
            isFlashlightOn = !isFlashlightOn;
            if (flashlightLight != null)
            {
                flashlightLight.enabled = isFlashlightOn;
            }
            if (heldFlashlightModel != null)
            {
                heldFlashlightModel.SetActive(isFlashlightOn);
            }
            // Optional: Play a click sound here
        }
        else
        {
            // Optional: Show message "You don't have a flashlight."
            // Debug.Log("Cannot use flashlight, not in inventory.");
        }
    }

    private bool HasFlashlight()
    {
        if (InventoryManager.Instance == null || flashlightItemData == null) return false;
        return InventoryManager.Instance.HasItem(flashlightItemData);
    }
}
