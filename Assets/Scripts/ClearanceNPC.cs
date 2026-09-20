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

    [Header("Voice & Audio")]
    [Tooltip("Crazy mumbles audio clip played when talking to this clearance NPC.")]
    public AudioClip mumbleAudioClip;

    // ── Runtime cache ──────────────────────────────────────────────────────────
    private Transform _playerTransform;
    private AudioSource _dialogueAudioSource;

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

        EnsureVoiceAudioSource();
    }

    private void EnsureVoiceAudioSource()
    {
        if (_dialogueAudioSource == null)
        {
            Transform existing = transform.Find("DialogueVoiceAudio");
            if (existing != null)
            {
                _dialogueAudioSource = existing.GetComponent<AudioSource>();
            }
            else
            {
                GameObject voiceObj = new GameObject("DialogueVoiceAudio");
                voiceObj.transform.SetParent(transform, false);
                _dialogueAudioSource = voiceObj.AddComponent<AudioSource>();
            }

            _dialogueAudioSource.spatialBlend = 0.45f;
            _dialogueAudioSource.minDistance = 2f;
            _dialogueAudioSource.maxDistance = 16f;
            _dialogueAudioSource.playOnAwake = false;
            _dialogueAudioSource.volume = 0.85f;
        }

        if (mumbleAudioClip == null)
        {
            mumbleAudioClip = Resources.Load<AudioClip>("freesound_community-human_male_crazy-mumbles_1-30950");
#if UNITY_EDITOR
            if (mumbleAudioClip == null)
            {
                mumbleAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
            }
#endif
        }
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
            if (ObjectiveHUD.Instance != null && signatureIndex + 1 <= 5)
            {
                ObjectiveHUD.Instance.SetObjectiveForSignature(signatureIndex + 1);
            }
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

        // Gate 4: Guidance Counselor (signatureIndex 1) requires solving the Core Values Scrambled Word Puzzle
        if (signatureIndex == 1)
        {
            var puzzle = GuidanceWordPuzzle.Instance ?? FindObjectOfType<GuidanceWordPuzzle>(true);
            if (puzzle != null && !puzzle.IsSolved)
            {
                ShowDialogue("Welcome to Guidance and Counseling. To clear your moral conduct standing, you must demonstrate alignment with our sacred institutional pillars. Unscramble the core value letters on this evaluation sheet... or your journey ends here.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Guidance Core Values Puzzle", "Unscramble the PnC Core Value letters on the guidance assessment sheet in Room 207.");
                }
                puzzle.OpenPuzzle(0);
                return;
            }
            else if (puzzle == null)
            {
                Debug.LogError("[ClearanceNPC] GuidanceWordPuzzle could not be found in scene!");
                return;
            }
        }

        // Gate 5: University Registrar (signatureIndex 3) requires solving the Document Sort Puzzle
        if (signatureIndex == 3)
        {
            var sortPuzzle = RegistrarDocumentSortPuzzle.Instance ?? FindObjectOfType<RegistrarDocumentSortPuzzle>(true);
            if (sortPuzzle != null && !sortPuzzle.IsSolved)
            {
                ShowDialogue("Window 2, University Registrar. Before I can evaluate and stamp your clearance slip, our archival desk is backed up with disorganized student grade sheets. Sort this stack of official documents into their correct archival trays so our records remain in order.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Registrar Document Sorting", "Sort the stack of student grade records into the correct trays at Window 2 of the University Registrar in Room 104.");
                }
                sortPuzzle.OpenPuzzle();
                return;
            }
            else if (sortPuzzle == null)
            {
                Debug.LogError("[ClearanceNPC] RegistrarDocumentSortPuzzle could not be found in scene!");
                return;
            }
        }

        // Gate 6: University Cashier (signatureIndex 4) requires solving the Balance Sheet Math Puzzle
        if (signatureIndex == 4)
        {
            var cashierPuzzle = CashierBalancePuzzle.Instance ?? FindObjectOfType<CashierBalancePuzzle>(true);
            if (cashierPuzzle != null && !cashierPuzzle.IsSolved)
            {
                ShowDialogue("Window 2, Cashier Department. Before I can clear and stamp your clearance slip, our records show pending unsettled fees. I have slid your assessment balance sheet through the window slot. Calculate the exact total due and submit it to clear your payment status.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Cashier Balance Sheet", "Calculate and submit the unsettled balance total at Window 2 of the University Cashier in Room 102.");
                }
                cashierPuzzle.OpenPuzzle();
                return;
            }
            else if (cashierPuzzle == null)
            {
                Debug.LogError("[ClearanceNPC] CashierBalancePuzzle could not be found in scene!");
                return;
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

    /// <summary>
    /// Called by GuidanceWordPuzzle when the player successfully unscrambles the PnC Core Value word.
    /// </summary>
    public void OnGuidancePuzzleCompleted()
    {
        if (signatureIndex != 1) return;

        bool isNew = ClearanceManager.Instance != null && ClearanceManager.Instance.GrantSignature(signatureIndex);
        if (isNew)
        {
            Debug.Log($"[ClearanceNPC] Guidance Counselor signed. ({ClearanceManager.Instance.SignatureCount}/6)");
            ShowDialogue("Remarkable... Your commitment to our institutional pillars has been verified. Your guidance clearance is granted.");

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjectiveForSignature(signatureIndex + 1);
            }
        }
    }

    /// <summary>
    /// Called by RegistrarDocumentSortPuzzle when the player successfully sorts the stack of student grade records.
    /// </summary>
    public void OnRegistrarPuzzleCompleted()
    {
        if (signatureIndex != 3) return;

        bool isNew = ClearanceManager.Instance != null && ClearanceManager.Instance.GrantSignature(signatureIndex);
        if (isNew)
        {
            Debug.Log($"[ClearanceNPC] Registrar signed. ({ClearanceManager.Instance.SignatureCount}/6)");
            ShowDialogue("All grade sheets properly filed and archived. Your registrar clearance is officially approved. Proceed to the UNIVERSITY CASHIER in Room 102 on the ground floor to settle any outstanding balances.");

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjectiveForSignature(signatureIndex + 1);
            }
        }
    }

    /// <summary>
    /// Called by CashierBalancePuzzle when the player successfully calculates and clears the balance sheet fees.
    /// </summary>
    public void OnCashierPuzzleCompleted()
    {
        if (signatureIndex != 4) return;

        bool isNew = ClearanceManager.Instance != null && ClearanceManager.Instance.GrantSignature(signatureIndex);
        if (isNew)
        {
            Debug.Log($"[ClearanceNPC] Cashier signed. ({ClearanceManager.Instance.SignatureCount}/6)");
            ShowDialogue("Payment cleared in full! Your official assessment is marked zero balance. Proceed to the OFFICE OF THE EXECUTIVE VICE PRESIDENT in Room 103 for your final clearance signature.");

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjectiveForSignature(signatureIndex + 1);
            }
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

        var sitCtrl = GetComponent<NPCSitController>();
        if (sitCtrl != null) sitCtrl.OnInteract();

        // Play crazy mumble voice audio when speaking
        EnsureVoiceAudioSource();
        if (_dialogueAudioSource != null && mumbleAudioClip != null)
        {
            _dialogueAudioSource.clip = mumbleAudioClip;
            _dialogueAudioSource.pitch = 0.85f + (signatureIndex % 6) * 0.08f;
            float maxStart = Mathf.Max(0f, mumbleAudioClip.length - 4.5f);
            _dialogueAudioSource.time = Random.Range(0f, maxStart);
            _dialogueAudioSource.volume = 0.85f;
            _dialogueAudioSource.loop = true;
            _dialogueAudioSource.Play();
        }

        var diagSystem = NPCDialogueSystem.Instance ?? FindObjectOfType<NPCDialogueSystem>();
        if (diagSystem != null)
        {
            diagSystem.StartDialogue(this, message);
        }
    }

    public void StopVoiceAudio()
    {
        if (_dialogueAudioSource != null && _dialogueAudioSource.isPlaying)
        {
            _dialogueAudioSource.Stop();
        }

        var staffAI = GetComponent<RoomStaffAI>();
        if (staffAI != null) staffAI.SetTalkingState(false);

        var proctorAI = GetComponent<ProctorAI>();
        if (proctorAI != null) proctorAI.SetTalkingState(false);

        var sitCtrl = GetComponent<NPCSitController>();
        if (sitCtrl != null) sitCtrl.OnEndInteract();
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
