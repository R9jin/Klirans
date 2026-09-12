using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Standalone Proctor AI — pathfinding + animation sync + dialogue bridge.
///
/// HOW TO USE WITH ClearanceNPC:
///   In your ClearanceNPC.Interact(), call:
///       GetComponent<ProctorAI>()?.SetTalkingState(true);
///
///   When dialogue closes, call:
///       GetComponent<ProctorAI>()?.SetTalkingState(false);
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ProctorAI : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Waypoints")]
    [Tooltip("Waypoints the Proctor will patrol between across the facility.")]
    public Transform[] waypoints;

    [Tooltip("Arrival distance threshold.")]
    public float waypointTolerance = 0.6f;

    [Header("Patrol Behaviour")]
    public bool loopInOrder = true;
    public float minIdleTime = 2f;
    public float maxIdleTime = 4f;

    [Header("Movement")]
    public float walkSpeed = 1.4f;
    public string speedParam   = "Speed";
    public string talkingParam = "Talking";

    [Header("Procedural Motion")]
    public bool enableProceduralWalk = true;
    public float walkBobFrequency = 7.5f;
    public float walkBobAmount    = 0.04f;
    public float walkSwayAngle    = 3.5f;
    public float idleBreatheRate  = 2f;
    public float idleBreatheAmount = 0.012f;

    // ── Private runtime ────────────────────────────────────────────────────────

    private NavMeshAgent _agent;
    private Animator     _animator;
    private Transform    _modelChild;
    private Vector3      _modelInitialLocalPos;
    private Quaternion   _modelInitialLocalRot;
    private Transform    _playerTransform;

    private int  _waypointIndex = 0;
    private bool _isIdling      = false;
    private bool _isTalking     = false;
    private bool _patrolStarted = false;
    private bool _isTraversingLink = false;
    private float _bobTimer     = 0f;
    private Coroutine _idleCoroutine;

    private int _speedHash;
    private int _talkingHash;

    // Floor clamping: prevent falling through floors
    private float _lastValidY;
    private float _floorCheckInterval = 0.5f;
    private float _floorCheckTimer    = 0f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();

        _speedHash   = Animator.StringToHash(speedParam);
        _talkingHash = Animator.StringToHash(talkingParam);

        _agent.speed = walkSpeed;
        _agent.stoppingDistance = waypointTolerance;
        _agent.autoBraking = true;
        _agent.updateRotation = true;
        _agent.autoTraverseOffMeshLink = false;

        // Use procedural motion only if NO active Animator controller
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            enableProceduralWalk = true;
        }
        else
        {
            enableProceduralWalk = false;
        }

        var mr = GetComponentInChildren<MeshRenderer>();
        if (mr != null)
        {
            _modelChild = mr.transform;
            _modelInitialLocalPos = _modelChild.localPosition;
            _modelInitialLocalRot = _modelChild.localRotation;
        }
    }

    private void Start()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            enableProceduralWalk = false;
        }

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;

        _lastValidY = transform.position.y;

        StartCoroutine(InitNavMeshPatrol());
    }

    private IEnumerator InitNavMeshPatrol()
    {
        // Wait 1 frame so scene colliders and NavMesh are 100% active
        yield return null;

        // Force place agent on the NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            _agent.enabled = false;
            transform.position = hit.position;
            _agent.enabled = true;
            _agent.Warp(hit.position);
        }

        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[ProctorAI] '{name}' could not warp to NavMesh. Retrying...");
            yield return new WaitForSeconds(0.2f);
            if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
            {
                _agent.enabled = false;
                transform.position = hit.position;
                _agent.enabled = true;
                _agent.Warp(hit.position);
            }
        }

        if (!_agent.isOnNavMesh)
        {
            Debug.LogError($"[ProctorAI] '{name}' failed to place on NavMesh at {transform.position}.");
            yield break;
        }

        _lastValidY = transform.position.y;
        _patrolStarted = true;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[ProctorAI] '{name}' has no waypoints assigned.");
            yield break;
        }

        // If already at starting waypoint, immediately walk to next waypoint
        if (waypoints.Length > 1 && Vector3.Distance(transform.position, waypoints[0].position) < waypointTolerance * 2.5f)
        {
            _waypointIndex = 1;
        }

        GoToNextWaypoint();
    }

    private void Update()
    {
        if (!_patrolStarted || !_agent.isOnNavMesh) return;

        // If currently traversing stairs / off-mesh link, coroutine handles motion
        if (_isTraversingLink) return;

        // Check if we reached a stair NavMeshLink
        if (_agent.isOnOffMeshLink)
        {
            StartCoroutine(TraverseOffMeshLinkRoutine());
            return;
        }

        // Floor clamping: periodically check we haven't fallen through
        FloorClamp();

        // Dialogue state: smoothly face player
        if (_isTalking)
        {
            if (_playerTransform != null)
            {
                Vector3 lookDir = _playerTransform.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
                }
            }
            return;
        }

        SyncAnimator();
        HandleProceduralMotion();

        if (waypoints == null || waypoints.Length == 0) return;
        if (_isIdling || _agent.pathPending) return;

        // Check if agent path is invalid (stale/partial) — skip to next waypoint
        if (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"[ProctorAI] '{name}' invalid path, skipping to next waypoint.");
            AdvanceWaypointIndex();
            GoToNextWaypoint();
            return;
        }

        // Check arrival
        if (_agent.hasPath && _agent.remainingDistance <= waypointTolerance)
        {
            ArrivedAtWaypoint();
        }
        // Failsafe: if stuck for too long without a path, try next waypoint
        else if (!_agent.hasPath && !_agent.pathPending)
        {
            GoToNextWaypoint();
        }
    }

    /// <summary>
    /// Smoothly walks the NPC along the stairs NavMeshLink at normal walk speed,
    /// keeping footstep bobbing active and body facing along the flight.
    /// </summary>
    private IEnumerator TraverseOffMeshLinkRoutine()
    {
        _isTraversingLink = true;
        OffMeshLinkData linkData = _agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * _agent.baseOffset;

        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / Mathf.Max(walkSpeed, 0.5f);
        float elapsed = 0f;

        Vector3 flatDir = endPos - startPos;
        flatDir.y = 0;
        Quaternion targetRot = flatDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flatDir) : transform.rotation;

        if (_animator != null)
        {
            _animator.SetFloat(_speedHash, 1.0f);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Move along the stair flight incline
            transform.position = Vector3.Lerp(startPos, endPos, t);

            // Turn smoothly to face the direction of the stairs
            if (flatDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }

            // Keep procedural walking motion active during stair traversal
            if (enableProceduralWalk && _modelChild != null)
            {
                _bobTimer += Time.deltaTime * walkBobFrequency;
                float bob = Mathf.Abs(Mathf.Sin(_bobTimer)) * walkBobAmount;
                _modelChild.localPosition = _modelInitialLocalPos + Vector3.up * bob;

                float sway = Mathf.Sin(_bobTimer * 0.5f) * walkSwayAngle;
                _modelChild.localRotation = _modelInitialLocalRot * Quaternion.Euler(0, 0, sway);
            }

            yield return null;
        }

        transform.position = endPos;
        if (_agent.isOnOffMeshLink)
        {
            _agent.CompleteOffMeshLink();
        }
        _lastValidY = transform.position.y;
        _isTraversingLink = false;
    }

    private void FloorClamp()
    {
        if (_isTraversingLink) return;

        _floorCheckTimer += Time.deltaTime;
        if (_floorCheckTimer < _floorCheckInterval) return;
        _floorCheckTimer = 0f;

        // When on NavMesh or traversing an off-mesh link between floors, track current Y
        if (_agent.isOnOffMeshLink || _agent.isOnNavMesh)
        {
            _lastValidY = transform.position.y;
            return;
        }

        // Failsafe: if agent dropped below ground level (underneath floor)
        NavMeshHit hit;
        Vector3 targetPos = new Vector3(transform.position.x, Mathf.Max(_lastValidY, 2.43f), transform.position.z);
        if (NavMesh.SamplePosition(targetPos, out hit, 4.0f, NavMesh.AllAreas))
        {
            _agent.enabled = false;
            transform.position = hit.position;
            _agent.enabled = true;
            _agent.Warp(hit.position);
            Debug.Log($"[ProctorAI] '{name}' re-clamped to NavMesh at {hit.position}");
        }
    }

    // ── Public API (ClearanceNPC Dialogue Bridge) ─────────────────────────────

    public void SetTalkingState(bool isTalking)
    {
        _isTalking = isTalking;

        if (isTalking)
        {
            if (_idleCoroutine != null)
            {
                StopCoroutine(_idleCoroutine);
                _idleCoroutine = null;
            }

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity  = Vector3.zero;
            }

            if (_animator != null)
            {
                _animator.SetFloat(_speedHash,   0f);
                _animator.SetBool (_talkingHash, true);
            }

            if (_modelChild != null)
            {
                _modelChild.localPosition = _modelInitialLocalPos;
                _modelChild.localRotation = _modelInitialLocalRot;
            }
        }
        else
        {
            if (_animator != null)
            {
                _animator.SetBool(_talkingHash, false);
            }

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _isIdling        = false;
                GoToNextWaypoint();
            }
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void SyncAnimator()
    {
        if (_animator == null) return;

        bool isMoving = !_agent.isStopped && _agent.isOnNavMesh && _agent.hasPath && (_agent.remainingDistance > waypointTolerance);

        if (!isMoving)
        {
            _animator.SetFloat(_speedHash, 0f, 0.1f, Time.deltaTime);
        }
        else
        {
            float speed = Mathf.Max(_agent.velocity.magnitude, _agent.desiredVelocity.magnitude);
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(walkSpeed, 0.01f));
            if (normalizedSpeed > 0.15f)
            {
                normalizedSpeed = Mathf.Max(normalizedSpeed, 0.85f);
            }
            _animator.SetFloat(_speedHash, normalizedSpeed, 0.08f, Time.deltaTime);
        }
    }

    private void HandleProceduralMotion()
    {
        if (!enableProceduralWalk || _modelChild == null) return;

        float currentSpeed = _agent.velocity.magnitude;
        if (currentSpeed > 0.15f && !_agent.isStopped)
        {
            // Dynamic walk motion: vertical bobbing + lateral stride sway
            _bobTimer += Time.deltaTime * walkBobFrequency * (currentSpeed / walkSpeed);

            float bob = Mathf.Abs(Mathf.Sin(_bobTimer)) * walkBobAmount;
            _modelChild.localPosition = _modelInitialLocalPos + Vector3.up * bob;

            float sway = Mathf.Sin(_bobTimer * 0.5f) * walkSwayAngle;
            _modelChild.localRotation = _modelInitialLocalRot * Quaternion.Euler(0, 0, sway);
        }
        else
        {
            // Idle breathing motion
            float breathe = Mathf.Sin(Time.time * idleBreatheRate) * idleBreatheAmount;
            _modelChild.localPosition = Vector3.Lerp(_modelChild.localPosition, _modelInitialLocalPos + Vector3.up * breathe, Time.deltaTime * 4f);
            _modelChild.localRotation = Quaternion.Slerp(_modelChild.localRotation, _modelInitialLocalRot, Time.deltaTime * 4f);
        }
    }

    private void ArrivedAtWaypoint()
    {
        _isIdling = true;
        if (_agent.isOnNavMesh) _agent.isStopped = true;

        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
        _idleCoroutine = StartCoroutine(IdleRoutine());
    }

    private IEnumerator IdleRoutine()
    {
        float waitTime = Random.Range(minIdleTime, maxIdleTime);
        yield return new WaitForSeconds(waitTime);

        _isIdling = false;
        if (_agent.isOnNavMesh) _agent.isStopped = false;
        AdvanceWaypointIndex();
        GoToNextWaypoint();
    }

    private void AdvanceWaypointIndex()
    {
        if (loopInOrder)
        {
            _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
        }
        else
        {
            int next = _waypointIndex;
            while (waypoints.Length > 1 && next == _waypointIndex)
                next = Random.Range(0, waypoints.Length);
            _waypointIndex = next;
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (!_agent.isOnNavMesh) return;

        // Validate the waypoint can be reached on the NavMesh
        NavMeshPath path = new NavMeshPath();
        Vector3 targetPos = waypoints[_waypointIndex].position;

        // Use SamplePosition to snap waypoint to nearest NavMesh point
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 2.0f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }

        // Only set destination if a complete path exists
        if (_agent.CalculatePath(targetPos, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            _agent.SetDestination(targetPos);
        }
        else
        {
            // Skip this waypoint if path is partial or invalid
            AdvanceWaypointIndex();
            // Prevent infinite recursion with a simple counter
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(waypoints[i].position, next.position);
        }
    }
#endif
}
