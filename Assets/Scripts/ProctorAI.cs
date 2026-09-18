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
    public float waypointTolerance = 1.1f;

    [Header("Patrol Behaviour")]
    public bool loopInOrder = true;
    public float minIdleTime = 2f;
    public float maxIdleTime = 4f;

    [Header("Movement")]
    public float walkSpeed = 1.15f;
    public string speedParam   = "Speed";
    public string talkingParam = "Talking";

    [Header("Footstep Audio")]
    [Tooltip("Audio clip for footsteps (softer/lighter atmospheric sound).")]
    public AudioClip footstepClip;
    [Range(0f, 1f)]
    [Tooltip("Lighter/softer volume for NPC footsteps.")]
    public float footstepVolume = 0.20f;
    public float footstepMinDistance = 1.2f;
    public float footstepMaxDistance = 14.0f;

    [Header("Creepy Head Tracking")]
    [Tooltip("If true, the NPC will randomly and frequently lock their head to stare at the player, even while their body continues walking another way.")]
    public bool enableCreepyStare = true;
    [Tooltip("Maximum distance from player to initiate a stare.")]
    public float stareMaxDistance = 11.0f;
    [Tooltip("Maximum head turn angle away from body forward (degrees).")]
    public float maxHeadTurnAngle = 95.0f;
    [Range(0f, 1f)]
    [Tooltip("Probability of triggering a stare when player is in range.")]
    public float stareChance = 0.85f;
    public float minStareDuration = 3.5f;
    public float maxStareDuration = 6.5f;
    public float minStareCooldown = 1.0f;
    public float maxStareCooldown = 3.0f;
    public float headTurnSpeed = 4.0f;

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
    private AudioSource  _footstepAudio;
    private Transform    _headBone;
    private Transform    _neckBone;
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

    // Creepy stare runtime
    private bool  _isStaringAtPlayer    = false;
    private float _stareTimer           = 0f;
    private float _stareCooldownTimer   = 0f;
    private float _currentStareWeight   = 0f;

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
        _agent.stoppingDistance = 0.25f;
        _agent.angularSpeed = 480.0f;
        _agent.acceleration = 12.0f;
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

        InitFootstepAudio();
        FindBones();
        _stareCooldownTimer = Random.Range(0.5f, 2.0f);

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

    private float _stuckTimer           = 0f;
    private Vector3 _lastSampledPos;

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
            UpdateFootstepAudio();
            return;
        }

        SyncAnimator();
        HandleProceduralMotion();
        UpdateFootstepAudio();
        UpdateCreepyStareTimer();

        if (waypoints == null || waypoints.Length == 0) return;
        if (_isIdling) return;

        // Check arrival using both 2D distance and agent remainingDistance
        bool arrived = false;
        if (waypoints[_waypointIndex] != null)
        {
            Vector3 targetWP = waypoints[_waypointIndex].position;
            Vector3 flatDiff = targetWP - transform.position;
            flatDiff.y = 0f;
            if (flatDiff.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                arrived = true;
            }
        }

        if (!arrived && _agent.hasPath && !_agent.pathPending)
        {
            if (_agent.remainingDistance <= waypointTolerance)
            {
                arrived = true;
            }
        }

        if (arrived)
        {
            _stuckTimer = 0f;
            ArrivedAtWaypoint();
            return;
        }

        // Anti-orbit / stuck watchdog: if agent spends too long without reaching waypoint, advance
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer >= 9.0f)
        {
            _stuckTimer = 0f;
            AdvanceWaypointIndex();
            GoToNextWaypoint();
            return;
        }

        // Failsafe: if path became invalid or empty, set destination
        if (!_agent.hasPath && !_agent.pathPending)
        {
            GoToNextWaypoint();
        }
    }

    private void LateUpdate()
    {
        UpdateCreepyHeadTracking();
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

        bool isMoving = !_agent.isStopped && _agent.isOnNavMesh && _agent.hasPath && (_agent.remainingDistance > 0.15f);

        if (!isMoving)
        {
            _animator.SetFloat(_speedHash, 0f, 0.15f, Time.deltaTime);
        }
        else
        {
            float actualSpeed = _agent.velocity.magnitude;
            // If physically blocked, return to idle
            if (actualSpeed < 0.10f)
            {
                _animator.SetFloat(_speedHash, 0f, 0.15f, Time.deltaTime);
                return;
            }

            // Smooth linear mapping of actual ground movement to walk animation
            float normalizedSpeed = Mathf.Clamp01(actualSpeed / Mathf.Max(walkSpeed, 0.01f));
            _animator.SetFloat(_speedHash, normalizedSpeed, 0.12f, Time.deltaTime);
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

        Vector3 targetPos = waypoints[_waypointIndex].position;

        // Snap waypoint to nearest NavMesh point
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 3.0f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }

        _agent.SetDestination(targetPos);
    }

    // ── Footstep Audio & Creepy Stare Implementation ──────────────────────────

    private void FindBones()
    {
        _headBone = FindBoneRecursive(transform, "mixamorig:Head");
        _neckBone = FindBoneRecursive(transform, "mixamorig:Neck");

        if (_headBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().Contains("head") && _headBone == null) _headBone = t;
                if (t.name.ToLower().Contains("neck") && _neckBone == null) _neckBone = t;
            }
        }
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindBoneRecursive(parent.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    private void InitFootstepAudio()
    {
        _footstepAudio = GetComponent<AudioSource>();
        if (_footstepAudio == null) _footstepAudio = gameObject.AddComponent<AudioSource>();

        if (footstepClip == null)
        {
#if UNITY_EDITOR
            footstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
#endif
        }

        if (footstepClip != null)
        {
            _footstepAudio.clip = footstepClip;
            _footstepAudio.spatialBlend = 1.0f; // 100% 3D spatialization
            _footstepAudio.rolloffMode = AudioRolloffMode.Linear;
            _footstepAudio.minDistance = footstepMinDistance;
            _footstepAudio.maxDistance = footstepMaxDistance;
            _footstepAudio.loop = true;
            _footstepAudio.playOnAwake = false;
            _footstepAudio.dopplerLevel = 0f;
            _footstepAudio.pitch = Random.Range(0.94f, 1.06f);
            _footstepAudio.time = Random.Range(0f, Mathf.Min(35f, footstepClip.length));
            _footstepAudio.volume = 0f;
        }
    }

    private void UpdateFootstepAudio()
    {
        if (_footstepAudio == null || _footstepAudio.clip == null) return;

        bool isMoving = !_isTalking && _agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.15f;
        float targetVol = isMoving ? footstepVolume : 0f;

        _footstepAudio.volume = Mathf.MoveTowards(_footstepAudio.volume, targetVol, Time.deltaTime * 2.0f);

        if (_footstepAudio.volume > 0.005f)
        {
            if (!_footstepAudio.isPlaying) _footstepAudio.Play();
        }
        else if (_footstepAudio.isPlaying && targetVol == 0f)
        {
            _footstepAudio.Pause();
        }
    }

    private void UpdateCreepyStareTimer()
    {
        if (!enableCreepyStare || _headBone == null)
        {
            _isStaringAtPlayer = false;
            return;
        }

        if (_playerTransform == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;
            if (_playerTransform == null) return;
        }

        if (_isTalking)
        {
            _isStaringAtPlayer = false;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

        if (_isStaringAtPlayer)
        {
            _stareTimer -= Time.deltaTime;

            Vector3 toPlayer = (_playerTransform.position + Vector3.up * 1.5f - _headBone.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, toPlayer);

            // Cancel stare if time expired, player got too far, or player walked behind NPC back
            if (_stareTimer <= 0f || distToPlayer > stareMaxDistance * 1.25f || angleToPlayer > maxHeadTurnAngle + 20f)
            {
                _isStaringAtPlayer = false;
                _stareCooldownTimer = Random.Range(minStareCooldown, maxStareCooldown);
            }
        }
        else
        {
            _stareCooldownTimer -= Time.deltaTime;

            if (_stareCooldownTimer <= 0f && distToPlayer <= stareMaxDistance)
            {
                Vector3 toPlayer = (_playerTransform.position + Vector3.up * 1.5f - _headBone.position).normalized;
                float angleToPlayer = Vector3.Angle(transform.forward, toPlayer);

                if (angleToPlayer <= maxHeadTurnAngle + 10f)
                {
                    if (Random.value <= stareChance)
                    {
                        _isStaringAtPlayer = true;
                        _stareTimer = Random.Range(minStareDuration, maxStareDuration);
                    }
                    else
                    {
                        _stareCooldownTimer = Random.Range(minStareCooldown, maxStareCooldown);
                    }
                }
                else
                {
                    _stareCooldownTimer = 0.5f;
                }
            }
        }
    }

    private void UpdateCreepyHeadTracking()
    {
        float targetWeight = (_isStaringAtPlayer && !_isTalking) ? 1.0f : 0.0f;
        _currentStareWeight = Mathf.MoveTowards(_currentStareWeight, targetWeight, Time.deltaTime * headTurnSpeed);

        if (_currentStareWeight <= 0.001f || _headBone == null || _playerTransform == null) return;

        Vector3 targetEyePos = _playerTransform.position + Vector3.up * 1.5f;
        Vector3 toPlayer = (targetEyePos - _headBone.position).normalized;

        // Clamp rotation within maxHeadTurnAngle of NPC forward direction
        Vector3 clampedDir = Vector3.RotateTowards(transform.forward, toPlayer, maxHeadTurnAngle * Mathf.Deg2Rad, 1.0f);
        Quaternion targetRot = Quaternion.LookRotation(clampedDir, Vector3.up);

        if (_neckBone != null)
        {
            _neckBone.rotation = Quaternion.Slerp(_neckBone.rotation, targetRot, _currentStareWeight * 0.25f);
        }
        _headBone.rotation = Quaternion.Slerp(_headBone.rotation, targetRot, _currentStareWeight);
    }

    private void OnDisable()
    {
        if (_footstepAudio != null && _footstepAudio.isPlaying)
        {
            _footstepAudio.Pause();
        }
        _isStaringAtPlayer = false;
        _currentStareWeight = 0f;
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
