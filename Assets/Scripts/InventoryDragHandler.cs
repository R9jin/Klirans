using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Singleton that manages drag-and-drop between ALL inventory slots.
/// Works across containers: hotbar (6 slots) <-> bag (12 slots).
///
/// Key behaviour notes:
///   - Unity fires OnEndDrag BEFORE OnDrop each frame.
///   - We keep sourceSlotUI alive through EndDrag so Drop() can still use it.
///   - A one-frame cleanup coroutine wipes sourceSlotUI only when no drop occurred.
///
/// Auto-creates itself at runtime — no manual scene setup needed.
/// </summary>
public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    private Image         ghostIcon;
    private RectTransform ghostRect;

    private SlotUI sourceSlotUI;
    private bool   dropHandled;     // set true by Drop() in the same frame as EndDrag()

    // ─── Auto-create ─────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
            new GameObject("InventoryDragHandler").AddComponent<InventoryDragHandler>();
    }

    // ─── Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BuildGhostCanvas();
    }

    private void BuildGhostCanvas()
    {
        // Dedicated overlay canvas (sortOrder 999) so ghost is always on top
        var cvGO          = new GameObject("_DragGhostCanvas");
        var cv            = cvGO.AddComponent<Canvas>();
        cv.renderMode     = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder   = 999;
        cvGO.AddComponent<CanvasScaler>();
        // No GraphicRaycaster — ghost must NEVER intercept click or drop events
        DontDestroyOnLoad(cvGO);

        var go            = new GameObject("GhostImage");
        go.transform.SetParent(cvGO.transform, false);

        ghostIcon                 = go.AddComponent<Image>();
        ghostIcon.raycastTarget   = false;
        ghostIcon.preserveAspect  = true;
        ghostIcon.color           = new Color(1f, 1f, 1f, 0.85f);

        ghostRect             = ghostIcon.rectTransform;
        ghostRect.sizeDelta   = new Vector2(60f, 60f);
        ghostRect.anchorMin   = Vector2.zero;
        ghostRect.anchorMax   = Vector2.zero;
        ghostRect.pivot       = new Vector2(0.5f, 0.5f);

        var cg            = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        go.SetActive(false);
    }

    // ─── Public API called by SlotUI ─────────────────────────────────

    public void BeginDrag(SlotUI source, PointerEventData eventData)
    {
        if (source?.BoundSlot == null || source.BoundSlot.IsEmpty) return;

        sourceSlotUI = source;
        dropHandled  = false;

        Sprite icon = source.BoundSlot.item?.icon;
        ghostIcon.sprite = icon;
        ghostIcon.color  = icon != null
            ? new Color(1f, 1f, 1f, 0.85f)
            : new Color(0.7f, 0.7f, 0.7f, 0.7f);

        ghostIcon.gameObject.SetActive(true);
        MoveGhost(eventData.position);
    }

    public void UpdateDrag(PointerEventData eventData)
    {
        // Keep moving the ghost even after isDragging clears (same-frame safety)
        if (ghostIcon != null && ghostIcon.gameObject.activeSelf)
            MoveGhost(eventData.position);
    }

    /// <summary>
    /// OnEndDrag fires BEFORE OnDrop.  Do NOT clear sourceSlotUI here.
    /// Just hide the ghost and schedule a cleanup for "dropped on nothing".
    /// </summary>
    public void EndDrag()
    {
        if (ghostIcon != null)
            ghostIcon.gameObject.SetActive(false);

        // Give Drop() a chance to fire this same frame before we clean up
        StartCoroutine(CleanupAfterFrame());
    }

    private System.Collections.IEnumerator CleanupAfterFrame()
    {
        yield return null;              // One-frame wait
        if (!dropHandled)               // Drop() was never called → dropped on nothing
            sourceSlotUI = null;
        dropHandled = false;            // Reset for next drag
    }

    /// <summary>
    /// Called by the TARGET SlotUI.OnDrop.
    /// Swaps item data and notifies both containers.
    /// Cross-container swaps (hotbar <-> bag) are fully supported.
    /// </summary>
    public void Drop(SlotUI target)
    {
        dropHandled = true;             // Prevent CleanupAfterFrame from wiping sourceSlotUI

        if (sourceSlotUI == null || target == null) return;
        if (sourceSlotUI == target) { sourceSlotUI = null; return; }

        InventorySlot src = sourceSlotUI.BoundSlot;
        InventorySlot dst = target.BoundSlot;

        if (src == null || dst == null) { sourceSlotUI = null; return; }

        // ── Swap item data directly ─────────────────────────────────
        // InventorySlot is a class — mutating .item/.quantity on the existing
        // objects updates the container list AND the UI reference simultaneously.
        InventoryItem tmpItem = dst.item;
        int           tmpQty  = dst.quantity;

        dst.item     = src.item;
        dst.quantity = src.quantity;
        src.item     = tmpItem;
        src.quantity = tmpQty;

        // ── Fire change events so both UIs refresh ──────────────────
        SlotContainer srcCont = sourceSlotUI.Owner?.GetContainer();
        SlotContainer dstCont = target.Owner?.GetContainer();

        srcCont?.NotifyChanged();
        if (dstCont != null && dstCont != srcCont)
            dstCont.NotifyChanged();

        sourceSlotUI = null;
    }

    // ─── Internal ─────────────────────────────────────────────────────

    private void MoveGhost(Vector2 screenPos)
    {
        if (ghostRect != null)
            ghostRect.position = screenPos;
    }
}
