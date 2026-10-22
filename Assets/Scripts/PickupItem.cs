using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    public InventoryItem itemData;
    public int amount = 1;

    /// <summary>
    /// Checks if this item can be picked up, preventing sequence breaks on key clearance documents
    /// before the player has assembled their clearance slip or received the faculty directive.
    /// Items previously dropped by the player are ALWAYS pickable.
    /// </summary>
    public bool CanBePickedUp(out string failureReason)
    {
        failureReason = string.Empty;
        if (itemData == null) return false;

        // Dropped items can ALWAYS be picked back up by the player
        if (GetComponent<DroppedItemFailsafe>() != null || GetComponentInParent<DroppedItemFailsafe>() != null)
        {
            return true;
        }

        string lowerName = itemData.itemName.ToLowerInvariant();

        // 1. Fragments, Flashlight, Keys, Food, Coins, Fuse can always be picked up freely
        if (lowerName.Contains("fragment") || lowerName.Contains("flashlight") || 
            lowerName.Contains("key") || lowerName.Contains("sandwich") || 
            lowerName.Contains("coin") || lowerName.Contains("fuse"))
        {
            return true;
        }

        // 2. ComLab Accountability Form: Requires Clearance Slip AND CCS Dean interaction/objective
        if (lowerName.Contains("comlab") && lowerName.Contains("form"))
        {
            bool hasSlip = ClearanceNPC.PlayerHasBlankSlip();
            if (!hasSlip)
            {
                failureReason = "Kailangan mo munang buuin ang iyong Clearance Slip mula sa 4 na punit na piraso sa hallways bago kunin ang ComLab Form!";
                return false;
            }

            // Must have completed Guidance Counselor (Signature 1) or have received Dean's directive
            bool guidanceDone = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(1);
            bool hasLockpin = LockpickingMinigame.HasLockpin();
            if (!guidanceDone && !hasLockpin)
            {
                failureReason = "Kailangan mo munang kausapin si CCS Dean sa Room 202 upang makatanggap ng authorization at Lockpin bago kunin ang form na ito!";
                return false;
            }
            return true;
        }

        // 3. Other official clearance documents require that the player has assembled the Clearance Slip
        if (lowerName.Contains("medical") || lowerName.Contains("seal") || lowerName.Contains("voucher"))
        {
            bool hasSlip = ClearanceNPC.PlayerHasBlankSlip();
            if (!hasSlip)
            {
                failureReason = "Kailangan mo munang buuin ang iyong Clearance Slip mula sa 4 na punit na piraso sa hallways bago kunin ang dokumentong ito!";
                return false;
            }
        }

        return true;
    }

    public string GetPrompt()
    {
        if (!CanBePickedUp(out string failureReason))
        {
            string lowerName = itemData != null ? itemData.itemName.ToLowerInvariant() : "";
            if (lowerName.Contains("comlab"))
            {
                bool hasSlip = ClearanceNPC.PlayerHasBlankSlip();
                if (!hasSlip)
                    return "[ComLab Form — Kailangan muna ng Clearance Slip]";
                return "[ComLab Form — Kailangan ng Directive mula kay Dean (Room 202)]";
            }
            return $"[{itemData?.itemName} — Kailangan muna ng Clearance Slip]";
        }

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

        if (!CanBePickedUp(out string reason))
        {
            if (ObjectiveHUD.Instance != null && !string.IsNullOrEmpty(reason))
            {
                ObjectiveHUD.Instance.SetObjective("Kailangan ng Clearance Directive", reason);
            }
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
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective("Puno ang Bag", "Pindutin ang [G] para mag-drop ng item sa sahig upang magkaroon ng espasyo.");
            }
        }
    }
}
