using System.Collections;
using UnityEngine;

/// <summary>
/// Controls faculty proctor / department head behaviors inside their assigned offices:
/// - Remains stationed at their assigned workstation / service window.
/// - Smoothly and naturally tracks the student with their gaze/torso when within awareness range.
/// - Returns to their default desk/window orientation when the student leaves.
/// - Bridges talking animation state with ClearanceNPC dialogue.
/// </summary>
public class RoomStaffAI : MonoBehaviour
{
    [Header("Staff Identity")]
    public string staffRole = "Faculty Staff";

    [Header("Player Awareness")]
    [Tooltip("Distance at which staff notices the player and turns to face them.")]
    public float awarenessRadius = 4.0f;
    public float facePlayerTurnSpeed = 3.5f;

    [System.Obsolete("Staff proctors are now stationed at their service desks/counters.")]
    [HideInInspector] public Transform[] waypoints = new Transform[0];
    [HideInInspector] public float moveSpeed = 0f;

    [Header("Animation Parameters")]
    public string speedParameter = "Speed";
    public string talkingParameter = "Talking";

    // Runtime state
    private Animator _animator;
    private Transform _playerTransform;
    private NPCSitController _sitController;
    private bool _isTalking = false;
    private Quaternion _homeRotation;
    private Vector3 _homePosition;

    private int _speedHash;
    private int _talkingHash;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _sitController = GetComponent<NPCSitController>();
        _homeRotation = transform.rotation;
        _homePosition = transform.position;

        _speedHash = Animator.StringToHash(speedParameter);
        _talkingHash = Animator.StringToHash(talkingParameter);
    }

    private void Start()
    {
        if (_sitController == null) _sitController = GetComponent<NPCSitController>();

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
        }

        // Lock to exact home post if not handled by chair controller
        if (_sitController == null || !_sitController.HasChair)
        {
            transform.position = _homePosition;
            transform.rotation = _homeRotation;
        }
    }

    private void Update()
    {
        bool hasChair = _sitController != null && _sitController.HasChair;

        // If not managed by chair controller, enforce stationary post
        if (!hasChair)
        {
            transform.position = _homePosition;
        }

        if (_isTalking)
        {
            FacePlayer();
            UpdateAnimator(0f, true);
            return;
        }

        // If seated on a chair, the NPC only stands when interacted with
        if (hasChair)
        {
            UpdateAnimator(0f, false);
            return;
        }

        // Check player proximity for non-chair staff
        if (_playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            if (distToPlayer <= awarenessRadius)
            {
                FacePlayer();
                UpdateAnimator(0f, false);
                return;
            }
        }

        // Return to home desk/window orientation when player is not nearby
        if (Quaternion.Angle(transform.rotation, _homeRotation) > 0.5f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _homeRotation, Time.deltaTime * facePlayerTurnSpeed * 0.75f);
        }
        else
        {
            transform.rotation = _homeRotation;
        }

        UpdateAnimator(0f, false);
    }

    private void FacePlayer()
    {
        if (_playerTransform == null) return;
        Vector3 targetDir = _playerTransform.position - transform.position;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * facePlayerTurnSpeed);
        }
    }

    private void UpdateAnimator(float speed, bool talking)
    {
        if (_animator == null) return;
        _animator.SetFloat(_speedHash, speed);
        _animator.SetBool(_talkingHash, talking);
    }

    public void SetTalkingState(bool talking)
    {
        _isTalking = talking;
        UpdateAnimator(0f, talking);

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            if (talking)
            {
                _sitController.StandUp();
            }
            else
            {
                _sitController.SitDown();
            }
        }
    }

    /// <summary>
    /// Re-anchors the staff member's home post to their current position and rotation.
    /// </summary>
    public void ResetHomeTransform()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
    }
}
