using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// University Registrar Document Sorting Mini-Game.
/// The player must sort a stack of official student grade records into the correct
/// archival trays (by department, by year level, or by semester) before the Registrar
/// in Room 104 (Window 2) will validate and sign the clearance slip.
/// 
/// Features authentic university document textures, drag-and-drop & point-and-click sorting,
/// keyboard hotkeys (1-4), tray drop audio, rubber stamp impact animation, and ClearanceNPC integration.
/// </summary>
public class RegistrarDocumentSortPuzzle : MonoBehaviour
{
    private static RegistrarDocumentSortPuzzle _instance;
    public static RegistrarDocumentSortPuzzle Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RegistrarDocumentSortPuzzle>(true);
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [System.Serializable]
    public class SortableDocument
    {
        public string studentName;
        public string studentId;
        public string department; // e.g. "College of Computing Studies (CCS)"
        public string degreeProgram; // e.g. "BS Information Technology"
        public string yearLevel; // e.g. "3rd Year (Junior)"
        public string semester; // e.g. "1st Semester A.Y. 2024-2025"
        public string gradeSummary; // e.g. "IT301: 1.25 | CS302: 1.50 | GE104: 1.75"
        public int targetTrayIndex; // 0-indexed destination tray
    }

    [System.Serializable]
    public class ArchivalTrayConfig
    {
        public string trayLabel; // e.g. "TRAY 1: COMPUTING (CCS)"
        public string categoryDetail; // e.g. "BSIT, BSCS, ACT"
        public Color trayAccentColor = new Color(0.2f, 0.45f, 0.25f);
    }

    [System.Serializable]
    public class SortingChallenge
    {
        public string criteriaTitle; // e.g. "CLASSIFICATION CRITERIA: SORT BY DEGREE DEPARTMENT"
        public string criteriaHint;  // e.g. "Check the student's college/department header and file into the matching tray."
        public List<ArchivalTrayConfig> trays = new List<ArchivalTrayConfig>();
        public List<SortableDocument> documents = new List<SortableDocument>();
    }

