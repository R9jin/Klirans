using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPCJumpscareManager — In-Your-Face FNAF / Fortnite-style NPC face jumpscare:
/// 1. Directly frames the horrifying face of a wandering student (Drei, Niel, Josua, Jessa, Glad, Ira) on screen.
/// 2. Isolates the head and neck mesh centered at (0, 0, 0) and renders it with an Unlit shader
///    so facial features (eyes, expression, mouth) are 100% crisp, clear, and recognizable (no white blowout blob).
/// 3. Lunges towards the camera glass with violent high-frequency jitter and tremors.
/// 4. Restricts jumpscares STRICTLY to hallways and circulation areas (blocked when inside any of the 24 rooms, on stairs, or in dialogue/puzzles).
/// </summary>
public class NPCJumpscareManager : MonoBehaviour
{
    public static NPCJumpscareManager Instance { get; private set; }

    [Header("Timing Settings")]
    [Tooltip("Minimum seconds between random jumpscares.")]
    public float minInterval = 70f;

    [Tooltip("Maximum seconds between random jumpscares.")]
    public float maxInterval = 150f;

    [Header("Jumpscare Tuning")]
    [Tooltip("Start distance of the lunging face from camera.")]
    public float startDistance = 0.58f;

    [Tooltip("End distance of the lunging face at closest point (right in your face).")]
    public float closestDistance = 0.26f;

    [Tooltip("Face scale multiplier to fill the screen (Fortnite screamer style).")]
    public float faceScale = 2.45f;

    [Tooltip("Duration of the jumpscare in seconds.")]
    public float scareDuration = 0.90f;

    [Tooltip("Stamina penalty when jumpscared.")]
    public float staminaDrain = 25f;

    [Tooltip("Maximum distance (metres) between the player and a walking NPC for the random jumpscare to be eligible. " +
             "Think of it as personal-space violation — the NPC must be right next to the player.")]
    public float personalSpaceRadius = 1.8f;

    [Header("Audio Clips")]
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

    private float _nextScareTime = 0f;
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

    private List<SkinnedMeshRenderer> _cachedStudentSMRs = new List<SkinnedMeshRenderer>();
    private Mesh _activeBakedMesh;

    // Tracks which NPC violated personal space and should be shown in the next jumpscare
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
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1.0f;

