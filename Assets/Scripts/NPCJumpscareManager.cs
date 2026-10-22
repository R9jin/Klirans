using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPCJumpscareManager — In-Your-Face FNAF / Fortnite-style NPC face jumpscare:
/// 1. Primary Trigger: Punishes player when really touching or blocking the walking students in the hallways.
///    - Detected via OnControllerColliderHit (direct physics contact) AND horizontal distance (&lt;= 1.30m).
///    - Balanced to near 50/50 chance, but not too frequent (18s global cooldown, 5s reroll debounce).
/// 2. Flashlight Blinding: Blinding walking students at close range (&lt;= 3.5m) for &gt;= 1.0s.
/// 3. External Errors API: PunishPlayerError (e.g. failed lockpicking or loud crashes).
/// 4. Directly frames the horrifying face of the offending student (Drei, Niel, Josua, Jessa, Glad, Ira) on screen.
/// 5. Isolates head and neck mesh centered at (0, 0, 0) and renders with Unlit shader so facial features are crisp.
/// 6. Lunges directly towards camera lens with violent high-frequency jitter, tremors, and screams.
/// 7. Adds anxiety to AnxietyManager (+12f) and drains stamina.
/// 8. Plays high-impact jumpscare screamer sound at full volume.
/// 9. Restricts jumpscares STRICTLY to hallways and circulation areas (blocked inside rooms, lockers, dialogue, puzzles).
/// </summary>
public class NPCJumpscareManager : MonoBehaviour
{
    private static NPCJumpscareManager _instance;
    public static NPCJumpscareManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<NPCJumpscareManager>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [System.Serializable]
    public class WanderingNPCData
    {
        public string name;
        public GameObject gameObject;
        public Transform transform;
        public SkinnedMeshRenderer smr;
        public Collider collider;
        public ProctorAI proctorAI;
    }

    [Header("Touch & Block Punishment Settings")]
    [Tooltip("Distance threshold (meters, horizontal XZ) where the player is touching or blocking a walking student (body collision: ~1.05m).")]
    public float touchDistanceThreshold = 1.05f;

    [Range(0f, 1f)]
    [Tooltip("Probability of being jumpscared when physically touching or blocking a walking student (near 50/50).")]
    public float touchScareChance = 0.50f;

    [Tooltip("Cooldown in seconds after a jumpscare occurs before another jumpscare can happen (pacing: not too frequent).")]
    public float jumpscareGlobalCooldown = 18.0f;

    [Tooltip("Cooldown in seconds after touching/blocking an NPC and rolling safe before checking touch again.")]
    public float touchRerollCooldown = 5.0f;

    [Header("Flashlight Blinding Punishment")]
    [Tooltip("If true, shining flashlight directly into a student's eyes causes a jumpscare.")]
    public bool enableFlashlightBlinding = false;
    [Tooltip("Distance threshold for blinding a student with the flashlight.")]
    public float flashlightInFaceRadius = 3.5f;

    [Tooltip("How long the flashlight must shine directly in a student's eyes before triggering a scare.")]
    public float flashlightBlindingThreshold = 1.0f;

    [Header("Horror & Balancing")]
    [Tooltip("Amount of anxiety added to the AnxietyMeter on jumpscare.")]
    public float jumpscareAnxietyIncrease = 12f;

    [Tooltip("Stamina penalty when jumpscared.")]
    public float staminaDrain = 25f;

    [Tooltip("Duration of the in-your-face scare in seconds.")]
    public float scareDuration = 0.90f;

    [Tooltip("Start distance of the lunging face from camera.")]
    public float startDistance = 0.58f;

    [Tooltip("End distance of the lunging face at closest point (right in your face).")]
    public float closestDistance = 0.38f;

    [Tooltip("Face scale multiplier to fill the screen.")]
    public float faceScale = 1.55f;

    [Tooltip("Height offset of the lunging face relative to camera center (positive elevates head).")]
    public float headHeightOffset = 0.085f;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [Tooltip("Master volume for jumpscare screamer.")]
    public float jumpscareVolume = 1.0f;

    public AudioClip scareStingClip;
    public AudioClip proctorStingClip;
    public AudioClip staticHissClip;
    public AudioClip gaspBreathClip;
    public AudioClip heartbeatClip;

    // ── Internal References ──────────────────────────────────────────────────
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private StaminaSystem _staminaSystem;
    private AudioSource _audioSource;
    private AudioSource _heartbeatSource;

    // ── Jumpscare 3D Rig parented to Camera ──────────────────────────────────
    private GameObject _scareRig;
    private MeshFilter _scareMeshFilter;
    private MeshRenderer _scareMeshRenderer;
    private Material _unlitMaterial;
    private Image _flashOverlay;

    private float _flashlightAimTimer = 0f;
    private float _cooldownUntil = 0f;
    private float _touchRerollCooldownUntil = 0f;
    private bool _isScaring = false;
    private bool _isGuardForced = false;

    // ── Wandering Student sources ────────────────────────────────────────────
    private readonly string[] _wanderingStudentNames = new string[]
    {
        "ClearanceNPC_Drei",
        "ClearanceNPC_Niel",
        "ClearanceNPC_Josua",
        "ClearanceNPC_Jessa",
        "ClearanceNPC_Glad",
        "ClearanceNPC_Ira",
        "ClearanceNPC_Rachel",
        "ClearanceNPC_Bene",
        "ClearanceNPC_JP"
    };

