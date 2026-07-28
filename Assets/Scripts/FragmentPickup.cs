using UnityEngine;

/// <summary>
/// Fragment Pickup Script
/// Attached to each of the 4 fragment paper GameObjects placed in the scene.
/// Supports interaction via Raycast (IInteractable) and optional Trigger collision.
/// Adds the fragment item directly to the player's bag inventory upon pickup.
/// </summary>
public class FragmentPickup : MonoBehaviour, IInteractable
{
    public enum FragmentPieceType
    {
        LeftPiece,
        RightPiece,
        TopPiece,
        BottomPiece
    }

    [Header("Fragment Setup")]
    [Tooltip("Specific fragment piece identifier (LeftPiece, RightPiece, TopPiece, BottomPiece).")]
    public FragmentPieceType pieceType;

    [Tooltip("Display name shown in UI prompts.")]
    public string fragmentName = "Clearance Fragment";

    [Tooltip("The ScriptableObject InventoryItem added to the bag when picked up.")]
    public InventoryItem itemData;

    [Header("Pickup Options")]
    [Tooltip("If true, walking into the object's trigger collider will automatically collect it without pressing E.")]
    public bool allowTriggerPickup = false;

    [Header("Pickup Effects")]
    [Tooltip("Optional sound effect to play when collected.")]
    public AudioClip pickupSound;

    /// <summary>
    /// UI prompt message returned to interaction systems (e.g., PlayerInteract).
    /// </summary>
    public string GetPrompt()
    {
        return $"Press [E] to pick up {fragmentName}";
    }

    /// <summary>
    /// Triggered when the player presses E while looking at this fragment (IInteractable).
    /// </summary>
    public void Interact()
    {
        Collect();
    }

    /// <summary>
    /// Optional pickup trigger when player walks over the fragment.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (allowTriggerPickup && other.CompareTag("Player"))
        {
            Collect();
        }
    }

    /// <summary>
    /// Performs pickup logic: plays SFX, adds item to inventory, notifies FragmentManager, and destroys this object.
    /// </summary>
    public void Collect()
    {
        // Play pickup sound at the fragment's position
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }

        // Register collection with FragmentManager Singleton
        if (FragmentManager.Instance != null)
        {
            FragmentManager.Instance.CollectFragment(pieceType.ToString(), gameObject, itemData);
        }
        else
        {
            Debug.LogWarning("[FragmentPickup] FragmentManager instance missing in scene! Destroying fragment directly.");
            Destroy(gameObject);
        }
    }
}
