using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// WeepingStudentAI — Arkham Knight / Weeping Angel / SCP-173 style student horror enemy.
/// 
/// Mechanics:
/// - Spawns during campus blackouts as an alternative to The Proctor.
/// - Quantum Lock / Freeze Mechanic:
///     * When observed by the player (illuminated by flashlight in camera view or close silhouette),
///       it instantly FREEZES completely solid like a terrifying stone statue in mid-pose.
///     * When the player looks away, turns around, or turns off the flashlight, it UNFREEZES
///       and charges rapidly toward the player with extreme speed!
///     * Turning back around instantly snaps it back to a dead halt, now dangerously closer!
/// - Catch & Jumpscare:
///     * If it catches the player from behind or in the dark, it triggers a violent in-your-face
///       screamer jumpscare, drains stamina, and adds anxiety.
///     * After the scare and whiteout shock, the student remains in the SAME SPOT (does NOT respawn
///       at the spawn point / safe zone).
/// - Confined strictly to the assigned hallway corridor of the player's floor.
/// - Despawns immediately when blackout ends or power returns.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingStudentAI : MonoBehaviour
{
    public static WeepingStudentAI ActiveWeepingStudent { get; private set; }

    public enum WeepingState
    {
        Inactive,
        Spawning,
        Stalking,
        Jumpscare,
        Despawning
    }

    [Header("Current State")]
    public WeepingState currentState = WeepingState.Inactive;

    [Header("Movement & Agility")]
    [Tooltip("Terrifying pursuit speed when the player is NOT looking.")]
    public float chaseSpeed = 7.2f;

    [Tooltip("NavMesh agent acceleration (instant surge).")]
    public float acceleration = 28.0f;

    [Tooltip("Angular turning speed.")]
    public float angularSpeed = 480.0f;

    [Tooltip("Distance at which the Weeping Student catches the player and jumpscares.")]
    public float catchDistance = 1.35f;

    [Tooltip("NavMesh stopping distance.")]
    public float stoppingDistance = 0.5f;

    [Header("Observation / Weeping Angel Rules")]
    [Tooltip("If true, requires flashlight illumination in pitch black to freeze, or within close silhouette radius.")]
    public bool requireFlashlightToFreeze = true;

    [Tooltip("Close range (meters) where the dark silhouette is clearly visible even without flashlight, freezing the enemy.")]
    public float silhouetteRadius = 2.8f;

    [Tooltip("How frequently line of sight and camera visibility are tested (seconds).")]
    public float visionCheckInterval = 0.04f;

    [Header("Assigned Hallway & Floor Limits")]
    public string assignedHallway = "Hallway_1F";
    public int assignedFloor = 1;
    public float hallwayMinZ = -16.0f;
    public float hallwayMaxZ = 30.0f;
    public float hallwayCenterX = -84.0f;
    public float hallwayHalfWidth = 2.4f;

    [Header("Audio Feedback")]
    public AudioClip footstepClip;
    public AudioClip alertStingClip;
    public AudioClip freezeStingClip;
    public AudioClip jumpscareScreamClip;
    public AudioClip staticHissClip;

    [Header("Horror & Balancing")]
    [Tooltip("Amount of anxiety added to the AnxietyMeter on jumpscare.")]
    public float jumpscareAnxietyIncrease = 15f;

    [Tooltip("Stamina penalty when jumpscared.")]
    public float staminaDrain = 30f;

    // ── Internal Runtime ──────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audioSource;
    private AudioSource _footstepAudio;

    private Transform _playerTransform;
    private PlayerMovement _playerMovement;
    private Camera _playerCam;

    private Transform _headBone;
    private Transform _spineBone;

    private bool _isObserved = false;
    public bool IsObserved => _isObserved;

    private bool _wasObservedLastFrame = false;
    private float _nextVisionCheckTime = 0f;
    private float _footstepTimer = 0f;
    private bool _hasCaughtPlayer = false;

    private int _speedHash;
    private List<Bounds> _cachedRoomBounds = new List<Bounds>();

    private void Awake()
    {
        if (ActiveWeepingStudent != null && ActiveWeepingStudent != this)
        {
            Destroy(gameObject);
            return;
        }
        ActiveWeepingStudent = this;

        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash("Speed");

        SetupAudioSources();
        LoadDefaultAudio();
        ConfigureNavMeshAgent();
        FindPlayerReferences();
        FindBones();
        CacheRoomBounds();
    }

    private void Start()
    {
        currentState = WeepingState.Spawning;
        StartCoroutine(SpawnSequence());
    }

    private void Update()
    {
        if (currentState == WeepingState.Inactive || currentState == WeepingState.Despawning) return;
        if (currentState == WeepingState.Jumpscare) return;

        // Failsafe: if power is restored by external means, despawn immediately
        if (BlackoutManager.Instance != null && !BlackoutManager.Instance.IsBlackoutActive)
        {
            Despawn();
            return;
        }

        if (_playerTransform == null) FindPlayerReferences();
        if (_playerTransform == null) return;

        // Dynamically track and update current floor boundaries based on elevation
        int currentFloor = GetFloorFromY(transform.position.y);
        if (currentFloor != assignedFloor)
        {
            UpdateFloorBoundaries(currentFloor);
        }

        // 1. Vision Check: Is the player looking at me?
        if (Time.time >= _nextVisionCheckTime)
        {
            _nextVisionCheckTime = Time.time + visionCheckInterval;
            UpdateObservedStatus();
        }

        // 2. Behavioral Update based on observation
        UpdateStalkingBehavior();

        // 3. Hallway boundary clamping
        ClampPositionToAssignedHallway();
    }

    private void SetupAudioSources()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1.0f;
        _audioSource.minDistance = 1.5f;
        _audioSource.maxDistance = 20.0f;
        _audioSource.playOnAwake = false;

        _footstepAudio = gameObject.AddComponent<AudioSource>();
        _footstepAudio.spatialBlend = 1.0f;
        _footstepAudio.minDistance = 1.0f;
        _footstepAudio.maxDistance = 12.0f;
        _footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        _footstepAudio.playOnAwake = false;
    }

    private void LoadDefaultAudio()
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
        _agent.acceleration = acceleration;
        _agent.angularSpeed = angularSpeed;
        _agent.stoppingDistance = stoppingDistance;
        _agent.radius = 0.40f;
        _agent.height = 1.80f;
        _agent.autoTraverseOffMeshLink = false;
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
            else if ((lower.Contains("spine") || lower.Contains("chest")) && _spineBone == null) _spineBone = t;
        }
    }

    private IEnumerator SpawnSequence()
    {
        yield return null;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.enabled = true;
            _agent.isStopped = false;
        }

        currentState = WeepingState.Stalking;
        Debug.Log($"[WeepingStudentAI] Spawned and stalking in {assignedHallway} at {transform.position}");

        // Play sudden ominous alert sting
        if (_audioSource != null && alertStingClip != null)
        {
            _audioSource.PlayOneShot(alertStingClip, 0.85f);
        }
    }

    // ── Vision / Observation Detection (Weeping Angel Rules) ─────────────────

    private void UpdateObservedStatus()
    {
        _wasObservedLastFrame = _isObserved;
        _isObserved = CheckIfPlayerIsLooking();

        // Sound cue on sudden freeze
        if (_isObserved && !_wasObservedLastFrame)
        {
            if (_audioSource != null && freezeStingClip != null)
            {
                _audioSource.PlayOneShot(freezeStingClip, 0.45f);
            }
        }
    }

    /// <summary>
    /// Returns true if the player has clear line of sight to the Weeping Student
    /// and either illuminates it with the flashlight or sees its close silhouette.
    /// </summary>
    public bool CheckIfPlayerIsLooking()
    {
        if (_playerCam == null || _playerTransform == null) return false;

        Vector3 eyePos = _playerCam.transform.position;
        Vector3 headPos = (_headBone != null) ? _headBone.position : (transform.position + Vector3.up * 1.55f);
        Vector3 chestPos = transform.position + Vector3.up * 1.0f;

        // 1. Frustum Check: Is head or chest within camera viewport?
        Vector3 vpHead = _playerCam.WorldToViewportPoint(headPos);
        Vector3 vpChest = _playerCam.WorldToViewportPoint(chestPos);

        bool inViewport = (vpHead.z > 0.1f && vpHead.x >= -0.05f && vpHead.x <= 1.05f && vpHead.y >= -0.05f && vpHead.y <= 1.05f) ||
                          (vpChest.z > 0.1f && vpChest.x >= -0.05f && vpChest.x <= 1.05f && vpChest.y >= -0.05f && vpChest.y <= 1.05f);

        if (!inViewport) return false;

        // 2. Line of Sight Raycast Check (not blocked by walls or closed doors)
        Vector3 toHead = headPos - eyePos;
        float distToHead = toHead.magnitude;
        bool headVisible = HasClearLineOfSight(eyePos, headPos);
        bool chestVisible = HasClearLineOfSight(eyePos, chestPos);

        if (!headVisible && !chestVisible) return false;

        // 3. Darkness / Flashlight Visibility Rule:
        // In pitch black blackout:
        // A) If within close silhouette radius (<= 2.8m), always visible!
        if (distToHead <= silhouetteRadius)
        {
            return true;
        }

        // B) If Flashlight is ON, check if within illumination cone
        bool isFlashlightOn = (FlashlightController.Instance != null && FlashlightController.Instance.IsLightOn);
        if (!isFlashlightOn)
        {
            var fl = GameObject.Find("FlashlightLight");
            if (fl != null)
            {
                var l = fl.GetComponent<Light>();
                if (l != null && l.enabled && l.intensity > 0.05f) isFlashlightOn = true;
            }
        }

        if (isFlashlightOn)
        {
            float angleToEnemy = Vector3.Angle(_playerCam.transform.forward, toHead);
            float spotAngle = 56.0f; // matches FlashlightController
            if (angleToEnemy <= (spotAngle * 0.5f + 12.0f) && distToHead <= 25.0f)
            {
                return true; // Caught in the flashlight beam!
            }
        }

        // C) In pitch darkness without flashlight and beyond close silhouette: cannot see
        if (requireFlashlightToFreeze)
        {
            return false;
        }

        return true;
    }

    private bool HasClearLineOfSight(Vector3 eyePos, Vector3 targetPos)
    {
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;
        if (dist <= 0.1f) return true;

        Ray ray = new Ray(eyePos, dir.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, dist - 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform) && hit.transform != _playerTransform)
            {
                // If it hit a thin baluster or fence post, don't count as complete wall occlusion
                if (hit.collider != null && hit.collider.bounds.size.magnitude < 0.25f)
                {
                    return true;
                }
                return false;
            }
        }
        return true;
    }

    // ── Stalking Movement & Catch Checking ───────────────────────────────────

    private void UpdateStalkingBehavior()
    {
        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        float dy = Mathf.Abs(transform.position.y - _playerTransform.position.y);

        bool playerHidden = (LockerHideManager.IsPlayerHidden);
        bool playerInRoom = IsPositionInsideAnyRoom(_playerTransform.position);

        // Catch check: Weeping Angel ONLY catches the player when NOT observed!
        // When observed, it is a frozen stone statue and cannot attack!
        if (!_isObserved && !playerHidden && !playerInRoom && distToPlayer <= catchDistance && dy < 1.9f)
        {
            TriggerCatchJumpscare();
            return;
        }

        if (_isObserved)
        {
            // ── FROZEN (Statue Mode) ─────────────────────────────────────────
            // Complete petrified stillness — cannot move an inch!
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            if (_animator != null)
            {
                _animator.speed = 0f; // Frozen in current animation frame
                _animator.SetFloat(_speedHash, 0f);
            }

            if (_footstepAudio != null && _footstepAudio.isPlaying)
            {
                _footstepAudio.Pause();
            }
        }
        else
        {
            // ── CHARGING (Weeping Angel Movement Mode) ───────────────────────
            // Player is not looking! Aggressively surge toward player!
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = chaseSpeed;

                Vector3 targetPos = GetHallwayTarget(_playerTransform.position);
                _agent.SetDestination(targetPos);
            }

            if (_animator != null)
            {
                _animator.speed = 1.35f; // Rapid terrifying pursuit motion
                _animator.SetFloat(_speedHash, 2.0f); // Full running motion
            }

            UpdateFootstepAudio();
        }
    }

    private void UpdateFootstepAudio()
    {
        if (_footstepAudio == null || footstepClip == null) return;

        bool isMoving = (_agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.2f);
        if (!isMoving) return;

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            _footstepTimer = 0.22f; // Rapid stealth scurrying footsteps
            _footstepAudio.pitch = UnityEngine.Random.Range(1.10f, 1.35f);
            _footstepAudio.PlayOneShot(footstepClip, 0.70f);
        }
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

    public bool IsInStairZone(Vector3 pos)
    {
        // Covers all 3 staircase enclosures (MainStairs, RightStairs, LeftStairs) across all floors
        if (pos.x < -94.0f || pos.x > -83.0f) return false;

        if (pos.z >= 11.0f && pos.z <= 22.0f) return true;  // MainStairs
        if (pos.z >= 45.0f && pos.z <= 54.0f) return true;  // RightStairs
        if (pos.z >= -21.0f && pos.z <= -12.0f) return true; // LeftStairs

        return false;
    }

    private Vector3 GetHallwayTarget(Vector3 desiredPos)
    {
        if (IsInStairZone(desiredPos)) return desiredPos;

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
        // Skip corridor clamping while in any of the stairwells
        if (IsInStairZone(transform.position)) return;

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

    private int GetFloorFromY(float y)
    {
        if (y < 5.5f) return 1;
        if (y < 11.5f) return 2;
        return 3;
    }

    // ── Catch & Jumpscare Sequence ────────────────────────────────────────────

    [ContextMenu("Test Catch Jumpscare")]
    public void TriggerCatchJumpscare()
    {
        if (_hasCaughtPlayer) return;
        _hasCaughtPlayer = true;
        currentState = WeepingState.Jumpscare;

        StartCoroutine(CatchJumpscareSequence());
    }

    private IEnumerator CatchJumpscareSequence()
    {
        Debug.Log("[WeepingStudentAI] PLAYER CAUGHT! Initiating Jumpscare Sequence.");

        // 1. Freeze Agent immediately
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }

        // 2. Freeze player movement and lock camera facing Weeping Student's face
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isJumpscareCameraOverride = true;
        }

        if (_playerCam == null) _playerCam = Camera.main;

        Vector3 camPos = _playerCam.transform.position;
        Vector3 camBaseLocalPos = _playerCam.transform.localPosition;
        float startFOV = _playerCam.fieldOfView;
        float jumpscareFOV = 40.0f;

        // Turn Weeping Student face directly toward camera squarely
        Vector3 camFwd = _playerCam.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;
        camFwd.Normalize();

        Vector3 faceDir = -camFwd; // Face directly back at player camera
        transform.rotation = Quaternion.LookRotation(faceDir);

        // Compute face offset relative to root (face center is 0.08m below head bone top)
        Vector3 faceOffset = (_headBone != null) ? (_headBone.position - Vector3.up * 0.08f - transform.position) : new Vector3(0f, 1.55f * 0.90f, 0f);

        // Desired face position in world space:
        // Positioned 0.55m in front of camera, elevated +0.04m above camera eye-level
        // Frames face center at exact viewport center (0.50, 0.50), top of skull at y=0.97
        Vector3 desiredFacePos = camPos + camFwd * 0.55f + Vector3.up * 0.04f;

        Vector3 targetPos = desiredFacePos - faceOffset;
        Vector3 initialPos = transform.position;

        // 3. Audio Horror Screamer
        if (_audioSource != null)
        {
            if (jumpscareScreamClip == null) LoadDefaultAudio();
            if (jumpscareScreamClip != null)
            {
                _audioSource.spatialBlend = 0f;
                _audioSource.volume = 1.0f;
                _audioSource.PlayOneShot(jumpscareScreamClip, 1.0f);
            }
        }

        // 4. Initial In-Your-Face Snap-Slam into Camera
        float elapsed = 0f;
        float initialLungeDuration = 0.50f;

        while (elapsed < initialLungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / initialLungeDuration;
            float ease = Mathf.Pow(t, 0.35f);

            transform.position = Vector3.Lerp(initialPos, targetPos, ease);
            _playerCam.fieldOfView = Mathf.Lerp(startFOV, jumpscareFOV, ease);

            Vector3 currentFacePos = (_headBone != null) ? (_headBone.position - Vector3.up * 0.08f) : (transform.position + faceOffset);
            Vector3 lookDir = currentFacePos - _playerCam.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                _playerCam.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            float shakeX = UnityEngine.Random.Range(-0.025f, 0.025f);
            float shakeY = UnityEngine.Random.Range(-0.025f, 0.025f);
            _playerCam.transform.localPosition = camBaseLocalPos + new Vector3(shakeX, shakeY, 0f);

            yield return null;
        }

        // 5. Add Anxiety and drain stamina
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(jumpscareAnxietyIncrease);
            Debug.Log($"[WeepingStudentAI] Added +{jumpscareAnxietyIncrease} anxiety on Weeping Student catch!");
        }

        var stamina = FindAnyObjectByType<StaminaSystem>();
        if (stamina != null)
        {
            stamina.currentStamina = Mathf.Max(5f, stamina.currentStamina - staminaDrain);
        }

        // Shudder in place for 0.7s
        float shudderTime = 0f;
        while (shudderTime < 0.70f)
        {
            shudderTime += Time.deltaTime;
            float shakeX = UnityEngine.Random.Range(-0.020f, 0.020f);
            float shakeY = UnityEngine.Random.Range(-0.020f, 0.020f);
            _playerCam.transform.localPosition = camBaseLocalPos + new Vector3(shakeX, shakeY, 0f);
            yield return null;
        }

        if (_playerCam != null) _playerCam.transform.localPosition = camBaseLocalPos;

        // 6. Whiteout Flash Overlay (Student remains in same spot!)
        yield return StartCoroutine(WhiteoutRoutine());

        // 7. Complete Encounter & Despawn
        TheProctorManager.Instance?.OnProctorCompletedEncounter();
        Despawn();
    }

    private IEnumerator WhiteoutRoutine()
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
            whiteoutImg.color = new Color(1f, 1f, 1f, 1f);
            whiteoutImg.raycastTarget = false;
        }

        if (_audioSource != null && staticHissClip != null)
        {
            _audioSource.PlayOneShot(staticHissClip, 0.7f);
        }

        // Hold solid white for 0.35s during shock
        yield return new WaitForSeconds(0.35f);

        // NOTE: Student remains in the same spot, does NOT teleport back to spawn point!

        float fadeElapsed = 0f;
        float fadeDuration = 1.0f;
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

    // ── Room & Hallway Filtering ─────────────────────────────────────────────

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
                    b.Expand(new Vector3(-0.15f, 0f, -0.15f));
                    _cachedRoomBounds.Add(b);
                }
            }
        }
    }

    private bool IsPositionInsideAnyRoom(Vector3 pos)
    {
        if (_cachedRoomBounds != null && _cachedRoomBounds.Count > 0)
        {
            foreach (var b in _cachedRoomBounds)
            {
                if (b.Contains(pos)) return true;
            }
        }
        return false;
    }

    // ── Despawn ───────────────────────────────────────────────────────────────

    public void Despawn()
    {
        currentState = WeepingState.Despawning;

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }

        if (ActiveWeepingStudent == this) ActiveWeepingStudent = null;

        Debug.Log("[WeepingStudentAI] Despawned.");
        Destroy(gameObject);
    }

    public static void DespawnActiveWeepingStudent()
    {
        if (ActiveWeepingStudent != null)
        {
            ActiveWeepingStudent.Despawn();
        }
    }

    private void OnDestroy()
    {
        if (ActiveWeepingStudent == this) ActiveWeepingStudent = null;
    }
}
