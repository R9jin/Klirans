using UnityEngine;

/// <summary>
/// Inventory Hotkey Equipment Controller
/// Listens for number keys 1 through 6 to equip items from the 6-slot hotbar.
/// Handles:
///   - Instantiating/viewing the full Blank_Paper clearance slip in front of the camera.
///   - Equipping the Flashlight (single model toggle + Left Click light control).
///   - Suppressing viewmodels for individual fragment pieces.
/// </summary>
public class InventoryEquipController : MonoBehaviour
{
    public static InventoryEquipController Instance { get; private set; }

    [Header("Camera & Equip Point Setup")]
    [Tooltip("Transform childed to Main Camera defining where equipped viewmodels sit (e.g. X:0.3, Y:-0.2, Z:0.5).")]
    public Transform equipPoint;

    [Header("Equippable Item References")]
    [Tooltip("ScriptableObject item data for Blank_Paper.")]
    public InventoryItem blankPaperItemData;

    [Tooltip("The 3D model/prefab of Blank_Paper shown in view when equipped.")]
    public GameObject heldPaperModelPrefab;

    [Tooltip("ScriptableObject item data for Flashlight.")]
    public InventoryItem flashlightItemData;

    [Header("Viewmodel Motion & Animation Settings")]
    [Tooltip("Speed at which held viewmodel smoothly lerps into position/rotation at EquipPoint.")]
    public float equipSmoothSpeed = 12f;

    [Header("Item Dropping")]
    [Tooltip("Key to drop the currently selected hotbar item onto the ground.")]
    public KeyCode dropKey = KeyCode.G;

    [Tooltip("How far in front of the player the dropped item spawns (metres).")]
    public float dropForwardOffset = 0.8f;

    [Tooltip("Upward force applied to dropped items so they arc slightly before landing.")]
    public float dropUpwardForce = 1.5f;

    [Tooltip("Forward force applied to dropped items.")]
    public float dropForwardForce = 2.0f;

    // Currently active hotkey slot index (-1 if none equipped)
    private int activeSlotIndex = -1;

    // Active instantiated viewmodel instance (e.g. Blank Paper)
    private GameObject currentEquippedInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Auto-find EquipPoint if not assigned in Inspector
        if (equipPoint == null)
        {
            Camera mainCam = Camera.main ?? GetComponentInChildren<Camera>();
            if (mainCam != null)
            {
                Transform foundPoint = mainCam.transform.Find("EquipPoint");
                if (foundPoint != null)
                {
                    equipPoint = foundPoint;
                }
                else
                {
                    GameObject newPoint = new GameObject("EquipPoint");
                    newPoint.transform.SetParent(mainCam.transform, false);
                    newPoint.transform.localPosition = new Vector3(0f, -0.05f, 0.42f);
                    newPoint.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    equipPoint = newPoint.transform;
                }
            }
        }

        // Ensure no viewmodel is active at game start
        UnequipCurrentItem();
        if (equipPoint != null)
        {
            foreach (Transform child in equipPoint)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void Update()
    {
        HandleHotkeyInput();
        HandleDropInput();
        UpdateViewmodelPosition();
    }

    /// <summary>
    /// Listens for Alphanumeric keys 1 through 6.
    /// </summary>
    private void HandleHotkeyInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectSlot(4);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) SelectSlot(5);

