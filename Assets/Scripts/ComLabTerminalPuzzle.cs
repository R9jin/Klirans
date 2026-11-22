using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// ComLabTerminalPuzzle — Interactive database terminal workstation in Room 105.
/// Part of the College of Computing Studies (CCS Dean) clearance fetch quest.
///
/// Mechanics:
/// 1. The CRT monitor initially shows "NO INPUT SIGNAL - CHECK VGA CABLE [VGA-01]".
/// 2. Player must find the VGA Monitor Cable (inspected in 3D to reveal serial CCS-SRV-TERMINAL-01).
/// 3. Connecting the cable boots up the terminal with authentic green phosphor CRT scanlines.
/// 4. Terminal clears the student's terminal hold on the campus database and dispenses
///    the official ComLab Accountability Form.
/// </summary>
public class ComLabTerminalPuzzle : MonoBehaviour, IInteractable
{
    public static ComLabTerminalPuzzle Instance { get; private set; }

    [Header("Terminal State")]
    public bool isCableConnected = false;
    public bool isHoldCleared = false;

    [Header("Item References")]
    [Tooltip("The VGA Monitor Cable item required to connect display.")]
    public InventoryItem vgaCableItem;

    [Tooltip("The ComLab Accountability Form dispensed upon clearing terminal hold.")]
    public InventoryItem comlabFormItem;

    [Tooltip("Reference to the world pickup for the ComLab Clearance Form (enabled if inventory is full).")]
    public GameObject physicalFormPickup;

    [Header("Visual References")]
    [Tooltip("Renderer of the CRT monitor screen.")]
    public Renderer screenRenderer;

    [Tooltip("Status light on terminal workstation.")]
    public Light terminalLight;

    [Header("Audio")]
    public AudioClip cablePlugAudio;
    public AudioClip crtHumAudio;
    public AudioClip printDispenseAudio;

    private AudioSource _audioSource;
    private Texture2D _noSignalTex;
    private Texture2D _bootedTex;
    private Material _screenMaterialInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.8f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 15f;
            _audioSource.playOnAwake = false;
        }

        LoadScreenTextures();

#if UNITY_EDITOR
        if (vgaCableItem == null)
            vgaCableItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/VGA_Cable.asset");
        if (comlabFormItem == null)
            comlabFormItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/ComLab_Clearance_Form.asset");
        if (cablePlugAudio == null)
            cablePlugAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (crtHumAudio == null)
            crtHumAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/printer_operation.wav");
        if (printDispenseAudio == null)
            printDispenseAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/xeroxSoundEffect_Print.wav");