    [Header("Sorting Challenges (Department, Year Level, Semester)")]
    public List<SortingChallenge> challenges = new List<SortingChallenge>
    {
        // Challenge 0: Sort by Department (3 Trays: CCS, CBA, COE)
        new SortingChallenge
        {
            criteriaTitle = "ARCHIVAL CRITERIA: SORT BY COLLEGE DEPARTMENT",
            criteriaHint = "File each student's official grade slip into their corresponding academic department tray.",
            trays = new List<ArchivalTrayConfig>
            {
                new ArchivalTrayConfig { trayLabel = "[ 1 ] COMPUTING (CCS)", categoryDetail = "BS Information Tech / BS Computer Sci", trayAccentColor = new Color(0.18f, 0.40f, 0.65f) },
                new ArchivalTrayConfig { trayLabel = "[ 2 ] BUSINESS (CBA)", categoryDetail = "BS Business Admin / BS Accountancy", trayAccentColor = new Color(0.65f, 0.38f, 0.12f) },
                new ArchivalTrayConfig { trayLabel = "[ 3 ] ENGINEERING (COE)", categoryDetail = "BS Civil Eng / BS Industrial Eng", trayAccentColor = new Color(0.55f, 0.18f, 0.18f) }
            },
            documents = new List<SortableDocument>
            {
                new SortableDocument { studentName = "SANTOS, JUAN MIGUEL M.", studentId = "2024-01928-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Information Technology", yearLevel = "3rd Year", semester = "1st Semester", gradeSummary = "IT311: 1.25 | CS302: 1.50 | GE104: 1.75", targetTrayIndex = 0 },
                new SortableDocument { studentName = "DELA CRUZ, CARLO P.", studentId = "2023-04412-CB", department = "College of Business Administration (CBA)", degreeProgram = "BS Accountancy", yearLevel = "2nd Year", semester = "1st Semester", gradeSummary = "ACT201: 1.50 | TAX101: 1.75 | LAW101: 2.00", targetTrayIndex = 1 },
                new SortableDocument { studentName = "REYES, MARIA CLARA C.", studentId = "2022-08819-CB", department = "College of Engineering (COE)", degreeProgram = "BS Civil Engineering", yearLevel = "4th Year", semester = "1st Semester", gradeSummary = "CE401: 1.25 | STR402: 1.50 | HYD301: 1.75", targetTrayIndex = 2 },
                new SortableDocument { studentName = "GONZALES, PAOLO R.", studentId = "2024-03115-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Computer Science", yearLevel = "1st Year", semester = "1st Semester", gradeSummary = "CS101: 1.00 | MATH101: 1.25 | ENG101: 1.50", targetTrayIndex = 0 },
                new SortableDocument { studentName = "AQUINO, BIANCA T.", studentId = "2023-09551-CB", department = "College of Business Administration (CBA)", degreeProgram = "BS Business Administration", yearLevel = "3rd Year", semester = "1st Semester", gradeSummary = "MKT301: 1.50 | FIN302: 1.75 | MGT201: 1.25", targetTrayIndex = 1 },
                new SortableDocument { studentName = "MENDOZA, JOSHUA D.", studentId = "2022-07734-CB", department = "College of Engineering (COE)", degreeProgram = "BS Industrial Engineering", yearLevel = "4th Year", semester = "1st Semester", gradeSummary = "IE401: 1.50 | OPR302: 1.75 | SAF201: 1.25", targetTrayIndex = 2 }
            }
        },
        // Challenge 1: Sort by Year Level (4 Trays: 1st, 2nd, 3rd, 4th Year)
        new SortingChallenge
        {
            criteriaTitle = "ARCHIVAL CRITERIA: SORT BY ACADEMIC YEAR LEVEL",
            criteriaHint = "File each student's official grade slip according to their current academic standing / year level.",
            trays = new List<ArchivalTrayConfig>
            {
                new ArchivalTrayConfig { trayLabel = "[ 1 ] 1ST YEAR (FRESHMAN)", categoryDetail = "Level 100 General Foundation", trayAccentColor = new Color(0.25f, 0.50f, 0.25f) },
                new ArchivalTrayConfig { trayLabel = "[ 2 ] 2ND YEAR (SOPHOMORE)", categoryDetail = "Level 200 Core Curriculum", trayAccentColor = new Color(0.20f, 0.40f, 0.60f) },
                new ArchivalTrayConfig { trayLabel = "[ 3 ] 3RD YEAR (JUNIOR)", categoryDetail = "Level 300 Major Specialization", trayAccentColor = new Color(0.60f, 0.35f, 0.15f) },
                new ArchivalTrayConfig { trayLabel = "[ 4 ] 4TH YEAR (SENIOR)", categoryDetail = "Level 400 Practicum & Capstone", trayAccentColor = new Color(0.55f, 0.15f, 0.35f) }
            },
            documents = new List<SortableDocument>
            {
                new SortableDocument { studentName = "RAMOS, ERICKA S.", studentId = "2025-01004-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Information Technology", yearLevel = "1st Year (Freshman)", semester = "1st Semester", gradeSummary = "IT101: 1.25 | INT102: 1.50 | NSTP1: 1.00", targetTrayIndex = 0 },
                new SortableDocument { studentName = "VILLANUEVA, MARK K.", studentId = "2023-03310-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Computer Science", yearLevel = "3rd Year (Junior)", semester = "1st Semester", gradeSummary = "ALGO301: 1.50 | AUTO302: 1.75 | RES101: 1.25", targetTrayIndex = 2 },
                new SortableDocument { studentName = "CASTILLO, CHLOE A.", studentId = "2024-05521-CB", department = "College of Business Administration (CBA)", degreeProgram = "BS Accountancy", yearLevel = "2nd Year (Sophomore)", semester = "1st Semester", gradeSummary = "ACC201: 1.50 | ECO101: 1.25 | TAX101: 1.75", targetTrayIndex = 1 },
                new SortableDocument { studentName = "TORRES, GABRIEL F.", studentId = "2022-09941-CB", department = "College of Engineering (COE)", degreeProgram = "BS Civil Engineering", yearLevel = "4th Year (Senior)", semester = "1st Semester", gradeSummary = "THES401: 1.00 | ENG402: 1.25 | ETH101: 1.50", targetTrayIndex = 3 },
                new SortableDocument { studentName = "BAUTISTA, LEAH M.", studentId = "2025-02198-CB", department = "College of Education (CED)", degreeProgram = "BSEd English", yearLevel = "1st Year (Freshman)", semester = "1st Semester", gradeSummary = "ENG101: 1.00 | LIT102: 1.25 | FIL101: 1.50", targetTrayIndex = 0 },
                new SortableDocument { studentName = "CRUZ, DANIEL J.", studentId = "2024-06782-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Information Technology", yearLevel = "2nd Year (Sophomore)", semester = "1st Semester", gradeSummary = "OOP201: 1.25 | DBM202: 1.50 | WEB203: 1.25", targetTrayIndex = 1 }
            }
        },
        // Challenge 2: Sort by Semester (3 Trays: 1st Sem, 2nd Sem, Summer/Midyear)
        new SortingChallenge
        {
            criteriaTitle = "ARCHIVAL CRITERIA: SORT BY ACADEMIC SEMESTER",
            criteriaHint = "File each student's official grade slip according to the academic term recorded on the header.",
            trays = new List<ArchivalTrayConfig>
            {
                new ArchivalTrayConfig { trayLabel = "[ 1 ] 1ST SEMESTER", categoryDetail = "Academic Term: Regular Fall Term", trayAccentColor = new Color(0.20f, 0.40f, 0.60f) },
                new ArchivalTrayConfig { trayLabel = "[ 2 ] 2ND SEMESTER", categoryDetail = "Academic Term: Regular Spring Term", trayAccentColor = new Color(0.55f, 0.35f, 0.15f) },
                new ArchivalTrayConfig { trayLabel = "[ 3 ] MIDYEAR / SUMMER TERM", categoryDetail = "Academic Term: Accelerated Summer Term", trayAccentColor = new Color(0.55f, 0.20f, 0.20f) }
            },
            documents = new List<SortableDocument>
            {
                new SortableDocument { studentName = "NAVARRO, MIGUEL V.", studentId = "2024-01123-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Information Technology", yearLevel = "2nd Year", semester = "1st Semester A.Y. 2024-2025", gradeSummary = "IT201: 1.25 | NET202: 1.50 | GE105: 1.75", targetTrayIndex = 0 },
                new SortableDocument { studentName = "SALAZAR, KATRINA B.", studentId = "2023-04987-CB", department = "College of Business Administration (CBA)", degreeProgram = "BS Business Administration", yearLevel = "3rd Year", semester = "2nd Semester A.Y. 2023-2024", gradeSummary = "MKT302: 1.50 | HRM301: 1.25 | LAW202: 1.75", targetTrayIndex = 1 },
                new SortableDocument { studentName = "DOMINGO, ANGELO R.", studentId = "2023-08711-CB", department = "College of Engineering (COE)", degreeProgram = "BS Civil Engineering", yearLevel = "3rd Year", semester = "Midyear / Summer Term 2024", gradeSummary = "SURV201: 1.50 | CAD101: 1.25", targetTrayIndex = 2 },
                new SortableDocument { studentName = "CORTEZ, DIANA P.", studentId = "2024-02341-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Computer Science", yearLevel = "1st Year", semester = "1st Semester A.Y. 2024-2025", gradeSummary = "PROG101: 1.00 | DIS102: 1.25 | ENG101: 1.50", targetTrayIndex = 0 },
                new SortableDocument { studentName = "FLORES, RAFAEL N.", studentId = "2023-06619-CB", department = "College of Computing Studies (CCS)", degreeProgram = "BS Information Technology", yearLevel = "3rd Year", semester = "2nd Semester A.Y. 2023-2024", gradeSummary = "CAP301: 1.25 | SYS302: 1.50 | ETH201: 1.75", targetTrayIndex = 1 },
                new SortableDocument { studentName = "GARCIA, SOFIA L.", studentId = "2024-09908-CB", department = "College of Arts & Sciences (CAS)", degreeProgram = "BS Psychology", yearLevel = "2nd Year", semester = "Midyear / Summer Term 2024", gradeSummary = "PSY201: 1.25 | STAT101: 1.50", targetTrayIndex = 2 }
            }
        }
    };

