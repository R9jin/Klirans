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
    public float minIdleTime = 0.5f;
    public float maxIdleTime = 1.5f;

    [Header("Movement")]
    public float walkSpeed = 1.40f;
    public string speedParam   = "Speed";
    public string talkingParam = "Talking";

    [Header("Footstep Audio")]
    [Tooltip("Enable or disable footstep sounds for wandering student NPCs.")]
    public bool enableFootsteps = false;
    [Tooltip("Audio clip for footsteps (softer/lighter atmospheric sound).")]
    public AudioClip footstepClip;
    [Range(0f, 1f)]
    [Tooltip("Lighter/softer volume for NPC footsteps.")]
    public float footstepVolume = 0.0f;
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

    [Header("Horror Vision & Aggressive Chase")]
    [Tooltip("If true, wandering students can spot the player with vision cone or proximity and chase them with an eerie audio sting.")]
    public bool enableStudentChase = true;
    [Tooltip("Vision cone angle (degrees from forward).")]
    public float visionAngle = 70.0f;
    [Tooltip("Vision cone maximum range (meters).")]
    public float visionRange = 7.5f;
    [Tooltip("Proximity detection radius (meters) where NPC senses player even behind/around corners.")]
    public float proximityRange = 2.8f;
    [Tooltip("Chase speed when student runs after the player.")]
    public float chaseSpeed = 3.6f;
    [Tooltip("Maximum duration of a chase before giving up and returning to patrol.")]
    public float maxChaseDuration = 4.2f;
    [Tooltip("Distance at which the chasing student catches the player and triggers jumpscare.")]
    public float catchDistance = 1.15f;
    [Range(0f, 1f)]
    [Tooltip("Base probability of triggering a chase when player is detected in vision/proximity.")]
    public float chaseChance = 0.20f;
    [Tooltip("Cooldown after a chase before this student can chase again.")]
    public float chaseCooldown = 25.0f;
    [Tooltip("Audio clip played when chase begins (sudden horror sting).")]
    public AudioClip chaseStingClip;

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

    public bool IsIdling => _isIdling;
    public bool IsTalking => _isTalking;
    private bool _patrolStarted = false;
    private bool _isTraversingLink = false;
    private float _bobTimer     = 0f;
    private Coroutine _idleCoroutine;

    // Creepy stare runtime
    private bool  _isStaringAtPlayer    = false;
    private float _stareTimer           = 0f;
    private float _stareCooldownTimer   = 0f;
    private float _currentStareWeight   = 0f;

    // Horror chase runtime
    private bool  _isChasing            = false;
    private float _chaseTimer           = 0f;
    private float _chaseCooldownTimer   = 0f;
    private float _detectionTimer       = 0f;
    private AudioSource _chaseAudio;
    private static float _globalChaseCooldownUntil = 0f;
    public bool IsChasing => _isChasing;

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

        _chaseAudio = gameObject.AddComponent<AudioSource>();
        _chaseAudio.playOnAwake = false;
        _chaseAudio.spatialBlend = 1.0f;
        _chaseAudio.minDistance = 2.0f;
        _chaseAudio.maxDistance = 18.0f;
        _chaseAudio.rolloffMode = AudioRolloffMode.Linear;

        if (chaseStingClip == null)
        {
#if UNITY_EDITOR
            chaseStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
#endif
        }

        GameSettingsManager.EnforceLobbyRoundTableCollision();
    }

    private static Transform _cachedLobbyRoundTable;
    private static Vector3 _lobbyTableCenter = new Vector3(-84.50f, 2.35f, 16.65f);
    private const float LobbyTableKeepOutRadius = 1.38f;

    private static void RefreshLobbyTableCache()
    {
        if (_cachedLobbyRoundTable == null)
        {
            var go = GameObject.Find("Lobby_RoundTable");
            if (go != null)
            {
                _cachedLobbyRoundTable = go.transform;
                var r = go.GetComponentInChildren<Renderer>();
                _lobbyTableCenter = r != null ? r.bounds.center : go.transform.position;
            }
        }
    }

    private void EnforceRoundTableKeepOut()
    {
        RefreshLobbyTableCache();
        if (_cachedLobbyRoundTable == null) return;

        // Only applies on 1F (y between 1.5 and 4.2)
        if (Mathf.Abs(transform.position.y - _lobbyTableCenter.y) > 1.8f) return;

        Vector3 offset = transform.position - _lobbyTableCenter;
        offset.y = 0f;
        float dist = offset.magnitude;
        if (dist < LobbyTableKeepOutRadius)
        {
            Vector3 pushDir = dist > 0.01f ? (offset / dist) : Vector3.forward;
            // Steer tangentially + radially outward around the round table so the NPC walks around it smoothly
            Vector3 tangent = new Vector3(-pushDir.z, 0f, pushDir.x);
            if (_agent != null && _agent.hasPath)
            {
                Vector3 toTarget = _agent.steeringTarget - transform.position;
                toTarget.y = 0f;
                if (Vector3.Dot(tangent, toTarget) < 0f) tangent = -tangent;
            }

            Vector3 correctedPos = new Vector3(
                _lobbyTableCenter.x + pushDir.x * LobbyTableKeepOutRadius,
                transform.position.y,
                _lobbyTableCenter.z + pushDir.z * LobbyTableKeepOutRadius
            ) + tangent * (walkSpeed * Time.deltaTime * 0.85f);

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Warp(correctedPos);
                if (waypoints != null && _waypointIndex >= 0 && _waypointIndex < waypoints.Length && waypoints[_waypointIndex] != null)
                {
                    _agent.SetDestination(waypoints[_waypointIndex].position);
                }
            }
            else
            {
                transform.position = correctedPos;
            }
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
        GameSettingsManager.EnforceLobbyRoundTableCollision();
        RefreshLobbyTableCache();

        // Push any waypoints that lie inside Lobby_RoundTable's footprint outside its perimeter
        if (_cachedLobbyRoundTable != null && waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                if (Mathf.Abs(waypoints[i].position.y - _lobbyTableCenter.y) < 1.8f)
                {
                    Vector3 wpDiff = waypoints[i].position - _lobbyTableCenter;
                    wpDiff.y = 0f;
                    if (wpDiff.magnitude < LobbyTableKeepOutRadius + 0.25f)
                    {
                        Vector3 dir = wpDiff.sqrMagnitude > 0.001f ? wpDiff.normalized : Vector3.forward;
                        waypoints[i].position = new Vector3(
                            _lobbyTableCenter.x + dir.x * (LobbyTableKeepOutRadius + 0.30f),
                            waypoints[i].position.y,
                            _lobbyTableCenter.z + dir.z * (LobbyTableKeepOutRadius + 0.30f)
                        );
                    }
                }
            }
        }

        // Wait 2 frames so scene colliders and NavMeshObstacle carving are 100% active
        yield return null;
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
    private float _pathRetryTimer       = 0f;
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

        // Floor clamping & Lobby_RoundTable keep-out
        FloorClamp();
        EnforceRoundTableKeepOut();

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

        // Horror chase state
        if (_isChasing)
        {
            UpdateChase();
            SyncAnimator();
            HandleProceduralMotion();
            UpdateFootstepAudio();
            return;
        }

        // Periodic detection check
        _chaseCooldownTimer -= Time.deltaTime;
        _detectionTimer -= Time.deltaTime;
        if (enableStudentChase && _chaseCooldownTimer <= 0f && Time.time >= _globalChaseCooldownUntil && _detectionTimer <= 0f)
        {
            _detectionTimer = 0.35f;
            CheckVisionAndProximityDetection();
        }

        SyncAnimator();
        HandleProceduralMotion();
        UpdateFootstepAudio();
        UpdateCreepyStareTimer();

        if (waypoints == null || waypoints.Length == 0) return;
        if (_isIdling) return;

        // Check arrival using flat 2D distance to current waypoint
        bool arrived = false;
        if (waypoints[_waypointIndex] != null)
        {
            Vector3 targetWP = waypoints[_waypointIndex].position;
            Vector3 flatDiff = targetWP - transform.position;
            flatDiff.y = 0f;
            float sqrDist = flatDiff.sqrMagnitude;
            if (sqrDist <= waypointTolerance * waypointTolerance)
            {
                arrived = true;
            }
            else if (_agent.hasPath && !_agent.pathPending && _agent.remainingDistance <= waypointTolerance && sqrDist <= (waypointTolerance * 2.5f) * (waypointTolerance * 2.5f))
            {
                arrived = true;
            }
        }

        if (arrived)
        {
            _stuckTimer = 0f;
            _pathRetryTimer = 0f;
            ArrivedAtWaypoint();
            return;
        }

        // Anti-orbit / stuck watchdog: if agent spends too long without reaching waypoint, advance
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer >= 9.0f)
        {
            _stuckTimer = 0f;
            _pathRetryTimer = 0f;
            AdvanceWaypointIndex();
            GoToNextWaypoint();
            return;
        }

        // Failsafe: if path became invalid or empty, re-issue destination with debounce
        if (!_agent.hasPath && !_agent.pathPending)
        {
            _pathRetryTimer += Time.deltaTime;
            if (_pathRetryTimer >= 1.0f)
            {
                _pathRetryTimer = 0f;
                GoToNextWaypoint();
            }
        }
        else
        {
            _pathRetryTimer = 0f;
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

    // ── Horror Vision & Aggressive Chase Implementation ──────────────────────

    public void StartChase()
    {
        if (_isChasing || _isTalking || _playerTransform == null) return;
        if (!_agent.isOnNavMesh) return;

        _isChasing = true;
        _chaseTimer = 0f;
        _chaseCooldownTimer = chaseCooldown;
        _globalChaseCooldownUntil = Time.time + 15.0f;

        if (_idleCoroutine != null)
        {
            StopCoroutine(_idleCoroutine);
            _idleCoroutine = null;
        }
        _isIdling = false;

        _agent.isStopped = false;
        _agent.speed = chaseSpeed;
        _agent.acceleration = 20.0f;
        _agent.SetDestination(_playerTransform.position);

        if (chaseStingClip != null)
        {
            if (_chaseAudio != null && _chaseAudio.enabled && _chaseAudio.gameObject.activeInHierarchy)
            {
                _chaseAudio.PlayOneShot(chaseStingClip, 0.85f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(chaseStingClip, transform.position, 0.85f);
            }
        }

        Debug.Log($"[ProctorAI] '{name}' SPOTTED PLAYER! Sprinting and chasing!");
    }

    public void EndChase()
    {
        _isChasing = false;
        _chaseTimer = 0f;
        _chaseCooldownTimer = chaseCooldown;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.speed = walkSpeed;
            _agent.acceleration = 12.0f;
            GoToNextWaypoint();
        }
    }

    private void UpdateChase()
    {
        if (_playerTransform == null)
        {
            EndChase();
            return;
        }

        if (LockerHideManager.IsPlayerHidden || OpeningCutsceneDirector.IsAnyCutscenePlaying ||
            (NPCJumpscareManager.Instance != null && !NPCJumpscareManager.Instance.CanTriggerScare()))
        {
            EndChase();
            return;
        }

        _chaseTimer += Time.deltaTime;

        Vector3 playerPos = _playerTransform.position;
        Vector3 diff = playerPos - transform.position;
        diff.y = 0f;
        float dist = diff.magnitude;

        // Catch player -> trigger jumpscare!
        if (dist <= catchDistance)
        {
            Debug.Log($"[ProctorAI] '{name}' CAUGHT PLAYER! Triggering jumpscare!");
            EndChase();
            if (NPCJumpscareManager.Instance != null)
            {
                NPCJumpscareManager.Instance.TriggerSpecificNPCJumpscare(gameObject);
            }
            return;
        }

        // Lost player / timed out / outrun
        if (_chaseTimer >= maxChaseDuration || dist > 11.5f || Mathf.Abs(transform.position.y - playerPos.y) > 2.0f)
        {
            Debug.Log($"[ProctorAI] '{name}' lost sight of player or chase timed out. Returning to patrol.");
            EndChase();
            return;
        }

        // Continue running towards player
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.SetDestination(playerPos);
        }
    }

    public void TriggerImmediateBumpScare()
    {
        if (_isTalking || _isChasing) return;
        if (NPCJumpscareManager.Instance == null || !NPCJumpscareManager.Instance.CanTriggerScare()) return;

        Debug.Log($"[ProctorAI] '{name}' physically bumped by player! Triggering immediate aggro jumpscare!");
        _globalChaseCooldownUntil = Time.time + 15.0f;
        _chaseCooldownTimer = chaseCooldown;

        NPCJumpscareManager.Instance.TriggerSpecificNPCJumpscare(gameObject);
    }

    private void CheckVisionAndProximityDetection()
    {
        if (_playerTransform == null || _isTalking || _isChasing || !_agent.isOnNavMesh) return;
        if (LockerHideManager.IsPlayerHidden) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (NPCJumpscareManager.Instance != null && !NPCJumpscareManager.Instance.CanTriggerScare()) return;

        // Verify same floor level
        if (Mathf.Abs(transform.position.y - _playerTransform.position.y) > 1.8f) return;

        Vector3 toPlayer = _playerTransform.position - transform.position;
        Vector3 toPlayerFlat = toPlayer;
        toPlayerFlat.y = 0f;
        float dist = toPlayerFlat.magnitude;

        if (dist > visionRange) return;

        bool inVisionCone = false;
        if (dist <= visionRange)
        {
            float angle = Vector3.Angle(transform.forward, toPlayerFlat);
            if (angle <= visionAngle)
            {
                inVisionCone = true;
            }
        }

        bool inProximity = (dist <= proximityRange);

        if (!inVisionCone && !inProximity) return;

        // Line-of-sight check: ensure no solid walls or closed doors block vision
        Vector3 eyePos = (_headBone != null) ? _headBone.position : (transform.position + Vector3.up * 1.5f);
        Vector3 playerChest = _playerTransform.position + Vector3.up * 1.2f;

        RaycastHit hit;
        int mask = LayerMask.GetMask("Default", "Environment", "Walls", "Props");
        if (mask == 0) mask = ~0;
        if (Physics.Linecast(eyePos, playerChest, out hit, mask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.CompareTag("Player") && !hit.transform.IsChildOf(_playerTransform))
            {
                return;
            }
        }

        // Calculate chance based on player state
        float effectiveChance = chaseChance;
        var pm = _playerTransform.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            if (pm.IsRunning) effectiveChance += 0.35f;
            if (pm.IsCrouching) effectiveChance -= 0.12f;
        }

        if (dist <= 1.8f) effectiveChance += 0.40f;

        if (Random.value <= effectiveChance)
        {
            StartChase();
        }
    }

    // ── Public API (ClearanceNPC Dialogue Bridge) ─────────────────────────────

    public void SetTalkingState(bool isTalking)
    {
        _isTalking = isTalking;

        if (isTalking)
        {
            if (_isChasing) EndChase();

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

        if (_isChasing)
        {
            _animator.SetFloat(_speedHash, 1.85f, 0.08f, Time.deltaTime);
            return;
        }

        bool isMoving = !_isTalking && !_isIdling && _agent != null && _agent.isOnNavMesh && !_agent.isStopped && (_agent.velocity.magnitude > 0.12f || _agent.desiredVelocity.magnitude > 0.12f);

        if (!isMoving)
        {
            _animator.SetFloat(_speedHash, 0f, 0.12f, Time.deltaTime);
        }
        else
        {
            float speed = Mathf.Max(_agent.velocity.magnitude, _agent.desiredVelocity.magnitude);
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(walkSpeed, 0.01f));
            if (normalizedSpeed > 0.10f)
            {
                normalizedSpeed = Mathf.Max(normalizedSpeed, 0.88f);
            }
            _animator.SetFloat(_speedHash, normalizedSpeed, 0.08f, Time.deltaTime);
        }
    }

    private void HandleProceduralMotion()
    {
        if (!enableProceduralWalk || _modelChild == null) return;

        if (_isChasing)
        {
            _bobTimer += Time.deltaTime * walkBobFrequency * 1.6f;
            float chaseBob = Mathf.Abs(Mathf.Sin(_bobTimer)) * (walkBobAmount * 1.35f);
            _modelChild.localPosition = _modelInitialLocalPos + Vector3.up * chaseBob;
            float chaseSway = Mathf.Sin(_bobTimer * 0.5f) * (walkSwayAngle * 1.35f);
            _modelChild.localRotation = _modelInitialLocalRot * Quaternion.Euler(0, 0, chaseSway);
            return;
        }

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
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

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

        if (_waypointIndex < 0 || _waypointIndex >= waypoints.Length || waypoints[_waypointIndex] == null)
        {
            AdvanceWaypointIndex();
        }

        if (waypoints[_waypointIndex] == null) return;

        Vector3 targetPos = waypoints[_waypointIndex].position;

        // Snap waypoint to nearest NavMesh point
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 3.0f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }

        _agent.isStopped = false;
        _agent.ResetPath();
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
        if (!enableFootsteps)
        {
            _footstepAudio = GetComponent<AudioSource>();
            if (_footstepAudio != null)
            {
                _footstepAudio.Stop();
                _footstepAudio.clip = null;
                _footstepAudio.enabled = false;
            }
            return;
        }

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
        if (!enableFootsteps || _footstepAudio == null || _footstepAudio.clip == null) return;

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

        if (_isTalking || _isChasing)
        {
            _isStaringAtPlayer = false;
            return;
        }

        // If the player is hiding in a locker, the Proctor loses sight of them completely
        if (LockerHideManager.IsPlayerHidden)
        {
            _isStaringAtPlayer  = false;
            _currentStareWeight = 0f;
            _stareCooldownTimer = Random.Range(minStareCooldown, maxStareCooldown);
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
