using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the library printer workstation in Room 308.
/// Implements IInteractable to handle loading paper, executing the tense printing horror event,
/// and dispensing the Library Clearance Voucher required by the Head Librarian.
/// </summary>
public class LibraryPrinterInteract : MonoBehaviour, IInteractable
{
    public static LibraryPrinterInteract Instance { get; private set; }

    public enum PrinterState
    {
        AwaitingQuest,   // Librarian hasn't assigned the task yet
        NeedsPaper,      // Librarian assigned task; needs bond paper
        ReadyToPrint,    // Paper loaded; ready to print voucher
        Printing,        // In the middle of printing sequence
        VoucherReady,    // Printed voucher waiting on output tray
        Completed        // Voucher taken by player
    }

    [Header("Current State")]
    public PrinterState currentState = PrinterState.AwaitingQuest;

    [Header("Item References")]
    [Tooltip("The ScriptableObject for Ream of Bond Paper.")]
    public InventoryItem paperItemData;

    [Tooltip("The ScriptableObject for Library Clearance Voucher.")]
    public InventoryItem voucherItemData;

    [Header("Visual & Output References")]
    [Tooltip("Physical voucher model sitting on the printer output slot (hidden until printed).")]
    public GameObject physicalVoucherModel;

    [Tooltip("Small status LED Light or Renderer indicating printer status.")]
    public Light statusLight;

    [Tooltip("Ceiling lights in Room 308 to flicker during printing.")]
    public Transform classroomLightsGroup;

    [Header("Audio References")]
    [Tooltip("Mechanical printing audio (stepper motor whir, roller feed, gear clicks).")]
    public AudioClip printerAudioClip;

    [Tooltip("Atmospheric horror cue played during/after printing.")]
    public AudioClip horrorCueClip;

    [Header("Printing Settings")]
    public float printDuration = 3.8f;

    // Runtime
    private AudioSource _audioSource;
    private List<Light> _ceilingLights = new List<Light>();
    private List<float> _defaultLightIntensities = new List<float>();

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
            _audioSource.spatialBlend = 1f; // 3D sound at printer
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 18f;
            _audioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        // Cache ceiling lights for the horror flickering sequence
        CacheCeilingLights();

        // Ensure physical voucher document is hidden initially
        if (physicalVoucherModel != null)
        {
            physicalVoucherModel.SetActive(false);
        }

