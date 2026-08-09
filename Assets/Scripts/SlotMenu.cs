using UnityEngine;

// Attach this to the "SlotMenu" GameObject (under SlotCanvas). Controls the
// 6 held-item hotbar slots - binding them to InventoryManager's data.
//
// Click behaviour:
//   - Single click: equip item (hotkey slot), show preview ONLY if Tab bag is open
//   - Double click: send to bag/storage if there's room
// (Tab-key open/close is handled separately by InventoryScreen.cs.)
public class SlotMenu : MonoBehaviour, ISlotOwner
{
    [Header("SlotCanvas > SlotMenu > SlotHolder references")]
    [SerializeField] private SlotUI[] slotUIElements; // drag Slot, Slot (1) ... Slot (5) here, IN ORDER

    private void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
        }

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

    // ─── ISlotOwner ──────────────────────────────────────────────────

    /// <summary>Returns the backing data container for drag-and-drop.</summary>
    public SlotContainer GetContainer() => InventoryManager.Instance;

    /// <summary>
    /// Single click on a hotbar slot.
    /// - Shows item preview ONLY when the Tab inventory bag is open.
    /// - Consumes the item if it is a Consumable type.
    /// </summary>
    public void OnSlotClicked(InventorySlot slot)
    {
        // Show preview only while inventory panel is open
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
            StorageMenu.ShowPreview(slot.item);

        // Original consumable-use behaviour
        if (slot.item.itemType == InventoryItem.ItemType.Consumable)
            UseConsumable(slot);
    }

    /// <summary>Double click on a hotbar slot: send it to the bag/storage if there's room.</summary>
    public void OnSlotDoubleClicked(InventorySlot slot)
    {
        if (StorageManager.Instance == null || slot == null || slot.IsEmpty) return;

        if (!StorageManager.Instance.HasFreeSlot())
        {
            Debug.Log("Storage is full.");
            return;
        }

        InventoryItem item = slot.item;
        int           qty  = slot.quantity;

        if (StorageManager.Instance.AddItem(item, qty))
        {
            slot.quantity -= qty;
            if (slot.quantity <= 0) slot.Clear();
            InventoryManager.Instance.NotifyChanged();
        }
    }

    /// <summary>Click on an empty hotbar slot: hide preview if inventory is open.</summary>
    public void OnSlotDeselected()
    {
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
            StorageMenu.HidePreview();
    }

    // ─── Private ─────────────────────────────────────────────────────

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
