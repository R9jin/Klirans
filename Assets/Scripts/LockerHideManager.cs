using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LockerHideManager — Singleton that manages the player's locker-hiding state.
///
/// Responsibilities:
///   1. Tracks IsPlayerHidden — checked by ProctorAI and NPCJumpscareManager.
///   2. Manages the peep-gap UI overlay (two black panels with a horizontal slit).
///   3. Handles camera transition into / out of the locker peek anchor.
///   4. Re-enables / disables player movement around hide/unhide.
///
/// SETUP:
///   Attach this to your HUD / GameManager GameObject.
///   The peep overlay panels are created automatically at runtime on the HudCanvas.
public class LockerHideManager : MonoBehaviour
{
    private static LockerHideManager _instance;
    public static LockerHideManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<LockerHideManager>();
                if (_instance == null)
                {
                    var gm = GameObject.Find("GameManager") ?? new GameObject("LockerHideManager");
                    _instance = gm.GetComponent<LockerHideManager>() ?? gm.AddComponent<LockerHideManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    // ── Static flag read by ProctorAI / NPCJumpscareManager ─────────────────
    public static bool IsPlayerHidden { get; private set; } = false;

    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Peek Gap Settings")]
    [Tooltip("Fraction of screen height occupied by top and bottom dark metal frames.")]
    [Range(0.10f, 0.40f)]
    public float blackPanelFraction = 0.18f;

    [Tooltip("FOV of the camera while peeking through the locker gap (narrow slit).")]
    public float peekFOV = 55f;

    [Tooltip("How fast the camera lerps to the peek anchor position.")]
    public float cameraTransitionSpeed = 8f;

    [Tooltip("Dim / dark tint applied inside the locker to suggest darkness.")]
    public Color lockerInteriorTint = new Color(0f, 0f, 0f, 0.30f);

    [Header("Exit Key")]
    [Tooltip("Key to exit the locker (in addition to pressing E again near the locker).")]
    public KeyCode exitKey = KeyCode.F;

    [Header("Heartbeat Sound Inside Locker")]
    [Tooltip("Frantic heartbeat audio played while hiding inside lockers.")]
    public AudioClip heartbeatAudio;

    [Range(0f, 1f)]
    public float heartbeatVolume = 0.55f;

    // ── Private runtime ──────────────────────────────────────────────────────
    private Camera          _playerCam;
    private PlayerMovement  _playerMovement;
    private LockerHide      _currentLocker;

    // Saved camera state
    private Vector3    _savedCamLocalPos;
    private Quaternion _savedCamLocalRot;
    private float      _savedFOV;
    private float      _savedNearClip = 0.3f;
    private Transform  _savedCamParent;

    // Peek anchor (the LockerHide sets this)
    private Transform _peekAnchor;

    // Audio
    private AudioSource _heartbeatSource;

    // UI
    private RectTransform _topPanel;
    private RectTransform _bottomPanel;
    private Image         _interiorTintImage;
    private CanvasGroup   _overlayGroup;

    private bool _isTransitioning = false;
    private int _enterFrame = -1;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure generous, crystal-clear hallway view (not narrow 14% slits or 28 FOV)
        if (blackPanelFraction > 0.15f) blackPanelFraction = 0.12f;
        if (peekFOV < 50f) peekFOV = 65f;
    }

    private void Start()
    {
        _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerMovement != null)
            _playerCam = _playerMovement.playerCamera;
        if (_playerCam == null)
            _playerCam = Camera.main;

#if UNITY_EDITOR
        if (heartbeatAudio == null)
            heartbeatAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");
#endif
        if (heartbeatAudio == null)
            heartbeatAudio = Resources.Load<AudioClip>("Sounds/fast heart beat");

        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.loop = true;
        _heartbeatSource.clip = heartbeatAudio;
        _heartbeatSource.volume = 0f;
        _heartbeatSource.playOnAwake = false;

