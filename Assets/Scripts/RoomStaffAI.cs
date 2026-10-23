using System.Collections;
using UnityEngine;

/// <summary>
/// Controls faculty proctor / department head behaviors inside their assigned offices:
/// - Remains stationed at their assigned workstation / service window.
/// - Smoothly and naturally tracks the student with their gaze/torso when within awareness range.
/// - Returns to their default desk/window orientation when the student leaves.
/// - Bridges talking animation state with ClearanceNPC dialogue.
/// - Handles the University Registrar's panicked runaway sequence out of Room 104 and down the hallway.
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

    [Header("Runaway Chase Feature")]
    public bool isRunawayActive = false;
    public bool isReturningHome = false;
    public bool isCaughtTalkingInHallway = false;
    private UnityEngine.AI.NavMeshAgent _navAgent;

    // Runtime state
    private Animator _animator;
    private Transform _playerTransform;
    private NPCSitController _sitController;
    private bool _isTalking = false;
    private Quaternion _homeRotation;
    private Vector3 _homePosition;
    private Vector3 _initialArmatureLocalPos;
    private Quaternion _initialArmatureLocalRot;

    private int _speedHash;
    private int _talkingHash;

    // Room 104 & Ground Floor Hallway obstacle-free path nodes
    private static readonly Vector3 Room104AisleNorth = new Vector3(-80.85f, 2.35f, 4.86f);
    private static readonly Vector3 Room104AisleDoor  = new Vector3(-80.85f, 2.35f, 1.35f);
    private static readonly Vector3 Room104DoorInner  = new Vector3(-82.30f, 2.35f, 1.35f);
    private static readonly Vector3 Room104DoorOuter  = new Vector3(-84.45f, 2.35f, 1.35f);

    private static readonly Vector3[] HallwayChaseNodes = new Vector3[]
    {
        new Vector3(-84.45f, 2.35f, 6.50f),   // Outside Room 104 windows
        new Vector3(-83.40f, 2.35f, 13.50f),  // South of Lobby RoundTable (clear east lane)
        new Vector3(-83.40f, 2.35f, 19.80f),  // North of Lobby RoundTable (clear east lane)
        new Vector3(-84.45f, 2.35f, 25.50f),  // North corridor past stairs
        new Vector3(-84.45f, 2.35f, 31.50f)   // North corridor by ComLab 1 / Room 105
    };

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _sitController = GetComponent<NPCSitController>();
        _homeRotation = transform.rotation;
        _homePosition = transform.position;

        if (_animator != null)
        {
            _initialArmatureLocalPos = _animator.transform.localPosition;
            _initialArmatureLocalRot = _animator.transform.localRotation;
            _animator.applyRootMotion = false;
        }

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
        else if (_sitController.SeatedRootPos != Vector3.zero)
        {
            _homePosition = _sitController.SeatedRootPos;
            _homeRotation = _sitController.SeatedRootRot;
        }
    }

    private void LateUpdate()
    {
        // Keep child Armature locked to root so any residual root motion never drifts the mesh off the collider
        if (_animator != null && _animator.transform != transform)
        {
            _animator.transform.localPosition = _initialArmatureLocalPos;
            _animator.transform.localRotation = _initialArmatureLocalRot;
        }
    }

    private void Update()
    {
        if (isRunawayActive)
        {
            if (Time.time >= _nextPanicScreamTime)
            {
                PlayPanicScream();
                _nextPanicScreamTime = Time.time + Random.Range(3.5f, 5.5f);
            }
            return;
        }

        if (isReturningHome)
        {
            if (isCaughtTalkingInHallway)
            {
                FacePlayer();
                UpdateAnimator(0f, true);
            }
            return;
        }

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
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator == null) return;
        _animator.SetFloat(_speedHash, speed);
        _animator.SetBool(_talkingHash, talking);
    }

    public void SetTalkingState(bool talking)
    {
        // Never override locomotion with stationary desk talking state during runaway or return home
        if (isRunawayActive) return;
        if (isReturningHome && !isCaughtTalkingInHallway) return;

        _isTalking = talking;
        UpdateAnimator(0f, talking);

        if (isReturningHome) return;

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

    private AudioSource _panicAudioSource;
    private float _nextPanicScreamTime = 0f;

    public void TriggerRunaway(Vector3 hallwayTarget)
    {
        if (isRunawayActive) return;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.SeatedRootPos != Vector3.zero)
        {
            _homePosition = _sitController.SeatedRootPos;
            _homeRotation = _sitController.SeatedRootRot;
        }

        isRunawayActive = true;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;
        _isTalking = false;

        // Immediately release sitting bone overrides and restore full standing capsule collider
        if (_sitController != null)
        {
            _sitController.ForceStandImmediate();
        }

        // Disable any NavMeshAgent so deterministic corridor steering controls root transform smoothly
        _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (_navAgent != null) _navAgent.enabled = false;

        ForceOpenRoom104Doors();

        StopAllCoroutines();
        StartCoroutine(RunawaySequenceRoutine());
    }

    private void ForceOpenRoom104Doors()
    {
        foreach (var door in FindObjectsByType<DoorInteract>(FindObjectsInactive.Exclude))
        {
            if (door == null) continue;
            if (door.gameObject.name.Contains("104") || Vector3.Distance(door.transform.position, Room104DoorInner) < 6.0f)
            {
                door.ForceOpen();
            }
        }
    }

    private IEnumerator MoveToPoint(Vector3 target, float speed, float animSpeedValue, float arriveDist = 0.25f, float maxTime = 8.0f)
    {
        float elapsed = 0f;
        while (elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            Vector3 currentPos = transform.position;
            Vector3 diff = new Vector3(target.x - currentPos.x, 0f, target.z - currentPos.z);
            float dist = diff.magnitude;
            if (dist <= arriveDist) break;

            Vector3 dir = diff / dist;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 14f);
            }

            float step = Mathf.Min(speed * Time.deltaTime, dist);
            transform.position = new Vector3(currentPos.x + dir.x * step, target.y, currentPos.z + dir.z * step);
            UpdateAnimator(animSpeedValue, false);
            yield return null;
        }
    }

    private IEnumerator RunawaySequenceRoutine()
    {
        EnsurePanicAudioSource();
        PlayPanicScream();
        _nextPanicScreamTime = Time.time + 3.0f;

        // Immediately enter running animation state
        UpdateAnimator(2.0f, false);

        // Step 1: Step back from Window 2 chair into the clear Room 104 aisle
        yield return StartCoroutine(MoveToPoint(Room104AisleNorth, 3.6f, 2.0f, 0.20f, 2.5f));

        // Step 2: Sprint south down the Room 104 aisle (in full view of the service windows!)
        yield return StartCoroutine(MoveToPoint(Room104AisleDoor, 4.0f, 2.0f, 0.22f, 3.0f));

        // Step 3: Turn toward Door_104A threshold and ensure the door is wide open
        ForceOpenRoom104Doors();
        yield return StartCoroutine(MoveToPoint(Room104DoorInner, 4.0f, 2.0f, 0.22f, 2.5f));

        // Step 4: Burst through Door_104A out into the Ground Floor Hallway
        yield return StartCoroutine(MoveToPoint(Room104DoorOuter, 4.2f, 2.0f, 0.25f, 2.5f));

        // Step 5: Continuous panicked hallway chase loop!
        // She runs North and South along the Ground Floor corridor at a catchable pace (3.55 m/s)
        // so the player actively chases her down the hallway.
        int index = 0;
        int direction = 1;
        while (isRunawayActive)
        {
            Vector3 targetNode = HallwayChaseNodes[index];
            yield return StartCoroutine(MoveToPoint(targetNode, 3.55f, 2.0f, 0.30f, 6.0f));

            if (!isRunawayActive) yield break;

            index += direction;
            if (index >= HallwayChaseNodes.Length)
            {
                index = HallwayChaseNodes.Length - 2;
                direction = -1;
            }
            else if (index < 0)
            {
                index = 1;
                direction = 1;
            }
        }
    }

    private void EnsurePanicAudioSource()
    {
        if (_panicAudioSource == null)
        {
            _panicAudioSource = gameObject.AddComponent<AudioSource>();
            _panicAudioSource.spatialBlend = 0.65f;
            _panicAudioSource.minDistance = 2.0f;
            _panicAudioSource.maxDistance = 22.0f;
            _panicAudioSource.playOnAwake = false;
            _panicAudioSource.volume = 0.85f;
        }
    }

    private void PlayPanicScream()
    {
        EnsurePanicAudioSource();
        var clrNpc = GetComponent<ClearanceNPC>();
        AudioClip clip = (clrNpc != null && clrNpc.mumbleAudioClip != null) ? clrNpc.mumbleAudioClip : null;
#if UNITY_EDITOR
        if (clip == null)
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
#endif
        if (_panicAudioSource != null && clip != null)
        {
            _panicAudioSource.pitch = Random.Range(1.20f, 1.35f);
            _panicAudioSource.PlayOneShot(clip, 0.85f);
        }
    }

    /// <summary>
    /// Called when the player catches up to the fleeing Registrar in the hallway and presses [E].
    /// Stops her running in place so she can face the player during the catch dialogue, then walks home when dialogue closes.
    /// </summary>
    public void CatchStaff()
    {
        if (!isRunawayActive) return;

        isRunawayActive = false;
        isReturningHome = true;
        isCaughtTalkingInHallway = true;

        if (_panicAudioSource != null && _panicAudioSource.isPlaying)
        {
            _panicAudioSource.Stop();
        }

        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = true;
            _navAgent.enabled = false;
        }

        StopAllCoroutines();
        FacePlayer();
        UpdateAnimator(0f, true);
    }

    /// <summary>
    /// Triggered when the hallway catch dialogue closes so the Registrar walks back into Room 104.
    /// </summary>
    public void BeginReturnHomeAfterCatch()
    {
        if (!isReturningHome) return;
        isCaughtTalkingInHallway = false;
        _isTalking = false;
        StopAllCoroutines();
        StartCoroutine(ReturnHomeRoutine());
    }

    /// <summary>
    /// If the player sprints back to Window 2 before the Registrar finishes walking back,
    /// snap her cleanly to her desk seat so the sorting puzzle can begin immediately.
    /// </summary>
    public void CompleteReturnHomeImmediate()
    {
        if (!isReturningHome) return;
        StopAllCoroutines();
        isRunawayActive = false;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;
        _isTalking = false;

        transform.position = _homePosition;
        transform.rotation = _homeRotation;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.SitDown();
        }

        UpdateAnimator(0f, false);
    }

    private IEnumerator ReturnHomeRoutine()
    {
        UpdateAnimator(1.0f, false);

        // Find closest hallway node to walk back south along the clear corridor lane
        int closestIdx = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < HallwayChaseNodes.Length; i++)
        {
            float d = Vector3.Distance(transform.position, HallwayChaseNodes[i]);
            if (d < bestDist)
            {
                bestDist = d;
                closestIdx = i;
            }
        }

        for (int i = closestIdx; i >= 0; i--)
        {
            if (HallwayChaseNodes[i].z <= transform.position.z + 1.0f)
            {
                yield return StartCoroutine(MoveToPoint(HallwayChaseNodes[i], 2.4f, 1.0f, 0.30f, 6.0f));
            }
        }

        ForceOpenRoom104Doors();

        // Walk to hallway door threshold, through Door_104A, up the aisle, and back to Window 2 chair
        yield return StartCoroutine(MoveToPoint(Room104DoorOuter, 2.4f, 1.0f, 0.25f, 6.0f));
        yield return StartCoroutine(MoveToPoint(Room104DoorInner, 2.2f, 1.0f, 0.22f, 3.0f));
        yield return StartCoroutine(MoveToPoint(Room104AisleDoor, 2.2f, 1.0f, 0.22f, 3.0f));
        yield return StartCoroutine(MoveToPoint(Room104AisleNorth, 2.2f, 1.0f, 0.22f, 3.5f));
        yield return StartCoroutine(MoveToPoint(_homePosition, 2.0f, 1.0f, 0.15f, 2.5f));

        transform.position = _homePosition;
        transform.rotation = _homeRotation;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.SitDown();
        }

        UpdateAnimator(0f, false);
    }
}
