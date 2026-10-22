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

    [Tooltip("Lockpin handed to the player by CCS Dean (signatureIndex 2) to pick ComLab.")]
    public InventoryItem lockpinItem;

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
        if (interactionRange < 4.2f) interactionRange = 4.2f;

        // Solid physical collider — NPC collides with the player
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        EnsureStaffInteractionZone();
        EnsureVoiceAudioSource();
    }

    /// <summary>
    /// Creates a generous trigger interaction volume encompassing the staff NPC and the space
    /// across their counter/desk/window so the player can easily interact without pixel-hunting.
    /// </summary>
    private void EnsureStaffInteractionZone()
    {
        Transform zoneT = transform.Find("StaffInteractionZone");
        GameObject zoneGO;
        if (zoneT != null)
        {
            zoneGO = zoneT.gameObject;
        }
        else
        {
            zoneGO = new GameObject("StaffInteractionZone");
            zoneGO.transform.SetParent(transform, false);
        }

        zoneGO.layer = gameObject.layer;
        zoneGO.transform.localPosition = Vector3.zero;
        zoneGO.transform.localRotation = Quaternion.identity;
        zoneGO.transform.localScale = Vector3.one;

        var box = zoneGO.GetComponent<BoxCollider>();
        if (box == null) box = zoneGO.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.05f, 0.35f);
        box.size = new Vector3(1.85f, 2.20f, 2.40f);
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
        // Special case: Chasing runaway Registrar in hallway
        if (signatureIndex == 3)
        {
            var staffAI = GetComponent<RoomStaffAI>();
            if (staffAI != null && staffAI.isRunawayActive)
            {
                float chaseDist = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 99f;
                if (chaseDist <= 5.5f) return "[E] Habulin at Pakalmahin si Registrar!";
            }
        }

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
        // Distance gate (allow wider range during hallway chase)
        if (_playerTransform != null)
        {
            float maxRange = interactionRange;
            if (signatureIndex == 3)
            {
                var staffAI = GetComponent<RoomStaffAI>();
                if (staffAI != null && staffAI.isRunawayActive) maxRange = 5.5f;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > maxRange) return;
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

        // Gate 4: Guidance Counselor (signatureIndex 1) — Step 1: Tape Fetch, Step 2: Core Values Puzzle
        if (signatureIndex == 1)
        {
            bool hasTape = PlayerHasItemNamed("Counseling Audio Cassette Tape") || PlayerHasItemNamed("Cassette Tape") || PlayerHasItemNamed("Tape") || PlayerHasItemNamed("Cassette");
            if (!hasTape && !ClearanceManager.Instance.GuidanceTapeDelivered)
            {
                ShowDialogue("Tuloy ka sa Guidance & Counseling. Bago kita i-evaluate, kailangan nating suriin ang iyong Guidance Counseling Case File Audio Tape. Iniwan ito ng intern sa 2nd Floor Clinic desk o Guidance storage shelf. Hanapin at dalhin mo rito sa akin bago tayo magpatuloy.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Hanapin ang Counseling Tape", "Hanapin ang Counseling Audio Cassette Tape sa 2nd Floor Clinic desk o Guidance shelf at dalhin sa Counselor sa Room 207.");
                }
                return;
            }
            else if (hasTape && !ClearanceManager.Instance.GuidanceTapeDelivered)
            {
                ClearanceManager.Instance.GuidanceTapeDelivered = true;
                RemoveItemNamed("Counseling Audio Cassette Tape", 1);
                RemoveItemNamed("Cassette Tape", 1);
                RemoveItemNamed("Tape", 1);
                RemoveItemNamed("Cassette", 1);
                Debug.Log("[ClearanceNPC] Guidance Counselor accepted the Counseling Tape!");
            }

            var puzzle = GuidanceWordPuzzle.Instance ?? FindAnyObjectByType<GuidanceWordPuzzle>(FindObjectsInactive.Include);
            if (puzzle != null && !puzzle.IsSolved)
            {
                ShowDialogue("Salamat at nahanap mo ang case file tape. Ngayon, para sa final evaluation ng moral standing mo, patunayan mong kabisado mo ang ating PnC Core Values. Ayusin mo ang nagkandalitong mga letra dito sa assessment sheet.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Guidance Core Values Puzzle", "Ayusin ang nagkandalitong letra ng PnC Core Values sa Guidance assessment sheet sa Room 207.");
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

        // Gate 4.5: CCS Dean / Department Head (signatureIndex 2) — Requires ComLab 105 Power/Access & Accountability Form
        if (signatureIndex == 2)
        {
            bool hasComLabForm = (comlabFormItem != null && PlayerHasItem(comlabFormItem)) || PlayerHasItemNamed("ComLab Accountability Form");
            if (!hasComLabForm)
            {
                if (lockpinItem == null)
                {
#if UNITY_EDITOR
                    lockpinItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Lockpin.asset");
#endif
                    if (lockpinItem == null) lockpinItem = Resources.Load<InventoryItem>("Lockpin");
                }

                bool alreadyHasLockpin = LockpickingMinigame.HasLockpin(lockpinItem);
                if (!alreadyHasLockpin && lockpinItem != null)
                {
                    if (InventoryManager.Instance != null && InventoryManager.Instance.HasFreeSlot())
                    {
                        InventoryManager.Instance.AddItem(lockpinItem, 1);
                        ShowDialogue("College of Computing Studies. Bago ko mapirmahan 'yang Department Clearance mo, may unresolved hold pa ang account mo sa database! Naka-lock ang ComLab (Room 105 / 106) at patid ang kuryente sa Slot 2 ng Main Breaker. Eto ang Lockpin ng departamento para mabuksan mo ang pinto. Kumuha ka rin ng 15A Ceramic Fuse sa 1F Lobby Security Counter para maibalik ang kuryente sa Breaker, at dalhin mo sa akin ang ComLab Accountability Form mula sa lab!");
                        if (ObjectiveHUD.Instance != null)
                        {
                            ObjectiveHUD.Instance.SetObjective("ComLab Terminal Hold", "Gamitin ang natanggap na Lockpin mula kay Dean para buksan ang ComLab, ibalik ang kuryente gamit ang 15A Fuse sa Breaker, at kunin ang Form.");
                        }
                    }
                    else
                    {
                        ShowDialogue("College of Computing Studies. Kailangan mong buksan ang ComLab pero puno ang iyong bag! Mag-drop ng gamit gamit ang [G] para maibigay ko sa 'yo ang Lockpin!");
                        if (ObjectiveHUD.Instance != null)
                        {
                            ObjectiveHUD.Instance.SetObjective("Puno ang Bag para sa Lockpin", "Pindutin ang [G] para mag-drop ng item sa sahig upang matanggap ang Lockpin mula kay Dean.");
                        }
                    }
                }
                else
                {
                    ShowDialogue("Hawak mo na ang Lockpin ng departamento! Pumunta sa 1st Floor ComLab (Room 105 / 106), buksan ang pinto, ikabit ang 15A Ceramic Fuse sa Main Breaker para magka-ilaw ang terminal, at kunin ang ComLab Accountability Form!");
                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective("Kunin ang ComLab Form", "Bumaba sa 1F ComLab (Room 105/106), gamitin ang Lockpin, ibalik ang kuryente sa Breaker, at kunin ang Form sa desk.");
                    }
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

        // Gate 5: University Registrar (signatureIndex 3) — Step 1: Runaway Chase, Step 2: Document Sort Puzzle
        if (signatureIndex == 3)
        {
            var staffAI = GetComponent<RoomStaffAI>();
            if (staffAI != null && !ClearanceManager.Instance.RegistrarChasedAndCalmed)
            {
                if (!staffAI.isRunawayActive)
                {
                    staffAI.TriggerRunaway(new Vector3(-84.8f, 2.35f, 22.0f));
                    if (PlayerThoughtVoice.Instance != null) PlayerThoughtVoice.Instance.OnRegistrarFled();
                    ShowDialogue("N-Narinig mo ba 'yung mga yabag sa pasilyo?! May halimaw na naglalakad sa dilim! WAAAAH! SARADO NA ANG REGISTRAR! HUWAG MO AKONG LAPITAN!");
                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective("HABULIN ANG REGISTRAR!", "Nag-panic at tumakbo sa pasilyo ang Registrar! Habulin siya sa hallway at pindutin ang [E] para pakalmahin!");
                    }
                    return;
                }
                else
                {
                    // Player caught up to fleeing staff in hallway!
                    staffAI.CatchStaff();
                    ClearanceManager.Instance.RegistrarChasedAndCalmed = true;
                    ShowDialogue("H-Haaaa... Diyos ko po! Ikaw lang pala 'yan... Akala ko kung anong halimaw na 'yung sumugod sa opisina ko! Sobrang takot na ako sa blackout na 'to. Tara, bumalik tayo sa loob ng window bago may makakita sa atin.");
                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective("Bumalik sa Registrar Window", "Bumalik sa loob ng Room 104 Registrar Window 2 para asikasuhin ang mga student records.");
                    }
                    return;
                }
            }

            var sortPuzzle = RegistrarDocumentSortPuzzle.Instance ?? FindAnyObjectByType<RegistrarDocumentSortPuzzle>(FindObjectsInactive.Include);
            if (sortPuzzle != null && !sortPuzzle.IsSolved)
            {
                ShowDialogue("Dahil sa takot ko kanina, nagkandalatag ang mga records sa desk! Ayusin at i-sort mo muna 'tong stack ng mga student grade records sa tamang trays para ma-verify ko ang credentials mo.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Registrar Document Sorting", "I-sort ang stack ng student grade records sa tamang archival trays sa Window 2 ng University Registrar sa Room 104.");
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

        // Gate 6: University Cashier (signatureIndex 4) — Step 1: Peso Coin Fee, Step 2: Balance Sheet Math Puzzle
        if (signatureIndex == 4)
        {
            bool hasPaidFee = ClearanceManager.Instance.CashierFeePaid;
            bool hasCoin = PlayerHasItemNamed("Peso Coin") || PlayerHasItemNamed("Coin") || (CoinManager.Instance != null && CoinManager.Instance.HasCoins(5));

            if (!hasPaidFee)
            {
                if (!hasCoin)
                {
                    ShowDialogue("Window 2, Cashier Department. May pending clearance processing fee ka pa sa records. Hindi kita puwedeng kuwentahan nang walang physical deposit. Hanapin mo ang lumang Peso Coin sa student lounge o mesa sa hallway at dalhin dito sa window slot.");
                    if (ObjectiveHUD.Instance != null)
                    {
                        ObjectiveHUD.Instance.SetObjective("Bayaran ang Cashier Fee", "Hanapin ang Peso Coin sa Ground Floor Student Lounge o hallway at ibayad sa Cashier Window 2 sa Room 102.");
                    }
                    return;
                }
                else
                {
                    ClearanceManager.Instance.CashierFeePaid = true;
                    if (PlayerHasItemNamed("Peso Coin")) RemoveItemNamed("Peso Coin", 1);
                    else if (PlayerHasItemNamed("Coin")) RemoveItemNamed("Coin", 1);
                    else if (CoinManager.Instance != null) CoinManager.Instance.SpendCoins(5);
                    Debug.Log("[ClearanceNPC] Cashier accepted clearance deposit fee!");
                }
            }

            var cashierPuzzle = CashierBalancePuzzle.Instance ?? FindAnyObjectByType<CashierBalancePuzzle>(FindObjectsInactive.Include);
            if (cashierPuzzle != null && !cashierPuzzle.IsSolved)
            {
                ShowDialogue("Tinanggap ko na ang fee deposit mo. Ngayon, kuwentahin mo ang eksaktong total ng unsettled balance sa assessment sheet para ma-verify sa official receipt ledger.");
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective("Cashier Balance Sheet", "Kuwentahin at i-submit ang eksaktong total ng unsettled fees sa Cashier Window 2 sa Room 102.");
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
    public static bool PlayerHasBlankSlip()
    {
        var blankItem = Resources.Load<InventoryItem>("Blank_Paper");
#if UNITY_EDITOR
        if (blankItem == null)
            blankItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
#endif
        if (blankItem != null && InventoryManager.Instance != null && InventoryManager.Instance.HasItem(blankItem))
            return true;
        if (InventoryManager.Instance != null && (InventoryManager.Instance.HasItemNamed("Clearance Slip") || InventoryManager.Instance.HasItemNamed("Blank_Paper")))
            return true;
        return false;
    }

    /// <summary>True if the player is carrying the specified item in the 6-slot hotbar.</summary>
    private bool PlayerHasItem(InventoryItem item)
    {
        if (item == null) return true;
        return InventoryManager.Instance != null && InventoryManager.Instance.HasItem(item);
    }

    /// <summary>Removes item from the 6-slot hotbar.</summary>
    private void RemoveItemFromPlayer(InventoryItem item, int amount = 1)
    {
        if (item == null) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(item))
        {
            InventoryManager.Instance.RemoveItem(item, amount);
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
