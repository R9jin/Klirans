using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cashier Department Balance Sheet Math Mini-Game.
/// The player must calculate the exact total of 3-5 unsettled assessment fee line items
/// slid through Window 2 slot by the University Cashier (1st Floor, Room 102).
/// 
/// Features authentic university accounting ledger UI, paper texture, numeric keypad,
/// physical keyboard support, audio cues (paper slide, clicks, stamp thud, errors),
/// "PAID & CLEARED" rubber stamp animation, and ClearanceNPC integration for Signature 4.
/// </summary>
public class CashierBalancePuzzle : MonoBehaviour
{
    private static CashierBalancePuzzle _instance;
    public static CashierBalancePuzzle Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CashierBalancePuzzle>(FindObjectsInactive.Include);
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [System.Serializable]
    public class BalanceFeeItem
    {
        public string feeName;
        public int amount; // in Philippine Pesos
    }

    [System.Serializable]
    public class BalanceSheetChallenge
    {
        public string studentId = "STUDENT NO.: 2024-08912-CB";
        public string assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025";
        public List<BalanceFeeItem> feeItems = new List<BalanceFeeItem>();

        public int CorrectTotal
        {
            get
            {
                int total = 0;
                if (feeItems != null)
                {
                    foreach (var item in feeItems)
                    {
                        total += item.amount;
                    }
                }
                return total;
            }
        }
    }

    [Header("Curated Balance Sheet Challenges")]
    public List<BalanceSheetChallenge> challenges = new List<BalanceSheetChallenge>
    {
        new BalanceSheetChallenge
        {
            studentId = "STUDENT NO.: 2024-08912-CB",
            assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025",
            feeItems = new List<BalanceFeeItem>
            {
                new BalanceFeeItem { feeName = "Library Overdue Book Surcharge", amount = 35 },
                new BalanceFeeItem { feeName = "Computer Laboratory Maintenance Fee", amount = 145 },
                new BalanceFeeItem { feeName = "Athletic & Cultural Assessment", amount = 75 },
                new BalanceFeeItem { feeName = "Student Handbook & ID Validation", amount = 45 }
            } // Total = 300
        },
        new BalanceSheetChallenge
        {
            studentId = "STUDENT NO.: 2024-08912-CB",
            assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025",
            feeItems = new List<BalanceFeeItem>
            {
                new BalanceFeeItem { feeName = "Library Processing Fee", amount = 25 },
                new BalanceFeeItem { feeName = "Science Laboratory Consumables", amount = 150 },
                new BalanceFeeItem { feeName = "Miscellaneous Institutional Arrears", amount = 75 }
            } // Total = 250
        },
        new BalanceSheetChallenge
        {
            studentId = "STUDENT NO.: 2024-08912-CB",
            assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025",
            feeItems = new List<BalanceFeeItem>
            {
                new BalanceFeeItem { feeName = "Chemistry Glassware Breakage Fee", amount = 120 },
                new BalanceFeeItem { feeName = "Late Registration Processing Charge", amount = 80 },
                new BalanceFeeItem { feeName = "Medical & Dental Assessment", amount = 95 },
                new BalanceFeeItem { feeName = "Audio-Visual Room Facilities Dues", amount = 55 }
            } // Total = 350
        },
        new BalanceSheetChallenge
        {
            studentId = "STUDENT NO.: 2024-08912-CB",
            assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025",
            feeItems = new List<BalanceFeeItem>
            {
                new BalanceFeeItem { feeName = "IT Campus Network Infrastructure", amount = 130 },
                new BalanceFeeItem { feeName = "Student Council Publication Surcharge", amount = 60 },
                new BalanceFeeItem { feeName = "PE Department Uniform Arrears", amount = 110 },
                new BalanceFeeItem { feeName = "Guidance Assessment Evaluation Sheet", amount = 40 }
            } // Total = 340
        },
        new BalanceSheetChallenge
        {
            studentId = "STUDENT NO.: 2024-08912-CB",
            assessmentPeriod = "1ST SEMESTER A.Y. 2024-2025",
            feeItems = new List<BalanceFeeItem>
            {
                new BalanceFeeItem { feeName = "Overdue Library Book Replacement", amount = 85 },
                new BalanceFeeItem { feeName = "Biology Specimen Conservation Fee", amount = 115 },
                new BalanceFeeItem { feeName = "Security Access Card Replacement", amount = 50 }
            } // Total = 250
        }
    };

