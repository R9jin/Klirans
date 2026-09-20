using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guidance and Counseling Scrambled Letter Core Values Puzzle.
/// Evaluates the student's alignment with PnC's institutional pillars:
///   N — Nurturing Faith and Respect
///   P — Promoting Compassion and Inclusivity
///   C — Cultivating Integrity and Excellence
///
/// Features interactive letter tiles, answer slots, dynamic audio reactions
/// (ambient murmurs, crazy mumbles on mistake, validation stamp), and seamless
/// integration with ClearanceNPC for Signature 1.
/// </summary>
public class GuidanceWordPuzzle : MonoBehaviour
{
    private static GuidanceWordPuzzle _instance;
    public static GuidanceWordPuzzle Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GuidanceWordPuzzle>(FindObjectsInactive.Include);
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [System.Serializable]
    public class CoreValueChallenge
    {
        public string pillarLabel;      // e.g. "C — Cultivating Integrity and Excellence"
        public string promptClue;       // e.g. "As a God-fearing institution that respects people of diverse faiths, NPC adheres to:"
        public string blankDisplay;     // e.g. "C — Cultivating [ ? ] and Excellence"
        public string targetWord;       // "INTEGRITY"
    }

    [Header("Core Value Challenges")]
    public List<CoreValueChallenge> challenges = new List<CoreValueChallenge>
    {
        new CoreValueChallenge
        {
            pillarLabel = "C — Cultivating Integrity and Excellence",
            promptClue = "As a God-fearing institution that respects people of diverse faiths, NPC adheres to the following core value:",
            blankDisplay = "C — Cultivating [ ? ] and Excellence",
            targetWord = "INTEGRITY"
        },
        new CoreValueChallenge
        {
            pillarLabel = "P — Promoting Compassion and Inclusivity",
            promptClue = "As a God-fearing institution that respects people of diverse faiths, NPC adheres to the following core value:",
            blankDisplay = "P — Promoting [ ? ] and Inclusivity",
            targetWord = "COMPASSION"
        },
        new CoreValueChallenge
        {
            pillarLabel = "N — Nurturing Faith and Respect",
            promptClue = "As a God-fearing institution that respects people of diverse faiths, NPC adheres to the following core value:",
            blankDisplay = "N — Nurturing Faith and [ ? ]",
            targetWord = "RESPECT"
        }
    };

    [Header("UI Panels & Text Elements")]
    public GameObject backdropOverlay;
    public GameObject puzzlePanel;
    public CanvasGroup canvasGroup;
    public Text headerTitleText;
    public Text institutionClueText;
    public Text pillarPromptText;
    public Text feedbackText;

    [Header("Tile & Slot Containers")]
    public Transform answerSlotsContainer;
    public Transform letterTilesContainer;

    [Header("Action Buttons")]
    public Button submitButton;
    public Button clearButton;
    public Button closeButton;

    [Header("Audio Clips")]
    [Tooltip("Ambient eerie murmur played softly while counseling puzzle is open.")]
    public AudioClip murmurAudio;

    [Tooltip("Disturbed mumbles played when puzzle opens or on incorrect submission.")]
    public AudioClip crazyMumbleAudio;

    [Tooltip("Paper stamp / xerox sound played upon correct validation.")]
    public AudioClip stampAudio;

    [Tooltip("Click feedback when picking/dropping a letter.")]
    public AudioClip tileClickAudio;

    // Runtime state
    public bool IsSolved { get; private set; } = false;
    public bool IsOpen => puzzlePanel != null && puzzlePanel.activeSelf;

    private CoreValueChallenge _activeChallenge;
    private List<char> _scrambledLetters = new List<char>();
    private List<int> _placedTileIndices = new List<int>(); // tracks which scrambled tile is in each answer slot
    private AudioSource _audioSource;
    private AudioSource _murmurLoopSource;
    private Coroutine _shakeCoroutine;

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

