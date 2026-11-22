using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SecurityGuardStalkerAI — "Mr. X" Style Relentless Endgame Stalker.
///
/// DESIGN PILLARS (Resident Evil 2 Mr. X):
///   • Persistent map-wide presence — patrols ALL floors, never stops, never despawns.
///   • Acoustic-first detection: sprinting is loud, walking is risky, crouching is silent.
///   • LOS-based sight cone: break LOS + hide in lockers to stay safe.
///   • Last-Known-Position chase: when LOS breaks, hunts to last seen spot then sweeps.
///   • CATCH = INSTANT GAME OVER + scene restart. No second chances. No grace period.
///   • Fair audio warning system:
///       – Heavy boot footsteps (audible up to 20 m).
///       – Periodic haunted whistle (fnx_sound-whistle-of-the-haunted-realm-287347).
///       – Alert sting stinger the moment he spots you (brief window to flee).
///       – Proximity heartbeat through AnxietyManager when he is < 8 m.
///   • Only activates after SecurityGuardEndgameManager.isEndgameActive = true
///     (i.e., all signing complete, player is heading to the main gate).
///
/// ACTIVATION:
///   SecurityGuardEndgameManager.CompleteEndgameActivation() → SpawnOrActivateStalker()
///   Stalker starts at ground floor corridor and patrols upward pressure.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SecurityGuardStalkerAI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static SecurityGuardStalkerAI ActiveStalker { get; private set; }

    // ── State Machine ──────────────────────────────────────────────────────────
    public enum GuardState
    {
        Patrol,         // Systematic floor-by-floor sweep
        Investigating,  // Moving to a sound source or last-known position
        Chasing,        // Direct LOS pursuit; speed increases
        InspectingLocker, // Loitering at a locker the player just hid in
        KillSequence,   // Catch animation → immediate game over
        Inactive        // Waiting to be activated
    }

    [Header("── State (Read Only) ──")]
    public GuardState currentState = GuardState.Inactive;

    // ── Movement ───────────────────────────────────────────────────────────────
    [Header("── Movement Speeds ──")]
    [Tooltip("Slow, menacing patrol walk — player can outrun at a walk.")]
    public float patrolSpeed     = 1.8f;
    [Tooltip("Power-walk pursuit — player must sprint or use doors/stairs cleverly.")]
    public float chaseSpeed      = 3.4f;
    [Tooltip("Speed when moving to sound origin.")]
    public float investigateSpeed = 2.2f;

    // ── Perception ─────────────────────────────────────────────────────────────
    [Header("── Vision & Perception ──")]
    [Range(40f, 110f)]
    public float visionConeAngle        = 75.0f;  // Horizontal FOV
    public float visionMaxDistance      = 16.0f;  // Clear-hallway sight range
    public float proximityAwarenessRadius = 2.5f; // Ambient awareness in darkness

    [Header("── Hearing ──")]
    [Tooltip("Distance sprinting footsteps are detected.")]
    public float hearingRangeSprint = 20.0f;
    [Tooltip("Distance walking footsteps are detected.")]
    public float hearingRangeWalk   = 9.0f;
    // Crouching = 0 (completely silent to the guard)

    [Header("── Catch ──")]
    [Tooltip("Distance at which the guard grabs the student.")]
    public float catchDistance = 1.6f;

    // ── Audio ──────────────────────────────────────────────────────────────────
    [Header("── Audio Clips ──")]
    public AudioClip whistleClip;       // fnx_sound-whistle-of-the-haunted-realm-287347
    public AudioClip footstepClip;
    public AudioClip alertStingClip;    // When guard first spots you
    public AudioClip voiceMumbleClip;   // While investigating
    public AudioClip killVoiceClip;     // Shout when grabbing player

    [Header("── Whistle Cadence ──")]
    public float whistleIntervalMin = 18.0f;
    public float whistleIntervalMax = 32.0f;

    // ── Floor Layout ───────────────────────────────────────────────────────────
    [Header("── Floor Layout ──")]
    [Tooltip("Approximate Y-heights of each floor's walkable surface.")]
    public float[] floorYHeights = { 2.43f, 8.35f, 14.35f };

    // ── Dev Spawn flag ─────────────────────────────────────────────────────────
    [HideInInspector]
    public bool isDevSpawn = false;

    // ── Private Runtime ────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator     _animator;
    private AudioSource  _whistleAudio;
    private AudioSource  _footstepAudio;
    private AudioSource  _voiceAudio;
    private Light        _flashlight;

    private Transform     _playerTransform;
    private PlayerMovement _playerMovement;
    private Camera        _playerCam;
    private Transform     _headBone;
    private Transform     _rightHandBone;

    // Timers & tracking
    private float _nextWhistleTime     = 0f;
    private float _footstepTimer       = 0f;
    private float _patrolWanderTimer   = 0f;
    private float _investigateTimer    = 0f;
    private float _lostSightTimer      = 0f;
    private float _lockerInspectTimer  = 0f;
    private float _proximityHeartbeatTimer = 0f;

    private Vector3 _lastKnownPlayerPos;
    private Vector3 _investigateTargetPos;

    // Floor pressure system (Mr. X goes where the player is)
    private int   _guardFloor          = 1;
    private int   _patrolTargetFloor   = 1;
    private bool  _isTraversingLink    = false;
    private bool  _killInProgress      = false;

    // Animator hashes
    private int _speedHash;
    private int _alertHash;

    // ══════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // In Calm Free-Roam, stay dormant
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

        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash("Speed");
        _alertHash = Animator.StringToHash("Alert"); // optional, graceful if missing

        if (CampusThreatDirector.Instance != null)
            CampusThreatDirector.Instance.ApplyStatsToGuard(this);

        SetupAudioSources();
        ConfigureNavMeshAgent();
        FindBones();
        SetupFlashlight();
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

        // First whistle arrives soon after emergence — warns the player
        _nextWhistleTime = Time.time + Random.Range(3.0f, 7.0f);

        // Start patrol on the ground floor
        _guardFloor        = 1;
        _patrolTargetFloor = DetermineInitialPressureFloor();
        currentState       = GuardState.Patrol;
        PickPatrolWaypoint();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MAIN UPDATE LOOP
    // ══════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        // Dev Calm-Mode kill switch
        if (GameModeManager.IsFreeRoamMode && DevDemoPanel.IsCalmMode && !isDevSpawn)
        {
            currentState = GuardState.Inactive;
            gameObject.SetActive(false);
            return;
        }

        // Yield during cutscenes / game-over
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            StopAgent();
            return;
        }
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (_killInProgress) return;
        if (currentState == GuardState.Inactive || currentState == GuardState.KillSequence) return;

        // Re-find player if lost (scene transition safety)
        if (_playerTransform == null) FindPlayerReferences();

        // Track current floor from Y
        _guardFloor = FloorFromY(transform.position.y);

        // Off-mesh link traversal (stairs)
        if (!_isTraversingLink && _agent != null && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
        {
            StartCoroutine(SmoothOffMeshLinkRoutine());
            return;
        }

        // Periodic patrol whistle
        if (currentState == GuardState.Patrol && Time.time >= _nextWhistleTime)
        {
            PlayWhistle(0.80f);
            _nextWhistleTime = Time.time + Random.Range(whistleIntervalMin, whistleIntervalMax);
        }

        // Proximity heartbeat warning (fair warning system)
        UpdateProximityWarning();

        // Perception
        PerceptionUpdate();

        // State machine
        switch (currentState)
        {
            case GuardState.Patrol:           UpdatePatrol();          break;
            case GuardState.Investigating:    UpdateInvestigate();     break;
            case GuardState.Chasing:          UpdateChase();           break;
            case GuardState.InspectingLocker: UpdateLockerInspect();   break;
        }

        // Locomotion
        UpdateLocomotion();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PERCEPTION
    // ══════════════════════════════════════════════════════════════════════════

    private void PerceptionUpdate()
    {
        if (_playerTransform == null) return;

        bool hidden   = LockerHideManager.IsPlayerHidden;
        float dist    = Vector3.Distance(transform.position, _playerTransform.position);
        float dy      = Mathf.Abs(_playerTransform.position.y - transform.position.y);
        bool sameFloor = dy < 3.5f;

        // ── CATCH CHECK ─────────────────────────────────────────────────────
        if (!hidden && dist <= catchDistance && dy < 2.0f && !_killInProgress)
        {
            // Force-close any open UI so the kill plays cleanly
            PauseMenu.ForceCloseAllModalsAndPuzzles();
            StartKillSequence();
            return;
        }

        // ── VISION ──────────────────────────────────────────────────────────
        bool canSee = CanSeePlayer();

        // ── HEARING ─────────────────────────────────────────────────────────
        bool heardPlayer  = false;
        Vector3 soundPos  = Vector3.zero;

        if (!hidden && _playerMovement != null)
        {
            bool sprinting = _playerMovement.IsRunning;
            bool walking   = _playerMovement.IsMoving && !_playerMovement.IsCrouching;

            if (sameFloor)
            {
                if (sprinting && dist <= hearingRangeSprint) { heardPlayer = true; soundPos = _playerTransform.position; }
                else if (walking && dist <= hearingRangeWalk) { heardPlayer = true; soundPos = _playerTransform.position; }
            }
            else
            {
                // Sprinting audible through open stairwell shaft (reduced range)
                if (sprinting && dist <= hearingRangeSprint * 0.55f) { heardPlayer = true; soundPos = _playerTransform.position; }
            }
        }

        // ── STATE TRANSITIONS ────────────────────────────────────────────────
        if (canSee)
        {
            _lastKnownPlayerPos = _playerTransform.position;
            _lostSightTimer     = 9.0f;  // Keeps hunting for 9 s after LOS breaks

            if (currentState != GuardState.Chasing)
            {
                // ALERT — blow whistle instantly so player hears him locking on
                PlayWhistle(1.0f);
                if (alertStingClip != null && _voiceAudio != null)
                    _voiceAudio.PlayOneShot(alertStingClip, 0.90f);
                currentState = GuardState.Chasing;
                SetAgentSpeed(chaseSpeed);
            }
        }
        else if (currentState == GuardState.Chasing)
        {
            _lostSightTimer -= Time.deltaTime;

            if (_lostSightTimer <= 0f)
            {
                // Gave up direct chase — investigate last known position
                if (hidden)
                    BeginLockerInspect();
                else
                    BeginInvestigate(_lastKnownPlayerPos, 10.0f);
            }
        }
        else if (heardPlayer && currentState != GuardState.Chasing)
        {
            if (currentState != GuardState.Investigating)
            {
                if (voiceMumbleClip != null && _voiceAudio != null && !_voiceAudio.isPlaying)
                    _voiceAudio.PlayOneShot(voiceMumbleClip, 0.70f);
            }
            // Refresh investigate target to sound origin
            BeginInvestigate(soundPos, 7.0f);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STATE HANDLERS
    // ══════════════════════════════════════════════════════════════════════════

    // ── PATROL ────────────────────────────────────────────────────────────────
    private void UpdatePatrol()
    {
        SetAgentSpeed(patrolSpeed);

        _patrolWanderTimer -= Time.deltaTime;
        bool arrivedAtWaypoint = _agent != null && _agent.isOnNavMesh
                                 && !_agent.pathPending
                                 && _agent.remainingDistance <= _agent.stoppingDistance + 0.25f;

        if (_patrolWanderTimer <= 0f || arrivedAtWaypoint)
        {
            _patrolWanderTimer = Random.Range(3.0f, 7.0f);
            PickPatrolWaypoint();
        }
    }

    // ── INVESTIGATE ───────────────────────────────────────────────────────────
    private void UpdateInvestigate()
    {
        SetAgentSpeed(investigateSpeed);

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(_investigateTargetPos);

        bool arrived = _agent != null && _agent.isOnNavMesh
                       && !_agent.pathPending
                       && _agent.remainingDistance <= _agent.stoppingDistance + 0.35f;

        _investigateTimer -= Time.deltaTime;

        if (_investigateTimer <= 0f || arrived)
        {
            // When investigation finishes without finding the player, do a short sweep
            // around the area then resume patrol pressure toward the player's floor.
            _patrolTargetFloor = PlayerFloor();
            currentState       = GuardState.Patrol;
            PickPatrolWaypoint();
        }
    }

    // ── CHASE ─────────────────────────────────────────────────────────────────
    private void UpdateChase()
    {
        SetAgentSpeed(chaseSpeed);

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(_lastKnownPlayerPos);

        // While chasing, continuously whistle if interval fires — terrifying
        if (Time.time >= _nextWhistleTime)
        {
            PlayWhistle(0.95f);
            _nextWhistleTime = Time.time + Random.Range(8.0f, 16.0f);
        }
    }

    // ── LOCKER INSPECT ────────────────────────────────────────────────────────
    private void UpdateLockerInspect()
    {
        SetAgentSpeed(1.4f);
        _lockerInspectTimer -= Time.deltaTime;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(_lastKnownPlayerPos);

        // While inspecting, mumble occasionally
        if (_lockerInspectTimer <= 0f)
        {
            // Done inspecting — resume floor-pressure patrol
            _patrolTargetFloor = PlayerFloor();
            currentState = GuardState.Patrol;
            _nextWhistleTime = Time.time + Random.Range(5.0f, 10.0f);
            PickPatrolWaypoint();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MR. X KILL SEQUENCE — INSTANT GAME OVER
    // ══════════════════════════════════════════════════════════════════════════

    private void StartKillSequence()
    {
        if (_killInProgress) return;
        _killInProgress = true;
        currentState = GuardState.KillSequence;

        StopAgent();
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        StartCoroutine(KillRoutine());
    }

    private IEnumerator KillRoutine()
    {
        // 1. Force close any open menus, inspection modals, or puzzles
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        // 2. Halt & disable NavMeshAgent so physics/agent don't fight custom animation/placement
        if (_agent != null)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            _agent.enabled = false;
        }

        // 3. Ensure player references are valid
        if (_playerTransform == null || _playerMovement == null || _playerCam == null)
            FindPlayerReferences();

        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isJumpscareCameraOverride = true;
        }

        Camera cam = (_playerCam != null) ? _playerCam : Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();

        Vector3 camBaseLocalPos = (cam != null) ? cam.transform.localPosition : Vector3.zero;
        float startFOV = (cam != null) ? cam.fieldOfView : 60f;
        float scareFOV = 36.0f; // Extreme in-your-face focal zoom

        // 4. Align Guard and Camera in physical space (NO duplicate or detached head!)
        Vector3 camPos = (cam != null) ? cam.transform.position : (_playerTransform != null ? _playerTransform.position + Vector3.up * 1.6f : transform.position + Vector3.forward * 1.5f);
        Vector3 startGuardPos = transform.position;

        // Flat direction from guard towards the player camera
        Vector3 toCamFlat = (camPos - startGuardPos);
        toCamFlat.y = 0f;
        if (toCamFlat.sqrMagnitude < 0.001f) toCamFlat = transform.forward;
        toCamFlat.Normalize();

        // Rotate guard directly towards player
        transform.rotation = Quaternion.LookRotation(toCamFlat);

        // Estimate exact head height of the guard's actual model
        float headOffset = (_headBone != null) ? Mathf.Abs(_headBone.position.y - transform.position.y) : 1.72f;
        if (headOffset < 1.1f || headOffset > 2.3f) headOffset = 1.72f;

        // Target lunge position: Guard's actual body steps violently forward to 0.62m right in front of camera
        Vector3 targetGuardPos = camPos - toCamFlat * 0.62f;
        targetGuardPos.y = startGuardPos.y; // Keep guard grounded on floor

        // Force player's camera to look straight up into the Guard's menacing eyes
        Vector3 guardFacePos = targetGuardPos + Vector3.up * headOffset;
        Vector3 toGuardFace = (guardFacePos - camPos).normalized;
        Quaternion startCamRot = (cam != null) ? cam.transform.rotation : Quaternion.identity;
        Quaternion targetCamRot = Quaternion.LookRotation(toGuardFace);

        // 5. Aim flashlight into player camera / upwards into guard's face for terrifying underlighting
        if (_flashlight != null)
        {
            _flashlight.enabled = true;
            _flashlight.intensity = 5.5f;
            _flashlight.spotAngle = 85.0f;
            _flashlight.range = 10.0f;
            _flashlight.color = new Color(0.96f, 0.98f, 1.0f);
            _flashlight.transform.LookAt(camPos);
        }

        // 6. Audio Screamer + Shrill Haunted Whistle
        if (_voiceAudio != null)
        {
            _voiceAudio.spatialBlend = 0f; // 2D Full volume right in player's ears
            if (killVoiceClip != null) _voiceAudio.PlayOneShot(killVoiceClip, 1.0f);
            else if (alertStingClip != null) _voiceAudio.PlayOneShot(alertStingClip, 1.0f);
        }
        PlayWhistle(1.0f);

        // 7. Trigger animator into forward locomotion
        if (_animator != null)
        {
            _animator.SetFloat(_speedHash, 2.0f);
            _animator.speed = 1.4f;
        }

        // 8. Create Blood/Crimson Flash Overlay on HUD canvas (NO floating head!)
        GameObject flashGO = null;
        Image flashImg = null;
        var canvas = GameObject.Find("HudCanvas")?.GetComponent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            flashGO = new GameObject("GuardKillBloodFlash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(canvas.transform, false);
            var rt = flashGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            flashImg = flashGO.GetComponent<Image>();
            flashImg.color = new Color(0.72f, 0.02f, 0.02f, 0.85f);
            flashImg.raycastTarget = false;
        }

        // 9. Multi-phase visceral in-world jumpscare animation
        float duration = 1.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Phase A: Rapid whiplash camera rotation in first 0.20s to force stare into Guard's face
            float whipT = Mathf.Clamp01(elapsed / 0.20f);
            float whipEase = Mathf.SmoothStep(0f, 1f, whipT);
            if (cam != null)
            {
                cam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, whipEase);
            }

            // Phase B: Guard lunges his actual body directly towards camera lens
            float lungeT = Mathf.Clamp01(elapsed / 0.55f);
            float lungeEase = Mathf.Pow(lungeT, 0.40f); // Fast initial snap-lunge!
            transform.position = Vector3.Lerp(startGuardPos, targetGuardPos, lungeEase);

            // Phase C: Dramatic FOV zoom & violent camera tremor
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(startFOV, scareFOV, lungeEase);

                // Violent screen shudder / struggle trauma shake
                float shakeIntensity = Mathf.Lerp(0.045f, 0.015f, t);
                float rx = Random.Range(-shakeIntensity, shakeIntensity);
                float ry = Random.Range(-shakeIntensity, shakeIntensity);
                cam.transform.localPosition = camBaseLocalPos + new Vector3(rx, ry, 0f);
            }

            // Phase D: Blood vignette flash pulse
            if (flashImg != null)
            {
                float flashA = (t < 0.25f)
                    ? Mathf.Lerp(0.85f, 0.35f, t / 0.25f)
                    : Mathf.Lerp(0.35f, 0.92f, (t - 0.25f) / 0.75f); // fades into dark crimson killout
                flashImg.color = new Color(0.68f, 0.02f, 0.02f, Mathf.Clamp01(flashA));
            }

            yield return null;
        }

        // 10. Clean up temporary flash before triggering GameOverUI
        if (flashGO != null) Destroy(flashGO);

        if (cam != null)
        {
            cam.transform.localPosition = camBaseLocalPos;
            cam.fieldOfView = startFOV;
        }

        // 11. Trigger Instant Game Over + Run Restart (Resident Evil 2 Mr. X style)
        var gameOverUI = GameOverUI.Instance ?? FindAnyObjectByType<GameOverUI>();
        if (gameOverUI != null && !GameOverUI.IsCutsceneActive)
        {
            gameOverUI.HandleGameOver();
        }
        else if (!GameOverUI.IsCutsceneActive)
        {
            if (AnxietyManager.Instance != null)
                AnxietyManager.Instance.SetAnxiety(AnxietyManager.Instance.maxAnxiety);

            yield return new WaitForSeconds(3.0f);
            if (!GameOverUI.IsCutsceneActive)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PATROL WAYPOINT SYSTEM (Mr. X floor-pressure algorithm)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Picks a NavMesh-sampled hallway waypoint.
    /// 60 % chance to target the player's floor, 25 % chance to escalate one floor up,
    /// 15 % chance to sweep the current floor again.
    /// </summary>
    private void PickPatrolWaypoint()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        // Decide target floor
        float roll = Random.value;
        if (roll < 0.60f)
            _patrolTargetFloor = PlayerFloor();
        else if (roll < 0.85f)
            _patrolTargetFloor = Mathf.Clamp(PlayerFloor() + Random.Range(-1, 2), 1, 3);
        // else keep current floor

        float targetY = FloorToY(_patrolTargetFloor);

        // Main corridor X range of the building
        float corridorX = -84.0f;
        float minZ = -20f, maxZ = 55f;
        float targetZ = Random.Range(minZ, maxZ);

        Vector3 rawPoint = new Vector3(corridorX, targetY, targetZ);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(rawPoint, out hit, 5.0f, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
        else
        {
            // Widen the search if corridor X fails (wing corridors, classrooms)
            rawPoint.x += Random.Range(-6f, 6f);
            if (NavMesh.SamplePosition(rawPoint, out hit, 8.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        _patrolWanderTimer = Random.Range(4.0f, 8.0f);
    }

    private void BeginInvestigate(Vector3 target, float duration)
    {
        _investigateTargetPos = target;
        _investigateTimer = duration;

        if (currentState != GuardState.Investigating)
            currentState = GuardState.Investigating;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(target);
    }

    private void BeginLockerInspect()
    {
        currentState = GuardState.InspectingLocker;
        _lockerInspectTimer = 5.5f;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.SetDestination(_lastKnownPlayerPos);

        if (voiceMumbleClip != null && _voiceAudio != null)
            _voiceAudio.PlayOneShot(voiceMumbleClip, 0.85f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LINE OF SIGHT
    // ══════════════════════════════════════════════════════════════════════════

    public bool CanSeePlayer()
    {
        if (_playerTransform == null) return false;
        if (LockerHideManager.IsPlayerHidden) return false;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        float dy   = Mathf.Abs(transform.position.y - _playerTransform.position.y);

        if (dy > 2.0f || dist > visionMaxDistance) return false;

        Vector3 eyePos = _headBone != null
            ? _headBone.position
            : transform.position + Vector3.up * 1.65f;

        Vector3 toPlayer = (_playerTransform.position + Vector3.up * 1.0f) - eyePos;
        Vector3 flatDir  = new Vector3(toPlayer.x, 0, toPlayer.z);

        float angle = Vector3.Angle(transform.forward, flatDir);
        if (angle <= visionConeAngle * 0.5f)
        {
            if (HasClearLOS(eyePos, _playerTransform.position + Vector3.up * 1.4f) ||
                HasClearLOS(eyePos, _playerTransform.position + Vector3.up * 0.7f))
                return true;
        }

        // Proximity aura — ambient dark-area awareness
        if (dist <= proximityAwarenessRadius)
            return HasClearLOS(eyePos, _playerTransform.position + Vector3.up * 1.0f);

        return false;
    }

    private bool HasClearLOS(Vector3 from, Vector3 to)
    {
        Vector3 dir  = to - from;
        float dist   = dir.magnitude;
        if (dist < 0.2f) return true;

        if (Physics.Raycast(new Ray(from, dir.normalized), out RaycastHit hit, dist - 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (_playerTransform != null && hit.transform.IsChildOf(_playerTransform)) return true;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) return true;
            return false;
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AUDIO & ANIMATION
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateLocomotion()
    {
        if (_agent == null) return;
        bool moving = _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.magnitude > 0.15f;

        // Animator
        if (_animator != null)
        {
            float normSpeed = 0f;
            if (moving)
                normSpeed = (currentState == GuardState.Chasing) ? 2.0f : 1.0f;
            _animator.SetFloat(_speedHash, normSpeed);
        }

        // Footsteps — heavy, deliberate, audible
        if (moving)
        {
            _footstepTimer -= Time.deltaTime;
            float interval = currentState == GuardState.Chasing ? 0.42f : 0.58f;
            if (_footstepTimer <= 0f)
            {
                _footstepTimer = interval;
                if (_footstepAudio != null && footstepClip != null)
                {
                    float vol   = currentState == GuardState.Chasing ? 0.90f : 0.70f;
                    float pitch = currentState == GuardState.Chasing
                        ? Random.Range(0.90f, 1.02f)
                        : Random.Range(0.80f, 0.93f);
                    _footstepAudio.pitch = pitch;
                    _footstepAudio.PlayOneShot(footstepClip, vol);
                }
            }
        }
        else _footstepTimer = 0f;

        // Flashlight sweep
        UpdateFlashlight();
    }

    private void UpdateFlashlight()
    {
        if (_flashlight == null) return;

        if (currentState == GuardState.Chasing || currentState == GuardState.KillSequence)
        {
            // Locked forward with subtle breathing tremble
            _flashlight.intensity = 3.0f;
            _flashlight.spotAngle = 70.0f;
            _flashlight.transform.localRotation = Quaternion.Euler(
                12f, Mathf.Sin(Time.time * 14f) * 1.8f, 0f);
        }
        else if (_agent != null && _agent.isOnNavMesh && _agent.velocity.magnitude > 0.1f)
        {
            // Eerie searching corridor sweep
            _flashlight.intensity = 2.4f;
            _flashlight.spotAngle = 78.0f;
            float sweep = Mathf.Sin(Time.time * 2.2f) * 18.0f;
            _flashlight.transform.localRotation = Quaternion.Euler(12f, sweep, 0f);
        }
        else
        {
            // Standing still — slow threatening pan
            _flashlight.intensity = 2.0f;
            float pan = Mathf.Sin(Time.time * 0.8f) * 25.0f;
            _flashlight.transform.localRotation = Quaternion.Euler(10f, pan, 0f);
        }
    }

    private void UpdateProximityWarning()
    {
        // Adds subtle anxiety pulse when guard is very close — fair "get out now" warning
        if (_playerTransform == null || AnxietyManager.Instance == null) return;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        if (dist < 8.0f && currentState != GuardState.Inactive)
        {
            _proximityHeartbeatTimer -= Time.deltaTime;
            if (_proximityHeartbeatTimer <= 0f)
            {
                // Small anxiety tickle — just enough to feel the heartbeat, not punishing
                float proximityAnxiety = Mathf.Lerp(0.3f, 0.05f, dist / 8.0f);
                if (!GameModeManager.IsFreeRoamMode || !DevDemoPanel.IsCalmMode)
                    AnxietyManager.Instance.AddAnxiety(proximityAnxiety);
                _proximityHeartbeatTimer = 0.6f;
            }
        }
    }

    public void PlayWhistle(float volume = 0.85f)
    {
        if (_whistleAudio != null && whistleClip != null)
        {
            _whistleAudio.pitch = Random.Range(0.95f, 1.05f);
            _whistleAudio.PlayOneShot(whistleClip, volume);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STAIR / OFF-MESH LINK TRAVERSAL — smooth, no teleporting
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator SmoothOffMeshLinkRoutine()
    {
        _isTraversingLink = true;
        OffMeshLinkData link = _agent.currentOffMeshLinkData;
        Vector3 start = transform.position;
        Vector3 end   = link.endPos + Vector3.up * _agent.baseOffset;

        float dist     = Vector3.Distance(start, end);
        float speed    = currentState == GuardState.Chasing ? chaseSpeed : patrolSpeed;
        float duration = dist / Mathf.Max(speed, 0.5f);
        float elapsed  = 0f;

        Vector3 flatDir = new Vector3(end.x - start.x, 0, end.z - start.z);
        Quaternion targetRot = flatDir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(flatDir) : transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);

            // Footstep during stair walk
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f && _footstepAudio != null && footstepClip != null)
            {
                _footstepTimer = 0.45f;
                _footstepAudio.pitch = Random.Range(0.82f, 0.94f);
                _footstepAudio.PlayOneShot(footstepClip, 0.65f);
            }

            yield return null;
        }

        transform.position = end;
        if (_agent != null && _agent.isOnNavMesh && _agent.isOnOffMeshLink)
            _agent.CompleteOffMeshLink();

        _guardFloor       = FloorFromY(transform.position.y);
        _isTraversingLink = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SETUP HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void SetupAudioSources()
    {
        // 1. Whistle (spacious 3D — audible across an entire floor slab)
        _whistleAudio = gameObject.AddComponent<AudioSource>();
        _whistleAudio.spatialBlend = 1.0f;
        _whistleAudio.minDistance  = 2.0f;
        _whistleAudio.maxDistance  = 50.0f;
        _whistleAudio.rolloffMode  = AudioRolloffMode.Logarithmic;
        _whistleAudio.playOnAwake  = false;

        // 2. Heavy boot footsteps
        _footstepAudio = gameObject.AddComponent<AudioSource>();
        _footstepAudio.spatialBlend = 1.0f;
        _footstepAudio.minDistance  = 1.5f;
        _footstepAudio.maxDistance  = 22.0f;
        _footstepAudio.rolloffMode  = AudioRolloffMode.Logarithmic;
        _footstepAudio.playOnAwake  = false;

        // 3. Voice / grunt
        _voiceAudio = gameObject.AddComponent<AudioSource>();
        _voiceAudio.spatialBlend = 1.0f;
        _voiceAudio.minDistance  = 2.0f;
        _voiceAudio.maxDistance  = 25.0f;
        _voiceAudio.playOnAwake  = false;

        LoadAudioClips();
    }

    private void LoadAudioClips()
    {
#if UNITY_EDITOR
        if (whistleClip == null)
            whistleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/fnx_sound-whistle-of-the-haunted-realm-287347.mp3");
        if (footstepClip == null)
            footstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/footsteps walking & running.mp3");
        if (alertStingClip == null)
            alertStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/encountering a proctor.mp3");
        if (voiceMumbleClip == null)
            voiceMumbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
        if (killVoiceClip == null)
            killVoiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/theProctorJumpscare.mp3");
#endif
        if (whistleClip == null)
            whistleClip = Resources.Load<AudioClip>("Sounds/fnx_sound-whistle-of-the-haunted-realm-287347");
        if (footstepClip == null)
            footstepClip = Resources.Load<AudioClip>("Sounds/footsteps walking & running");
    }

    private void ConfigureNavMeshAgent()
    {
        if (_agent == null) return;
        _agent.speed            = patrolSpeed;
        _agent.angularSpeed     = 300.0f;
        _agent.acceleration     = 9.0f;
        _agent.stoppingDistance = 0.5f;
        _agent.autoBraking      = true;
        _agent.autoTraverseOffMeshLink = false; // We handle stairs manually for smooth movement
        _agent.height  = 1.90f;
        _agent.radius  = 0.38f;
        _agent.avoidancePriority = 30; // Higher priority than NPCs
    }

    private void FindBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n.Contains("head") && _headBone == null)          _headBone = t;
            if ((n.Contains("righthand") || n.Contains("right_hand") || n.Contains("hand.r"))
                && _rightHandBone == null)                          _rightHandBone = t;
        }
    }

    private void SetupFlashlight()
    {
        Transform mount = _rightHandBone != null ? _rightHandBone : transform;

        GameObject go = new GameObject("GuardFlashlight");
        go.transform.SetParent(mount, false);

        if (_rightHandBone != null)
        {
            go.transform.localPosition = new Vector3(0.05f, 0.10f, 0.05f);
            go.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        }
        else
        {
            go.transform.localPosition = new Vector3(0.25f, 1.4f, 0.45f);
        }

        _flashlight              = go.AddComponent<Light>();
        _flashlight.type         = LightType.Spot;
        _flashlight.intensity    = 2.4f;
        _flashlight.range        = 24.0f;
        _flashlight.spotAngle    = 75.0f;
        _flashlight.innerSpotAngle = 32.0f;
        _flashlight.color        = new Color(0.92f, 0.95f, 1.0f, 1f);
        _flashlight.shadows      = LightShadows.Hard;
    }

    private void FindPlayerReferences()
    {
        var pGO = GameObject.FindGameObjectWithTag("Player");
        if (pGO != null)
        {
            _playerTransform = pGO.transform;
            _playerMovement  = pGO.GetComponent<PlayerMovement>();
            if (_playerMovement != null) _playerCam = _playerMovement.playerCamera;
        }
        if (_playerCam == null) _playerCam = Camera.main;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FLOOR HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private int FloorFromY(float y)
    {
        if (y < 6.5f)  return 1;
        if (y < 12.5f) return 2;
        return 3;
    }

    private float FloorToY(int floor)
    {
        if (floor <= 1) return floorYHeights.Length > 0 ? floorYHeights[0] : 2.43f;
        if (floor == 2) return floorYHeights.Length > 1 ? floorYHeights[1] : 8.35f;
        return floorYHeights.Length > 2 ? floorYHeights[2] : 14.35f;
    }

    private int PlayerFloor()
    {
        return _playerTransform != null ? FloorFromY(_playerTransform.position.y) : 1;
    }

    private int DetermineInitialPressureFloor()
    {
        // Start patrolling toward wherever the player likely is (clearance desks area = 1F-2F)
        return 1;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UTILITY
    // ══════════════════════════════════════════════════════════════════════════

    private void SetAgentSpeed(float speed)
    {
        if (_agent != null && _agent.isOnNavMesh)
            _agent.speed = speed;
    }

    private void StopAgent()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity  = Vector3.zero;
        }
    }

    // ── Dev Panel API ──────────────────────────────────────────────────────────
    public void ApplyThreatStats(float newPatrolSpeed, float newChaseSpeed,
                                  float newVisionDist, float newWhistleMin, float newWhistleMax)
    {
        patrolSpeed       = newPatrolSpeed;
        chaseSpeed        = newChaseSpeed;
        visionMaxDistance = newVisionDist;
        whistleIntervalMin = newWhistleMin;
        whistleIntervalMax = newWhistleMax;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.speed = currentState == GuardState.Chasing ? chaseSpeed : patrolSpeed;
    }

    public void Despawn()
    {
        currentState = GuardState.Inactive;
        StopAgent();
        if (_agent != null) _agent.enabled = false;
        if (ActiveStalker == this) ActiveStalker = null;
        Destroy(gameObject);
    }

    public static void DespawnActiveGuardStalker()
    {
        if (ActiveStalker != null) ActiveStalker.Despawn();
    }

    private void OnDestroy()
    {
        if (ActiveStalker == this) ActiveStalker = null;
    }
}