    [Header("UI Panels & Containers")]
    public GameObject backdropOverlay;
    public GameObject puzzlePanel;
    public CanvasGroup canvasGroup;

    [Header("Header & Metadata Texts")]
    public Text headerTitleText;
    public Text studentIdText;
    public Text assessmentPeriodText;
    public Text windowSlotNoticeText;

    [Header("Line Items Container & Rows")]
    public Transform lineItemsContainer;
    public GameObject lineItemRowPrefab; // optional if pre-baked in container

    [Header("Total Display & Input")]
    public Text totalDisplayText;
    public Transform totalRowTransform;
    public Text feedbackText;

    [Header("Keypad Action Buttons")]
    public Button[] numberButtons; // Digits 0 to 9
    public Button clearButton;
    public Button backspaceButton;
    public Button submitButton;
    public Button closeButton;

    [Header("Rubber Stamp Visual")]
    public Image paidStampImage;

    [Header("Audio Clips")]
    [Tooltip("Paper slide whoosh played when balance sheet slides through window slot.")]
    public AudioClip paperSlideAudio;

    [Tooltip("Keypad button click sound.")]
    public AudioClip keyClickAudio;

    [Tooltip("Rubber stamp impact thud when payment verified.")]
    public AudioClip stampAudio;

    [Tooltip("Disappointed error sound / crazy mumble on wrong arithmetic.")]
    public AudioClip errorAudio;

    // Runtime state
    public bool IsSolved { get; private set; } = false;
    public void RestoreSolvedState() { IsSolved = true; }
    public bool IsOpen => puzzlePanel != null && puzzlePanel.activeSelf;

    private BalanceSheetChallenge _activeChallenge;
    private string _currentInput = "";
    private AudioSource _audioSource;
    private Coroutine _shakeCoroutine;
    private Coroutine _stampAnimCoroutine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        // Bind keypad buttons
        if (numberButtons != null)
        {
            for (int i = 0; i < numberButtons.Length; i++)
            {
                int digit = i;
                if (numberButtons[i] != null)
                {
                    numberButtons[i].onClick.AddListener(() => OnDigitClicked(digit));
                }
            }
        }