    [Header("UI Panels & Containers")]
    public GameObject backdropOverlay;
    public GameObject puzzlePanel;
    public CanvasGroup canvasGroup;

    [Header("Header & Criteria Texts")]
    public Text criteriaTitleText;
    public Text criteriaHintText;
    public Text progressCounterText;
    public Text feedbackText;

    [Header("Archival Trays Container & Trays")]
    public Transform traysContainer;
    public List<ArchivalTrayUI> trayUIList = new List<ArchivalTrayUI>();

    [Header("Document Card Display")]
    public RectTransform documentCardTransform;
    public Text docStudentNameText;
    public Text docStudentIdText;
    public Text docDepartmentText;
    public Text docYearLevelText;
    public Text docSemesterText;
    public Text docGradesText;
    public Text stackRemainingText;

    [Header("Rubber Stamp Visual")]
    public Image archivedStampImage;

    [Header("Action Buttons")]
    public Button closeButton;

    [Header("Audio Clips")]
    [Tooltip("Paper slide swoosh played when puzzle opens or card advances.")]
    public AudioClip paperSlideAudio;

    [Tooltip("Paper shuffle / tray drop sound played on successful filing.")]
    public AudioClip trayDropAudio;

    [Tooltip("Rubber stamp impact thud when all documents filed.")]
    public AudioClip stampAudio;

