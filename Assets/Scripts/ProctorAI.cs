using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls a wandering NPC student in the school hallways.
/// 
/// Behavior:
///   - WANDERING: Walks continuously between waypoints or random points along the hallway NavMesh.
///   - CHASING (Hostile Twist): Occasionally, when the player gets too close or crosses their
///     line of sight, the student snaps, sprints directly at the player, and triggers a
///     terrifying 3D face jumpscare using THIS exact NPC's model before resuming wandering!
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ProctorAI : MonoBehaviour
{
    public enum NPCState
    {
        Wandering,
        Chasing
    }

    [Header("Current State")]
    public NPCState currentState = NPCState.Wandering;
    public bool isTalking = false;
    public bool isChasing => currentState == NPCState.Chasing;

    [Header("Waypoints & Patrol Settings")]
    public Transform[] waypoints;
    public float waypointTolerance = 0.6f;
    public bool loopInOrder = true;
    public float minIdleTime = 2f;
    public float maxIdleTime = 4f;
    public bool enableProceduralWalk = false;
    public float walkBobFrequency = 8f;
    public float walkBobAmount = 0.03f;
    public float walkSwayAngle = 2f;
    public float idleBreatheRate = 2f;
    public float idleBreatheAmount = 0.01f;

    [Header("Footstep Audio")]
    public AudioClip footstepClip;
    public float footstepVolume = 0.20f;
    public float footstepMinDistance = 1.2f;
    public float footstepMaxDistance = 14.0f;

    [Header("Creepy Stare Settings")]
    public bool enableCreepyStare = true;
    public float stareChance = 0.85f;
    public float stareMaxDistance = 11.0f;
    public float maxHeadTurnAngle = 95.0f;
    public float headTurnSpeed = 4.0f;

    [Header("Movement Speeds")]
    [Tooltip("Normal casual walking speed in the hallway")]
    public float walkSpeed = 1.6f;

    [Tooltip("Fast sprint speed when chasing the player for a jumpscare")]
    public float chaseSpeed = 4.6f;

    [Header("Wander Settings")]
    [Tooltip("How far the NPC looks for a new point to walk to")]
    public float wanderRadius = 15f;

    [Tooltip("Maximum time spent walking to a single point before picking a new one")]
    public float maxWalkTime = 12f;

    [Header("Chase & Jumpscare Settings")]
    [Tooltip("How close the player can be in front of the NPC to potentially trigger a chase")]
    public float visionRange = 5.0f;

    [Tooltip("Close proximity range (even from the side) where the NPC senses the player")]
    public float proximityRange = 1.0f;

    [Tooltip("Field of view angle in degrees")]
    [Range(30f, 180f)]
    public float visionAngle = 100f;

    [Tooltip("Base chance (0.0 to 1.0) that the NPC decides to chase when spotting the player")]
    [Range(0f, 1f)]
    public float chaseChance = 0.12f;

    [Tooltip("How long the NPC waits before rolling again if they decided not to chase")]
    public float checkInterval = 5.0f;

    [Tooltip("Maximum duration of a chase before the NPC gives up and goes back to walking")]
    public float maxChaseDuration = 6.0f;

    [Tooltip("Distance at which the chasing NPC catches the player and triggers the jumpscare (forgiving radius)")]
    public float catchDistance = 0.65f;

    [Tooltip("Cooldown after a chase/scare before this NPC can chase again")]
    public float chaseCooldown = 25f;

    // ─── Internal State ───────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator _animator;
    private SkinnedMeshRenderer _smr;
    private Transform _player;
    private PlayerMovement _playerMovement;

    private float _walkTimer;
    private float _stuckTimer;
    private Vector3 _lastPosition;
    private int _currentWaypointIndex = 0;

    private float _nextChaseCheckTime;
    private float _chaseTimer;
    private float _cooldownUntilTime;

    private int _speedHash;
    private bool _hasSpeedParam;
    private static bool _globalChaseActive = false;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _smr = GetComponentInChildren<SkinnedMeshRenderer>();

        // Enforce forgiving wandering NPC radius & chase chance even if old values were serialized in the scene
        catchDistance = Mathf.Min(catchDistance, 0.68f);
        if (catchDistance <= 0.2f) catchDistance = 0.68f;
        proximityRange = Mathf.Min(proximityRange, 1.20f);
        if (proximityRange <= 0.2f) proximityRange = 1.20f;
        visionRange = Mathf.Min(visionRange, 5.0f);
        chaseChance = Mathf.Min(chaseChance, 0.12f);
        checkInterval = Mathf.Max(checkInterval, 5.0f);
        chaseCooldown = Mathf.Max(chaseCooldown, 25f);

        // Configure NavMeshAgent for smooth hallway walking
        _agent.speed = walkSpeed;
        _agent.angularSpeed = 300f;
        _agent.acceleration = 8f;
        _agent.stoppingDistance = 0.3f;
        _agent.autoBraking = false;

        // Avoid getting stuck on other NPCs
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.avoidancePriority = Random.Range(20, 80);

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _speedHash = Animator.StringToHash("Speed");
            foreach (var param in _animator.parameters)
            {
                if (param.nameHash == _speedHash && param.type == AnimatorControllerParameterType.Float)
                {
                    _hasSpeedParam = true;
                    break;
                }
            }
        }

        FindPlayer();

        // Stagger initial check times so all NPCs don't roll on the exact same frame
        _nextChaseCheckTime = Time.time + Random.Range(1.5f, 4.5f);

        // Immediately start walking
        PickNewWanderDestination();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
            _playerMovement = playerObj.GetComponent<PlayerMovement>();
        }
    }

    private void Update()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        // Immediately stop any chase and never jumpscare during or after Game Over
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            if (currentState == NPCState.Chasing)
            {
                StopChaseAndResumeWander();
            }
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            return;
        }

        if (_player == null) FindPlayer();

        if (isTalking)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            if (_player != null)
            {
                Vector3 dir = _player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                }
            }
            UpdateAnimation();
            return;
        }

        switch (currentState)
        {
            case NPCState.Wandering:
                UpdateWandering();
                if (!GameModeManager.IsFreeRoamMode)
                {
                    CheckForChaseTrigger();
                }
                break;

            case NPCState.Chasing:
                if (GameModeManager.IsFreeRoamMode)
                {
                    StopChaseAndResumeWander();
                }
                else
                {
                    UpdateChasing();
                }
                break;
        }

        UpdateAnimation();
    }

    public void SetTalkingState(bool talking)
    {
        isTalking = talking;
        if (talking)
        {
            if (currentState == NPCState.Chasing)
            {
                StopChaseAndResumeWander();
            }
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }
        else
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                PickNewWanderDestination();
            }
        }
    }

    public void StopChase()
    {
        StopChaseAndResumeWander();
    }

    // ─── Wandering Logic ──────────────────────────────────────────────

    private void UpdateWandering()
    {
        _walkTimer += Time.deltaTime;

        // Pick a new destination if we reached the current one or walked long enough
        if (!_agent.pathPending && _agent.remainingDistance <= 0.8f)
        {
            PickNewWanderDestination();
        }
        else if (_walkTimer >= maxWalkTime)
        {
            PickNewWanderDestination();
        }

        // Stuck detection: if barely moving, pick a new direction immediately
        if (Vector3.Distance(transform.position, _lastPosition) < 0.03f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 1.2f)
            {
                PickNewWanderDestination();
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        _lastPosition = transform.position;
    }

    private void PickNewWanderDestination()
    {
        _walkTimer = 0f;

        // If explicit waypoints are assigned, cycle through them
        if (waypoints != null && waypoints.Length > 0)
        {
            for (int a = 0; a < waypoints.Length; a++)
            {
                int idx = loopInOrder
                    ? ((_currentWaypointIndex + a) % waypoints.Length)
                    : Random.Range(0, waypoints.Length);
                if (waypoints[idx] != null)
                {
                    _currentWaypointIndex = (idx + 1) % waypoints.Length;
                    _agent.SetDestination(waypoints[idx].position);
                    return;
                }
            }
        }

        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(5f, wanderRadius);
            Vector3 randomPoint = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 4f, NavMesh.AllAreas))
            {
                // Keep NPC on the same floor
                if (Mathf.Abs(hit.position.y - transform.position.y) < 1.5f)
                {
                    if (Vector3.Distance(transform.position, hit.position) > 3f)
                    {
                        _agent.SetDestination(hit.position);
                        return;
                    }
                }
            }
        }
    }

    // ─── Chase & Jumpscare Logic ──────────────────────────────────────

    private void CheckForChaseTrigger()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (_player == null) return;
        if (_globalChaseActive) return; // Only one NPC chases at a time
        if (Time.time < _cooldownUntilTime) return;
        if (Time.time < _nextChaseCheckTime) return;

        // Don't start a chase if a jumpscare, dialogue, or pause menu is active
        if (PauseMenu.GameIsPaused) return;
        if (NPCJumpscareManager.Instance != null && NPCJumpscareManager.Instance.IsScaring) return;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return;

        // Must be on the same floor
        float yDiff = Mathf.Abs(_player.position.y - transform.position.y);
        if (yDiff > 1.8f) return;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distToPlayer > visionRange) return;

        _nextChaseCheckTime = Time.time + checkInterval;

        // Check if player is within proximity range OR inside vision cone with clear line of sight
        bool detected = false;

        if (distToPlayer <= proximityRange)
        {
            detected = HasLineOfSightToPlayer();
        }
        else
        {
            Vector3 dirToPlayer = (_player.position - transform.position);
            dirToPlayer.y = 0f;
            float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            if (angle <= visionAngle * 0.5f)
            {
                detected = HasLineOfSightToPlayer();
            }
        }

        if (!detected) return;

        // Calculate chase probability (small bonus if player is sprinting or right in their face)
        float effectiveChance = chaseChance;
        if (_playerMovement != null && _playerMovement.IsRunning)
            effectiveChance += 0.12f;
        if (distToPlayer <= 1.0f)
            effectiveChance += 0.15f;

        if (Random.value <= effectiveChance)
        {
            TriggerChase();
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (_player == null) return false;
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = _player.position + Vector3.up * 1.3f;
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;

        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == _player || hit.transform.IsChildOf(_player))
                return true;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Forces this NPC to immediately start chasing the player (or jumpscare if already touching).
    /// </summary>
    public void TriggerChase()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (currentState == NPCState.Chasing) return;

        // If already within catch distance, trigger the jumpscare with THIS NPC's face immediately!
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= catchDistance)
        {
            TriggerSpecificNPCJumpscare();
            return;
        }

        // Exit any open puzzle or modal immediately if this NPC starts chasing
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        currentState = NPCState.Chasing;
        _globalChaseActive = true;
        _chaseTimer = 0f;
        _agent.speed = chaseSpeed;
        _agent.acceleration = 18f;
        _agent.angularSpeed = 540f;

        if (_player != null)
        {
            _agent.SetDestination(_player.position);
        }
    }

    /// <summary>
    /// Called when the player physically bumps/runs into this NPC collider.
    /// Routes through TriggerChase so it respects catchDistance instead of forcing a 100% instant jumpscare from afar.
    /// </summary>
    public void TriggerImmediateBumpScare()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (Time.time < _cooldownUntilTime) return;
        if (NPCJumpscareManager.Instance != null && NPCJumpscareManager.Instance.IsScaring) return;

        if (_player != null && Vector3.Distance(transform.position, _player.position) <= catchDistance)
        {
            TriggerSpecificNPCJumpscare();
        }
    }

    private void UpdateChasing()
    {
        if (_player == null)
        {
            StopChaseAndResumeWander();
            return;
        }

        _chaseTimer += Time.deltaTime;

        // Continuously path directly toward the player
        _agent.SetDestination(_player.position);

        // Check planar distance & height difference
        float yDiff = Mathf.Abs(_player.position.y - transform.position.y);
        float dx = _player.position.x - transform.position.x;
        float dz = _player.position.z - transform.position.z;
        float planarDist = Mathf.Sqrt(dx * dx + dz * dz);

        // Exit any open puzzle/modal if the chasing NPC is about to jumpscare the player!
        if (planarDist <= catchDistance + 1.0f && yDiff < 1.8f && PauseMenu.IsAnyActivePuzzleOrModalOpen)
        {
            PauseMenu.ForceCloseAllModalsAndPuzzles();
        }

        // Catch check -> JUMPSCARE with THIS exact NPC's face!
        if (planarDist <= catchDistance && yDiff < 1.8f)
        {
            TriggerSpecificNPCJumpscare();
            return;
        }

        // Give up if chase lasted too long or player escaped to another floor / far away
        if (_chaseTimer >= maxChaseDuration || yDiff > 2.5f || planarDist > visionRange * 1.8f)
        {
            StopChaseAndResumeWander();
        }
    }

    private void TriggerSpecificNPCJumpscare()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            StopChaseAndResumeWander();
            return;
        }

        StopChaseAndResumeWander();

        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;

        // Force-close any open puzzle or modal before triggering the jumpscare
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        if (NPCJumpscareManager.Instance != null)
        {
            if (_smr == null) _smr = GetComponentInChildren<SkinnedMeshRenderer>();
            NPCJumpscareManager.Instance.TriggerJumpscare(_smr);
        }
    }

    public void StopChaseAndResumeWander()
    {
        if (currentState == NPCState.Chasing)
        {
            _globalChaseActive = false;
        }

        currentState = NPCState.Wandering;
        _cooldownUntilTime = Time.time + chaseCooldown;
        _nextChaseCheckTime = Time.time + checkInterval;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.speed = walkSpeed;
            _agent.acceleration = 8f;
            _agent.angularSpeed = 300f;
            PickNewWanderDestination();
        }
    }

    private void OnDisable()
    {
        if (currentState == NPCState.Chasing)
        {
            _globalChaseActive = false;
            currentState = NPCState.Wandering;
        }
    }

    // ─── Animation Sync ───────────────────────────────────────────────

    private void UpdateAnimation()
    {
        if (_animator == null) return;

        float currentSpeed = _agent.velocity.magnitude;

        if (_hasSpeedParam)
        {
            float animSpeed = 0f;
            if (currentSpeed > 0.1f)
            {
                animSpeed = (currentState == NPCState.Chasing) ? 2.0f : 1.0f;
            }
            _animator.SetFloat(_speedHash, animSpeed);
        }

        // Speed up the walk cycle when sprinting after the player
        if (currentState == NPCState.Chasing && currentSpeed > 0.2f)
        {
            _animator.speed = 1.6f;
        }
        else
        {
            _animator.speed = 1.0f;
        }
    }
}