        // Dedicated looping source for atmospheric murmurs
        GameObject murmurObj = new GameObject("MurmurAudioLoop");
        murmurObj.transform.SetParent(transform, false);
        _murmurLoopSource = murmurObj.AddComponent<AudioSource>();
        _murmurLoopSource.playOnAwake = false;
        _murmurLoopSource.loop = true;
        _murmurLoopSource.spatialBlend = 0f;
        _murmurLoopSource.volume = 0.25f;

        if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePuzzle);
    }

    private void Start()
    {
        if (backdropOverlay != null)
            backdropOverlay.SetActive(false);
        if (puzzlePanel != null)
            puzzlePanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Escape / Tab to close
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            ClosePuzzle();
            return;
        }

        // Backspace to remove last letter
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            RemoveLastLetter();
            return;
        }

        // Return / Enter to submit
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSubmitClicked();
            return;
        }

        // Support keyboard typing of letters
        foreach (char c in Input.inputString)
        {
            if (char.IsLetter(c))
            {
                TryTypeLetter(char.ToUpper(c));
            }
        }
    }

    /// <summary>
    /// Opens the Guidance Scrambled Core Values puzzle with the specified or random challenge.
    /// </summary>
    public void OpenPuzzle(int challengeIndex = 0)
    {
        if (challenges == null || challenges.Count == 0)
        {
            Debug.LogError("[GuidanceWordPuzzle] No challenges defined!");
            return;
        }

        int index = Mathf.Clamp(challengeIndex, 0, challenges.Count - 1);
        _activeChallenge = challenges[index];

        _placedTileIndices.Clear();
        GenerateScrambledLetters(_activeChallenge.targetWord);

        if (backdropOverlay != null)
        {
            backdropOverlay.SetActive(true);
        }

        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(true);
        }

        // Immediately suppress interaction prompt so "Press E..." does not overlap
        PlayerInteract playerInteract = FindAnyObjectByType<PlayerInteract>();
        if (playerInteract != null && playerInteract.promptText != null)
        {
            playerInteract.promptText.gameObject.SetActive(false);
        }

        // Populate clue labels
        if (headerTitleText != null)
            headerTitleText.text = "OFFICE OF GUIDANCE AND COUNSELING\nMORAL EVALUATION & CORE VALUES ASSESSMENT";

        if (institutionClueText != null)
        {
            institutionClueText.text = $"\"{_activeChallenge.promptClue}\"";
            institutionClueText.color = new Color(0.22f, 0.18f, 0.14f, 1f);
        }

        if (pillarPromptText != null)
        {
            pillarPromptText.text = $"<b>{_activeChallenge.blankDisplay}</b>   <size=15><color=#7A583E>({_activeChallenge.targetWord.Length} Letters)</color></size>";
            pillarPromptText.color = new Color(0.15f, 0.12f, 0.10f, 1f);
        }

        if (feedbackText != null)
        {
            feedbackText.text = "<color=#3E3228>Click letter tiles below or type on keyboard to spell the missing pillar.</color>";
        }

        // Lock / Unlock mouse
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Freeze player movement while evaluation sheet is open
        var playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(false);

        // Start ambient murmurs
        if (_murmurLoopSource != null && murmurAudio != null)
        {
            _murmurLoopSource.clip = murmurAudio;
            _murmurLoopSource.Play();
        }

        // Play counselor opening mumble
        if (_audioSource != null && crazyMumbleAudio != null)
        {
            _audioSource.PlayOneShot(crazyMumbleAudio, 0.65f);
        }

        RefreshUI();
    }

    /// <summary>
    /// Closes the puzzle sheet and restores normal gameplay controls.
    /// </summary>
    public void ClosePuzzle()
    {
        if (backdropOverlay != null)
            backdropOverlay.SetActive(false);

        if (puzzlePanel != null)
            puzzlePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null) playerMovement.SetControlsEnabled(true);

        if (_murmurLoopSource != null && _murmurLoopSource.isPlaying)
        {
            _murmurLoopSource.Stop();
        }
    }

    private void GenerateScrambledLetters(string word)
    {
        _scrambledLetters = new List<char>(word.ToCharArray());

        // Fisher-Yates shuffle ensuring it doesn't match original by chance
        int attempts = 0;
        do
        {
            for (int i = _scrambledLetters.Count - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                char temp = _scrambledLetters[i];
                _scrambledLetters[i] = _scrambledLetters[rnd];
                _scrambledLetters[rnd] = temp;
            }
            attempts++;
        } while (new string(_scrambledLetters.ToArray()) == word && attempts < 10);
    }

    /// <summary>
    /// Called when player clicks an available scrambled letter tile in the bottom tray.
    /// </summary>
    public void OnTrayTileClicked(int tileIndex)
    {
        if (_placedTileIndices.Contains(tileIndex)) return;
        if (_placedTileIndices.Count >= _activeChallenge.targetWord.Length) return;

        _placedTileIndices.Add(tileIndex);

        if (_audioSource != null && tileClickAudio != null)
            _audioSource.PlayOneShot(tileClickAudio, 0.5f);

        RefreshUI();

        // Auto-check if all slots filled
        if (_placedTileIndices.Count == _activeChallenge.targetWord.Length)
        {
            ValidateAnswer();
        }
    }

    /// <summary>
    /// Called when player clicks an answer slot to return that letter back to the tray.
    /// </summary>
    public void OnAnswerSlotClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _placedTileIndices.Count) return;

        _placedTileIndices.RemoveAt(slotIndex);

        if (_audioSource != null && tileClickAudio != null)
            _audioSource.PlayOneShot(tileClickAudio, 0.4f);

        RefreshUI();
    }

    public void OnClearClicked()
    {
        _placedTileIndices.Clear();
        if (_audioSource != null && tileClickAudio != null)
            _audioSource.PlayOneShot(tileClickAudio, 0.4f);

        RefreshUI();
    }

    public void OnSubmitClicked()
    {
        ValidateAnswer();
    }

    private void RemoveLastLetter()
    {
        if (_placedTileIndices.Count > 0)
        {
            _placedTileIndices.RemoveAt(_placedTileIndices.Count - 1);
            if (_audioSource != null && tileClickAudio != null)
                _audioSource.PlayOneShot(tileClickAudio, 0.4f);
            RefreshUI();
        }
    }

    private void TryTypeLetter(char typedLetter)
    {
        for (int i = 0; i < _scrambledLetters.Count; i++)
        {
            if (_scrambledLetters[i] == typedLetter && !_placedTileIndices.Contains(i))
            {
                OnTrayTileClicked(i);
                break;
            }
        }
    }

    private void ValidateAnswer()
    {
        if (_activeChallenge == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (int idx in _placedTileIndices)
        {
            sb.Append(_scrambledLetters[idx]);
        }
        string currentAttempt = sb.ToString();

        if (currentAttempt == _activeChallenge.targetWord)
        {
            // SUCCESS
            IsSolved = true;
            if (feedbackText != null)
            {
                feedbackText.text = $"<color=#00AA22><b>CORRECT: {_activeChallenge.targetWord}!</b> Institutional Pillar Verified.</color>";
            }

            if (_audioSource != null && stampAudio != null)
            {
                _audioSource.PlayOneShot(stampAudio, 0.85f);
            }

            StartCoroutine(SuccessRoutine());
        }
        else
        {
            // FAILED ATTEMPT
            if (feedbackText != null)
            {
                feedbackText.text = "<color=#AA1111><b>INCORRECT ARRANGEMENT.</b> The counselor mutters in disappointment...</color>";
            }

            if (_audioSource != null && crazyMumbleAudio != null)
            {
                _audioSource.PlayOneShot(crazyMumbleAudio, 0.8f);
            }

            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeAnswerRow());
        }
    }

    private IEnumerator SuccessRoutine()
    {
        // Brief pause to admire the solved core value
        yield return new WaitForSeconds(1.5f);

        ClosePuzzle();

        // Notify ClearanceNPC for Guidance Counselor (signature 1)
        var npcs = FindObjectsByType<ClearanceNPC>(FindObjectsInactive.Include);
        foreach (var npc in npcs)
        {
            if (npc.signatureIndex == 1)
            {
                npc.OnGuidancePuzzleCompleted();
                break;
            }
        }
    }

    private IEnumerator ShakeAnswerRow()
    {
        if (answerSlotsContainer == null) yield break;

        Vector3 originalPos = answerSlotsContainer.localPosition;
        float elapsed = 0f;
        float duration = 0.35f;
        float magnitude = 10f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float xOffset = Random.Range(-1f, 1f) * magnitude;
            answerSlotsContainer.localPosition = new Vector3(originalPos.x + xOffset, originalPos.y, originalPos.z);
            yield return null;
        }

        answerSlotsContainer.localPosition = originalPos;
    }

    /// <summary>
    /// Updates all slot and tile UI visual states.
    /// </summary>
    public void RefreshUI()
    {
        if (_activeChallenge == null) return;
        int targetLen = _activeChallenge.targetWord.Length;

        // 1. Refresh Answer Slots
        if (answerSlotsContainer != null)
        {
            for (int i = 0; i < answerSlotsContainer.childCount; i++)
            {
                Transform slotTrans = answerSlotsContainer.GetChild(i);
                if (i < targetLen)
                {
                    slotTrans.gameObject.SetActive(true);
                    Text slotTxt = slotTrans.GetComponentInChildren<Text>();
                    if (slotTxt != null)
                    {
                        if (i < _placedTileIndices.Count)
                        {
                            slotTxt.text = _scrambledLetters[_placedTileIndices[i]].ToString();
                            slotTxt.color = new Color(0.12f, 0.10f, 0.08f, 1f);
                        }
                        else
                        {
                            slotTxt.text = "";
                            slotTxt.color = new Color(0.5f, 0.45f, 0.4f, 0.4f);
                        }
                    }

                    // Click to remove
                    Button slotBtn = slotTrans.GetComponent<Button>();
                    if (slotBtn != null)
                    {
                        int slotIdx = i;
                        slotBtn.onClick.RemoveAllListeners();
                        slotBtn.onClick.AddListener(() => OnAnswerSlotClicked(slotIdx));
                    }
                }
                else
                {
                    slotTrans.gameObject.SetActive(false);
                }
            }
        }

        // 2. Refresh Scrambled Tray Tiles
        if (letterTilesContainer != null)
        {
            for (int i = 0; i < letterTilesContainer.childCount; i++)
            {
                Transform tileTrans = letterTilesContainer.GetChild(i);
                if (i < _scrambledLetters.Count)
                {
                    tileTrans.gameObject.SetActive(true);
                    Text tileTxt = tileTrans.GetComponentInChildren<Text>();
                    if (tileTxt != null)
                    {
                        tileTxt.text = _scrambledLetters[i].ToString();
                    }

                    bool isPlaced = _placedTileIndices.Contains(i);
                    Button tileBtn = tileTrans.GetComponent<Button>();
                    CanvasGroup tileCg = tileTrans.GetComponent<CanvasGroup>();
                    if (tileCg == null) tileCg = tileTrans.gameObject.AddComponent<CanvasGroup>();

                    tileCg.alpha = isPlaced ? 0.25f : 1f;
                    tileCg.interactable = !isPlaced;

                    if (tileBtn != null)
                    {
                        int tileIdx = i;
                        tileBtn.onClick.RemoveAllListeners();
                        tileBtn.onClick.AddListener(() => OnTrayTileClicked(tileIdx));
                    }
                }
                else
                {
                    tileTrans.gameObject.SetActive(false);
                }
            }
        }
    }
}