    [Tooltip("Registrar buzzer / crazy mumble when document is misfiled.")]
    public AudioClip errorAudio;

    // Runtime state
    public bool IsSolved { get; private set; } = false;
    public bool IsOpen => puzzlePanel != null && puzzlePanel.activeSelf;

    private SortingChallenge _activeChallenge;
    private int _currentDocIndex = 0;
    private AudioSource _audioSource;
    private Coroutine _shakeCoroutine;
    private Coroutine _stampCoroutine;
    private Vector2 _docCardInitialPos;

    [System.Serializable]
    public class ArchivalTrayUI
    {
        public GameObject trayObject;
        public RectTransform trayRect;
        public Image trayBackground;
        public Text trayTitleText;
        public Text trayDetailText;
        public Button sortButton;
    }

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

        if (closeButton != null) closeButton.onClick.AddListener(ClosePuzzle);

        if (documentCardTransform != null)
        {
            _docCardInitialPos = documentCardTransform.anchoredPosition;
            SetupDragHandlers();
        }
    }

    private void Start()
    {
        if (backdropOverlay != null) backdropOverlay.SetActive(false);
        if (puzzlePanel != null) puzzlePanel.SetActive(false);
        if (archivedStampImage != null) archivedStampImage.gameObject.SetActive(false);
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

        // Numeric hotkeys 1, 2, 3, 4
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TrySortToTray(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TrySortToTray(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TrySortToTray(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) TrySortToTray(3);
    }

    /// <summary>
    /// Opens the Document Sorting puzzle with a random or specified challenge.
    /// </summary>
    public void OpenPuzzle(int challengeIndex = -1)
    {
        if (challenges == null || challenges.Count == 0)
        {
            Debug.LogError("[RegistrarDocumentSortPuzzle] No challenges defined!");
            return;
        }

        int index = challengeIndex >= 0 ? Mathf.Clamp(challengeIndex, 0, challenges.Count - 1) : Random.Range(0, challenges.Count);
        _activeChallenge = challenges[index];
        _currentDocIndex = 0;

        if (archivedStampImage != null) archivedStampImage.gameObject.SetActive(false);
        if (backdropOverlay != null) backdropOverlay.SetActive(true);
        if (puzzlePanel != null) puzzlePanel.SetActive(true);

        if (documentCardTransform != null)
        {
            documentCardTransform.anchoredPosition = _docCardInitialPos;
            documentCardTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1.2f, 1.2f));
        }

        // Suppress player interaction prompt so "Press E..." does not overlap
        PlayerInteract playerInteract = FindObjectOfType<PlayerInteract>();
        if (playerInteract != null && playerInteract.promptText != null)
        {
            playerInteract.promptText.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var playerMovement = FindObjectOfType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(false);

        // Header info
        if (criteriaTitleText != null) criteriaTitleText.text = _activeChallenge.criteriaTitle;
        if (criteriaHintText != null) criteriaHintText.text = _activeChallenge.criteriaHint;
        if (feedbackText != null) feedbackText.text = "<color=#3E3228>Drag the document or click a destination tray / press [1-4] to file it.</color>";

        // Configure trays
        SetupTrays();

        // Play slide audio
        if (_audioSource != null && paperSlideAudio != null)
        {
            _audioSource.PlayOneShot(paperSlideAudio, 0.85f);
        }

        RefreshCurrentDocumentDisplay();
    }

    /// <summary>
    /// Closes the sorting desk and restores normal gameplay controls.
    /// </summary>
    public void ClosePuzzle()
    {
        if (backdropOverlay != null) backdropOverlay.SetActive(false);
        if (puzzlePanel != null) puzzlePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var playerMovement = FindObjectOfType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(true);
    }

    private void SetupTrays()
    {
        if (_activeChallenge == null) return;
        int trayCount = _activeChallenge.trays.Count;

        for (int i = 0; i < trayUIList.Count; i++)
        {
            var trayUI = trayUIList[i];
            if (i < trayCount)
            {
                trayUI.trayObject.SetActive(true);
                var cfg = _activeChallenge.trays[i];

                if (trayUI.trayTitleText != null) trayUI.trayTitleText.text = cfg.trayLabel;
                if (trayUI.trayDetailText != null) trayUI.trayDetailText.text = cfg.categoryDetail;
                if (trayUI.trayBackground != null) trayUI.trayBackground.color = cfg.trayAccentColor;

                int trayIndex = i;
                if (trayUI.sortButton != null)
                {
                    trayUI.sortButton.onClick.RemoveAllListeners();
                    trayUI.sortButton.onClick.AddListener(() => TrySortToTray(trayIndex));
                }
            }
            else
            {
                trayUI.trayObject.SetActive(false);
            }
        }
    }

    private void RefreshCurrentDocumentDisplay()
    {
        if (_activeChallenge == null || _activeChallenge.documents == null) return;

        int total = _activeChallenge.documents.Count;
        if (_currentDocIndex >= total)
        {
            // All sorted!
            OnAllDocumentsSorted();
            return;
        }

        SortableDocument doc = _activeChallenge.documents[_currentDocIndex];

        if (docStudentNameText != null) docStudentNameText.text = $"<b>{doc.studentName}</b>";
        if (docStudentIdText != null) docStudentIdText.text = $"STUDENT NO.: {doc.studentId}";
        if (docDepartmentText != null) docDepartmentText.text = $"COLLEGE: <b>{doc.department}</b>\nPROGRAM: {doc.degreeProgram}";
        if (docYearLevelText != null) docYearLevelText.text = $"YEAR LEVEL: <b>{doc.yearLevel}</b>";
        if (docSemesterText != null) docSemesterText.text = $"SEMESTER: <b>{doc.semester}</b>";
        if (docGradesText != null) docGradesText.text = $"OFFICIAL GRADE RECORD:\n<i>{doc.gradeSummary}</i>";

        if (progressCounterText != null)
        {
            progressCounterText.text = $"DOCUMENTS FILED: <b>{_currentDocIndex} / {total}</b>";
        }

        if (stackRemainingText != null)
        {
            int remaining = total - _currentDocIndex;
            stackRemainingText.text = $"REMAINING IN STACK: <b>{remaining}</b>";
        }
    }

    /// <summary>
    /// Attempts to sort the active document into the specified tray.
    /// </summary>
    public void TrySortToTray(int trayIndex)
    {
        if (_activeChallenge == null || IsSolved) return;
        if (_currentDocIndex >= _activeChallenge.documents.Count) return;

        SortableDocument doc = _activeChallenge.documents[_currentDocIndex];

        if (doc.targetTrayIndex == trayIndex)
        {
            // CORRECT SORT
            if (_audioSource != null && trayDropAudio != null)
            {
                _audioSource.PlayOneShot(trayDropAudio, 0.75f);
            }

            if (feedbackText != null)
            {
                feedbackText.text = $"<color=#008822><b>FILED PROPERLY!</b> {doc.studentName} filed into Tray {trayIndex + 1}.</color>";
            }

            StartCoroutine(AnimateDocumentIntoTray(trayIndex));
        }
        else
        {
            // INCORRECT SORT
            string expectedTrayName = (_activeChallenge.trays != null && doc.targetTrayIndex < _activeChallenge.trays.Count)
                ? _activeChallenge.trays[doc.targetTrayIndex].trayLabel
                : $"Tray {doc.targetTrayIndex + 1}";

            if (feedbackText != null)
            {
                feedbackText.text = $"<color=#AA1111><b>MISFILED!</b> {doc.studentName} belongs in <b>{expectedTrayName}</b>. Re-examine the criteria!</color>";
            }

            if (_audioSource != null && errorAudio != null)
            {
                _audioSource.PlayOneShot(errorAudio, 0.8f);
            }

            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeDocumentCard());
        }
    }

    private IEnumerator AnimateDocumentIntoTray(int trayIndex)
    {
        if (documentCardTransform == null)
        {
            _currentDocIndex++;
            RefreshCurrentDocumentDisplay();
            yield break;
        }

        Vector2 startPos = documentCardTransform.anchoredPosition;
        Vector2 targetPos = startPos + new Vector2(0f, 180f);

        if (trayIndex < trayUIList.Count && trayUIList[trayIndex].trayRect != null)
        {
            targetPos = trayUIList[trayIndex].trayRect.anchoredPosition;
        }

        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            documentCardTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            documentCardTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.45f, t);
            yield return null;
        }

        // Reset document card for next document
        documentCardTransform.anchoredPosition = _docCardInitialPos;
        documentCardTransform.localScale = Vector3.one;
        documentCardTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1.5f, 1.5f));

        _currentDocIndex++;

        if (_audioSource != null && paperSlideAudio != null)
        {
            _audioSource.PlayOneShot(paperSlideAudio, 0.5f);
        }

        RefreshCurrentDocumentDisplay();
    }

    private IEnumerator ShakeDocumentCard()
    {
        if (documentCardTransform == null) yield break;

        Vector2 originalPos = _docCardInitialPos;
        float elapsed = 0f;
        float duration = 0.35f;
        float magnitude = 14f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float xOffset = Random.Range(-1f, 1f) * magnitude;
            documentCardTransform.anchoredPosition = new Vector2(originalPos.x + xOffset, originalPos.y);
            yield return null;
        }

        documentCardTransform.anchoredPosition = originalPos;
    }

    private void OnAllDocumentsSorted()
    {
        IsSolved = true;

        if (feedbackText != null)
        {
            feedbackText.text = "<color=#008822><b>ALL GRADE RECORDS FILED & ARCHIVED!</b> Registrar clearance approved.</color>";
        }

        if (progressCounterText != null)
        {
            progressCounterText.text = "STATUS: <color=#00AA22><b>COMPLETE (6 / 6 FILED)</b></color>";
        }

        if (_stampCoroutine != null) StopCoroutine(_stampCoroutine);
        _stampCoroutine = StartCoroutine(AnimateArchivedStamp());

        StartCoroutine(SuccessRoutine());
    }

    private IEnumerator AnimateArchivedStamp()
    {
        if (archivedStampImage == null) yield break;

        archivedStampImage.gameObject.SetActive(true);
        RectTransform stampRt = archivedStampImage.rectTransform;

        stampRt.localScale = Vector3.one * 2.3f;
        stampRt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, -6f));

        CanvasGroup cg = archivedStampImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = archivedStampImage.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            stampRt.localScale = Vector3.Lerp(Vector3.one * 2.3f, Vector3.one, t);
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
        yield return new WaitForSeconds(1.8f);

        ClosePuzzle();

        // Notify Registrar ClearanceNPC (signature 3)
        var npcs = FindObjectsOfType<ClearanceNPC>();
        foreach (var npc in npcs)
        {
            if (npc.signatureIndex == 3)
            {
                npc.OnRegistrarPuzzleCompleted();
                break;
            }
        }
    }

    // ── Drag & Drop Implementation ─────────────────────────────────────────────
    private void SetupDragHandlers()
    {
        EventTrigger trigger = documentCardTransform.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = documentCardTransform.gameObject.AddComponent<EventTrigger>();

        // Drag entry
        EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => { OnCardDragged((PointerEventData)data); });
        trigger.triggers.Add(dragEntry);

        // End Drag entry
        EventTrigger.Entry endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener((data) => { OnCardEndDrag((PointerEventData)data); });
        trigger.triggers.Add(endDragEntry);
    }

    private void OnCardDragged(PointerEventData data)
    {
        if (IsSolved || !IsOpen) return;
        documentCardTransform.position = data.position;
    }

    private void OnCardEndDrag(PointerEventData data)
    {
        if (IsSolved || !IsOpen) return;

        int closestTray = -1;
        float closestDist = 160f; // threshold radius

        for (int i = 0; i < trayUIList.Count; i++)
        {
            if (!trayUIList[i].trayObject.activeSelf) continue;
            float dist = Vector2.Distance(documentCardTransform.position, trayUIList[i].trayRect.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTray = i;
            }
        }

        if (closestTray >= 0)
        {
            TrySortToTray(closestTray);
        }
        else
        {
            // Snap back
            documentCardTransform.anchoredPosition = _docCardInitialPos;
        }
    }
}
