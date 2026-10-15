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
                // Auto-equip and turn on flashlight immediately upon pickup
                if (itemData != null && itemData.itemName.ToLower().Contains("flashlight"))
                {
                    if (FlashlightController.Instance != null)
                    {
                        FlashlightController.Instance.SetEquippedState(true);
                    }
                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective(
                            "SURVIVE THE CLEARANCE NIGHTMARE",
                            "Nakatulog ka sa classroom at sarado na ang buong campus! Hanapin ang 4 na piraso ng clearance slip at kumpletuhin ang 6 na signatories bago mag-hatinggabi!"
                        );
                    }
                }

                var highlight = GetComponent<ItemVisualHighlight>() ?? GetComponentInChildren<ItemVisualHighlight>();
                if (highlight != null) highlight.OnPickedUp();

                var lights = GetComponentsInChildren<Light>(true);
                foreach (var l in lights) l.enabled = false;

                var renderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.enabled = false;

                var failsafe = GetComponentInParent<DroppedItemFailsafe>();
                GameObject toDestroy = (failsafe != null) ? failsafe.gameObject : gameObject;
                Destroy(toDestroy);
            }
        }
        else
        {
            Debug.Log("Inventory is full!");
        }
    }
}
