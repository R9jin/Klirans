using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ObjectiveHUD - Survival Horror Clearance Directive & Expandable Mission Dossier.
/// - Top-Right Compact HUD: Displays Main Objective + Active Sub-Objective with [Hold TAB] prompt.
/// - Hold [TAB] Mission Dossier: Expands full-screen authentic clearance clipboard showing
///   the overall mission directive, active sub-task callout, and real-time status of all 6
///   faculty signatories and exit gate status.
/// </summary>
public class ObjectiveHUD : MonoBehaviour
{
    public static ObjectiveHUD Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root GameObject for the objective panel.")]
    public GameObject panelRoot;

    [Tooltip("Text element for the objective category / header tag.")]
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

    // Expanded Dossier UI
    private GameObject _dossierRoot;
    private Text _dossierTitleText;
    private Text _dossierStatusText;
    private Text _dossierActiveTaskText;
    private Text _dossierChecklistText;
    private CanvasGroup _dossierCanvasGroup;
    private bool _isDossierOpen = false;
    // private float _lastDossierToggleTime = 0f;

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

        EnforcePaperUIStyling();
    }

    private void Start()
    {
        EnforcePaperUIStyling();
        BuildExpandedDossierUI();

        // If no objective has been set yet, initialize based on game progress
        if (string.IsNullOrEmpty(_currentTitle))
        {
            EvaluateDefaultObjective();
        }
    }

    private void Update()
    {
        // Suppress Tab expansion during pause or full-screen puzzle modals
        if (PauseMenu.GameIsPaused || PauseMenu.IsAnyModalOrPuzzleOpen)
        {
            if (_isDossierOpen) SetDossierVisible(false);
            return;
        }

        // Hold TAB (or press Tab) to expand the full mission dossier to front of screen
        bool tabHeld = Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.I);

        if (tabHeld != _isDossierOpen)
        {
            SetDossierVisible(tabHeld);
        }

        if (_isDossierOpen)
        {
            RefreshDossierContent();
        }
    }

    /// <summary>
    /// Positions and styles the compact objective HUD at the TOP-LEFT corner of the screen.
    /// Uses sharp bold typewriter typography with high contrast dark charcoal ink on paper.
    /// </summary>
    public void EnforcePaperUIStyling()
    {
        var typewriterFont = GetTypewriterFont();

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (panelRoot != null)
        {
            var prt = panelRoot.GetComponent<RectTransform>();
            if (prt != null)
            {
                // Anchor to Top-Left of the screen
                prt.anchorMin = new Vector2(0f, 1f);
                prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(0f, 1f);
                prt.anchoredPosition = new Vector2(25f, -24f);
                prt.sizeDelta = new Vector2(500f, 160f);
                prt.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }

            var pImg = panelRoot.GetComponent<Image>();
            if (pImg != null)
            {
                pImg.color = new Color(1f, 1f, 1f, 0.98f);
            }
        }

        if (headerText != null)
        {
            headerText.gameObject.SetActive(false);
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
            var trt = titleText.GetComponent<RectTransform>();
            if (trt != null)
            {
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(28f, 12f);
                trt.offsetMax = new Vector2(-28f, -14f);
            }
            titleText.font = typewriterFont;
            titleText.fontSize = 17;
            titleText.fontStyle = FontStyle.Bold;
            titleText.lineSpacing = 1.25f;
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.supportRichText = true;
            titleText.color = new Color(0.06f, 0.04f, 0.04f, 1f);

            var shadow = titleText.GetComponent<Shadow>();
            if (shadow == null) shadow = titleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 0.98f, 0.92f, 0.5f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        if (detailText != null)
        {
            detailText.gameObject.SetActive(false);
        }
    }

    private Font GetTypewriterFont()
    {
        var font = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold") 
            ?? Resources.Load<Font>("Lucida_Typewriter_Bold")
            ?? Resources.Load<Font>("Fonts/Typewriter_Bold") 
            ?? Resources.Load<Font>("Typewriter_Bold");
#if UNITY_EDITOR
        if (font == null)
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lucida_Typewriter_Bold.ttf");
        if (font == null)
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    /// <summary>
    /// Constructs the rich Expanded Clearance Dossier UI centered on screen.
    /// </summary>
    private void BuildExpandedDossierUI()
    {
        if (_dossierRoot != null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        Font font = GetTypewriterFont();

        // 1. Root Container (Full Screen Overlay)
        _dossierRoot = new GameObject("ClearanceMissionDossier", typeof(RectTransform));
        _dossierRoot.transform.SetParent(parentCanvas.transform, false);
        _dossierCanvasGroup = _dossierRoot.AddComponent<CanvasGroup>();
        _dossierCanvasGroup.alpha = 0f;
        _dossierCanvasGroup.blocksRaycasts = false;

        RectTransform rootRT = _dossierRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.sizeDelta = Vector2.zero;

        // 2. Dark Cinematic Vignette Backdrop
        GameObject bgObj = new GameObject("DossierBackdrop", typeof(Image));
        bgObj.transform.SetParent(_dossierRoot.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // 3. Central Weathered Manila Board
        GameObject boardObj = new GameObject("ClearanceBoard", typeof(Image));
        boardObj.transform.SetParent(_dossierRoot.transform, false);
        Image boardImg = boardObj.GetComponent<Image>();
        boardImg.color = new Color(0.92f, 0.89f, 0.82f, 0.98f); // Aged cardstock paper
        RectTransform boardRT = boardObj.GetComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.pivot = new Vector2(0.5f, 0.5f);
        boardRT.sizeDelta = new Vector2(880f, 620f);

        // Dark Border Frame
        GameObject borderObj = new GameObject("BorderFrame", typeof(Outline));
        borderObj.transform.SetParent(boardObj.transform, false);
        var outline = borderObj.GetComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.15f, 0.12f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        // Header Title Text
        GameObject headerObj = new GameObject("HeaderTitle", typeof(Text));
        headerObj.transform.SetParent(boardObj.transform, false);
        _dossierTitleText = headerObj.GetComponent<Text>();
        _dossierTitleText.font = font;
        _dossierTitleText.fontSize = 20;
        _dossierTitleText.fontStyle = FontStyle.Bold;
        _dossierTitleText.color = new Color(0.65f, 0.08f, 0.08f, 1f); // Stamp Red
        _dossierTitleText.alignment = TextAnchor.MiddleCenter;
        _dossierTitleText.supportRichText = true;
        _dossierTitleText.text = "PAMANTASAN NG CLEARANCE • OFFICIAL MISSION DOSSIER";
        RectTransform hRT = headerObj.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -22f);
        hRT.sizeDelta = new Vector2(-40f, 32f);

        // Overall Status Sub-Bar
        GameObject statusObj = new GameObject("StatusSubBar", typeof(Text));
        statusObj.transform.SetParent(boardObj.transform, false);
        _dossierStatusText = statusObj.GetComponent<Text>();
        _dossierStatusText.font = font;
        _dossierStatusText.fontSize = 15;
        _dossierStatusText.fontStyle = FontStyle.Bold;
        _dossierStatusText.color = new Color(0.12f, 0.10f, 0.08f, 1f);
        _dossierStatusText.alignment = TextAnchor.MiddleCenter;
        _dossierStatusText.supportRichText = true;
        RectTransform sRT = statusObj.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 1f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.pivot = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -54f);
        sRT.sizeDelta = new Vector2(-40f, 26f);

        // Active Task Callout Box (Gold/Slate panel)
        GameObject activeBoxObj = new GameObject("ActiveTaskBox", typeof(Image));
        activeBoxObj.transform.SetParent(boardObj.transform, false);
        Image activeBoxImg = activeBoxObj.GetComponent<Image>();
        activeBoxImg.color = new Color(0.85f, 0.81f, 0.72f, 0.95f);
        RectTransform atbRT = activeBoxObj.GetComponent<RectTransform>();
        atbRT.anchorMin = new Vector2(0f, 1f);
        atbRT.anchorMax = new Vector2(1f, 1f);
        atbRT.pivot = new Vector2(0.5f, 1f);
        atbRT.anchoredPosition = new Vector2(0f, -88f);
        atbRT.sizeDelta = new Vector2(800f, 78f);

        GameObject activeTextObj = new GameObject("ActiveTaskText", typeof(Text));
        activeTextObj.transform.SetParent(activeBoxObj.transform, false);
        _dossierActiveTaskText = activeTextObj.GetComponent<Text>();
        _dossierActiveTaskText.font = font;
        _dossierActiveTaskText.fontSize = 15;
        _dossierActiveTaskText.color = new Color(0.08f, 0.06f, 0.05f, 1f);
        _dossierActiveTaskText.alignment = TextAnchor.MiddleLeft;
        _dossierActiveTaskText.supportRichText = true;
        RectTransform attRT = activeTextObj.GetComponent<RectTransform>();
        attRT.anchorMin = Vector2.zero;
        attRT.anchorMax = Vector2.one;
        attRT.offsetMin = new Vector2(18f, 8f);
        attRT.offsetMax = new Vector2(-18f, -8f);

        // 6 Department Checklist (Two-column layout in rich text)
        GameObject checklistObj = new GameObject("ChecklistText", typeof(Text));
        checklistObj.transform.SetParent(boardObj.transform, false);
        _dossierChecklistText = checklistObj.GetComponent<Text>();
        _dossierChecklistText.font = font;
        _dossierChecklistText.fontSize = 15;
        _dossierChecklistText.lineSpacing = 1.28f;
        _dossierChecklistText.color = new Color(0.08f, 0.06f, 0.06f, 1f);
        _dossierChecklistText.alignment = TextAnchor.UpperLeft;
        _dossierChecklistText.supportRichText = true;
        RectTransform clRT = checklistObj.GetComponent<RectTransform>();
        clRT.anchorMin = new Vector2(0f, 0f);
        clRT.anchorMax = new Vector2(1f, 1f);
        clRT.offsetMin = new Vector2(42f, 48f);
        clRT.offsetMax = new Vector2(-42f, -175f);

        // Footer prompt
        GameObject footerObj = new GameObject("FooterPrompt", typeof(Text));
        footerObj.transform.SetParent(boardObj.transform, false);
        Text footerText = footerObj.GetComponent<Text>();
        footerText.font = font;
        footerText.fontSize = 13;
        footerText.fontStyle = FontStyle.Bold;
        footerText.color = new Color(0.40f, 0.35f, 0.30f, 1f);
        footerText.alignment = TextAnchor.MiddleCenter;
        footerText.text = "[ RELEASE TAB TO RETURN TO GAMEPLAY ]";
        RectTransform fRT = footerObj.GetComponent<RectTransform>();
        fRT.anchorMin = new Vector2(0f, 0f);
        fRT.anchorMax = new Vector2(1f, 0f);
        fRT.pivot = new Vector2(0.5f, 0f);
        fRT.anchoredPosition = new Vector2(0f, 14f);
        fRT.sizeDelta = new Vector2(-40f, 26f);

        _dossierRoot.SetActive(false);
    }

    private void SetDossierVisible(bool visible)
    {
        _isDossierOpen = visible;

        if (_dossierRoot == null) BuildExpandedDossierUI();
        if (_dossierRoot == null) return;

        _dossierRoot.SetActive(visible);
        if (_dossierCanvasGroup != null)
        {
            _dossierCanvasGroup.alpha = visible ? 1f : 0f;
        }

        // Dim the top-right compact HUD while full dossier is expanded to the front of the screen
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 0.20f : 1.0f;
        }

        if (visible)
        {
            RefreshDossierContent();
            if (_audioSource != null && updateSound != null)
            {
                _audioSource.pitch = 1.15f;
                _audioSource.PlayOneShot(updateSound, 0.5f);
            }
        }
    }

    private void RefreshDossierContent()
    {
        if (_dossierRoot == null || !_dossierRoot.activeSelf) return;

        int sigCount = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
        bool isFullyClear = ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear();
        bool isSubmitted = ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted;

        if (_dossierStatusText != null)
        {
            string gateStr = isSubmitted 
                ? "<color=#1b5e20>UNLOCKED — ESCAPE MAIN LOBBY GATE!</color>" 
                : (isFullyClear ? "<color=#b71c1c>READY — SUBMIT AT REGISTRAR TO UNLOCK GATE</color>" : "<color=#777777>LOCKED (REQUIRES 6 SIGNATURES)</color>");

            _dossierStatusText.text = $"SIGNATURES OBTAINED: <color=#881111><b>{sigCount} / 6</b></color>      EXIT GRILLE: <b>{gateStr}</b>";
        }

        if (_dossierActiveTaskText != null)
        {
            string title = string.IsNullOrEmpty(_currentTitle) ? "SURVIVE AND EXPLORE" : _currentTitle;
            string detail = string.IsNullOrEmpty(_currentDetail) ? "Scour the campus rooms and corridors for clearance items." : _currentDetail;
            _dossierActiveTaskText.text = $"<color=#881111><b>AKTIBONG SUB-OBJECTIVE:</b></color> <b>{title}</b>\n<size=13><color=#222222>{detail}</color></size>";
        }

        if (_dossierChecklistText != null)
        {
            StringBuilder sb = new StringBuilder();

            bool s0 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(0);
            bool s1 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(1);
            bool s2 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(2);
            bool s3 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(3);
            bool s4 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(4);
            bool s5 = ClearanceManager.Instance != null && ClearanceManager.Instance.HasSignature(5);

            string Tag(bool signed, bool active)
            {
                if (signed) return "<color=#1b5e20><b>[✓ PIRMA OK]</b></color>";
                if (active) return "<color=#b71c1c><b>[● IN PROGRESS]</b></color>";
                return "<color=#777777>[○ PENDING]</color>";
            }

            sb.AppendLine($"{Tag(s0, sigCount == 0)} <b>1. 3F LIBRARY (Room 308)</b> — Return Archive Book to study table (Librarian Signature)");
            sb.AppendLine($"{Tag(s1, sigCount == 1)} <b>2. 2F GUIDANCE (Room 207)</b> — Audio Cassette Clue & Wall Safe 4-1-9 (Counselor Signature)");
            sb.AppendLine($"{Tag(s2, sigCount == 2)} <b>3. 2F CCS DEAN (Room 202)</b> — Dean's Lockpin, 15A Fuse at Breaker, ComLab Form (Dean Signature)");
            sb.AppendLine($"{Tag(s3, sigCount == 3)} <b>4. 1F REGISTRAR (Room 104)</b> — Clinic Medical Record & Grade Sorting (Registrar Signature)");
            sb.AppendLine($"{Tag(s4, sigCount == 4)} <b>5. 1F CASHIER (Room 102)</b> — 50 PHP Tuition Coin & Balance Ledger (Cashier Signature)");
            sb.AppendLine($"{Tag(s5, sigCount == 5)} <b>6. 1F EXECUTIVE (Room 103)</b> — Official University Seal Stamp (Executive VP Signature)");
            sb.AppendLine("");
            string exitTag = isSubmitted ? "<color=#1b5e20><b>[🔓 BUKAS NA]</b></color>" : "<color=#777777>[🔒 NAKA-KANDADO]</color>";
            sb.AppendLine($"{exitTag} <b>7. 1F LOBBY EXIT GATE</b> — I-submit ang kumpletong slip sa Registrar Window 2 para buksan ang gate at makatakas!");

            _dossierChecklistText.text = sb.ToString();
        }
    }

    private string GetCurrentMainObjective()
    {
        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
        {
            return "ESCAPE THE CAMPUS (GATES UNLOCKED)";
        }
        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear())
        {
            return "SUBMIT CLEARANCE SLIP (6/6 SIGNATURES)";
        }
        int sigCount = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
        return $"SURVIVE & COMPLETE CLEARANCE ({sigCount}/6)";
    }

    /// <summary>
    /// Checks game state to display appropriate starting objective.
    /// </summary>
    public void EvaluateDefaultObjective()
    {
        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
        {
            SetObjective("Escape The Campus", "Campus main gates unlocked. Exit through Lobby!");
            return;
        }

        if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear())
        {
            SetObjective("Submit Clearance Slip (6/6)", "Return to University Registrar in Room 104");
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
            SetObjective("Head Librarian Clearance (1/6)", "Report to Room 308 on the 3rd Floor");
            return;
        }

        int fragCount = FragmentManager.Instance != null ? FragmentManager.Instance.GetCollectedCount() : 0;
        if (fragCount > 0)
        {
            SetObjective(
                $"Find or Craft Clearance Slip ({fragCount}/4)",
                $"Search corridors for 4 torn fragments ({fragCount}/4)"
            );
            return;
        }

        // Initial starting objective
        SetObjective(
            "Find or Craft Clearance Slip (0/4)",
            "Search corridors for 4 torn fragments"
        );
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

        EnforcePaperUIStyling();

        // Update Top-Right HUD text
        if (titleText != null)
        {
            string mainGoal = GetCurrentMainObjective();
            if (GameModeManager.IsFreeRoamMode && !mainGoal.StartsWith("[FREE ROAM]"))
            {
                mainGoal = $"<color=#1b5e20>[FREE ROAM]</color> {mainGoal}";
            }

            string subTaskDisplay = !string.IsNullOrEmpty(detail) ? detail : title;
            string subTaskHeader = !string.IsNullOrEmpty(detail) ? title : "";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<size=14><color=#990000><b>● MAIN:</b> {mainGoal.ToUpper()}</color></size>");
            if (!string.IsNullOrEmpty(subTaskHeader))
            {
                sb.AppendLine($"<size=16><color=#080505><b>{subTaskHeader.ToUpper()}</b></color></size>");
            }
            sb.AppendLine($"<size=13><color=#1c1212><b>• {subTaskDisplay}</b></color></size>");
            sb.Append("<size=11><color=#3a2828><b>[HOLD TAB] EXPAND CLEARANCE DOSSIER</b></color></size>");

            titleText.text = sb.ToString();
        }

        if (detailText != null) detailText.gameObject.SetActive(false);
        if (headerText != null) headerText.gameObject.SetActive(false);

        if (Application.isPlaying)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateObjectiveUpdate());
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
                SetObjective("Head Librarian Clearance (1/6)", "Report to Room 308 on the 3rd Floor");
                break;
            case 1:
                SetObjective("Guidance Clearance (2/6)", "Report to Room 207 on the 2nd Floor");
                break;
            case 2:
                SetObjective("CCS Dean Clearance (3/6)", "Report to Room 202 on the 2nd Floor");
                break;
            case 3:
                SetObjective("Registrar Clearance (4/6)", "Report to Room 104 Window 2 on Ground Floor");
                break;
            case 4:
                SetObjective("Cashier Clearance (5/6)", "Report to Room 102 Window 2 on Ground Floor");
                break;
            case 5:
                SetObjective("Executive Office Clearance (6/6)", "Report to Room 103 on Ground Floor");
                break;
            default:
                SetObjective("Submit Clearance Slip (6/6)", "Return to Registrar in Room 104 to unlock gates");
                break;
        }
    }

    private IEnumerator AnimateObjectiveUpdate()
    {
        if (_audioSource != null && updateSound != null)
        {
            _audioSource.PlayOneShot(updateSound, 0.7f);
        }

        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0.2f, t / 0.15f);
                yield return null;
            }

            t = 0f;
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