    private readonly List<WanderingNPCData> _cachedNPCs = new List<WanderingNPCData>();
    private Mesh _activeBakedMesh;

    // Tracks which NPC violated personal space / caught the player
    private SkinnedMeshRenderer _proximityChosenSMR = null;

    // ── Room Bounding Boxes (to prevent scaring inside rooms) ────────────────
    private List<Bounds> _cachedRoomBounds = new List<Bounds>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _playerMovement = GetComponent<PlayerMovement>() ?? FindAnyObjectByType<PlayerMovement>();
        if (_playerMovement != null) _playerCam = _playerMovement.playerCamera;
        if (_playerCam == null) _playerCam = Camera.main;

        _staminaSystem = GetComponent<StaminaSystem>() ?? FindAnyObjectByType<StaminaSystem>();

        var audioGO = new GameObject("JumpscareAudioSource");
        audioGO.transform.SetParent(transform, false);
        _audioSource = audioGO.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D in-your-face sound
        _audioSource.volume = jumpscareVolume;

        var hbGO = new GameObject("JumpscareHeartbeatSource");
        hbGO.transform.SetParent(transform, false);
        _heartbeatSource = hbGO.AddComponent<AudioSource>();
        _heartbeatSource.playOnAwake = false;
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.loop = true;
        _heartbeatSource.volume = 0f;

        LoadDefaultAudio();
        CacheWanderingNPCs();
        CacheRoomBounds();
        BuildScareRig();

