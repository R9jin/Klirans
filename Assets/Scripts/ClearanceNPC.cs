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

    [Tooltip("Optional voucher required for Head Librarian (signatureIndex 0).")]
    public InventoryItem libraryVoucherItem;

    [Header("Interaction Range")]
    [Tooltip("Maximum distance (in meters) the player can be from the NPC to interact.")]
    public float interactionRange = 4.0f;

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
        // Solid physical collider — NPC collides with the player
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
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

        // Already signed check
        if (ClearanceManager.Instance != null &&
            ClearanceManager.Instance.HasSignature(signatureIndex))
        {
            // Special case: Registrar (signatureIndex 3) receives completed slip after all 6 signatures
            if (signatureIndex == 3 && ClearanceManager.Instance.IsFullyClear())
            {
                if (!ClearanceManager.Instance.IsSlipSubmitted)
                    return $"[{npcName}] Submit Completed Clearance Slip (Press E)";
                else
                    return $"[{npcName}] Clearance Slip Submitted (Press E to talk)";
            }

            return $"[{npcName}] Already signed (Press E to talk)";
        }

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

        // Special case: Registrar (signatureIndex 3) handles slip submission after all 6 signatures are gathered
        if (signatureIndex == 3 && ClearanceManager.Instance.IsFullyClear())
        {
            if (!ClearanceManager.Instance.IsSlipSubmitted)
            {
                ClearanceManager.Instance.SubmitSlipToRegistrar();
                ShowDialogue("Let me verify your clearance slip... Library, Guidance & SAS, College of Computing Studies, Registrar, Cashier, and the Executive Vice President. All signatures confirmed and officially recorded! Your clearance is complete. The campus main gate at the lobby entrance is now unlocked for you. Have a safe journey!");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Escape The Campus", "The campus main gate at the lobby entrance is now unlocked. Escape the building!");
                }
                return;
            }
            else
            {
                ShowDialogue("Your clearance has already been submitted and officially recorded. Proceed to the campus main gate at the lobby entrance to exit.");
                return;
            }
        }

        // Already signed
        if (ClearanceManager.Instance.HasSignature(signatureIndex))
        {
            ShowDialogue(alreadySignedDialogue);
            return;
        }

        // Gate 1: Check clearance slip in inventory
        bool hasSlip = PlayerHasBlankSlip();
        if (!hasSlip)
        {
            Debug.Log($"[{npcName}] No clearance slip in inventory — player must piece together fragments first.");
            if (signatureIndex == 0)
            {
                ShowDialogue("Keep your voice down, this is the university library. Where is your clearance form? You need to find all 4 torn fragments of your clearance slip scattered around the building before I can sign anything for you.");
            }
            else
            {
                ShowDialogue("You cannot be cleared without an official clearance slip. Search the corridors for the 4 torn fragments first.");
            }
            return;
        }

        // Gate 2: Chronological order enforcement
        if (!PreviousNPCSigned())
        {
            Debug.Log($"[{npcName}] Previous signature not yet collected — blocked.");
            ShowDialogue(notYourTurnDialogue);
            return;
        }

        // Gate 3: Head Librarian (signatureIndex 0) requires Library Clearance Voucher from printer
        if (signatureIndex == 0 && libraryVoucherItem != null)
        {
            bool hasVoucher = PlayerHasItem(libraryVoucherItem);
            if (!hasVoucher)
            {
                if (LibraryPrinterInteract.Instance != null)
                {
                    LibraryPrinterInteract.Instance.UnlockPrinterQuest();
                }
                ShowDialogue("Keep your voice down, this is the university library. You want your clearance signed? The database flags your student number with an UNRESOLVED OVERDUE BORROWING VIOLATION from 1994. You must print your official Library Clearance Voucher at the workstation printer across the room. Bring me the printed voucher, or your clearance ends here.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Clear Library Overdue Record", "Print your Library Clearance Voucher from the printer station in Room 308.");
                }
                return;
            }
            else
            {
                // Take the voucher from player
                RemoveItemFromPlayer(libraryVoucherItem, 1);
                Debug.Log("[ClearanceNPC] Librarian accepted the Library Clearance Voucher!");
            }
        }

        bool isNew = ClearanceManager.Instance.GrantSignature(signatureIndex);

        if (isNew)
        {
            Debug.Log($"[ClearanceNPC] {npcName} signed. ({ClearanceManager.Instance.SignatureCount}/6)");
            ShowDialogue(unsignedDialogue);

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjectiveForSignature(signatureIndex + 1);
            }

            if (ClearanceManager.Instance.IsFullyClear())
                Debug.Log("[ClearanceNPC] All 6 signatures! Clearance complete!");
        }
        else
        {
            ShowDialogue(alreadySignedDialogue);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void GiveBlankSlipToPlayer()
    {
        if (blankPaperItem == null)
        {
            blankPaperItem = Resources.Load<InventoryItem>("Blank_Paper");
            if (blankPaperItem == null)
            {
#if UNITY_EDITOR
                blankPaperItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
#endif
            }
        }

        if (blankPaperItem != null)
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(blankPaperItem, 1);
            }
            else if (StorageManager.Instance != null)
            {
                StorageManager.Instance.AddItem(blankPaperItem, 1);
            }
            Debug.Log($"[{npcName}] Issued Blank_Paper clearance slip to player.");
        }
    }

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
        return PlayerHasItem(blankPaperItem);
    }

    /// <summary>True if the player is carrying the specified item in hotbar or bag storage.</summary>
    private bool PlayerHasItem(InventoryItem item)
    {
        if (item == null) return true;
        bool inHotbar = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(item);
        bool inStorage = StorageManager.Instance != null && StorageManager.Instance.HasItem(item);
        return inHotbar || inStorage;
    }

    /// <summary>Removes item from hotbar or bag storage.</summary>
    private void RemoveItemFromPlayer(InventoryItem item, int amount = 1)
    {
        if (item == null) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(item))
        {
            InventoryManager.Instance.RemoveItem(item, amount);
        }
        else if (StorageManager.Instance != null && StorageManager.Instance.HasItem(item))
        {
            StorageManager.Instance.RemoveItem(item, amount);
        }
    }

    private static Coroutine _activeDialogueCoroutine;

    private void ShowDialogue(string message)
    {
        Debug.Log($"[{npcName}] \"{message}\"");

        var staffAI = GetComponent<RoomStaffAI>();
        if (staffAI != null) staffAI.SetTalkingState(true);

        var proctorAI = GetComponent<ProctorAI>();
        if (proctorAI != null) proctorAI.SetTalkingState(true);

        // Find or setup on-screen dialogue UI on HudCanvas
        GameObject hud = GameObject.Find("HudCanvas");
        if (hud != null)
        {
            Transform diagTrans = hud.transform.Find("DialogueBox");
            UnityEngine.UI.Text diagText = null;
            if (diagTrans != null)
            {
                // Ensure existing scene DialogueBox is placed above the hotbar/inventory
                RectTransform rt = diagTrans.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.15f, 0.23f);
                    rt.anchorMax = new Vector2(0.85f, 0.37f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                // Add or configure subtle background panel if missing
                var bgImage = diagTrans.GetComponent<UnityEngine.UI.Image>();
                if (bgImage == null) bgImage = diagTrans.gameObject.AddComponent<UnityEngine.UI.Image>();
                bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);

                diagText = diagTrans.GetComponentInChildren<UnityEngine.UI.Text>();
            }
            else
            {
                // Create clean subtle dialogue panel above hotbar/inventory
                GameObject boxGO = new GameObject("DialogueBox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                boxGO.transform.SetParent(hud.transform, false);
                RectTransform rt = boxGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.15f, 0.23f);
                rt.anchorMax = new Vector2(0.85f, 0.37f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var bgImage = boxGO.GetComponent<UnityEngine.UI.Image>();
                bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);

                GameObject textGO = new GameObject("DialogueText", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
                textGO.transform.SetParent(boxGO.transform, false);
                RectTransform textRt = textGO.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(16, 8);
                textRt.offsetMax = new Vector2(-16, -8);

                diagText = textGO.GetComponent<UnityEngine.UI.Text>();
                diagText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                diagText.fontSize = 20;
                diagText.alignment = TextAnchor.MiddleCenter;
                diagText.color = new Color(1f, 0.96f, 0.85f, 1f); // Warm readable white

                var outline = textGO.GetComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                diagTrans = boxGO.transform;
            }

            if (diagText != null)
            {
                diagTrans.gameObject.SetActive(true);
                diagText.text = $"<b>[{npcName}]</b>\n\"{message}\"";

                if (_activeDialogueCoroutine != null)
                {
                    StopCoroutine(_activeDialogueCoroutine);
                }
                _activeDialogueCoroutine = StartCoroutine(HideDialogueRoutine(diagTrans.gameObject, 5.0f));
            }
        }
    }

    private System.Collections.IEnumerator HideDialogueRoutine(GameObject box, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (box != null)
        {
            box.SetActive(false);
        }

        var staffAI = GetComponent<RoomStaffAI>();
        if (staffAI != null) staffAI.SetTalkingState(false);

        var proctorAI = GetComponent<ProctorAI>();
        if (proctorAI != null) proctorAI.SetTalkingState(false);
    }

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