        if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
        if (backspaceButton != null) backspaceButton.onClick.AddListener(OnBackspaceClicked);
        if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePuzzle);

        EnsureTexturesAssigned();
        EnsureLegibleTypography();
    }

    private void EnsureLegibleTypography()
    {
        Font boldFont = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
        if (boldFont == null)
            boldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lucida_Typewriter_Bold.ttf");
#endif
        if (boldFont == null) boldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (totalDisplayText != null)
        {
            if (boldFont != null) totalDisplayText.font = boldFont;
            totalDisplayText.fontSize = 24;
            totalDisplayText.fontStyle = FontStyle.Bold;
            totalDisplayText.horizontalOverflow = HorizontalWrapMode.Overflow;
            totalDisplayText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        if (headerTitleText != null)
        {
            if (boldFont != null) headerTitleText.font = boldFont;
            headerTitleText.fontStyle = FontStyle.Bold;
            headerTitleText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        }

        if (studentIdText != null)
        {
            if (boldFont != null) studentIdText.font = boldFont;
            studentIdText.fontStyle = FontStyle.Bold;
            studentIdText.color = new Color(0.12f, 0.10f, 0.08f, 1f);
        }

        if (assessmentPeriodText != null)
        {
            if (boldFont != null) assessmentPeriodText.font = boldFont;
            assessmentPeriodText.fontStyle = FontStyle.Bold;
            assessmentPeriodText.color = new Color(0.12f, 0.10f, 0.08f, 1f);
        }

        if (feedbackText != null)
        {
            if (boldFont != null) feedbackText.font = boldFont;
            feedbackText.fontStyle = FontStyle.Bold;
            feedbackText.fontSize = 15;
            feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }

    private void EnsureTexturesAssigned()
    {
        if (puzzlePanel != null)
        {
            var panelImg = puzzlePanel.GetComponent<Image>();
            if (panelImg != null && panelImg.sprite == null)
            {
                panelImg.sprite = LoadSprite("Assets/PlayerAssets/CashierPaperSheet.png");
                if (panelImg.sprite != null) panelImg.color = Color.white;
            }
        }

        if (paidStampImage != null && paidStampImage.sprite == null)
        {
            paidStampImage.sprite = LoadSprite("Assets/PlayerAssets/PaidClearedStamp.png");
            if (paidStampImage.sprite != null) paidStampImage.color = Color.white;
        }
    }

    private Sprite LoadSprite(string relativePath)
    {
        Sprite spr = null;
#if UNITY_EDITOR
        spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
        if (spr != null) return spr;

        Texture2D texEd = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        if (texEd != null)
        {
            return Sprite.Create(texEd, new Rect(0, 0, texEd.width, texEd.height), new Vector2(0.5f, 0.5f));
        }
#endif
        string fullPath = Path.Combine(Application.dataPath, "..", relativePath).Replace('\\', '/');
        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = Path.GetFileNameWithoutExtension(relativePath);
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CashierBalancePuzzle] Failed to load sprite from {fullPath}: {ex.Message}");
            }
        }
        return spr;
    }

    private void Start()
    {
        if (backdropOverlay != null) backdropOverlay.SetActive(false);
        if (puzzlePanel != null) puzzlePanel.SetActive(false);
        if (paidStampImage != null) paidStampImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || IsSolved) return;

        // Escape / Tab to close
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            ClosePuzzle();
            return;
        }

        // Backspace to delete last digit
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            OnBackspaceClicked();
            return;
        }

        // Enter to submit
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSubmitClicked();
            return;
        }

        // Physical keyboard number typing
        foreach (char c in Input.inputString)
        {
            if (char.IsDigit(c))
            {
                AppendDigit(c - '0');
            }
        }
    }

    /// <summary>
    /// Opens the Cashier Balance Sheet puzzle with a random or specified challenge.
    /// </summary>
    public void OpenPuzzle(int challengeIndex = -1)
    {
        if (challenges == null || challenges.Count == 0)
        {
            Debug.LogError("[CashierBalancePuzzle] No challenges defined!");
            return;
        }

        int index = challengeIndex >= 0 ? Mathf.Clamp(challengeIndex, 0, challenges.Count - 1) : Random.Range(0, challenges.Count);
        _activeChallenge = challenges[index];

        _currentInput = "";

        if (paidStampImage != null)
        {
            paidStampImage.gameObject.SetActive(false);
        }

        if (backdropOverlay != null)
        {
            backdropOverlay.SetActive(true);
        }

        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(true);
        }

        // Suppress interaction prompt so "Press E..." does not overlap
        PlayerInteract playerInteract = FindAnyObjectByType<PlayerInteract>();
        if (playerInteract != null && playerInteract.promptText != null)
        {
            playerInteract.promptText.gameObject.SetActive(false);
        }

        // Unlock mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Freeze player movement while examining balance sheet
        var playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(false);

        // Populate header and metadata
        if (headerTitleText != null)
        {
            headerTitleText.text = "PAMANTASAN NG CABUYAO\nOFFICE OF THE UNIVERSITY CASHIER — STATEMENT OF ACCOUNT";
        }

        if (studentIdText != null)
        {
            studentIdText.text = _activeChallenge.studentId;
        }

        if (assessmentPeriodText != null)
        {
            assessmentPeriodText.text = _activeChallenge.assessmentPeriod;
        }

        if (windowSlotNoticeText != null)
        {
            windowSlotNoticeText.text = "<i>(Assessment slip slid through Window 2 slot. Tally all pending line items below.)</i>";
        }

        if (feedbackText != null)
        {
            feedbackText.text = "<color=#3E3228>Calculate the total of all fees listed above. Enter amount and press SUBMIT.</color>";
        }

        // Play paper slide audio
        if (_audioSource != null && paperSlideAudio != null)
        {
            _audioSource.PlayOneShot(paperSlideAudio, 0.85f);
        }

        PopulateLineItems();
        RefreshInputDisplay();
    }

    /// <summary>
    /// Closes the balance sheet and restores normal gameplay controls.
    /// </summary>
    public void ClosePuzzle()
    {
        if (backdropOverlay != null) backdropOverlay.SetActive(false);
        if (puzzlePanel != null) puzzlePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(true);
    }

    private void PopulateLineItems()
    {
        if (lineItemsContainer == null || _activeChallenge == null) return;

        int itemCount = _activeChallenge.feeItems.Count;
        for (int i = 0; i < lineItemsContainer.childCount; i++)
        {
            Transform rowTrans = lineItemsContainer.GetChild(i);
            if (i < itemCount)
            {
                rowTrans.gameObject.SetActive(true);
                var item = _activeChallenge.feeItems[i];

                Text[] texts = rowTrans.GetComponentsInChildren<Text>(true);
                // Look for NameText and AmountText
                Text nameText = null;
                Text amountText = null;
                Text indexText = null;

                foreach (var t in texts)
                {
                    if (t.name.ToLower().Contains("name") || t.name.ToLower().Contains("desc")) nameText = t;
                    else if (t.name.ToLower().Contains("amount") || t.name.ToLower().Contains("price")) amountText = t;
                    else if (t.name.ToLower().Contains("num") || t.name.ToLower().Contains("index")) indexText = t;
                }

                Font boldFont = Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
                if (boldFont == null) boldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lucida_Typewriter_Bold.ttf");
#endif
                Color inkColor = new Color(0.06f, 0.06f, 0.06f, 1f);

                if (indexText != null)
                {
                    if (boldFont != null) indexText.font = boldFont;
                    indexText.fontStyle = FontStyle.Bold;
                    indexText.color = inkColor;
                    indexText.text = $"{i + 1}.";
                }

                if (nameText != null)
                {
                    if (boldFont != null) nameText.font = boldFont;
                    nameText.fontStyle = FontStyle.Bold;
                    nameText.color = inkColor;
                    nameText.text = item.feeName;
                }

                if (amountText != null)
                {
                    if (boldFont != null) amountText.font = boldFont;
                    amountText.fontStyle = FontStyle.Bold;
                    amountText.color = inkColor;
                    amountText.text = $"PHP {item.amount:N2}";
                }
            }
            else
            {
                rowTrans.gameObject.SetActive(false);
            }
        }
    }

    public void OnDigitClicked(int digit)
    {
        AppendDigit(digit);
    }

    private void AppendDigit(int digit)
    {
        if (_currentInput.Length >= 7) return; // Prevent excessively large input

        // Don't allow leading zeros
        if (_currentInput == "0") _currentInput = "";

        _currentInput += digit.ToString();

        if (_audioSource != null && keyClickAudio != null)
        {
            _audioSource.PlayOneShot(keyClickAudio, 0.45f);
        }

        RefreshInputDisplay();
    }

    public void OnBackspaceClicked()
    {
        if (_currentInput.Length > 0)
        {
            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
            if (_audioSource != null && keyClickAudio != null)
            {
                _audioSource.PlayOneShot(keyClickAudio, 0.4f);
            }
            RefreshInputDisplay();
        }
    }

    public void OnClearClicked()
    {
        _currentInput = "";
        if (_audioSource != null && keyClickAudio != null)
        {
            _audioSource.PlayOneShot(keyClickAudio, 0.4f);
        }
        RefreshInputDisplay();
    }

    private void RefreshInputDisplay()
    {
        if (totalDisplayText != null)
        {
            totalDisplayText.horizontalOverflow = HorizontalWrapMode.Overflow;
            totalDisplayText.verticalOverflow = VerticalWrapMode.Overflow;
            totalDisplayText.fontStyle = FontStyle.Bold;

            if (string.IsNullOrEmpty(_currentInput))
            {
                totalDisplayText.text = "PHP  0.00";
                totalDisplayText.color = new Color(0.25f, 0.22f, 0.20f, 0.85f);
            }
            else
            {
                if (int.TryParse(_currentInput, out int val))
                {
                    totalDisplayText.text = $"PHP  {val:N0}.00";
                }
                else
                {
                    totalDisplayText.text = $"PHP  {_currentInput}.00";
                }
                totalDisplayText.color = new Color(0.04f, 0.04f, 0.04f, 1f);
            }
        }
    }

    public void OnSubmitClicked()
    {
        if (_activeChallenge == null || IsSolved) return;

        if (string.IsNullOrEmpty(_currentInput))
        {
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#AA1111><b>NO TOTAL ENTERED.</b> Enter the total sum of fees on the keypad.</color>";
            }
            TriggerErrorFeedback();
            return;
        }

        if (int.TryParse(_currentInput, out int playerTotal))
        {
            int correctTotal = _activeChallenge.CorrectTotal;

            if (playerTotal == correctTotal)
            {
                // SUCCESS
                IsSolved = true;

                if (feedbackText != null)
                {
                    feedbackText.text = $"<color=#008822><b>CORRECT TOTAL: ₱{correctTotal:N2}!</b> Zero balance confirmed.</color>";
                }

                if (_stampAnimCoroutine != null) StopCoroutine(_stampAnimCoroutine);
                _stampAnimCoroutine = StartCoroutine(AnimatePaidStamp());

                StartCoroutine(SuccessRoutine());
            }
            else
            {
                // INCORRECT
                if (feedbackText != null)
                {
                    feedbackText.text = $"<color=#AA1111><b>INCORRECT TOTAL: ₱{playerTotal:N2}.</b> The cashier shakes her head. Re-tally the figures!</color>";
                }
                TriggerErrorFeedback();
            }
        }
        else
        {
            TriggerErrorFeedback();
        }
    }

    private void TriggerErrorFeedback()
    {
        if (_audioSource != null && errorAudio != null)
        {
            _audioSource.PlayOneShot(errorAudio, 0.8f);
        }

        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeTotalRow());

        // Clear input for quick retry
        _currentInput = "";
        RefreshInputDisplay();
    }

    private IEnumerator ShakeTotalRow()
    {
        if (totalRowTransform == null) yield break;

        Vector3 originalPos = totalRowTransform.localPosition;
        float elapsed = 0f;
        float duration = 0.35f;
        float magnitude = 12f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float xOffset = Random.Range(-1f, 1f) * magnitude;
            totalRowTransform.localPosition = new Vector3(originalPos.x + xOffset, originalPos.y, originalPos.z);
            yield return null;
        }

        totalRowTransform.localPosition = originalPos;
    }

    private IEnumerator AnimatePaidStamp()
    {
        if (paidStampImage == null) yield break;

        paidStampImage.gameObject.SetActive(true);
        RectTransform stampRt = paidStampImage.rectTransform;

        // Stamp slam animation
        stampRt.localScale = Vector3.one * 2.2f;
        stampRt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, -8f));

        CanvasGroup cg = paidStampImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = paidStampImage.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            stampRt.localScale = Vector3.Lerp(Vector3.one * 2.2f, Vector3.one, t);
            cg.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        stampRt.localScale = Vector3.one;
        cg.alpha = 1f;

        if (_audioSource != null && stampAudio != null)
        {
            _audioSource.PlayOneShot(stampAudio, 0.95f);
        }
    }

    private IEnumerator SuccessRoutine()
    {
        // Brief pause to admire the stamped balance sheet
        yield return new WaitForSeconds(1.8f);

        ClosePuzzle();

        // Notify Cashier ClearanceNPC (signature 4)
        var npcs = FindObjectsByType<ClearanceNPC>(FindObjectsInactive.Include);
        foreach (var npc in npcs)
        {
            if (npc.signatureIndex == 4)
            {
                npc.OnCashierPuzzleCompleted();
                break;
            }
        }
    }
}
