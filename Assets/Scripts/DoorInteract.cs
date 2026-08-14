using UnityEngine;

/// <summary>
/// Attach to a door GameObject. The player can open/close it via PlayerInteract's raycast.
/// The door swings open by rotating around its hinge (pivot point).
/// Make sure the door's pivot is at the hinge edge, or parent it under an empty
/// GameObject whose pivot is at the hinge.
/// </summary>
public class DoorInteract : MonoBehaviour, IInteractable
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

    [Header("Lock Settings")]
    [Tooltip("Is this door currently locked?")]
    public bool isLocked = false;

    [Tooltip("Specific lockpin item required. If null, any item named 'Lockpin' works.")]
    public InventoryItem requiredLockpinItem;

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
    /// Called by PlayerInteract when the player presses E while looking at this object.
    /// </summary>
    public void Interact()
    {
        // Prevent interaction if the door is currently moving
        if (Quaternion.Angle(transform.localRotation, targetRotation) > 0.1f)
        {
            return;
        }

        if (isLocked)
        {
            if (LockpickingMinigame.Instance != null)
            {
                LockpickingMinigame.Instance.StartLockpicking(this);
            }
            else
            {
                Debug.LogWarning("[DoorInteract] Door is locked, but LockpickingMinigame instance was not found!");
            }
            return;
        }

        ToggleDoor();
    }

    public void UnlockDoor()
    {
        isLocked = false;
        ToggleDoor();
    }

    [Header("Audio Settings")]
    [Tooltip("AudioSource to play door sounds")]
    public AudioSource audioSource;

    [Tooltip("Sounds to play when opening")]
    public AudioClip[] openSounds;

    [Tooltip("Sound to play when closing")]
    public AudioClip closeSound;

    [Tooltip("Volume multiplier for closing sound")]
    [Range(0f, 1f)]
    public float closeVolume = 0.4f;

    [Tooltip("Makes the door close faster than the audio clip length.")]
    public float closeSpeedMultiplier = 1.5f;

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        targetRotation = isOpen ? openRotation : closedRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            if (isOpen && openSounds != null && openSounds.Length > 0)
            {
                AudioClip clipToPlay = openSounds[Random.Range(0, openSounds.Length)];
                audioSource.PlayOneShot(clipToPlay);
                
                if (clipToPlay.length > 0f)
                {
                    swingSpeed = Mathf.Abs(openAngle) / clipToPlay.length;
                }
            }
            else if (!isOpen && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound, closeVolume);
                
                if (closeSound.length > 0f)
                {
                    swingSpeed = (Mathf.Abs(openAngle) / closeSound.length) * closeSpeedMultiplier;
                }
            }
        }
    }

    /// <summary>
    /// Returns the prompt text to display on the HUD.
    /// </summary>
    public string GetPrompt()
    {
        if (Quaternion.Angle(transform.localRotation, targetRotation) > 0.1f)
        {
            return "";
        }

        if (isLocked)
        {
            bool hasPin = LockpickingMinigame.HasLockpin(requiredLockpinItem);
            return hasPin ? "Press E to Pick Lock" : "Locked (Requires Lockpin)";
        }

        return isOpen ? "Press E to Close" : "Press E to Open";
    }
}
