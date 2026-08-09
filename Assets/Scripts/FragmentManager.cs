using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fragment Manager Script
/// Tracks the 4 collected fragment pieces, displays HUD notifications,
/// adds each fragment item to the player's bag inventory upon pickup,
/// and combines them into the completed Blank_Paper clearance slip item upon finding all 4 pieces.
/// </summary>
public class FragmentManager : MonoBehaviour
{
    public static FragmentManager Instance { get; private set; }

    [Header("Reward Item / Prefab Setup")]
    [Tooltip("ScriptableObject InventoryItem representing the completed 'Blank_Paper' clearance slip.")]
    public InventoryItem blankPaperRewardItem;

    [Tooltip("Optional GameObject Prefab if instantiating directly into a custom inventory transform.")]
    public GameObject blankPaperPrefab;

    [Header("Fragment Tracking")]
    [Tooltip("Total number of required fragments (default is 4: Left, Right, Top, Bottom).")]
    public int totalFragmentsRequired = 4;

    [Header("UI Feedback (Optional)")]
    [Tooltip("UI Text element to display collection notifications (e.g. 'Fragments: 3/4').")]
    public Text statusNotificationText;

    [Tooltip("How long notification messages stay on screen.")]
    public float notificationDisplayTime = 3f;

    [Header("Completion Audio/Effects")]
    [Tooltip("Optional audio clip played when all 4 fragments are combined.")]
    public AudioClip completionSound;

    // Set of collected unique fragment piece IDs & items
    private HashSet<string> collectedFragments = new HashSet<string>();
    private List<InventoryItem> collectedFragmentItems = new List<InventoryItem>();
    private float notificationTimer = 0f;
    private Camera mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Notification fade/hide timer
        if (notificationTimer > 0)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0 && statusNotificationText != null)
            {
                statusNotificationText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Called when a fragment piece is picked up by the player.
    /// Adds the fragment item directly to the player's hotbar/bag inventory for monitoring.
    /// </summary>
    public void CollectFragment(string pieceID, GameObject fragmentWorldObject, InventoryItem itemData = null)
    {
        if (collectedFragments.Contains(pieceID))
        {
            Debug.LogWarning($"[FragmentManager] Fragment '{pieceID}' was already collected.");
            if (fragmentWorldObject != null) Destroy(fragmentWorldObject);
            return;
        }

        collectedFragments.Add(pieceID);
        if (itemData != null)
        {
            collectedFragmentItems.Add(itemData);

            InventoryManager inventory = InventoryManager.Instance;
            StorageManager storage = StorageManager.Instance;

            // 1. Try adding to InventoryManager (6-Slot Hotbar on screen) so it appears immediately
            bool itemAdded = false;
            if (inventory != null)
            {
                itemAdded = inventory.AddItem(itemData, 1);
            }

            // 2. Fallback to StorageManager (12-Slot Bag Inventory) if Hotbar is full
            if (!itemAdded && storage != null)
            {
                storage.AddItem(itemData, 1);
            }
        }

        int currentCount = collectedFragments.Count;
        Debug.Log($"[FragmentManager] Fragment collected: {pieceID}. Progress: {currentCount}/{totalFragmentsRequired}");

        // Destroy physical pickup object in world
        if (fragmentWorldObject != null)
        {
            Destroy(fragmentWorldObject);
        }

        // Show UI Status Notification
        ShowNotification($"Clearance Fragment Collected! ({currentCount}/{totalFragmentsRequired})");

        // Check puzzle completion condition
        if (currentCount >= totalFragmentsRequired)
        {
            CompleteFragmentPuzzle();
        }
    }

    /// <summary>
    /// Triggers minigame completion: consumes the 4 fragment items from hotbar/bag inventory and awards the completed Blank_Paper clearance slip.
    /// </summary>
    private void CompleteFragmentPuzzle()
    {
        Debug.Log("[FragmentManager] All 4 fragments collected! Combining into Blank_Paper clearance slip...");

        // Play completion SFX
        if (completionSound != null)
        {
            AudioSource.PlayClipAtPoint(completionSound, mainCamera != null ? mainCamera.transform.position : transform.position, 1.0f);
        }

        InventoryManager inventory = InventoryManager.Instance;
        StorageManager storage = StorageManager.Instance;

        // 1. Remove individual fragment items from Hotbar/Bag inventory
        foreach (var fragItem in collectedFragmentItems)
        {
            if (fragItem != null)
            {
                if (inventory != null && inventory.HasItem(fragItem))
                {
                    inventory.RemoveItem(fragItem, 1);
                }
                else if (storage != null && storage.HasItem(fragItem))
                {
                    storage.RemoveItem(fragItem, 1);
                }
            }
        }

        // 2. Add the completed Blank_Paper Clearance Slip item to Inventory/Bag
        bool itemAdded = AddRewardToPlayerInventory();

        if (itemAdded)
        {
            ShowNotification("All 4 Fragments Combined! 'Blank Paper' clearance slip added to inventory!");
        }
        else
        {
            ShowNotification("Fragments Combined! Clearance slip created (Check inventory).");
        }

        // Clear tracking set after puzzle completion
        collectedFragments.Clear();
        collectedFragmentItems.Clear();
    }

    /// <summary>
    /// Adds the Blank_Paper item directly into the player's hotbar or bag inventory.
    /// </summary>
    private bool AddRewardToPlayerInventory()
    {
        InventoryManager inventory = InventoryManager.Instance;
        StorageManager storage = StorageManager.Instance;

        // 1. Try adding to InventoryManager (6-Slot Hotbar on screen)
        if (inventory != null && blankPaperRewardItem != null)
        {
            bool addedToHotbar = inventory.AddItem(blankPaperRewardItem, 1);
            if (addedToHotbar) return true;
        }

        // 2. Fallback to StorageManager (12-Slot Bag Inventory)
        if (storage != null && blankPaperRewardItem != null)
        {
            bool addedToBag = storage.AddItem(blankPaperRewardItem, 1);
            if (addedToBag) return true;
        }

        return false;
    }

    /// <summary>
    /// Displays a temporary UI status message on the player's screen.
    /// </summary>
    private void ShowNotification(string message)
    {
        if (statusNotificationText != null)
        {
            statusNotificationText.text = message;
            statusNotificationText.gameObject.SetActive(true);
            notificationTimer = notificationDisplayTime;
        }
    }

    /// <summary>
    /// Helper method to retrieve current fragment collection count.
    /// </summary>
    public int GetCollectedCount() => collectedFragments.Count;
}
