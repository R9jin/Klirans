using System;

// A plain data class (not a MonoBehaviour) representing one of the 6 inventory slots.
// [Serializable] lets it show up nicely in the Inspector if you ever expose it there.
[Serializable]
public class InventorySlot
{
    public InventoryItem item;
    public int quantity;

    public bool IsEmpty => item == null;

    public void Clear()
    {
        item = null;
        quantity = 0;
    }
}
