using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the "InventoryMenu" GameObject (under InventoryCanvas).
/// Controls the 12-slot bag grid and the item preview panel (icon + description).
///
/// Also exposes static ShowPreview / HidePreview so SlotMenu can show
/// the same preview panel when the player clicks a hotbar slot while the
/// inventory is open.
///
/// Setup notes:
///   - inventoryDescription : drag the "ItemDescription" (panel root) here
///   - itemImage            : drag the "ItemImage" Image child here
///   - itemDescriptionText  : drag any Text child of ItemDescription here.
///                            If left NULL at Start(), this script will auto-create
///                            a Text child inside the ItemDescription object.
/// </summary>
public class StorageMenu : MonoBehaviour, ISlotOwner
{
    // Static singleton so SlotMenu can call ShowPreview / HidePreview
    public static StorageMenu Instance { get; private set; }

    [Header("InventoryCanvas > InventoryMenu > InventorySlot references")]
    [SerializeField] private SlotUI[] slotUIElements; // drag ItemSlot x12 here, IN ORDER

    [Header("InventoryCanvas > InventoryMenu > InventoryDescription references")]
    [SerializeField] private GameObject inventoryDescription; // "InventoryDescription" parent panel
    [SerializeField] private Image      itemImage;            // "ItemImage" child Image
    [SerializeField] private Text       itemNameText;         // (auto-created) Large bold item name
    [SerializeField] private Text       itemDescriptionText;  // (auto-created) Description body

