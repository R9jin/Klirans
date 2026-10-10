using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ProctorJumpscareQTE — Interactive struggle mechanic when caught by The Proctor.
/// 
/// Mechanics:
/// - Proctor stays in the player's face, violently shuddering, wiggling, and lunging.
/// - Anxiety continuously climbs over time (e.g. 14 anxiety/sec) toward 100% (Game Over).
/// - An in-your-face retro horror QTE overlay appears on the HUD:
///     * Action Banner: "BREAK FREE!"
///     * Pulsing Key Prompt: "[ Q ]"
///     * Escape Progress Bar: Fills from 0% to 100% as the player spams the key.
///     * Warning Subtitle: "SPAM [Q] TO ESCAPE! — ANXIETY RISING!"
/// - Required presses are randomized per encounter from a preset range (e.g. 12 to 18 presses).
/// - Every press gives tactile feedback: Proctor is shoved back slightly, screen jolts, key badge punches.
/// - If player breaks free: Proctor is violently recoiled and the safe-zone whiteout transition begins.
/// - If anxiety hits 100%: The player collapses from terror/cardiac arrest and Game Over triggers!
/// </summary>
public class ProctorJumpscareQTE : MonoBehaviour
{
    public static ProctorJumpscareQTE Instance { get; private set; }

    [Header("QTE Key Settings")]
    [Tooltip("Primary key the player must spam to break free.")]
    public KeyCode qteKey = KeyCode.Q;

    [Tooltip("Optional alternate key (e.g. Space) allowed for accessibility/playstyle.")]
    public KeyCode alternateKey = KeyCode.Space;

    [Header("Press Count Preset (Randomized per encounter)")]
    [Tooltip("Minimum required presses in the random range.")]
    public int minRequiredPresses = 12;

    [Tooltip("Maximum required presses in the random range.")]
    public int maxRequiredPresses = 18;

    [Header("Anxiety Buildup Rate")]
    [Tooltip("Continuous anxiety added per second while struggling in The Proctor's grasp.")]
    public float anxietyIncreaseRate = 14.0f;

    [Header("Struggle Balancing")]
    [Tooltip("Progress decay rate in presses per second when the player stops pressing (forces frantic spamming).")]
    public float progressDecayRate = 1.0f;

    [Tooltip("Grace period in seconds after a key press before decay begins.")]
    public float decayGracePeriod = 0.28f;

    [Header("Audio Feedback")]
    [Tooltip("Rapid frantic heartbeat loop played during struggle.")]
    public AudioClip heartbeatAudio;

    [Tooltip("Sound played on each struggling QTE button press.")]
    public AudioClip strugglePressAudio;

    [Tooltip("Sound played when successfully breaking free from The Proctor.")]
    public AudioClip breakFreeAudio;

    // ── UI References ────────────────────────────────────────────────────────
    private GameObject _rootUI;
    private CanvasGroup _canvasGroup;
    private RectTransform _containerRect;
    private Text _bannerText;
    private RectTransform _keyBadgeRect;
    private Text _keyText;
    private Image _keyBadgeBg;
    private RectTransform _barRect;
    private Image _barFill;
    private Text _barText;
    private Text _warningText;

    private AudioSource _heartbeatSource;
    private AudioSource _sfxSource;

    private bool _isQTEActive = false;
    public bool IsQTEActive => _isQTEActive;

    private bool _simulatedPressTriggered = false;
    private float _extraPressesToAdd = 0f;

    /// <summary>
    /// Can be called externally (e.g. mobile touch button, accessibility, or test suite)
    /// to register an escape struggle button press.
    /// </summary>
    public void RegisterStrugglePress()
    {
        _simulatedPressTriggered = true;
    }

    /// <summary>
    /// Programmatically simulates multiple rapid spam presses (e.g. for accessibility macro or tests).
    /// </summary>
    public void SimulateSpamPresses(int count)
    {
        _extraPressesToAdd += count;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupAudioSources();
        LoadDefaultAudio();
    }

