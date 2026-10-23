using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SecurityGuardStalkerAI — "Mr. X" Style Relentless Endgame Stalker.
/// 
/// LORE:
/// Upon final clearance authorization, Campus Security Guard SG-2 Dela Cruz abandons
/// his lobby booth and becomes possessed / corrupted by the university lockdown entity.
/// He relentlessly stalks the building corridors and stairwells to prevent the player
/// from escaping, periodically blowing his haunted whistle.
/// 
/// PERCEPTION SUITE:
/// 1. Vision Cone (80° horizontal FOV) + Line of Sight Raycast check.
/// 2. Dynamic Patrol Flashlight beam sweeping corridors.
/// 3. Acoustic / Hearing detection (sprinting = 22m, walking = 10m, crouch = 0m).
/// 4. Close-range personal space proximity aura (2.8m).
/// 5. Periodic & alert haunted whistle sound effect (fnx_sound-whistle-of-the-haunted-realm-287347.mp3).
/// 6. Hiding in lockers remains safe — guard investigates louvers and moves on.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SecurityGuardStalkerAI : MonoBehaviour
{
    public static SecurityGuardStalkerAI ActiveStalker { get; private set; }

    public enum GuardState
    {
        Patrol,
        Investigating,
        Chasing,
        InspectingLocker,
        Jumpscare,
        Inactive
    }

    [Header("Current State (Read Only)")]
    public GuardState currentState = GuardState.Patrol;

    [Header("Movement Speeds (Mr. X Menacing Pacing)")]
    [Tooltip("Deliberate heavy patrol walk speed.")]
    public float patrolSpeed = 2.1f;
    [Tooltip("Relentless, heavy power-walk chase speed.")]
    public float chaseSpeed = 3.6f;
    [Tooltip("Speed when investigating a suspicious noise.")]
    public float investigateSpeed = 2.4f;

    [Header("Vision & Perception")]
    [Tooltip("Horizontal Field of View angle in degrees.")]
    [Range(40f, 120f)]
    public float visionConeAngle = 80.0f;
    [Tooltip("Maximum sight distance in clear hallways.")]
    public float visionMaxDistance = 18.0f;
    [Tooltip("Close-range proximity awareness radius even if in shadow.")]
    public float proximityAwarenessRadius = 2.8f;
    [Tooltip("Distance at which the guard catches the student.")]
    public float catchDistance = 1.75f;

    [Header("Acoustic / Hearing")]
    [Tooltip("Distance at which sprinting footsteps are heard.")]
    public float hearingRangeSprint = 22.0f;
    [Tooltip("Distance at which normal walking footsteps are heard.")]
    public float hearingRangeWalk = 10.0f;

    [Header("Whistle Audio Settings")]
    [Tooltip("Haunted whistle audio clip.")]
    public AudioClip whistleClip;
    [Tooltip("Minimum seconds between periodic patrol whistle blasts.")]
    public float whistleIntervalMin = 22.0f;
    [Tooltip("Maximum seconds between periodic patrol whistle blasts.")]
    public float whistleIntervalMax = 38.0f;

    [Header("Audio References")]
    public AudioClip footstepClip;
    public AudioClip alertStingClip;
    public AudioClip voiceMumbleClip;
    public AudioClip jumpscareClip;

    [Header("Floor & Building Layout")]
    public int assignedFloor = 1;

    // ── Internal Runtime ──────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _whistleAudio;
    private AudioSource _footstepAudio;
    private AudioSource _voiceAudio;
    private Light _flashlight;

    private Transform _playerTransform;
    private PlayerMovement _playerMovement;
    private Camera _playerCam;

    private Transform _headBone;
    private Transform _rightHandBone;

    private float _nextWhistleTime = 0f;
    private float _footstepTimer = 0f;
    private float _patrolWanderTimer = 0f;
    private float _investigateTimer = 0f;
    private float _lostSightTimer = 0f;
    private float _lockerInspectTimer = 0f;

    private Vector3 _lastKnownPlayerPos;
    private Vector3 _investigateTargetPos;
    private bool _hasCaughtPlayer = false;
    private bool _isTraversingLink = false;
    private List<Bounds> _cachedRoomBounds = new List<Bounds>();

    [HideInInspector]
    public bool isDevSpawn = false;

    private int _speedHash;

    private void Awake()
    {
        if (GameModeManager.IsFreeRoamMode && !isDevSpawn && !DevDemoPanel.IsDevSpawning)
        {
            currentState = GuardState.Inactive;
            gameObject.SetActive(false);
            return;
        }

        if (ActiveStalker != null && ActiveStalker != this)
        {
            Destroy(gameObject);
            return;
        }
        ActiveStalker = this;

        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash("Speed");

        // Dynamic threat scaling (FNAF-style progression based on clearance signatures)
        if (CampusThreatDirector.Instance != null)
        {
            CampusThreatDirector.Instance.ApplyStatsToGuard(this);
        }

        SetupAudioSources();
        ConfigureNavMeshAgent();
        FindBones();
        SetupFlashlight();
        CacheRoomBounds();
    }

    private void Start()
    {
        if (GameModeManager.IsFreeRoamMode && !isDevSpawn && !DevDemoPanel.IsDevSpawning)
        {
            currentState = GuardState.Inactive;
            gameObject.SetActive(false);
            return;
        }
        if (DevDemoPanel.IsDevSpawning) isDevSpawn = true;

        FindPlayerReferences();
        _nextWhistleTime = Time.time + Random.Range(4.0f, 9.0f); // First whistle soon after emergence
        PickHallwayPatrolTarget();
    }

    private void SetupAudioSources()
    {
        // 1. Dedicated Whistle AudioSource (Spacious 3D reverberation)
        _whistleAudio = gameObject.AddComponent<AudioSource>();
        _whistleAudio.spatialBlend = 1.0f;
        _whistleAudio.minDistance = 3.0f;
        _whistleAudio.maxDistance = 45.0f; // Audible across entire floor slabs
        _whistleAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        _whistleAudio.playOnAwake = false;

        // 2. Heavy Combat Boot Footstep AudioSource
        _footstepAudio = gameObject.AddComponent<AudioSource>();
        _footstepAudio.spatialBlend = 1.0f;
        _footstepAudio.minDistance = 1.5f;
        _footstepAudio.maxDistance = 20.0f;
        _footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        _footstepAudio.playOnAwake = false;

        // 3. Voice / Grunt AudioSource
        _voiceAudio = gameObject.AddComponent<AudioSource>();
        _voiceAudio.spatialBlend = 1.0f;
        _voiceAudio.minDistance = 2.0f;
        _voiceAudio.maxDistance = 22.0f;
        _voiceAudio.playOnAwake = false;

        LoadAudioClips();
    }

    private void LoadAudioClips()
    {
#if UNITY_EDITOR
        if (whistleClip == null)
            whistleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fnx_sound-whistle-of-the-haunted-realm-287347.mp3");

        if (footstepClip == null)
            footstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");

        if (alertStingClip == null)
            alertStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");

        if (voiceMumbleClip == null)
            voiceMumbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");

        if (jumpscareClip == null)
            jumpscareClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/theProctorJumpscare.mp3");
#endif
        if (whistleClip == null)
            whistleClip = Resources.Load<AudioClip>("Sounds/fnx_sound-whistle-of-the-haunted-realm-287347");
        if (footstepClip == null)
            footstepClip = Resources.Load<AudioClip>("Sounds/footsteps walking & running");
    }

    private void ConfigureNavMeshAgent()
    {
        if (_agent == null) return;
        _agent.speed = patrolSpeed;
        _agent.stoppingDistance = 0.5f;
        _agent.acceleration = 10.0f;
        _agent.angularSpeed = 360.0f;
        _agent.autoBraking = true;
        _agent.autoTraverseOffMeshLink = false;
        _agent.height = 1.90f;
        _agent.radius = 0.40f;
    }

    private void FindBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head") && _headBone == null) _headBone = t;
            else if ((lower.Contains("righthand") || lower.Contains("right_hand") || lower.Contains("hand.r")) && _rightHandBone == null)
            {
                _rightHandBone = t;
            }
        }
    }

    private void SetupFlashlight()
    {
        Transform mount = _rightHandBone != null ? _rightHandBone : transform;

        GameObject lightGO = new GameObject("GuardPatrolFlashlight");
        lightGO.transform.SetParent(mount, false);

        if (_rightHandBone != null)
        {
            lightGO.transform.localPosition = new Vector3(0.05f, 0.10f, 0.05f);
            lightGO.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        }
        else
        {
            lightGO.transform.localPosition = new Vector3(0.25f, 1.4f, 0.4f);
            lightGO.transform.localRotation = Quaternion.identity;
        }

        _flashlight = lightGO.AddComponent<Light>();
        _flashlight.type = LightType.Spot;
        _flashlight.intensity = 2.6f;
        _flashlight.range = 22.0f;
        _flashlight.spotAngle = 75.0f;
        _flashlight.innerSpotAngle = 35.0f;
        _flashlight.color = new Color(0.92f, 0.95f, 1.0f, 1f); // Cold industrial security halogen
        _flashlight.shadows = LightShadows.Hard;
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

    private void CacheRoomBounds()
    {
        _cachedRoomBounds.Clear();
        string[] roomParents = { "COMLAB 1", "COMLAB 2", "1stFloor", "2ndFloor", "3rdFloor" };
        foreach (var pName in roomParents)
        {
            var pGO = GameObject.Find(pName);
            if (pGO == null) continue;

            foreach (var col in pGO.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                string colName = col.name.ToLower();
                if (colName.Contains("floor") || colName.Contains("room") || colName.Contains("lab") || colName.Contains("office"))
                {
                    _cachedRoomBounds.Add(col.bounds);
                }
            }
        }
    }

    private void Update()
    {
        if (GameModeManager.IsFreeRoamMode && !isDevSpawn)
        {
            currentState = GuardState.Inactive;
            gameObject.SetActive(false);
            return;
        }

        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            return;
        }

        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (currentState == GuardState.Inactive || currentState == GuardState.Jumpscare) return;

        if (_playerTransform == null) FindPlayerReferences();

        // Update floor tracking & stair traversals
        if (!_isTraversingLink)
        {
            int currentFloor = GetFloorFromY(transform.position.y);
            if (currentFloor != assignedFloor) assignedFloor = currentFloor;

            if (_agent != null && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
            {
                StartCoroutine(TraverseOffMeshLinkRoutine());
                return;
            }
        }

        // Periodic Whistle Timer in Patrol
        if (currentState == GuardState.Patrol && Time.time >= _nextWhistleTime)
        {
            PlayWhistleBlast(0.85f);
            _nextWhistleTime = Time.time + Random.Range(whistleIntervalMin, whistleIntervalMax);
        }

        // Core Perception Evaluation
        PerceptionUpdate();

        // State Machine Update
        switch (currentState)
        {
            case GuardState.Patrol:
                UpdatePatrolState();
                break;
            case GuardState.Investigating:
                UpdateInvestigateState();
                break;
            case GuardState.Chasing:
                UpdateChaseState();
                break;
            case GuardState.InspectingLocker:
                UpdateLockerInspectState();
                break;
        }

        // Locomotion Audio & Animation Sync
        UpdateLocomotionAudioAndAnim();
    }

    // ── Perception Evaluation ─────────────────────────────────────────────────

    private void PerceptionUpdate()
    {
        if (_playerTransform == null) return;

        bool isPlayerHidden = LockerHideManager.IsPlayerHidden;
        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        float dy = Mathf.Abs(_playerTransform.position.y - transform.position.y);
        bool sameFloor = dy < 3.2f;

        // Force-close any open puzzle/modal if the Guard is about to catch the player
        if (!isPlayerHidden && dy < 1.9f && distToPlayer <= catchDistance + 1.2f && PauseMenu.IsAnyActivePuzzleOrModalOpen)
        {
            PauseMenu.ForceCloseAllModalsAndPuzzles();
        }

        // Catch Check: close contact when player is not hidden in a locker
        if (!isPlayerHidden && distToPlayer <= catchDistance && dy < 1.9f && !_hasCaughtPlayer)
        {
            TriggerCatchSequence();
            return;
        }

        // 1. Line-of-Sight & Vision Cone Check
        bool canSeePlayer = CanSeePlayer();

        // 2. Acoustic / Hearing Check
        bool heardPlayerSound = false;
        Vector3 soundOrigin = Vector3.zero;

        if (!isPlayerHidden && _playerMovement != null)
        {
            bool isSprinting = _playerMovement.IsRunning;
            bool isWalking = _playerMovement.IsMoving && !_playerMovement.IsCrouching;

            if (sameFloor)
            {
                if (isSprinting && distToPlayer <= hearingRangeSprint)
                {
                    heardPlayerSound = true;
                    soundOrigin = _playerTransform.position;
                }
                else if (isWalking && distToPlayer <= hearingRangeWalk)
                {
                    heardPlayerSound = true;
                    soundOrigin = _playerTransform.position;
                }
            }
            else
            {
                // Heavy sprinting vibrations audible across open stairwell
                if (isSprinting && distToPlayer <= hearingRangeSprint * 0.65f)
                {
                    heardPlayerSound = true;
                    soundOrigin = _playerTransform.position;
                }
            }
        }

        // 3. State Transitions based on Perception
        if (canSeePlayer)
        {
            _lastKnownPlayerPos = _playerTransform.position;
            _lostSightTimer = 7.0f; // Search area for 7 seconds if vision lost

            if (currentState != GuardState.Chasing)
            {
                // Instant Alert!
                PlayWhistleBlast(1.0f);
                if (alertStingClip != null && _voiceAudio != null)
                {
                    _voiceAudio.PlayOneShot(alertStingClip, 0.85f);
                }
                currentState = GuardState.Chasing;
            }
        }
        else if (currentState == GuardState.Chasing)
        {
            // Just broke line of sight
            _lostSightTimer -= Time.deltaTime;
            if (_lostSightTimer <= 0f)
            {
                // Lost student — investigate last known location or nearby lockers
                if (isPlayerHidden)
                {
                    StartLockerInspection();
                }
                else
                {
                    _investigateTargetPos = _lastKnownPlayerPos;
                    _investigateTimer = 6.0f;
                    currentState = GuardState.Investigating;
                }
            }
        }
        else if (heardPlayerSound && currentState != GuardState.Chasing)
        {
            // Suspicious sound heard! Investigate origin
            _investigateTargetPos = soundOrigin;
            _investigateTimer = 6.0f;

            if (currentState != GuardState.Investigating)
            {
                if (voiceMumbleClip != null && _voiceAudio != null && !_voiceAudio.isPlaying)
                {
                    _voiceAudio.PlayOneShot(voiceMumbleClip, 0.75f);
                }
                currentState = GuardState.Investigating;
            }
        }
    }

    public bool CanSeePlayer()
    {
        if (_playerTransform == null) return false;
        bool isPlayerHidden = LockerHideManager.IsPlayerHidden;
        if (isPlayerHidden) return false;

        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        bool sameFloor = Mathf.Abs(transform.position.y - _playerTransform.position.y) < 1.8f;
        if (!sameFloor || distToPlayer > visionMaxDistance) return false;

        Vector3 eyePos = _headBone != null ? _headBone.position : (transform.position + Vector3.up * 1.65f);
        Vector3 toPlayer = _playerTransform.position + Vector3.up * 1.0f - eyePos;
        Vector3 flatDir = toPlayer;
        flatDir.y = 0;

        float angle = Vector3.Angle(transform.forward, flatDir);
        if (angle <= visionConeAngle * 0.5f)
        {
            if (HasClearLineOfSight(eyePos, _playerTransform.position + Vector3.up * 1.4f) ||
                HasClearLineOfSight(eyePos, _playerTransform.position + Vector3.up * 0.8f))
            {
                return true;
            }
        }

        if (distToPlayer <= proximityAwarenessRadius)
        {
            if (HasClearLineOfSight(eyePos, _playerTransform.position + Vector3.up * 1.0f))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasClearLineOfSight(Vector3 fromPos, Vector3 targetPos)
    {
        Vector3 dir = targetPos - fromPos;
        float dist = dir.magnitude;
        if (dist <= 0.2f) return true;

        Ray ray = new Ray(fromPos, dir.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, dist - 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (_playerTransform != null && !hit.transform.IsChildOf(_playerTransform) && hit.transform != transform)
            {
                return false;
            }
        }
        return true;
    }

    // ── State Handlers ────────────────────────────────────────────────────────

    private void UpdatePatrolState()
    {
        _agent.speed = patrolSpeed;

        _patrolWanderTimer -= Time.deltaTime;
        if (_patrolWanderTimer <= 0f || (_agent.isOnNavMesh && _agent.remainingDistance < 0.8f))
        {
            _patrolWanderTimer = Random.Range(5.0f, 9.0f);
            PickHallwayPatrolTarget();
        }
    }

    private void UpdateInvestigateState()
    {
        _agent.speed = investigateSpeed;

        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(_investigateTargetPos);
        }

        _investigateTimer -= Time.deltaTime;
        if (_investigateTimer <= 0f || (_agent.isOnNavMesh && _agent.remainingDistance < 0.6f))
        {
            // Finished inspecting disturbance without spotting anyone — resume patrol
            currentState = GuardState.Patrol;
            PickHallwayPatrolTarget();
        }
    }

    private void UpdateChaseState()
    {
        _agent.speed = chaseSpeed;

        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(_lastKnownPlayerPos);
        }
    }

    private void StartLockerInspection()
    {
        currentState = GuardState.InspectingLocker;
        _lockerInspectTimer = 4.5f;

        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(_lastKnownPlayerPos);
        }

        if (voiceMumbleClip != null && _voiceAudio != null)
        {
            _voiceAudio.PlayOneShot(voiceMumbleClip, 0.8f);
        }
    }

    private void UpdateLockerInspectState()
    {
        _agent.speed = 1.6f;
        _lockerInspectTimer -= Time.deltaTime;

        if (_lockerInspectTimer <= 0f)
        {
            // Gave up on locker inspection — continue patrol
            currentState = GuardState.Patrol;
            _nextWhistleTime = Time.time + Random.Range(6.0f, 12.0f);
            PickHallwayPatrolTarget();
        }
    }

    private void PickHallwayPatrolTarget()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        float minZ = -18f, maxZ = 52f;
        float x = -84.0f; // Main central corridor X coordinate
        float targetY = transform.position.y;

        // Multi-floor stalker pressure:
        // 55% chance to wander towards the floor where the player is located
        if (_playerTransform != null && Random.value < 0.55f)
        {
            targetY = _playerTransform.position.y;
        }
        else if (Random.value < 0.30f)
        {
            // 30% chance to roam randomly to another building floor
            float[] floorHeights = { 2.43f, 8.35f, 14.35f };
            targetY = floorHeights[Random.Range(0, floorHeights.Length)];
        }

        float targetZ = Random.Range(minZ, maxZ);
        Vector3 rawPoint = new Vector3(x, targetY, targetZ);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(rawPoint, out hit, 4.0f, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    // ── Audio & Animation Synchronization ─────────────────────────────────────

    private void UpdateLocomotionAudioAndAnim()
    {
        bool isMoving = (_agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.15f);

        // Sync Animator Speed
        if (_animator != null)
        {
            float normSpeed = 0f;
            if (isMoving)
            {
                normSpeed = (currentState == GuardState.Chasing) ? 2.0f : 1.0f;
            }
            _animator.SetFloat(_speedHash, normSpeed);
        }

        // Heavy Combat Boot Footsteps
        if (isMoving)
        {
            _footstepTimer -= Time.deltaTime;
            float interval = (currentState == GuardState.Chasing) ? 0.30f : 0.48f;

            if (_footstepTimer <= 0f)
            {
                _footstepTimer = interval;
                if (_footstepAudio != null && footstepClip != null)
                {
                    _footstepAudio.pitch = (currentState == GuardState.Chasing) ? Random.Range(0.92f, 1.05f) : Random.Range(0.82f, 0.95f);
                    _footstepAudio.PlayOneShot(footstepClip, (currentState == GuardState.Chasing) ? 0.85f : 0.65f);
                }
            }
        }

        // Dynamic Flashlight Corridor Sweeping
        if (_flashlight != null)
        {
            if (currentState == GuardState.Chasing)
            {
                // Focused forward beam with subtle breathing jitter
                _flashlight.transform.localRotation = Quaternion.Euler(15f, Mathf.Sin(Time.time * 12f) * 1.5f, 0f);
            }
            else if (isMoving)
            {
                // Eerie searching sweep across walls, lockers, and corners
                float sweep = Mathf.Sin(Time.time * 2.4f) * 16.0f;
                _flashlight.transform.localRotation = Quaternion.Euler(15f, sweep, 0f);
            }
        }
    }

    public void PlayWhistleBlast(float volume = 0.90f)
    {
        if (_whistleAudio != null && whistleClip != null)
        {
            _whistleAudio.pitch = Random.Range(0.96f, 1.04f);
            _whistleAudio.PlayOneShot(whistleClip, volume);
            Debug.Log("[SecurityGuardStalkerAI] Blown haunted patrol whistle!");
        }
    }

    // ── Stair Navigation ──────────────────────────────────────────────────────

    private IEnumerator TraverseOffMeshLinkRoutine()
    {
        _isTraversingLink = true;
        OffMeshLinkData linkData = _agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * _agent.baseOffset;

        float dist = Vector3.Distance(startPos, endPos);
        float speed = (currentState == GuardState.Chasing) ? chaseSpeed : patrolSpeed;
        float duration = dist / Mathf.Max(speed, 0.5f);
        float elapsed = 0f;

        Vector3 flatDir = endPos - startPos;
        flatDir.y = 0;
        Quaternion targetRot = flatDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flatDir) : transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, endPos, t);

            if (flatDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }
            yield return null;
        }

        transform.position = endPos;
        if (_agent != null && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
        {
            _agent.CompleteOffMeshLink();
        }

        assignedFloor = GetFloorFromY(transform.position.y);
        _isTraversingLink = false;
    }

    private int GetFloorFromY(float y)
    {
        if (y < 6.5f) return 1;
        if (y < 12.5f) return 2;
        return 3;
    }

    // ── Catch Confrontation ───────────────────────────────────────────────────

    private void TriggerCatchSequence()
    {
        if (DevDemoPanel.IsGodModeActive) return;
        if (GameModeManager.IsFreeRoamMode || OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (_hasCaughtPlayer) return;
        _hasCaughtPlayer = true;
        currentState = GuardState.Jumpscare;

        // Immediately close any open puzzle or modal!
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        StartCoroutine(GuardCatchRoutine());
    }

    private IEnumerator GuardCatchRoutine()
    {
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        if (jumpscareClip != null && _voiceAudio != null)
        {
            _voiceAudio.PlayOneShot(jumpscareClip, 1.0f);
        }

        PlayWhistleBlast(1.0f);

        // Turn guard to look directly at player
        if (_playerTransform != null)
        {
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        }

        // Trigger Jumpscare / Failure Sequence through NPCJumpscareManager using the Guard's own model
        SkinnedMeshRenderer guardSMR = GetComponentInChildren<SkinnedMeshRenderer>();
        if (guardSMR == null)
        {
            var boothGuard = GameObject.Find("Security_Guard_NPC") ?? FindAnyObjectByType<SecurityGuardInteract>(FindObjectsInactive.Include)?.gameObject;
            if (boothGuard != null) guardSMR = boothGuard.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }
        var jumpscareMgr = NPCJumpscareManager.Instance ?? FindAnyObjectByType<NPCJumpscareManager>();
        if (jumpscareMgr != null)
        {
            jumpscareMgr.TriggerJumpscare(guardSMR, isGuard: true);
        }
        else
        {
            // Fallback: drain stamina & anxiety surge
            if (AnxietyManager.Instance != null) AnxietyManager.Instance.AddAnxiety(25f);
            if (_playerMovement != null && _playerMovement.staminaSystem != null)
            {
                _playerMovement.staminaSystem.currentStamina = Mathf.Max(0f, _playerMovement.staminaSystem.currentStamina - 50f);
            }
            Debug.Log("[SecurityGuardStalkerAI] Student apprehended by Security Guard!");
        }

        // Wait for jumpscare screamer and recovery to finish
        yield return new WaitForSeconds(2.6f);

        // If player succumbed (Anxiety reached 100% -> Game Over), halt routine
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            yield break;
        }

        // Survival break-away: push player slightly backward (2.2m) away from guard
        if (_playerTransform != null && _playerMovement != null && _playerMovement.Controller != null)
        {
            Vector3 shoveDir = (_playerTransform.position - transform.position).normalized;
            shoveDir.y = 0f;
            if (shoveDir.sqrMagnitude < 0.01f) shoveDir = -transform.forward;
            _playerMovement.Controller.Move(shoveDir * 2.2f);
        }

        // Stern warning / catching breath cooldown (4.0s) giving the player time to run and break line of sight
        float graceTimer = 4.0f;
        while (graceTimer > 0f)
        {
            graceTimer -= Time.deltaTime;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            yield return null;
        }

        // Guard resumes pursuit or hunt
        _hasCaughtPlayer = false;
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
        }

        bool isPlayerHidden = LockerHideManager.IsPlayerHidden;
        if (_playerTransform != null && !isPlayerHidden && CanSeePlayer())
        {
            currentState = GuardState.Chasing;
        }
        else
        {
            currentState = GuardState.Investigating;
            _investigateTargetPos = (_playerTransform != null) ? _playerTransform.position : transform.position;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(_investigateTargetPos);
            }
        }

        Debug.Log("[SecurityGuardStalkerAI] Guard catch cooldown ended; resuming hunt.");
    }

    /// <summary>
    /// Updates patrol, chase, vision, and whistle cadence based on final lockdown threat level.
    /// </summary>
    public void ApplyThreatStats(float newPatrolSpeed, float newChaseSpeed, float newVisionDist, float newWhistleMin, float newWhistleMax)
    {
        patrolSpeed = newPatrolSpeed;
        chaseSpeed = newChaseSpeed;
        visionMaxDistance = newVisionDist;
        whistleIntervalMin = newWhistleMin;
        whistleIntervalMax = newWhistleMax;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.speed = (currentState == GuardState.Chasing) ? chaseSpeed : patrolSpeed;
        }

        Debug.Log($"[SecurityGuardStalkerAI] Threat stats updated: Patrol={patrolSpeed:F1}, Chase={chaseSpeed:F1}, Vision={visionMaxDistance:F1}m, Whistle={whistleIntervalMin:F0}-{whistleIntervalMax:F0}s");
    }

    public void Despawn()
    {
        currentState = GuardState.Inactive;
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }
        if (ActiveStalker == this) ActiveStalker = null;
        Destroy(gameObject);
    }

    public static void DespawnActiveGuardStalker()
    {
        if (ActiveStalker != null)
        {
            ActiveStalker.Despawn();
        }
    }
}