        BuildPeekOverlayUI();
        SetOverlayVisible(false, instant: true);
    }

    private void Update()
    {
        if (!IsPlayerHidden) return;

        // Smooth camera drift toward peek anchor — locked horizontally and vertically looking forward into the corridor
        if (_peekAnchor != null && _playerCam != null && !_isTransitioning)
        {
            _playerCam.transform.position = Vector3.Lerp(
                _playerCam.transform.position,
                _peekAnchor.position,
                cameraTransitionSpeed * Time.deltaTime
            );
            _playerCam.transform.rotation = Quaternion.Slerp(
                _playerCam.transform.rotation,
                _peekAnchor.rotation,
                cameraTransitionSpeed * Time.deltaTime
            );
        }

        // Dynamic terror heartbeat: subtle at first, increases with anxiety, surges when enemy stalks close
        if (_heartbeatSource != null && _heartbeatSource.isPlaying)
        {
            float nearestEnemyDist = float.MaxValue;
            if (TheProctorAI.ActiveProctor != null)
            {
                float d = Vector3.Distance(_playerCam.transform.position, TheProctorAI.ActiveProctor.transform.position);
                if (d < nearestEnemyDist) nearestEnemyDist = d;
            }
            if (WeepingStudentAI.ActiveWeepingStudent != null)
            {
                float d = Vector3.Distance(_playerCam.transform.position, WeepingStudentAI.ActiveWeepingStudent.transform.position);
                if (d < nearestEnemyDist) nearestEnemyDist = d;
            }

            float anxietyNorm = (AnxietyManager.Instance != null && AnxietyManager.Instance.maxAnxiety > 0f)
                ? Mathf.Clamp01(AnxietyManager.Instance.CurrentAnxiety / AnxietyManager.Instance.maxAnxiety)
                : 0f;

            // Base subtle volume (0.16f) scaling up to 0.55f with player anxiety
            float baseVol = Mathf.Lerp(0.16f, 0.55f, anxietyNorm);
            float basePitch = Mathf.Lerp(0.95f, 1.15f, anxietyNorm);

            // Stalking enemy proximity surge (within 16 meters)
            float enemyProximity = (nearestEnemyDist < 16.0f) ? Mathf.Clamp01(1.0f - (nearestEnemyDist / 16.0f)) : 0f;

            float targetVol = Mathf.Clamp01(baseVol + enemyProximity * 0.55f);
            float targetPitch = Mathf.Clamp(basePitch + enemyProximity * 0.35f, 0.95f, 1.48f);

            _heartbeatSource.pitch = Mathf.MoveTowards(_heartbeatSource.pitch, targetPitch, Time.deltaTime * 0.8f);
            _heartbeatSource.volume = Mathf.MoveTowards(_heartbeatSource.volume, targetVol, Time.deltaTime * 0.8f);
        }

        // Prevent exit on the same frame player entered (avoids consuming same 'E' key press)
        if (Time.frameCount == _enterFrame) return;

        // Exit on F key, E key, or Escape
        if (Input.GetKeyDown(exitKey) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitLocker();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LockerHide when the player enters a locker.
    /// </summary>
    public void EnterLocker(LockerHide locker, Transform peekAnchor)
    {
        if (IsPlayerHidden || _isTransitioning) return;

        _enterFrame = Time.frameCount;
        _currentLocker = locker;
        _peekAnchor    = peekAnchor;

        // Save camera state
        if (_playerCam != null)
        {
            _savedCamLocalPos = _playerCam.transform.localPosition;
            _savedCamLocalRot = _playerCam.transform.localRotation;
            _savedFOV         = _playerCam.fieldOfView;
            _savedNearClip    = _playerCam.nearClipPlane;
            _savedCamParent   = _playerCam.transform.parent;

            // Tighten near clip plane so camera never clips through locker louvers / door panels
            _playerCam.nearClipPlane = 0.05f;

            // Un-parent camera so we can world-space position it at the peek anchor
            _playerCam.transform.SetParent(null, worldPositionStays: true);
        }

        // Hide locker mesh renderers so solid locker geometry never blocks the camera from inside
        if (_currentLocker != null)
        {
            var rends = _currentLocker.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r != null) r.enabled = false;
            }
        }

        // Freeze player movement and disable character controller so player body does not block NPCs in hallway
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            var cc = _playerMovement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        IsPlayerHidden = true;

        StartCoroutine(TransitionIn());
    }

    /// <summary>
    /// Called by LockerHide or the exit key handler to leave a locker.
    /// </summary>
    public void ExitLocker()
    {
        if (!IsPlayerHidden || _isTransitioning) return;

        StartCoroutine(TransitionOut());
    }

    // ── Transitions ──────────────────────────────────────────────────────────

    private IEnumerator TransitionIn()
    {
        _isTransitioning = true;

        // Start looping heartbeat sound
        if (_heartbeatSource != null && heartbeatAudio != null)
        {
            _heartbeatSource.pitch = 1.0f;
            _heartbeatSource.volume = 0f;
            _heartbeatSource.Play();
        }

        float anxietyNorm = (AnxietyManager.Instance != null && AnxietyManager.Instance.maxAnxiety > 0f)
            ? Mathf.Clamp01(AnxietyManager.Instance.CurrentAnxiety / AnxietyManager.Instance.maxAnxiety)
            : 0f;
        float targetEnterVol = Mathf.Lerp(0.16f, 0.55f, anxietyNorm);

        // Fade overlay in and ramp heartbeat volume
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * cameraTransitionSpeed;
            if (_overlayGroup != null)
                _overlayGroup.alpha = Mathf.Clamp01(t);

            if (_heartbeatSource != null)
                _heartbeatSource.volume = Mathf.Lerp(0f, targetEnterVol, t);

            // Move camera toward peek anchor
            if (_playerCam != null && _peekAnchor != null)
            {
                _playerCam.transform.position = Vector3.Lerp(
                    _playerCam.transform.position,
                    _peekAnchor.position,
                    t
                );
                _playerCam.transform.rotation = Quaternion.Slerp(
                    _playerCam.transform.rotation,
                    _peekAnchor.rotation,
                    t
                );
                _playerCam.fieldOfView = Mathf.Lerp(_savedFOV, peekFOV, t);
            }

            yield return null;
        }

        // Snap final
        if (_playerCam != null && _peekAnchor != null)
        {
            _playerCam.transform.position = _peekAnchor.position;
            _playerCam.transform.rotation = _peekAnchor.rotation;
            _playerCam.fieldOfView        = peekFOV;
        }

        if (_heartbeatSource != null)
            _heartbeatSource.volume = targetEnterVol;

        SetOverlayVisible(true, instant: true);
        _isTransitioning = false;
    }

    private IEnumerator TransitionOut()
    {
        _isTransitioning = true;

        // Re-parent camera back to player
        if (_playerCam != null && _savedCamParent != null)
        {
            _playerCam.transform.SetParent(_savedCamParent, worldPositionStays: true);
        }

        // Fade overlay out and fade heartbeat out
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * cameraTransitionSpeed;
            if (_overlayGroup != null)
                _overlayGroup.alpha = Mathf.Clamp01(1f - t);

            if (_heartbeatSource != null)
                _heartbeatSource.volume = Mathf.Lerp(heartbeatVolume, 0f, t);

            if (_playerCam != null)
            {
                _playerCam.transform.localPosition = Vector3.Lerp(
                    _playerCam.transform.localPosition,
                    _savedCamLocalPos,
                    t
                );
                _playerCam.transform.localRotation = Quaternion.Slerp(
                    _playerCam.transform.localRotation,
                    _savedCamLocalRot,
                    t
                );
                _playerCam.fieldOfView = Mathf.Lerp(peekFOV, _savedFOV, t);
            }

            yield return null;
        }

        // Stop heartbeat audio
        if (_heartbeatSource != null)
        {
            _heartbeatSource.Stop();
            _heartbeatSource.volume = 0f;
        }

        // Snap final
        if (_playerCam != null)
        {
            _playerCam.transform.localPosition = _savedCamLocalPos;
            _playerCam.transform.localRotation = _savedCamLocalRot;
            _playerCam.fieldOfView             = _savedFOV;
            _playerCam.nearClipPlane           = _savedNearClip;
        }

        // Re-enable movement and character controller
        if (_playerMovement != null)
        {
            var cc = _playerMovement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            _playerMovement.SetControlsEnabled(true);
        }

        // Sync pitch to avoid camera snap
        if (_playerMovement != null && _playerCam != null)
        {
            float pitch = _playerCam.transform.localRotation.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            _playerMovement.SyncPitch(pitch);
        }

        // Restore locker mesh renderers
        if (_currentLocker != null)
        {
            var rends = _currentLocker.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r != null) r.enabled = true;
            }
        }

        SetOverlayVisible(false, instant: true);

        IsPlayerHidden = false;
        _currentLocker = null;
        _peekAnchor    = null;
        _isTransitioning = false;

        Debug.Log("[LockerHideManager] Player exited locker.");
    }

    // ── UI Overlay ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the locker peek hole overlay UI with metal doors, ventilation slats, and exit prompt on HudCanvas.
    /// </summary>
    private void BuildPeekOverlayUI()
    {
        var hud = GameObject.Find("HudCanvas");
        if (hud == null)
        {
            Debug.LogWarning("[LockerHideManager] HudCanvas not found — peek overlay UI will not be created.");
            return;
        }

        // Root group for easy alpha control
        var overlayRoot = new GameObject("LockerPeekOverlay", typeof(RectTransform), typeof(CanvasGroup));
        overlayRoot.transform.SetParent(hud.transform, false);
        var rootRT = overlayRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        _overlayGroup = overlayRoot.GetComponent<CanvasGroup>();
        _overlayGroup.blocksRaycasts = false;
        _overlayGroup.interactable   = false;

        // Wide, clear view of the hallway: 84% width and 64% height
        float slitLeft = 0.08f;
        float slitRight = 0.92f;
        float slitBottom = blackPanelFraction; // 0.18f
        float slitTop = 1f - blackPanelFraction; // 0.82f

        // 1. Left locker door panel
        CreatePanel(overlayRoot.transform, "PeekDoorLeft",
            new Vector2(0f, 0f), new Vector2(slitLeft, 1f),
            new Color(0.04f, 0.04f, 0.05f, 0.98f));

        // 2. Right locker door panel
        CreatePanel(overlayRoot.transform, "PeekDoorRight",
            new Vector2(slitRight, 0f), new Vector2(1f, 1f),
            new Color(0.04f, 0.04f, 0.05f, 0.98f));

        // 3. Top locker door panel
        var topImg = CreatePanel(overlayRoot.transform, "PeekTop",
            new Vector2(slitLeft, slitTop), new Vector2(slitRight, 1f),
            new Color(0.03f, 0.03f, 0.04f, 0.98f));
        _topPanel = topImg.GetComponent<RectTransform>();

        // 4. Bottom locker door panel
        var botImg = CreatePanel(overlayRoot.transform, "PeekBottom",
            new Vector2(slitLeft, 0f), new Vector2(slitRight, slitBottom),
            new Color(0.03f, 0.03f, 0.04f, 0.98f));
        _bottomPanel = botImg.GetComponent<RectTransform>();

        // Subtle top and bottom louver shadow trim (at outer edges of the slit frame only, never blocking corridor view)
        CreatePanel(overlayRoot.transform, "LockerSlat_TopTrim",
            new Vector2(slitLeft, slitTop - 0.012f), new Vector2(slitRight, slitTop),
            new Color(0.08f, 0.08f, 0.10f, 0.95f));

        CreatePanel(overlayRoot.transform, "LockerSlat_BotTrim",
            new Vector2(slitLeft, slitBottom), new Vector2(slitRight, slitBottom + 0.012f),
            new Color(0.08f, 0.08f, 0.10f, 0.95f));

        // 6. Exit Prompt Badge at bottom of screen
        var promptGO = new GameObject("ExitHintBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
        promptGO.transform.SetParent(overlayRoot.transform, false);
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.5f, 0.04f);
        promptRT.anchorMax = new Vector2(0.5f, 0.04f);
        promptRT.pivot = new Vector2(0.5f, 0.5f);
        promptRT.sizeDelta = new Vector2(240f, 32f);

        var promptImg = promptGO.GetComponent<Image>();
        promptImg.color = new Color(0.05f, 0.05f, 0.07f, 0.88f);

        var promptOutline = promptGO.GetComponent<Outline>();
        promptOutline.effectColor = new Color(0.6f, 0.2f, 0.2f, 0.6f);
        promptOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var textGO = new GameObject("ExitText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(promptGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var promptText = textGO.GetComponent<Text>();
        promptText.text = "[E] or [F] to Exit Locker";
        var customFont = Resources.Load<Font>("Fonts/Typewriter_Bold");
        promptText.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 14;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = new Color(0.95f, 0.92f, 0.88f, 0.95f);
    }

    private Image CreatePanel(Transform parent, string goName, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go  = new GameObject(goName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color          = color;
        img.raycastTarget  = false;
        return img;
    }

    private void SetOverlayVisible(bool visible, bool instant = false)
    {
        if (_overlayGroup == null) return;
        _overlayGroup.alpha = visible ? 1f : 0f;
    }
}