        UpdateStatusVisuals();
    }

    private void CacheCeilingLights()
    {
        if (classroomLightsGroup == null)
        {
            var r308Lights = GameObject.Find("Rooms/3rdFloor/Room 308/ClassroomLights");
            if (r308Lights != null) classroomLightsGroup = r308Lights.transform;
        }

        if (classroomLightsGroup != null)
        {
            _ceilingLights.Clear();
            _defaultLightIntensities.Clear();
            foreach (Transform t in classroomLightsGroup)
            {
                var l = t.GetComponentInChildren<Light>();
                if (l != null)
                {
                    _ceilingLights.Add(l);
                    _defaultLightIntensities.Add(l.intensity);
                }
            }
        }
    }

    /// <summary>
    /// Called by Head Librarian when she explains the overdue record and unlocks the printer task.
    /// </summary>
    public void UnlockPrinterQuest()
    {
        if (currentState == PrinterState.AwaitingQuest)
        {
            currentState = PrinterState.NeedsPaper;
            UpdateStatusVisuals();
            Debug.Log("[LibraryPrinter] Quest unlocked: Printer now requires bond paper.");

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Find Bond Paper Ream",
                    "Search the dark bookshelf aisles at the back of Room 308 for a ream of bond paper."
                );
            }
        }
    }

    public string GetPrompt()
    {
        switch (currentState)
        {
            case PrinterState.AwaitingQuest:
                return "Printer Offline (Awaiting Librarian Authorization)";

            case PrinterState.NeedsPaper:
                if (PlayerHasPaper())
                {
                    return "Press [E] to Load Bond Paper into Printer";
                }
                return "Printer: [ERROR - TRAY EMPTY: Search bookshelf archives for paper]";

            case PrinterState.ReadyToPrint:
                return "Press [E] to Print Library Clearance Voucher";

            case PrinterState.Printing:
                return "[Printing in progress...]";

            case PrinterState.VoucherReady:
                return "Press [E] to Take Library Clearance Voucher";

            case PrinterState.Completed:
                return "Printer: [JOB COMPLETED: Clearance Voucher Dispensed]";

            default:
                return string.Empty;
        }
    }

    public void Interact()
    {
        switch (currentState)
        {
            case PrinterState.AwaitingQuest:
                Debug.Log("[LibraryPrinter] Printer locked by librarian.");
                break;

            case PrinterState.NeedsPaper:
                if (PlayerHasPaper())
                {
                    LoadPaper();
                }
                else
                {
                    Debug.Log("[LibraryPrinter] Player does not have paper yet.");
                }
                break;

            case PrinterState.ReadyToPrint:
                StartCoroutine(ExecutePrintingSequence());
                break;

            case PrinterState.VoucherReady:
                CollectVoucher();
                break;

            case PrinterState.Completed:
                break;
        }
    }

    private bool PlayerHasPaper()
    {
        if (paperItemData == null) return false;
        bool inHotbar = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(paperItemData);
        bool inStorage = StorageManager.Instance != null && StorageManager.Instance.HasItem(paperItemData);
        return inHotbar || inStorage;
    }

    private void LoadPaper()
    {
        // Remove 1 paper ream from inventory
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(paperItemData))
        {
            InventoryManager.Instance.RemoveItem(paperItemData, 1);
        }
        else if (StorageManager.Instance != null && StorageManager.Instance.HasItem(paperItemData))
        {
            StorageManager.Instance.RemoveItem(paperItemData, 1);
        }

        currentState = PrinterState.ReadyToPrint;
        UpdateStatusVisuals();
        Debug.Log("[LibraryPrinter] Paper loaded into tray. Ready to print!");

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Print Clearance Voucher",
                "Press [E] on the printer to print your Library Clearance Voucher."
            );
        }
    }

    private IEnumerator ExecutePrintingSequence()
    {
        currentState = PrinterState.Printing;
        UpdateStatusVisuals();

        // 1. Play mechanical printer audio
        if (_audioSource != null && printerAudioClip != null)
        {
            _audioSource.clip = printerAudioClip;
            _audioSource.loop = false;
            _audioSource.Play();
        }

        // 2. Trigger ceiling lights flicker horror effect
        Coroutine flickerRoutine = StartCoroutine(FlickerLightsRoutine(printDuration));

        // 3. Wait for print job to complete
        yield return new WaitForSeconds(printDuration);

        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        RestoreCeilingLights();

        // 4. Play horror audio sting
        if (horrorCueClip != null)
        {
            AudioSource.PlayClipAtPoint(horrorCueClip, transform.position, 0.9f);
        }

        // 5. Dispense physical voucher
        if (physicalVoucherModel != null)
        {
            physicalVoucherModel.SetActive(true);
        }

        currentState = PrinterState.VoucherReady;
        UpdateStatusVisuals();
        Debug.Log("[LibraryPrinter] Printing complete! Clearance voucher ready on tray.");

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Collect Clearance Voucher",
                "Take the printed Library Clearance Voucher from the printer tray."
            );
        }
    }

    private void CollectVoucher()
    {
        if (voucherItemData == null)
        {
            Debug.LogError("[LibraryPrinter] voucherItemData is null!");
            return;
        }

        bool added = false;
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasFreeSlot())
        {
            added = InventoryManager.Instance.AddItem(voucherItemData, 1);
        }
        else if (StorageManager.Instance != null)
        {
            added = StorageManager.Instance.AddItem(voucherItemData, 1);
        }

        if (added)
        {
            if (physicalVoucherModel != null)
            {
                physicalVoucherModel.SetActive(false);
            }

            currentState = PrinterState.Completed;
            UpdateStatusVisuals();
            Debug.Log("[LibraryPrinter] Library Clearance Voucher collected.");

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Obtain Librarian's Signature",
                    "Present the printed Library Clearance Voucher to the Head Librarian in Room 308."
                );
            }
        }
        else
        {
            Debug.LogWarning("[LibraryPrinter] Inventory full, cannot collect voucher!");
        }
    }

    private IEnumerator FlickerLightsRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Random flicker intensity across ceiling lights
            for (int i = 0; i < _ceilingLights.Count; i++)
            {
                if (_ceilingLights[i] != null && i < _defaultLightIntensities.Count)
                {
                    float baseIntensity = _defaultLightIntensities[i];
                    bool lightDrop = Random.value < 0.45f;
                    _ceilingLights[i].intensity = lightDrop ? Random.Range(0f, 0.4f) : baseIntensity * Random.Range(0.7f, 1.2f);
                }
            }

            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }

        RestoreCeilingLights();
    }

    private void RestoreCeilingLights()
    {
        for (int i = 0; i < _ceilingLights.Count; i++)
        {
            if (_ceilingLights[i] != null && i < _defaultLightIntensities.Count)
            {
                _ceilingLights[i].intensity = _defaultLightIntensities[i];
            }
        }
    }

    private void UpdateStatusVisuals()
    {
        if (statusLight != null)
        {
            switch (currentState)
            {
                case PrinterState.AwaitingQuest:
                    statusLight.color = new Color(0.3f, 0.3f, 0.3f);
                    statusLight.intensity = 0.5f;
                    break;
                case PrinterState.NeedsPaper:
                    statusLight.color = Color.red;
                    statusLight.intensity = 1.2f;
                    break;
                case PrinterState.ReadyToPrint:
                    statusLight.color = new Color(1f, 0.7f, 0f); // Amber
                    statusLight.intensity = 1.5f;
                    break;
                case PrinterState.Printing:
                    statusLight.color = Color.yellow;
                    statusLight.intensity = 2.0f;
                    break;
                case PrinterState.VoucherReady:
                case PrinterState.Completed:
                    statusLight.color = Color.green;
                    statusLight.intensity = 1.5f;
                    break;
            }
        }
    }
}