#endif

        FindScreenRendererIfNeeded();
        ApplyScreenVisuals(isCableConnected);
    }

    private void Start()
    {
        EnsurePhysicalFormPickupCached();
    }

    private void EnsurePhysicalFormPickupCached()
    {
        if (physicalFormPickup == null)
        {
            var container = GameObject.Find("QuestPickupsContainer");
            if (container != null)
            {
                var child = container.transform.Find("Pickup_ComLabClearanceForm");
                if (child != null) physicalFormPickup = child.gameObject;
            }
            if (physicalFormPickup == null)
            {
                physicalFormPickup = GameObject.Find("Pickup_ComLabClearanceForm");
            }
        }
        if (physicalFormPickup != null && !isHoldCleared)
        {
            physicalFormPickup.SetActive(false);
        }
    }

    private bool IsGridPowerRestored()
    {
        if (GameModeManager.IsFreeRoamMode) return true;
        if (CircuitBreakerPuzzle.Instance != null)
        {
            return CircuitBreakerPuzzle.Instance.isPowerRestored;
        }
        return true;
    }

    private bool IsPlayerAuthorizedForComLabQuest()
    {
        if (GameModeManager.IsFreeRoamMode) return true;
        // Player should at least have unlocked or completed Guidance (Signature 1)
        if (ClearanceManager.Instance != null)
        {
            return ClearanceManager.Instance.HasSignature(1);
        }
        return true;
    }

    private void LoadScreenTextures()
    {
        _noSignalTex = LoadTexture("Assets/Textures/PuzzleTextures/ComLab_Screen_NoSignal.png");
        _bootedTex = LoadTexture("Assets/Textures/PuzzleTextures/ComLab_Screen_Booted.png");
    }

    private Texture2D LoadTexture(string relativePath)
    {
        Texture2D tex = null;
#if UNITY_EDITOR
        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        if (tex != null) return tex;
#endif
        string fullPath = Path.Combine(Application.dataPath, "..", relativePath).Replace('\\', '/');
        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = Path.GetFileNameWithoutExtension(relativePath);
                    return tex;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ComLabTerminalPuzzle] Failed to load texture from {fullPath}: {ex.Message}");
            }
        }
        return tex;
    }

    private void FindScreenRendererIfNeeded()
    {
        if (screenRenderer != null) return;

        // Search for named CRTScreen child
        var screenTrans = transform.Find("CRTMonitor/CRTScreen") ?? transform.Find("CRTScreen");
        if (screenTrans != null)
        {
            screenRenderer = screenTrans.GetComponent<Renderer>();
            return;
        }

        // Search all renderers for one named "screen" or "crt"
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("screen") || n.Contains("display") || n.Contains("crt"))
            {
                screenRenderer = r;
                return;
            }
        }
    }

    private void ApplyScreenVisuals(bool booted)
    {
        if (screenRenderer == null) return;

        if (_screenMaterialInstance == null)
        {
            _screenMaterialInstance = new Material(screenRenderer.sharedMaterial);
            _screenMaterialInstance.name = "CRTScreen_RuntimeMat";
            screenRenderer.material = _screenMaterialInstance;
        }

        bool powered = IsGridPowerRestored();
        if (!powered)
        {
            _screenMaterialInstance.SetColor("_EmissionColor", Color.black);
            if (terminalLight != null) terminalLight.enabled = false;
            return;
        }

        Texture2D activeTex = booted ? _bootedTex : _noSignalTex;
        if (activeTex != null)
        {
            _screenMaterialInstance.mainTexture = activeTex;
            _screenMaterialInstance.EnableKeyword("_EMISSION");
            _screenMaterialInstance.SetTexture("_EmissionMap", activeTex);

            Color emColor = booted
                ? new Color(0.12f, 0.45f, 0.20f, 1f)   // Phosphor Green glow
                : new Color(0.35f, 0.18f, 0.05f, 1f);  // Amber standby glow
            _screenMaterialInstance.SetColor("_EmissionColor", emColor);
        }

        if (terminalLight != null)
        {
            terminalLight.enabled = booted;
            terminalLight.color = new Color(0.20f, 0.95f, 0.45f, 1f);
            terminalLight.intensity = booted ? 1.4f : 0f;
            terminalLight.range = 2.5f;
        }
    }

    public string GetPrompt()
    {
        if (isHoldCleared)
        {
            return "[ComLab Terminal] (Terminal Hold Na-clear Na - Dalhin ang Form sa CCS Dean)";
        }

        if (!IsPlayerAuthorizedForComLabQuest())
        {
            return "[ComLab Terminal] (AWAITING CLEARANCE AUTHORIZATION - Kumausap muna sa CCS Dean sa Room 202)";
        }

        if (!IsGridPowerRestored())
        {
            return "[ComLab Terminal] (WALANG KURYENTE - Ibalik ang kuryente gamit ang 15A Fuse sa Main Breaker Box sa 2nd Floor)";
        }

        if (!isCableConnected)
        {
            bool hasCable = PlayerHasVGACable();
            bool knowsSerial = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("VGACableSerial");

            if (hasCable)
            {
                return knowsSerial
                    ? "[E] Ikabit ang VGA Cable (CCS-SRV-TERMINAL-01) at I-boot ang Terminal"
                    : "[E] Ikabit ang VGA Monitor Cable";
            }
            return "[ComLab Terminal] (NO SIGNAL - Kailangan ng VGA Monitor Cable)";
        }

        return "[E] I-clear ang ComLab Terminal Hold";
    }

    public void Interact()
    {
        if (isHoldCleared)
        {
            Debug.Log("[ComLabTerminalPuzzle] Terminal hold is already resolved.");
            return;
        }

        if (!IsPlayerAuthorizedForComLabQuest())
        {
            if (PlayerThoughtVoice.Instance != null)
            {
                PlayerThoughtVoice.Instance.ShowThought("Naka-lock ang terminal sa database. Kailangan ko munang kausapin ang CCS Dean sa Room 202.");
            }
            return;
        }

        if (!IsGridPowerRestored())
        {
            PlayAudio(cablePlugAudio, 0.5f, 0.8f);
            if (PlayerThoughtVoice.Instance != null)
            {
                PlayerThoughtVoice.Instance.ShowThought("Patay ang monitor... Walang kuryente. Kailangan kong ikabit ang 15A Fuse sa Main Breaker Box sa 2nd floor.");
            }
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Ibalik ang Kuryente sa ComLab",
                    "Pumunta sa Main Breaker Box sa 2nd Floor at magkabit ng 15A Ceramic Fuse upang magkaroon ng kuryente ang ComLab 1."
                );
            }
            return;
        }

        if (!isCableConnected)
        {
            if (PlayerHasVGACable())
            {
                ConnectCable();
            }
            else
            {
                PlayAudio(cablePlugAudio, 0.7f, 0.9f);
                if (ObjectiveHUD.Instance != null)
                {
                    ObjectiveHUD.Instance.SetObjective(
                        "ComLab Terminal: Walang Signal",
                        "Hanapin ang VGA Monitor Cable sa paligid ng ComLab 1 o maintenance shelves upang maikabit ang display."
                    );
                }
            }
            return;
        }

        // Cable connected, run clearing sequence
        StartCoroutine(ResolveHoldSequence());
    }

    private void ConnectCable()
    {
        isCableConnected = true;
        ConsumeVGACable();
        PlayAudio(cablePlugAudio, 0.9f, 1.1f);
        PlayAudio(crtHumAudio, 0.8f, 1.0f);

        StartCoroutine(BootSequenceRoutine());
    }

    private IEnumerator BootSequenceRoutine()
    {
        // 1. CRT Degauss flash
        if (_screenMaterialInstance != null)
        {
            _screenMaterialInstance.SetColor("_EmissionColor", Color.white * 0.8f);
            yield return new WaitForSeconds(0.12f);
            _screenMaterialInstance.SetColor("_EmissionColor", Color.black);
            yield return new WaitForSeconds(0.08f);
            _screenMaterialInstance.SetColor("_EmissionColor", new Color(0.12f, 0.45f, 0.20f));
        }

        ApplyScreenVisuals(true);

        Debug.Log("[ComLabTerminalPuzzle] VGA cable connected! CRT monitor powered on.");
        yield return StartCoroutine(ResolveHoldSequence());
    }

    private IEnumerator ResolveHoldSequence()
    {
        isHoldCleared = true;

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Terminal Booting: Resolving Hold...",
                "PNC NETWORK TERMINAL v3.2 ONLINE — Kinakansela ang terminal hold sa database..."
            );
        }

        yield return new WaitForSeconds(1.8f);

        PlayAudio(printDispenseAudio, 0.9f, 1.0f);

        // Grant ComLab Accountability Form to player
        bool added = false;
        if (comlabFormItem != null)
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.HasFreeSlot())
                added = InventoryManager.Instance.AddItem(comlabFormItem, 1);
            if (!added && StorageManager.Instance != null)
                added = StorageManager.Instance.AddItem(comlabFormItem, 1);

            if (added)
            {
                // Open 3D inspection for the newly acquired form!
                ItemInspectionViewer3D.Inspect(comlabFormItem);
            }
            else
            {
                // Inventory is full: reveal physical pickup on workstation desk!
                EnsurePhysicalFormPickupCached();
                if (physicalFormPickup != null)
                {
                    physicalFormPickup.SetActive(true);
                    var highlight = physicalFormPickup.GetComponent<ItemVisualHighlight>();
                    if (highlight == null) highlight = physicalFormPickup.AddComponent<ItemVisualHighlight>();
                }
                if (PlayerThoughtVoice.Instance != null)
                {
                    PlayerThoughtVoice.Instance.ShowThought("Puno ang aking bag! Iniluwa ng printer ang ComLab Form sa ibabaw ng mesa.");
                }
            }
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "ComLab Clearance Form Nakuha!",
                "Dalhin ang opisyal na form sa CCS Dean sa Room 202 para sa ika-2 clearance signature."
            );
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.TriggerAutosave("ComLab Form Printed");
        }

        Debug.Log("[ComLabTerminalPuzzle] Terminal hold cleared and ComLab form dispensed!");
    }

    private bool PlayerHasVGACable()
    {
        if (InventoryManager.Instance != null)
        {
            if (vgaCableItem != null && InventoryManager.Instance.HasItem(vgaCableItem)) return true;
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("VGA", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        if (StorageManager.Instance != null)
        {
            if (vgaCableItem != null && StorageManager.Instance.HasItem(vgaCableItem)) return true;
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("VGA", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private void ConsumeVGACable()
    {
        if (InventoryManager.Instance != null)
        {
            if (vgaCableItem != null && InventoryManager.Instance.HasItem(vgaCableItem))
            {
                InventoryManager.Instance.RemoveItem(vgaCableItem, 1);
                return;
            }
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("VGA", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }
        if (StorageManager.Instance != null)
        {
            if (vgaCableItem != null && StorageManager.Instance.HasItem(vgaCableItem))
            {
                StorageManager.Instance.RemoveItem(vgaCableItem, 1);
                return;
            }
        }
    }

    private void PlayAudio(AudioClip clip, float vol, float pitch)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, vol);
        }
    }

    public void RestoreTerminalState(bool cableConnected, bool holdCleared)
    {
        isCableConnected = cableConnected;
        isHoldCleared = holdCleared;
        FindScreenRendererIfNeeded();
        ApplyScreenVisuals(isCableConnected);
        if (physicalFormPickup != null)
        {
            physicalFormPickup.SetActive(isHoldCleared);
        }
    }
}
