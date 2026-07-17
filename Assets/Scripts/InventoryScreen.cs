using UnityEngine;

// Put this on ANY GameObject that stays active in the scene (e.g. directly on
// InventoryCanvas). Only controls the 12-slot BAG (InventoryMenu) with [Tab].
// The 6-slot held bar (SlotMenu) is NOT touched here - it's meant to stay
// visible on screen at all times, like a HUD hotbar.
public class InventoryScreen : MonoBehaviour
{
    [SerializeField] private GameObject inventoryMenuPanel; // "InventoryMenu" under InventoryCanvas

    private bool isOpen = false;

    private void Start()
    {
        inventoryMenuPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            Toggle();
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        inventoryMenuPanel.SetActive(isOpen);

        // Cursor still needs to unlock so you can click items in the bag.
        // Player movement/look is intentionally NOT frozen anymore - the
        // game keeps running while the bag is open.
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }
}