using UnityEngine;

// The 6 slots the player physically holds (SlotCanvas > SlotMenu > SlotHolder > Slot x6).
// This is the "carry" inventory - what's directly usable/quick-access during gameplay.
public class InventoryManager : SlotContainer
{
    public static InventoryManager Instance { get; private set; }

    [Header("Starting Items")]
    [Tooltip("Items to add to the player's hotbar at game start (e.g. Blank_Paper clearance slip).")]
    public InventoryItem[] startingItems;

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

    private void Start()
    {
        if (startingItems != null)
        {
            foreach (var item in startingItems)
            {
                if (item != null && !HasItem(item))
                {
                    AddItem(item, 1);
                }
            }
        }
    }
}