    // ─── Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;
        }

        // Auto-wire text fields if not assigned in Inspector
        EnsureDescriptionTexts();

        // Clear and hide preview panel on startup
        HidePreview();

        // Bind each slot UI to the matching data slot
        if (StorageManager.Instance == null)
        {
            Debug.LogError("[StorageMenu] StorageManager.Instance is null!");
            return;
        }

        for (int i = 0; i < slotUIElements.Length; i++)
        {
            if (i < StorageManager.Instance.Slots.Count)
                slotUIElements[i].Bind(StorageManager.Instance.Slots[i], this);
        }

        StorageManager.Instance.OnInventoryChanged += RefreshAllSlots;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (StorageManager.Instance != null)
            StorageManager.Instance.OnInventoryChanged -= RefreshAllSlots;
    }

    private void RefreshAllSlots()
    {
        foreach (var slotUI in slotUIElements)
            slotUI.Refresh();
    }

    /// <summary>
    /// Ensures itemNameText and itemDescriptionText exist inside the inventoryDescription panel.
    /// Configures bold black text styling for high contrast readability.
    /// </summary>
    private void EnsureDescriptionTexts()
    {
        if (inventoryDescription == null) return;

        // 1. Ensure ItemImage has crisp white color and aspect preservation
        if (itemImage != null)
        {
            itemImage.color = Color.white;
            itemImage.preserveAspect = true;
        }

        // 2. Find or configure text components
        Transform descPanel = inventoryDescription.transform.Find("ItemDescription") ??
                              inventoryDescription.transform;

        if (itemNameText == null)
        {
            Transform existing = descPanel.Find("ItemNameText");
            if (existing != null)
                itemNameText = existing.GetComponent<Text>();
            else
                itemNameText = CreateText(descPanel, "ItemNameText",
                    fontSize: 28,
                    fontStyle: FontStyle.Bold,
                    alignment: TextAnchor.UpperLeft,
                    color: Color.black,
                    anchorMin: new Vector2(0f, 0.75f),
                    anchorMax: Vector2.one,
                    offsetMin: new Vector2(16f, 0f),
                    offsetMax: new Vector2(-16f, -10f));
        }

        if (itemNameText != null)
        {
            itemNameText.fontSize = 28;
            itemNameText.fontStyle = FontStyle.Bold;
            itemNameText.color = Color.black;
        }

        if (itemDescriptionText == null)
        {
            Transform existing = descPanel.Find("ItemDescText");
            if (existing != null)
                itemDescriptionText = existing.GetComponent<Text>();
            else
                itemDescriptionText = CreateText(descPanel, "ItemDescText",
                    fontSize: 20,
                    fontStyle: FontStyle.Bold,
                    alignment: TextAnchor.UpperLeft,
                    color: Color.black,
                    anchorMin: Vector2.zero,
                    anchorMax: new Vector2(1f, 0.75f),
                    offsetMin: new Vector2(16f, 12f),
                    offsetMax: new Vector2(-16f, -5f));
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.fontSize = 20;
            itemDescriptionText.fontStyle = FontStyle.Bold;
            itemDescriptionText.color = Color.black;
        }
    }

    private Text CreateText(Transform parent, string goName,
        int fontSize, FontStyle fontStyle, TextAnchor alignment,
        Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go   = new GameObject(goName);
        go.transform.SetParent(parent, false);

        var rt         = go.AddComponent<RectTransform>();
        rt.anchorMin   = anchorMin;
        rt.anchorMax   = anchorMax;
        rt.offsetMin   = offsetMin;
        rt.offsetMax   = offsetMax;

        var txt             = go.AddComponent<Text>();
        txt.fontSize        = fontSize;
        txt.fontStyle       = fontStyle;
        txt.alignment       = alignment;
        txt.color           = color;
        txt.raycastTarget   = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Truncate;

        Font builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.font = builtIn;

        return txt;
    }

    // ─── ISlotOwner ──────────────────────────────────────────────────

    /// <summary>Returns the backing data container for drag-and-drop.</summary>
    public SlotContainer GetContainer() => StorageManager.Instance;

    /// <summary>Single click on a bag slot -> show item preview.</summary>
    public void OnSlotClicked(InventorySlot slot)
    {
        ShowPreview(slot.item);
    }

    /// <summary>Double click on a bag slot -> move to 6-slot hotbar if there's room.</summary>
    public void OnSlotDoubleClicked(InventorySlot slot)
    {
        if (InventoryManager.Instance == null || slot == null || slot.IsEmpty) return;

        if (!InventoryManager.Instance.HasFreeSlot())
        {
            Debug.Log("Held 6-slot inventory is full.");
            return;
        }

        InventoryItem item = slot.item;
        int           qty  = slot.quantity;

        if (InventoryManager.Instance.AddItem(item, qty))
        {
            slot.quantity -= qty;
            if (slot.quantity <= 0) slot.Clear();
            StorageManager.Instance.NotifyChanged();
        }
    }

    /// <summary>Click on empty bag slot -> hide preview.</summary>
    public void OnSlotDeselected()
    {
        HidePreview();
    }

    // ─── Static Preview API ──────────────────────────────────────────

    /// <summary>
    /// Show an item's icon and description in the preview panel.
    /// Called by both StorageMenu.OnSlotClicked and SlotMenu.OnSlotClicked
    /// (hotbar slot - only when Tab inventory is open).
    /// </summary>
    public static void ShowPreview(InventoryItem item)
    {
        if (Instance == null || item == null) return;
        if (Instance.inventoryDescription == null) return;

        Instance.inventoryDescription.SetActive(true);

        // Item icon
        if (Instance.itemImage != null)
        {
            Instance.itemImage.sprite          = item.icon;
            Instance.itemImage.enabled         = (item.icon != null);
            Instance.itemImage.color           = Color.white;
            Instance.itemImage.preserveAspect  = true;
        }

        // Item name (large, bold black text)
        if (Instance.itemNameText != null)
        {
            Instance.itemNameText.text = item.itemName;
            Instance.itemNameText.color = Color.black;
            Instance.itemNameText.fontStyle = FontStyle.Bold;
        }

        // Item description body (bold black text)
        if (Instance.itemDescriptionText != null)
        {
            Instance.itemDescriptionText.text = item.description;
            Instance.itemDescriptionText.color = Color.black;
            Instance.itemDescriptionText.fontStyle = FontStyle.Bold;
        }
    }

    /// <summary>Hide the item preview panel and clear all preview image and text content.</summary>
    public static void HidePreview()
    {
        if (Instance == null) return;

        if (Instance.itemImage != null)
        {
            Instance.itemImage.sprite  = null;
            Instance.itemImage.enabled = false;
        }

        if (Instance.itemNameText != null)
            Instance.itemNameText.text = "";

        if (Instance.itemDescriptionText != null)
            Instance.itemDescriptionText.text = "";

        if (Instance.inventoryDescription != null)
            Instance.inventoryDescription.SetActive(false);
    }
}
