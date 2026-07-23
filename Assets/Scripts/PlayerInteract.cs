using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask interactableLayer;

    [Header("Interaction UI")]
    public Text promptText;

    private Camera playerCamera;
    private PickupItem currentTarget;

    private void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            return;
        }

        // Create a ray from the center of the player's camera.
        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(
                Screen.width / 2f,
                Screen.height / 2f,
                0f
            )
        );

        RaycastHit hit;

        // Check if the ray hits an object on the interactable layer.
        if (Physics.Raycast(
            ray,
            out hit,
            interactRange,
            interactableLayer
        ))
        {
            // Look for PickupItem on the object that was hit
            // OR on any of its parents.
            //
            // This allows the flashlight to be structured like:
            //
            // Flashlight
            // ├── Back
            // ├── Base
            // ├── Button
            // ├── ButtonDesign
            // ├── Glass
            // └── PickupItem
            //
            // If the ray hits Glass, Base, or Back,
            // Unity will find PickupItem on the Flashlight parent.
            PickupItem pickupItem = hit.collider.GetComponentInParent<PickupItem>();

            if (pickupItem != null)
            {
                currentTarget = pickupItem;

                if (promptText != null)
                {
                    promptText.text = "Press E to Pick Up";
                    promptText.gameObject.SetActive(true);
                }

                // Pick up the item when E is pressed.
                if (Input.GetKeyDown(KeyCode.E))
                {
                    currentTarget.Interact();

                    currentTarget = null;

                    if (promptText != null)
                    {
                        promptText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget = null;

            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }
        }
    }
}