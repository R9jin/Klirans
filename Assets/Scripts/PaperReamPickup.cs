using UnityEngine;

/// <summary>
/// Interactable pickup for the Ream of Bond Paper hidden in the dark library bookshelf aisles.
/// Implements IInteractable for raycast interaction.
/// </summary>
public class PaperReamPickup : MonoBehaviour, IInteractable
{
    [Header("Item Reference")]
    [Tooltip("The ScriptableObject InventoryItem for the Bond Paper Ream.")]
    public InventoryItem paperItemData;

    [Header("Horror / Audio Feedback")]
    [Tooltip("Creepy sound or pickup cue played upon taking the paper.")]
    public AudioClip pickupSound;

    [Header("Visual Glint")]
    [Tooltip("Gentle vertical floating/bobbing to make it stand out in dark aisles.")]
    public bool enableFloat = true;
    public float floatAmplitude = 0.05f;
    public float floatFrequency = 2.0f;

    private Vector3 _basePos;

    private void Start()
    {
        _basePos = transform.position;
    }

    private void Update()
    {
        if (enableFloat)
        {
            float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.position = _basePos + new Vector3(0f, yOffset, 0f);
        }
    }

    public string GetPrompt()
    {
        return "Press [E] to Pick Up Ream of Bond Paper";
    }

    public void Interact()
    {
        if (paperItemData == null)
        {
            Debug.LogWarning("[PaperReamPickup] No InventoryItem assigned!");
            return;
        }

        bool added = false;
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasFreeSlot())
        {
            added = InventoryManager.Instance.AddItem(paperItemData, 1);
        }
        else if (StorageManager.Instance != null)
        {
            added = StorageManager.Instance.AddItem(paperItemData, 1);
        }

        if (added)
        {
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
            }

            // Update objective to guide player to printer
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Print Clearance Voucher",
                    "Return to the workstation printer across from the Head Librarian and load the bond paper."
                );
            }

            Debug.Log("[PaperReamPickup] Ream of Bond Paper added to inventory.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("[PaperReamPickup] Inventory is completely full!");
        }
    }
}
