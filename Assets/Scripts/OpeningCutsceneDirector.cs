using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OpeningCutsceneDirector — Cinematic opening cutscene for KLIRANS.
/// Uses authentic paper textures (ScrambledPaper_HUD, Blank_paper, Blank clearance slip,
/// PaidClearedStamp) and typewriter/horror fonts matching the in-game dialogue and document UI.
/// </summary>
public class OpeningCutsceneDirector : MonoBehaviour
{
    public static OpeningCutsceneDirector Instance { get; private set; }
    public static bool IsCutsceneActive { get; private set; } = false;
    public static bool IsAnyCutscenePlaying =>
        IsCutsceneActive || (WinCutsceneDirector.Instance != null && WinCutsceneDirector.IsCutsceneActive);

    [Header("Configuration")]
    [Tooltip("Play the cutscene on game launch.")]
    public bool playOnStart = true;

    [Tooltip("Force play cutscene even if previously played this session.")]
    public bool forcePlayEveryLaunch = true;

    [Header("Paper UI Sprites")]
    public Sprite dialoguePaperSprite;
    public Sprite blankPaperSprite;
    public Sprite unsignedSlipSprite;
    public Sprite documentSheetSprite;
    public Sprite stampSprite;
    public Sprite buttonPaperSprite;

    [Header("Audio Clips")]
    public AudioClip tapeHissAmbience;
    public AudioClip tickingClockClip;
    public AudioClip groggyBreathClip;
    public AudioClip gaspClip;
    public AudioClip powerOutageClip;
    public AudioClip doorSlamClip;
    public AudioClip monsterCueClip;
    public AudioClip objectiveChimeClip;

    [Header("Debug")]
    [Tooltip("Press this key in playmode to replay the cutscene at any time for testing.")]
    public KeyCode replayCutsceneKey = KeyCode.F7;

    // UI Elements
    private GameObject _canvasGO;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private RectTransform _eyelidTop;
    private RectTransform _eyelidBottom;
    private Image _blackBackdrop;
    private RectTransform _letterboxTop;
    private RectTransform _letterboxBottom;

    private GameObject _subtitleBox;
    private Text _subtitleText;
    private Text _speakerTagText;
    private Text _contextTagText;
    private Coroutine _typewriterRoutine;

    private GameObject _timeCardBox;
    private Text _timeCardClockText;
    private Text _timeCardWarningText;

    private GameObject _clearanceSlipPreviewBox;
    private Text _clearanceTitleText;
    private Text _clearanceStampsText;

    private GameObject _skipPromptRoot;
    private Text _skipPromptText;
    private Button _skipButton;

    // Audio sources
    private AudioSource _ambienceSource;
    private AudioSource _voiceSource;
    private AudioSource _sfxSource;

    // Runtime state
    private Coroutine _cutsceneRoutine;
    private PlayerMovement _playerMovement;
    private Camera _playerCamera;
    private Vector3 _originalCamLocalPos;
    private Quaternion _originalCamLocalRot;
    private bool _hasSkipped = false;

    public event Action OnCutsceneFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Debug.Log("[OpeningCutsceneDirector] Awake initialized.");

