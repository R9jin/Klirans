using System.Collections;
using UnityEngine;

/// <summary>
/// SecurityGuardEndgameManager — Orchestrates the endgame climax when the final clearance is obtained.
///
/// SEQUENCE:
/// 1. Triggered when the final signature/clearance slip is submitted to the Registrar (or via Debug F8).
/// 2. Deactivates the sitting Security_Guard_NPC in the main lobby (booth is now empty).
/// 3. Spawns/Activates the "Mr. X" Security Guard Stalker (SecurityGuardStalkerAI) equipped with flashlight and whistle.
/// 4. Broadcasts the eerie haunted whistle sound across the university.
/// 5. Updates ObjectiveHUD to direct player to the now-unguarded Ground Floor Main Entrance.
/// </summary>
public class SecurityGuardEndgameManager : MonoBehaviour
{
    private static SecurityGuardEndgameManager _instance;
    public static SecurityGuardEndgameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SecurityGuardEndgameManager>();
                if (_instance == null)
                {
                    GameObject go = GameObject.Find("GameManager") ?? new GameObject("SecurityGuardEndgameManager");
                    _instance = go.GetComponent<SecurityGuardEndgameManager>() ?? go.AddComponent<SecurityGuardEndgameManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Endgame State (Read Only)")]
    public bool isEndgameActive = false;

    [Header("Prefab & Spawn Configuration")]
    [Tooltip("The Stalker Guard Prefab or active scene instance.")]
    public GameObject stalkerGuardPrefab;

    [Tooltip("Spawn position for the Stalker Guard when endgame commences.")]
    public Vector3 stalkerSpawnPos = new Vector3(-84.0f, 2.36f, 18.0f); // Ground floor corridor just outside lobby

    [Header("Haunted Whistle Audio Broadcast")]
    public AudioClip whistleBroadcastClip;

    [Header("Debug")]
    [Tooltip("Press this key in playmode to instantly trigger the endgame stalker sequence for testing.")]
    public KeyCode debugTriggerKey = KeyCode.F9;

    private GameObject _spawnedStalker;
    private AudioSource _broadcastAudioSource;

    private void Awake()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            isEndgameActive = false;
            return;
        }

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _broadcastAudioSource = gameObject.AddComponent<AudioSource>();
        _broadcastAudioSource.spatialBlend = 0f; // 2D building-wide broadcast announcement
        _broadcastAudioSource.playOnAwake = false;

#if UNITY_EDITOR
        if (whistleBroadcastClip == null)
        {
            whistleBroadcastClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fnx_sound-whistle-of-the-haunted-realm-287347.mp3");
        }
#endif
    }

    private void Start()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        ClearanceManager.OnSlipSubmittedEvent += HandleSlipSubmitted;

        // Pre-initialize cutscene director
        _ = GuardChaseCutsceneDirector.Instance;
    }

    private void OnDestroy()
    {
        ClearanceManager.OnSlipSubmittedEvent -= HandleSlipSubmitted;
    }

    private void Update()
    {
        if (GameModeManager.IsFreeRoamMode) return;

        // Debug key to test endgame immediately
        if (Input.GetKeyDown(debugTriggerKey) && !isEndgameActive)
        {
            Debug.Log("[SecurityGuardEndgameManager] Debug trigger F9 pressed — launching Endgame Stalker!");
            TriggerEndgame();
        }
    }

    private void HandleSlipSubmitted()
    {
        if (GameModeManager.IsFreeRoamMode) return;

        if (!isEndgameActive)
        {
            StartCoroutine(WaitDialogueAndTriggerEndgame());
        }
    }

    private IEnumerator WaitDialogueAndTriggerEndgame()
    {
        // Wait until player finishes reading and closes the Registrar dialogue
        while (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive)
        {
            yield return null;
        }

        // Brief cinematic beat (0.4s) before the anomaly strikes!
        yield return new WaitForSeconds(0.4f);

        TriggerEndgame();
    }

    /// <summary>
    /// Launches the Mr. X endgame climax sequence.
    /// </summary>
    [ContextMenu("Trigger Endgame Stalker")]
    public void TriggerEndgame()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (isEndgameActive) return;

        Debug.Log("[SecurityGuardEndgameManager] ENDGAME ACTIVATED! Security guard vacating lobby booth to hunt student!");

        // If GuardChaseCutsceneDirector exists, launch the cinematic anomaly chase cutscene
        var cutscene = GuardChaseCutsceneDirector.Instance ?? FindAnyObjectByType<GuardChaseCutsceneDirector>(FindObjectsInactive.Include);
        if (cutscene != null && !GuardChaseCutsceneDirector.IsCutsceneActive)
        {
            cutscene.PlayChaseCutscene();
            return;
        }

        // Direct fallback
        CompleteEndgameActivation();
    }

    /// <summary>
    /// Activates the Stalker AI, vacates the lobby booth, and updates HUD.
    /// Called when cutscene concludes or is skipped, or directly if cutscene is disabled.
    /// </summary>
    public void CompleteEndgameActivation()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        isEndgameActive = true;

        // 1. Play initial chilling building-wide haunted whistle broadcast
        if (_broadcastAudioSource != null && whistleBroadcastClip != null)
        {
            _broadcastAudioSource.PlayOneShot(whistleBroadcastClip, 0.90f);
        }

        // 2. Spawn or activate the Stalker Guard BEFORE hiding the sitting lobby guard
        // This ensures the visual Armature, SkinnedMeshRenderer, and materials are cleanly cloned!
        SpawnOrActivateStalker();

        // 3. Vacate the lobby security booth (hide stationary sitting NPC)
        var lobbyGuard = GameObject.Find("Security_Guard_NPC");
        if (lobbyGuard == null)
        {
            var guardInteract = FindAnyObjectByType<SecurityGuardInteract>(FindObjectsInactive.Include);
            if (guardInteract != null) lobbyGuard = guardInteract.gameObject;
        }
        if (lobbyGuard != null)
        {
            lobbyGuard.SetActive(false);
            Debug.Log("[SecurityGuardEndgameManager] Lobby Security_Guard_NPC vacated from booth!");
        }

        // 4. Update ObjectiveHUD
        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "ESCAPE THE ANOMALOUS SECURITY GUARD!",
                "The possessed Security Guard is hunting you through the hallways! Evade his flashlight, hide in lockers if needed, and reach the Main Entrance Gate!"
            );
        }
    }

    public GameObject SpawnOrActivateStalker()
    {
        if (_spawnedStalker != null)
        {
            _spawnedStalker.SetActive(true);
            return _spawnedStalker;
        }

        // Sample valid NavMesh point
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(stalkerSpawnPos, out hit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            stalkerSpawnPos = hit.position;
        }

        // Check if an existing Stalker instance exists in the scene
        var existingAI = FindAnyObjectByType<SecurityGuardStalkerAI>(FindObjectsInactive.Include);
        if (existingAI != null)
        {
            _spawnedStalker = existingAI.gameObject;
            _spawnedStalker.SetActive(true);
            var agent = _spawnedStalker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.Warp(stalkerSpawnPos);
            else _spawnedStalker.transform.position = stalkerSpawnPos;
            return _spawnedStalker;
        }

        // Otherwise instantiate from prefab or create dynamically from guard model
        if (stalkerGuardPrefab != null)
        {
            _spawnedStalker = Instantiate(stalkerGuardPrefab, stalkerSpawnPos, Quaternion.identity);
            _spawnedStalker.name = "SecurityGuard_Stalker";
        }
        else
        {
            // Dynamically construct using the guard model
            _spawnedStalker = CreateStalkerInstance(stalkerSpawnPos);
        }

        if (_spawnedStalker != null)
        {
            var agent = _spawnedStalker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.Warp(stalkerSpawnPos);
        }

        return _spawnedStalker;
    }

    public static GameObject CreateStalkerInstance(Vector3 spawnPos)
    {
        GameObject stalkerRoot = new GameObject("SecurityGuard_Stalker");
        stalkerRoot.transform.position = spawnPos;

        // Model child — Clone visual Armature from the lobby Security_Guard_NPC
        GameObject visualSource = null;
        var lobbyGuard = GameObject.Find("Security_Guard_NPC");
        if (lobbyGuard == null)
        {
            var guardInteract = FindAnyObjectByType<SecurityGuardInteract>(FindObjectsInactive.Include);
            if (guardInteract != null) lobbyGuard = guardInteract.gameObject;
        }

        if (lobbyGuard != null)
        {
            Transform existingArmature = lobbyGuard.transform.Find("Armature");
            if (existingArmature != null)
            {
                visualSource = existingArmature.gameObject;
            }
            else
            {
                var smr = lobbyGuard.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) visualSource = smr.transform.parent != null ? smr.transform.parent.gameObject : smr.gameObject;
            }
        }

        if (visualSource != null)
        {
            GameObject modelInstance = Instantiate(visualSource, stalkerRoot.transform);
            modelInstance.name = "Armature";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;
            modelInstance.SetActive(true);

            // Re-bind animator to stalker hierarchy
            var anim = modelInstance.GetComponent<Animator>();
            if (anim != null) anim.Rebind();
        }
        else
        {
            // Fallback: load model from Assets
            GameObject guardModelPrefab = null;
#if UNITY_EDITOR
            guardModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NPC Assets/Animations/animateds/guard.fbx") ??
                               UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NPC Assets/Models/guard.fbx");
#endif
            if (guardModelPrefab != null)
            {
                GameObject modelInstance = Instantiate(guardModelPrefab, stalkerRoot.transform);
                modelInstance.name = "Armature";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;

#if UNITY_EDITOR
                var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/NPC Assets/AnimatorControllers/SecurityGuard_Ctrl.controller");
                var anim = modelInstance.GetComponent<Animator>() ?? modelInstance.AddComponent<Animator>();
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;

                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/NPC Assets/Materials/SecurityGuard_Mat.mat");
                if (mat != null)
                {
                    foreach (var r in modelInstance.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = mat;
                    }
                }
#endif
            }
        }

        // Capsule Collider
        var col = stalkerRoot.AddComponent<CapsuleCollider>();
        col.height = 1.90f;
        col.radius = 0.40f;
        col.center = new Vector3(0f, 0.95f, 0f);

        // Rigidbody
        var rb = stalkerRoot.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // NavMeshAgent
        var agent = stalkerRoot.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 2.1f;
        agent.angularSpeed = 360f;
        agent.acceleration = 10f;
        agent.height = 1.90f;
        agent.radius = 0.40f;

        // Stalker AI
        stalkerRoot.AddComponent<SecurityGuardStalkerAI>();

        return stalkerRoot;
    }
}
