using System.Collections;
using UnityEngine;

/// <summary>
/// VendingMachineInteract — Vintage snack & emergency supply vending machine
/// located in the ground floor hallway/cafeteria area.
///
/// Accepts the Philippine 1-Peso Coin (Peso_Coin).
/// Allows the player to purchase vital puzzle items and survival supplies:
/// 1. Ceramic Cartridge Fuse 15A (Required for Circuit Breaker Puzzle)
/// 2. Ham & Cheese Sandwich (Consumable: restores stamina and drops anxiety)
/// 3. Clinic Medical Record Slip (Medical clearance document)
/// </summary>
public class VendingMachineInteract : MonoBehaviour, IInteractable
{
    public static VendingMachineInteract Instance { get; private set; }

    [Header("Item References")]
    public InventoryItem pesoCoinItem;
    public InventoryItem ceramicFuseItem;
    public InventoryItem sandwichItem;
    public InventoryItem medicalSlipItem;

    [Header("Audio")]
    public AudioClip coinInsertAudio;
    public AudioClip spiralMotorAudio;
    public AudioClip itemDropAudio;

    private AudioSource _audioSource;
    private bool _isDispensing = false;

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
            _audioSource.spatialBlend = 0.75f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 14f;
            _audioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        if (pesoCoinItem == null)
            pesoCoinItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Peso_Coin.asset");
        if (ceramicFuseItem == null)
            ceramicFuseItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Ceramic_Fuse.asset");
        if (sandwichItem == null)
            sandwichItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/HamAndCheeseSandwich.asset");
        if (medicalSlipItem == null)
            medicalSlipItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Medical_Record_Slip.asset");

        if (coinInsertAudio == null)
            coinInsertAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (spiralMotorAudio == null)
            spiralMotorAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/printer_operation.wav");
        if (itemDropAudio == null)
            itemDropAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
#endif
    }

    public string GetPrompt()
    {
        if (_isDispensing)
        {
            return "[Vending Machine] (Nagluluwal ng gamit...)";
        }

        bool hasCoin = PlayerHasCoin();
        return hasCoin
            ? "[E] Maghulog ng 1-Piso Barya sa Vending Machine"
            : "[Vending Machine] (Kailangan ng 1-Piso Pilipino Coin)";
    }

    public void Interact()
    {
        if (_isDispensing) return;

        if (!PlayerHasCoin())
        {
            PlayAudio(coinInsertAudio, 0.5f, 0.8f);
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Vending Machine: Walang Barya",
                    "Maghanap ng 1-Piso barya sa mga desk o lockers para makabili ng supplies sa vending machine."
                );
            }
            return;
        }

        // Has coin, dispense item
        StartCoroutine(DispenseItemSequence());
    }

    private IEnumerator DispenseItemSequence()
    {
        _isDispensing = true;
        ConsumeCoin();

        PlayAudio(coinInsertAudio, 0.9f, 1.2f);
        yield return new WaitForSeconds(0.5f);

        PlayAudio(spiralMotorAudio, 0.85f, 1.0f);
        yield return new WaitForSeconds(1.4f);

        PlayAudio(itemDropAudio, 1.0f, 0.9f);

        // Decide what to dispense based on puzzle progression:
        // Priority 1: Ceramic Fuse if breaker not restored
        // Priority 2: Ham & Cheese sandwich
        InventoryItem itemToDispense = sandwichItem;
        if (CircuitBreakerPuzzle.Instance != null && !CircuitBreakerPuzzle.Instance.isPowerRestored)
        {
            itemToDispense = ceramicFuseItem;
        }
        else if (Random.value < 0.4f && medicalSlipItem != null)
        {
            itemToDispense = medicalSlipItem;
        }

        if (itemToDispense != null)
        {
            bool added = false;
            if (InventoryManager.Instance != null) added = InventoryManager.Instance.AddItem(itemToDispense, 1);
            if (!added && StorageManager.Instance != null) added = StorageManager.Instance.AddItem(itemToDispense, 1);

            // Open 3D inspection for the newly vended item!
            ItemInspectionViewer3D.Inspect(itemToDispense);

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    $"{itemToDispense.itemName} Nakuha!",
                    "Nailuwa mula sa vending machine. Suriin ang gamit para sa mga detalye."
                );
            }
        }

        yield return new WaitForSeconds(0.6f);
        _isDispensing = false;
    }

    private bool PlayerHasCoin()
    {
        if (InventoryManager.Instance != null)
        {
            if (pesoCoinItem != null && InventoryManager.Instance.HasItem(pesoCoinItem)) return true;
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        if (StorageManager.Instance != null)
        {
            if (pesoCoinItem != null && StorageManager.Instance.HasItem(pesoCoinItem)) return true;
            foreach (var slot in StorageManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private void ConsumeCoin()
    {
        if (InventoryManager.Instance != null)
        {
            if (pesoCoinItem != null && InventoryManager.Instance.HasItem(pesoCoinItem))
            {
                InventoryManager.Instance.RemoveItem(pesoCoinItem, 1);
                return;
            }
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }
        if (StorageManager.Instance != null)
        {
            if (pesoCoinItem != null && StorageManager.Instance.HasItem(pesoCoinItem))
            {
                StorageManager.Instance.RemoveItem(pesoCoinItem, 1);
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
