// Both SlotMenu (6 held slots) and StorageMenu (12 bag slots) implement this,
// so a single SlotUI script can be reused on BOTH the "Slot" objects and the
// "ItemSlot" objects without duplicating code.
public interface ISlotOwner
{
    void OnSlotClicked(InventorySlot slot);
    void OnSlotDoubleClicked(InventorySlot slot);
}
