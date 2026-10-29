using UnityEngine;

using UnityEngine.UI;



/// <summary>

/// Inventory Hotkey Equipment Controller

/// Listens for number keys 1 through 6 to equip items from the 6-slot hotbar.

/// Handles:

///   - Instantiating/viewing the full Blank_Paper clearance slip in front of the camera.

///   - Equipping the Flashlight (single model toggle + Left Click / F light control).

///   - Suppressing viewmodels for individual fragment pieces.

///   - Displaying on-screen paper HUD controls for the held item (Toggle, Drop [G], Inspect [V], Tutorial [H]).

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

    public float dropForwardOffset = 0.40f;



    [Tooltip("Upward force applied to dropped items so they arc slightly before landing.")]

    public float dropUpwardForce = 0.20f;



    [Tooltip("Forward force applied to dropped items.")]

    public float dropForwardForce = 0.45f;



    // Currently active hotkey slot index (-1 if none equipped)

    private int activeSlotIndex = -1;



    // Active instantiated viewmodel instance (e.g. Blank Paper)

    private GameObject currentEquippedInstance;



    // On-screen Held Item Controls HUD Bar

    private GameObject _controlsHudCanvasGO;

    private GameObject _controlsHudBox;

    private Text _controlsHudText;



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

                    equipPoint = newPoint.transform;

                }

            }

        }



        // Configure Right-Hand held position (lower right quadrant, angled inward naturally)

        if (equipPoint != null)

        {

            equipPoint.localPosition = new Vector3(0.28f, -0.22f, 0.44f);

            equipPoint.localRotation = Quaternion.Euler(5f, -14f, 4f);

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



        BuildControlsHudBar();

    }



    private void Update()

    {
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive)
            return;


        ValidateActiveEquippedSlot();

        HandleHotkeyInput();

        HandleItemUseInput();

        HandleDropInput();

        UpdateViewmodelPosition();

        UpdateControlsHudBar();

    }



    /// <summary>

    /// Validates that the active equipped slot still holds a valid item.

    /// If the item was dropped, swapped, or removed via UI, automatically unequips it to prevent phantom held items.

    /// Flashlight is handled independently in the left hand (offhand).

    /// </summary>

    public void ValidateActiveEquippedSlot()

    {

        InventoryManager inventory = InventoryManager.Instance;

        if (inventory == null) return;



        if (activeSlotIndex < 0) return;



        if (activeSlotIndex >= inventory.Slots.Count)

        {

            UnequipCurrentItem();

            return;

        }



        InventorySlot slot = inventory.Slots[activeSlotIndex];

        if (slot == null || slot.IsEmpty || slot.item == null)

        {

            UnequipCurrentItem();

        }

    }



    private int FindFlashlightSlotIndex(InventoryManager inventory)

    {

        if (inventory == null || inventory.Slots == null) return -1;

        for (int i = 0; i < inventory.Slots.Count; i++)

        {

            var s = inventory.Slots[i];

            if (s != null && !s.IsEmpty && s.item != null && s.item.itemName.ToLower().Contains("flashlight"))

            {

                return i;

            }

        }

        return -1;

    }



    /// <summary>

    /// Listens for Alphanumeric keys 1 through 6.

    /// </summary>

    private void HandleHotkeyInput()

    {

        if (PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen) return;



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

    /// Handles using or consuming the currently equipped item (e.g. eating Ham & Cheese Sandwich on Left Click or [F]).

    /// </summary>

    private void HandleItemUseInput()

    {

        if (activeSlotIndex < 0) return;

        if (PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen) return;



        InventoryManager inventory = InventoryManager.Instance;

        if (inventory == null || activeSlotIndex >= inventory.Slots.Count) return;



        InventorySlot slot = inventory.Slots[activeSlotIndex];

        if (slot == null || slot.IsEmpty || slot.item == null) return;



        // Press [V] (or custom Inspect keybind) to inspect currently equipped hotbar item in 3D

        if (GameSettingsManager.GetKeyDown(GameAction.InspectItem) && !ItemInspectionViewer3D.IsOpen)

        {

            ItemInspectionViewer3D.Inspect(slot.item);

            return;

        }



        // Check if held item is Ham & Cheese Sandwich

        string nameLower = slot.item.itemName.ToLower();

        if (nameLower.Contains("ham") && nameLower.Contains("cheese"))

        {

            if (!DevDemoPanel.IsOpen && (Input.GetMouseButtonDown(0) || GameSettingsManager.GetKeyDown(GameAction.Flashlight)))

            {

                Debug.Log("[InventoryEquipController] Player consumed held Ham & Cheese Sandwich!");

                HamAndCheeseItem.Consume(30f, 25f);

                inventory.RemoveItem(slot.item, 1);

                SlotMenu.Instance?.PingVisibility();

                if (slot.IsEmpty) UnequipCurrentItem();

            }

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



        // 1. Flashlight Handling — Held in left hand (offhand); right hand is free

        if (itemNameLower.Contains("flashlight") || (flashlightItemData != null && item == flashlightItemData))

        {

            // FlashlightController automatically renders the flashlight in the left hand

            return;

        }



        // 2. Spawn 3D viewmodel for items with an itemPrefab or completed Blank_Paper

        bool isCompletedPaper = (blankPaperItemData != null && item == blankPaperItemData) ||

                                (itemNameLower.Contains("blank") && itemNameLower.Contains("paper"));



        GameObject prefabToSpawn = item.itemPrefab;

        if (prefabToSpawn == null && isCompletedPaper && heldPaperModelPrefab != null)

            prefabToSpawn = heldPaperModelPrefab;



#if UNITY_EDITOR

        if (prefabToSpawn == null)

        {

            if (itemNameLower.Contains("lockpin") || itemNameLower.Contains("lockpick"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_Lockpin.prefab");

            else if (itemNameLower.Contains("cassette") || itemNameLower.Contains("tape"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_CassetteTape.prefab");

            else if (itemNameLower.Contains("fuse"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_CeramicFuse.prefab");

            else if (itemNameLower.Contains("handbook"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_StudentHandbook.prefab");

            else if (itemNameLower.Contains("voucher"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_LibraryVoucher.prefab");

            else if (itemNameLower.Contains("comlab"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_ComLabClearanceForm.prefab");

            else if (itemNameLower.Contains("seal"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_ExecutiveSealStamp.prefab");

            else if (itemNameLower.Contains("key"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_LockerKey.prefab");

            else if (itemNameLower.Contains("coin") || itemNameLower.Contains("peso"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_Coin.prefab");

            else if (itemNameLower.Contains("vga") || itemNameLower.Contains("cable"))

                prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_VGACable.prefab");

        }

#endif



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



            // Strip ItemVisualHighlight components, glint lights, and any Light from held viewmodel

            ItemVisualHighlight[] highlights = currentEquippedInstance.GetComponentsInChildren<ItemVisualHighlight>(true);

            foreach (var h in highlights)

            {

                if (h != null) Destroy(h);

            }



            Light[] strayLights = currentEquippedInstance.GetComponentsInChildren<Light>(true);

            foreach (var lt in strayLights)

            {

                if (lt != null)

                {

                    lt.enabled = false;

                    Destroy(lt);

                }

            }



            foreach (Transform child in currentEquippedInstance.GetComponentsInChildren<Transform>(true))

            {

                if (child != null && (child.name.Contains("ItemGlint") || child.name.Contains("Glint")))

                {

                    Destroy(child.gameObject);

                }

            }



            Rigidbody rb = currentEquippedInstance.GetComponent<Rigidbody>();

            if (rb != null)

            {

                rb.isKinematic = true;

            }



            // Right-hand ergonomic held orientation

            if (isCompletedPaper)

            {

                currentEquippedInstance.transform.localPosition = new Vector3(0.02f, 0.02f, 0.04f);

                currentEquippedInstance.transform.localRotation = Quaternion.Euler(15f, -12f, 10f);

                currentEquippedInstance.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);

            }

            else if (itemNameLower.Contains("lockpin") || itemNameLower.Contains("lockpick"))

            {

                currentEquippedInstance.transform.localPosition = new Vector3(0.04f, -0.04f, 0.08f);

                currentEquippedInstance.transform.localRotation = Quaternion.Euler(45f, 25f, -20f);

                currentEquippedInstance.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

            }

            else if (itemNameLower.Contains("handbook"))

            {

                currentEquippedInstance.transform.localPosition = new Vector3(0.02f, 0.01f, 0.04f);

                currentEquippedInstance.transform.localRotation = Quaternion.Euler(20f, -15f, 8f);

                currentEquippedInstance.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            }

            else if (itemNameLower.Contains("fuse"))

            {

                currentEquippedInstance.transform.localPosition = new Vector3(0.02f, -0.02f, 0.05f);

                currentEquippedInstance.transform.localRotation = Quaternion.Euler(30f, 20f, -10f);

                currentEquippedInstance.transform.localScale = new Vector3(0.40f, 0.40f, 0.40f);

            }

            else if (itemNameLower.Contains("cassette") || itemNameLower.Contains("tape"))

            {

                currentEquippedInstance.transform.localPosition = new Vector3(0.02f, -0.02f, 0.05f);

                currentEquippedInstance.transform.localRotation = Quaternion.Euler(25f, 15f, -5f);

                currentEquippedInstance.transform.localScale = new Vector3(0.40f, 0.40f, 0.40f);

            }

            else

            {

                currentEquippedInstance.transform.localPosition = Vector3.zero;

                currentEquippedInstance.transform.localRotation = Quaternion.identity;

            }



            currentEquippedInstance.SetActive(!LockerHideManager.IsPlayerHidden);



            if (isCompletedPaper)

            {

                Texture slipTex = ClearanceManager.Instance != null ? ClearanceManager.Instance.GetCurrentSlipTexture() : null;

#if UNITY_EDITOR

                if (slipTex == null)

                    slipTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PlayerAssets/signed0.png");

#endif

                RefreshEquippedClearanceSlipTexture(slipTex);

            }

        }

    }



    public void RefreshEquippedClearanceSlipTexture(Texture targetTex)

    {

        if (currentEquippedInstance == null || targetTex == null) return;

        Renderer[] rends = currentEquippedInstance.GetComponentsInChildren<Renderer>(true);

        foreach (var r in rends)

        {

            if (r == null) continue;

            Material[] mats = r.materials;

            for (int i = 0; i < mats.Length; i++)

            {

                if (mats[i] == null) continue;

                mats[i].mainTexture = targetTex;

                if (mats[i].HasProperty("_BaseMap")) mats[i].SetTexture("_BaseMap", targetTex);

                if (mats[i].HasProperty("_MainTex")) mats[i].SetTexture("_MainTex", targetTex);

                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.white);

                if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", Color.white);

            }

            r.materials = mats;

        }

    }



    public void SetLockerHiddenState(bool hidden)

    {

        if (hidden)

        {

            if (currentEquippedInstance != null)

                currentEquippedInstance.SetActive(false);

            if (equipPoint != null)

                equipPoint.gameObject.SetActive(false);

            if (FlashlightController.Instance != null)

            {

                if (FlashlightController.Instance.heldFlashlightModel != null)

                    FlashlightController.Instance.heldFlashlightModel.SetActive(false);

                if (FlashlightController.Instance.flashlightLight != null)

                    FlashlightController.Instance.flashlightLight.enabled = false;

                if (FlashlightController.Instance.flashlightSpillLight != null)

                    FlashlightController.Instance.flashlightSpillLight.enabled = false;

            }

        }

        else

        {

            if (equipPoint != null)

                equipPoint.gameObject.SetActive(true);

            if (activeSlotIndex >= 0 && InventoryManager.Instance != null && activeSlotIndex < InventoryManager.Instance.Slots.Count)

            {

                var slot = InventoryManager.Instance.Slots[activeSlotIndex];

                if (slot != null && !slot.IsEmpty && slot.item != null)

                {

                    string lower = slot.item.itemName.ToLowerInvariant();

                    if (lower.Contains("flashlight"))

                    {

                        if (FlashlightController.Instance != null)

                            FlashlightController.Instance.SetEquippedState(true);

                    }

                    else if (currentEquippedInstance != null)

                    {

                        currentEquippedInstance.SetActive(true);

                    }

                }

            }

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

        if (!GameSettingsManager.GetKeyDown(GameAction.DropItem) && !Input.GetKeyDown(dropKey) && !Input.GetKeyDown(KeyCode.Q) && !Input.GetKeyDown(KeyCode.G))

            return;

        if (PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen) return;



        InventoryManager inventory = InventoryManager.Instance;

        if (inventory == null) return;

        inventory.EnsureSlotsInitialized();



        // Target active equipped slot, or fallback to first non-empty hotbar slot

        int targetSlot = activeSlotIndex;

        if (targetSlot < 0 || targetSlot >= inventory.Slots.Count || inventory.Slots[targetSlot].IsEmpty || inventory.Slots[targetSlot].item == null)

        {

            targetSlot = -1;

            for (int i = 0; i < inventory.Slots.Count; i++)

            {

                if (!inventory.Slots[i].IsEmpty && inventory.Slots[i].item != null)

                {

                    targetSlot = i;

                    break;

                }

            }

        }



        if (targetSlot < 0) return;



        InventorySlot slot = inventory.Slots[targetSlot];

        if (slot == null || slot.IsEmpty || slot.item == null) return;



        InventoryItem item = slot.item;



        // All items can now be dropped by the player onto the floor as physical rigidbodies!

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



        // If dropping the Flashlight, ensure offhand light state is turned off

        if (item != null && item.itemName.ToLowerInvariant().Contains("flashlight"))

        {

            if (FlashlightController.Instance != null)

            {

                FlashlightController.Instance.SetEquippedState(false);

            }

        }



        // Remove copy from inventory (or the entire stack if > 1)

        int qtyToDrop = slot.quantity;

        InventoryManager.Instance.RemoveItem(item, qtyToDrop);



        SlotMenu.Instance?.PingVisibility();



        SpawnDroppedItemInWorld(item, qtyToDrop);

    }



    /// <summary>

    /// Spawns a physical pickup in the world in front of the player for the dropped item.

    /// Used by hotkey drop, StorageMenu Drop button, and drag-and-drop out of inventory.

    /// </summary>

    public GameObject SpawnDroppedItemInWorld(InventoryItem item, int quantity = 1)

    {

        if (item == null) return null;



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



        string lowerName = item.itemName.ToLowerInvariant();

        bool isPaperOrDoc = lowerName.Contains("paper") || lowerName.Contains("form") || 

                            lowerName.Contains("slip") || lowerName.Contains("voucher") || 

                            lowerName.Contains("fragment") || lowerName.Contains("handbook") || 

                            lowerName.Contains("logbook");



        // 1. Instantiate initially with Identity rotation so unrotated local composite bounds can be computed accurately

        if (item.itemPrefab != null)

        {

            dropped = Instantiate(item.itemPrefab, spawnPos, Quaternion.identity);

        }

        else

        {

            // Fallback for items with no world prefab

            dropped = GameObject.CreatePrimitive(PrimitiveType.Cube);

            dropped.name = $"{item.itemName}_Dropped";

            dropped.transform.position = spawnPos;

            dropped.transform.rotation = Quaternion.identity;

            dropped.transform.localScale = isPaperOrDoc ? new Vector3(0.28f, 0.015f, 0.38f) : new Vector3(0.18f, 0.08f, 0.18f);



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



            // Calculate precise composite unrotated local bounds from all renderers

            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

            bool boundsInit = false;

            var renderers = dropped.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)

            {

                if (r is MeshRenderer || r is SkinnedMeshRenderer)

                {

                    Bounds b = r.bounds;

                    Vector3 lCenter = dropped.transform.InverseTransformPoint(b.center);

                    Vector3 lExtents = dropped.transform.InverseTransformVector(b.extents);

                    lExtents = new Vector3(Mathf.Abs(lExtents.x), Mathf.Abs(lExtents.y), Mathf.Abs(lExtents.z));

                    Bounds childB = new Bounds(lCenter, lExtents * 2f);

                    if (!boundsInit)

                    {

                        localBounds = childB;

                        boundsInit = true;

                    }

                    else

                    {

                        localBounds.Encapsulate(childB);

                    }

                }

            }



            // Configure BoxCollider on root

            var rootCol = dropped.GetComponent<Collider>();

            if (rootCol == null)

            {

                var box = dropped.AddComponent<BoxCollider>();

                if (boundsInit)

                {

                    box.center = localBounds.center;

                    if (isPaperOrDoc)

                    {

                        // Ultra-thin realistic document/paper collider so it lies flat on the floor

                        box.size = new Vector3(

                            Mathf.Max(0.18f, localBounds.size.x),

                            0.015f,

                            Mathf.Max(0.22f, localBounds.size.z)

                        );

                    }

                    else

                    {

                        box.size = new Vector3(

                            Mathf.Clamp(localBounds.size.x, 0.04f, 0.40f),

                            Mathf.Clamp(localBounds.size.y, 0.02f, 0.35f),

                            Mathf.Clamp(localBounds.size.z, 0.04f, 0.40f)

                        );

                    }

                }

                else

                {

                    box.size = isPaperOrDoc ? new Vector3(0.25f, 0.015f, 0.35f) : new Vector3(0.15f, 0.08f, 0.15f);

                }

            }



            // Rigidbody with realistic physics mass, continuous collision detection, and natural floor contact

            Rigidbody rb = dropped.GetComponent<Rigidbody>();

            if (rb == null) rb = dropped.AddComponent<Rigidbody>();

            rb.isKinematic = false;

            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            rb.interpolation = RigidbodyInterpolation.Interpolate;



            if (isPaperOrDoc)

            {

                // Lightweight document physics: low mass, high air damping, natural slight flat tilt

                rb.mass = 0.15f;

                rb.linearDamping = 1.2f;

                rb.angularDamping = 2.5f;



                dropped.transform.rotation = Quaternion.Euler(

                    Random.Range(-4f, 4f),

                    Random.Range(0f, 360f),

                    Random.Range(-4f, 4f)

                );

                rb.linearVelocity = forward * (dropForwardForce * 0.75f) + Vector3.down * 0.15f;

                rb.angularVelocity = Random.insideUnitSphere * 0.8f;

            }

            else

            {

                // Rigid prop physics (tools, keys, cans, fuses, sandwich)

                rb.mass = 0.45f;

                rb.linearDamping = 0.5f;

                rb.angularDamping = 1.5f;



                dropped.transform.rotation = Random.rotation;

                rb.linearVelocity = forward * dropForwardForce + Vector3.up * dropUpwardForce;

                rb.angularVelocity = Random.insideUnitSphere * 3.2f;

            }



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

            pickup.amount = quantity;



            // If dropping the Clearance Slip, display the current signed texture

            bool isClearanceSlip = lowerName.Contains("blank") && lowerName.Contains("paper");

            if (isClearanceSlip && ClearanceManager.Instance != null)

            {

                Texture slipTex = ClearanceManager.Instance.GetCurrentSlipTexture();

                RefreshDroppedSlipTexture(dropped, slipTex);

            }



            // Attach ItemVisualHighlight so dropped item sparkles and glows

            var highlight = dropped.GetComponent<ItemVisualHighlight>();

            if (highlight == null) highlight = dropped.AddComponent<ItemVisualHighlight>();



            // Ensure layer is Default for PlayerInteract raycasting

            dropped.layer = LayerMask.NameToLayer("Default");

            foreach (Transform c in dropped.transform)

            {

                c.gameObject.layer = LayerMask.NameToLayer("Default");

            }



            Debug.Log($"[InventoryEquipController] Dropped {item.itemName} x{quantity} safely at {spawnPos}.");

        }



        return dropped;

    }



    public void RefreshDroppedSlipTexture(GameObject droppedObj, Texture targetTex)

    {

        if (droppedObj == null || targetTex == null) return;

        Renderer[] rends = droppedObj.GetComponentsInChildren<Renderer>(true);

        foreach (var r in rends)

        {

            if (r == null) continue;

            Material[] mats = r.materials;

            for (int i = 0; i < mats.Length; i++)

            {

                if (mats[i] == null) continue;

                mats[i].mainTexture = targetTex;

                if (mats[i].HasProperty("_BaseMap")) mats[i].SetTexture("_BaseMap", targetTex);

                if (mats[i].HasProperty("_MainTex")) mats[i].SetTexture("_MainTex", targetTex);

                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.white);

                if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", Color.white);

            }

            r.materials = mats;

        }

    }



    public int GetActiveSlotIndex() => activeSlotIndex;



    // ── On-Screen Held Item Controls HUD Bar ─────────────────────────────────



    private void BuildControlsHudBar()

    {

        if (_controlsHudCanvasGO != null) return;



        _controlsHudCanvasGO = new GameObject("HeldItemControls_HUD", typeof(Canvas), typeof(CanvasScaler));

        _controlsHudCanvasGO.transform.SetParent(transform, false);



        var canvas = _controlsHudCanvasGO.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 85; // Above gameplay view, below modals/cutscenes



        var scaler = _controlsHudCanvasGO.GetComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;



        _controlsHudBox = new GameObject("ControlsPaperStrip", typeof(RectTransform), typeof(Image), typeof(Shadow));

        _controlsHudBox.transform.SetParent(_controlsHudCanvasGO.transform, false);



        var rt = _controlsHudBox.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(1f, 0f);

        rt.anchorMax = new Vector2(1f, 0f);

        rt.pivot = new Vector2(1f, 0f);

        rt.anchoredPosition = new Vector2(-24f, 18f);

        rt.sizeDelta = new Vector2(650f, 44f);



        var bg = _controlsHudBox.GetComponent<Image>();

#if UNITY_EDITOR

        Sprite paperSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/ScrambledPaper_HUD.png");

        if (paperSp == null)

            paperSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Blank_paper.png");

        if (paperSp != null) bg.sprite = paperSp;

#endif

        bg.color = new Color(0.94f, 0.89f, 0.79f, 0.94f);

        bg.raycastTarget = false;



        var sh = _controlsHudBox.GetComponent<Shadow>();

        sh.effectColor = new Color(0f, 0f, 0f, 0.75f);

        sh.effectDistance = new Vector2(3f, -3f);



        var txtGO = new GameObject("ControlsPromptText", typeof(RectTransform), typeof(Text));

        txtGO.transform.SetParent(_controlsHudBox.transform, false);

        var trt = txtGO.GetComponent<RectTransform>();

        trt.anchorMin = new Vector2(0.03f, 0.08f);

        trt.anchorMax = new Vector2(0.97f, 0.92f);

        trt.offsetMin = Vector2.zero;

        trt.offsetMax = Vector2.zero;



        _controlsHudText = txtGO.GetComponent<Text>();

        Font f = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold") ?? Resources.Load<Font>("Fonts/Typewriter_Bold");

#if UNITY_EDITOR

        if (f == null) f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");

#endif

        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");



        _controlsHudText.font = f;

        _controlsHudText.fontSize = 14;

        _controlsHudText.fontStyle = FontStyle.Bold;

        _controlsHudText.alignment = TextAnchor.MiddleCenter;

        _controlsHudText.color = new Color(0.10f, 0.07f, 0.05f, 0.98f);

        _controlsHudText.raycastTarget = false;

    }



    private void UpdateControlsHudBar()

    {

        if (_controlsHudBox == null || _controlsHudText == null) return;



        bool showSetting = GameSettingsManager.Instance == null || GameSettingsManager.Instance.ShowControlsHUD;

        bool shouldHide = !showSetting ||

                          PauseMenu.GameIsPaused ||

                          LockerHideManager.IsPlayerHidden ||

                          PauseMenu.IsAnyModalOrPuzzleOpen;



        if (shouldHide)

        {

            if (_controlsHudBox.activeSelf) _controlsHudBox.SetActive(false);

            return;

        }



        if (!_controlsHudBox.activeSelf) _controlsHudBox.SetActive(true);



        string dropKeyStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.DropItem));

        string flashKeyStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.Flashlight));

        string inspectKeyStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.InspectItem));

        string helpKeyStr = GameSettingsManager.FormatKeyName(GameSettingsManager.GetBinding(GameAction.TutorialHelp));



        InventorySlot activeSlot = null;

        if (InventoryManager.Instance != null && activeSlotIndex >= 0 && activeSlotIndex < InventoryManager.Instance.Slots.Count)

        {

            activeSlot = InventoryManager.Instance.Slots[activeSlotIndex];

        }



        if (activeSlot != null && !activeSlot.IsEmpty && activeSlot.item != null)

        {

            string itemName = activeSlot.item.itemName.ToUpperInvariant();

            string lower = activeSlot.item.itemName.ToLowerInvariant();



            if (lower.Contains("flashlight"))

            {

                _controlsHudText.text = $"HAWAK: FLASHLIGHT  •  [LMB / {flashKeyStr}] Ilaw  •  [{dropKeyStr}] Drop Item  •  [{inspectKeyStr}] Suriin  •  [{helpKeyStr}] Help";

            }

            else if (lower.Contains("ham") || lower.Contains("sandwich"))

            {

                _controlsHudText.text = $"HAWAK: {itemName}  •  [LMB / {flashKeyStr}] Kainin  •  [{dropKeyStr}] Drop Item  •  [{helpKeyStr}] Help";

            }

            else if (activeSlot.item.canBeDropped)

            {

                _controlsHudText.text = $"HAWAK: {itemName}  •  [{dropKeyStr}] Drop Item  •  [{inspectKeyStr}] Suriin  •  [1-6] Slot  •  [{helpKeyStr}] Help";

            }

            else

            {

                _controlsHudText.text = $"HAWAK: {itemName}  •  [{inspectKeyStr}] Suriin  •  [1-6 / Scroll] Slot  •  [{helpKeyStr}] Help";

            }

        }

        else

        {

            bool hasFlashlight = FlashlightController.Instance != null && FlashlightController.Instance.HasFlashlightInInventory();

            if (hasFlashlight)

            {

                _controlsHudText.text = $"[LMB / {flashKeyStr}] Flashlight (Kaliwa)  •  [1-6 / Scroll] Pumili ng Gamit  •  [{inspectKeyStr}] Suriin  •  [{helpKeyStr}] Help";

            }

            else

            {

                _controlsHudText.text = $"[1-6 / Scroll] Pumili ng Gamit  •  [{inspectKeyStr}] Suriin  •  [{helpKeyStr}] Tutorial / Help";

            }

        }

    }

}

