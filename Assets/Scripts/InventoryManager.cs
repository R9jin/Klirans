using UnityEngine;

// The 6 slots the player physically holds (SlotCanvas > SlotMenu > SlotHolder > Slot x6).
// This is the "carry" inventory - what's directly usable/quick-access during gameplay.
public class InventoryManager : SlotContainer
{
    public static InventoryManager Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        size = 6; // matches the concept doc's 6-slot design - don't change this in the Inspector
        base.Awake();
    }
}
