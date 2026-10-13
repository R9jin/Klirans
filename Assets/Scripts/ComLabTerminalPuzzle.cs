using System.Collections;
using UnityEngine;

/// <summary>
/// ComLabTerminalPuzzle — Interactive database terminal workstation in Room 105.
/// Part of the College of Computing Studies (CCS Dean) clearance fetch quest.
///
/// Mechanics:
/// 1. The monitor terminal initially has "NO SIGNAL - CABLE DISCONNECTED".
/// 2. Player must find the VGA Monitor Cable (inspected in 3D to reveal serial CCS-SRV-TERMINAL-01).
/// 3. Connecting the cable boots up the terminal with authentic green CRT/MS-DOS animation.
/// 4. Terminal clears the student's terminal hold and dispenses the ComLab Accountability Form.
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
    }

    public string GetPrompt()
    {
        if (isHoldCleared)
        {
            return "[ComLab Terminal] (Terminal Hold Na-clear Na - Kunin ang Form sa CCS Dean)";
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
                        "Hanapin ang VGA Monitor Cable sa paligid ng Room 105 o maintenance shelves upang maikabit ang display."
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

        if (terminalLight != null)
        {
            terminalLight.color = Color.green;
            terminalLight.intensity = 1.2f;
        }

        Debug.Log("[ComLabTerminalPuzzle] VGA cable connected! CRT monitor powered on.");
        StartCoroutine(ResolveHoldSequence());
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
            if (InventoryManager.Instance != null) added = InventoryManager.Instance.AddItem(comlabFormItem, 1);
            if (!added && StorageManager.Instance != null) added = StorageManager.Instance.AddItem(comlabFormItem, 1);

            // Open 3D inspection for the newly acquired form!
            ItemInspectionViewer3D.Inspect(comlabFormItem);
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "ComLab Clearance Form Nakuha!",
                "Dalhin ang opisyal na form sa CCS Dean sa Room 202 para sa ika-2 clearance signature."
            );
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
}
