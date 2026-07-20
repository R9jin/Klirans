using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Tooltip("The InventoryItem that represents the flashlight.")]
    public InventoryItem flashlightItemData;

    [Tooltip("The Unity Light component to toggle (e.g., a Spotlight).")]
    public Light flashlightLight;

    private bool isFlashlightOn = false;

    void Start()
    {
        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
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
