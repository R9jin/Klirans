using UnityEngine;

// Attach this to the "SlotMenu" GameObject (under SlotCanvas). Controls the
// 6 held-item hotbar slots - binding them to InventoryManager's data.
//
// Click behaviour:
//   - Single click: equip item (hotkey slot), show preview ONLY if Tab bag is open
//   - Double click: send to bag/storage if there's room
// (Tab-key open/close is handled separately by InventoryScreen.cs.)
public class SlotMenu : MonoBehaviour, ISlotOwner
{
    public static SlotMenu Instance { get; private set; }

    [Header("SlotCanvas > SlotMenu > SlotHolder references")]
    [SerializeField] private SlotUI[] slotUIElements; // drag Slot, Slot (1) ... Slot (5) here, IN ORDER

    [Header("Auto-Hide Settings")]
    [SerializeField] private CanvasGroup hotbarCanvasGroup;
    [SerializeField] private float displayDuration = 3.5f;
    [SerializeField] private float fadeSpeed = 4.0f;

    private float hideTimer = 2.5f; // Brief display on startup, then fades out
    private int currentActiveSlot = -1;

    private void Awake()
    {
        Instance = this;

        if (hotbarCanvasGroup == null)
            hotbarCanvasGroup = GetComponent<CanvasGroup>();
        if (hotbarCanvasGroup == null)
            hotbarCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
        }

        for (int i = 0; i < slotUIElements.Length; i++)
        {
            if (i < InventoryManager.Instance.Slots.Count)
                slotUIElements[i].Bind(InventoryManager.Instance.Slots[i], this);

            slotUIElements[i].SetSlotNumber((i + 1).ToString());
        }

        InventoryManager.Instance.OnInventoryChanged += OnInventoryDataChanged;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryDataChanged;
    }

    private void Update()
    {
        HandleAutoFade();
    }

    private void HandleAutoFade()
    {
        if (hotbarCanvasGroup == null) return;

        // Keep visible whenever Tab inventory bag is open
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
        {
            hideTimer = displayDuration;
        }

        // Detect user activity (number keys 1-6 or mouse scroll)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) ||
            Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) ||
            Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Alpha6) ||
            Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
        {
            PingVisibility();
        }

        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
        }

        float targetAlpha = (hideTimer > 0f) ? 1f : 0f;
        hotbarCanvasGroup.alpha = Mathf.MoveTowards(hotbarCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        bool isVisible = hotbarCanvasGroup.alpha > 0.05f;
        hotbarCanvasGroup.blocksRaycasts = isVisible;
        hotbarCanvasGroup.interactable = isVisible;
    }

    /// <summary>
    /// Forces the hotbar to fade in and stay visible for the specified duration.
    /// </summary>
    public void PingVisibility(float duration = -1f)
    {
        hideTimer = Mathf.Max(hideTimer, duration > 0f ? duration : displayDuration);
    }

    /// <summary>
    /// Updates the visual highlight for the currently equipped hotbar slot.
    /// </summary>
    public void UpdateActiveSlotHighlight(int activeIndex)
    {
        currentActiveSlot = activeIndex;
        for (int i = 0; i < slotUIElements.Length; i++)
        {
            if (slotUIElements[i] != null)
            {
                slotUIElements[i].SetActiveHighlight(i == activeIndex);
            }
        }
        PingVisibility();
    }

    private void OnInventoryDataChanged()
    {
        RefreshAllSlots();
        PingVisibility();
    }

    private void RefreshAllSlots()
    {
        foreach (var slotUI in slotUIElements)
            slotUI.Refresh();
    }

    // ─── ISlotOwner ──────────────────────────────────────────────────

    /// <summary>Returns the backing data container for drag-and-drop.</summary>
    public SlotContainer GetContainer() => InventoryManager.Instance;

    /// <summary>
    /// Single click on a hotbar slot.
    /// - Shows item preview ONLY when the Tab inventory bag is open.
    /// - Consumes the item if it is a Consumable type.
    /// </summary>
    public void OnSlotClicked(InventorySlot slot)
    {
        PingVisibility();

        // Show preview only while inventory panel is open
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
            StorageMenu.ShowPreview(slot.item);

        // Equip clicked slot if equip controller is present
        if (InventoryEquipController.Instance != null && slot != null && InventoryManager.Instance != null)
        {
            int idx = InventoryManager.Instance.Slots.IndexOf(slot);
            if (idx >= 0 && idx < 6)
            {
                InventoryEquipController.Instance.SelectSlot(idx);
            }
        }

        // Original consumable-use behaviour
        if (slot.item != null && slot.item.itemType == InventoryItem.ItemType.Consumable)
            UseConsumable(slot);
    }

    /// <summary>Double click on a hotbar slot: send it to the bag/storage if there's room.</summary>
    public void OnSlotDoubleClicked(InventorySlot slot)
    {
        if (StorageManager.Instance == null || slot == null || slot.IsEmpty) return;

        if (!StorageManager.Instance.HasFreeSlot())
        {
            Debug.Log("Storage is full.");
            return;
        }

        InventoryItem item = slot.item;
        int           qty  = slot.quantity;

        if (StorageManager.Instance.AddItem(item, qty))
        {
            slot.quantity -= qty;
            if (slot.quantity <= 0) slot.Clear();
            InventoryManager.Instance.NotifyChanged();
        }
    }

    /// <summary>Click on an empty hotbar slot: hide preview if inventory is open.</summary>
    public void OnSlotDeselected()
    {
        if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen)
            StorageMenu.HidePreview();
    }

    // ─── Private ─────────────────────────────────────────────────────

    private void UseConsumable(InventorySlot slot)
    {
        InventoryItem item = slot.item;

        // Hook these up once your Anxiety/Stamina scripts exist, e.g.:
        // PlayerStatus.Instance.ChangeAnxiety(item.anxietyChange);
        // PlayerStatus.Instance.ChangeStamina(item.staminaChange);
        Debug.Log($"Used {item.itemName}: Anxiety {item.anxietyChange}, Stamina {item.staminaChange}");

        InventoryManager.Instance.RemoveItem(item, 1);
    }
}