        // Mouse scroll wheel slot cycling (forward/backward through 6 slots)
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.05f)
        {
            int current = (activeSlotIndex >= 0) ? activeSlotIndex : 0;
            int next = (scroll < 0f) ? (current + 1) % 6 : (current - 1 + 6) % 6;
            SelectSlot(next);
        }
    }

    /// <summary>
    /// Selects or toggles an inventory slot by index (0-5).
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        InventoryManager inventory = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (inventory == null)
        {
            Debug.LogWarning("[InventoryEquipController] InventoryManager instance not found!");
            return;
        }

        // Toggling off if pressing the same key again
        if (activeSlotIndex == slotIndex)
        {
            UnequipCurrentItem();
            SlotMenu.Instance?.UpdateActiveSlotHighlight(-1);
            return;
        }

        inventory.EnsureSlotsInitialized();
        var slots = inventory.Slots;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        InventorySlot targetSlot = slots[slotIndex];

        if (targetSlot.IsEmpty || targetSlot.item == null)
        {
            // Selected empty slot -> Unequip current item
            UnequipCurrentItem();
            activeSlotIndex = slotIndex;
            SlotMenu.Instance?.UpdateActiveSlotHighlight(activeSlotIndex);
            Debug.Log($"[InventoryEquipController] Selected empty slot {slotIndex + 1}.");
            return;
        }

        // Slot contains item -> Equip item
        EquipItem(targetSlot.item);
        activeSlotIndex = slotIndex;
        SlotMenu.Instance?.UpdateActiveSlotHighlight(activeSlotIndex);
    }

    /// <summary>
    /// Equips the specified item data.
    /// Spawns 3D viewmodel ONLY for completed Blank_Paper, and toggles Flashlight for Flashlight item.
    /// Individual fragment items spawn NO 3D viewmodel.
    /// </summary>
    public void EquipItem(InventoryItem item)
    {
        UnequipCurrentItem();

        if (item == null) return;

        Debug.Log($"[InventoryEquipController] Equipping item: {item.itemName}");

        string itemNameLower = item.itemName.ToLower();

        // 1. Flashlight Handling
        if (itemNameLower.Contains("flashlight") || (flashlightItemData != null && item == flashlightItemData))
        {
            FlashlightController flashlight = FlashlightController.Instance ?? FindAnyObjectByType<FlashlightController>(FindObjectsInactive.Include);
            if (flashlight != null)
            {
                flashlight.SetEquippedState(true);
            }
            return;
        }

        // 2. Spawn 3D viewmodel for items with an itemPrefab or completed Blank_Paper
        bool isCompletedPaper = (blankPaperItemData != null && item == blankPaperItemData) ||
                                (itemNameLower.Contains("blank") && itemNameLower.Contains("paper"));

        GameObject prefabToSpawn = item.itemPrefab;
        if (prefabToSpawn == null && isCompletedPaper && heldPaperModelPrefab != null)
            prefabToSpawn = heldPaperModelPrefab;

        if (prefabToSpawn != null && equipPoint != null)
        {
            currentEquippedInstance = Instantiate(prefabToSpawn, equipPoint.position, equipPoint.rotation, equipPoint);

            // Strip PickupItem scripts and Colliders on held viewmodels
            PickupItem[] pickups = currentEquippedInstance.GetComponentsInChildren<PickupItem>(true);
            foreach (var p in pickups)
            {
                if (p != null) Destroy(p);
            }

            Collider[] colliders = currentEquippedInstance.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null) Destroy(col);
            }

            Rigidbody rb = currentEquippedInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            currentEquippedInstance.transform.localPosition = Vector3.zero;
            currentEquippedInstance.transform.localRotation = Quaternion.identity;
            currentEquippedInstance.SetActive(true);
        }
    }

    /// <summary>
    /// Destroys the active viewmodel instance and stows active equipment/flashlight.
    /// </summary>
    public void UnequipCurrentItem()
    {
        if (currentEquippedInstance != null)
        {
            currentEquippedInstance.SetActive(false);
            if (Application.isPlaying) Destroy(currentEquippedInstance);
            else DestroyImmediate(currentEquippedInstance);
            currentEquippedInstance = null;
        }

        if (equipPoint != null)
        {
            for (int i = equipPoint.childCount - 1; i >= 0; i--)
            {
                Transform child = equipPoint.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // Stow flashlight & turn off light
        FlashlightController flashlight = FlashlightController.Instance ?? FindAnyObjectByType<FlashlightController>(FindObjectsInactive.Include);
        if (flashlight != null)
        {
            flashlight.SetEquippedState(false);
        }

        activeSlotIndex = -1;
        SlotMenu.Instance?.UpdateActiveSlotHighlight(-1);
    }

    /// <summary>
    /// Smoothly updates position of held item to track EquipPoint.
    /// </summary>
    private void UpdateViewmodelPosition()
    {
        if (currentEquippedInstance == null || equipPoint == null) return;

        currentEquippedInstance.transform.position = Vector3.Lerp(
            currentEquippedInstance.transform.position,
            equipPoint.position,
            Time.deltaTime * equipSmoothSpeed
        );

        currentEquippedInstance.transform.rotation = Quaternion.Slerp(
            currentEquippedInstance.transform.rotation,
            equipPoint.rotation,
            Time.deltaTime * equipSmoothSpeed
        );
    }

    // ── Item Dropping ─────────────────────────────────────────────────────────

    /// <summary>
    /// Checks for the drop key every frame and drops the active hotbar item.
    /// Suppressed while paused, in dialogue, or inside a locker.
    /// </summary>
    private void HandleDropInput()
    {
        if (!Input.GetKeyDown(dropKey) && !Input.GetKeyDown(KeyCode.Q)) return;
        if (PauseMenu.GameIsPaused) return;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return;
        if (LockerHideManager.IsPlayerHidden) return;

        // Need an actively selected slot
        if (activeSlotIndex < 0) return;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null) return;

        inventory.EnsureSlotsInitialized();
        if (activeSlotIndex >= inventory.Slots.Count) return;

        InventorySlot slot = inventory.Slots[activeSlotIndex];
        if (slot == null || slot.IsEmpty || slot.item == null) return;

        InventoryItem item = slot.item;

        // Respect the quest-item flag
        if (!item.canBeDropped)
        {
            Debug.Log($"[InventoryEquipController] {item.itemName} cannot be dropped (quest item).");
            return;
        }

        DropActiveItem(item, slot);
    }

    /// <summary>
    /// Removes the item from the inventory, unequips it, and spawns its world prefab
    /// in front of the player with Rigidbody physics and floor failsafe so it arcs and lands naturally.
    /// Guarantees that every droppable item can always be picked up again with 'E'.
    /// </summary>
    private void DropActiveItem(InventoryItem item, InventorySlot slot)
    {
        // Unequip first so the viewmodel disappears cleanly
        UnequipCurrentItem();

        // Remove copy from inventory (or the entire stack if > 1)
        int qtyToDrop = slot.quantity;
        InventoryManager.Instance.RemoveItem(item, qtyToDrop);

        SlotMenu.Instance?.PingVisibility();

        // Determine spawn position: slightly in front and at waist height
        Camera cam = Camera.main;
        if (cam == null) cam = GetComponentInChildren<Camera>();

        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Detect floor height below player to ensure item never spawns below floor level
        float floorY = transform.position.y;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit floorHit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            floorY = floorHit.point.y;
        }

        Vector3 spawnPos = transform.position + forward * dropForwardOffset;
        spawnPos.y = Mathf.Max(transform.position.y + 0.4f, floorY + 0.35f);

        GameObject dropped = null;

        // If the item has a world prefab, spawn it
        if (item.itemPrefab != null)
        {
            dropped = Instantiate(item.itemPrefab, spawnPos, Random.rotation);
        }
        else
        {
            // Fallback for items with no world prefab (e.g. Blank_Paper, Voucher, Printer_Paper)
            dropped = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dropped.name = $"{item.itemName}_Dropped";
            dropped.transform.position = spawnPos;
            dropped.transform.rotation = Random.rotation;
            dropped.transform.localScale = new Vector3(0.25f, 0.05f, 0.35f);

            var mr = dropped.GetComponent<MeshRenderer>();
            if (mr != null && item.icon != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.mainTexture = item.icon.texture;
                mr.sharedMaterial = mat;
            }
        }

        if (dropped != null)
        {
            // Remove broken child MeshColliders or ensure convex
            var meshCols = dropped.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in meshCols)
            {
                if (mc.sharedMesh == null)
                {
                    Destroy(mc);
                }
                else
                {
                    mc.convex = true;
                }
            }

            // Ensure a solid primitive collider on root
            var rootCol = dropped.GetComponent<Collider>();
            if (rootCol == null)
            {
                var sphere = dropped.AddComponent<SphereCollider>();
                sphere.radius = 0.16f;
            }

            // Rigidbody with continuous collision detection
            Rigidbody rb = dropped.GetComponent<Rigidbody>();
            if (rb == null) rb = dropped.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Apply a gentle arc throw
            rb.linearVelocity = forward * dropForwardForce + Vector3.up * dropUpwardForce;
            rb.angularVelocity = Random.insideUnitSphere * 3f;

            // Attach DroppedItemFailsafe to guarantee it never drops through the map
            var failsafe = dropped.GetComponent<DroppedItemFailsafe>();
            if (failsafe == null) failsafe = dropped.AddComponent<DroppedItemFailsafe>();
            failsafe.Initialize(floorY);

            // Strip any rogue child PickupItems so only one authoritative PickupItem exists on root
            var childPickups = dropped.GetComponentsInChildren<PickupItem>(true);
            foreach (var cp in childPickups)
            {
                if (cp.gameObject != dropped) Destroy(cp);
            }

            // Re-attach / configure PickupItem on the root so it can ALWAYS be picked back up with 'E'
            PickupItem pickup = dropped.GetComponent<PickupItem>();
            if (pickup == null) pickup = dropped.AddComponent<PickupItem>();
            pickup.itemData = item;
            pickup.amount = qtyToDrop;

            // Ensure layer is Default for PlayerInteract raycasting
            dropped.layer = LayerMask.NameToLayer("Default");
            foreach (Transform c in dropped.transform)
            {
                c.gameObject.layer = LayerMask.NameToLayer("Default");
            }

            Debug.Log($"[InventoryEquipController] Dropped {item.itemName} x{qtyToDrop} safely at {spawnPos}.");
        }
    }

    public int GetActiveSlotIndex() => activeSlotIndex;
}