        SetupAudioSources();
        LoadDefaultAssets();
        BuildCutsceneUI();
        SetUIVisible(false);
    }

    private void Start()
    {
        FindPlayerReferences();

        if (playOnStart)
        {
            StartCoroutine(DelayedStartRoutine());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator DelayedStartRoutine()
    {
        yield return new WaitForEndOfFrame();
        PlayCutscene();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(replayCutsceneKey) && !IsCutsceneActive)
        {
            PlayCutscene();
            return;
        }
#endif

        if (!IsCutsceneActive || _hasSkipped) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
        {
            SkipCutscene();
        }
    }

    private void FindPlayerReferences()
    {
        if (_playerMovement == null)
            _playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (_playerMovement != null && _playerMovement.playerCamera != null)
        {
            _playerCamera = _playerMovement.playerCamera;
            _originalCamLocalPos = _playerCamera.transform.localPosition;
            _originalCamLocalRot = _playerCamera.transform.localRotation;
        }
        else if (Camera.main != null)
        {
            _playerCamera = Camera.main;
            _originalCamLocalPos = _playerCamera.transform.localPosition;
            _originalCamLocalRot = _playerCamera.transform.localRotation;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void PlayCutscene()
    {
        if (IsCutsceneActive) return;

        FindPlayerReferences();

        IsCutsceneActive = true;
        _hasSkipped = false;
        Debug.Log("[OpeningCutsceneDirector] >>> STARTING OPENING NIGHTMARE CUTSCENE <<<");

        HideGameplayHUD(true);

        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetUIVisible(true);

        if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
        _cutsceneRoutine = StartCoroutine(CutsceneSequence());
    }

    public void SkipCutscene()
    {
        if (!IsCutsceneActive || _hasSkipped) return;
        _hasSkipped = true;

        if (_cutsceneRoutine != null)
        {
            StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = null;
        }
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        if (_ambienceSource != null) _ambienceSource.Stop();
        if (_voiceSource != null) _voiceSource.Stop();
        if (_sfxSource != null) _sfxSource.Stop();

        ConcludeCutscene();
        StartCoroutine(FastSkipTransition());
    }

    private IEnumerator FastSkipTransition()
    {
        float fade = 0.30f;
        Color c = _blackBackdrop != null ? _blackBackdrop.color : Color.black;
        while (fade > 0f)
        {
            fade -= Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(fade / 0.30f);
            if (_blackBackdrop != null) _blackBackdrop.color = c;
            yield return null;
        }

        SetUIVisible(false);
    }

    private void ConcludeCutscene()
    {
        IsCutsceneActive = false;

        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos;
            _playerCamera.transform.localRotation = _originalCamLocalRot;
        }

        if (_playerMovement != null)
        {
            _playerMovement.isDialogueCameraOverride = false;
            _playerMovement.SyncPitch(0f);
            _playerMovement.SetControlsEnabled(true);
        }

        HideGameplayHUD(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_sfxSource != null && objectiveChimeClip != null)
        {
            _sfxSource.PlayOneShot(objectiveChimeClip, 0.9f);
        }

        if (ObjectiveHUD.Instance != null)
        {
            bool hasFlashlight = InventoryManager.Instance != null && InventoryManager.Instance.HasItemNamed("Flashlight");
            if (!hasFlashlight)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "KUNIN ANG FLASHLIGHT SA MESA // PICK UP FLASHLIGHT",
                    "Napakadilim! Kunin ang umiilaw na flashlight sa bilog na mesa sa harap mo upang magkaroon ng liwanag! [Pindutin ang E]"
                );
            }
            else
            {
                ObjectiveHUD.Instance.SetObjective(
                    "SURVIVE THE CLEARANCE NIGHTMARE",
                    "Nakatulog ka sa classroom at sarado na ang buong campus! Hanapin ang 4 na piraso ng clearance slip at kumpletuhin ang 6 na signatories bago mag-hatinggabi!"
                );
            }
        }

        OnCutsceneFinished?.Invoke();
        Debug.Log("[OpeningCutsceneDirector] Cutscene finished — player controls fully engaged!");
    }

    // ── Cutscene Animation Choreography ──────────────────────────────────────

    private IEnumerator CutsceneSequence()
    {
        // ── BEAT 0: Initial Pitch Darkness & Slumber ────────────────────────
        _blackBackdrop.color = Color.black;
        SetEyelidsOpenAmount(0.0f);
        SetLetterboxVisible(true);
        HideSubtitles();
        _timeCardBox.SetActive(false);
        _clearanceSlipPreviewBox.SetActive(false);

        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _originalCamLocalPos + new Vector3(0f, -0.35f, 0.1f);
            _playerCamera.transform.localRotation = Quaternion.Euler(46f, 0f, -3f);
        }

        if (_ambienceSource != null && tapeHissAmbience != null)
        {
            _ambienceSource.clip = tapeHissAmbience;
            _ambienceSource.volume = 0.55f;
            _ambienceSource.Play();
        }

        if (_voiceSource != null && groggyBreathClip != null)
        {
            _voiceSource.PlayOneShot(groggyBreathClip, 0.75f);
        }

        yield return new WaitForSeconds(1.8f);

        // ── BEAT 1: First Groggy Blink ──────────────────────────────────────
        yield return AnimateEyelids(0.0f, 0.35f, 0.9f);
        yield return new WaitForSeconds(0.4f);
        yield return AnimateEyelids(0.35f, 0.0f, 0.6f);

        yield return new WaitForSeconds(0.8f);

        // ── BEAT 2: Waking Up & First Dialogue ──────────────────────────────
        yield return AnimateEyelids(0.0f, 0.70f, 1.2f);

        ShowSubtitle("ESTUDYANTE (IKAW)", "Ugh... ang sakit ng leeg ko... Nakatulog yata ako sa desk... Teka, anong oras na ba?", "BCH BUILDING // LOBBY");

        yield return AnimateCameraView(
            startPos: _playerCamera != null ? _playerCamera.transform.localPosition : Vector3.zero,
            targetPos: _originalCamLocalPos,
            startRot: _playerCamera != null ? _playerCamera.transform.localRotation : Quaternion.identity,
            targetRot: Quaternion.Euler(12f, 0f, 0f),
            duration: 2.4f
        );

        yield return new WaitForSeconds(1.2f);

        // ── BEAT 3: Clock Check & Sudden Panic ──────────────────────────────
        yield return AnimateEyelids(0.70f, 1.0f, 0.6f);

        _timeCardBox.SetActive(true);
        if (_timeCardClockText != null) _timeCardClockText.text = "19:04 — LAGPAS NA SA CURFEW";
        if (_timeCardWarningText != null) _timeCardWarningText.text = "PAUNAWA: SARADO NA ANG MAIN GATE NG CAMPUS (7:00 PM LOCKDOWN)";

        if (_voiceSource != null && gaspClip != null)
        {
            _voiceSource.PlayOneShot(gaspClip, 0.95f);
        }

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(12f, 0f, 0f),
            targetRot: Quaternion.Euler(-4f, 0f, 0f),
            duration: 0.35f
        );

        ShowSubtitle("ESTUDYANTE (IKAW)", "GAGO! 7:00 PM NA?! Bakit sarado na ang buong building?! Nakatulog ako sa klase?!", "19:04 PM // CURFEW");

        yield return new WaitForSeconds(3.2f);
        _timeCardBox.SetActive(false);

        // ── BEAT 4: The Nightmare Stakes (Clearance Slip Deadline) ──────────
        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos + new Vector3(0f, -0.05f, 0.05f),
            startRot: Quaternion.Euler(-4f, 0f, 0f),
            targetRot: Quaternion.Euler(34f, 0f, 0f),
            duration: 0.8f
        );

        _clearanceSlipPreviewBox.SetActive(true);

        ShowSubtitle("ESTUDYANTE (IKAW)", "Patay ako... Huling araw na pala ngayon ng clearance signing! Kapag hindi ko nakumpleto ang 6 na pirma bago mag-hatinggabi, hindi ako makakagraduate!", "DEADLINE: 11:59 PM");

        yield return new WaitForSeconds(4.2f);

        ShowSubtitle("ESTUDYANTE (IKAW)", "Kailangan kong mahanap ang mga piraso ng clearance slip at papirmahan sa 6 na opisina... Pero bakit parang walang tao rito?!", "6 NA PIRMA ANG KULANG");

        yield return new WaitForSeconds(3.5f);
        _clearanceSlipPreviewBox.SetActive(false);

        // ── BEAT 5: Power Surge Blackout & Horror Threat Emerges ────────────
        if (_sfxSource != null && powerOutageClip != null)
        {
            _sfxSource.PlayOneShot(powerOutageClip, 0.95f);
        }

        for (int i = 0; i < 4; i++)
        {
            _blackBackdrop.color = new Color(0f, 0f, 0f, 0.85f);
            yield return new WaitForSeconds(0.08f);
            _blackBackdrop.color = new Color(0f, 0f, 0f, 0.2f);
            yield return new WaitForSeconds(0.06f);
        }
        _blackBackdrop.color = new Color(0f, 0f, 0f, 0.5f);

        if (_sfxSource != null && doorSlamClip != null)
        {
            _sfxSource.PlayOneShot(doorSlamClip, 0.85f);
        }

        yield return new WaitForSeconds(0.8f);

        if (_sfxSource != null && monsterCueClip != null)
        {
            _sfxSource.PlayOneShot(monsterCueClip, 0.90f);
        }

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(34f, 0f, 0f),
            targetRot: Quaternion.Euler(0f, -28f, 0f),
            duration: 0.5f
        );
        yield return new WaitForSeconds(0.3f);

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(0f, -28f, 0f),
            targetRot: Quaternion.Euler(0f, 32f, 0f),
            duration: 0.6f
        );
        yield return new WaitForSeconds(0.3f);

        yield return AnimateCameraView(
            startPos: _originalCamLocalPos,
            targetPos: _originalCamLocalPos,
            startRot: Quaternion.Euler(0f, 32f, 0f),
            targetRot: Quaternion.Euler(0f, 0f, 0f),
            duration: 0.5f
        );

        ShowSubtitle("ESTUDYANTE (IKAW)", "S-sino 'yun?! Bakit nakapatay ang mga ilaw sa hallway?! Hello?! May tao pa ba d'yan?!", "BLACKOUT // PANGANIB");

        yield return new WaitForSeconds(3.5f);

        ShowSubtitle("ESTUDYANTE (IKAW)", "Hindi 'to normal... Naka-lockdown ang buong campus, at may kung anong gumagala sa dilim. Kailangan kong kunin ang flashlight sa mesa!", "TUMAKAS BAGO MAG-12:00");

        yield return new WaitForSeconds(4.0f);

        // ── BEAT 6: Smooth Transition to Gameplay ───────────────────────────
        HideSubtitles();

        float t = 0f;
        Color bgC = _blackBackdrop.color;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            bgC.a = Mathf.Lerp(0.5f, 0f, t / 0.6f);
            _blackBackdrop.color = bgC;
            yield return null;
        }

        SetLetterboxVisible(false);
        ConcludeCutscene();
        SetUIVisible(false);
    }

    // ── Procedural Camera & Eyelid Animation Helpers ─────────────────────────

    private IEnumerator AnimateEyelids(float startOpen, float targetOpen, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float currentOpen = Mathf.Lerp(startOpen, targetOpen, t);
            SetEyelidsOpenAmount(currentOpen);
            yield return null;
        }
        SetEyelidsOpenAmount(targetOpen);
    }

    private void SetEyelidsOpenAmount(float openFactor)
    {
        float closedHeight = Screen.height * 0.55f;
        float currentHeight = Mathf.Lerp(closedHeight, 0f, Mathf.Clamp01(openFactor));

        if (_eyelidTop != null)
            _eyelidTop.sizeDelta = new Vector2(0f, currentHeight);
        if (_eyelidBottom != null)
            _eyelidBottom.sizeDelta = new Vector2(0f, currentHeight);
    }

    private IEnumerator AnimateCameraView(Vector3 startPos, Vector3 targetPos, Quaternion startRot, Quaternion targetRot, float duration)
    {
        if (_playerCamera == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _playerCamera.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            _playerCamera.transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _playerCamera.transform.localPosition = targetPos;
        _playerCamera.transform.localRotation = targetRot;
    }

    private void ShowSubtitle(string speaker, string dialogue, string contextTag = "KLIRANS // TALA")
    {
        if (_subtitleBox == null) return;
        _subtitleBox.SetActive(true);
        if (_speakerTagText != null) _speakerTagText.text = speaker;
        if (_contextTagText != null) _contextTagText.text = contextTag;

        if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
        _typewriterRoutine = StartCoroutine(TypewriterReveal(dialogue));
    }

    private IEnumerator TypewriterReveal(string fullText)
    {
        if (_subtitleText == null) yield break;
        _subtitleText.text = "";
        for (int i = 0; i < fullText.Length; i++)
        {
            _subtitleText.text = fullText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(0.016f);
        }
        _subtitleText.text = fullText;
    }

    private void HideSubtitles()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        if (_subtitleBox != null) _subtitleBox.SetActive(false);
    }

    private void SetLetterboxVisible(bool visible)
    {
        if (_letterboxTop != null) _letterboxTop.gameObject.SetActive(visible);
        if (_letterboxBottom != null) _letterboxBottom.gameObject.SetActive(visible);
    }

    private void SetUIVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    private void HideGameplayHUD(bool hide)
    {
        if (ObjectiveHUD.Instance != null && ObjectiveHUD.Instance.canvasGroup != null)
        {
            ObjectiveHUD.Instance.canvasGroup.alpha = hide ? 0f : 1f;
        }

        var slotCanvas = GameObject.Find("SlotCanvas");
        if (slotCanvas != null)
        {
            var cg = slotCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = slotCanvas.AddComponent<CanvasGroup>();
            cg.alpha = hide ? 0f : 1f;
        }

        var hudCanvas = GameObject.Find("HudCanvas");
        if (hudCanvas != null)
        {
            var cg = hudCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = hudCanvas.AddComponent<CanvasGroup>();
            cg.alpha = hide ? 0f : 1f;
        }
    }

    // ── Dynamic Paper UI Builder ─────────────────────────────────────────────

    private void BuildCutsceneUI()
    {
        _canvasGO = new GameObject("OpeningCutscene_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvasGO.transform.SetParent(transform, false);

        _canvas = _canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 990;

        var scaler = _canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _canvasGO.GetComponent<CanvasGroup>();

        Font horrorFont = LoadFont("Main Menu/watch people die", "Assets/Main Menu/watch people die.ttf");
        Font typewriterFont = LoadFont("Fonts/Typewriter_Bold", "Assets/Fonts/Typewriter_Bold.ttf");

        // 1. Black Backdrop
        var bgGO = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        _blackBackdrop = bgGO.GetComponent<Image>();
        _blackBackdrop.color = Color.black;
        _blackBackdrop.raycastTarget = false;

        // 2. Eyelids (Top & Bottom)
        _eyelidTop = CreateEyelidBar("Eyelid_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _eyelidBottom = CreateEyelidBar("Eyelid_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

        // 3. Cinematic Letterbox Bars
        _letterboxTop = CreateLetterbox("Letterbox_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 72f);
        _letterboxBottom = CreateLetterbox("Letterbox_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 72f);

        // 4. Paper Curfew Notice Slip (Time Card)
        BuildTimeCard(horrorFont, typewriterFont);

        // 5. Paper Clearance Slip Document Preview
        BuildClearanceCard(horrorFont, typewriterFont);

        // 6. Taped Paper Dialogue Box (matching NPCDialogueSystem)
        BuildSubtitleBox(horrorFont, typewriterFont);

        // 7. Pinned Paper Skip Note (Bottom Right)
        BuildSkipPrompt(typewriterFont);
    }

    private RectTransform CreateEyelidBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(0f, 600f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.01f, 0.01f, 0.02f, 1f);
        img.raycastTarget = false;
        return rt;
    }

    private RectTransform CreateLetterbox(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(0f, height);
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    private void BuildTimeCard(Font horrorFont, Font typewriterFont)
    {
        _timeCardBox = new GameObject("TimeCardPaper", typeof(RectTransform), typeof(Image));
        _timeCardBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _timeCardBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.77f);
        rt.anchorMax = new Vector2(0.5f, 0.77f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(650f, 155f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 1.1f);

        var bg = _timeCardBox.GetComponent<Image>();
        bg.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : blankPaperSprite;
        bg.color = new Color(0.95f, 0.90f, 0.82f, 0.98f);

        var shadow = _timeCardBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(5f, -6f);

        // Optional rubber stamp watermark on the right
        if (stampSprite != null)
        {
            var stampGO = new GameObject("StampWatermark", typeof(RectTransform), typeof(Image));
            stampGO.transform.SetParent(_timeCardBox.transform, false);
            var srt = stampGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.76f, 0.18f);
            srt.anchorMax = new Vector2(0.94f, 0.82f);
            srt.sizeDelta = Vector2.zero;
            srt.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var sImg = stampGO.GetComponent<Image>();
            sImg.sprite = stampSprite;
            sImg.preserveAspect = true;
            sImg.color = new Color(0.75f, 0.12f, 0.12f, 0.35f);
            sImg.raycastTarget = false;
        }

        var clockGO = new GameObject("ClockText", typeof(RectTransform), typeof(Text));
        clockGO.transform.SetParent(_timeCardBox.transform, false);
        var crt = clockGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.08f, 0.46f);
        crt.anchorMax = new Vector2(0.92f, 0.86f);
        crt.sizeDelta = Vector2.zero;
        _timeCardClockText = clockGO.GetComponent<Text>();
        _timeCardClockText.font = horrorFont;
        _timeCardClockText.fontSize = 27;
        _timeCardClockText.fontStyle = FontStyle.Bold;
        _timeCardClockText.alignment = TextAnchor.MiddleCenter;
        _timeCardClockText.color = new Color(0.68f, 0.05f, 0.05f, 1f);

        var warnGO = new GameObject("WarningText", typeof(RectTransform), typeof(Text));
        warnGO.transform.SetParent(_timeCardBox.transform, false);
        var wrt = warnGO.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0.08f, 0.14f);
        wrt.anchorMax = new Vector2(0.92f, 0.46f);
        wrt.sizeDelta = Vector2.zero;
        _timeCardWarningText = warnGO.GetComponent<Text>();
        _timeCardWarningText.font = typewriterFont;
        _timeCardWarningText.fontSize = 14;
        _timeCardWarningText.fontStyle = FontStyle.Bold;
        _timeCardWarningText.alignment = TextAnchor.MiddleCenter;
        _timeCardWarningText.color = new Color(0.10f, 0.07f, 0.06f, 0.96f);
    }

    private void BuildClearanceCard(Font horrorFont, Font typewriterFont)
    {
        _clearanceSlipPreviewBox = new GameObject("ClearancePreviewPaper", typeof(RectTransform), typeof(Image));
        _clearanceSlipPreviewBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _clearanceSlipPreviewBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.56f);
        rt.anchorMax = new Vector2(0.5f, 0.56f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 380f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -0.9f);

        var bg = _clearanceSlipPreviewBox.GetComponent<Image>();
        bg.sprite = documentSheetSprite != null ? documentSheetSprite : (blankPaperSprite != null ? blankPaperSprite : dialoguePaperSprite);
        bg.color = new Color(0.96f, 0.92f, 0.85f, 0.99f);

        var shadow = _clearanceSlipPreviewBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        shadow.effectDistance = new Vector2(6f, -7f);

        // Left: Actual Unsigned Clearance Slip Graphic (Blank.png)
        if (unsignedSlipSprite != null)
        {
            var slipImgGO = new GameObject("UnsignedSlipGraphic", typeof(RectTransform), typeof(Image));
            slipImgGO.transform.SetParent(_clearanceSlipPreviewBox.transform, false);
            var sirt = slipImgGO.GetComponent<RectTransform>();
            sirt.anchorMin = new Vector2(0.11f, 0.15f);
            sirt.anchorMax = new Vector2(0.43f, 0.83f);
            sirt.sizeDelta = Vector2.zero;
            sirt.localRotation = Quaternion.Euler(0f, 0f, 1.8f);
            var sImg = slipImgGO.GetComponent<Image>();
            sImg.sprite = unsignedSlipSprite;
            sImg.preserveAspect = true;
            sImg.color = Color.white;
        }

        // Right Header Title
        var titleGO = new GameObject("ClearanceTitle", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(_clearanceSlipPreviewBox.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.45f, 0.70f);
        trt.anchorMax = new Vector2(0.89f, 0.84f);
        trt.sizeDelta = Vector2.zero;
        _clearanceTitleText = titleGO.GetComponent<Text>();
        _clearanceTitleText.font = horrorFont;
        _clearanceTitleText.fontSize = 20;
        _clearanceTitleText.fontStyle = FontStyle.Bold;
        _clearanceTitleText.alignment = TextAnchor.MiddleLeft;
        _clearanceTitleText.color = new Color(0.68f, 0.05f, 0.05f, 1f);
        _clearanceTitleText.text = "CLEARANCE SLIP // HULING ARAW";

        // Right Checklist of 6 Missing Departments in dark typewriter ink
        var stampGO = new GameObject("ClearanceStamps", typeof(RectTransform), typeof(Text));
        stampGO.transform.SetParent(_clearanceSlipPreviewBox.transform, false);
        var srt = stampGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.45f, 0.15f);
        srt.anchorMax = new Vector2(0.89f, 0.68f);
        srt.sizeDelta = Vector2.zero;
        _clearanceStampsText = stampGO.GetComponent<Text>();
        _clearanceStampsText.font = typewriterFont;
        _clearanceStampsText.fontSize = 14;
        _clearanceStampsText.lineSpacing = 1.26f;
        _clearanceStampsText.alignment = TextAnchor.UpperLeft;
        _clearanceStampsText.color = new Color(0.08f, 0.06f, 0.05f, 0.98f);
        _clearanceStampsText.text =
            "[ ] 1. LIBRARY & ARCHIVES (3F)\n" +
            "[ ] 2. GUIDANCE OFFICE (2F)\n" +
            "[ ] 3. COMPUTER LAB (1F)\n" +
            "[ ] 4. UNIVERSITY CLINIC (1F)\n" +
            "[ ] 5. CASHIER WINDOW (1F)\n" +
            "[ ] 6. REGISTRAR / EVP (1F)\n" +
            "STATUS: 0 / 6 PIRMA (DEADLINE: 11:59 PM)";
    }

    private void BuildSubtitleBox(Font horrorFont, Font typewriterFont)
    {
        _subtitleBox = new GameObject("SubtitlePaperBox", typeof(RectTransform), typeof(Image));
        _subtitleBox.transform.SetParent(_canvasGO.transform, false);
        var rt = _subtitleBox.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.13f);
        rt.anchorMax = new Vector2(0.5f, 0.13f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 180f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -0.5f);

        var bg = _subtitleBox.GetComponent<Image>();
        bg.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : blankPaperSprite;
        bg.color = new Color(0.96f, 0.92f, 0.84f, 0.98f);

        var shadow = _subtitleBox.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(4f, -5f);

        // Speaker Header (Left, crimson horror font matching NPCDialogueSystem)
        var spkGO = new GameObject("SpeakerTag", typeof(RectTransform), typeof(Text));
        spkGO.transform.SetParent(_subtitleBox.transform, false);
        var srt = spkGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.09f, 0.65f);
        srt.anchorMax = new Vector2(0.62f, 0.87f);
        srt.sizeDelta = Vector2.zero;
        _speakerTagText = spkGO.GetComponent<Text>();
        _speakerTagText.font = horrorFont;
        _speakerTagText.fontSize = 21;
        _speakerTagText.fontStyle = FontStyle.Bold;
        _speakerTagText.alignment = TextAnchor.MiddleLeft;
        _speakerTagText.color = new Color(0.68f, 0.05f, 0.05f, 1f);

        // Context / Location Tag (Right, faded typewriter ink)
        var ctxGO = new GameObject("ContextTag", typeof(RectTransform), typeof(Text));
        ctxGO.transform.SetParent(_subtitleBox.transform, false);
        var crt = ctxGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.58f, 0.66f);
        crt.anchorMax = new Vector2(0.91f, 0.86f);
        crt.sizeDelta = Vector2.zero;
        _contextTagText = ctxGO.GetComponent<Text>();
        _contextTagText.font = typewriterFont;
        _contextTagText.fontSize = 13;
        _contextTagText.fontStyle = FontStyle.Bold;
        _contextTagText.alignment = TextAnchor.MiddleRight;
        _contextTagText.color = new Color(0.32f, 0.23f, 0.17f, 0.90f);

        // Subtle Ink Divider Rule
        var divGO = new GameObject("InkDivider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(_subtitleBox.transform, false);
        var drt = divGO.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.09f, 0.62f);
        drt.anchorMax = new Vector2(0.91f, 0.63f);
        drt.sizeDelta = Vector2.zero;
        divGO.GetComponent<Image>().color = new Color(0.25f, 0.16f, 0.12f, 0.35f);

        // Body Dialogue Text (Dark charcoal typewriter ink on paper)
        var subGO = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
        subGO.transform.SetParent(_subtitleBox.transform, false);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.09f, 0.14f);
        subRT.anchorMax = new Vector2(0.91f, 0.59f);
        subRT.sizeDelta = Vector2.zero;
        _subtitleText = subGO.GetComponent<Text>();
        _subtitleText.font = typewriterFont;
        _subtitleText.fontSize = 18;
        _subtitleText.lineSpacing = 1.18f;
        _subtitleText.alignment = TextAnchor.UpperLeft;
        _subtitleText.color = new Color(0.07f, 0.05f, 0.04f, 1f);
        _subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildSkipPrompt(Font typewriterFont)
    {
        _skipPromptRoot = new GameObject("SkipPromptPaper", typeof(RectTransform), typeof(Image), typeof(Button));
        _skipPromptRoot.transform.SetParent(_canvasGO.transform, false);
        var rt = _skipPromptRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-36f, 24f);
        rt.sizeDelta = new Vector2(285f, 54f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -1.5f);

        var img = _skipPromptRoot.GetComponent<Image>();
        img.sprite = dialoguePaperSprite != null ? dialoguePaperSprite : buttonPaperSprite;
        img.color = new Color(0.95f, 0.90f, 0.82f, 0.96f);

        var shadow = _skipPromptRoot.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
        shadow.effectDistance = new Vector2(3f, -4f);

        _skipButton = _skipPromptRoot.GetComponent<Button>();
        _skipButton.onClick.AddListener(SkipCutscene);

        var txtGO = new GameObject("SkipText", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(_skipPromptRoot.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.08f, 0.12f);
        trt.anchorMax = new Vector2(0.92f, 0.88f);
        trt.sizeDelta = Vector2.zero;

        _skipPromptText = txtGO.GetComponent<Text>();
        _skipPromptText.font = typewriterFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _skipPromptText.fontSize = 14;
        _skipPromptText.fontStyle = FontStyle.Bold;
        _skipPromptText.alignment = TextAnchor.MiddleCenter;
        _skipPromptText.color = new Color(0.12f, 0.06f, 0.05f, 0.98f);
        _skipPromptText.text = "[SPACE / ESC] LAKTAWAN";
    }

    // ── Audio & Asset Setup ──────────────────────────────────────────────────

    private void SetupAudioSources()
    {
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.loop = true;
        _ambienceSource.playOnAwake = false;

        _voiceSource = gameObject.AddComponent<AudioSource>();
        _voiceSource.spatialBlend = 0f;
        _voiceSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.spatialBlend = 0f;
        _sfxSource.playOnAwake = false;
    }

    private void LoadDefaultAssets()
    {
#if UNITY_EDITOR
        if (dialoguePaperSprite == null)
            dialoguePaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");
        if (blankPaperSprite == null)
            blankPaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Blank_paper.png");
        if (unsignedSlipSprite == null)
            unsignedSlipSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Blank.png");
        if (documentSheetSprite == null)
            documentSheetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Blank_paper.png");
        if (stampSprite == null)
            stampSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/PaidClearedStamp.png");
        if (buttonPaperSprite == null)
            buttonPaperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/KeypadButton_Normal.png");

        if (tapeHissAmbience == null)
            tapeHissAmbience = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs tape hiss.mp3");
        if (groggyBreathClip == null)
            groggyBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");
        if (gaspClip == null)
            gaspClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");
        if (powerOutageClip == null)
            powerOutageClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/FNAF_ Power Outage - Gaming Sound Effect (HD).mp3");
        if (doorSlamClip == null)
            doorSlamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/universfield-creepy-creaking-door-402155.mp3");
        if (monsterCueClip == null)
            monsterCueClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
        if (objectiveChimeClip == null)
            objectiveChimeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
    }

    private Font LoadFont(string resourcePath, string editorAssetPath)
    {
        Font font = Resources.Load<Font>(resourcePath);
#if UNITY_EDITOR
        if (font == null) font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);
#endif
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }
}
