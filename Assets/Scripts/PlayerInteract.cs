using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance at which the player can interact with an object.")]
    public float interactRange = 1.9f;

    [Tooltip("Radius of the interaction ray to make pointing at items more forgiving.")]
    public float interactRadius = 0.08f;

    [Tooltip("The layers that can be interacted with.")]
    public LayerMask interactableLayer;

    [Header("Interaction UI")]
    [Tooltip("The UI Text that displays the interaction prompt.")]
    public Text promptText;

    private Camera playerCamera;

    private PickupItem currentTarget;
    private RaycastHit[] hitBuffer = new RaycastHit[32];

    private void Start()
    {
        // Enforce tight natural reach bounds
        if (interactRange > 2.0f) interactRange = 1.9f;
        if (interactRadius > 0.12f) interactRadius = 0.08f;

        // Find the camera attached to the player or one of its children.
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerInteract could not find a Camera. " +
                "Make sure the Player has a Camera component on itself or one of its children."
            );
        }

        // Hide the interaction prompt when the game starts and style with retro font
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);

            Font typewriter = Resources.Load<Font>("Fonts/Typewriter_Bold") ?? Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
            if (typewriter == null)
                typewriter = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
            if (typewriter != null) promptText.font = typewriter;

            promptText.fontSize = 22;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var rt = promptText.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Anchor to bottom of screen (just above stamina gauge at Y=145), well clear of center reticle
                rt.anchorMin = new Vector2(0.5f, 0.0f);
                rt.anchorMax = new Vector2(0.5f, 0.0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(900f, 60f);
                rt.anchoredPosition = new Vector2(0f, 195f);
            }

            var outline = promptText.GetComponent<Outline>();
            if (outline == null) outline = promptText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
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
        if (playerCamera == null || PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
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
                    // Line of sight check: do not allow interacting through solid obstacles
                    Vector3 targetPos = h.collider.bounds.center;
                    Vector3 rayDir = targetPos - ray.origin;
                    float rayDist = rayDir.magnitude;
                    if (rayDist > 0.05f && Physics.Raycast(ray.origin, rayDir.normalized, out RaycastHit losHit, rayDist - 0.05f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (losHit.collider != null && 
                            losHit.collider != h.collider && 
                            losHit.collider.transform.root != transform.root &&
                            !losHit.collider.transform.IsChildOf(h.collider.transform) && 
                            !h.collider.transform.IsChildOf(losHit.collider.transform))
                        {
                            // Obstructed by intermediate solid geometry
                            continue;
                        }
                    }

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