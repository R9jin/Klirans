using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance at which the player can interact with an object.")]
    public float interactRange = 3f;

    [Tooltip("Radius of the interaction ray to make pointing at items more forgiving.")]
    public float interactRadius = 0.5f;

    [Tooltip("The layers that can be interacted with.")]
    public LayerMask interactableLayer;

    [Header("Interaction UI")]
    [Tooltip("The UI Text that displays the interaction prompt.")]
    public Text promptText;

    private Camera playerCamera;

    private PickupItem currentTarget;
    private RaycastHit[] hitBuffer = new RaycastHit[10];

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

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        // Use SphereCast instead of a thin Raycast so the player doesn't have to aim perfectly
        int hitCount = Physics.SphereCastNonAlloc(ray, interactRadius, hitBuffer, interactRange, interactableLayer, QueryTriggerInteraction.Collide);
        
        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;
        PickupItem closestPickup = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = hitBuffer[i];
            
            if (h.collider.transform.IsChildOf(transform) || (playerCamera != null && h.collider.transform.IsChildOf(playerCamera.transform)))
            {
                continue;
            }

            if (h.distance < closestDistance)
            {
                IInteractable interactable = h.collider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    interactable = h.collider.GetComponentInChildren<IInteractable>();
                }

                if (interactable != null)
                {
                    closestDistance = h.distance;
                    closestInteractable = interactable;
                    closestPickup = interactable as PickupItem;
                }
            }
        }

        if (closestInteractable != null)
        {
            if (closestPickup != null) SetCurrentTarget(closestPickup);

            if (promptText != null)
            {
                string prompt = closestInteractable.GetPrompt();
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

            if (Input.GetKeyDown(KeyCode.E))
            {
                closestInteractable.Interact();
            }

            return;
        }

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
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) return;
        }

        Gizmos.color = Color.yellow;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Gizmos.DrawRay(
            ray.origin,
            ray.direction * interactRange
        );

        // Draw the sphere at the end of the cast
        Gizmos.DrawWireSphere(ray.origin + ray.direction * interactRange, interactRadius);
    }
}