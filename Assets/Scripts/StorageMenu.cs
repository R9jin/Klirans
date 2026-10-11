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
                    fontSize: 18,
                    fontStyle: FontStyle.Bold,
                    alignment: TextAnchor.UpperLeft,
                    color: Color.black,
                    anchorMin: new Vector2(0f, 0.18f),
                    anchorMax: new Vector2(1f, 0.75f),
                    offsetMin: new Vector2(16f, 5f),
                    offsetMax: new Vector2(-16f, -5f));
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.fontSize = 18;
            itemDescriptionText.fontStyle = FontStyle.Bold;
            itemDescriptionText.color = Color.black;
            var rt = itemDescriptionText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.18f);
            rt.anchorMax = new Vector2(1f, 0.75f);
        }

        // 3. Create or find Inspect Button and Drop Button
        if (_inspectButton == null)
        {
            Transform existing = descPanel.Find("InspectItemButton");
            if (existing != null)
            {
                _inspectButton = existing.GetComponent<Button>();
            }
            else
            {
                _inspectButton = CreateButton(descPanel, "InspectItemButton", "Inspect / Read",
                    anchorMin: new Vector2(0.04f, 0.02f), anchorMax: new Vector2(0.48f, 0.16f),
                    bgColor: new Color(0.18f, 0.14f, 0.10f, 0.95f),
                    textColor: new Color(0.96f, 0.93f, 0.86f, 1f),
                    outlineColor: new Color(0.85f, 0.70f, 0.35f, 0.75f));
                _inspectButton.onClick.AddListener(InspectSelectedItem);
            }
        }

        if (_dropButton == null)
        {
            Transform existing = descPanel.Find("DropItemButton");
            if (existing != null)
            {
                _dropButton = existing.GetComponent<Button>();
                _dropButtonText = existing.GetComponentInChildren<Text>();
            }
            else
            {
                _dropButton = CreateButton(descPanel, "DropItemButton", "Drop Item",
                    anchorMin: new Vector2(0.52f, 0.02f), anchorMax: new Vector2(0.96f, 0.16f),
                    bgColor: new Color(0.24f, 0.12f, 0.12f, 0.95f),
                    textColor: new Color(1f, 0.92f, 0.92f, 1f),
                    outlineColor: new Color(0.85f, 0.25f, 0.25f, 0.75f));
                _dropButtonText = _dropButton.GetComponentInChildren<Text>();
                _dropButton.onClick.AddListener(DropSelectedItem);
            }
        }
    }

    private Button CreateButton(Transform parent, string goName, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        Color bgColor, Color textColor, Color outlineColor)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = bgColor;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var textGO = new GameObject("BtnText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;

        var txt = textGO.GetComponent<Text>();
        txt.text = text;
        txt.fontSize = 15;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.raycastTarget = false;

        Font builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.font = builtIn;

        return go.GetComponent<Button>();
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

    // ─── Button Actions ──────────────────────────────────────────────

    private void InspectSelectedItem()
    {
        if (_selectedItem == null) return;
        ItemInspectionModal.Inspect(_selectedItem);
    }

    private void DropSelectedItem()
    {
        if (_selectedSlot == null || _selectedSlot.IsEmpty || _selectedSlot.item == null) return;

        if (!_selectedSlot.item.canBeDropped)
        {
            Debug.Log($"[StorageMenu] Cannot drop {_selectedSlot.item.itemName} (quest item).");
            return;
        }

        InventoryItem itemToDrop = _selectedSlot.item;
        _selectedSlot.quantity -= 1;
        if (_selectedSlot.quantity <= 0)
        {
            _selectedSlot.Clear();
        }

        _selectedContainer?.NotifyChanged();

        if (InventoryEquipController.Instance != null)
        {
            InventoryEquipController.Instance.SpawnDroppedItemInWorld(itemToDrop, 1);
        }

        if (_selectedSlot.IsEmpty)
        {
            HidePreview();
        }
        else
        {
            ShowPreview(_selectedSlot.item, _selectedSlot, _selectedContainer);
        }
    }

    // ─── ISlotOwner ──────────────────────────────────────────────────

    /// <summary>Returns the backing data container for drag-and-drop.</summary>
    public SlotContainer GetContainer() => StorageManager.Instance;

    /// <summary>Single click on a bag slot -> show item preview.</summary>
    public void OnSlotClicked(InventorySlot slot)
    {
        if (slot == null || slot.IsEmpty)
        {
            HidePreview();
            return;
        }
        ShowPreview(slot.item, slot, StorageManager.Instance);
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
            HidePreview();
        }
    }

    /// <summary>Click on empty bag slot -> hide preview.</summary>
    public void OnSlotDeselected()
    {
        HidePreview();
    }

    // ─── Static Preview API ──────────────────────────────────────────

    private Button _inspectButton;
    private Button _dropButton;
    private Text _dropButtonText;
    private InventoryItem _selectedItem;
    private InventorySlot _selectedSlot;
    private SlotContainer _selectedContainer;

    /// <summary>
    /// Show an item's icon, description, and action buttons in the preview panel.
    /// Called by both StorageMenu.OnSlotClicked and SlotMenu.OnSlotClicked.
    /// </summary>
    public static void ShowPreview(InventoryItem item, InventorySlot slot = null, SlotContainer container = null)
    {
        if (Instance == null || item == null) return;
        if (Instance.inventoryDescription == null) return;

        Instance._selectedItem = item;
        Instance._selectedSlot = slot;
        Instance._selectedContainer = container;

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

        // Action Buttons
        if (Instance._inspectButton != null)
        {
            bool canInspect = item.isInspectable || !string.IsNullOrEmpty(item.inspectionText);
            Instance._inspectButton.gameObject.SetActive(canInspect);
        }

        if (Instance._dropButton != null)
        {
            Instance._dropButton.gameObject.SetActive(slot != null);
            Instance._dropButton.interactable = item.canBeDropped;
            if (Instance._dropButtonText != null)
            {
                Instance._dropButtonText.text = item.canBeDropped ? "Drop Item" : "Quest Item";
            }
        }
    }

    /// <summary>Hide the item preview panel and clear all preview image and text content.</summary>
    public static void HidePreview()
    {
        if (Instance == null) return;

        Instance._selectedItem = null;
        Instance._selectedSlot = null;
        Instance._selectedContainer = null;

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
