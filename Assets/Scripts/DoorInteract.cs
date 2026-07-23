using UnityEngine;

/// <summary>
/// Attach to a door GameObject. The player can open/close it via PlayerInteract's raycast.
/// The door swings open by rotating around its hinge (pivot point).
/// Make sure the door's pivot is at the hinge edge, or parent it under an empty
/// GameObject whose pivot is at the hinge.
/// </summary>
public class DoorInteract : MonoBehaviour
{
    [Tooltip("How far the door swings open, in degrees.")]
    public float openAngle = 90f;

    [Tooltip("How fast the door swings (degrees per second).")]
    public float swingSpeed = 200f;

    [Tooltip("The local axis to rotate around (usually Y for a standard door).")]
    public Vector3 rotationAxis = Vector3.up;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    private void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);
        targetRotation = closedRotation;
    }

    private void Update()
    {
        // Smoothly rotate toward the target
        if (transform.localRotation != targetRotation)
        {
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                swingSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Called by PlayerInteract when the player presses E while looking at this door.
    /// </summary>
    public void ToggleDoor()
    {
        isOpen = !isOpen;
        targetRotation = isOpen ? openRotation : closedRotation;
    }

    /// <summary>
    /// Returns the prompt text to display on the HUD.
    /// </summary>
    public string GetPrompt()
    {
        return isOpen ? "Press E to Close" : "Press E to Open";
    }
}
