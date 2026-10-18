using System.Collections.Generic;
using System.IO;
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
    [SerializeField] private Text slotNumberText;

    // Optional: assign the slot's background Image for hover/active highlight
    [SerializeField] private Image slotBackground;

    [Header("Border Color Styling")]
    [SerializeField] private Color normalBorderColor = new Color(0.85f, 0.85f, 0.90f, 0.85f);
    [SerializeField] private Color hoverBorderColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color activeBorderColor = new Color(1f, 0.85f, 0.35f, 1f);

    private InventorySlot boundSlot;
    private ISlotOwner owner;
    private CanvasGroup canvasGroup;
    private bool isEquippedActive = false;

    // Time-based double-click (more reliable than eventData.clickCount)
    private float lastClickTime = -99f;
    private const float DoubleClickThreshold = 0.35f;

    // Public read-only access so InventoryDragHandler can query these
    public InventorySlot BoundSlot => boundSlot;
    public ISlotOwner    Owner     => owner;

    // ─── Unity Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        // CanvasGroup is used to fade & block raycasts during drag
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (iconImage == null)
        {
            var iconT = transform.Find("Icon") ?? transform.Find("ItemIcon");
            if (iconT != null) iconImage = iconT.GetComponent<Image>();
        }

        if (iconImage != null)
            iconImage.preserveAspect = true;

        if (slotBackground != null)
            slotBackground.color = normalBorderColor;
    }

    public void SetActiveHighlight(bool isActive)
    {
        isEquippedActive = isActive;
        if (slotBackground != null)
        {
            slotBackground.color = isActive ? activeBorderColor : normalBorderColor;
        }
    }

    public void SetSlotNumber(string number)
    {
        if (slotNumberText != null)
            slotNumberText.text = number;
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
            if (iconImage != null) iconImage.enabled = false;
            if (quantityText != null) quantityText.text = "";
            return;
        }

        Sprite icon = ItemIconRegistry.GetOrResolveIcon(boundSlot.item);
        if (icon != null)
        {
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite  = icon;
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite  = null;
            }
        }

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
        if (slotBackground != null && !isEquippedActive)
            slotBackground.color = hoverBorderColor;

        SlotMenu.Instance?.PingVisibility();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (slotBackground != null && !isEquippedActive)
            slotBackground.color = normalBorderColor;
    }
}

/// <summary>
/// ItemIconRegistry — Automatically resolves and provides high-quality inventory icons for every item.
/// Ensures that items like Counseling Audio Cassette Tape, Ceramic Fuse, Guard Logbook, Medical Slip,
/// Coins, and Cables always have crisp, dedicated, thematic icons in their inventory slots.
/// </summary>
public static class ItemIconRegistry
{
    private static readonly Dictionary<string, Sprite> _resolvedIcons = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

    public static Sprite GetOrResolveIcon(InventoryItem item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;

        string key = !string.IsNullOrEmpty(item.itemName) ? item.itemName : item.name;
        if (_resolvedIcons.TryGetValue(key, out Sprite cached) && cached != null)
        {
            item.icon = cached;
            return cached;
        }

        Sprite resolved = TryResolveDiskIcon(key);
        if (resolved == null)
        {
            resolved = GenerateProceduralIcon(key, item.itemType);
        }

        if (resolved != null)
        {
            _resolvedIcons[key] = resolved;
            item.icon = resolved;
        }

        return resolved;
    }

    private static Sprite TryResolveDiskIcon(string itemName)
    {
        string lower = itemName.ToLowerInvariant();
        string assetPath = null;

        if (lower.Contains("counseling") || lower.Contains("cassette") || lower.Contains("tape") || lower.Contains("audio"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_CassetteTape_A.png";
        }
        else if (lower.Contains("fuse") || lower.Contains("ceramic"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_CeramicFuse.png";
        }
        else if (lower.Contains("guard") || lower.Contains("logbook") || lower.Contains("security log"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_GuardLogbook.png";
        }
        else if (lower.Contains("medical") || lower.Contains("clinic"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_MedicalSlip.png";
        }
        else if (lower.Contains("coin") || lower.Contains("peso"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_PesoCoin.png";
        }
        else if (lower.Contains("vga") || lower.Contains("cable") || lower.Contains("cord"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_VGACable.png";
        }
        else if (lower.Contains("lockpin") || lower.Contains("lockpick") || lower.Contains("pin"))
        {
            assetPath = "Assets/Textures/ItemInspectTextures/Tex_KeyTag.png";
        }
        else if (lower.Contains("clearance") || lower.Contains("slip") || lower.Contains("blank paper"))
        {
            assetPath = "Assets/PlayerAssets/signed0.png";
        }

        if (assetPath != null)
        {
            Sprite s = LoadDiskSprite(assetPath);
            if (s != null) return s;
        }

        return null;
    }

    private static Sprite LoadDiskSprite(string path)
    {
#if UNITY_EDITOR
        Sprite edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (edSprite != null) return edSprite;
#endif
        if (File.Exists(path))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemIconRegistry] Failed to load sprite at '{path}': {ex.Message}");
            }
        }
        return null;
    }

    private static Sprite GenerateProceduralIcon(string name, InventoryItem.ItemType type)
    {
        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        Color baseCol = new Color(0.25f, 0.28f, 0.35f, 0.95f);
        Color borderCol = new Color(0.85f, 0.75f, 0.40f, 1f);

        if (type == InventoryItem.ItemType.Tool) baseCol = new Color(0.35f, 0.30f, 0.20f, 0.95f);
        else if (type == InventoryItem.ItemType.KeyItem) baseCol = new Color(0.40f, 0.20f, 0.22f, 0.95f);
        else if (type == InventoryItem.ItemType.Document) baseCol = new Color(0.20f, 0.30f, 0.40f, 0.95f);

        // Draw rounded icon border & fill
        for (int y = 14; y < size - 14; y++)
        {
            for (int x = 14; x < size - 14; x++)
            {
                bool isBorder = (x <= 16 || x >= size - 16 || y <= 16 || y >= size - 16);
                tex.SetPixel(x, y, isBorder ? borderCol : baseCol);
            }
        }

        // Draw inner accent stripes
        for (int y = 30; y < size - 30; y += 12)
        {
            for (int x = 24; x < size - 24; x++)
            {
                tex.SetPixel(x, y, new Color(borderCol.r, borderCol.g, borderCol.b, 0.5f));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