        LoadDefaultAudio();
        CacheNPCSkinRenderers();
        CacheRoomBounds();
        BuildScareRig();
        ScheduleNextScare();
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
    }

    /// <summary>
    /// Caches specifically the wandering students so the jumpscare clearly features their face.
    /// </summary>
    public void CacheNPCSkinRenderers()
    {
        _cachedStudentSMRs.Clear();

        foreach (var name in _wanderingStudentNames)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                {
                    _cachedStudentSMRs.Add(smr);
                }
            }
        }

        // Fallback: search all student clearance NPCs
        if (_cachedStudentSMRs.Count == 0)
        {
            var allSMRs = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            foreach (var smr in allSMRs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.transform.root.CompareTag("Player")) continue;
                if (smr.transform.root.name.Contains("LobbyArea") || smr.gameObject.name.Contains("ClearanceNPC"))
                {
                    _cachedStudentSMRs.Add(smr);
                }
            }
        }

        Debug.Log($"[NPCJumpscareManager] Cached {_cachedStudentSMRs.Count} wandering student character models for jumpscares.");
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
                    // Exclude service counters protruding through hallway walls
                    if (c.gameObject.name.Contains("ReceptionServiceWindow")) continue;
                    if (first) { b = c.bounds; first = false; }
                    else b.Encapsulate(c.bounds);
                }
                if (!first)
                {
                    // Slightly contract bounds horizontally so doorways aren't falsely flagged as inside
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

        // Dedicated Unlit material: NO specular blowout, NO white blob!
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        _unlitMaterial = new Material(unlitShader);
        _scareMeshRenderer.sharedMaterial = _unlitMaterial;

        // Horror flash vignette on HudCanvas
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

        if (Time.time >= _nextScareTime)
        {
            if (CanTriggerScare())
            {
                // ── Personal-space proximity gate ──────────────────────────
                // The jumpscare only fires when the player is within
                // personal-space distance of one of the wandering NPCs.
                // We find the closest NPC and use their face for the scare.
                SkinnedMeshRenderer nearestSMR = GetNPCInPersonalSpace();
                if (nearestSMR != null)
                {
                    _proximityChosenSMR = nearestSMR;
                    TriggerJumpscare();
                }
                else
                {
                    // No NPC is close enough yet — retry in a short interval
                    _nextScareTime = Time.time + 4f;
                }
            }
            else
            {
                _nextScareTime = Time.time + 8f;
            }
        }
    }

    /// <summary>
    /// Returns the SkinnedMeshRenderer of the wandering NPC that is currently
    /// within <see cref="personalSpaceRadius"/> metres of the player,
    /// or null if no NPC is that close.
    /// </summary>
    private SkinnedMeshRenderer GetNPCInPersonalSpace()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return null;

        Vector3 playerPos = playerGO.transform.position;

        if (_cachedStudentSMRs.Count == 0) CacheNPCSkinRenderers();

        SkinnedMeshRenderer closest = null;
        float closestDist = float.MaxValue;

        foreach (var smr in _cachedStudentSMRs)
        {
            if (smr == null) continue;

            // Use the NPC root transform position for the distance check
            Transform npcRoot = smr.transform.root;
            float dist = Vector3.Distance(playerPos, npcRoot.position);

            if (dist <= personalSpaceRadius && dist < closestDist)
            {
                closestDist = dist;
                closest = smr;
            }
        }

        if (closest != null)
            Debug.Log($"[NPCJumpscareManager] NPC in personal space at {closestDist:F2} m → triggering jumpscare with {closest.transform.root.name}.");

        return closest;
    }

    /// <summary>
    /// Ensures jumpscares happen ONLY in hallways and circulation areas, never inside rooms.
    /// </summary>
    public bool CanTriggerScare()
    {
        if (PauseMenu.GameIsPaused) return false;
        if (LockerHideManager.IsPlayerHidden) return false;  // Player is hiding in a locker
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return false;

        var guidancePuzzle = GuidanceWordPuzzle.Instance;
        if (guidancePuzzle != null && guidancePuzzle.IsOpen) return false;

        var cashierPuzzle = CashierBalancePuzzle.Instance;
        if (cashierPuzzle != null && cashierPuzzle.IsOpen) return false;

        var registrarPuzzle = RegistrarDocumentSortPuzzle.Instance;
        if (registrarPuzzle != null && registrarPuzzle.IsOpen) return false;

        // Never scare while player is climbing or transitioning stairs
        if (IsPlayerOnStairs()) return false;

        // Jumpscare the player ONLY if he is not inside rooms and is in the hallways!
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

        if (_cachedStudentSMRs.Count == 0) CacheNPCSkinRenderers();
        if (_cachedStudentSMRs.Count == 0)
        {
            _isScaring = false;
            yield break;
        }

        // Use the NPC that violated the player's personal space (set in Update).
        // Falls back to a random wandering student only when triggered manually (e.g. F8).
        SkinnedMeshRenderer chosenSMR = _proximityChosenSMR;
        _proximityChosenSMR = null; // consume it

        if (chosenSMR == null)
        {
            int idx = Random.Range(0, _cachedStudentSMRs.Count);
            chosenSMR = _cachedStudentSMRs[idx];
        }

        if (chosenSMR == null)
        {
            CacheNPCSkinRenderers();
            chosenSMR = _cachedStudentSMRs.Count > 0 ? _cachedStudentSMRs[0] : null;
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

        // Compute the true face center (eyes and bridge of nose)
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

        // 2. Center all vertices directly on the eyes/nose and isolate head & face triangles
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

            // Keep triangle if vertices belong to the face/head zone
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

        // 4. Audio scream / horror sting
        if (_audioSource != null)
        {
            AudioClip clipToPlay = (Random.value < 0.65f && scareStingClip != null) ? scareStingClip : proctorStingClip;
            if (clipToPlay == null) clipToPlay = scareStingClip;
            if (clipToPlay != null) _audioSource.PlayOneShot(clipToPlay, 1.0f);
        }

        // 5. Activate rig and overlay
        _scareRig.SetActive(true);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(true);

        // Deduct stamina
        if (_staminaSystem != null)
        {
            _staminaSystem.currentStamina = Mathf.Max(5f, _staminaSystem.currentStamina - staminaDrain);
        }

        float elapsed = 0f;
        float baseScale = faceScale;

        // 6. Michael Jackson / Fortnite Screamer Loop:
        // Huge in-your-face lunging head with violent tilt, jitter, and screen shudder
        float randomTilt = Random.Range(-12f, 12f);
        float basePitch = 16.0f; // Forward pitch so eyes stare directly into player camera lens

        while (elapsed < scareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scareDuration;

            // Aggressive snap-forward lunge: fast initial slam towards camera
            float lungeCurve = Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.6f);
            float lungeZ = Mathf.Lerp(startDistance, closestDistance, lungeCurve);

            // Violent high-frequency jitter (rapid convulsions)
            float intensity = 1.0f - (t * 0.30f);
            float jitterX = Random.Range(-0.022f, 0.022f) * intensity;
            float jitterY = Random.Range(-0.022f, 0.022f) * intensity;
            float jitterZ = Random.Range(-0.012f, 0.012f) * intensity;

            float pitchJitter = Random.Range(-6f, 6f) * intensity;
            float yawJitter = Random.Range(-8f, 8f) * intensity;
            float rollJitter = randomTilt + Random.Range(-5f, 5f) * intensity;

            // Micro-pulsing scale to enhance the breathing/screaming terror
            float scalePulse = baseScale * (1.0f + Random.Range(-0.035f, 0.035f) * intensity);

            // Eyes and nose are centered at (0,0,0) with forward tilt staring into your face
            _scareRig.transform.localScale = Vector3.one * scalePulse;
            _scareRig.transform.localPosition = new Vector3(jitterX, jitterY, lungeZ + jitterZ);
            _scareRig.transform.localRotation = Quaternion.Euler(basePitch + pitchJitter, 180f + yawJitter, rollJitter);

            // Red horror vignette pulse (leaves center face crisp and clear)
            if (_flashOverlay != null)
            {
                float flashAlpha = Mathf.Lerp(0.24f, 0f, t);
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
            if (staticHissClip != null) _audioSource.PlayOneShot(staticHissClip, 0.5f);
            if (gaspBreathClip != null) _audioSource.PlayOneShot(gaspBreathClip, 0.85f);
        }

        _isScaring = false;
        ScheduleNextScare();
    }

    private void ScheduleNextScare()
    {
        _nextScareTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