        // Initial grace period on scene load so player is never jumpscared the instant the game starts
        _cooldownUntil = Time.time + 15f;
        _touchRerollCooldownUntil = Time.time + 15f;
    }

    /// <summary>
    /// Grants a grace period during which walking student jumpscares cannot trigger (e.g. after cutscenes or puzzles).
    /// </summary>
    public void ApplyGraceCooldown(float duration = 15f)
    {
        _cooldownUntil = Mathf.Max(_cooldownUntil, Time.time + duration);
        _touchRerollCooldownUntil = Mathf.Max(_touchRerollCooldownUntil, Time.time + duration);
        _flashlightAimTimer = 0f;
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (scareStingClip == null)
            scareStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");

        if (proctorStingClip == null)
            proctorStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");

        if (staticHissClip == null)
            staticHissClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");

        if (gaspBreathClip == null)
            gaspBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");

        if (heartbeatClip == null)
            heartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");
#endif
        // Runtime fallback for standalone builds
        if (scareStingClip == null)
            scareStingClip = Resources.Load<AudioClip>("Sounds/you died (lobotomy sound)");
        if (proctorStingClip == null)
            proctorStingClip = Resources.Load<AudioClip>("Sounds/encountering a proctor");
        if (staticHissClip == null)
            staticHissClip = Resources.Load<AudioClip>("Sounds/vhs static");
        if (gaspBreathClip == null)
            gaspBreathClip = Resources.Load<AudioClip>("Sounds/man gasping for air");
        if (heartbeatClip == null)
            heartbeatClip = Resources.Load<AudioClip>("Sounds/fast heart beat");
    }

    private Texture2D LoadTexture(string path)
    {
#if UNITY_EDITOR
        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
#endif
        string fullPath = System.IO.Path.Combine(Application.dataPath, "..", path).Replace('\\', '/');
        string targetPath = System.IO.File.Exists(fullPath) ? fullPath : (System.IO.File.Exists(path) ? path : null);
        if (targetPath != null)
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(targetPath);
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(bytes)) return t;
            }
            catch {}
        }
        return null;
    }

    /// <summary>
    /// Caches all wandering student character models, true 3D transforms, and colliders.
    /// </summary>
    public void CacheWanderingNPCs()
    {
        _cachedNPCs.Clear();

        // 1. Check parent ClearanceNPCs root if present
        var parent = GameObject.Find("ClearanceNPCs");
        if (parent != null)
        {
            var ais = parent.GetComponentsInChildren<ProctorAI>(true);
            foreach (var ai in ais)
            {
                if (ai == null || ai.name.Contains("TheProctor")) continue;
                var smr = ai.GetComponentInChildren<SkinnedMeshRenderer>();
                var col = ai.GetComponent<Collider>() ?? ai.GetComponentInChildren<Collider>();
                if (smr != null && smr.sharedMesh != null)
                {
                    _cachedNPCs.Add(new WanderingNPCData
                    {
                        name = ai.name,
                        gameObject = ai.gameObject,
                        transform = ai.transform,
                        smr = smr,
                        collider = col,
                        proctorAI = ai
                    });
                }
            }
        }

        // 2. Add any listed student names not yet added
        foreach (var name in _wanderingStudentNames)
        {
            if (_cachedNPCs.Exists(n => n.name == name)) continue;
            var go = GameObject.Find(name);
            if (go != null)
            {
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var col = go.GetComponent<Collider>() ?? go.GetComponentInChildren<Collider>();
                var ai = go.GetComponent<ProctorAI>();

                if (smr != null && smr.sharedMesh != null)
                {
                    _cachedNPCs.Add(new WanderingNPCData
                    {
                        name = name,
                        gameObject = go,
                        transform = go.transform,
                        smr = smr,
                        collider = col,
                        proctorAI = ai
                    });
                }
            }
        }

        // Fallback: search all ProctorAI walking students
        if (_cachedNPCs.Count == 0)
        {
            var allAI = FindObjectsByType<ProctorAI>(FindObjectsInactive.Include);
            foreach (var ai in allAI)
            {
                if (ai.name.Contains("TheProctor")) continue;
                var smr = ai.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                {
                    _cachedNPCs.Add(new WanderingNPCData
                    {
                        name = ai.name,
                        gameObject = ai.gameObject,
                        transform = ai.transform,
                        smr = smr,
                        collider = ai.GetComponent<Collider>() ?? ai.GetComponentInChildren<Collider>(),
                        proctorAI = ai
                    });
                }
            }
        }

        Debug.Log($"[NPCJumpscareManager] Cached {_cachedNPCs.Count} wandering student NPCs for touch/blocking jumpscares.");
    }

    /// <summary>
    /// Caches the 3D bounding boxes of all 24 rooms in the building.
    /// </summary>
    public void CacheRoomBounds()
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
                string rName = room.name.ToLower();
                if (rName.Contains("locker") || rName.Contains("hallway") || rName.Contains("corridor") ||
                    rName.Contains("lobby") || rName.Contains("stair") || rName.Contains("circulation"))
                {
                    continue;
                }

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
                    if ((b.size.z > 18f && b.size.x < 5f) || (b.size.x > 18f && b.size.z < 5f))
                    {
                        continue;
                    }
                    b.Expand(new Vector3(-0.15f, 0f, -0.15f));
                    _cachedRoomBounds.Add(b);
                }
            }
        }

        Debug.Log($"[NPCJumpscareManager] Cached {_cachedRoomBounds.Count} room boundaries for hallway-only jumpscare filtering.");
    }

    private void BuildScareRig()
    {
        if (_playerCam == null) return;

        _scareRig = new GameObject("NPC_Fortnite_ScareRig");
        _scareRig.transform.SetParent(_playerCam.transform, false);
        _scareRig.transform.localPosition = new Vector3(0f, headHeightOffset, startDistance);
        _scareRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        _scareRig.transform.localScale = Vector3.one * faceScale;

        _scareMeshFilter = _scareRig.AddComponent<MeshFilter>();
        _scareMeshRenderer = _scareRig.AddComponent<MeshRenderer>();
        _scareMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _scareMeshRenderer.receiveShadows = false;

        // Dedicated Unlit material: NO specular blowout, sharp recognizable face
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        _unlitMaterial = new Material(unlitShader);
        _scareMeshRenderer.sharedMaterial = _unlitMaterial;

        // Red horror vignette on HudCanvas
        var hud = GameObject.Find("HudCanvas");
        if (hud != null)
        {
            var flashGO = new GameObject("JumpscareFlash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(hud.transform, false);
            var rt = flashGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _flashOverlay = flashGO.GetComponent<Image>();
            _flashOverlay.color = new Color(0.60f, 0.04f, 0.04f, 0f);
            _flashOverlay.raycastTarget = false;
            flashGO.SetActive(false);
        }

        _scareRig.SetActive(false);
    }

    private void Update()
    {
        // Debug F8 jumpscare test: only accessible in Free Roam dev mode
        if (GameModeManager.IsFreeRoamMode && Input.GetKeyDown(KeyCode.F8))
        {
            Debug.Log("[NPCJumpscareManager] Manual F8 jumpscare test triggered in Free Roam!");
            TriggerJumpscare();
            return;
        }

        if (GameModeManager.IsFreeRoamMode) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;

        if (_isScaring) return;

        // ── 1. PRIMARY TRIGGER: Really Touching / Blocking Walking NPC in Hallways ───
        if (Time.time >= _cooldownUntil && Time.time >= _touchRerollCooldownUntil && CanTriggerScare())
        {
            var (touchNPC, distXZ) = GetTouchedNPC(touchDistanceThreshold);
            if (touchNPC != null)
            {
                // Only trigger if NPC is actively walking into player or player is running into them (never when standing peacefully)
                bool npcWalking = touchNPC.proctorAI != null && !touchNPC.proctorAI.IsIdling;
                bool playerRunning = _playerMovement != null && _playerMovement.IsRunning;
                if (npcWalking || playerRunning)
                {
                    HandleTouchOrBlock(touchNPC, $"Continuous Proximity/Block (distXZ={distXZ:F2}m)");
                    return;
                }
            }
        }

        // ── 2. FLASHLIGHT BLINDING PUNISHMENT (Disabled by default to prevent false scares) ───
        if (enableFlashlightBlinding && Time.time >= _cooldownUntil && CanTriggerScare())
        {
            if (FlashlightController.Instance != null && FlashlightController.Instance.IsLightOn)
            {
                var (aimNPC, aimDist) = GetAimedNPCInRadius(flashlightInFaceRadius, 0.82f);
                if (aimNPC != null)
                {
                    _flashlightAimTimer += Time.deltaTime;
                    if (_flashlightAimTimer >= flashlightBlindingThreshold)
                    {
                        _flashlightAimTimer = 0f;
                        HandleTouchOrBlock(aimNPC, $"Flashlight Blinding (dist={aimDist:F2}m)");
                        return;
                    }
                }
                else
                {
                    _flashlightAimTimer = Mathf.Max(0f, _flashlightAimTimer - Time.deltaTime * 2f);
                }
            }
            else
            {
                _flashlightAimTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Invoked by PlayerMovement when the CharacterController collides with any object in the world.
    /// </summary>
    public void OnPlayerControllerHit(GameObject hitGO)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring) return;
        if (Time.time < _cooldownUntil) return;
        if (!CanTriggerScare()) return;

        var npc = FindMatchingNPC(hitGO);
        if (npc != null)
        {
            if (npc.proctorAI != null && !npc.proctorAI.IsTalking)
            {
                npc.proctorAI.TriggerImmediateBumpScare();
            }
            else
            {
                HandleTouchOrBlock(npc, "Physical Collision (OnPlayerControllerHit)");
            }
        }
    }

    /// <summary>
    /// Directly triggers a jumpscare using the horrifying face of the specified NPC.
    /// </summary>
    public void TriggerSpecificNPCJumpscare(GameObject npcGO)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring) return;
        if (!CanTriggerScare()) return;

        var npc = FindMatchingNPC(npcGO);
        if (npc != null)
        {
            _proximityChosenSMR = npc.smr;
            _cooldownUntil = Time.time + jumpscareGlobalCooldown;
            _touchRerollCooldownUntil = Time.time + jumpscareGlobalCooldown;
            TriggerJumpscare();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit != null && hit.collider != null)
        {
            OnPlayerControllerHit(hit.collider.gameObject);
        }
    }

    /// <summary>
    /// Evaluates the 50/50 jumpscare roll when the player touches or blocks a walking student.
    /// If scare triggers, initiates jumpscare and applies long global cooldown (not too frequent).
    /// If safe, applies a reroll debounce so the player isn't spammed with checks while touching.
    /// </summary>
    private void HandleTouchOrBlock(WanderingNPCData npc, string source)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring) return;
        if (Time.time < _cooldownUntil || Time.time < _touchRerollCooldownUntil) return;
        if (!CanTriggerScare()) return;

        float effectiveChance = touchScareChance;
        if (_playerMovement != null)
        {
            if (_playerMovement.IsRunning) effectiveChance = Mathf.Min(0.85f, touchScareChance + 0.25f);
            else if (_playerMovement.IsCrouching) effectiveChance = Mathf.Max(0.15f, touchScareChance - 0.30f);
        }

        float roll = Random.value;
        bool shouldScare = (roll < effectiveChance);

        Debug.Log($"[NPCJumpscareManager] Player touched/blocked {npc.name} ({source}). Roll: {roll:F2} < {effectiveChance:F2} => {(shouldScare ? "JUMPSCARE!" : "SAFE (reroll cooldown)")}");

        if (shouldScare)
        {
            _proximityChosenSMR = npc.smr;
            _cooldownUntil = Time.time + jumpscareGlobalCooldown;
            _touchRerollCooldownUntil = Time.time + jumpscareGlobalCooldown;
            TriggerJumpscare();
        }
        else
        {
            // Debounce: safe roll means no jumpscare for at least touchRerollCooldown seconds
            _touchRerollCooldownUntil = Time.time + touchRerollCooldown;
        }
    }

    /// <summary>
    /// Public API: Punishes specific player mistakes (e.g. lockpicking fail or loud noises).
    /// </summary>
    public void PunishPlayerError(string reason, float chance = 0.50f)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring || Time.time < _cooldownUntil) return;
        if (!CanTriggerScare()) return;

        var (targetNPC, dist) = GetTouchedNPC(4.0f);
        if (targetNPC != null && Random.value <= chance)
        {
            Debug.Log($"[NPCJumpscareManager] Punishing error '{reason}' with jumpscare from {targetNPC.name}!");
            _proximityChosenSMR = targetNPC.smr;
            _cooldownUntil = Time.time + jumpscareGlobalCooldown;
            _touchRerollCooldownUntil = Time.time + jumpscareGlobalCooldown;
            TriggerJumpscare();
        }
    }

    /// <summary>
    /// Matches a hit collider's GameObject to one of our cached wandering NPCs.
    /// </summary>
    private WanderingNPCData FindMatchingNPC(GameObject go)
    {
        if (go == null) return null;
        if (_cachedNPCs.Count == 0) CacheWanderingNPCs();

        foreach (var npc in _cachedNPCs)
        {
            if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy) continue;
            if (go == npc.gameObject || go.transform.IsChildOf(npc.transform))
            {
                return npc;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the wandering student being touched or blocked horizontally within maxDistXZ on the same floor level.
    /// </summary>
    private (WanderingNPCData npc, float distXZ) GetTouchedNPC(float maxDistXZ)
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return (null, float.MaxValue);

        Vector3 playerPos = playerGO.transform.position;
        Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);

        if (_cachedNPCs.Count == 0) CacheWanderingNPCs();

        WanderingNPCData closest = null;
        float closestDistXZ = float.MaxValue;

        foreach (var npc in _cachedNPCs)
        {
            if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy) continue;

            Vector3 npcPos = npc.transform.position;
            float distY = Mathf.Abs(playerPos.y - npcPos.y);
            if (distY > 1.8f) continue; // Must be on same floor

            Vector2 npcXZ = new Vector2(npcPos.x, npcPos.z);
            float distXZ = Vector2.Distance(playerXZ, npcXZ);

            if (distXZ <= maxDistXZ && distXZ < closestDistXZ)
            {
                closestDistXZ = distXZ;
                closest = npc;
            }
        }

        return (closest, closestDistXZ);
    }

    /// <summary>
    /// Returns any wandering student whose face/head is being aimed at directly by player camera.
    /// </summary>
    private (WanderingNPCData npc, float dist) GetAimedNPCInRadius(float radius, float minDot)
    {
        if (_playerCam == null) return (null, float.MaxValue);
        if (_cachedNPCs.Count == 0) CacheWanderingNPCs();

        Vector3 camPos = _playerCam.transform.position;
        Vector3 camFwd = _playerCam.transform.forward;

        WanderingNPCData bestNPC = null;
        float bestDist = float.MaxValue;

        foreach (var npc in _cachedNPCs)
        {
            if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy) continue;

            Vector3 headPos = npc.transform.position + Vector3.up * 1.55f;
            Vector3 toHead = headPos - camPos;
            float dist = toHead.magnitude;

            if (dist <= radius && dist < bestDist)
            {
                float dot = Vector3.Dot(camFwd, toHead / dist);
                if (dot >= minDot)
                {
                    bestDist = dist;
                    bestNPC = npc;
                }
            }
        }

        return (bestNPC, bestDist);
    }

    /// <summary>
    /// Ensures jumpscares happen ONLY in hallways and circulation areas, never inside rooms or safe spots.
    /// </summary>
    public bool CanTriggerScare()
    {
        if (DevDemoPanel.IsGodModeActive) return false;
        if (GameModeManager.IsFreeRoamMode) return false;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return false;
        if (PauseMenu.GameIsPaused) return false;
        if (LockerHideManager.IsPlayerHidden) return false;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return false;
        if (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive) return false;

        var guidancePuzzle = GuidanceWordPuzzle.Instance;
        if (guidancePuzzle != null && guidancePuzzle.IsOpen) return false;

        var cashierPuzzle = CashierBalancePuzzle.Instance;
        if (cashierPuzzle != null && cashierPuzzle.IsOpen) return false;

        var registrarPuzzle = RegistrarDocumentSortPuzzle.Instance;
        if (registrarPuzzle != null && registrarPuzzle.IsOpen) return false;

        if (IsPlayerOnStairs()) return false;
        if (IsPlayerInsideAnyRoom()) return false;

        return true;
    }

    public bool IsPlayerInsideAnyRoom()
    {
        if (_cachedRoomBounds.Count == 0) CacheRoomBounds();

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return false;
        Vector3 playerPos = playerGO.transform.position;

        foreach (var b in _cachedRoomBounds)
        {
            if (b.Contains(playerPos)) return true;
        }
        return false;
    }

    private bool IsPlayerOnStairs()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return false;

        var stairZones = FindObjectsByType<StairTriggerZone>(FindObjectsInactive.Include);
        Vector3 playerPos = playerGO.transform.position;
        foreach (var zone in stairZones)
        {
            var col = zone.GetComponent<Collider>();
            if (col != null && col.bounds.Contains(playerPos)) return true;
        }

        return false;
    }

    [ContextMenu("Trigger Jumpscare Now")]
    public void TriggerJumpscare(SkinnedMeshRenderer customSMR = null, bool isGuard = false)
    {
        if (DevDemoPanel.IsGodModeActive) return;
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring || _scareRig == null) return;
        if (customSMR != null)
        {
            _proximityChosenSMR = customSMR;
        }
        _isGuardForced = isGuard;
        StartCoroutine(FortniteStyleJumpscareSequence());
    }

    private IEnumerator FortniteStyleJumpscareSequence()
    {
        if (GameModeManager.IsFreeRoamMode)
        {
            _isScaring = false;
            yield break;
        }

        _isScaring = true;

        SkinnedMeshRenderer chosenSMR = _proximityChosenSMR;
        _proximityChosenSMR = null;

        bool isGuardCatch = _isGuardForced ||
                            (chosenSMR != null && (chosenSMR.name.ToLower().Contains("guard") ||
                                                   chosenSMR.transform.root.name.ToLower().Contains("guard") ||
                                                   chosenSMR.GetComponentInParent<SecurityGuardStalkerAI>() != null ||
                                                   chosenSMR.GetComponentInParent<SecurityGuardInteract>() != null)) ||
                            (SecurityGuardStalkerAI.ActiveStalker != null && SecurityGuardStalkerAI.ActiveStalker.currentState == SecurityGuardStalkerAI.GuardState.Jumpscare);
        _isGuardForced = false;

        // If the Security Guard stalker is actively catching the student, ALWAYS use the Security Guard model, NEVER fall back to a random student!
        if (isGuardCatch)
        {
            if (chosenSMR == null && SecurityGuardStalkerAI.ActiveStalker != null)
                chosenSMR = SecurityGuardStalkerAI.ActiveStalker.GetComponentInChildren<SkinnedMeshRenderer>();
            if (chosenSMR == null)
            {
                var boothGuard = GameObject.Find("Security_Guard_NPC") ?? FindAnyObjectByType<SecurityGuardInteract>(FindObjectsInactive.Include)?.gameObject;
                if (boothGuard != null) chosenSMR = boothGuard.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
            if (chosenSMR == null)
            {
                var allSMRs = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
                foreach (var s in allSMRs)
                {
                    if (s.name.ToLower().Contains("guard") || s.transform.root.name.ToLower().Contains("guard") || (s.sharedMaterial != null && s.sharedMaterial.name.ToLower().Contains("guard")))
                    {
                        chosenSMR = s;
                        break;
                    }
                }
            }
        }
        else if (chosenSMR == null)
        {
            if (_cachedNPCs.Count == 0) CacheWanderingNPCs();
            if (_cachedNPCs.Count > 0)
            {
                int idx = Random.Range(0, _cachedNPCs.Count);
                chosenSMR = _cachedNPCs[idx].smr;
            }
        }

        if (chosenSMR == null)
        {
            _isScaring = false;
            yield break;
        }

        // 1. Bake the student's current skinned mesh pose
        Mesh fullBakedMesh = new Mesh();
        chosenSMR.BakeMesh(fullBakedMesh);

        Vector3[] rawVerts = fullBakedMesh.vertices;
        Vector2[] rawUVs = fullBakedMesh.uv;
        int[] rawTris = fullBakedMesh.triangles;

        float maxY = float.MinValue;
        float minY = float.MaxValue;
        for (int i = 0; i < rawVerts.Length; i++)
        {
            if (rawVerts[i].y > maxY) maxY = rawVerts[i].y;
            if (rawVerts[i].y < minY) minY = rawVerts[i].y;
        }

        // Neutralize head bone rotation/tilt from walking animation
        Transform headBone = null;
        if (chosenSMR.bones != null)
        {
            foreach (var b in chosenSMR.bones)
            {
                if (b != null && b.name.EndsWith("Head")) { headBone = b; break; }
            }
        }

        Quaternion headCancelRot = Quaternion.identity;
        Vector3 headBoneLocalPos = Vector3.zero;
        if (headBone != null)
        {
            Quaternion headLocalRot = Quaternion.Inverse(chosenSMR.transform.rotation) * headBone.rotation;
            headCancelRot = Quaternion.Inverse(headLocalRot);
            headBoneLocalPos = chosenSMR.transform.InverseTransformPoint(headBone.position);
        }

        Vector3[] neutralVerts = new Vector3[rawVerts.Length];
        for (int i = 0; i < rawVerts.Length; i++)
        {
            Vector3 v = rawVerts[i];
            if (headBone != null && v.y >= maxY - 0.40f)
                neutralVerts[i] = headCancelRot * (v - headBoneLocalPos) + headBoneLocalPos;
            else
                neutralVerts[i] = v;
        }

        maxY = float.MinValue;
        for (int i = 0; i < neutralVerts.Length; i++)
        {
            if (neutralVerts[i].y > maxY) maxY = neutralVerts[i].y;
        }

        // Compute true face center (eyes and bridge of nose)
        float eyeY = maxY - 0.12f;
        float sumX = 0f;
        float sumZ = 0f;
        int countXZ = 0;
        for (int i = 0; i < neutralVerts.Length; i++)
        {
            if (neutralVerts[i].y >= maxY - 0.25f)
            {
                sumX += neutralVerts[i].x;
                sumZ += neutralVerts[i].z;
                countXZ++;
            }
        }
        float centerX = countXZ > 0 ? (sumX / countXZ) : 0f;
        float centerZ = countXZ > 0 ? (sumZ / countXZ) : 0f;
        Vector3 faceCenter = new Vector3(centerX, eyeY, centerZ);

        // 2. Center vertices directly on eyes/nose and isolate head & face triangles
        var newVerts = new List<Vector3>();
        var newUVs = new List<Vector2>();
        var newTris = new List<int>();
        var oldToNew = new Dictionary<int, int>();

        float keepMinY = isGuardCatch ? -0.32f : -0.22f; // Chin & security uniform collar
        float keepMaxY = isGuardCatch ? 0.38f : 0.22f;  // Top of security guard peaked cap & visor

        for (int i = 0; i < rawTris.Length; i += 3)
        {
            int i1 = rawTris[i], i2 = rawTris[i + 1], i3 = rawTris[i + 2];
            Vector3 v1 = neutralVerts[i1] - faceCenter;
            Vector3 v2 = neutralVerts[i2] - faceCenter;
            Vector3 v3 = neutralVerts[i3] - faceCenter;

            if ((v1.y >= keepMinY && v1.y <= keepMaxY) ||
                (v2.y >= keepMinY && v2.y <= keepMaxY) ||
                (v3.y >= keepMinY && v3.y <= keepMaxY))
            {
                int AddOrGet(int oldIdx, Vector3 pos)
                {
                    if (!oldToNew.TryGetValue(oldIdx, out int nIdx))
                    {
                        nIdx = newVerts.Count;
                        newVerts.Add(pos);
                        newUVs.Add(rawUVs != null && oldIdx < rawUVs.Length ? rawUVs[oldIdx] : Vector2.zero);
                        oldToNew[oldIdx] = nIdx;
                    }
                    return nIdx;
                }

                newTris.Add(AddOrGet(i1, v1));
                newTris.Add(AddOrGet(i2, v2));
                newTris.Add(AddOrGet(i3, v3));
            }
        }

        Destroy(fullBakedMesh);

        if (_activeBakedMesh != null) Destroy(_activeBakedMesh);
        _activeBakedMesh = new Mesh();
        _activeBakedMesh.SetVertices(newVerts);
        _activeBakedMesh.SetUVs(0, newUVs);
        _activeBakedMesh.SetTriangles(newTris, 0);
        _activeBakedMesh.RecalculateNormals();
        _activeBakedMesh.RecalculateBounds();

        // 3. Apply character texture with Unlit shader so it's 100% sharp and recognizable
        _scareMeshFilter.sharedMesh = _activeBakedMesh;
        if (_unlitMaterial != null)
        {
            Texture charTex = null;
            if (isGuardCatch)
            {
                charTex = LoadTexture("Assets/NPC Assets/Animations/Texture/guard.png")
                       ?? LoadTexture("Assets/NPC Assets/Texture/guard.png");
            }
            if (charTex == null && chosenSMR.sharedMaterial != null)
            {
                charTex = chosenSMR.sharedMaterial.mainTexture;
                if (charTex == null && chosenSMR.sharedMaterial.HasProperty("_BaseMap"))
                    charTex = chosenSMR.sharedMaterial.GetTexture("_BaseMap");
            }
            if (charTex != null)
                _unlitMaterial.mainTexture = charTex;
        }

        // 4. Audio scream / horror sting at FULL VOLUME
        if (_audioSource != null)
        {
            if (scareStingClip == null) LoadDefaultAudio();
            AudioClip clipToPlay = (Random.value < 0.65f && scareStingClip != null) ? scareStingClip : proctorStingClip;
            if (clipToPlay == null) clipToPlay = scareStingClip;
            if (clipToPlay != null)
            {
                _audioSource.volume = jumpscareVolume;
                _audioSource.PlayOneShot(clipToPlay, jumpscareVolume);
            }
        }

        // 5. Increment Anxiety Meter!
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(jumpscareAnxietyIncrease);
            Debug.Log($"[NPCJumpscareManager] Added +{jumpscareAnxietyIncrease} anxiety on NPC jumpscare! Current: {AnxietyManager.Instance.CurrentAnxiety:F1}");
        }

        // Deduct stamina
        if (_staminaSystem != null)
        {
            _staminaSystem.currentStamina = Mathf.Max(5f, _staminaSystem.currentStamina - staminaDrain);
        }

        // 6. Camera Shock Dynamics & Controls Lock
        float startFOV = (_playerCam != null) ? _playerCam.fieldOfView : 60f;
        float jumpscareFOV = 40.0f; // Close-up vertigo focal punch
        Vector3 camBaseLocalPos = (_playerCam != null) ? _playerCam.transform.localPosition : Vector3.zero;
        Quaternion camBaseLocalRot = (_playerCam != null) ? _playerCam.transform.localRotation : Quaternion.identity;

        if (_playerMovement != null)
        {
            _playerMovement.isJumpscareCameraOverride = true;
            _playerMovement.SetControlsEnabled(false);
        }

        // Activate rig and horror vignette
        _scareRig.SetActive(true);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(true);

        try
        {
            float elapsed = 0f;
            float baseScale = faceScale;

            // 7. Screamer Loop:
            // Lunging head elevated slightly higher and staring straight into camera lens with violent tremors
            float basePitch = 0.0f; // Upright direct eye contact with camera lens

            while (elapsed < scareDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = (scareDuration > 0f) ? Mathf.Clamp01(elapsed / scareDuration) : 1f;

                // Fast slam towards camera lens
                float sinVal = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 0.5f));
                float lungeCurve = Mathf.Pow(sinVal, 0.6f);
                float lungeZ = Mathf.Lerp(startDistance, closestDistance, lungeCurve);
                if (float.IsNaN(lungeZ)) lungeZ = closestDistance;

                // Vertigo FOV punch (first 0.28s snaps down to 40 FOV, then holds)
                if (_playerCam != null)
                {
                    float fovProgress = Mathf.Clamp01(elapsed / 0.28f);
                    _playerCam.fieldOfView = Mathf.Lerp(startFOV, jumpscareFOV, Mathf.Pow(fovProgress, 0.4f));
                }

                // Violent high-frequency jitter (centered around headHeightOffset, no sideways tilt)
                float intensity = 1.0f - (t * 0.30f);
                float jitterX = Random.Range(-0.018f, 0.018f) * intensity;
                float jitterY = Random.Range(-0.018f, 0.018f) * intensity;
                float jitterZ = Random.Range(-0.010f, 0.010f) * intensity;

                float pitchJitter = Random.Range(-3.0f, 3.0f) * intensity;
                float yawJitter = Random.Range(-3.0f, 3.0f) * intensity;
                float rollJitter = Random.Range(-3.0f, 3.0f) * intensity;

                float scalePulse = baseScale * (1.0f + Random.Range(-0.04f, 0.04f) * intensity);

                _scareRig.transform.localScale = Vector3.one * scalePulse;
                _scareRig.transform.localPosition = new Vector3(jitterX, headHeightOffset + jitterY, lungeZ + jitterZ);
                _scareRig.transform.localRotation = Quaternion.Euler(basePitch + pitchJitter, 180f + yawJitter, rollJitter);

                // Camera involuntary recoil & tremors
                if (_playerCam != null)
                {
                    float camRecoilY = Mathf.Sin(t * Mathf.PI) * 0.035f;
                    float camRecoilZ = -Mathf.Sin(t * Mathf.PI * 0.5f) * 0.05f; // Pulls back in shock
                    float camShakeX = Random.Range(-0.022f, 0.022f) * intensity;
                    float camShakeY = Random.Range(-0.022f, 0.022f) * intensity;
                    _playerCam.transform.localPosition = camBaseLocalPos + new Vector3(camShakeX, camRecoilY + camShakeY, camRecoilZ);

                    float recoilPitch = -Mathf.Sin(t * Mathf.PI) * 4.0f; // Jolt back
                    float recoilRoll = Random.Range(-1.5f, 1.5f) * intensity;
                    _playerCam.transform.localRotation = camBaseLocalRot * Quaternion.Euler(recoilPitch, 0f, recoilRoll);
                }

                if (_flashOverlay != null)
                {
                    float flashAlpha = Mathf.Lerp(0.60f, 0.05f, t) + Random.Range(-0.05f, 0.05f);
                    _flashOverlay.color = new Color(0.72f, 0.03f, 0.03f, Mathf.Clamp01(flashAlpha));
                }

                yield return null;
            }

            // Clean up 3D rig
            _scareRig.SetActive(false);

            if (_activeBakedMesh != null)
            {
                Destroy(_activeBakedMesh);
                _activeBakedMesh = null;
            }

            // Post-scare audio: static hiss, gasping for breath, and adrenaline heartbeat
            if (_audioSource != null)
            {
                if (staticHissClip != null) _audioSource.PlayOneShot(staticHissClip, jumpscareVolume * 0.65f);
                if (gaspBreathClip != null) _audioSource.PlayOneShot(gaspBreathClip, jumpscareVolume * 0.95f);
            }

            // Start post-scare heart thumping
            if (_heartbeatSource != null && heartbeatClip != null)
            {
                _heartbeatSource.clip = heartbeatClip;
                _heartbeatSource.volume = 0.85f;
                _heartbeatSource.pitch = 1.35f;
                _heartbeatSource.Play();
            }

            // 8. Smooth Recovery Phase (FOV restoration, heartbeat deceleration, calm reset)
            float recoveryElapsed = 0f;
            float recoveryDuration = 1.6f;

            while (recoveryElapsed < recoveryDuration)
            {
                recoveryElapsed += Time.unscaledDeltaTime;
                float rt = recoveryElapsed / recoveryDuration;

                if (_playerCam != null)
                {
                    _playerCam.fieldOfView = Mathf.Lerp(jumpscareFOV, startFOV, Mathf.SmoothStep(0f, 1f, rt));

                    float trem = (1f - rt) * 0.007f;
                    _playerCam.transform.localPosition = Vector3.Lerp(_playerCam.transform.localPosition, camBaseLocalPos, Time.unscaledDeltaTime * 8f)
                                                        + new Vector3(Random.Range(-trem, trem), Random.Range(-trem, trem), 0f);
                    _playerCam.transform.localRotation = Quaternion.Slerp(_playerCam.transform.localRotation, camBaseLocalRot, Time.unscaledDeltaTime * 8f);
                }

                if (_flashOverlay != null)
                {
                    float fa = Mathf.Lerp(0.18f, 0f, rt);
                    _flashOverlay.color = new Color(0.55f, 0.02f, 0.02f, fa);
                }

                if (_heartbeatSource != null && _heartbeatSource.isPlaying)
                {
                    _heartbeatSource.pitch = Mathf.Lerp(1.35f, 0.75f, rt);
                    _heartbeatSource.volume = Mathf.Lerp(0.85f, 0f, rt);
                }

                yield return null;
            }
        }
        finally
        {
            if (_heartbeatSource != null && _heartbeatSource.isPlaying) _heartbeatSource.Stop();

            // Exact camera reset
            if (_playerCam != null)
            {
                _playerCam.fieldOfView = startFOV;
                _playerCam.transform.localPosition = camBaseLocalPos;
                _playerCam.transform.localRotation = camBaseLocalRot;
            }
            if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);
            if (_scareRig != null) _scareRig.SetActive(false);
            if (_activeBakedMesh != null)
            {
                Destroy(_activeBakedMesh);
                _activeBakedMesh = null;
            }

            // Restore player movement controls and camera look
            if (_playerMovement != null)
            {
                _playerMovement.isJumpscareCameraOverride = false;
                _playerMovement.SetControlsEnabled(true);
            }

            _isScaring = false;
        }
    }
}