    private void Start()
    {
        EnsureUIConstructed();
        HideUIInstant();
    }

    private void SetupAudioSources()
    {
        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.playOnAwake = false;
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.loop = true;
        _heartbeatSource.volume = 0.85f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 1.0f;
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (heartbeatAudio == null)
            heartbeatAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");

        if (strugglePressAudio == null)
            strugglePressAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");

        if (breakFreeAudio == null)
            breakFreeAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
#endif
        if (heartbeatAudio == null)
            heartbeatAudio = Resources.Load<AudioClip>("Sounds/fast heart beat");
        if (strugglePressAudio == null)
            strugglePressAudio = Resources.Load<AudioClip>("Sounds/man gasping for air");
        if (breakFreeAudio == null)
            breakFreeAudio = Resources.Load<AudioClip>("Sounds/encountering a proctor");
    }

    /// <summary>
    /// Programmatically constructs the retro horror QTE overlay on HudCanvas.
    /// </summary>
    public void EnsureUIConstructed()
    {
        if (_rootUI != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null)
        {
            Debug.LogWarning("[ProctorJumpscareQTE] HudCanvas not found in scene. QTE UI will not be visible.");
            return;
        }

        // Font
        Font horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
        if (loadedFont != null) horrorFont = loadedFont;
#endif

        // Root container - Positioned sleekly at bottom of screen (leaves face completely visible)
        _rootUI = new GameObject("ProctorQTEContainer", typeof(RectTransform), typeof(CanvasGroup));
        _rootUI.transform.SetParent(hud.transform, false);
        _containerRect = _rootUI.GetComponent<RectTransform>();
        _containerRect.anchorMin = new Vector2(0.5f, 0.015f);
        _containerRect.anchorMax = new Vector2(0.5f, 0.015f);
        _containerRect.pivot = new Vector2(0.5f, 0f);
        _containerRect.anchoredPosition = Vector2.zero;
        _containerRect.sizeDelta = new Vector2(360f, 92f);

        _canvasGroup = _rootUI.GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // 1. Action Banner Text ("BREAK FREE!")
        var bannerGO = new GameObject("QTEBanner", typeof(RectTransform), typeof(Text), typeof(Outline));
        bannerGO.transform.SetParent(_rootUI.transform, false);
        var bannerRT = bannerGO.GetComponent<RectTransform>();
        bannerRT.anchorMin = new Vector2(0.5f, 1f);
        bannerRT.anchorMax = new Vector2(0.5f, 1f);
        bannerRT.pivot = new Vector2(0.5f, 1f);
        bannerRT.anchoredPosition = new Vector2(0f, 0f);
        bannerRT.sizeDelta = new Vector2(360f, 26f);

        _bannerText = bannerGO.GetComponent<Text>();
        _bannerText.text = "BREAK FREE!";
        _bannerText.font = horrorFont;
        _bannerText.fontSize = 24;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.color = new Color(0.95f, 0.18f, 0.18f, 1f); // Deep survival horror red

        var bannerOutline = bannerGO.GetComponent<Outline>();
        bannerOutline.effectColor = Color.black;
        bannerOutline.effectDistance = new Vector2(2f, -2f);

        // 2. Key Badge Container ("[ Q ]")
        var keyBadgeGO = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
        keyBadgeGO.transform.SetParent(_rootUI.transform, false);
        _keyBadgeRect = keyBadgeGO.GetComponent<RectTransform>();
        _keyBadgeRect.anchorMin = new Vector2(0.5f, 0f);
        _keyBadgeRect.anchorMax = new Vector2(0.5f, 0f);
        _keyBadgeRect.pivot = new Vector2(0.5f, 0f);
        _keyBadgeRect.anchoredPosition = new Vector2(0f, 44f);
        _keyBadgeRect.sizeDelta = new Vector2(46f, 40f);

        _keyBadgeBg = keyBadgeGO.GetComponent<Image>();
        _keyBadgeBg.color = new Color(0.10f, 0.10f, 0.14f, 0.95f); // Dark charcoal badge

        var keyBadgeOutline = keyBadgeGO.GetComponent<Outline>();
        keyBadgeOutline.effectColor = new Color(0.85f, 0.20f, 0.20f, 0.95f);
        keyBadgeOutline.effectDistance = new Vector2(2f, 2f);

        // Key Label Text
        var keyTextGO = new GameObject("KeyText", typeof(RectTransform), typeof(Text), typeof(Outline));
        keyTextGO.transform.SetParent(keyBadgeGO.transform, false);
        var keyTextRT = keyTextGO.GetComponent<RectTransform>();
        keyTextRT.anchorMin = Vector2.zero;
        keyTextRT.anchorMax = Vector2.one;
        keyTextRT.offsetMin = Vector2.zero;
        keyTextRT.offsetMax = Vector2.zero;

        _keyText = keyTextGO.GetComponent<Text>();
        _keyText.text = qteKey.ToString();
        _keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _keyText.fontStyle = FontStyle.Bold;
        _keyText.fontSize = 24;
        _keyText.alignment = TextAnchor.MiddleCenter;
        _keyText.color = new Color(1f, 0.95f, 0.95f, 1f);

        var keyTextOutline = keyTextGO.GetComponent<Outline>();
        keyTextOutline.effectColor = Color.black;
        keyTextOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Escape Progress Bar Background
        var barBgGO = new GameObject("EscapeProgressBar", typeof(RectTransform), typeof(Image), typeof(Outline));
        barBgGO.transform.SetParent(_rootUI.transform, false);
        _barRect = barBgGO.GetComponent<RectTransform>();
        _barRect.anchorMin = new Vector2(0.5f, 0f);
        _barRect.anchorMax = new Vector2(0.5f, 0f);
        _barRect.pivot = new Vector2(0.5f, 0f);
        _barRect.anchoredPosition = new Vector2(0f, 22f);
        _barRect.sizeDelta = new Vector2(320f, 18f);

        var barBgImg = barBgGO.GetComponent<Image>();
        barBgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        var barOutline = barBgGO.GetComponent<Outline>();
        barOutline.effectColor = new Color(0.6f, 0.12f, 0.12f, 0.85f);
        barOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Progress Bar Fill Image
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barBgGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2f, 2f);
        fillRT.offsetMax = new Vector2(-2f, -2f);

