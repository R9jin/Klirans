using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance at which the player can interact with an object.")]
    public float interactRange = 3f;

    [Tooltip("The layers that can be interacted with.")]
    public LayerMask interactableLayer;

    [Header("Interaction UI")]
    [Tooltip("The UI Text that displays the interaction prompt.")]
    public Text promptText;

    private Camera playerCamera;

    private PickupItem currentTarget;

    private void Start()
    {
        // Find the camera attached to the player or one of its children.
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerInteract could not find a Camera. " +
                "Make sure the Player has a Camera component on itself or one of its children."
            );
        }

        // Hide the interaction prompt when the game starts.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "PlayerInteract has no Prompt Text assigned. " +
                "The interaction may work, but the interaction message cannot be displayed."
            );
        }
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            return;
        }

        // Create a ray from the exact center of the player's screen.
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        // Cast ray forward from camera using RaycastAll to handle layered or childed meshes
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactableLayer, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            // Ignore any colliders attached to the player or childed under player/camera (e.g. held items)
            if (h.collider.transform.IsChildOf(transform) || (playerCamera != null && h.collider.transform.IsChildOf(playerCamera.transform)))
            {
                continue;
            }

            // Check for IInteractable on hit object, its parents, or its children
            IInteractable interactable = h.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = h.collider.GetComponentInChildren<IInteractable>();
            }

            if (interactable != null)
            {
                // Set current target if it's a PickupItem for backward compatibility
                PickupItem pickupItem = interactable as PickupItem;
                if (pickupItem != null) SetCurrentTarget(pickupItem);

                // Display prompt
                if (promptText != null)
                {
                    string prompt = interactable.GetPrompt();
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        promptText.text = prompt;
                        promptText.gameObject.SetActive(true);
                    }
                    else
                    {
                        promptText.gameObject.SetActive(false);
                    }
                }

                // Interact when E is pressed
                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.Interact();
                }

                return;
            }
        }

        // The player is not looking at an interactable object.
        ClearCurrentTarget();
    }

    private void SetCurrentTarget(PickupItem pickupItem)
    {
        currentTarget = pickupItem;
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;

        // Always hide the prompt when there is no valid target.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Only draw the interaction ray when the player is selected
        // and the camera has already been found.
        if (playerCamera == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Gizmos.DrawRay(
            ray.origin,
            ray.direction * interactRange
        );
    }
}