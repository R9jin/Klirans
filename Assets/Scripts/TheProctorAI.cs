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
    public float chaseSpeed = 5.6f;

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
        if (chaseSpeed < 5.5f) chaseSpeed = 5.6f;
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
        if (currentState == ProctorState.Inactive || currentState == ProctorState.Despawning || currentState == ProctorState.Jumpscare) return;

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

        if (currentState != ProctorState.Jumpscare)
        {
            _agent.enabled = true;
            _agent.isStopped = false;
            currentState = ProctorState.Searching;
            Debug.Log($"[TheProctorAI] Spawned and patrolling in {assignedHallway} at {transform.position}");
        }
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

        // Sync Animator speed parameter for walking / sprinting leg locomotion (never glides)
        if (_animator != null)
        {
            bool isChasing = (currentState == ProctorState.Chasing);
            float currentSpeed = (_agent != null && _agent.isOnNavMesh) ? _agent.velocity.magnitude : 0f;
            float normSpeed = 0f;

            if (isChasing)
            {
                // Full sprint chase animation: BlendTree threshold 2.0 (Runningdrei_InPlace)
                normSpeed = Mathf.Clamp(1.6f + (currentSpeed / chaseSpeed) * 0.6f, 1.5f, 2.3f);
                _animator.speed = 1.30f; // Frantic, aggressive running pace
            }
            else
            {
                if (currentSpeed > 0.08f)
                {
                    normSpeed = Mathf.Clamp(currentSpeed / patrolSpeed, 0.4f, 1.15f);
                }
                _animator.speed = 1.0f;
            }

            _animator.SetFloat("Speed", normSpeed);
            _animator.SetBool("Chasing", isChasing);
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
        bool isChasing = (currentState == ProctorState.Chasing);

        // 1. Deep predatory forward spine hunch during chase (lean aggressively towards player!)
        if (_spineBone != null)
        {
            float targetHunch = isChasing ? 24.0f : spineHunchAngle;
            _spineBone.localRotation *= Quaternion.Euler(targetHunch, 0f, 0f);
        }

        // 2. Left arm grips clipboard to chest; Right arm reaches/lunges forward during chase
        if (_leftArmBone != null)
        {
            _leftArmBone.localRotation *= Quaternion.Euler(22f, 15f, -10f);
        }
        if (_rightArmBone != null && isChasing)
        {
            // Menacing reaching forward claw / grasping motion
            float armReaching = 38f + Mathf.Sin(Time.time * 14f) * 12f;
            _rightArmBone.localRotation *= Quaternion.Euler(armReaching, -12f, 15f);
        }

        // 3. Creepy entity head twitches: rapid frantic stutters during chase
        float interval = isChasing ? 0.35f : headTwitchInterval;
        if (Time.time >= _nextHeadTwitchTime)
        {
            _nextHeadTwitchTime = Time.time + Random.Range(interval * 0.7f, interval * 1.3f);
            float twitchYaw = isChasing ? Random.Range(-45f, 45f) : Random.Range(-35f, 35f);
            float twitchPitch = isChasing ? Random.Range(-18f, 20f) : Random.Range(-10f, 16f);
            float twitchRoll = Random.Range(-15f, 15f);
            _targetHeadTwitch = Quaternion.Euler(twitchPitch, twitchYaw, twitchRoll);
        }

        float slerpSpeed = isChasing ? 16f : 5f;
        _currentHeadTwitch = Quaternion.Slerp(_currentHeadTwitch, _targetHeadTwitch, Time.deltaTime * slerpSpeed);
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
            // Rapid terrifying stomping footsteps when sprinting, heavy dragging when patrolling
            bool isChasing = (currentState == ProctorState.Chasing);
            _footstepTimer = isChasing ? 0.28f : 0.85f;
            _footstepAudio.pitch = isChasing ? Random.Range(0.95f, 1.15f) : Random.Range(0.75f, 0.90f);
            float vol = isChasing ? 0.90f : 0.65f;
            _footstepAudio.PlayOneShot(footstepClip, vol);
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

        // 1. Disable NavMeshAgent so it does NOT clamp transform.position.y to floor during 3D camera scare
        if (_agent != null)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            _agent.enabled = false;
        }

        // 2. Freeze player movement and lock camera facing Proctor's face
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isJumpscareCameraOverride = true;
        }

        if (_playerCam == null) _playerCam = Camera.main;

        Vector3 camPos = _playerCam.transform.position;
        Vector3 camBaseLocalPos = _playerCam.transform.localPosition;
        float startFOV = _playerCam.fieldOfView;
        float jumpscareFOV = 40.0f; // Extreme FNAF Help Wanted VR close-up zoom!

        // Turn Proctor face directly toward camera horizontally
        Vector3 faceDir = (camPos - transform.position);
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.001f)
        {
            faceDir = -_playerCam.transform.forward;
            faceDir.y = 0f;
        }
        faceDir.Normalize();
        transform.rotation = Quaternion.LookRotation(faceDir);

        // Compute exact head offset relative to Proctor's root position
        Vector3 headOffset = (_headBone != null) ? (_headBone.position - transform.position) : new Vector3(0f, targetHeightMeters * 0.90f, 0f);

        // Desired head position in world space:
        // Positioned 0.25m in front of camera, at EXACT camera eye-level height!
        // Camera look direction is strictly horizontal (-faceDir): zero downward pitch!
        Vector3 desiredHeadPos = camPos - faceDir * 0.25f;
        desiredHeadPos.y = camPos.y; // Eye-level horizontal alignment!

        Vector3 targetProctorPos = desiredHeadPos - headOffset;
        Vector3 initialProctorPos = transform.position;

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

        // 4. Initial In-Your-Face Snap-Slam into Camera with FNAF VR FOV Zoom
        float elapsed = 0f;
        float initialLungeDuration = 0.55f;

        while (elapsed < initialLungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / initialLungeDuration;
            float ease = Mathf.Pow(t, 0.35f);

            // Aggressive snap-slam of his FACE right into camera lens
            transform.position = Vector3.Lerp(initialProctorPos, targetProctorPos, ease);

            // Dramatic vertigo camera zoom directly into his face
            _playerCam.fieldOfView = Mathf.Lerp(startFOV, jumpscareFOV, ease);

            // Continuously lock camera directly on his face throughout the scare
            Vector3 currentHeadPos = (_headBone != null) ? _headBone.position : (transform.position + headOffset);
            Vector3 lookDir = currentHeadPos - _playerCam.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                _playerCam.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Violent shudder shake
            float shakeX = Random.Range(-0.035f, 0.035f);
            float shakeY = Random.Range(-0.035f, 0.035f);
            _playerCam.transform.localPosition = camBaseLocalPos + new Vector3(shakeX, shakeY, 0f);

            yield return null;
        }

        // 5. Interactive Struggle QTE & Continuous Anxiety Buildup
        var qte = ProctorJumpscareQTE.Instance;
        if (qte == null)
        {
            var qteGO = new GameObject("ProctorJumpscareQTE");
            qte = qteGO.AddComponent<ProctorJumpscareQTE>();
        }

        bool playerEscaped = false;
        yield return qte.StartCoroutine(qte.RunStruggleQTE(
            this,
            _playerCam,
            _headBone,
            targetProctorPos,
            faceDir,
            headOffset.y,
            camBaseLocalPos,
            (escaped) => { playerEscaped = escaped; }
        ));

        if (_playerCam != null) _playerCam.transform.localPosition = camBaseLocalPos;

        // If player failed (Anxiety reached 100% -> Game Over), halt routine
        if (!playerEscaped || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            yield break;
        }

        // 6. Breaking Free! Knock Proctor backward with visceral shove
        float pushElapsed = 0f;
        float pushDuration = 0.35f;
        Vector3 shoveStartPos = transform.position;
        Vector3 shoveTargetPos = targetProctorPos - faceDir * 1.6f;

        while (pushElapsed < pushDuration)
        {
            pushElapsed += Time.deltaTime;
            float t = pushElapsed / pushDuration;
            transform.position = Vector3.Lerp(shoveStartPos, shoveTargetPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        // 7. Blinding Whiteout Flash Overlay & Teleport to Safe Zone
        yield return StartCoroutine(WhiteoutAndResetRoutine());

        // 8. Despawn Proctor & Restore Power
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

        // Smooth fade down from whiteout and restore camera FOV
        float fadeElapsed = 0f;
        float fadeDuration = 1.2f;
        float currentFOV = (_playerCam != null) ? _playerCam.fieldOfView : 40f;
        float normalFOV = (_playerMovement != null) ? _playerMovement.normalFOV : 60f;

        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / fadeDuration;
            float alpha = Mathf.Clamp01(1f - t);
            if (whiteoutImg != null)
            {
                whiteoutImg.color = new Color(1f, 1f, 1f, alpha);
            }
            if (_playerCam != null)
            {
                _playerCam.fieldOfView = Mathf.Lerp(currentFOV, normalFOV, t);
            }
            yield return null;
        }

        if (whiteoutGO != null) Destroy(whiteoutGO);

        // Re-enable player movement & restore camera controls
        if (_playerMovement != null)
        {
            _playerMovement.isJumpscareCameraOverride = false;
            if (_playerCam != null)
            {
                _playerCam.fieldOfView = normalFOV;
                _playerMovement.SyncPitch(_playerCam.transform.localEulerAngles.x);
            }
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
