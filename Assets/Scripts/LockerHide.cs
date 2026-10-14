using UnityEngine;

/// <summary>
/// LockerHide — Attach to a locker GameObject in the scene to make it a hideable spot.
///
/// SETUP per locker:
///   1. Add this component to the locker root GameObject.
///   2. Create a child empty named "PeekAnchor" — position it inside the locker
///      at eye-level, with its forward direction pointing OUT through the door gap.
///      (i.e. looking at the hallway through the crack between the doors.)
///   3. Make sure the locker has a BoxCollider set as a Trigger on the Interactable layer,
///      so PlayerInteract's SphereCast can find it.
///
/// Only a limited number of lockers in the scene should have this component.
/// The interaction prompt is suppressed when the player is already hidden elsewhere.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LockerHide : MonoBehaviour, IInteractable
{
    [Header("Peek Anchor")]
    [Tooltip("A child Transform positioned inside the locker at the door gap (eye level). " +
             "Its forward (+Z) should face the hallway — the direction the player peeks toward.")]
    public Transform peekAnchor;

    [Header("Locker Identity")]
    [Tooltip("Optional label shown in the interaction prompt, e.g. 'Staff Locker', '2F Locker'.")]
    public string lockerLabel = "Locker";

    [Header("Lock Settings")]
    [Tooltip("Is this locker locked? Requires a Locker Key to unlock before the player can hide.")]
    public bool isLocked = false;

    [Tooltip("Specific key item required. If null, any item named 'Locker Key' is accepted.")]
    public InventoryItem requiredKey;

    [Tooltip("If true, removes 1 key from inventory when successfully unlocking.")]
    public bool consumeKeyOnUnlock = true;

    [Tooltip("Sound played when successfully unlocking the locker.")]
    public AudioClip unlockAudio;

    [Tooltip("Sound played when trying to open a locked locker without a key.")]
    public AudioClip lockedRattleAudio;

    [Header("Locker Rewards / Quest Items")]
    [Tooltip("Optional quest item stored inside this locker (e.g. Executive Seal Stamp in 2F Faculty Locker).")]
    public InventoryItem storedRewardItem;

    private bool _hasRewardBeenClaimed = false;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f;
            _audioSource.minDistance = 1.0f;
            _audioSource.maxDistance = 10.0f;
            _audioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        if (unlockAudio == null)
            unlockAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
        if (lockedRattleAudio == null)
            lockedRattleAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (storedRewardItem == null && lockerLabel != null && (lockerLabel.Contains("2F") || lockerLabel.IndexOf("CAB-04", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            storedRewardItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Executive_Seal_Stamp.asset");
        }
#endif
    }

    // ── Key Verification ─────────────────────────────────────────────────────

    public bool HasRequiredKey()
    {
        // 1. Check hotbar inventory
        if (InventoryManager.Instance != null)
        {
            if (requiredKey != null && InventoryManager.Instance.HasItem(requiredKey)) return true;
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Locker Key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        // 2. Check storage bag
        if (StorageManager.Instance != null)
        {
            if (requiredKey != null && StorageManager.Instance.HasItem(requiredKey)) return true;
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Locker Key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return false;
    }

    private void ConsumeKey()
    {
        if (InventoryManager.Instance != null)
        {
            if (requiredKey != null && InventoryManager.Instance.HasItem(requiredKey))
            {
                InventoryManager.Instance.RemoveItem(requiredKey, 1);
                return;
            }
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Locker Key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }

        if (StorageManager.Instance != null)
        {
            if (requiredKey != null && StorageManager.Instance.HasItem(requiredKey))
            {
                StorageManager.Instance.RemoveItem(requiredKey, 1);
                return;
            }
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Locker Key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    StorageManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }
    }

    // ── IInteractable ────────────────────────────────────────────────────────

    public string GetPrompt()
    {
        // Don't show prompt if player is already hiding somewhere
        if (LockerHideManager.IsPlayerHidden) return string.Empty;

        if (isLocked)
        {
            bool hasKey = HasRequiredKey();
            bool knowsCode = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("LockerKeyCode");

            if (hasKey)
            {
                return knowsCode ? $"[E] Buksan ang {lockerLabel} (Code 7-3-2)" : $"[E] Buksan ang {lockerLabel} gamit ang Susi";
            }
            return knowsCode ? $"[{lockerLabel}] (Naka-lock - Kailangan ang Susi na may Code 7-3-2)" : $"[{lockerLabel}] (Naka-lock - Kailangan ng Locker Key)";
        }

        return $"[E] Magtago sa {lockerLabel}";
    }

    public void Interact()
    {
        // Already hidden — treat E as exit
        if (LockerHideManager.IsPlayerHidden)
        {
            LockerHideManager.Instance?.ExitLocker();
            return;
        }

        // Check if locked
        if (isLocked)
        {
            if (HasRequiredKey())
            {
                if (consumeKeyOnUnlock) ConsumeKey();
                isLocked = false;

                if (_audioSource != null && unlockAudio != null)
                {
                    _audioSource.pitch = 1.15f;
                    _audioSource.PlayOneShot(unlockAudio, 0.85f);
                }

                Debug.Log($"[LockerHide] Unlocked {lockerLabel} with Locker Key!");

                // Check for stored quest item reward (e.g. Executive Seal Stamp in 2F Faculty Locker)
                if (storedRewardItem != null && !_hasRewardBeenClaimed)
                {
                    _hasRewardBeenClaimed = true;
                    bool added = false;
                    if (InventoryManager.Instance != null) added = InventoryManager.Instance.AddItem(storedRewardItem, 1);
                    if (!added && StorageManager.Instance != null) added = StorageManager.Instance.AddItem(storedRewardItem, 1);

                    // Open 3D inspection viewer for the newly discovered item!
                    ItemInspectionViewer3D.Inspect(storedRewardItem);

                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective(
                            "Nakuha ang Executive Seal Stamp!",
                            "Suriin ang ilalim ng stamp gamit ang 3D inspection para makita ang opisyal na EVP seal code."
                        );
                    }
                    return; // Don't hide inside locker on the frame of inspecting the reward
                }
                // Continue execution below to immediately enter the unlocked locker
            }
            else
            {
                if (_audioSource != null && lockedRattleAudio != null)
                {
                    _audioSource.pitch = 1.4f;
                    _audioSource.PlayOneShot(lockedRattleAudio, 0.6f);
                }
                Debug.Log($"[LockerHide] {lockerLabel} is locked! Locker Key required.");
                return;
            }
        }

        if (LockerHideManager.Instance == null)
        {
            Debug.LogWarning("[LockerHide] No LockerHideManager found in the scene!");
            return;
        }

        // Auto-find or create PeekAnchor if not assigned
        Transform anchor = peekAnchor;
        if (anchor == null)
        {
            anchor = transform.Find("PeekAnchor");
            if (anchor == null)
            {
                // Create a fallback anchor positioned inside looking toward the hallway corridor
                var anchorGO = new GameObject("PeekAnchor");
                anchorGO.transform.SetParent(transform, false);

                // Position slightly inside the locker at eye level (~1.55m)
                anchorGO.transform.localPosition = new Vector3(0f, 0.45f, 0f);

                // Look straight out into the hallway corridor center (X = -84.0f)
                Vector3 corridorCenter = new Vector3(-84.0f, transform.position.y, transform.position.z);
                Vector3 worldDir = (corridorCenter - transform.position).normalized;
                anchorGO.transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);

                anchor = anchorGO.transform;
                peekAnchor = anchor;
                Debug.Log($"[LockerHide] Created PeekAnchor on {name} looking toward hallway ({worldDir}).");
            }
        }

        Debug.Log($"[LockerHide] Player entering locker: {lockerLabel} on {gameObject.name}");
        LockerHideManager.Instance.EnterLocker(this, anchor);
    }

    // ── Editor Gizmos ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Draw a green eye-slit icon at the peek anchor
        Transform anchor = peekAnchor != null ? peekAnchor : transform.Find("PeekAnchor");
        if (anchor == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(anchor.position, 0.06f);
        // Draw the peek direction (the direction the player looks outward)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(anchor.position, anchor.forward * 0.5f);

        // Label bounding box of the locker
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
