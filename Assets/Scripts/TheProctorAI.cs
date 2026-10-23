using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// TheProctorAI — Tall, slender, uncanny horror antagonist that spawns during campus blackouts.
/// 
/// Features:
/// - Hunts the player strictly along the assigned hallway of the player's current floor.
/// - Sensitive acoustic hearing: detects player footsteps when walking or sprinting,
///   and immediately charges to the sound source!
/// - Crouching or standing still makes zero noise, allowing stealthy evasion.
/// - Hiding inside a Hallway Locker conceals the player from sight and sound.
/// - Creepy procedural animation overrides: forward hunched posture, erratic head twitches,
///   and a clipboard clutched in his left hand.
/// - In-your-face 3D screaming jumpscare + whiteout teleport back to safe zone on catch.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TheProctorAI : MonoBehaviour
{
    public static TheProctorAI ActiveProctor { get; private set; }

    public enum ProctorState
    {
        Inactive,
        Spawning,
        Patrolling,
        AlertedBySound,
        Chasing,
        SearchingLockerArea,
        Jumpscare,
        Despawning
    }

    [Header("Current State")]
    public ProctorState currentState = ProctorState.Inactive;
    [HideInInspector] public bool isDevSpawn = false;

    [Header("Assigned Hallway & Floor")]
    public string assignedHallway = "Hallway_1F";
    public int assignedFloor = 1;
    public float hallwayMinZ = -16.0f;
    public float hallwayMaxZ = 30.0f;
    public float hallwayCenterX = -84.0f;
    public float hallwayHalfWidth = 2.4f;

    [Header("Movement Speeds (Balanced vs Player Walk=2.5, Run=4.0)")]
    [Tooltip("Slow, menacing stalk speed along the dark hallway.")]
    public float patrolSpeed = 1.9f;

    [Tooltip("Brisk investigation speed when he hears a sound.")]
    public float investigateSpeed = 2.8f;

    [Tooltip("Aggressive sprint speed when actively chasing the player (slightly slower than player sprint 4.0 so player can narrowly reach a locker!).")]
    public float chaseSpeed = 3.65f;

    [Header("Vision & Hearing Detection")]
    [Tooltip("Direct line-of-sight range down the dark hallway (longer if player's flashlight is ON).")]
    public float baseVisionRange = 14.0f;
    public float flashlightVisionBonus = 7.0f;
    [Range(40f, 160f)]
    public float visionAngle = 115f;

    [Tooltip("Distance within which walking footsteps are heard by The Proctor.")]
    public float walkHearingRange = 10.5f;

    [Tooltip("Distance within which sprinting footsteps are heard (almost the entire hallway!).")]
    public float sprintHearingRange = 22.0f;

    [Tooltip("Base hearing range (alias for threat scaling).")]
    public float baseHearingRange = 10.5f;

    [Tooltip("How long The Proctor continues hunting before giving up when alert expires.")]
    public float huntPersistenceDuration = 8.0f;

    [Tooltip("Distance at which The Proctor grabs the player for a jumpscare.")]
    public float catchDistance = 1.30f;

    [Header("Uncanny Tall Proportions & Procedural Rig")]
    [Tooltip("Target height in meters (normal NPCs are ~1.6m; The Proctor towers at ~2.12m).")]
    public float targetHeightMeters = 2.12f;

    [Tooltip("Slender width multiplier for an elongated silhouette.")]
    public float slenderWidthRatio = 0.86f;

    [Tooltip("Forward spine hunch angle in degrees.")]
    public float spineHunchAngle = 14.0f;

    [Tooltip("How frequently his head twitches erratically while stalking.")]
    public float headTwitchInterval = 0.9f;

    [Header("Audio Clips")]
    public float acceleration = 14f;
    public float angularSpeed = 420f;
    public AudioClip spawnStingClip;
    public AudioClip footstepClip;
    public AudioClip alertScreamClip;
    public AudioClip alertStingClip;
    public AudioClip jumpscareScreamClip;
    public AudioClip staticHissClip;

    // ── Internal Runtime References ──────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator _animator;
    private SkinnedMeshRenderer _smr;
    private Transform _playerTransform;
    private PlayerMovement _playerMovement;
    private Camera _playerCam;

    // Bones for procedural creepy posture & clipboard attachment
    private Transform _headBone;
    private Transform _neckBone;
    private Transform _spineBone;
    private Transform _leftHandBone;
    private Transform _rightHandBone;
    private Transform _leftArmBone;
    private Transform _rightArmBone;
    private GameObject _clipboardProp;

    // Navigation & State Timers
    private Vector3 _patrolTarget;
    private Vector3 _lastHeardPosition;
    private float _stateTimer = 0f;
    private float _footstepTimer = 0f;
    private float _nextHeadTwitchTime = 0f;
    private Quaternion _currentHeadTwitch = Quaternion.identity;
    private Quaternion _targetHeadTwitch = Quaternion.identity;
    private int _patrolDirection = 1; // +1 towards maxZ, -1 towards minZ
    private int _speedHash;
    private bool _hasSpeedParam = false;
    private bool _hasCaughtPlayer = false;
    private float _alertHuntTimer = 0f; // Keeps sprinting aggressively after being alerted
    private float _lastAlertStingTime = -99f;
    private bool _isTraversingLink = false; // Smooth stair climbing state

    // Audio Sources
    private AudioSource _audioSource;
    private AudioSource _footstepAudio;

    // Cached Room Bounds to ensure Proctor never enters side rooms
    private List<Bounds> _cachedRoomBounds = new List<Bounds>();

    private void Awake()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            Destroy(gameObject);
            return;
        }

        if (ActiveProctor != null && ActiveProctor != this)
        {
            Destroy(ActiveProctor.gameObject);
        }
        ActiveProctor = this;

        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _smr = GetComponentInChildren<SkinnedMeshRenderer>();

        SetupAudioSources();
        LoadAudioAssets();
        CacheRoomBounds();
    }

    private void Start()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            Despawn();
            return;
        }

        FindPlayerReferences();
        ConfigureTallSlenderProportions();
        FindRigBones();
        AttachClipboardToLeftHand();
        ConfigureNavMeshAgent();

        // Dynamic threat scaling (FNAF-style progression based on clearance signatures)
        if (CampusThreatDirector.Instance != null)
        {
            CampusThreatDirector.Instance.ApplyStatsToProctor(this);
        }

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _speedHash = Animator.StringToHash("Speed");
            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == _speedHash && p.type == AnimatorControllerParameterType.Float)
                {
                    _hasSpeedParam = true;
                    break;
                }
            }
        }

        // Play terrifying spawn sting
        if (_audioSource != null && spawnStingClip != null)
        {
            _audioSource.PlayOneShot(spawnStingClip, 0.9f);
        }

        currentState = ProctorState.Patrolling;
        PickNextHallwayPatrolPoint(initial: true);
    }

    private void Update()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            Despawn();
            return;
        }
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            Despawn();
            return;
        }
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (currentState == ProctorState.Inactive || currentState == ProctorState.Despawning) return;

        // Failsafe: if power is restored by external means, despawn immediately (unless dev-spawned)
        if (!isDevSpawn && BlackoutManager.Instance != null && !BlackoutManager.Instance.IsBlackoutActive)
        {
            Despawn();
            return;
        }

        if (currentState == ProctorState.Jumpscare) return;

        if (_playerTransform == null) FindPlayerReferences();
        if (_playerTransform == null) return;

        // Handle smooth traversal across Staircase OffMeshLinks between 1F, 2F, and 3F
        if (_agent != null && _agent.isOnOffMeshLink && !_isTraversingLink)
        {
            StartCoroutine(TraverseStairLinkRoutine());
        }

        // Dynamically track and update current floor boundaries based on Proctor's actual elevation
        int currentFloor = GetFloorFromY(transform.position.y);
        if (currentFloor != assignedFloor)
        {
            UpdateFloorBoundaries(currentFloor);
        }

        if (!_isTraversingLink)
        {
            UpdateAIBehavior();
            ClampPositionToAssignedHallway();
        }

        UpdateAnimatorSpeed();
        UpdateFootstepAudio();
    }

    private void LateUpdate()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (currentState == ProctorState.Inactive || currentState == ProctorState.Despawning || currentState == ProctorState.Jumpscare)
            return;

        ApplyCreepyProceduralMotion();
    }

    // ── Initialization & Proportions ─────────────────────────────────────────

    private void FindPlayerReferences()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerMovement = playerObj.GetComponent<PlayerMovement>();
            if (_playerMovement != null && _playerMovement.playerCamera != null)
                _playerCam = _playerMovement.playerCamera;
        }
        if (_playerCam == null) _playerCam = Camera.main;
    }

    private void SetupAudioSources()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.35f; // Mostly 2D/3D hybrid for ominous presence
        _audioSource.minDistance = 3f;
        _audioSource.maxDistance = 32f;
        _audioSource.playOnAwake = false;

        _footstepAudio = gameObject.AddComponent<AudioSource>();
        _footstepAudio.spatialBlend = 1.0f; // Full 3D directional heavy footsteps
        _footstepAudio.minDistance = 2.0f;
        _footstepAudio.maxDistance = 24.0f;
        _footstepAudio.rolloffMode = AudioRolloffMode.Linear;
        _footstepAudio.playOnAwake = false;
    }

    private void LoadAudioAssets()
    {
#if UNITY_EDITOR
        if (spawnStingClip == null)
            spawnStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
        if (footstepClip == null)
            footstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
        if (alertScreamClip == null)
            alertScreamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/theProctorJumpscare.mp3");
        if (jumpscareScreamClip == null)
            jumpscareScreamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/theProctorJumpscare.mp3");
        if (staticHissClip == null)
            staticHissClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");
#endif
        if (spawnStingClip == null)
            spawnStingClip = Resources.Load<AudioClip>("Sounds/encountering a proctor");
        if (footstepClip == null)
            footstepClip = Resources.Load<AudioClip>("Sounds/footsteps walking & running");
        if (alertScreamClip == null)
            alertScreamClip = Resources.Load<AudioClip>("Sounds/theProctorJumpscare");
        if (jumpscareScreamClip == null)
            jumpscareScreamClip = Resources.Load<AudioClip>("Sounds/theProctorJumpscare");
        if (staticHissClip == null)
            staticHissClip = Resources.Load<AudioClip>("Sounds/vhs static");
    }

    private void ConfigureTallSlenderProportions()
    {
        if (_smr == null) _smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (_smr == null) return;

        // Measure current world height and scale up to towering 2.12m height with gaunt width
        Bounds b = _smr.bounds;
        float currentHeight = b.size.y;
        if (currentHeight > 0.1f && currentHeight < 10f)
        {
            float scaleY = (targetHeightMeters / currentHeight) * transform.localScale.y;
            float scaleXZ = scaleY * slenderWidthRatio;
            transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
        }

        // Darken his suit/skin slightly for an ominous shadowed appearance in the blackout
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || r.sharedMaterial == null) continue;
            Material mat = new Material(r.sharedMaterial);
            Color darkTint = new Color(0.72f, 0.70f, 0.76f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", darkTint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", darkTint);
            r.material = mat;
        }
    }

    private void FindRigBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string n = t.name;
            if (_headBone == null && (n.EndsWith("Head") || n.Contains(":Head"))) _headBone = t;
            else if (_neckBone == null && (n.EndsWith("Neck") || n.Contains(":Neck"))) _neckBone = t;
            else if (_spineBone == null && (n.EndsWith("Spine1") || n.EndsWith("Spine2") || n.Contains(":Spine1"))) _spineBone = t;
            else if (_leftHandBone == null && (n.EndsWith("LeftHand") || n.Contains(":LeftHand"))) _leftHandBone = t;
            else if (_rightHandBone == null && (n.EndsWith("RightHand") || n.Contains(":RightHand"))) _rightHandBone = t;
            else if (_leftArmBone == null && (n.EndsWith("LeftArm") || n.Contains(":LeftArm"))) _leftArmBone = t;
            else if (_rightArmBone == null && (n.EndsWith("RightArm") || n.Contains(":RightArm"))) _rightArmBone = t;
        }
    }

    /// <summary>
    /// Builds a 3D Proctor Clipboard with a red-marked attendance/violation sheet and attaches it to his left hand.
    /// </summary>
    private void AttachClipboardToLeftHand()
    {
        if (_leftHandBone == null) return;

        _clipboardProp = new GameObject("Proctor_Clipboard");
        _clipboardProp.transform.SetParent(_leftHandBone, false);
        _clipboardProp.transform.localPosition = new Vector3(0.04f, 0.08f, 0.03f);
        _clipboardProp.transform.localRotation = Quaternion.Euler(15f, 80f, -10f);

        // Normalize scale relative to parent bone
        Vector3 parentLossy = _leftHandBone.lossyScale;
        float invScale = parentLossy.y > 0.001f ? (1f / parentLossy.y) : 1f;
        _clipboardProp.transform.localScale = Vector3.one * invScale;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Wooden board backing
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(_clipboardProp.transform, false);
        board.transform.localScale = new Vector3(0.24f, 0.34f, 0.012f);
        Destroy(board.GetComponent<Collider>());
        if (litShader != null)
        {
            var boardMat = new Material(litShader);
            Color woodCol = new Color(0.28f, 0.16f, 0.08f);
            if (boardMat.HasProperty("_BaseColor")) boardMat.SetColor("_BaseColor", woodCol);
            if (boardMat.HasProperty("_Color")) boardMat.SetColor("_Color", woodCol);
            board.GetComponent<Renderer>().sharedMaterial = boardMat;
        }

        // White examination sheet
        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "ExamSheet";
        paper.transform.SetParent(_clipboardProp.transform, false);
        paper.transform.localPosition = new Vector3(0f, -0.01f, 0.008f);
        paper.transform.localScale = new Vector3(0.21f, 0.29f, 0.004f);
        Destroy(paper.GetComponent<Collider>());
        if (litShader != null)
        {
            var paperMat = new Material(litShader);
            Color paperCol = new Color(0.88f, 0.85f, 0.78f);
            if (paperMat.HasProperty("_BaseColor")) paperMat.SetColor("_BaseColor", paperCol);
            if (paperMat.HasProperty("_Color")) paperMat.SetColor("_Color", paperCol);
            paper.GetComponent<Renderer>().sharedMaterial = paperMat;
        }

        // Metal clip at top
        var clip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clip.name = "MetalClip";
        clip.transform.SetParent(_clipboardProp.transform, false);
        clip.transform.localPosition = new Vector3(0f, 0.14f, 0.012f);
        clip.transform.localScale = new Vector3(0.10f, 0.035f, 0.015f);
        Destroy(clip.GetComponent<Collider>());
        if (litShader != null)
        {
            var clipMat = new Material(litShader);
            Color metalCol = new Color(0.55f, 0.58f, 0.62f);
            if (clipMat.HasProperty("_BaseColor")) clipMat.SetColor("_BaseColor", metalCol);
            if (clipMat.HasProperty("_Color")) clipMat.SetColor("_Color", metalCol);
            clip.GetComponent<Renderer>().sharedMaterial = clipMat;
        }
    }

    private void ConfigureNavMeshAgent()
    {
        if (_agent == null) return;
        _agent.speed = patrolSpeed;
        _agent.angularSpeed = 420f;
        _agent.acceleration = 14f;
        _agent.stoppingDistance = 0.25f;
        _agent.autoBraking = false;
        _agent.autoTraverseOffMeshLink = false; // Handled by smooth stair traversal coroutine
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.avoidancePriority = 10; // Highest priority so normal NPCs yield to The Proctor
    }

    private IEnumerator TraverseStairLinkRoutine()
    {
        _isTraversingLink = true;
        OffMeshLinkData data = _agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = data.endPos + Vector3.up * _agent.baseOffset;

        // Climbing speed: fast when chasing or alerted, steady when patrolling
        float climbSpeed = (currentState == ProctorState.Chasing || _alertHuntTimer > 0f)
            ? chaseSpeed * 0.92f
            : patrolSpeed * 1.15f;

        float dist = Vector3.Distance(startPos, endPos);
        float duration = Mathf.Max(0.25f, dist / Mathf.Max(1.0f, climbSpeed));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_agent == null || !gameObject.activeInHierarchy) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(startPos, endPos, t);

            // Face the direction of the stairs while ascending/descending
            Vector3 lookDir = (endPos - startPos);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 10f);
            }

            yield return null;
        }

        if (_agent != null && _agent.isOnNavMesh)
        {
            transform.position = endPos;
            _agent.CompleteOffMeshLink();
            _agent.isStopped = false;
        }

        // Update floor boundaries upon arriving on the new floor
        int newFloor = GetFloorFromY(transform.position.y);
        UpdateFloorBoundaries(newFloor);

        _isTraversingLink = false;
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
                    if (first) { b = c.bounds; first = false; }
                    else b.Encapsulate(c.bounds);
                }
                if (!first)
                {
                    // Shrink slightly inward so hallway walls don't count as inside a room
                    b.Expand(new Vector3(-0.6f, 0f, -0.6f));
                    _cachedRoomBounds.Add(b);
                }
            }
        }
    }

    // ── AI Sensory & State Machine ───────────────────────────────────────────

    private void UpdateAIBehavior()
    {
        Vector3 playerPos = _playerTransform.position;
        float yDiff = Mathf.Abs(playerPos.y - transform.position.y);
        bool sameFloor = (yDiff < 2.2f);
        float distToPlayer = Vector3.Distance(transform.position, playerPos);

        // Check if player is hidden inside a locker or inside a side classroom/office
        bool playerHidden = LockerHideManager.IsPlayerHidden;
        bool playerInsideRoom = IsPositionInsideAnyRoom(playerPos);

        // Force-close any open puzzle/minigame/modal when The Proctor is about to catch/jumpscare the player!
        if (!playerHidden && sameFloor && distToPlayer <= catchDistance + 4.5f && PauseMenu.IsAnyActivePuzzleOrModalOpen)
        {
            PauseMenu.ForceCloseAllModalsAndPuzzles();
        }

        // 1. Check for immediate Catch / Jumpscare (only if player is NOT hidden in a locker and NOT inside a side room)
        if (!playerHidden && !playerInsideRoom && sameFloor)
        {
            if (distToPlayer <= catchDistance)
            {
                TriggerCatchJumpscare();
                return;
            }
        }

        // 2. Sensory Checks: Can The Proctor SEE or HEAR the player?
        //     Vision works on the same floor; Hearing works across floors via stairwells (attenuated by vertical distance)!
        bool canSeePlayer = false;
        bool canHearPlayer = false;

        if (!playerHidden && !playerInsideRoom)
        {
            if (sameFloor)
            {
                canSeePlayer = CheckVisionToPlayer(playerPos, distToPlayer);
            }
            canHearPlayer = CheckHearingPlayer(playerPos, distToPlayer, yDiff);
        }

        // Decay aggressive hunt timer
        if (_alertHuntTimer > 0f)
        {
            _alertHuntTimer -= Time.deltaTime;
        }

        // 3. State Transitions based on Senses
        if (canSeePlayer)
        {
            if (currentState != ProctorState.Chasing)
            {
                EnterChaseState(playerPos, playSting: true);
            }
            else
            {
                _lastHeardPosition = GetHallwayClampedTarget(playerPos);
                _stateTimer = 4.5f; // Keep chasing for 4.5s after losing sight
                _alertHuntTimer = 8.0f;
            }
        }
        else if (canHearPlayer)
        {
            // Player made noise (walking/sprinting)!
            // Immediately trigger full-speed sprint toward the sound and keep sprinting while hunting!
            _lastHeardPosition = GetHallwayClampedTarget(playerPos);
            _alertHuntTimer = 9.0f; // Sprint continuously for 9 seconds after hearing footsteps

            if (currentState != ProctorState.Chasing)
            {
                // Play terrifying alert scream with a short cooldown so it doesn't spam every step
                bool shouldSting = (Time.time - _lastAlertStingTime > 5.0f);
                EnterChaseState(playerPos, playSting: shouldSting);
            }
            else
            {
                _stateTimer = 5.0f;
            }
        }

        // 4. Execute Current State
        switch (currentState)
        {
            case ProctorState.Patrolling:
                UpdatePatrolState();
                break;

            case ProctorState.AlertedBySound:
                UpdateAlertedState();
                break;

            case ProctorState.Chasing:
                UpdateChaseState(canSeePlayer, playerHidden, playerInsideRoom);
                break;

            case ProctorState.SearchingLockerArea:
                UpdateSearchingAreaState();
                break;
        }
    }

    private bool CheckVisionToPlayer(Vector3 playerPos, float distToPlayer)
    {
        bool flashlightOn = (FlashlightController.Instance != null && FlashlightController.Instance.IsLightOn);
        float effectiveVisionRange = baseVisionRange + (flashlightOn ? flashlightVisionBonus : 0f);

        if (distToPlayer > effectiveVisionRange) return false;

        // Close proximity sense (within 2.2m in the hallway)
        if (distToPlayer > 2.2f)
        {
            Vector3 toPlayer = (playerPos - transform.position);
            toPlayer.y = 0f;
            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > visionAngle * 0.5f) return false;
        }

        // Raycast line of sight
        Vector3 eyePos = transform.position + Vector3.up * 1.85f;
        Vector3 targetPos = playerPos + Vector3.up * 1.3f;
        Vector3 dir = targetPos - eyePos;

        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == _playerTransform || hit.transform.IsChildOf(_playerTransform))
                return true;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Acoustic Detection:
    /// - Sprinting: Loud footsteps heard from 22m away (even across floors via stairwells!).
    /// - Walking (not crouched): Audible footsteps heard from 10.5m away.
    /// - Crouching or Standing Still: Silent (0m).
    /// </summary>
    private bool CheckHearingPlayer(Vector3 playerPos, float distToPlayer, float yDiff)
    {
        if (_playerMovement == null) return false;

        // Standing still or crouch-walking is completely silent!
        if (!_playerMovement.IsMoving || _playerMovement.IsCrouching)
            return false;

        float hearingRadius = _playerMovement.IsRunning ? sprintHearingRange : walkHearingRange;

        // Attenuate slightly if the sound has to travel up/down the stairwell from another floor
        if (yDiff >= 2.2f)
        {
            hearingRadius *= 0.75f;
        }

        return distToPlayer <= hearingRadius;
    }

    private void EnterChaseState(Vector3 targetPos, bool playSting)
    {
        currentState = ProctorState.Chasing;
        _lastHeardPosition = GetHallwayClampedTarget(targetPos);
        _stateTimer = 5.0f;
        _alertHuntTimer = 9.0f;

        // If the player has a puzzle/minigame/modal open when The Proctor spots them and charges, exit it immediately!
        if (PauseMenu.IsAnyActivePuzzleOrModalOpen)
        {
            PauseMenu.ForceCloseAllModalsAndPuzzles();
        }

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.speed = chaseSpeed;
            _agent.acceleration = 22f;
            _agent.SetDestination(_lastHeardPosition);
        }

        if (playSting && _audioSource != null && alertScreamClip != null)
        {
            _lastAlertStingTime = Time.time;
            _audioSource.PlayOneShot(alertScreamClip, 0.75f);
        }
    }

    private void EnterAlertedState(Vector3 soundPos)
    {
        // Directly escalate to full-speed sprinting chase towards the sound!
        EnterChaseState(soundPos, playSting: (Time.time - _lastAlertStingTime > 5.0f));
    }

    private void UpdatePatrolState()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        // If still agitated from a recent sound/chase, keep sprinting along the hallway!
        _agent.speed = (_alertHuntTimer > 0f) ? chaseSpeed : patrolSpeed;

        if (!_agent.pathPending && _agent.remainingDistance <= 1.2f)
        {
            PickNextHallwayPatrolPoint(initial: false);
        }
    }

    private void UpdateAlertedState()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        _agent.speed = chaseSpeed;
        _stateTimer -= Time.deltaTime;

        if ((!_agent.pathPending && _agent.remainingDistance <= 1.0f) || _stateTimer <= 0f)
        {
            // Reached the sound source — linger and search nearby (especially if a locker is here)
            currentState = ProctorState.SearchingLockerArea;
            _stateTimer = 3.5f;
        }
    }

    private void UpdateChaseState(bool canSeePlayer, bool playerHidden, bool playerInsideRoom)
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        _agent.speed = chaseSpeed;

        if (canSeePlayer && !playerHidden && !playerInsideRoom)
        {
            _lastHeardPosition = GetHallwayClampedTarget(_playerTransform.position);
            _agent.SetDestination(_lastHeardPosition);
            _stateTimer = 4.0f;
        }
        else
        {
            // Move to last known hallway position
            _agent.SetDestination(_lastHeardPosition);
            _stateTimer -= Time.deltaTime;

            if (_stateTimer <= 0f || (!_agent.pathPending && _agent.remainingDistance <= 1.0f))
            {
                // Lost the player (e.g. they ducked into a locker or side room) -> pace around the spot briefly
                currentState = ProctorState.SearchingLockerArea;
                _stateTimer = 4.0f;
            }
        }
    }

    private void UpdateSearchingAreaState()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        // Keep running/pacing rapidly while searching the area after an alert!
        _agent.speed = (_alertHuntTimer > 0f) ? (chaseSpeed * 0.85f) : (patrolSpeed * 1.15f);
        _stateTimer -= Time.deltaTime;

        if (!_agent.pathPending && _agent.remainingDistance <= 0.8f)
        {
            // Pick a nearby point within 5m in the hallway to pace back and forth outside the locker/room
            float offsetZ = Random.Range(-5.0f, 5.0f);
            Vector3 searchPt = new Vector3(
                hallwayCenterX + Random.Range(-0.8f, 0.8f),
                transform.position.y,
                Mathf.Clamp(_lastHeardPosition.z + offsetZ, hallwayMinZ + 1.5f, hallwayMaxZ - 1.5f)
            );

            if (NavMesh.SamplePosition(searchPt, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        if (_stateTimer <= 0f)
        {
            currentState = ProctorState.Patrolling;
            PickNextHallwayPatrolPoint(initial: false);
        }
    }

    private void PickNextHallwayPatrolPoint(bool initial)
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        if (initial)
        {
            // Head toward the player's general Z direction first
            if (_playerTransform != null)
            {
                _patrolDirection = (_playerTransform.position.z >= transform.position.z) ? 1 : -1;
            }
        }
        else
        {
            // Sweep back and forth along the full hallway length
            if (transform.position.z >= hallwayMaxZ - 4.0f) _patrolDirection = -1;
            else if (transform.position.z <= hallwayMinZ + 4.0f) _patrolDirection = 1;
            else if (Random.value < 0.25f) _patrolDirection = -_patrolDirection;
        }

        // Occasionally (or if the player is on another floor while hunting), patrol to another floor via the stairs!
        float targetY = transform.position.y;
        if (!initial && _playerTransform != null)
        {
            int playerFloor = GetFloorFromY(_playerTransform.position.y);
            if (playerFloor != assignedFloor && Random.value < 0.55f)
            {
                targetY = _playerTransform.position.y;
            }
        }

        float stepZ = Random.Range(10.0f, 22.0f) * _patrolDirection;
        float targetZ = Mathf.Clamp(transform.position.z + stepZ, hallwayMinZ + 1.5f, hallwayMaxZ - 1.5f);
        float targetX = hallwayCenterX + Random.Range(-0.9f, 0.9f);

        Vector3 candidate = new Vector3(targetX, targetY, targetZ);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            _patrolTarget = hit.position;
            _agent.SetDestination(_patrolTarget);
        }
    }

    // ── Floor & Staircase Helpers ─────────────────────────────────────────────

    private int GetFloorFromY(float y)
    {
        if (y < 5.2f) return 1;
        if (y < 11.2f) return 2;
        return 3;
    }

    public void UpdateFloorBoundaries(int floor)
    {
        assignedFloor = floor;
        hallwayCenterX = -84.0f;
        switch (floor)
        {
            case 1:
                assignedHallway = "Hallway_1F";
                hallwayMinZ = -22.0f;
                hallwayMaxZ = 55.0f;
                hallwayHalfWidth = 3.6f;
                break;
            case 2:
                assignedHallway = "Hallway_2F";
                hallwayMinZ = -18.0f;
                hallwayMaxZ = 51.0f;
                hallwayHalfWidth = 2.8f;
                break;
            case 3:
                assignedHallway = "Hallway_3F";
                hallwayMinZ = -18.0f;
                hallwayMaxZ = 51.0f;
                hallwayHalfWidth = 2.8f;
                break;
        }
    }

    private bool IsInStairZone(Vector3 pos)
    {
        // Covers all 3 staircase enclosures (MainStairs Z:12..21, RightStairs Z:46..53, LeftStairs Z:-20..-13)
        if (pos.x < -94.0f || pos.x > -83.0f) return false;

        if (pos.z >= 11.0f && pos.z <= 22.0f) return true;  // MainStairs
        if (pos.z >= 45.0f && pos.z <= 54.0f) return true;  // RightStairs
        if (pos.z >= -21.0f && pos.z <= -12.0f) return true; // LeftStairs

        return false;
    }

    private void UpdateAnimatorSpeed()
    {
        if (_animator == null) return;

        // Keep running legs moving during stair traversal as well
        float vel = _isTraversingLink
            ? ((currentState == ProctorState.Chasing || _alertHuntTimer > 0f) ? chaseSpeed : patrolSpeed)
            : ((_agent != null) ? _agent.velocity.magnitude : 0f);

        bool isSprintingNow = (currentState == ProctorState.Chasing || _alertHuntTimer > 0f);

        if (_hasSpeedParam)
        {
            float animSpeed = 0f;
            if (vel > 0.1f)
            {
                animSpeed = isSprintingNow ? 2.0f : 1.0f;
            }
            _animator.SetFloat(_speedHash, animSpeed);
        }

        // Speed up stride cycle when sprinting
        if (isSprintingNow && vel > 0.2f)
            _animator.speed = 1.65f;
        else
            _animator.speed = 1.0f;
    }

    // ── Hallway Clamping (Supports Multi-Floor & Stairwells) ─────────────────

    private Vector3 GetHallwayClampedTarget(Vector3 desiredPos)
    {
        // Allow targets inside staircase zones so The Proctor can chase the player onto stairs
        if (IsInStairZone(desiredPos))
        {
            return desiredPos;
        }

        int targetFloor = GetFloorFromY(desiredPos.y);
        float minZ = -18.0f;
        float maxZ = 51.0f;
        float halfW = 2.8f;

        if (targetFloor == 1) { minZ = -22.0f; maxZ = 55.0f; halfW = 3.6f; }
        else if (targetFloor == 2) { minZ = -18.0f; maxZ = 51.0f; halfW = 2.8f; }
        else if (targetFloor == 3) { minZ = -18.0f; maxZ = 51.0f; halfW = 2.8f; }

        float clampedX = Mathf.Clamp(desiredPos.x, hallwayCenterX - halfW, hallwayCenterX + halfW);
        float clampedZ = Mathf.Clamp(desiredPos.z, minZ, maxZ);
        return new Vector3(clampedX, desiredPos.y, clampedZ);
    }

    private void ClampPositionToAssignedHallway()
    {
        // If traversing stairs or inside the stairwell enclosure, skip hallway corridor clamping
        if (_isTraversingLink || IsInStairZone(transform.position)) return;

        Vector3 pos = transform.position;
        float clampedX = Mathf.Clamp(pos.x, hallwayCenterX - hallwayHalfWidth, hallwayCenterX + hallwayHalfWidth);
        float clampedZ = Mathf.Clamp(pos.z, hallwayMinZ, hallwayMaxZ);

        if (Mathf.Abs(pos.x - clampedX) > 0.05f || Mathf.Abs(pos.z - clampedZ) > 0.05f)
        {
            pos.x = clampedX;
            pos.z = clampedZ;
            transform.position = pos;
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(pos);
        }
    }

    private bool IsPositionInsideAnyRoom(Vector3 pos)
    {
        // Staircases and landings are part of circulation, never classified as rooms
        if (IsInStairZone(pos)) return false;

        int floor = GetFloorFromY(pos.y);
        float minZ = -18.0f;
        float maxZ = 51.0f;
        float halfW = 2.8f;
        if (floor == 1) { minZ = -22.0f; maxZ = 55.0f; halfW = 3.6f; }
        else if (floor == 2) { minZ = -18.0f; maxZ = 51.0f; halfW = 2.8f; }
        else if (floor == 3) { minZ = -18.0f; maxZ = 51.0f; halfW = 2.8f; }

        // 1. If pos is outside hallway corridor laterally, it's definitely inside a side room or office
        bool inCorridor = (pos.x >= hallwayCenterX - (halfW + 0.6f)) &&
                          (pos.x <= hallwayCenterX + (halfW + 0.6f)) &&
                          (pos.z >= minZ - 1.0f) &&
                          (pos.z <= maxZ + 1.0f);

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

        bool isMoving = _isTraversingLink || (_agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.15f);
        if (!isMoving) return;

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            // Rapid terrifying stomping footsteps when sprinting, heavy dragging when patrolling
            bool isChasing = (currentState == ProctorState.Chasing || _alertHuntTimer > 0f);
            _footstepTimer = isChasing ? 0.28f : 0.85f;
            _footstepAudio.pitch = isChasing ? Random.Range(0.95f, 1.15f) : Random.Range(0.75f, 0.90f);
            float vol = isChasing ? 0.90f : 0.65f;

            // Muffle footsteps heavily if player is on a different floor (through thick concrete floor slabs)
            if (_playerTransform != null)
            {
                float dy = Mathf.Abs(_playerTransform.position.y - transform.position.y);
                if (dy > 3.2f)
                {
                    vol *= 0.12f;
                }
            }

            _footstepAudio.PlayOneShot(footstepClip, vol);
        }
    }

    // ── Catch & Jumpscare Sequence ────────────────────────────────────────────

    [ContextMenu("Test Catch Jumpscare")]
    public void TriggerCatchJumpscare()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            Despawn();
            return;
        }
        if (_hasCaughtPlayer) return;
        _hasCaughtPlayer = true;
        currentState = ProctorState.Jumpscare;

        // Immediately exit any open puzzle, minigame, inspection modal, or dialogue!
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        StartCoroutine(CatchJumpscareSequence());
    }

    private IEnumerator CatchJumpscareSequence()
    {
        Debug.Log("[TheProctorAI] PLAYER CAUGHT! Initiating Jumpscare Sequence.");

        // Ensure all puzzles and modals are closed so nothing covers the jumpscare or blocks [Q]
        PauseMenu.ForceCloseAllModalsAndPuzzles();

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

        // Turn Proctor face directly toward camera squarely
        Vector3 camFwd = _playerCam.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;
        camFwd.Normalize();

        Vector3 faceDir = -camFwd; // Face directly back at player camera
        transform.rotation = Quaternion.LookRotation(faceDir);

        // Lock camera squarely forward along camFwd
        _playerCam.transform.rotation = Quaternion.LookRotation(camFwd);

        // Compute exact true head height relative to root
        float headLocalHeight = (_headBone != null) ? Mathf.Abs(transform.InverseTransformPoint(_headBone.position).y) : (targetHeightMeters * 0.88f);
        if (headLocalHeight < 1.0f || headLocalHeight > 3.0f) headLocalHeight = targetHeightMeters * 0.88f;

        // Desired face position in world space:
        // Positioned 0.68m in front of camera, elevated +0.02m above camera eye-level
        // This frames face center at exact viewport center, chin safely above the QTE UI
        Vector3 desiredFacePos = camPos + camFwd * 0.68f + Vector3.up * 0.02f;
        Vector3 targetProctorPos = desiredFacePos - Vector3.up * headLocalHeight;

        // Place him initially 1.45m in front of camera so he slams directly towards the lens!
        Vector3 initialProctorPos = (camPos + camFwd * 1.45f + Vector3.up * 0.02f) - Vector3.up * headLocalHeight;
        transform.position = initialProctorPos;

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
            if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
            {
                Despawn();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / initialLungeDuration;
            float ease = Mathf.Pow(t, 0.35f);

            // Aggressive snap-slam of his FACE right into camera lens
            transform.position = Vector3.Lerp(initialProctorPos, targetProctorPos, ease);

            // Dramatic vertigo camera zoom directly into his face
            _playerCam.fieldOfView = Mathf.Lerp(startFOV, jumpscareFOV, ease);

            // Lock camera directly on his screaming face along camFwd
            _playerCam.transform.rotation = Quaternion.LookRotation(camFwd);

            // Violent shudder shake
            float shakeX = Random.Range(-0.025f, 0.025f);
            float shakeY = Random.Range(-0.025f, 0.025f);
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
            headLocalHeight,
            camBaseLocalPos,
            (escaped) => { playerEscaped = escaped; }
        ));

        if (_playerCam != null) _playerCam.transform.localPosition = camBaseLocalPos;

        // If player failed (Anxiety reached 100% -> Game Over), despawn immediately so Proctor does not block Game Over cutscene!
        if (!playerEscaped || GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            Despawn();
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

        // 7. Entity vanishes behind whiteout flash & restores power immediately!
        if (TheProctorManager.Instance != null)
        {
            TheProctorManager.Instance.TriggerJumpscareWhiteout(staticHissClip, _playerCam, _playerMovement);
            TheProctorManager.Instance.OnProctorCompletedEncounter();
        }
        else if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.RestorePower();
        }

        Despawn();
    }

    private void SetRenderersEnabled(bool enabled)
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null) rends[i].enabled = enabled;
        }
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

        // Instantly vanish The Proctor behind the solid whiteout blast!
        SetRenderersEnabled(false);
        if (_audioSource != null) _audioSource.Stop();
        if (_footstepAudio != null) _footstepAudio.Stop();

        // Hold solid white for 0.35s during shock
        yield return new WaitForSeconds(0.35f);

        // Student remains in the same spot (does NOT respawn at the safe zone / spawn point)

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

    /// <summary>
    /// Updates speed and hearing sensitivity based on current campus threat level (FNAF-style difficulty scaling).
    /// </summary>
    public void ApplyThreatStats(float newPatrolSpeed, float newChaseSpeed, float newWalkHearing, float newSprintHearing, float newHuntDuration)
    {
        patrolSpeed = newPatrolSpeed;
        chaseSpeed = newChaseSpeed;
        baseHearingRange = newWalkHearing;
        sprintHearingRange = newSprintHearing;
        huntPersistenceDuration = newHuntDuration;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.speed = (currentState == ProctorState.Chasing) ? chaseSpeed : patrolSpeed;
        }

        Debug.Log($"[TheProctorAI] Threat stats updated: Patrol={patrolSpeed:F1}, Chase={chaseSpeed:F1}, Hearing={baseHearingRange:F1}/{sprintHearingRange:F1}m, HuntDur={huntPersistenceDuration:F1}s");
    }

    // ── Despawn ───────────────────────────────────────────────────────────────

    public void Despawn()
    {
        StopAllCoroutines();
        currentState = ProctorState.Despawning;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }

        if (_clipboardProp != null) Destroy(_clipboardProp);

        if (ActiveProctor == this) ActiveProctor = null;

        SetRenderersEnabled(false);
        gameObject.SetActive(false);

        Debug.Log("[TheProctorAI] Despawned.");
        Destroy(gameObject);
    }

    public static void DespawnActiveProctor()
    {
        if (ActiveProctor != null)
        {
            ActiveProctor.Despawn();
        }
        var all = UnityEngine.Object.FindObjectsByType<TheProctorAI>(UnityEngine.FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject != null)
            {
                all[i].Despawn();
            }
        }
    }

    private void OnDestroy()
    {
        if (ActiveProctor == this) ActiveProctor = null;
    }
}
