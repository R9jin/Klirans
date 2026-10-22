using UnityEngine;

// Put this on ANY GameObject that stays active in the scene (e.g. directly on
// InventoryCanvas). Only controls the 12-slot BAG (InventoryMenu) with [Tab].
// The 6-slot held bar (SlotMenu) is NOT touched here - it's meant to stay
// visible on screen at all times, like a HUD hotbar.
public class InventoryScreen : MonoBehaviour
{
    public static InventoryScreen Instance { get; private set; }

    [SerializeField] private GameObject inventoryMenuPanel; // "InventoryMenu" under InventoryCanvas

    /// <summary>True while the inventory bag panel is open (Tab toggled on).</summary>
    public bool IsOpen => isOpen;

    private bool isOpen = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        isOpen = false;
        if (inventoryMenuPanel != null)
            inventoryMenuPanel.SetActive(false);
    }

    private void Update()
    {
        // Only 6 hotbar slots are used; the [TAB] bag is permanently disabled.
        if (isOpen)
        {
            isOpen = false;
            if (inventoryMenuPanel != null)
                inventoryMenuPanel.SetActive(false);
        }
    }

    public void Toggle()
    {
        isOpen = false;
        if (inventoryMenuPanel != null)
            inventoryMenuPanel.SetActive(false);
        StorageMenu.HidePreview();
    }
}