        _barFill = fillGO.GetComponent<Image>();
        _barFill.type = Image.Type.Filled;
        _barFill.fillMethod = Image.FillMethod.Horizontal;
        _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _barFill.fillAmount = 0f;
        _barFill.color = new Color(0.88f, 0.18f, 0.18f, 1f); // Vibrant horror red

        // Progress Text Overlay
        var barTextGO = new GameObject("BarText", typeof(RectTransform), typeof(Text), typeof(Outline));
        barTextGO.transform.SetParent(barBgGO.transform, false);
        var barTextRT = barTextGO.GetComponent<RectTransform>();
        barTextRT.anchorMin = Vector2.zero;
        barTextRT.anchorMax = Vector2.one;
        barTextRT.offsetMin = Vector2.zero;
        barTextRT.offsetMax = Vector2.zero;

        _barText = barTextGO.GetComponent<Text>();
        _barText.text = "0%";
        _barText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _barText.fontStyle = FontStyle.Bold;
        _barText.fontSize = 12;
        _barText.alignment = TextAnchor.MiddleCenter;
        _barText.color = new Color(1f, 0.95f, 0.95f, 0.95f);

        var barTextOutline = barTextGO.GetComponent<Outline>();
        barTextOutline.effectColor = Color.black;
        barTextOutline.effectDistance = new Vector2(1f, -1f);

