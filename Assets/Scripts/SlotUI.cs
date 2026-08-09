using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Attach this to EVERY individual slot cell in BOTH grids:
// - the 6 "Slot" objects under SlotCanvas > SlotMenu > SlotHolder
// - the 12 "ItemSlot" objects under InventoryCanvas > InventoryMenu > InventorySlot
//
// Supports:
//   - Single click  -> show item preview (via ISlotOwner)
//   - Double click  -> move item to the other container
//   - Drag & drop   -> swap items within or across containers (hotbar <-> bag)
public class SlotUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text quantityText;

    // Optional: assign the slot's background Image for hover highlight
    [SerializeField] private Image slotBackground;

    private InventorySlot boundSlot;
    private ISlotOwner owner;
    private CanvasGroup canvasGroup;

    // Time-based double-click (more reliable than eventData.clickCount)
    private float lastClickTime = -99f;
    private const float DoubleClickThreshold = 0.35f;

    // Hover colours
    private Color defaultBgColor;
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.18f);

    // Public read-only access so InventoryDragHandler can query these
    public InventorySlot BoundSlot => boundSlot;
    public ISlotOwner    Owner     => owner;

    // ─── Unity Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        // CanvasGroup is used to fade & block raycasts during drag
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (slotBackground != null)
            defaultBgColor = slotBackground.color;
    }

    // ─── Bind ────────────────────────────────────────────────────────

    public void Bind(InventorySlot slot, ISlotOwner owningMenu)
    {
        boundSlot = slot;
        owner     = owningMenu;
        Refresh();
    }

    public void Refresh()
    {
        if (boundSlot == null || boundSlot.IsEmpty)
        {
            iconImage.enabled = false;
            if (quantityText != null) quantityText.text = "";
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite  = boundSlot.item.icon;

        if (quantityText != null)
            quantityText.text = (boundSlot.item.isStackable && boundSlot.quantity > 1)
                ? boundSlot.quantity.ToString()
                : "";
    }

    // ─── Click ───────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundSlot == null || boundSlot.IsEmpty)
        {
            owner?.OnSlotDeselected();
            return;
        }

        // Right-click or Time-based double-left-click transfers item between hotbar & bag
        bool isRightClick = (eventData.button == PointerEventData.InputButton.Right);
        float now = Time.unscaledTime;
        bool isDouble = (now - lastClickTime) <= DoubleClickThreshold;
        lastClickTime = now;

        if (isRightClick || isDouble)
        {
            lastClickTime = -99f;   // Reset so triple-click doesn't count again
            owner?.OnSlotDoubleClicked(boundSlot);
        }
        else
        {
            owner?.OnSlotClicked(boundSlot);
        }
    }

    // ─── Drag ────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Don't allow dragging from empty slots
        if (boundSlot == null || boundSlot.IsEmpty)
        {
            eventData.pointerDrag = null;   // Cancels the drag in EventSystem
            return;
        }

        InventoryDragHandler.Instance?.BeginDrag(this, eventData);
        canvasGroup.blocksRaycasts = false; // Let drop events pass through to slot below
        canvasGroup.alpha          = 0.35f; // Fade source slot
    }

    public void OnDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.EndDrag();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha          = 1f;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Tell the drag handler this slot is the drop target
        InventoryDragHandler.Instance?.Drop(this);
    }

    // ─── Hover ───────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (slotBackground != null)
            slotBackground.color = HoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (slotBackground != null)
            slotBackground.color = defaultBgColor;
    }
}
