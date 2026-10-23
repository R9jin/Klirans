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
    public bool IsOpen => isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    public void ForceOpen()
    {
        isLocked = false;
        isBlackoutLocked = false;
        if (!isOpen)
        {
            ToggleDoor();
        }
    }

    [Header("Lock Settings")]
    [Tooltip("Is this door currently locked?")]
    public bool isLocked = false;

    [Tooltip("Is this door currently locked by the emergency campus blackout?")]
    public bool isBlackoutLocked = false;

    private bool _wasLockedBeforeBlackout = false;
    private bool _isSubscribedToBlackout = false;

    [Tooltip("Specific lockpin item required. If null, any item named 'Lockpin' works.")]
    public InventoryItem requiredLockpinItem;

    private void OnEnable()
    {
        TrySubscribeBlackout();
    }

    private void OnDisable()
    {
        UnsubscribeBlackout();
    }

    private void OnDestroy()
    {
        UnsubscribeBlackout();
    }

    private void TrySubscribeBlackout()
    {
        if (!_isSubscribedToBlackout && BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart += HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd += HandleBlackoutEnd;
            _isSubscribedToBlackout = true;
        }
    }

    private void UnsubscribeBlackout()
    {
        if (_isSubscribedToBlackout && BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart -= HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd -= HandleBlackoutEnd;
            _isSubscribedToBlackout = false;
        }
    }

    private void EnsureRotationsInitialized()
    {
        if (targetRotation.w == 0f && targetRotation.x == 0f && targetRotation.y == 0f && targetRotation.z == 0f)
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);
            targetRotation = closedRotation;
        }
    }

    private void Start()
    {
        TrySubscribeBlackout();

        EnsureRotationsInitialized();

        string dName = gameObject.name.ToLowerInvariant();
        if (dName.Contains("105") || dName.Contains("106") || dName.Contains("comlab"))
        {
            // Room 105 / ComLab 1 (106) is locked by department custodian, pickable with the lockpin from the lobby
            isLocked = true;
        }

        // If spawned while blackout is already active, lock immediately
        if (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive)
        {
            LockForBlackout();
        }
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

        EnsureRotationsInitialized();

        if (isLocked)
        {
            // If player has a lockpin (either specific required item or generic lockpin/lockpick in inventory),
            // allow picking the mechanical lock cylinder directly even during electrical blackout!
            bool hasPin = LockpickingMinigame.HasLockpin(requiredLockpinItem);
            if (hasPin)
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

            if (isBlackoutLocked)
            {
                // Play locked handle jiggle rattling sound
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                if (audioSource != null && closeSound != null)
                {
                    audioSource.pitch = 1.35f;
                    audioSource.PlayOneShot(closeSound, 0.45f);
                }
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Emergency Lockdown", "Naka-lockdown ang pinto dahil sa blackout. Gamitin ang Lockpin para buksan ang mechanical lock.");
                }
                return;
            }

            // Play locked handle jiggle rattling sound
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null && closeSound != null)
            {
                audioSource.pitch = 1.30f;
                audioSource.PlayOneShot(closeSound, 0.50f);
            }
            if (ObjectiveHUD.Instance != null)
            {
                bool isComLab = gameObject.name.ToLowerInvariant().Contains("105") || gameObject.name.ToLowerInvariant().Contains("106") || gameObject.name.ToLowerInvariant().Contains("comlab");
                if (isComLab)
                {
                    ObjectiveHUD.Instance.SetObjective("Kailangan ng Lockpin mula kay Dean", "Naka-lock ang ComLab. Kausapin si CCS Dean sa Room 202 (2nd Floor) upang makuha ang Lockpin.");
                }
                else
                {
                    ObjectiveHUD.Instance.SetObjective("Naka-lock ang Pinto", "Kailangan ng Lockpin para mabuksan ang pintong ito.");
                }
            }
            return;
        }

        ToggleDoor();
    }

    public void UnlockDoor()
    {
        isLocked = false;
        isBlackoutLocked = false;
        SaveManager.RegisterUnlockedDoor(gameObject.name);
        ToggleDoor();
    }

    // ── Campus Blackout Emergency Locking ─────────────────────────────────────

    private void HandleBlackoutStart()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        LockForBlackout();
    }

    private void HandleBlackoutEnd()
    {
        UnlockFromBlackout();
    }

    /// <summary>
    /// Locks down this door when campus blackout strikes.
    /// Closes the door if open and locks it until power is restored.
    /// </summary>
    public void LockForBlackout()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (isBlackoutLocked) return;

        _wasLockedBeforeBlackout = isLocked;
        isLocked = true;
        isBlackoutLocked = true;

        // If door was currently open, slam it shut!
        if (isOpen)
        {
            ToggleDoor();
        }
    }

    /// <summary>
    /// Restores the door to its pre-blackout state once power returns.
    /// </summary>
    public void UnlockFromBlackout()
    {
        if (!isBlackoutLocked) return;

        isLocked = _wasLockedBeforeBlackout;
        isBlackoutLocked = false;
    }

    /// <summary>
    /// Authoritative helper: locks down all doors in the scene when power cuts out.
    /// </summary>
    public static void LockAllDoorsForBlackout()
    {
        var doors = Object.FindObjectsByType<DoorInteract>(FindObjectsInactive.Include);
        foreach (var d in doors)
        {
            if (d != null) d.LockForBlackout();
        }
    }

    /// <summary>
    /// Authoritative helper: restores all doors once power is back.
    /// </summary>
    public static void UnlockAllDoorsFromBlackout()
    {
        var doors = Object.FindObjectsByType<DoorInteract>(FindObjectsInactive.Include);
        foreach (var d in doors)
        {
            if (d != null) d.UnlockFromBlackout();
        }
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
                if (clipToPlay != null)
                {
                    audioSource.PlayOneShot(clipToPlay);
                    if (clipToPlay.length > 0f)
                    {
                        swingSpeed = Mathf.Abs(openAngle) / clipToPlay.length;
                    }
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
        EnsureRotationsInitialized();

        if (Quaternion.Angle(transform.localRotation, targetRotation) > 0.1f)
        {
            return "";
        }

        if (isLocked)
        {
            bool hasPin = LockpickingMinigame.HasLockpin(requiredLockpinItem);
            if (hasPin)
            {
                return "Press [E] to Pick Lock";
            }

            if (isBlackoutLocked)
            {
                return "[Locked — Emergency Lockdown Active (Requires Lockpin)]";
            }

            bool isComLab = gameObject.name.ToLowerInvariant().Contains("105") || gameObject.name.ToLowerInvariant().Contains("106") || gameObject.name.ToLowerInvariant().Contains("comlab");
            return isComLab ? "Locked (Requires Lockpin from CCS Dean in Room 202)" : "Locked (Requires Lockpin)";
        }

        return isOpen ? "Press E to Close" : "Press E to Open";
    }
}
