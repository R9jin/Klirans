using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls faculty proctor / department head behaviors inside their assigned offices:
/// - Remains stationed at their assigned workstation / service window.
/// - Smoothly and naturally tracks the student with their gaze/torso when within awareness range.
/// - Returns to their default desk/window orientation when the student leaves.
/// - Bridges talking animation state with ClearanceNPC dialogue.
/// - Handles the University Registrar's dynamic multi-floor panicked runaway chase sequence
///   across staircases and corridors of the entire building.
/// </summary>
public class RoomStaffAI : MonoBehaviour
{
    [Header("Staff Identity")]
    public string staffRole = "Faculty Staff";

    [Header("Player Awareness")]
    [Tooltip("Distance at which staff notices the player and turns to face them.")]
    public float awarenessRadius = 4.0f;
    public float facePlayerTurnSpeed = 3.5f;

    [System.Obsolete("Staff proctors are now stationed at their service desks/counters.")]
    [HideInInspector] public Transform[] waypoints = new Transform[0];
    [HideInInspector] public float moveSpeed = 0f;

    [Header("Animation Parameters")]
    public string speedParameter = "Speed";
    public string talkingParameter = "Talking";

    [Header("Runaway Chase Feature")]
    public bool isRunawayActive = false;
    public bool isReturningHome = false;
    public bool isCaughtTalkingInHallway = false;
    public float fleeSprintSpeed = 5.25f;
    public float fleeJogSpeed = 4.20f;
    public float walkReturnSpeed = 2.40f;

    private NavMeshAgent _navAgent;

    // Runtime state
    private Animator _animator;
    private Transform _playerTransform;
    private NPCSitController _sitController;
    private bool _isTalking = false;
    private Quaternion _homeRotation;
    private Vector3 _homePosition;
    private Vector3 _initialArmatureLocalPos;
    private Quaternion _initialArmatureLocalRot;

    private int _speedHash;
    private int _talkingHash;

    // Room 104 obstacle-free breakout path nodes
    private static readonly Vector3 Room104AisleNorth = new Vector3(-80.85f, 2.35f, 4.86f);
    private static readonly Vector3 Room104AisleDoor  = new Vector3(-80.85f, 2.35f, 1.35f);
    private static readonly Vector3 Room104DoorInner  = new Vector3(-82.30f, 2.35f, 1.35f);
    private static readonly Vector3 Room104DoorOuter  = new Vector3(-84.45f, 2.35f, 1.35f);

    // Multi-floor candidate escape nodes across all 3 floors and 3 stairwells
    private static readonly Vector3[] BuildingEscapeNodes = new Vector3[]
    {
        // ── Floor 1 (Ground Floor, Y ~ 2.35 - 2.45) ──
        new Vector3(-84.45f, 2.36f, -16.35f), // LeftStairs 1F South
        new Vector3(-84.45f, 2.35f, -7.00f),  // Outside Cashier Window (Room 102)
        new Vector3(-84.45f, 2.35f, 3.50f),   // Outside Room 104
        new Vector3(-83.40f, 2.35f, 13.50f),  // Lobby East South lane
        new Vector3(-85.50f, 2.43f, 12.65f),  // MainStairs 1F Bottom
        new Vector3(-83.40f, 2.35f, 19.80f),  // Lobby East North lane
        new Vector3(-84.45f, 2.35f, 28.00f),  // North Hallway Mid
        new Vector3(-84.45f, 2.35f, 36.00f),  // Outside ComLab 105
        new Vector3(-84.60f, 2.36f, 49.30f),  // RightStairs 1F North

        // ── Floor 2 (Second Floor, Y ~ 8.35 - 8.45) ──
        new Vector3(-84.60f, 8.36f, -16.35f), // LeftStairs 2F Landing South
        new Vector3(-84.45f, 8.35f, -4.00f),  // Outside CCS Dean (Room 202)
        new Vector3(-84.45f, 8.35f, 6.00f),   // 2F Hallway South-Mid
        new Vector3(-85.50f, 8.35f, 12.65f),  // MainStairs 2F Landing A
        new Vector3(-84.45f, 8.35f, 20.63f),  // MainStairs 2F Landing B
        new Vector3(-84.45f, 8.35f, 28.00f),  // 2F Hallway North-Mid
        new Vector3(-84.45f, 8.35f, 42.00f),  // Outside Guidance (Room 207)
        new Vector3(-84.60f, 8.36f, 49.30f),  // RightStairs 2F Landing North

        // ── Floor 3 (Third Floor, Y ~ 14.30 - 14.40) ──
        new Vector3(-84.60f, 14.36f, -16.35f), // LeftStairs 3F Landing South
        new Vector3(-84.45f, 14.30f, -4.00f),  // 3F Hallway South
        new Vector3(-84.60f, 14.36f, 16.65f),  // MainStairs 3F Landing Center
        new Vector3(-84.45f, 14.30f, 28.00f),  // 3F Hallway North-Mid
        new Vector3(-84.45f, 14.30f, 42.00f),  // Outside Library (Room 308)
        new Vector3(-84.60f, 14.36f, 49.30f)   // RightStairs 3F Landing North
    };

