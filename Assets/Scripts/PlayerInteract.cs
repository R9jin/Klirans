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

        RaycastHit hit;

        // Cast the ray forward from the player's camera.
        if (Physics.Raycast(
            ray,
            out hit,
            interactRange,
            interactableLayer,
            QueryTriggerInteraction.Collide
        ))
        {
            // Search the object that was hit and all of its parents
            // for a PickupItem component.
            //
            // This supports the following hierarchy:
            //
            // Flashlight
            // ├── Back
            // ├── Base
            // ├── Button
            // ├── ButtonDesign
            // ├── Glass
            // └── PickupItem
            //
            // If the ray hits Back, Base, or Glass,
            // GetComponentInParent will find PickupItem
            // on the Flashlight parent.
            PickupItem pickupItem = hit.collider.GetComponentInParent<PickupItem>();

            if (pickupItem != null)
            {
                SetCurrentTarget(pickupItem);

                // Show the pickup prompt.
                if (promptText != null)
                {
                    promptText.text = "Press E to Pick Up";
                    promptText.gameObject.SetActive(true);
                }

                // Pick up the item when E is pressed.
                if (Input.GetKeyDown(KeyCode.E))
                {
                    pickupItem.Interact();
                }

                return;
            }

            // Check for an interactable door.
            DoorInteract door = hit.collider.GetComponentInParent<DoorInteract>();

            if (door != null)
            {
                // Show the door prompt.
                if (promptText != null)
                {
                    promptText.text = door.GetPrompt();
                    promptText.gameObject.SetActive(true);
                }

                // Toggle the door when E is pressed.
                if (Input.GetKeyDown(KeyCode.E))
                {
                    door.ToggleDoor();
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