using UnityEngine;

// This is a "data container" for one type of item (e.g. "Coffee Cup", "Library Card").
// It is NOT attached to a GameObject in the scene - instead you right-click in the
// Project window and create an instance of it as an .asset file. That asset gets
// dragged into your InventoryManager/pickup scripts wherever you need to reference it.
[CreateAssetMenu(fileName = "New Item", menuName = "Klirans/Inventory Item")]
public class InventoryItem : ScriptableObject
{
    public enum ItemType { Document, KeyItem, Consumable, Tool }

    [Header("Basic Info")]
    public string itemName = "New Item";
    [TextArea(2, 4)]
    public string description = "";
    public Sprite icon;
    public ItemType itemType;

    [Header("Stacking")]
    // Documents/Key Items should usually be non-stackable (e.g. one unique grade sheet).
    // Consumables like coffee could stack if you want multiple.
    public bool isStackable = false;
    public int maxStack = 1;

    [Header("Consumable Effects (only relevant if ItemType = Consumable)")]
    public float anxietyChange = 0f;   // negative = reduces Anxiety, positive = raises it (e.g. energy drink = +15)
    public float staminaChange = 0f;   // positive = restores Stamina

    [Header("Quest Item Behaviour")]
    public bool canBeDropped = true;   // set false for clearance documents you should never lose
}
