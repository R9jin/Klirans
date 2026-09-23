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
/// </summary>
public class LockerHideManager : MonoBehaviour
{
    public static LockerHideManager Instance { get; private set; }

    // ── Static flag read by ProctorAI / NPCJumpscareManager ─────────────────
    public static bool IsPlayerHidden { get; private set; } = false;

    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Peek Gap Settings")]
    [Tooltip("Fraction of screen height occupied by each black panel (top + bottom). " +
             "0.45 = 45% top + 45% bottom → 10% slit in the middle.")]
    [Range(0.30f, 0.49f)]
    public float blackPanelFraction = 0.43f;

    [Tooltip("FOV of the camera while peeking through the locker gap (narrow slit).")]
    public float peekFOV = 28f;

    [Tooltip("How fast the camera lerps to the peek anchor position.")]
    public float cameraTransitionSpeed = 8f;

    [Tooltip("Dim / dark tint applied inside the locker to suggest darkness.")]
    public Color lockerInteriorTint = new Color(0f, 0f, 0f, 0.30f);

    [Header("Exit Key")]
    [Tooltip("Key to exit the locker (in addition to pressing E again near the locker).")]
    public KeyCode exitKey = KeyCode.F;

    // ── Private runtime ──────────────────────────────────────────────────────
    private Camera          _playerCam;
    private PlayerMovement  _playerMovement;
    private LockerHide      _currentLocker;

    // Saved camera state
    private Vector3    _savedCamLocalPos;
    private Quaternion _savedCamLocalRot;
    private float      _savedFOV;
    private Transform  _savedCamParent;

    // Peek anchor (the LockerHide sets this)
    private Transform _peekAnchor;

    // UI
    private RectTransform _topPanel;
    private RectTransform _bottomPanel;
    private Image         _interiorTintImage;
    private CanvasGroup   _overlayGroup;

    private bool _isTransitioning = false;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerMovement != null)
            _playerCam = _playerMovement.playerCamera;
        if (_playerCam == null)
            _playerCam = Camera.main;

        BuildPeekOverlayUI();
        SetOverlayVisible(false, instant: true);
    }

    private void Update()
    {
        if (!IsPlayerHidden) return;

        // Smooth camera drift toward peek anchor each frame
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

        // Exit on F key or Escape
        if (Input.GetKeyDown(exitKey) || Input.GetKeyDown(KeyCode.Escape))
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

        _currentLocker = locker;
        _peekAnchor    = peekAnchor;

        // Save camera state
        if (_playerCam != null)
        {
            _savedCamLocalPos = _playerCam.transform.localPosition;
            _savedCamLocalRot = _playerCam.transform.localRotation;
            _savedFOV         = _playerCam.fieldOfView;
            _savedCamParent   = _playerCam.transform.parent;

            // Un-parent camera so we can world-space position it at the peek anchor
            _playerCam.transform.SetParent(null, worldPositionStays: true);
        }

        // Freeze player movement
        if (_playerMovement != null)
            _playerMovement.SetControlsEnabled(false);

        // Lock cursor remains locked — player can still look left/right a tiny bit
        // (movement is frozen but mouse is not freed so it feels like peering)

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

        // Fade overlay in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * cameraTransitionSpeed;
            if (_overlayGroup != null)
                _overlayGroup.alpha = Mathf.Clamp01(t);

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

        // Fade overlay out and restore camera
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * cameraTransitionSpeed;
            if (_overlayGroup != null)
                _overlayGroup.alpha = Mathf.Clamp01(1f - t);

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

        // Snap final
        if (_playerCam != null)
        {
            _playerCam.transform.localPosition = _savedCamLocalPos;
            _playerCam.transform.localRotation = _savedCamLocalRot;
            _playerCam.fieldOfView             = _savedFOV;
        }

        // Re-enable movement
        if (_playerMovement != null)
            _playerMovement.SetControlsEnabled(true);

        // Sync pitch to avoid camera snap
        if (_playerMovement != null && _playerCam != null)
        {
            float pitch = _playerCam.transform.localRotation.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            _playerMovement.SyncPitch(pitch);
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
    /// Builds the black top/bottom panels and interior tint on HudCanvas at runtime.
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

        // Interior darkness tint (full screen, behind the gap panels)
        _interiorTintImage = CreatePanel(overlayRoot.transform, "LockerTint",
            new Vector2(0f, 0f), new Vector2(1f, 1f), lockerInteriorTint);

        // Top black panel — covers top portion of screen
        var topImg = CreatePanel(overlayRoot.transform, "PeekTop",
            new Vector2(0f, 1f - blackPanelFraction), new Vector2(1f, 1f),
            Color.black);
        _topPanel = topImg.GetComponent<RectTransform>();

        // Bottom black panel — covers bottom portion of screen
        var botImg = CreatePanel(overlayRoot.transform, "PeekBottom",
            new Vector2(0f, 0f), new Vector2(1f, blackPanelFraction),
            Color.black);
        _bottomPanel = botImg.GetComponent<RectTransform>();

        // Thin dark edges on left and right of the slit (simulates locker door frame)
        CreatePanel(overlayRoot.transform, "PeekLeft",
            new Vector2(0f, blackPanelFraction), new Vector2(0.012f, 1f - blackPanelFraction),
            new Color(0f, 0f, 0f, 0.85f));
        CreatePanel(overlayRoot.transform, "PeekRight",
            new Vector2(0.988f, blackPanelFraction), new Vector2(1f, 1f - blackPanelFraction),
            new Color(0f, 0f, 0f, 0.85f));
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
