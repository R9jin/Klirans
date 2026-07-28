using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    public InventoryItem itemData;
    public int amount = 1;

    public string GetPrompt()
    {
        return "Press E to Pick Up " + (itemData != null ? itemData.itemName : "Item");
    }

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
