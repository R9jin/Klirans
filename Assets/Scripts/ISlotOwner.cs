// Both SlotMenu (6 held slots) and StorageMenu (12 bag slots) implement this,
// so a single SlotUI script can be reused on BOTH the "Slot" objects and the
// "ItemSlot" objects without duplicating code.
public interface ISlotOwner
{
    /// <summary>Single click on a filled slot.</summary>
    void OnSlotClicked(InventorySlot slot);

    /// <summary>Double click on a filled slot (move to other container).</summary>
    void OnSlotDoubleClicked(InventorySlot slot);

    /// <summary>Click on an empty slot or outside - hide preview if needed.</summary>
    void OnSlotDeselected();

    /// <summary>Returns the backing SlotContainer so InventoryDragHandler can fire change events.</summary>
    SlotContainer GetContainer();
}
