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
                    newPoint.transform.localPosition = new Vector3(0.2f, -0.2f, 0.45f);
                    newPoint.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
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
    }

    /// <summary>
    /// Selects or toggles an inventory slot by index (0-5).
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        InventoryManager inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>(true);
        if (inventory == null)
        {
            Debug.LogWarning("[InventoryEquipController] InventoryManager instance not found!");
            return;
        }

        // Toggling off if pressing the same key again
        if (activeSlotIndex == slotIndex)
        {
            UnequipCurrentItem();
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
            Debug.Log($"[InventoryEquipController] Selected empty slot {slotIndex + 1}.");
            return;
        }

        // Slot contains item -> Equip item
        EquipItem(targetSlot.item);
        activeSlotIndex = slotIndex;
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
            FlashlightController flashlight = FlashlightController.Instance ?? FindObjectOfType<FlashlightController>(true);
            if (flashlight != null)
            {
                flashlight.SetEquippedState(true);
            }
            return;
        }

        // 2. Spawn 3D viewmodel for items with an itemPrefab or paper/fragment/lockpin items
        bool isPaperOrFragment = (blankPaperItemData != null && item == blankPaperItemData) ||
                                  itemNameLower.Contains("paper") ||
                                  itemNameLower.Contains("fragment") ||
                                  itemNameLower.Contains("clearance") ||
                                  itemNameLower.Contains("slip");

        GameObject prefabToSpawn = item.itemPrefab;
        if (prefabToSpawn == null && isPaperOrFragment && heldPaperModelPrefab != null)
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
        FlashlightController flashlight = FlashlightController.Instance ?? FindObjectOfType<FlashlightController>(true);
        if (flashlight != null)
        {
            flashlight.SetEquippedState(false);
        }

        activeSlotIndex = -1;
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

    public int GetActiveSlotIndex() => activeSlotIndex;
}
