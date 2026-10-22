using UnityEngine;

// The bigger 12-slot bag/stash (InventoryCanvas > InventoryMenu > InventorySlot > ItemSlot x12).
// This is separate storage - items sitting "in your bag" but not immediately equipped/held.
public class StorageManager : SlotContainer
{
    public static StorageManager Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        size = 12; // matches your ItemSlot (0) through ItemSlot (11)
        base.Awake();
    }
}
