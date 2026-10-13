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

    [Tooltip("Optional ComLab Accountability Form required for CCS Dean (signatureIndex 2).")]
    public InventoryItem comlabFormItem;

    [Tooltip("Optional University Seal Stamp required for Executive Vice President (signatureIndex 5).")]
    public InventoryItem executiveSealItem;

    [Header("Interaction Range")]
    [Tooltip("Maximum distance (in meters) the player can be from the NPC to interact.")]
    public float interactionRange = 4.0f;

    [Header("Dialogue")]
    [TextArea(2, 3)]
    public string unsignedDialogue      = "Akin na 'yang clearance slip mo, pipirmahan ko na.";
    [TextArea(2, 3)]
    public string alreadySignedDialogue = "Napirmahan ko na 'yang clearance mo kanina. Dumiretso ka na sa susunod na opisina.";
    [TextArea(2, 3)]
    public string notYourTurnDialogue   = "Teka lang, hindi pa puwede sa akin 'yan. Kailangan mo munang papirmahan sa naunang department bago kita asikasuhin.";

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
                    return $"[{npcName}] Ipasa ang Kompletong Clearance Slip (Pindutin ang E)";
                else
                    return $"[{npcName}] Naipasa na ang Clearance Slip (Pindutin ang E para kausapin)";
            }

            return $"[{npcName}] Pumirma na kanina (Pindutin ang E para kausapin)";
        }

        return $"Pindutin ang E para kausapin si {npcName}";
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
                ShowDialogue("Tingnan ko nga 'yang clearance slip mo... Library, Guidance & SAS, College of Computing Studies, Registrar, Cashier, at Executive Vice President. Ayos, kompleto at na-record na lahat ng anim na pirma sa system! Clearance complete ka na. Na-unlock na 'yung main gate ng campus doon sa lobby entrance. Mag-ingat ka sa paglabas!");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Escape The Campus", "The campus main gate at the lobby entrance is now unlocked. Escape the building!");
                }
                return;
            }
            else
            {
                ShowDialogue("Naipasa at opisyal nang na-record 'yang clearance mo kanina. Dumiretso ka na agad sa main gate doon sa lobby entrance para makalabas.");
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
                ShowDialogue("Psst! Hinaan mo 'yang boses mo, nasa university library ka! Nasaan ang clearance form mo? Hanapin mo muna 'yung apat na punit na piraso ng clearance slip mo na nagkalat sa building bago ako pumirma rito.");
            }
            else
            {
                ShowDialogue("Hindi kita maki-clear nang walang official clearance slip. Hanapin mo muna sa mga hallway 'yung apat na punit na piraso at buuin mo bago ka pumila rito.");
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
                ShowDialogue("Hinaan mo ang boses mo, library 'to! Magpapacheck ka ng clearance? May flag 'yang student number mo sa database—may UNRESOLVED OVERDUE BORROWING VIOLATION ka pa noong 1994! Kailangan mong i-print ang official Library Clearance Voucher mo doon sa printer station sa kabila. Dalhin mo rito sa akin 'yung voucher, kundi hindi kita pipirmahan.");
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
            var puzzle = GuidanceWordPuzzle.Instance ?? FindAnyObjectByType<GuidanceWordPuzzle>(FindObjectsInactive.Include);
            if (puzzle != null && !puzzle.IsSolved)
            {
                ShowDialogue("Tuloy ka sa Guidance and Counseling. Bago ko ma-clear ang moral conduct standing mo, kailangan mong patunayan na aligned ka sa core values ng ating institusyon. Ayusin mo 'yung nagkandalitong mga letra dito sa evaluation sheet... kung gusto mong makapasa.");
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

        // Gate 4.5: CCS Dean / Department Head (signatureIndex 2) requires ComLab Accountability Form from the 1st Floor ComLab
        if (signatureIndex == 2)
        {
            bool hasComLabForm = (comlabFormItem != null && PlayerHasItem(comlabFormItem)) || PlayerHasItemNamed("ComLab Accountability Form");
            if (!hasComLabForm)
            {
                ShowDialogue("College of Computing Studies. Bago ko mapirmahan 'yang Department Clearance mo, may flag pa ang terminal account mo sa database—may UNRESOLVED COMPUTER LAB TERMINAL HOLD ka! Iniwan ng ComLab custodian 'yung official ComLab Accountability Form mo sa 1st Floor Computer Lab (Room 105). Kunin mo muna doon sa desk at dalhin mo rito sa akin!");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("ComLab Terminal Hold", "Retrieve your ComLab Accountability Form from the 1st Floor Computer Lab in Room 105.");
                }
                return;
            }
            else
            {
                if (comlabFormItem != null && PlayerHasItem(comlabFormItem)) RemoveItemFromPlayer(comlabFormItem, 1);
                else RemoveItemNamed("ComLab Accountability Form", 1);
                Debug.Log("[ClearanceNPC] CCS Dean accepted the ComLab Accountability Form!");
            }
        }

        // Gate 5: University Registrar (signatureIndex 3) requires solving the Document Sort Puzzle
        if (signatureIndex == 3)
        {
            var sortPuzzle = RegistrarDocumentSortPuzzle.Instance ?? FindAnyObjectByType<RegistrarDocumentSortPuzzle>(FindObjectsInactive.Include);
            if (sortPuzzle != null && !sortPuzzle.IsSolved)
            {
                ShowDialogue("Window 2, University Registrar. Bago ko masuri at matatakan 'yang clearance mo, tambak pa ang kalat na student grade sheets dito sa archival desk. Ayusin at i-sort mo muna 'tong stack ng mga dokumento sa tamang archival trays para maging maayos ang records natin.");
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
            var cashierPuzzle = CashierBalancePuzzle.Instance ?? FindAnyObjectByType<CashierBalancePuzzle>(FindObjectsInactive.Include);
            if (cashierPuzzle != null && !cashierPuzzle.IsSolved)
            {
                ShowDialogue("Window 2, Cashier Department. Bago ko matatakan 'yang clearance slip mo, may pending unsettled fees ka pa sa records. Inusad ko 'yung assessment balance sheet mo rito sa window slot. Kuwentahin mo ang eksaktong total na dapat bayaran at i-submit mo para ma-clear ang payment status mo.");
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

        // Gate 7: Executive Vice President (signatureIndex 5) requires the Official University Seal Stamp from 2nd Floor Records Locker
        if (signatureIndex == 5)
        {
            bool hasSeal = (executiveSealItem != null && PlayerHasItem(executiveSealItem)) || PlayerHasItemNamed("Official University Seal Stamp");
            if (!hasSeal)
            {
                ShowDialogue("Office of the Executive Vice President. Maayos naman ang approvals ng bawat departamento, pero ayon sa protocol, kailangan munang maitatak ang Official University Seal bago ang final executive endorsement. Kunin mo muna ang University Seal Stamp sa Department Records Locker sa 2nd Floor Faculty Office!");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Executive Endorsement Seal", "Retrieve the Official University Seal Stamp from the Department Records Locker in the 2nd Floor Faculty Office.");
                }
                return;
            }
            else
            {
                if (executiveSealItem != null && PlayerHasItem(executiveSealItem)) RemoveItemFromPlayer(executiveSealItem, 1);
                else RemoveItemNamed("Official University Seal Stamp", 1);
                Debug.Log("[ClearanceNPC] EVP accepted the Official University Seal Stamp!");
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
            ShowDialogue("Napakahusay... Na-verify na ang commitment mo sa mga core values ng paaralan. Cleared ka na rito sa Guidance. Dumiretso ka na sa COLLEGE OF COMPUTING STUDIES sa Room 202 sa 2nd floor.");

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
            ShowDialogue("Nai-file at na-archive na nang maayos ang mga grade sheets. Approved na ang registrar clearance mo. Dumiretso ka na agad sa UNIVERSITY CASHIER sa Room 102 sa ground floor para ma-settle ang balance mo.");

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
            ShowDialogue("Bayad na nang buo! Zero balance na ang official assessment mo. Pumunta ka na sa OFFICE OF THE EXECUTIVE VICE PRESIDENT sa Room 103 para sa huling clearance signature mo.");

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

    private bool PlayerHasItemNamed(string partialName)
    {
        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        if (StorageManager.Instance != null)
        {
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private void RemoveItemNamed(string partialName, int amount = 1)
    {
        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, amount);
                    return;
                }
            }
        }
        if (StorageManager.Instance != null)
        {
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    StorageManager.Instance.RemoveItem(slot.item, amount);
                    return;
                }
            }
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

        var diagSystem = NPCDialogueSystem.Instance ?? FindAnyObjectByType<NPCDialogueSystem>();
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
