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

    [Header("Runaway Chase Feature")]
    public bool isRunawayActive = false;
    public bool isReturningHome = false;
    private UnityEngine.AI.NavMeshAgent _navAgent;

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
        if (isRunawayActive)
        {
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                float spd = _navAgent.velocity.magnitude > 0.1f ? 2.0f : 0f;
                UpdateAnimator(spd, false);
            }

            if (Time.time >= _nextPanicScreamTime)
            {
                PlayPanicScream();
                _nextPanicScreamTime = Time.time + Random.Range(3.5f, 5.5f);
            }
            return;
        }

        if (isReturningHome)
        {
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

    private AudioSource _panicAudioSource;
    private float _nextPanicScreamTime = 0f;

    public void TriggerRunaway(Vector3 hallwayTarget)
    {
        if (isRunawayActive) return;
        isRunawayActive = true;
        isReturningHome = false;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.StandUp();
        }

        // Auto-open nearby doors so runaway is completely unobstructed
        foreach (var door in FindObjectsByType<DoorInteract>(FindObjectsInactive.Exclude))
        {
            if (Vector3.Distance(door.transform.position, transform.position) < 8.5f)
            {
                door.ForceOpen();
            }
        }

        StopAllCoroutines();
        StartCoroutine(RunawaySequenceRoutine(hallwayTarget));
    }

    private IEnumerator MoveToPoint(Vector3 target, float speed, float arriveDist = 0.35f)
    {
        float timeout = 4.5f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;
            Vector3 diff = target - transform.position;
            diff.y = 0f;
            if (diff.magnitude <= arriveDist) break;

            Vector3 dir = diff.normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 12f);
            }
            transform.position += dir * speed * Time.deltaTime;
            UpdateAnimator(speed > 2.2f ? 2.0f : 1.0f, false);
            yield return null;
        }
    }

    private IEnumerator RunawaySequenceRoutine(Vector3 hallwayTarget)
    {
        // Panic screams
        EnsurePanicAudioSource();
        PlayPanicScream();
        _nextPanicScreamTime = Time.time + 3.0f;

        // Ensure NavAgent is disabled during door threshold transition
        _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (_navAgent != null) _navAgent.enabled = false;

        // Step 1: Move from desk to inner doorway threshold
        Vector3 doorInner = new Vector3(-82.4f, 2.35f, 0.53f);
        yield return StartCoroutine(MoveToPoint(doorInner, 2.8f));

        // Step 2: Step through doorway into the hallway corridor
        Vector3 doorOuter = new Vector3(-84.6f, 2.35f, 0.53f);
        yield return StartCoroutine(MoveToPoint(doorOuter, 3.2f));

        // Step 3: Now out in the main hallway, activate NavMeshAgent for smooth corridor navigation
        if (_navAgent == null)
        {
            _navAgent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _navAgent.radius = 0.35f;
            _navAgent.height = 1.8f;
            _navAgent.baseOffset = 0f;
            _navAgent.acceleration = 14f;
            _navAgent.angularSpeed = 380f;
        }

        _navAgent.enabled = true;
        _navAgent.speed = 3.3f;
        _navAgent.stoppingDistance = 0.5f;

        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit startHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            _navAgent.Warp(startHit.position);
        }

        if (UnityEngine.AI.NavMesh.SamplePosition(hallwayTarget, out UnityEngine.AI.NavMeshHit hit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            _navAgent.SetDestination(hit.position);
        }
        else
        {
            _navAgent.SetDestination(hallwayTarget);
        }

        UpdateAnimator(2.0f, false);
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

    public void CatchStaff()
    {
        if (!isRunawayActive) return;

        isRunawayActive = false;
        isReturningHome = true;

        if (_panicAudioSource != null && _panicAudioSource.isPlaying)
        {
            _panicAudioSource.Stop();
        }

        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.velocity = Vector3.zero;
        }

        StartCoroutine(ReturnHomeRoutine());
    }

    private IEnumerator ReturnHomeRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        Vector3 doorOuter = new Vector3(-84.6f, 2.35f, 0.53f);
        Vector3 doorInner = new Vector3(-82.4f, 2.35f, 0.53f);

        // Step 1: Walk to hallway door threshold using NavAgent
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = false;
            _navAgent.speed = 1.8f;
            _navAgent.SetDestination(doorOuter);

            float navTimeout = 8f;
            while (_navAgent != null && _navAgent.enabled && Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(doorOuter.x, 0, doorOuter.z)) > 0.8f && navTimeout > 0f)
            {
                navTimeout -= Time.deltaTime;
                UpdateAnimator(1.0f, false);
                yield return null;
            }

            _navAgent.isStopped = true;
            _navAgent.enabled = false;
        }

        // Step 2: Step through doorway into Room 104
        yield return StartCoroutine(MoveToPoint(doorInner, 1.6f));

        // Step 3: Walk back to home desk
        yield return StartCoroutine(MoveToPoint(_homePosition, 1.5f, 0.2f));

        transform.position = _homePosition;
        transform.rotation = _homeRotation;
        isReturningHome = false;

        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.SitDown();
        }

        UpdateAnimator(0f, false);
    }
}
