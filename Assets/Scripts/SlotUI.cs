using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Attach this to EVERY individual slot cell in BOTH grids:
// - the 6 "Slot" objects under SlotCanvas > SlotMenu > SlotHolder
// - the 12 "ItemSlot" objects under InventoryCanvas > InventoryMenu > InventorySlot
// It doesn't care which grid it belongs to - that's handled by whichever
// ISlotOwner (SlotMenu or StorageMenu) it gets bound to.
public class SlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text quantityText;

    private InventorySlot boundSlot;
    private ISlotOwner owner;

    public void Bind(InventorySlot slot, ISlotOwner owningMenu)
    {
        boundSlot = slot;
        owner = owningMenu;
        Refresh();
    }

    public void Refresh()
    {
        if (boundSlot == null || boundSlot.IsEmpty)
        {
            iconImage.enabled = false;
            quantityText.text = "";
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = boundSlot.item.icon;

        quantityText.text = (boundSlot.item.isStackable && boundSlot.quantity > 1)
            ? boundSlot.quantity.ToString()
            : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundSlot == null || boundSlot.IsEmpty) return;

        // Double-click moves the item between the bag and the held slots (see SlotMenu/StorageMenu).
        if (eventData.clickCount >= 2)
            owner.OnSlotDoubleClicked(boundSlot);
        else
            owner.OnSlotClicked(boundSlot);
    }
}
