using UnityEngine;

/// <summary>
/// Attach to each clearance NPC in the lobby.
///
/// INTERACTION RULES:
///   1. Player must have the assembled Blank_Paper slip in inventory.
///   2. Signatures must be collected IN ORDER: signed1 → signed2 → … → signed6.
///      You cannot talk to NPC 3 until NPC 2 has already signed.
///   3. Player must be within interactionRange.
///   4. Once signed, prompt shows "Already signed" and E does nothing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClearanceNPC : MonoBehaviour, IInteractable
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("NPC Identity")]
    public string npcName = "NPC";

    [Header("Clearance Signing")]
    [Tooltip("0 = signed1 … 5 = signed6  (must be collected in ascending order)")]
    [Range(0, 5)]
    public int signatureIndex = 0;

    [Tooltip("The Blank_Paper InventoryItem ScriptableObject (FragmentManager reward). " +
             "Player must carry this before ANY NPC interaction is unlocked.")]
    public InventoryItem blankPaperItem;

    [Header("Interaction Range")]
    [Tooltip("Maximum distance (in meters) the player can be from the NPC to interact.")]
    public float interactionRange = 2.5f;

    [Header("Dialogue")]
    [TextArea(2, 3)]
    public string unsignedDialogue      = "I'll sign your clearance slip.";
    [TextArea(2, 3)]
    public string alreadySignedDialogue = "I already signed your clearance slip.";
    [TextArea(2, 3)]
    public string notYourTurnDialogue   = "Someone else needs to sign before me.";

    // ── Runtime cache ──────────────────────────────────────────────────────────
    private Transform _playerTransform;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Collider must be a trigger — NPCs must never block the player's path
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;
    }

    // ── IInteractable ──────────────────────────────────────────────────────────

    public string GetPrompt()
    {
        // Distance gate — must be within interactionRange
        if (_playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > interactionRange) return string.Empty;
        }

        // Gate 1: player needs the blank slip — show nothing if they don't have it
        if (!PlayerHasBlankSlip()) return string.Empty;

        // Already signed
        if (ClearanceManager.Instance != null &&
            ClearanceManager.Instance.HasSignature(signatureIndex))
            return $"[{npcName}] Already signed";

        // Gate 2: chronological order — previous NPC must already be signed
        if (!PreviousNPCSigned()) return string.Empty;

        return $"Press E to talk to {npcName}";
    }

    public void Interact()
    {
        // Distance gate
        if (_playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > interactionRange) return;
        }

        if (ClearanceManager.Instance == null)
        {
            Debug.LogError("[ClearanceNPC] ClearanceManager missing from scene!");
            return;
        }

        // Gate 1: blank slip
        if (!PlayerHasBlankSlip())
        {
            Debug.Log($"[{npcName}] No clearance slip in inventory — blocked.");
            return;
        }

        // Gate 2: order enforcement
        if (!PreviousNPCSigned())
        {
            Debug.Log($"[{npcName}] Previous signature not yet collected — blocked.");
            ShowDialogue(notYourTurnDialogue);
            return;
        }

        bool isNew = ClearanceManager.Instance.GrantSignature(signatureIndex);

        if (isNew)
        {
            Debug.Log($"[ClearanceNPC] {npcName} signed. ({ClearanceManager.Instance.SignatureCount}/6)");
            ShowDialogue(unsignedDialogue);

            if (ClearanceManager.Instance.IsFullyClear())
                Debug.Log("[ClearanceNPC] All 6 signatures! Clearance complete!");
        }
        else
        {
            ShowDialogue(alreadySignedDialogue);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>True if this is the first NPC (index 0) or the previous one is already signed.</summary>
    private bool PreviousNPCSigned()
    {
        if (signatureIndex == 0) return true;
        return ClearanceManager.Instance != null &&
               ClearanceManager.Instance.HasSignature(signatureIndex - 1);
    }

    /// <summary>True if the player is carrying the assembled Blank_Paper slip.</summary>
    private bool PlayerHasBlankSlip()
    {
        if (blankPaperItem == null) return true; // no item assigned → gate open (testing)

        bool inHotbar  = InventoryManager.Instance != null &&
                         InventoryManager.Instance.HasItem(blankPaperItem);
        bool inStorage = StorageManager.Instance  != null &&
                         StorageManager.Instance.HasItem(blankPaperItem);
        return inHotbar || inStorage;
    }

    private void ShowDialogue(string message) => Debug.Log($"[{npcName}] \"{message}\"");

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.4f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, interactionRange);
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            $"{npcName}  |  slot: signed{signatureIndex + 1}  |  range: {interactionRange}m"
        );
    }
#endif
}
