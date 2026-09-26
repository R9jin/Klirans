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
    public static NPCJumpscareManager Instance { get; private set; }

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
    [Tooltip("Distance threshold (meters, horizontal XZ) where the player is touching or blocking a walking student.")]
    public float touchDistanceThreshold = 1.30f;

    [Range(0f, 1f)]
    [Tooltip("Probability of being jumpscared when physically touching or blocking a walking student (near 50/50).")]
    public float touchScareChance = 0.50f;

    [Tooltip("Cooldown in seconds after a jumpscare occurs before another jumpscare can happen (pacing: not too frequent).")]
    public float jumpscareGlobalCooldown = 18.0f;

    [Tooltip("Cooldown in seconds after touching/blocking an NPC and rolling safe before checking touch again.")]
    public float touchRerollCooldown = 5.0f;

    [Header("Flashlight Blinding Punishment")]
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
    public float closestDistance = 0.25f;

    [Tooltip("Face scale multiplier to fill the screen.")]
    public float faceScale = 2.45f;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [Tooltip("Master volume for jumpscare screamer.")]
    public float jumpscareVolume = 1.0f;

    public AudioClip scareStingClip;
    public AudioClip proctorStingClip;
    public AudioClip staticHissClip;
    public AudioClip gaspBreathClip;

    // ── Internal References ──────────────────────────────────────────────────
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private StaminaSystem _staminaSystem;
    private AudioSource _audioSource;

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

    // ── Wandering Student sources ────────────────────────────────────────────
    private readonly string[] _wanderingStudentNames = new string[]
    {
        "ClearanceNPC_Drei",
        "ClearanceNPC_Niel",
        "ClearanceNPC_Josua",
        "ClearanceNPC_Jessa",
        "ClearanceNPC_Glad",
        "ClearanceNPC_Ira"
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

        LoadDefaultAudio();
        CacheWanderingNPCs();
        CacheRoomBounds();
        BuildScareRig();
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
    }

    /// <summary>
    /// Caches all wandering student character models, true 3D transforms, and colliders.
    /// </summary>
    public void CacheWanderingNPCs()
    {
        _cachedNPCs.Clear();

        foreach (var name in _wanderingStudentNames)
        {
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
        _scareRig.transform.localPosition = new Vector3(0f, 0f, startDistance);
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
        // Debug trigger for testing: press F8 to test jumpscare immediately
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Debug.Log("[NPCJumpscareManager] Manual F8 jumpscare triggered!");
            TriggerJumpscare();
            return;
        }

        if (_isScaring) return;

        // ── 1. PRIMARY TRIGGER: Really Touching / Blocking Walking NPC in Hallways ───
        if (Time.time >= _cooldownUntil && Time.time >= _touchRerollCooldownUntil && CanTriggerScare())
        {
            var (touchNPC, distXZ) = GetTouchedNPC(touchDistanceThreshold);
            if (touchNPC != null)
            {
                HandleTouchOrBlock(touchNPC, $"Continuous Proximity/Block (distXZ={distXZ:F2}m)");
                return;
            }
        }

        // ── 2. FLASHLIGHT BLINDING PUNISHMENT ────────────────────────────────────
        if (Time.time >= _cooldownUntil && CanTriggerScare())
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
    /// Direct PhysX collision callback: called when CharacterController hits a collider while moving.
    /// Guarantees that walking into / pushing against the NPC triggers the touch check.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (_isScaring) return;
        if (Time.time < _cooldownUntil || Time.time < _touchRerollCooldownUntil) return;
        if (!CanTriggerScare()) return;

        var npc = FindMatchingNPC(hit.collider.gameObject);
        if (npc != null)
        {
            HandleTouchOrBlock(npc, "Physical Collision (OnControllerColliderHit)");
        }
    }

    /// <summary>
    /// Evaluates the 50/50 jumpscare roll when the player touches or blocks a walking student.
    /// If scare triggers, initiates jumpscare and applies long global cooldown (not too frequent).
    /// If safe, applies a reroll debounce so the player isn't spammed with checks while touching.
    /// </summary>
    private void HandleTouchOrBlock(WanderingNPCData npc, string source)
    {
        if (_isScaring) return;
        if (Time.time < _cooldownUntil || Time.time < _touchRerollCooldownUntil) return;
        if (!CanTriggerScare()) return;

        float roll = Random.value;
        bool shouldScare = (roll < touchScareChance);

        Debug.Log($"[NPCJumpscareManager] Player touched/blocked {npc.name} ({source}). 50/50 Roll: {roll:F2} < {touchScareChance:F2} => {(shouldScare ? "JUMPSCARE!" : "SAFE (reroll cooldown)")}");

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
            if (npc == null || npc.gameObject == null) continue;
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
            if (npc == null || npc.gameObject == null) continue;

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
            if (npc == null || npc.gameObject == null) continue;

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
        if (PauseMenu.GameIsPaused) return false;
        if (LockerHideManager.IsPlayerHidden) return false;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return false;

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
    public void TriggerJumpscare()
    {
        if (_isScaring || _scareRig == null) return;
        StartCoroutine(FortniteStyleJumpscareSequence());
    }

    private IEnumerator FortniteStyleJumpscareSequence()
    {
        _isScaring = true;

        if (_cachedNPCs.Count == 0) CacheWanderingNPCs();
        if (_cachedNPCs.Count == 0)
        {
            _isScaring = false;
            yield break;
        }

        SkinnedMeshRenderer chosenSMR = _proximityChosenSMR;
        _proximityChosenSMR = null;

        if (chosenSMR == null)
        {
            int idx = Random.Range(0, _cachedNPCs.Count);
            chosenSMR = _cachedNPCs[idx].smr;
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

        // Compute true face center (eyes and bridge of nose)
        float eyeY = maxY - 0.15f;
        float sumZ = 0f;
        int countZ = 0;
        for (int i = 0; i < rawVerts.Length; i++)
        {
            if (rawVerts[i].y >= maxY - 0.25f)
            {
                sumZ += rawVerts[i].z;
                countZ++;
            }
        }
        float centerZ = countZ > 0 ? (sumZ / countZ) : 0f;
        Vector3 faceCenter = new Vector3(0f, eyeY, centerZ);

        // 2. Center vertices directly on eyes/nose and isolate head & face triangles
        var newVerts = new List<Vector3>();
        var newUVs = new List<Vector2>();
        var newTris = new List<int>();
        var oldToNew = new Dictionary<int, int>();

        float keepMinY = -0.22f; // Chin & upper collar cutoff
        float keepMaxY = 0.22f;  // Top of hair cutoff

        for (int i = 0; i < rawTris.Length; i += 3)
        {
            int i1 = rawTris[i], i2 = rawTris[i + 1], i3 = rawTris[i + 2];
            Vector3 v1 = rawVerts[i1] - faceCenter;
            Vector3 v2 = rawVerts[i2] - faceCenter;
            Vector3 v3 = rawVerts[i3] - faceCenter;

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

        // 3. Apply student texture with Unlit shader so it's 100% sharp and recognizable
        _scareMeshFilter.sharedMesh = _activeBakedMesh;
        if (_unlitMaterial != null && chosenSMR.sharedMaterial != null)
        {
            _unlitMaterial.mainTexture = chosenSMR.sharedMaterial.mainTexture;
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

        // 6. Activate rig and horror vignette
        _scareRig.SetActive(true);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(true);

        float elapsed = 0f;
        float baseScale = faceScale;

        // 7. Screamer Loop:
        // Lunging head staring straight into camera lens with violent tilt, jitter, and screen shudder
        float randomTilt = Random.Range(-10f, 10f);
        float basePitch = 15.0f; // Stare straight into camera lens

        while (elapsed < scareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scareDuration;

            // Fast slam towards camera lens
            float lungeCurve = Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.6f);
            float lungeZ = Mathf.Lerp(startDistance, closestDistance, lungeCurve);

            // Violent high-frequency jitter
            float intensity = 1.0f - (t * 0.30f);
            float jitterX = Random.Range(-0.024f, 0.024f) * intensity;
            float jitterY = Random.Range(-0.024f, 0.024f) * intensity;
            float jitterZ = Random.Range(-0.012f, 0.012f) * intensity;

            float pitchJitter = Random.Range(-6f, 6f) * intensity;
            float yawJitter = Random.Range(-8f, 8f) * intensity;
            float rollJitter = randomTilt + Random.Range(-5f, 5f) * intensity;

            float scalePulse = baseScale * (1.0f + Random.Range(-0.035f, 0.035f) * intensity);

            _scareRig.transform.localScale = Vector3.one * scalePulse;
            _scareRig.transform.localPosition = new Vector3(jitterX, jitterY, lungeZ + jitterZ);
            _scareRig.transform.localRotation = Quaternion.Euler(basePitch + pitchJitter, 180f + yawJitter, rollJitter);

            if (_flashOverlay != null)
            {
                float flashAlpha = Mathf.Lerp(0.25f, 0f, t);
                _flashOverlay.color = new Color(0.65f, 0.04f, 0.04f, flashAlpha);
            }

            yield return null;
        }

        // Clean up
        _scareRig.SetActive(false);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);

        if (_activeBakedMesh != null)
        {
            Destroy(_activeBakedMesh);
            _activeBakedMesh = null;
        }

        // Post-scare audio: static hiss and gasping for breath
        if (_audioSource != null)
        {
            if (staticHissClip != null) _audioSource.PlayOneShot(staticHissClip, jumpscareVolume * 0.7f);
            if (gaspBreathClip != null) _audioSource.PlayOneShot(gaspBreathClip, jumpscareVolume * 0.9f);
        }

        _isScaring = false;
    }
}
