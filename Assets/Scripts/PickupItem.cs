using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public InventoryItem itemData;
    public int amount = 1;

    public void Interact()
    {
        if (itemData == null)
        {
            Debug.LogWarning("PickupItem has no InventoryItem assigned!");
            return;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.HasFreeSlot())
        {
            bool added = InventoryManager.Instance.AddItem(itemData, amount);
            if (added)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.Log("Inventory is full!");
        }
    }
}
