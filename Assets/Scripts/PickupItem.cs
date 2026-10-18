using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    public InventoryItem itemData;
    public int amount = 1;

    public string GetPrompt()
    {
        string keyName = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Interact));
        return $"Press [{keyName}] to Pick Up " + (itemData != null ? itemData.itemName : "Item");
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
                bool isFlashlight = itemData != null && itemData.itemName.ToLower().Contains("flashlight");

                // Find the slot where this item was placed and select it in InventoryEquipController
                // so the player can immediately use or drop ([G]/[Q]) the item!
                if (InventoryEquipController.Instance != null)
                {
                    int targetSlot = -1;
                    var slots = InventoryManager.Instance.Slots;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (slots[i] != null && !slots[i].IsEmpty && slots[i].item == itemData)
                        {
                            targetSlot = i;
                            break;
                        }
                    }

                    if (targetSlot >= 0 && (isFlashlight || InventoryEquipController.Instance.GetActiveSlotIndex() < 0))
                    {
                        if (InventoryEquipController.Instance.GetActiveSlotIndex() == targetSlot)
                        {
                            // Already on this slot index, force re-equip
                            InventoryEquipController.Instance.EquipItem(itemData);
                        }
                        else
                        {
                            InventoryEquipController.Instance.SelectSlot(targetSlot);
                        }
                    }
                }

                // Auto-equip and turn on flashlight immediately upon pickup
                if (isFlashlight)
                {
                    if (FlashlightController.Instance != null && !FlashlightController.Instance.IsEquipped)
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
