using UnityEngine;

// Attach this to the "SlotMenu" GameObject (under SlotCanvas). Controls the
// 6 held-item slots - binding them to InventoryManager's data, and handling clicks.
// (Tab-key open/close is handled separately, by InventoryScreen.cs.)
public class SlotMenu : MonoBehaviour, ISlotOwner
{
    [Header("SlotCanvas > SlotMenu > SlotHolder references")]
    [SerializeField] private SlotUI[] slotUIElements; // drag Slot, Slot (1) ... Slot (5) here, IN ORDER

    private void Start()
    {
        for (int i = 0; i < slotUIElements.Length; i++)
        {
            if (i < InventoryManager.Instance.Slots.Count)
                slotUIElements[i].Bind(InventoryManager.Instance.Slots[i], this);
        }

        InventoryManager.Instance.OnInventoryChanged += RefreshAllSlots;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshAllSlots;
    }

    private void RefreshAllSlots()
    {
        foreach (var slotUI in slotUIElements)
            slotUI.Refresh();
    }

    // Single click on a held item: use it if it's a consumable.
    public void OnSlotClicked(InventorySlot slot)
    {
        if (slot.item.itemType == InventoryItem.ItemType.Consumable)
            UseConsumable(slot);
    }

    // Double click on a held item: send it to the bag/storage, if there's room.
    public void OnSlotDoubleClicked(InventorySlot slot)
    {
        if (StorageManager.Instance == null) return;

        if (!StorageManager.Instance.HasFreeSlot())
        {
            Debug.Log("Storage is full.");
            return;
        }

        InventoryItem item = slot.item;
        int qty = slot.quantity;

        if (StorageManager.Instance.AddItem(item, qty))
            InventoryManager.Instance.RemoveItem(item, qty);
    }

    private void UseConsumable(InventorySlot slot)
    {
        InventoryItem item = slot.item;

        // Hook these up once your Anxiety/Stamina scripts exist, e.g.:
        // PlayerStatus.Instance.ChangeAnxiety(item.anxietyChange);
        // PlayerStatus.Instance.ChangeStamina(item.staminaChange);
        Debug.Log($"Used {item.itemName}: Anxiety {item.anxietyChange}, Stamina {item.staminaChange}");

        InventoryManager.Instance.RemoveItem(item, 1);
    }
}
