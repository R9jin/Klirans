using UnityEngine;
using UnityEngine.UI;

// Attach this to the "InventoryMenu" GameObject (under InventoryCanvas). Controls
// the 12-slot bag grid, plus the item preview panel (the two boxes on the right
// in your screenshot - the icon and the description text).
public class StorageMenu : MonoBehaviour, ISlotOwner
{
    [Header("InventoryCanvas > InventoryMenu > InventorySlot references")]
    [SerializeField] private SlotUI[] slotUIElements; // drag ItemSlot, ItemSlot (1) ... ItemSlot (11) here, IN ORDER

    [Header("InventoryCanvas > InventoryMenu > InventoryDescription references")]
    [SerializeField] private GameObject inventoryDescription; // the "InventoryDescription" GameObject
    [SerializeField] private Image itemImage;                 // the "ItemImage" child
    [SerializeField] private Text itemDescriptionText;        // the "ItemDescription" child

    private void Start()
    {
        if (inventoryDescription != null)
            inventoryDescription.SetActive(false);

        for (int i = 0; i < slotUIElements.Length; i++)
        {
            if (i < StorageManager.Instance.Slots.Count)
                slotUIElements[i].Bind(StorageManager.Instance.Slots[i], this);
        }

        StorageManager.Instance.OnInventoryChanged += RefreshAllSlots;
    }

    private void OnDestroy()
    {
        if (StorageManager.Instance != null)
            StorageManager.Instance.OnInventoryChanged -= RefreshAllSlots;
    }

    private void RefreshAllSlots()
    {
        foreach (var slotUI in slotUIElements)
            slotUI.Refresh();
    }

    // Single click on a stored item: show its icon + description in the preview panel.
    public void OnSlotClicked(InventorySlot slot)
    {
        if (inventoryDescription == null) return;

        inventoryDescription.SetActive(true);
        if (itemImage != null) itemImage.sprite = slot.item.icon;
        if (itemDescriptionText != null)
            itemDescriptionText.text = $"{slot.item.itemName}\n{slot.item.description}";
    }

    // Double click on a stored item: move it to the held (6-slot) inventory, if there's room.
    public void OnSlotDoubleClicked(InventorySlot slot)
    {
        if (InventoryManager.Instance == null) return;

        if (!InventoryManager.Instance.HasFreeSlot())
        {
            Debug.Log("Held inventory is full (max 6).");
            return;
        }

        InventoryItem item = slot.item;
        int qty = slot.quantity;

        if (InventoryManager.Instance.AddItem(item, qty))
            StorageManager.Instance.RemoveItem(item, qty);
    }
}