    // Audio & Sounds
    private AudioSource _panicAudioSource;
    private AudioSource _footstepAudioSource;
    private AudioClip _femaleBreathingClip;
    private AudioClip _runningFootstepsClip;
    private AudioClip _panicScreamClip;
    private float _nextPanicScreamTime = 0f;
    private float _nextFootstepTime = 0f;
    private Vector3 _lastDestination = Vector3.zero;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _sitController = GetComponent<NPCSitController>();
        _homeRotation = transform.rotation;
        _homePosition = transform.position;

        if (_animator != null)
        {
            _initialArmatureLocalPos = _animator.transform.localPosition;
            _initialArmatureLocalRot = _animator.transform.localRotation;
            _animator.applyRootMotion = false;
        }

        _speedHash = Animator.StringToHash(speedParameter);
        _talkingHash = Animator.StringToHash(talkingParameter);

        LoadAudioAssets();
    }

    private void Start()
    {
        if (_sitController == null) _sitController = GetComponent<NPCSitController>();

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
        }

        if (_sitController == null || !_sitController.HasChair)
        {
            transform.position = _homePosition;
            transform.rotation = _homeRotation;
        }
        else if (_sitController.SeatedRootPos != Vector3.zero)
        {
            _homePosition = _sitController.SeatedRootPos;
            _homeRotation = _sitController.SeatedRootRot;
        }
    }

    private void LateUpdate()
    {
        if (_animator != null && _animator.transform != transform)
        {
            _animator.transform.localPosition = _initialArmatureLocalPos;
            _animator.transform.localRotation = _initialArmatureLocalRot;
        }
    }

    private void Update()
    {
        if (isRunawayActive)
        {
            if (Time.time >= _nextPanicScreamTime)
            {
                PlayPanicScream();
                _nextPanicScreamTime = Time.time + Random.Range(4.0f, 7.0f);
            }

            // Real-time touch catch failsafe: if player sprints right into the Registrar
            if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) <= 1.45f)
            {
                var clrNpc = GetComponent<ClearanceNPC>();
                if (clrNpc != null)
                {
                    clrNpc.Interact();
                }
                else
                {
                    CatchStaff();
                }
            }
            return;
        }

        if (isReturningHome)
        {
            if (isCaughtTalkingInHallway)
            {
                FacePlayer();
                UpdateAnimator(0f, true);
            }
            return;
        }

        bool hasChair = _sitController != null && _sitController.HasChair;

        if (!hasChair)
        {
            transform.position = _homePosition;
        }

        if (_isTalking)
        {
            FacePlayer();
            UpdateAnimator(0f, true);
            return;
        }

        if (hasChair)
        {
            UpdateAnimator(0f, false);
            return;
        }

        if (_playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            if (distToPlayer <= awarenessRadius)
            {
                FacePlayer();
                UpdateAnimator(0f, false);
                return;
            }
        }

        if (Quaternion.Angle(transform.rotation, _homeRotation) > 0.5f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _homeRotation, Time.deltaTime * facePlayerTurnSpeed * 0.75f);
        }
        else
        {
            transform.rotation = _homeRotation;
        }

        UpdateAnimator(0f, false);
    }

    private void FacePlayer()
    {
        if (_playerTransform == null) return;
        Vector3 targetDir = _playerTransform.position - transform.position;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * facePlayerTurnSpeed);
        }
    }

    private void UpdateAnimator(float speed, bool talking)
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator == null) return;
        _animator.SetFloat(_speedHash, speed);
        _animator.SetBool(_talkingHash, talking);
    }

    public void SetTalkingState(bool talking)
    {
        if (isRunawayActive) return;
        if (isReturningHome && !isCaughtTalkingInHallway) return;

        _isTalking = talking;
        UpdateAnimator(0f, talking);

        if (isReturningHome) return;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            if (talking) _sitController.StandUp();
            else _sitController.SitDown();
        }
    }

    public void ResetHomeTransform()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
    }

    public void TriggerRunaway(Vector3 targetOverride)
    {
        if (isRunawayActive) return;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.SeatedRootPos != Vector3.zero)
        {
            _homePosition = _sitController.SeatedRootPos;
            _homeRotation = _sitController.SeatedRootRot;
        }

        isRunawayActive = true;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;
        _isTalking = false;

        if (_sitController != null)
        {
            _sitController.ForceStandImmediate();
        }

        _navAgent = GetComponent<NavMeshAgent>();
        if (_navAgent != null) _navAgent.enabled = false;

        ForceOpenRoom104Doors();

        StopAllCoroutines();
        StartCoroutine(RunawaySequenceRoutine());
    }

    private void ForceOpenRoom104Doors()
    {
        foreach (var door in FindObjectsByType<DoorInteract>(FindObjectsInactive.Exclude))
        {
            if (door == null) continue;
            if (door.gameObject.name.Contains("104") || Vector3.Distance(door.transform.position, Room104DoorInner) < 6.0f)
            {
                door.ForceOpen();
            }
        }
    }

    private IEnumerator RunawaySequenceRoutine()
    {
        EnsureAudioSources();
        PlayPanicScream();
        _nextPanicScreamTime = Time.time + 3.0f;

        // Step 1: Step back from Window 2 chair into the clear Room 104 aisle
        yield return StartCoroutine(MoveToPoint(Room104AisleNorth, 3.8f, 2.0f, 0.20f, 2.5f));

        // Step 2: Sprint south down the Room 104 aisle in full view of the service windows
        yield return StartCoroutine(MoveToPoint(Room104AisleDoor, 4.4f, 2.0f, 0.22f, 3.0f));

        // Step 3: Turn toward Door_104A threshold
        ForceOpenRoom104Doors();
        yield return StartCoroutine(MoveToPoint(Room104DoorInner, 4.5f, 2.0f, 0.22f, 2.5f));

        // Step 4: Burst through Door_104A out into the Ground Floor Hallway
        yield return StartCoroutine(MoveToPoint(Room104DoorOuter, 4.8f, 2.0f, 0.25f, 2.5f));

        // Step 5: Continuous multi-floor panicked fleeing loop!
        // The Registrar flees across different floors (1F, 2F, 3F) and staircases,
        // dynamically avoiding running back towards the player.
        _lastDestination = Room104DoorOuter;

        while (isRunawayActive)
        {
            Vector3 fleeTarget = PickBestEscapeDestination();
            _lastDestination = fleeTarget;

            NavMeshPath path = new NavMeshPath();
            bool hasPath = NavMesh.CalculatePath(transform.position, fleeTarget, NavMesh.AllAreas, path);

            if (hasPath && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1)
            {
                // Speed depends on player proximity: full 5.25 m/s sprint when player is chasing nearby!
                float distToPlayer = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 30f;
                float currentSpeed = (distToPlayer <= 18.0f) ? fleeSprintSpeed : fleeJogSpeed;

                yield return StartCoroutine(FollowNavPathRoutine(path.corners, currentSpeed, 2.0f));
            }
            else
            {
                // Fallback direct movement towards target node
                yield return StartCoroutine(MoveToPoint(fleeTarget, fleeSprintSpeed, 2.0f, 0.40f, 6.0f));
            }

            if (!isRunawayActive) yield break;

            // Brief breather / look around if player is far away
            float playerGap = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 30f;
            if (playerGap > 22.0f)
            {
                UpdateAnimator(0.5f, false);
                yield return new WaitForSeconds(Random.Range(0.8f, 1.4f));
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Evaluates all building nodes across Floor 1, Floor 2, and Floor 3,
    /// selecting an escape route that leads away from the player without back-tracking through them.
    /// </summary>
    private Vector3 PickBestEscapeDestination()
    {
        Vector3 playerPos = (_playerTransform != null) ? _playerTransform.position : transform.position;
        Vector3 currentPos = transform.position;
        float currentDistToPlayer = Vector3.Distance(currentPos, playerPos);
        int currentFloor = GetFloorIndex(currentPos.y);
        int playerFloor = GetFloorIndex(playerPos.y);

        List<(Vector3 node, float score)> candidates = new List<(Vector3, float)>();

        for (int i = 0; i < BuildingEscapeNodes.Length; i++)
        {
            Vector3 candidate = BuildingEscapeNodes[i];

            // Don't pick where she already is or just came from
            if (Vector3.Distance(candidate, currentPos) < 5.0f) continue;
            if (Vector3.Distance(candidate, _lastDestination) < 3.0f) continue;

            // Check if path is valid on NavMesh
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(currentPos, candidate, NavMesh.AllAreas, path)) continue;
            if (path.status != NavMeshPathStatus.PathComplete) continue;

            // Reject paths that run directly through or close to the player!
            bool passesThroughPlayer = false;
            for (int c = 0; c < Mathf.Min(path.corners.Length - 1, 4); c++)
            {
                float distSegment = DistancePointToSegment(playerPos, path.corners[c], path.corners[c + 1]);
                if (distSegment < 4.2f && distSegment < currentDistToPlayer - 1.0f)
                {
                    passesThroughPlayer = true;
                    break;
                }
            }
            if (passesThroughPlayer) continue;

            // Safety scoring:
            // 1. Distance from player at destination
            float distFromPlayer = Vector3.Distance(candidate, playerPos);
            float score = distFromPlayer * 1.5f;

            // 2. Bonus for escaping to a different floor than the player!
            int nodeFloor = GetFloorIndex(candidate.y);
            if (nodeFloor != playerFloor)
            {
                score += 24.0f; // Strong incentive to take stairwells!
            }
            if (nodeFloor != currentFloor)
            {
                score += 15.0f; // Incentive to transition floors dynamically
            }

            // 3. Directional alignment away from player vector
            Vector3 dirAwayFromPlayer = (currentPos - playerPos);
            dirAwayFromPlayer.y = 0f;
            Vector3 dirToNode = (candidate - currentPos);
            dirToNode.y = 0f;
            if (dirAwayFromPlayer.sqrMagnitude > 0.01f && dirToNode.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(dirAwayFromPlayer.normalized, dirToNode.normalized);
                score += dot * 16.0f; // Huge reward for running directly away from player
            }

            candidates.Add((candidate, score));
        }

        if (candidates.Count == 0)
        {
            // Failsafe: return opposite end node of current corridor
            return (currentPos.z < 15.0f) ? new Vector3(-84.60f, 2.36f, 49.30f) : new Vector3(-84.45f, 2.36f, -16.35f);
        }

        // Sort descending by escape safety score
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        // Add fun variety: pick among top 3 safest escape routes
        int pickIndex = Random.Range(0, Mathf.Min(3, candidates.Count));
        return candidates[pickIndex].node;
    }

    private static int GetFloorIndex(float y)
    {
        if (y < 5.2f) return 1;
        if (y < 11.2f) return 2;
        return 3;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 segA, Vector3 segB)
    {
        Vector3 ab = segB - segA;
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq < 0.0001f) return Vector3.Distance(point, segA);

        float t = Mathf.Clamp01(Vector3.Dot(point - segA, ab) / abLenSq);
        Vector3 projection = segA + t * ab;
        return Vector3.Distance(point, projection);
    }

    private IEnumerator FollowNavPathRoutine(Vector3[] corners, float speed, float animSpeed)
    {
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 target = corners[i];
            float maxTimeForCorner = 12.0f;
            float cornerElapsed = 0f;

            while (cornerElapsed < maxTimeForCorner)
            {
                if (!isRunawayActive) yield break;

                cornerElapsed += Time.deltaTime;
                Vector3 currentPos = transform.position;
                Vector3 diff = target - currentPos;
                float dist = diff.magnitude;

                if (dist <= 0.40f) break;

                // Face corner direction horizontally
                Vector3 lookDir = new Vector3(diff.x, 0f, diff.z);
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 14f);
                }

                // Move smoothly in 3D (including climbing stairs)
                float step = Mathf.Min(speed * Time.deltaTime, dist);
                Vector3 nextPos = currentPos + diff.normalized * step;

                // Ground feet onto NavMesh surface
                if (NavMesh.SamplePosition(nextPos, out NavMeshHit navHit, 0.75f, NavMesh.AllAreas))
                {
                    nextPos.y = navHit.position.y;
                }
                transform.position = nextPos;

                UpdateAnimator(animSpeed, false);
                PlayFootstepAudio();

                // Reactive player avoidance in real-time:
                if (_playerTransform != null)
                {
                    float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

                    // Touch catch
                    if (distToPlayer <= 1.45f)
                    {
                        var clrNpc = GetComponent<ClearanceNPC>();
                        if (clrNpc != null) clrNpc.Interact();
                        else CatchStaff();
                        yield break;
                    }

                    // If player cuts her off in front, shriek and abort this path immediately to flee opposite!
                    Vector3 toPlayer = _playerTransform.position - transform.position;
                    toPlayer.y = 0f;
                    float forwardAngle = Vector3.Angle(transform.forward, toPlayer);
                    if (forwardAngle < 70f && distToPlayer < 7.0f)
                    {
                        PlayPanicScream();
                        yield break; // Abort path, loop will pick new flee destination heading away from player
                    }
                }

                yield return null;
            }
        }
    }

    private IEnumerator MoveToPoint(Vector3 target, float speed, float animSpeedValue, float arriveDist = 0.25f, float maxTime = 8.0f)
    {
        float elapsed = 0f;
        while (elapsed < maxTime)
        {
            if (!isRunawayActive && !isReturningHome) yield break;

            elapsed += Time.deltaTime;
            Vector3 currentPos = transform.position;
            Vector3 diff = target - currentPos;
            float dist = diff.magnitude;
            if (dist <= arriveDist) break;

            Vector3 lookDir = new Vector3(diff.x, 0f, diff.z);
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(lookDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 14f);
            }

            float step = Mathf.Min(speed * Time.deltaTime, dist);
            Vector3 nextPos = currentPos + diff.normalized * step;
            if (NavMesh.SamplePosition(nextPos, out NavMeshHit navHit, 0.75f, NavMesh.AllAreas))
            {
                nextPos.y = navHit.position.y;
            }
            transform.position = nextPos;

            UpdateAnimator(animSpeedValue, false);
            PlayFootstepAudio();
            yield return null;
        }
    }

    private void LoadAudioAssets()
    {
#if UNITY_EDITOR
        if (_femaleBreathingClip == null)
            _femaleBreathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/female heavy breathing.mp3");
        if (_runningFootstepsClip == null)
            _runningFootstepsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
        if (_panicScreamClip == null)
            _panicScreamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
#endif
    }

    private void EnsureAudioSources()
    {
        if (_panicAudioSource == null)
        {
            _panicAudioSource = gameObject.AddComponent<AudioSource>();
            _panicAudioSource.spatialBlend = 0.70f;
            _panicAudioSource.minDistance = 2.0f;
            _panicAudioSource.maxDistance = 24.0f;
            _panicAudioSource.playOnAwake = false;
            _panicAudioSource.volume = 0.90f;
        }

        if (_footstepAudioSource == null)
        {
            _footstepAudioSource = gameObject.AddComponent<AudioSource>();
            _footstepAudioSource.spatialBlend = 0.85f;
            _footstepAudioSource.minDistance = 1.5f;
            _footstepAudioSource.maxDistance = 18.0f;
            _footstepAudioSource.playOnAwake = false;
            _footstepAudioSource.volume = 0.55f;
        }

        LoadAudioAssets();
    }

    private void PlayFootstepAudio()
    {
        if (Time.time < _nextFootstepTime) return;
        EnsureAudioSources();
        if (_footstepAudioSource != null && _runningFootstepsClip != null)
        {
            _footstepAudioSource.pitch = Random.Range(1.25f, 1.45f);
            _footstepAudioSource.PlayOneShot(_runningFootstepsClip, 0.45f);
            _nextFootstepTime = Time.time + 0.32f;
        }
    }

    private void PlayPanicScream()
    {
        EnsureAudioSources();
        var clrNpc = GetComponent<ClearanceNPC>();
        AudioClip clip = (clrNpc != null && clrNpc.mumbleAudioClip != null) ? clrNpc.mumbleAudioClip : _panicScreamClip;

        if (_panicAudioSource != null && clip != null)
        {
            _panicAudioSource.pitch = Random.Range(1.25f, 1.40f);
            _panicAudioSource.PlayOneShot(clip, 0.85f);
        }
    }

    /// <summary>
    /// Called when the player catches up to the fleeing Registrar in the hallway and presses [E] or touches her.
    /// </summary>
    public void CatchStaff()
    {
        if (!isRunawayActive) return;

        isRunawayActive = false;
        isReturningHome = true;
        isCaughtTalkingInHallway = true;

        EnsureAudioSources();
        if (_panicAudioSource != null && _panicAudioSource.isPlaying)
        {
            _panicAudioSource.Stop();
        }

        // Play panting / catching breath audio
        if (_panicAudioSource != null && _femaleBreathingClip != null)
        {
            _panicAudioSource.pitch = 1.0f;
            _panicAudioSource.PlayOneShot(_femaleBreathingClip, 0.90f);
        }

        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = true;
            _navAgent.enabled = false;
        }

        StopAllCoroutines();
        FacePlayer();
        UpdateAnimator(0f, true);
    }

    /// <summary>
    /// Triggered when the hallway catch dialogue closes so the Registrar walks back into Room 104.
    /// </summary>
    public void BeginReturnHomeAfterCatch()
    {
        if (!isReturningHome) return;
        isCaughtTalkingInHallway = false;
        _isTalking = false;
        StopAllCoroutines();
        StartCoroutine(ReturnHomeRoutine());
    }

    /// <summary>
    /// If the player sprints back to Window 2 before the Registrar finishes walking back,
    /// snap her cleanly to her desk seat so the sorting puzzle can begin immediately.
    /// </summary>
    public void CompleteReturnHomeImmediate()
    {
        if (!isReturningHome) return;
        StopAllCoroutines();
        isRunawayActive = false;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;
        _isTalking = false;

        transform.position = _homePosition;
        transform.rotation = _homeRotation;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.SitDown();
        }

        UpdateAnimator(0f, false);
    }

    private IEnumerator ReturnHomeRoutine()
    {
        UpdateAnimator(1.0f, false); // Calm walking pace
        ForceOpenRoom104Doors();

        // Calculate smooth NavMesh return path all the way from current floor/location back to desk seat
        NavMeshPath returnPath = new NavMeshPath();
        bool foundPath = NavMesh.CalculatePath(transform.position, _homePosition, NavMesh.AllAreas, returnPath);

        if (foundPath && returnPath.status == NavMeshPathStatus.PathComplete && returnPath.corners.Length > 1)
        {
            yield return StartCoroutine(FollowNavPathRoutine(returnPath.corners, walkReturnSpeed, 1.0f));
        }
        else
        {
            yield return StartCoroutine(MoveToPoint(_homePosition, walkReturnSpeed, 1.0f, 0.20f, 15.0f));
        }

        transform.position = _homePosition;
        transform.rotation = _homeRotation;
        isReturningHome = false;
        isCaughtTalkingInHallway = false;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null && _sitController.HasChair)
        {
            _sitController.SitDown();
        }

        UpdateAnimator(0f, false);
    }
}
