using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays dynamic, atmospheric quest objectives in the top-left corner of the screen.
/// Fits the retro survival-horror aesthetic with typewriter/pulse feedback and clean typography.
/// </summary>
public class ObjectiveHUD : MonoBehaviour
{
    public static ObjectiveHUD Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root GameObject for the objective panel.")]
    public GameObject panelRoot;

    [Tooltip("Text element for the objective category / header tag (e.g. 'CURRENT OBJECTIVE').")]
    public Text headerText;

    [Tooltip("Text element for the main objective title.")]
    public Text titleText;

    [Tooltip("Text element for detailed sub-instructions / location guidance.")]
    public Text detailText;

    [Tooltip("CanvasGroup for smooth fade-in and pulse animations.")]
    public CanvasGroup canvasGroup;

    [Header("Styling & Effects")]
    public Color headerColor = new Color(0.68f, 0.08f, 0.08f, 1f); // Dried blood red
    public Color titleColor = new Color(0.08f, 0.06f, 0.06f, 1f);     // Inky black charcoal
    public Color detailColor = new Color(0.20f, 0.16f, 0.16f, 0.95f); // Graphite pencil smudged

    [Tooltip("Sound played when an objective updates.")]
    public AudioClip updateSound;

    // Runtime tracking
    public static bool HasDiscoveredExitLockdown = false;
    private string _currentTitle = string.Empty;
    private string _currentDetail = string.Empty;
    private Coroutine _animCoroutine;
    private AudioSource _audioSource;

    private void Awake()
    {
        HasDiscoveredExitLockdown = false;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panelRoot == null) panelRoot = gameObject;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D UI sound
        }
    }

    private void Start()
    {
        // If no objective has been set yet, initialize based on game progress
        if (string.IsNullOrEmpty(_currentTitle))
        {
            EvaluateDefaultObjective();
        }
    }

    /// <summary>
    /// Checks game state to display appropriate starting objective.
    /// </summary>
    public void EvaluateDefaultObjective()
    {
        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
        {
            SetObjective("Escape The Campus", "Clearance officially validated. The campus main gates are unlocked—escape now!");
            return;
        }

        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear())
        {
            SetObjective("Submit Clearance Slip (6/6)", "All 6 department signatures gathered. Return to the University Registrar in Room 104 on the ground floor to authorize gate release.");
            return;
        }

        if (ClearanceManager.Instance != null && ClearanceManager.Instance.SignatureCount > 0)
        {
            int nextSig = ClearanceManager.Instance.SignatureCount;
            SetObjectiveForSignature(nextSig);
            return;
        }

        // Check if player has Blank Paper
        var blankItem = Resources.Load<InventoryItem>("Blank_Paper");
#if UNITY_EDITOR
        if (blankItem == null)
            blankItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
#endif
        bool hasPaper = false;
        if (blankItem != null)
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(blankItem)) hasPaper = true;
            if (StorageManager.Instance != null && StorageManager.Instance.HasItem(blankItem)) hasPaper = true;
        }

        if (hasPaper)
        {
            SetObjective("Head Librarian Clearance (1/6)", "Report to the Head Librarian in Room 308 (3rd Floor) to obtain your first signature.");
            return;
        }

        int fragCount = FragmentManager.Instance != null ? FragmentManager.Instance.GetCollectedCount() : 0;
        if (fragCount > 0)
        {
            SetObjective($"Find Clearance Fragments ({fragCount}/4)", $"Search the building corridors for the remaining torn clearance slip fragments ({fragCount}/4).");
            return;
        }

        // Initial state before player discovers the exit lockdown:
        if (!HasDiscoveredExitLockdown)
        {
            SetObjective("Find an Exit", "Search the ground floor lobby for a way out of the campus.");
            return;
        }

        SetObjective("Find Clearance Fragments (0/4)", "The main exit gates are under security lockdown. Search the corridors for the 4 torn clearance slip fragments (0/4).");
    }

    /// <summary>
    /// Updates the displayed objective with an animated fade & audio cue.
    /// </summary>
    public void SetObjective(string title, string detail = "")
    {
        if (_currentTitle == title && _currentDetail == detail) return;

        _currentTitle = title;
        _currentDetail = detail;

        if (panelRoot != null) panelRoot.SetActive(true);

        // Immediate text assignment
        if (headerText != null)
        {
            headerText.text = "OBJECTIVE:";
            headerText.color = headerColor;
        }

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = titleColor;
        }

        if (detailText != null)
        {
            detailText.text = detail;
            detailText.color = detailColor;
            detailText.gameObject.SetActive(!string.IsNullOrEmpty(detail));
        }

        if (Application.isPlaying)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateObjectiveUpdate(title, detail));
        }
        else
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// Sets the objective guiding the player to the next faculty signatory.
    /// </summary>
    public void SetObjectiveForSignature(int sigIndex)
    {
        switch (sigIndex)
        {
            case 0:
                SetObjective("Head Librarian Clearance (1/6)", "Report to the Head Librarian in Room 308 (3rd Floor) to obtain your first signature.");
                break;
            case 1:
                SetObjective("Guidance Clearance (2/6)", "Report to the Guidance Counselor in Room 207 (2nd Floor).");
                break;
            case 2:
                SetObjective("CCS Dean Clearance (3/6)", "Report to the College of Computing Studies Dean in Room 202 (2nd Floor).");
                break;
            case 3:
                SetObjective("Registrar Clearance (4/6)", "Report to Window 2 of the University Registrar in Room 104 (Ground Floor).");
                break;
            case 4:
                SetObjective("Cashier Clearance (5/6)", "Report to Window 2 of the University Cashier in Room 102 (Ground Floor).");
                break;
            case 5:
                SetObjective("Executive VP Clearance (6/6)", "Report to the Executive Vice President in Room 103 (Ground Floor).");
                break;
            default:
                SetObjective("Final Slip Submission", "Return to the University Registrar in Room 104 to officially submit your clearance slip.");
                break;
        }
    }

    private IEnumerator AnimateObjectiveUpdate(string title, string detail)
    {
        // Play notification audio if assigned
        if (_audioSource != null && updateSound != null)
        {
            _audioSource.PlayOneShot(updateSound, 0.7f);
        }

        if (canvasGroup != null)
        {
            // Subtle dip
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0.2f, t / 0.15f);
                yield return null;
            }
        }

        if (headerText != null)
        {
            headerText.text = "OBJECTIVE:";
            headerText.color = headerColor;
        }

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = titleColor;
        }

        if (detailText != null)
        {
            detailText.text = detail;
            detailText.color = detailColor;
            detailText.gameObject.SetActive(!string.IsNullOrEmpty(detail));
        }

        if (canvasGroup != null)
        {
            // Fade & snap in with punch
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0.2f, 1f, t / 0.3f);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }
}
