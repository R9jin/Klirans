using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// TheProctorAI — The primary horror entity during campus blackouts.
///
/// Lore & Mechanics:
/// - Towering 2.5-meter figure in faded polo & slacks, pale blank face, toothless dark mouth.
/// - Mindless enforcer of silence with a dripping clipboard.
/// - Blind, but drawn to vibrations, sprinting, heavy breathing, and flashlight.
/// - Uncanny, creepy lurching motion (spine hunched, blind head twitches, dragging steps).
/// - Confined strictly to its assigned floor hallway; CANNOT enter any rooms or cross stairs.
/// - Blocked from catching the player when hidden in lockers (LockerHideManager.IsPlayerHidden).
/// - Catching player triggers an in-your-face jumpscare, whiteout, +10 anxiety, and safe-zone reset.
/// - Despawns immediately when blackout ends (either via timer or catch).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TheProctorAI : MonoBehaviour
{
    public static TheProctorAI ActiveProctor { get; private set; }

    public enum ProctorState
    {
        Inactive,
        Spawning,
        Searching,
        Chasing,
        Jumpscare,
        Despawning
    }

    [Header("Current State")]
    public ProctorState currentState = ProctorState.Inactive;

    [Header("Movement & Balancing")]
    [Tooltip("Fast stalking walk speed in hallway.")]
    public float patrolSpeed = 3.2f;

    [Tooltip("Terrifying pursuit speed when alerted or chasing player.")]
    public float chaseSpeed = 5.2f;

    [Tooltip("Distance at which Proctor catches the player and triggers jumpscare.")]
    public float catchDistance = 1.4f;

    [Tooltip("NavMesh stopping distance.")]
    public float stoppingDistance = 0.5f;

    [Tooltip("NavMesh agent acceleration.")]
    public float acceleration = 12.0f;

    [Tooltip("Angular turning speed.")]
    public float angularSpeed = 420.0f;

    [Header("Assigned Hallway & Floor Limits")]
    [Tooltip("The hallway name/identifier The Proctor is strictly confined to.")]
    public string assignedHallway = "Hallway_1F";

    [Tooltip("Floor index (1, 2, or 3).")]
    public int assignedFloor = 1;

    [Tooltip("Minimum Z coordinate of the assigned hallway.")]
    public float hallwayMinZ = -16.0f;

    [Tooltip("Maximum Z coordinate of the assigned hallway.")]
    public float hallwayMaxZ = 30.0f;

    [Tooltip("Hallway center X position (approx -84 for building corridors).")]
    public float hallwayCenterX = -84.0f;

    [Tooltip("Half-width of the hallway corridor where Proctor is allowed to walk.")]
    public float hallwayHalfWidth = 2.4f;

    [Header("Acoustic Sensitivity (Blind Sound-Hunting Monster)")]
    [Tooltip("Base distance within which Proctor hears normal walking (14m).")]
    public float baseHearingRange = 14.0f;

    [Tooltip("Distance within which Proctor hears sprinting footsteps & heavy floor vibrations (28m).")]
    public float sprintHearingRange = 28.0f;

    [Header("Audio References")]
    public AudioClip footstepClip;
    public AudioClip alertStingClip;
    public AudioClip jumpscareScreamClip;
    public AudioClip staticHissClip;

    [Header("Visual & Creepy Motion Tuning")]
    [Tooltip("Target height in meters (stands towering Slenderman-like ~2.60m).")]
    public float targetHeightMeters = 2.60f;

    [Tooltip("Spine hunch angle (degrees).")]
    public float spineHunchAngle = 10.0f;

    [Tooltip("Frequency of sudden blind head twitches.")]
    public float headTwitchInterval = 2.2f;

    // ── Internal Runtime ──────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audioSource;
    private AudioSource _footstepAudio;

    private Transform _playerTransform;
    private PlayerMovement _playerMovement;
    private Camera _playerCam;

    // Skeleton bone references for creepy procedural poses
    private Transform _headBone;
    private Transform _neckBone;
    private Transform _spineBone;
    private Transform _leftArmBone;
    private Transform _rightArmBone;
    private Transform _leftHandBone;

    private GameObject _clipboardProp;

    private float _nextHeadTwitchTime = 0f;
    private Quaternion _targetHeadTwitch = Quaternion.identity;
    private Quaternion _currentHeadTwitch = Quaternion.identity;
    private float _footstepTimer = 0f;
    private float _searchWanderTimer = 0f;
    private Vector3 _currentWanderTarget;
    private float _alertHuntTimer = 0f;
    private Vector3 _lastHeardSoundPosition;

    private bool _hasCaughtPlayer = false;
    private List<Bounds> _cachedRoomBounds = new List<Bounds>();

    private void Awake()
    {
        if (ActiveProctor != null && ActiveProctor != this)
        {
            Destroy(gameObject);
            return;
        }
        ActiveProctor = this;

        // Enforce Slenderman height & fast pursuit speeds even if overridden by old prefab serialization
        if (patrolSpeed < 3.0f) patrolSpeed = 3.2f;
        if (chaseSpeed < 5.0f) chaseSpeed = 5.2f;
        if (targetHeightMeters < 2.50f) targetHeightMeters = 2.60f;
        if (catchDistance < 1.35f) catchDistance = 1.4f;
        if (acceleration < 11.0f) acceleration = 12.0f;
        if (angularSpeed < 400.0f) angularSpeed = 420.0f;

        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();

        // Audio setup
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1.0f;
        _audioSource.minDistance = 2.0f;
        _audioSource.maxDistance = 22.0f;
        _audioSource.playOnAwake = false;

        _footstepAudio = gameObject.AddComponent<AudioSource>();
        _footstepAudio.spatialBlend = 1.0f;
        _footstepAudio.minDistance = 2.0f;
        _footstepAudio.maxDistance = 20.0f;
        _footstepAudio.playOnAwake = false;

        LoadAudioAssets();
        ConfigureNavMeshAgent();
        FindPlayerReferences();
        FindBones();
        CacheRoomBounds();
        EnforceToweringHeight();
        CreateClipboardProp();
    }

    private void Start()
    {
        currentState = ProctorState.Spawning;
        StartCoroutine(SpawnSequence());
    }

    private void Update()
    {
        if (currentState == ProctorState.Inactive || currentState == ProctorState.Despawning) return;
        if (currentState == ProctorState.Jumpscare) return;

        // Failsafe: if power is restored by external means, despawn immediately
        if (BlackoutManager.Instance != null && !BlackoutManager.Instance.IsBlackoutActive)
        {
            Despawn();
            return;
        }

        // Keep player reference warm
        if (_playerTransform == null) FindPlayerReferences();

        UpdateAIBehavior();
        UpdateFootstepAudio();
        ClampPositionToAssignedHallway();
    }

    private void LateUpdate()
    {
        if (currentState == ProctorState.Inactive || currentState == ProctorState.Despawning) return;

        // Apply creepy procedural poses (spine hunch, blind listening head twitches)
        ApplyCreepyProceduralMotion();
    }

    // ── Setup & Initialization ───────────────────────────────────────────────

    private void LoadAudioAssets()
    {
#if UNITY_EDITOR
        if (alertStingClip == null)
            alertStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");

        if (jumpscareScreamClip == null)
            jumpscareScreamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");

        if (footstepClip == null)
            footstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");

        if (staticHissClip == null)
            staticHissClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");
#endif
        // Runtime fallback for standalone builds
        if (jumpscareScreamClip == null)
            jumpscareScreamClip = Resources.Load<AudioClip>("Sounds/you died (lobotomy sound)");
        if (alertStingClip == null)
            alertStingClip = Resources.Load<AudioClip>("Sounds/encountering a proctor");
        if (footstepClip == null)
            footstepClip = Resources.Load<AudioClip>("Sounds/footsteps walking & running");
        if (staticHissClip == null)
            staticHissClip = Resources.Load<AudioClip>("Sounds/vhs static");
    }

    private void ConfigureNavMeshAgent()
    {
        if (_agent == null) return;
        _agent.speed = chaseSpeed;
        _agent.stoppingDistance = stoppingDistance;
        _agent.acceleration = acceleration;
        _agent.angularSpeed = angularSpeed;
        _agent.autoBraking = true;
        _agent.autoTraverseOffMeshLink = false; // STRICT: CANNOT USE STAIRS TO LEAVE FLOOR
        _agent.height = targetHeightMeters;
        _agent.radius = 0.45f;
    }

    private void FindPlayerReferences()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
            _playerMovement = playerGO.GetComponent<PlayerMovement>();
            if (_playerMovement != null) _playerCam = _playerMovement.playerCamera;
        }
        if (_playerCam == null) _playerCam = Camera.main;
    }

    private void FindBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head") && _headBone == null) _headBone = t;
            else if (lower.Contains("neck") && _neckBone == null) _neckBone = t;
            else if ((lower.Contains("spine1") || lower.Contains("spine2") || lower.Contains("chest")) && _spineBone == null) _spineBone = t;
            else if (lower.Contains("leftarm") && _leftArmBone == null) _leftArmBone = t;
            else if (lower.Contains("rightarm") && _rightArmBone == null) _rightArmBone = t;
            else if (lower.Contains("lefthand") && _leftHandBone == null) _leftHandBone = t;
        }
    }

    private void EnforceToweringHeight()
    {
        transform.localScale = Vector3.one;
        if (_agent != null)
        {
            _agent.height = targetHeightMeters;
        }
        var cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.height = targetHeightMeters;
            cap.center = new Vector3(0f, targetHeightMeters * 0.5f, 0f);
        }

        // Scale Armature so height matches targetHeightMeters without clipping ceilings or doorframes
        Transform armature = transform.Find("Armature");
        if (armature != null)
        {
            float scale = (targetHeightMeters / 2.18f) * 49.745f;
            armature.localScale = Vector3.one * scale;
        }

        Debug.Log($"[TheProctorAI] Height enforced to {targetHeightMeters:F2}m (near ceiling, no clipping).");
    }

    private void CreateClipboardProp()
    {
        // Attach dripping clipboard prop to left hand
        Transform parentTransform = _leftHandBone != null ? _leftHandBone : transform;

        _clipboardProp = new GameObject("DrippingClipboard");
        _clipboardProp.transform.SetParent(parentTransform, false);

        if (_leftHandBone != null)
        {
            Vector3 ls = _leftHandBone.lossyScale;
            _clipboardProp.transform.localScale = new Vector3(
                ls.x > 0.0001f ? 1f / ls.x : 1f,
                ls.y > 0.0001f ? 1f / ls.y : 1f,
                ls.z > 0.0001f ? 1f / ls.z : 1f
            );
            _clipboardProp.transform.localPosition = Vector3.zero;
            _clipboardProp.transform.localRotation = Quaternion.Euler(20f, 40f, -10f);
        }
        else
        {
            _clipboardProp.transform.localPosition = new Vector3(-0.35f, 1.4f, 0.35f);
            _clipboardProp.transform.localScale = Vector3.one;
        }

        // Visual board (A4 size: ~22cm wide, ~32cm tall, ~1.2cm thick)
        var boardGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardGO.name = "BoardMesh";
        boardGO.transform.SetParent(_clipboardProp.transform, false);
        boardGO.transform.localPosition = new Vector3(0.02f, -0.04f, 0.06f);
        boardGO.transform.localScale = new Vector3(0.22f, 0.32f, 0.012f);

        var col = boardGO.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var renderer = boardGO.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.14f, 0.10f, 0.08f, 1f); // Aged dark mahogany clipboard
            renderer.sharedMaterial = mat;
        }

        // White paper clamped on board
        var paperGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paperGO.name = "PaperSlip";
        paperGO.transform.SetParent(boardGO.transform, false);
        paperGO.transform.localPosition = new Vector3(0f, -0.01f, 0.55f);
        paperGO.transform.localScale = new Vector3(0.85f, 0.85f, 0.1f);

        var paperCol = paperGO.GetComponent<Collider>();
        if (paperCol != null) Destroy(paperCol);

        var paperRend = paperGO.GetComponent<MeshRenderer>();
        if (paperRend != null)
        {
            var pMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pMat.color = new Color(0.85f, 0.82f, 0.76f, 1f); // Yellowed bureaucracy paper
            paperRend.sharedMaterial = pMat;
        }
    }

    private void CacheRoomBounds()
    {
        _cachedRoomBounds.Clear();
        var roomsRoot = GameObject.Find("Rooms");
        if (roomsRoot == null) return;

        for (int f = 0; f < roomsRoot.transform.childCount; f++)
        {
            var floor = roomsRoot.transform.GetChild(f);
            for (int r = 0; r < floor.transform.childCount; r++)
            {
                var room = floor.transform.GetChild(r);
                var cols = room.GetComponentsInChildren<Collider>(true);
                bool first = true;
                Bounds b = new Bounds();
                foreach (var c in cols)
                {
                    if (c.isTrigger) continue;
                    if (c.gameObject.name.Contains("ReceptionServiceWindow")) continue;
                    if (first) { b = c.bounds; first = false; }
                    else b.Encapsulate(c.bounds);
                }
                if (!first)
                {
                    _cachedRoomBounds.Add(b);
                }
            }
        }
    }

    // ── Spawning & State Machine ──────────────────────────────────────────────

    private IEnumerator SpawnSequence()
    {
        _agent.enabled = false;

        // Play alert sting to announce the entity's arrival in the blackout corridor
        if (alertStingClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(alertStingClip, 0.9f);
        }

        yield return new WaitForSeconds(0.4f);

        // Snap to valid NavMesh position on assigned floor
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 4.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }

        _agent.enabled = true;
        _agent.isStopped = false;
        currentState = ProctorState.Searching;
        Debug.Log($"[TheProctorAI] Spawned and patrolling in {assignedHallway} at {transform.position}");
    }

    private void UpdateAIBehavior()
    {
        if (_playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        bool playerHidden = LockerHideManager.IsPlayerHidden;
        bool playerInRoom = IsPositionInsideAnyRoom(_playerTransform.position);

        // Check if player is on the same floor elevation
        bool sameFloor = Mathf.Abs(_playerTransform.position.y - transform.position.y) < 3.2f;

        // 1. CATCH CHECK: Can only catch if player is in the hallway on the same floor, NOT hidden, NOT inside a room
        if (sameFloor && !playerHidden && !playerInRoom && distToPlayer <= catchDistance)
        {
            TriggerCatchJumpscare();
            return;
        }

        // 2. DETECTION / ACOUSTIC SENSING (STRICTLY AUDITORY ONLY):
        // The Proctor is completely blind and deaf to sight, flashlight, or ambient proximity.
        // It strictly hunts based on sound and floor vibration:
        // - Sprinting: heavy running footsteps vibrate the building -> heard up to sprintHearingRange (28m).
        // - Normal un-crouched walking: audible up to baseHearingRange (14m).
        // - Crouching (IsCrouching) or standing still: produces 0 sound! The Proctor will pass right by.
        // - Locker hiding: player is enclosed, producing 0 external sound.
        bool isSprinting = _playerMovement != null && _playerMovement.IsRunning;
        bool isWalking = _playerMovement != null && _playerMovement.IsMoving && !_playerMovement.IsCrouching;

        bool heardSoundNow = false;

        if (sameFloor && !playerHidden)
        {
            if (isSprinting && distToPlayer <= sprintHearingRange)
            {
                heardSoundNow = true;
            }
            else if (isWalking && distToPlayer <= baseHearingRange)
            {
                heardSoundNow = true;
            }
        }

        if (heardSoundNow)
        {
            // Fresh sound heard! Alert and hunt toward sound source for 4.5 seconds
            if (_alertHuntTimer <= 0f && alertStingClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(alertStingClip, 0.75f);
            }
            _alertHuntTimer = 4.5f;
            _lastHeardSoundPosition = _playerTransform.position;
        }

        // Count down hunt timer if not actively hearing fresh sound
        if (_alertHuntTimer > 0f)
        {
            _alertHuntTimer -= Time.deltaTime;
        }

        bool isHunting = (_alertHuntTimer > 0f);

        // 3. BEHAVIOR EXECUTION:
        if (isHunting && sameFloor)
        {
            currentState = ProctorState.Chasing;
            _agent.speed = chaseSpeed;

            Vector3 huntTarget = GetHallwayConstrainedPosition(_lastHeardSoundPosition);
            _agent.SetDestination(huntTarget);
        }
        else
        {
            // Searching/Patrolling the hallway:
            currentState = ProctorState.Searching;
            _agent.speed = patrolSpeed;

            _searchWanderTimer -= Time.deltaTime;
            if (_searchWanderTimer <= 0f || _agent.remainingDistance < 0.6f)
            {
                _searchWanderTimer = Random.Range(3.5f, 6.0f);
                // Stalk towards the player's sector of the hallway instead of aimlessly wandering away!
                PickHallwayPatrolTargetTowardsPlayer();
            }
        }

        // Sync Animator speed parameter for walking leg locomotion (never glides)
        if (_animator != null)
        {
            float currentSpeed = _agent.velocity.magnitude;
            float normSpeed = 0f;
            if (currentSpeed > 0.08f)
            {
                normSpeed = Mathf.Clamp(currentSpeed / patrolSpeed, 0.5f, 1.25f);
            }
            _animator.SetFloat("Speed", normSpeed);
            _animator.SetBool("Chasing", currentState == ProctorState.Chasing);
        }
    }

    private void PickHallwayPatrolTargetTowardsPlayer()
    {
        // Patrol along corridor biased towards player's Z location to maintain suspense
        float playerZ = (_playerTransform != null) ? _playerTransform.position.z : (hallwayMinZ + hallwayMaxZ) * 0.5f;

        // Step 6-12m in the general direction of the player, clamped within the hallway
        float currentZ = transform.position.z;
        float dirZ = Mathf.Sign(playerZ - currentZ);
        float step = Random.Range(6.0f, 12.0f);
        float targetZ = Mathf.Clamp(currentZ + dirZ * step, hallwayMinZ + 2f, hallwayMaxZ - 2f);

        float targetX = hallwayCenterX + Random.Range(-hallwayHalfWidth * 0.4f, hallwayHalfWidth * 0.4f);
        Vector3 rawTarget = new Vector3(targetX, transform.position.y, targetZ);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(rawTarget, out hit, 3.0f, NavMesh.AllAreas))
        {
            _currentWanderTarget = hit.position;
            _agent.SetDestination(_currentWanderTarget);
        }
    }

    private Vector3 GetHallwayConstrainedPosition(Vector3 desiredPos)
    {
        // Clamps target strictly to the hallway bounds so Proctor never routes into a classroom or room
        float clampedX = Mathf.Clamp(desiredPos.x, hallwayCenterX - hallwayHalfWidth, hallwayCenterX + hallwayHalfWidth);
        float clampedZ = Mathf.Clamp(desiredPos.z, hallwayMinZ, hallwayMaxZ);
        return new Vector3(clampedX, transform.position.y, clampedZ);
    }

    private void ClampPositionToAssignedHallway()
    {
        // Strict boundary fence: ensures agent never drifts into side rooms
        Vector3 pos = transform.position;
        float clampedX = Mathf.Clamp(pos.x, hallwayCenterX - hallwayHalfWidth, hallwayCenterX + hallwayHalfWidth);
        float clampedZ = Mathf.Clamp(pos.z, hallwayMinZ, hallwayMaxZ);

        if (Mathf.Abs(pos.x - clampedX) > 0.05f || Mathf.Abs(pos.z - clampedZ) > 0.05f)
        {
            pos.x = clampedX;
            pos.z = clampedZ;
            transform.position = pos;
            if (_agent.isOnNavMesh) _agent.Warp(pos);
        }
    }

    private bool IsPositionInsideAnyRoom(Vector3 pos)
    {
        // 1. If pos is outside hallway corridor laterally, it's definitely inside a side room or office
        bool inCorridor = (pos.x >= hallwayCenterX - (hallwayHalfWidth + 0.6f)) &&
                          (pos.x <= hallwayCenterX + (hallwayHalfWidth + 0.6f)) &&
                          (pos.z >= hallwayMinZ - 1.0f) &&
                          (pos.z <= hallwayMaxZ + 1.0f);

        if (!inCorridor) return true;

        // 2. Also check cached room bounds if available
        if (_cachedRoomBounds != null && _cachedRoomBounds.Count > 0)
        {
            foreach (var b in _cachedRoomBounds)
            {
                if (b.Contains(pos)) return true;
            }
        }
        return false;
    }

    // ── Creepy Procedural Locomotion ──────────────────────────────────────────

    private void ApplyCreepyProceduralMotion()
    {
        // 1. Spine forward hunch - fixed controlled angle, non-accumulating!
        if (_spineBone != null)
        {
            _spineBone.localRotation *= Quaternion.Euler(spineHunchAngle, 0f, 0f);
        }

        // 2. Left arm positioned to hold dripping clipboard firmly against chest
        if (_leftArmBone != null)
        {
            _leftArmBone.localRotation *= Quaternion.Euler(22f, 15f, -10f);
        }

        // 3. Blind listening head twitches (smoothly tracking target tilt)
        if (Time.time >= _nextHeadTwitchTime)
        {
            _nextHeadTwitchTime = Time.time + Random.Range(headTwitchInterval * 0.7f, headTwitchInterval * 1.3f);
            float twitchYaw = Random.Range(-35f, 35f);
            float twitchPitch = Random.Range(-10f, 16f);
            float twitchRoll = Random.Range(-14f, 14f);
            _targetHeadTwitch = Quaternion.Euler(twitchPitch, twitchYaw, twitchRoll);
        }

        _currentHeadTwitch = Quaternion.Slerp(_currentHeadTwitch, _targetHeadTwitch, Time.deltaTime * 5f);
        if (_headBone != null)
        {
            _headBone.localRotation *= _currentHeadTwitch;
        }
    }

    private void UpdateFootstepAudio()
    {
        if (_footstepAudio == null || footstepClip == null) return;

        bool isMoving = _agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.15f;
        if (!isMoving) return;

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            // Heavy, spaced, dragging steps
            _footstepTimer = (currentState == ProctorState.Chasing) ? 0.55f : 0.85f;
            _footstepAudio.pitch = Random.Range(0.75f, 0.90f); // Low ominous resonant pitch
            _footstepAudio.PlayOneShot(footstepClip, 0.65f);
        }
    }

    // ── Catch & Jumpscare Sequence ────────────────────────────────────────────

    [ContextMenu("Test Catch Jumpscare")]
    public void TriggerCatchJumpscare()
    {
        if (_hasCaughtPlayer) return;
        _hasCaughtPlayer = true;
        currentState = ProctorState.Jumpscare;

        StartCoroutine(CatchJumpscareSequence());
    }

    private IEnumerator CatchJumpscareSequence()
    {
        Debug.Log("[TheProctorAI] PLAYER CAUGHT! Initiating Jumpscare Sequence.");

        // 1. Freeze Proctor movement immediately
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        // 2. Freeze player movement and lock camera facing Proctor's face
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
        }

        float headOffsetFromRootY = (_headBone != null) ? (_headBone.position.y - transform.position.y) : (targetHeightMeters * 0.90f);
        if (_playerCam != null)
        {
            Vector3 initialHeadPos = transform.position + Vector3.up * headOffsetFromRootY;
            Vector3 lookAtProctor = initialHeadPos - _playerCam.transform.position;
            if (lookAtProctor.sqrMagnitude > 0.01f)
            {
                _playerCam.transform.rotation = Quaternion.LookRotation(lookAtProctor);
            }
        }

        // 3. Audio Horror Screamer
        if (_audioSource != null)
        {
            if (jumpscareScreamClip == null) LoadAudioAssets();
            if (jumpscareScreamClip != null)
            {
                _audioSource.spatialBlend = 0f; // 2D in-your-face audio
                _audioSource.volume = 1.0f;
                _audioSource.PlayOneShot(jumpscareScreamClip, 1.0f);
            }
        }

        // 4. In-Your-Face Camera Shudder & Face Lunging
        float elapsed = 0f;
        float scareDuration = 1.15f;
        Vector3 initialProctorPos = transform.position;

        // Turn Proctor face directly toward camera
        Vector3 faceDir = (_playerCam.transform.position - transform.position).normalized;
        faceDir.y = 0;
        if (faceDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(faceDir);

        // Position target so his terrifying FACE (not his feet!) is positioned directly 0.42m in front of camera lens
        Vector3 targetProctorPos = _playerCam.transform.position - faceDir * 0.42f;
        targetProctorPos.y = _playerCam.transform.position.y - headOffsetFromRootY + 0.04f;

        Vector3 camBaseLocalPos = _playerCam.transform.localPosition;

        while (elapsed < scareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scareDuration;

            // Aggressive snap-slam of his FACE right into camera
            transform.position = Vector3.Lerp(initialProctorPos, targetProctorPos, Mathf.Pow(t, 0.35f));

            // Continuously lock camera directly on his face throughout the scare
            Vector3 currentHeadPos = _headBone != null ? _headBone.position : (transform.position + Vector3.up * headOffsetFromRootY);
            Vector3 lookDir = currentHeadPos - _playerCam.transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                _playerCam.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Violent shudder shake
            float shakeX = Random.Range(-0.04f, 0.04f);
            float shakeY = Random.Range(-0.04f, 0.04f);
            _playerCam.transform.localPosition = camBaseLocalPos + new Vector3(shakeX, shakeY, 0f);

            yield return null;
        }

        if (_playerCam != null) _playerCam.transform.localPosition = camBaseLocalPos;

        // 5. Blinding Whiteout Flash Overlay
        yield return StartCoroutine(WhiteoutAndResetRoutine());

        // 6. Increase Anxiety by jumpscareAnxietyIncrease (exactly once)
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(AnxietyManager.Instance.jumpscareAnxietyIncrease);
        }

        // 7. Despawn Proctor & Restore Power
        TheProctorManager.Instance?.OnProctorCompletedEncounter();
        Despawn();
    }

    private IEnumerator WhiteoutAndResetRoutine()
    {
        var hud = GameObject.Find("HudCanvas");
        Image whiteoutImg = null;
        GameObject whiteoutGO = null;

        if (hud != null)
        {
            whiteoutGO = new GameObject("WhiteoutOverlay", typeof(RectTransform), typeof(Image));
            whiteoutGO.transform.SetParent(hud.transform, false);
            var rt = whiteoutGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            whiteoutImg = whiteoutGO.GetComponent<Image>();
            whiteoutImg.color = new Color(1f, 1f, 1f, 1f); // Pure white flash
            whiteoutImg.raycastTarget = false;
        }

        // Play static hiss during whiteout shock
        if (_audioSource != null && staticHissClip != null)
        {
            _audioSource.PlayOneShot(staticHissClip, 0.7f);
        }

        // Hold solid white for 0.35s while teleporting
        yield return new WaitForSeconds(0.35f);

        // Teleport player back to last safe zone
        if (_playerTransform != null && SafeZoneManager.Instance != null)
        {
            SafeZoneManager.Instance.TeleportToSafeZone(_playerTransform.gameObject);
        }

        // Smooth fade down from whiteout
        float fadeElapsed = 0f;
        float fadeDuration = 1.2f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (fadeElapsed / fadeDuration));
            if (whiteoutImg != null)
            {
                whiteoutImg.color = new Color(1f, 1f, 1f, alpha);
            }
            yield return null;
        }

        if (whiteoutGO != null) Destroy(whiteoutGO);

        // Re-enable player movement
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(true);
        }
    }

    // ── Despawn ───────────────────────────────────────────────────────────────

    public void Despawn()
    {
        currentState = ProctorState.Despawning;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }

        if (_clipboardProp != null) Destroy(_clipboardProp);

        if (ActiveProctor == this) ActiveProctor = null;

        Debug.Log("[TheProctorAI] Despawned.");
        Destroy(gameObject);
    }

    public static void DespawnActiveProctor()
    {
        if (ActiveProctor != null)
        {
            ActiveProctor.Despawn();
        }
    }

    private void OnDestroy()
    {
        if (ActiveProctor == this) ActiveProctor = null;
    }
}
