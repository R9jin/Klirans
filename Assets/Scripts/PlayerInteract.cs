using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    public float interactRange = 3f;
    public LayerMask interactableLayer;
    public Text promptText; // We will link a Text UI element here
    private Camera playerCamera;
    
    private PickupItem currentTarget;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;

        // Perform raycast
        if (Physics.Raycast(ray, out hit, interactRange, interactableLayer))
        {
            PickupItem pickupItem = hit.collider.GetComponent<PickupItem>();
            if (pickupItem != null)
            {
                currentTarget = pickupItem;
                if (promptText != null)
                {
                    promptText.text = "Press E to Pick Up";
                    promptText.gameObject.SetActive(true);
                }

                // Check for input
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
