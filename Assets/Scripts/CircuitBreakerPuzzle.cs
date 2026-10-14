using System.Collections;
using UnityEngine;

/// <summary>
/// CircuitBreakerPuzzle — Main electrical distribution panel puzzle.
/// Restores power to the ComLab & Faculty Wing when the 15A Ceramic Cartridge Fuse is inserted.
///
/// Features:
/// - 4 circuit branches (Hallway, ComLab/Faculty, Guidance, Ventilation).
/// - Slot 2 is blown/tripped, requiring the 15A Ceramic Fuse.
/// - Inspecting the Ceramic Fuse in 3D reveals "15A - 250V // RATING: COMLAB & FACULTY WING GRID".
/// - Restoring power illuminates wing lights and activates Room 105 computer terminal.
/// </summary>
public class CircuitBreakerPuzzle : MonoBehaviour, IInteractable
{
    public static CircuitBreakerPuzzle Instance { get; private set; }

    [Header("Puzzle State")]
    public bool isFuseInstalled = false;
    public bool isPowerRestored = false;

    [Header("Item References")]
    [Tooltip("The 15A Ceramic Cartridge Fuse item required.")]
    public InventoryItem ceramicFuseItem;

    [Header("Visual Feedback")]
    [Tooltip("Status LED for Slot 2 (Red when blown, Green when fused).")]
    public Light slotStatusLight;

    [Tooltip("Wing lights group turned on when power is restored.")]
    public Transform wingLightsGroup;

    [Header("Audio")]
    public AudioClip fuseInsertAudio;
    public AudioClip switchThrowAudio;
    public AudioClip powerHumAudio;

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
            _audioSource.spatialBlend = 0.85f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 15f;
            _audioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        if (ceramicFuseItem == null)
            ceramicFuseItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Ceramic_Fuse.asset");
        if (fuseInsertAudio == null)
            fuseInsertAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (switchThrowAudio == null)
            switchThrowAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/opening door.mp3");
        if (powerHumAudio == null)
            powerHumAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/FNAF_Power_On_Short.wav");
#endif
    }

    public string GetPrompt()
    {
        if (isPowerRestored)
        {
            return "[Main Breaker Box] (Aktibo ang Kuryente - Slot 2: 15A OK)";
        }

        bool hasFuse = PlayerHasCeramicFuse();
        bool knowsRating = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("CeramicFuseRating");

        if (hasFuse)
        {
            return knowsRating
                ? "[E] Ilagay ang 15A Ceramic Fuse sa Slot 2 at I-on ang Switch"
                : "[E] Magkabit ng Ceramic Fuse sa Breaker Slot 2";
        }

        return "[Main Breaker Box] (Napatid ang Kuryente sa Slot 2 - Kailangan ng 15A Ceramic Fuse)";
    }

    public void Interact()
    {
        if (isPowerRestored)
        {
            Debug.Log("[CircuitBreakerPuzzle] Power is already fully restored to the wing.");
            return;
        }

        if (PlayerHasCeramicFuse())
        {
            StartCoroutine(RestorePowerSequence());
        }
        else
        {
            PlayAudio(fuseInsertAudio, 0.6f, 0.85f);
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Breaker Panel: Napatid na Fuse",
                    "Kailangan ng 15A Ceramic Cartridge Fuse upang maibalik ang kuryente sa ComLab at Faculty wing."
                );
            }
        }
    }

    private IEnumerator RestorePowerSequence()
    {
        ConsumeCeramicFuse();
        isFuseInstalled = true;

        PlayAudio(fuseInsertAudio, 0.85f, 1.15f);
        yield return new WaitForSeconds(0.4f);

        PlayAudio(switchThrowAudio, 1.0f, 1.0f);
        yield return new WaitForSeconds(0.3f);

        PlayAudio(powerHumAudio, 0.95f, 1.0f);
        isPowerRestored = true;

        if (slotStatusLight != null)
        {
            slotStatusLight.color = Color.green;
            slotStatusLight.intensity = 1.5f;
        }

        if (wingLightsGroup != null)
        {
            foreach (var light in wingLightsGroup.GetComponentsInChildren<Light>(true))
            {
                light.enabled = true;
            }
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Kuryente sa ComLab Naibalik!",
                "Naibalik ang power grid sa 2nd Floor. Maaari nang puntahan ang Master Computer Terminal sa Room 105."
            );
        }

        Debug.Log("[CircuitBreakerPuzzle] 15A Ceramic Fuse installed. Grid power restored!");
    }

    private bool PlayerHasCeramicFuse()
    {
        if (InventoryManager.Instance != null)
        {
            if (ceramicFuseItem != null && InventoryManager.Instance.HasItem(ceramicFuseItem)) return true;
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Fuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        if (StorageManager.Instance != null)
        {
            if (ceramicFuseItem != null && StorageManager.Instance.HasItem(ceramicFuseItem)) return true;
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Fuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private void ConsumeCeramicFuse()
    {
        if (InventoryManager.Instance != null)
        {
            if (ceramicFuseItem != null && InventoryManager.Instance.HasItem(ceramicFuseItem))
            {
                InventoryManager.Instance.RemoveItem(ceramicFuseItem, 1);
                return;
            }
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Fuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }
        if (StorageManager.Instance != null)
        {
            if (ceramicFuseItem != null && StorageManager.Instance.HasItem(ceramicFuseItem))
            {
                StorageManager.Instance.RemoveItem(ceramicFuseItem, 1);
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