        // 4. Warning Subtitle Text ("SPAM [Q] TO ESCAPE! — ANXIETY RISING!")
        var warnGO = new GameObject("WarningText", typeof(RectTransform), typeof(Text), typeof(Outline));
        warnGO.transform.SetParent(_rootUI.transform, false);
        var warnRT = warnGO.GetComponent<RectTransform>();
        warnRT.anchorMin = new Vector2(0.5f, 0f);
        warnRT.anchorMax = new Vector2(0.5f, 0f);
        warnRT.pivot = new Vector2(0.5f, 0f);
        warnRT.anchoredPosition = new Vector2(0f, 4f);
        warnRT.sizeDelta = new Vector2(360f, 15f);

        _warningText = warnGO.GetComponent<Text>();
        _warningText.text = $"SPAM [{qteKey}] TO ESCAPE! — ANXIETY RISING!";
        _warningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _warningText.fontSize = 11;
        _warningText.alignment = TextAnchor.MiddleCenter;
        _warningText.color = new Color(0.90f, 0.75f, 0.75f, 0.95f);

        var warnOutline = warnGO.GetComponent<Outline>();
        warnOutline.effectColor = Color.black;
        warnOutline.effectDistance = new Vector2(1f, -1f);

        Debug.Log("[ProctorJumpscareQTE] QTE UI successfully constructed on HudCanvas.");
    }

    private void HideUIInstant()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
        if (_rootUI != null)
        {
            _rootUI.SetActive(false);
        }
    }

    /// <summary>
    /// Runs the interactive struggle QTE sequence while The Proctor violently wiggles
    /// and the player's Anxiety Meter continuously rises.
    /// </summary>
    public IEnumerator RunStruggleQTE(
        TheProctorAI proctor,
        Camera playerCam,
        Transform headBone,
        Vector3 targetProctorPos,
        Vector3 faceDir,
        float headOffsetFromRootY,
        Vector3 camBaseLocalPos,
        Action<bool> onComplete)
    {
        EnsureUIConstructed();

        _isQTEActive = true;
        int requiredPresses = UnityEngine.Random.Range(minRequiredPresses, maxRequiredPresses + 1);
        float currentPresses = 0f;
        float lastPressTime = Time.time;
        float struggleTimer = 0f;
        Vector3 pushbackOffset = Vector3.zero;
        Vector3 cameraJolt = Vector3.zero;

        Debug.Log($"[ProctorJumpscareQTE] Starting Struggle QTE! Required Presses: {requiredPresses}. Anxiety Rate: {anxietyIncreaseRate}/sec.");

        // Update key prompt text
        if (_keyText != null) _keyText.text = qteKey.ToString();
        if (_warningText != null) _warningText.text = $"SPAM [{qteKey}] TO ESCAPE! — ANXIETY RISING!";

        // Show UI with quick fade-in
        if (_rootUI != null) _rootUI.SetActive(true);
        float fadeIn = 0f;
        while (fadeIn < 0.12f)
        {
            fadeIn += Time.deltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(fadeIn / 0.12f);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        // Start frantic heartbeat audio
        if (_heartbeatSource != null && heartbeatAudio != null)
        {
            _heartbeatSource.clip = heartbeatAudio;
            _heartbeatSource.pitch = 1.0f;
            _heartbeatSource.Play();
        }

        bool playerEscaped = false;

        while (currentPresses < requiredPresses)
        {
            // If anxiety hits 100%, AnxietyManager fires Game Over
            if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)
            {
                Debug.LogWarning("[ProctorJumpscareQTE] Anxiety reached 100%! Struggle failed due to terror overload.");
                playerEscaped = false;
                break;
            }

            float dt = Time.deltaTime;
            struggleTimer += dt;

            // 1. Continuous Anxiety Buildup
            if (AnxietyManager.Instance != null)
            {
                AnxietyManager.Instance.AddAnxiety(anxietyIncreaseRate * dt);
            }

            // 2. Read Player Spam Inputs
            bool keyPressed = Input.GetKeyDown(qteKey) || Input.GetKeyDown(alternateKey) || _simulatedPressTriggered || _extraPressesToAdd > 0f;
            float pressAmount = 1f;
            if (_extraPressesToAdd > 0f)
            {
                pressAmount = _extraPressesToAdd;
                _extraPressesToAdd = 0f;
            }
            _simulatedPressTriggered = false;

            if (keyPressed)
            {
                currentPresses += pressAmount;
                lastPressTime = Time.time;

                // Play tactile struggle press sound
                if (_sfxSource != null && strugglePressAudio != null)
                {
                    _sfxSource.pitch = UnityEngine.Random.Range(0.92f, 1.12f);
                    _sfxSource.PlayOneShot(strugglePressAudio, 0.75f);
                }

                // Punch UI key badge
                StartCoroutine(PunchKeyBadgeRoutine());

                // Tangible physical pushback: player pushes Proctor back slightly, clamped so he never retreats behind UI
                pushbackOffset += -faceDir * (0.025f * Mathf.Min(pressAmount, 2f));
                if (pushbackOffset.magnitude > 0.14f)
                {
                    pushbackOffset = pushbackOffset.normalized * 0.14f;
                }

                // Frantic camera impulse & visual FOV kick
                cameraJolt = UnityEngine.Random.insideUnitSphere * 0.025f;
                if (playerCam != null) playerCam.fieldOfView = 42.0f;
            }

            // 3. Subtle Progress Decay (forces active, continuous spamming)
            if (progressDecayRate > 0f && Time.time - lastPressTime > decayGracePeriod)
            {
                currentPresses = Mathf.Max(0f, currentPresses - progressDecayRate * dt);
            }

            // 4. Update HUD Progress Display
            float progress = Mathf.Clamp01(currentPresses / (float)requiredPresses);
            UpdateDisplay(progress, struggleTimer);

            // 5. Recover Pushback Offset Smoothly
            pushbackOffset = Vector3.Lerp(pushbackOffset, Vector3.zero, dt * 6.5f);

            // 6. Proctor Violent Jumpscare Wiggle & Twitch (FNAF Help Wanted VR in-your-face shudder)
            float tremorFreq = 28.0f;
            float tremorAmp = 0.026f;
            Vector3 tremor = new Vector3(
                Mathf.Sin(struggleTimer * tremorFreq) * tremorAmp + UnityEngine.Random.Range(-0.008f, 0.008f),
                Mathf.Cos(struggleTimer * tremorFreq * 1.35f) * (tremorAmp * 0.75f) + UnityEngine.Random.Range(-0.006f, 0.006f),
                Mathf.Sin(struggleTimer * tremorFreq * 0.65f) * 0.010f
            );

            if (proctor != null)
            {
                proctor.transform.position = targetProctorPos + pushbackOffset + tremor;

                // High-frequency horror spasms directly facing the player with no sideways turning
                float yawShake = Mathf.Sin(struggleTimer * 28f) * 1.8f + UnityEngine.Random.Range(-0.8f, 0.8f);
                float pitchShake = Mathf.Cos(struggleTimer * 24f) * 1.5f + UnityEngine.Random.Range(-0.8f, 0.8f);
                float rollShake = Mathf.Sin(struggleTimer * 20f) * 1.5f;
                proctor.transform.rotation = Quaternion.LookRotation(faceDir) * Quaternion.Euler(pitchShake, yawShake, rollShake);
            }

            // 7. Lock Camera directly on Proctor's face & maintain FNAF VR FOV Zoom (40 deg)
            if (playerCam != null)
            {
                playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, 40.0f, dt * 10f);

                Vector3 currentHeadPos = (headBone != null) ? (headBone.position - Vector3.up * 0.10f) : (proctor.transform.position + Vector3.up * headOffsetFromRootY);
                Vector3 lookDir = currentHeadPos - playerCam.transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    playerCam.transform.rotation = Quaternion.LookRotation(lookDir);
                }

                // Frantic camera shudder shake
                float camShakeIntensity = 0.026f;
                Vector3 camShake = new Vector3(
                    UnityEngine.Random.Range(-camShakeIntensity, camShakeIntensity),
                    UnityEngine.Random.Range(-camShakeIntensity, camShakeIntensity),
                    0f
                ) + cameraJolt;
                cameraJolt = Vector3.Lerp(cameraJolt, Vector3.zero, dt * 10f);
                playerCam.transform.localPosition = camBaseLocalPos + camShake;
            }

            // 8. Ramp Heartbeat pitch as Anxiety increases
            if (_heartbeatSource != null && AnxietyManager.Instance != null)
            {
                float anxietyPct = Mathf.Clamp01(AnxietyManager.Instance.CurrentAnxiety / AnxietyManager.Instance.maxAnxiety);
                _heartbeatSource.pitch = Mathf.Lerp(1.0f, 1.40f, anxietyPct);
            }

            yield return null;
        }

        // Check if player succeeded
        if (currentPresses >= requiredPresses && (AnxietyManager.Instance == null || !AnxietyManager.Instance.IsGameOver))
        {
            playerEscaped = true;
            Debug.Log("[ProctorJumpscareQTE] PLAYER BROKE FREE! Escaped The Proctor's grasp!");

            // Play break-free sound
            if (_sfxSource != null && breakFreeAudio != null)
            {
                _sfxSource.pitch = 1.0f;
                _sfxSource.PlayOneShot(breakFreeAudio, 1.0f);
            }
        }

        // Stop heartbeat audio
        if (_heartbeatSource != null && _heartbeatSource.isPlaying)
        {
            _heartbeatSource.Stop();
        }

        // Hide UI with quick fade-out
        float fadeOut = 0f;
        while (fadeOut < 0.15f)
        {
            fadeOut += Time.deltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(1f - (fadeOut / 0.15f));
            yield return null;
        }
        HideUIInstant();

        _isQTEActive = false;
        onComplete?.Invoke(playerEscaped);
    }

    private void UpdateDisplay(float progress, float timer)
    {
        if (_barFill != null)
        {
            _barFill.fillAmount = progress;

            // Gradient: blood red at 0% -> electric escape amber/gold at 100%
            Color lowCol = new Color(0.88f, 0.15f, 0.15f, 1f);
            Color highCol = new Color(1.0f, 0.65f, 0.12f, 1f);
            _barFill.color = Color.Lerp(lowCol, highCol, progress);
        }

        if (_barText != null)
        {
            int pct = Mathf.RoundToInt(progress * 100f);
            _barText.text = $"{pct}%";
        }

        // Subtle key badge breathing pulse
        if (_keyBadgeRect != null)
        {
            float pulse = 1.0f + Mathf.Sin(timer * 8f) * 0.06f;
            _keyBadgeRect.localScale = Vector3.one * pulse;
        }

        // Warning text flash when anxiety is high
        if (_warningText != null && AnxietyManager.Instance != null)
        {
            float anxietyPct = AnxietyManager.Instance.CurrentAnxiety / AnxietyManager.Instance.maxAnxiety;
            if (anxietyPct >= 0.70f)
            {
                float flash = Mathf.Sin(timer * 12f) > 0f ? 1f : 0.4f;
                _warningText.color = new Color(1.0f, 0.2f * flash, 0.2f * flash, 1f);
            }
            else
            {
                _warningText.color = new Color(0.90f, 0.75f, 0.75f, 0.95f);
            }
        }
    }

    private IEnumerator PunchKeyBadgeRoutine()
    {
        if (_keyBadgeRect == null) yield break;

        _keyBadgeRect.localScale = Vector3.one * 1.30f;
        if (_keyBadgeBg != null) _keyBadgeBg.color = new Color(0.85f, 0.20f, 0.20f, 1.0f);

        float elapsed = 0f;
        float duration = 0.12f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _keyBadgeRect.localScale = Vector3.Lerp(Vector3.one * 1.30f, Vector3.one, t);
            if (_keyBadgeBg != null)
            {
                _keyBadgeBg.color = Color.Lerp(new Color(0.85f, 0.20f, 0.20f, 1.0f), new Color(0.10f, 0.10f, 0.14f, 0.95f), t);
            }
            yield return null;
        }

        _keyBadgeRect.localScale = Vector3.one;
    }
}
