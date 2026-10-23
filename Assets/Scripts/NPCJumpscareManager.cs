using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Manages creepy hallway interactions with wandering student NPCs AND faculty/signatories:
/// - Tracks all student NPCs and clearance faculty (Librarian, Guidance, Dean, Registrar, Cashier, EVP)
/// - Makes NPC heads eerily turn and stare at the player when nearby (Head Bone Override in LateUpdate)
/// - Plays subtle creepy whispers/mumbles when passing close to an NPC
/// - Triggers an intense, terrifying IN-YOUR-FACE 3D screaming jumpscare with:
///     * Dynamic 5-light horror face rig (blood-red side lights, under-chin uplight, piercing eye-glint light)
///     * 2D procedural blood-shot / dilated horror eyes overlay & bloody screen vignette
///     * Extreme FOV zoom punch (60 -> 26 FOV) + violent camera shake & recoil
///     * Layered multi-track screamer audio + post-scare gasping & pounding heart recovery
/// </summary>
public class NPCJumpscareManager : MonoBehaviour
{
    private static NPCJumpscareManager _instance;
    public static NPCJumpscareManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<NPCJumpscareManager>(FindObjectsInactive.Include);
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Touch / Block Detection")]
    [Tooltip("Distance at which walking into an NPC counts as touching/blocking them (forgiving radius so passing in hallways is safe).")]
    public float touchDistanceThreshold = 0.52f;

    [Tooltip("Probability (0.0 to 1.0) that touching/bumping an NPC triggers a jumpscare.")]
    [Range(0f, 1f)]
    public float touchScareChance = 0.18f;

    [Tooltip("Cooldown before the same NPC can roll for a touch scare again if the roll didn't trigger.")]
    public float touchRerollCooldown = 8f;

    [Header("Eerie Hallway Stare & Whispers")]
    public float flashlightInFaceRadius = 3.5f;
    [Tooltip("Distance within which NPCs will slowly turn their heads to stare at the player.")]
    public float eerieStareRadius = 5.5f;

    [Tooltip("Max head yaw angle (left/right) for NPCs staring at the player.")]
    public float maxHeadTurnYaw = 68f;

    [Tooltip("Max head pitch angle (up/down) for NPCs staring at the player.")]
    public float maxHeadTurnPitch = 35f;

    [Tooltip("Minimum seconds between eerie hallway whispers when passing students.")]
    public float whisperCooldown = 14f;

    [Header("Jumpscare Timing & Cooldown")]
    [Tooltip("Minimum seconds between any two jumpscares so it stays scary, not spammy.")]
    public float jumpscareGlobalCooldown = 25f;

    [Tooltip("How long the 3D screaming face stays in front of the camera.")]
    public float scareDuration = 1.05f;

    [Header("3D Face Framing (Camera Space)")]
    [Tooltip("Starting Z distance in front of the camera for the lunging slam.")]
    public float startDistance = 0.72f;

    [Tooltip("Closest Z distance right in front of the camera lens (fills screen with full head).")]
    public float closestDistance = 0.36f;

    [Tooltip("Vertical offset in camera space so the NPC's eyes and screaming mouth are dead-center.")]
    public float headHeightOffset = 0.02f;

    [Tooltip("Scale multiplier for the isolated head mesh during the jumpscare.")]
    public float faceScale = 1.15f;

    [Header("Gameplay Impact")]
    [Tooltip("Stamina drained when startled by a jumpscare.")]
    public float staminaDrain = 10f;

    [Tooltip("Amount of anxiety (0-100) added to the AnxietyManager when jumpscared by a wandering NPC.")]
    public float jumpscareAnxietyIncrease = 2.0f;

    [Tooltip("Master volume for the jumpscare scream audio.")]
    [Range(0.1f, 1f)]
    public float jumpscareVolume = 1.0f;

    [Header("Audio Clips (Auto-loaded if empty)")]
    public AudioClip scareStingClip;
    public AudioClip proctorStingClip;
    public AudioClip staticHissClip;
    public AudioClip gaspBreathClip;
    public AudioClip heartbeatClip;
    public AudioClip whisperMurmurClip;
    public AudioClip crazyMumbleClip;

    // ── Internal Runtime State ────────────────────────────────────────────────
    private Transform _playerTransform;
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private CharacterController _playerCC;
    private StaminaSystem _staminaSystem;

    private class TrackedNPC
    {
        public Transform root;
        public SkinnedMeshRenderer smr;
        public Animator animator;
        public NavMeshAgent agent;
        public ProctorAI proctorAI;
        public Transform headBone;
        public Transform neckBone;
        public float nextTouchRollTime;
        public float currentLookWeight;
        public Quaternion smoothedHeadRot = Quaternion.identity;
        public bool isFaculty;
    }

    private readonly List<TrackedNPC> _cachedNPCs = new List<TrackedNPC>();
    private float _nextCacheRefreshTime = 0f;
    private float _lastScareTime = -999f;
    private float _lastWhisperTime = -999f;
    private bool _isScaring = false;
    private Coroutine _activeScareCoroutine;

    /// <summary>True while a 3D jumpscare is actively playing on screen.</summary>
    public bool IsScaring => _isScaring;

    // 3D Jumpscare Rig parented to Player Camera
    private GameObject _scareRig;
    private MeshFilter _scareMeshFilter;
    private MeshRenderer _scareMeshRenderer;
    private Light _scarePointLight;      // Warm eerie front fill
    private Light _scareLeftRedLight;    // Blood-red rim light (left)
    private Light _scareRightRedLight;   // Deep crimson rim light (right)
    private Light _scareUnderLight;      // Flashlight-under-chin horror uplight
    private Material _unlitMaterial;
    private Mesh _activeBakedMesh;

    // Screen Vignette Overlay (HudCanvas)
    private Image _flashOverlay;

    // Dedicated 2D AudioSources for Jumpscare & Whispers
    private AudioSource _audioSource;
    private AudioSource _secondaryScreamSource;
    private AudioSource _heartbeatSource;
    private AudioSource _whisperSource;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;

        jumpscareAnxietyIncrease = 2.0f;
        staminaDrain = Mathf.Min(staminaDrain, 10f);
        touchDistanceThreshold = Mathf.Min(touchDistanceThreshold, 0.62f);
        if (touchDistanceThreshold <= 0.1f) touchDistanceThreshold = 0.62f;
        touchScareChance = Mathf.Min(touchScareChance, 0.22f);
        touchRerollCooldown = Mathf.Max(touchRerollCooldown, 8f);
        jumpscareGlobalCooldown = Mathf.Max(jumpscareGlobalCooldown, 25f);
    }

    private void Start()
    {
        FindPlayerReferences();
        LoadDefaultAudio();
        SetupAudioSources();
        SetupFlashOverlay();
        SetupScareRig();
        RefreshNPCCache();
    }

    private void FindPlayerReferences()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerMovement = playerObj.GetComponent<PlayerMovement>();
            _playerCC = playerObj.GetComponent<CharacterController>();
            _staminaSystem = playerObj.GetComponent<StaminaSystem>();
            if (_playerMovement != null && _playerMovement.playerCamera != null)
            {
                _playerCam = _playerMovement.playerCamera;
            }
        }

        if (_playerCam == null)
        {
            _playerCam = Camera.main;
        }
    }

    /// <summary>
    /// Applies a temporary grace period where wandering NPCs cannot jumpscare the player.
    /// </summary>
    public void ApplyGraceCooldown(float durationSeconds)
    {
        _lastScareTime = Mathf.Max(_lastScareTime, Time.time - jumpscareGlobalCooldown + durationSeconds);
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
            gaspBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/out of breath.mp3");
        if (heartbeatClip == null)
            heartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Heartbeat sound effect.mp3");
        if (whisperMurmurClip == null)
            whisperMurmurClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/creepy_murmur.wav");
        if (crazyMumbleClip == null)
            crazyMumbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
#endif
        if (scareStingClip == null)
            scareStingClip = Resources.Load<AudioClip>("Sounds/you died (lobotomy sound)");
        if (proctorStingClip == null)
            proctorStingClip = Resources.Load<AudioClip>("Sounds/encountering a proctor");
        if (staticHissClip == null)
            staticHissClip = Resources.Load<AudioClip>("Sounds/vhs static");
        if (gaspBreathClip == null)
            gaspBreathClip = Resources.Load<AudioClip>("Sounds/out of breath");
        if (heartbeatClip == null)
            heartbeatClip = Resources.Load<AudioClip>("Sounds/Heartbeat sound effect");
        if (whisperMurmurClip == null)
            whisperMurmurClip = Resources.Load<AudioClip>("Sounds/creepy_murmur");
        if (crazyMumbleClip == null)
            crazyMumbleClip = Resources.Load<AudioClip>("Sounds/freesound_community-human_male_crazy-mumbles_1-30950");
    }

    private Texture LoadTexture(string path)
    {
#if UNITY_EDITOR
        Texture tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
        if (tex != null) return tex;
#endif
        string resPath = path.Replace("Assets/Resources/", "").Replace("Assets/", "");
        int dot = resPath.LastIndexOf('.');
        if (dot > 0) resPath = resPath.Substring(0, dot);
        return Resources.Load<Texture>(resPath);
    }

    private void SetupAudioSources()
    {
        // Primary scream source
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // Full 2D stereo in-your-head scream
        _audioSource.playOnAwake = false;
        _audioSource.volume = jumpscareVolume;
        _audioSource.priority = 0;

        // Secondary layered shriek/sting source (pitched up for piercing terror)
        _secondaryScreamSource = gameObject.AddComponent<AudioSource>();
        _secondaryScreamSource.spatialBlend = 0f;
        _secondaryScreamSource.playOnAwake = false;
        _secondaryScreamSource.volume = jumpscareVolume;
        _secondaryScreamSource.priority = 1;

        // Post-scare pounding heart source
        _heartbeatSource = gameObject.AddComponent<AudioSource>();
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.playOnAwake = false;
        _heartbeatSource.loop = true;
        _heartbeatSource.volume = 0f;

        // Hallway eerie whisper source
        _whisperSource = gameObject.AddComponent<AudioSource>();
        _whisperSource.spatialBlend = 0f;
        _whisperSource.playOnAwake = false;
        _whisperSource.volume = 0.45f;
    }

    private void SetupFlashOverlay()
    {
        var hudCanvas = GameObject.Find("HudCanvas");
        if (hudCanvas == null) return;

        // Blood-red / dark horror vignette flash
        var flashObj = new GameObject("JumpscareFlashOverlay");
        flashObj.transform.SetParent(hudCanvas.transform, false);

        var rect = flashObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _flashOverlay = flashObj.AddComponent<Image>();
        _flashOverlay.color = new Color(0.6f, 0f, 0f, 0f);
        _flashOverlay.raycastTarget = false;
        flashObj.SetActive(false);
    }

    private void SetupScareRig()
    {
        if (_playerCam == null) return;

        // Ensure camera renders as close as 5cm so the screaming face never clips into the near plane
        _playerCam.nearClipPlane = Mathf.Min(_playerCam.nearClipPlane, 0.04f);

        _scareRig = new GameObject("Jumpscare3DFaceRig");
        _scareRig.transform.SetParent(_playerCam.transform, false);
        _scareRig.transform.localPosition = new Vector3(0f, headHeightOffset, closestDistance);
        _scareRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        _scareMeshFilter = _scareRig.AddComponent<MeshFilter>();
        _scareMeshRenderer = _scareRig.AddComponent<MeshRenderer>();
        _scareMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _scareMeshRenderer.receiveShadows = false;

        // Use an Unlit shader so the character's face texture is 100% crisp, clear, and recognizable
        // without being washed out or blown out into a white/red blob by scene/point lights!
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            _unlitMaterial = new Material(unlitShader);
            // Slightly eerie cold-pale horror tint so the face looks clear & menacing
            if (_unlitMaterial.HasProperty("_BaseColor"))
                _unlitMaterial.SetColor("_BaseColor", new Color(1.0f, 0.92f, 0.92f, 1f));
            if (_unlitMaterial.HasProperty("_Color"))
                _unlitMaterial.SetColor("_Color", new Color(1.0f, 0.92f, 0.92f, 1f));
            _scareMeshRenderer.sharedMaterial = _unlitMaterial;
        }

        _scareRig.SetActive(false);
    }

    /// <summary>
    /// Finds all wandering student NPCs and seated/standing Faculty Signatories in the scene
    /// and caches their SkinnedMeshRenderers and Head/Neck bones for eerie staring & jumpscares.
    /// </summary>
    public void RefreshNPCCache()
    {
        _cachedNPCs.Clear();

        // 1. Wandering Student NPCs
        var allProctors = FindObjectsByType<ProctorAI>(FindObjectsSortMode.None);
        foreach (var p in allProctors)
        {
            if (p == null) continue;
            RegisterTrackedNPC(p.transform, p, isFaculty: false);
        }

        // 2. Clearance Faculty / Signatories (Librarian, Guidance, CCS Dean, Registrar, Cashier, EVP)
        var allFaculty = FindObjectsByType<ClearanceNPC>(FindObjectsSortMode.None);
        foreach (var f in allFaculty)
        {
            if (f == null) continue;
            RegisterTrackedNPC(f.transform, null, isFaculty: true);
        }
    }

    private void RegisterTrackedNPC(Transform root, ProctorAI proctor, bool isFaculty)
    {
        if (root == null) return;

        // Avoid duplicates
        for (int i = 0; i < _cachedNPCs.Count; i++)
        {
            if (_cachedNPCs[i].root == root) return;
        }

        var smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) return;

        Transform headBone = null;
        Transform neckBone = null;

        if (smr.bones != null)
        {
            foreach (var b in smr.bones)
            {
                if (b == null) continue;
                if (headBone == null && (b.name.EndsWith("Head") || b.name.Contains(":Head")))
                    headBone = b;
                else if (neckBone == null && (b.name.EndsWith("Neck") || b.name.Contains(":Neck")))
                    neckBone = b;
            }
        }

        // Fallback search in hierarchy if bones array didn't list them
        if (headBone == null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.EndsWith("Head") || t.name.Contains(":Head"))
                {
                    headBone = t;
                    break;
                }
            }
        }

        _cachedNPCs.Add(new TrackedNPC
        {
            root = root,
            smr = smr,
            animator = root.GetComponentInChildren<Animator>(),
            agent = root.GetComponent<NavMeshAgent>(),
            proctorAI = proctor,
            headBone = headBone,
            neckBone = neckBone,
            nextTouchRollTime = 0f,
            currentLookWeight = 0f,
            smoothedHeadRot = headBone != null ? headBone.rotation : Quaternion.identity,
            isFaculty = isFaculty
        });
    }

    private void Update()
    {
        // Never allow wandering NPCs to scare or chase during or after Game Over
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            if (_isScaring)
            {
                AbortActiveJumpscare();
            }
            return;
        }

        if (GameModeManager.IsFreeRoamMode) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;

        if (_playerTransform == null || _playerCam == null)
        {
            FindPlayerReferences();
            if (_playerCam != null && _scareRig == null) SetupScareRig();
            return;
        }

        if (Time.time >= _nextCacheRefreshTime)
        {
            _nextCacheRefreshTime = Time.time + 5f;
            if (_cachedNPCs.Count == 0) RefreshNPCCache();
        }

        // Dev Shortcut: Press [J] in Editor to test the jumpscare at any time
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.J) && !_isScaring)
        {
            TriggerJumpscare(null);
            return;
        }
#endif

        if (_isScaring) return;

        Vector3 playerPos = _playerTransform.position;

        for (int i = 0; i < _cachedNPCs.Count; i++)
        {
            var npc = _cachedNPCs[i];
            if (npc.root == null || !npc.root.gameObject.activeInHierarchy) continue;

            Vector3 npcPos = npc.root.position;

            // Must be on the same floor
            float yDiff = Mathf.Abs(playerPos.y - npcPos.y);
            if (yDiff > 1.8f) continue;

            float dx = playerPos.x - npcPos.x;
            float dz = playerPos.z - npcPos.z;
            float planarDist = Mathf.Sqrt(dx * dx + dz * dz);

            // 1. Eerie Hallway Whisper when passing close to a student (1.4m - 3.6m)
            if (!npc.isFaculty && planarDist < 3.4f && planarDist > 1.1f)
            {
                if (Time.time - _lastWhisperTime >= whisperCooldown && Random.value < 0.008f)
                {
                    PlayEerieHallwayWhisper();
                }
            }

            // 2. Touch / Block Check (only for wandering students, not desk faculty behind counters)
            if (!npc.isFaculty && planarDist <= touchDistanceThreshold)
            {
                HandleTouchOrBlock(npc, planarDist);
                if (_isScaring) break;
            }
        }
    }

    /// <summary>
    /// LateUpdate overrides NPC head bones AFTER Animator evaluation so students AND faculty
    /// eerily turn their heads to track and stare at the player as they walk by!
    /// </summary>
    private void LateUpdate()
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (GameModeManager.IsFreeRoamMode) return;
        if (_playerCam == null || _isScaring) return;

        Vector3 playerEyePos = _playerCam.transform.position;

        for (int i = 0; i < _cachedNPCs.Count; i++)
        {
            var npc = _cachedNPCs[i];
            if (npc.root == null || npc.headBone == null || !npc.root.gameObject.activeInHierarchy) continue;

            Vector3 npcPos = npc.root.position;
            float yDiff = Mathf.Abs(playerEyePos.y - npcPos.y);
            float dist = Vector3.Distance(playerEyePos, npcPos);

            // Check if player is within eerie stare range and roughly in front/side of the NPC
            bool shouldStare = false;
            if (yDiff < 2.0f && dist <= eerieStareRadius && dist > 0.45f)
            {
                Vector3 toPlayerPlanar = playerEyePos - npcPos;
                toPlayerPlanar.y = 0f;
                if (toPlayerPlanar.sqrMagnitude > 0.001f)
                {
                    float angleFromForward = Vector3.Angle(npc.root.forward, toPlayerPlanar.normalized);
                    // Can turn head if player is within 115 degrees of NPC's chest direction
                    if (angleFromForward <= 115f)
                    {
                        shouldStare = true;
                    }
                }
            }

            float targetWeight = shouldStare ? 0.88f : 0f;
            npc.currentLookWeight = Mathf.MoveTowards(npc.currentLookWeight, targetWeight, Time.deltaTime * 2.8f);

            if (npc.currentLookWeight > 0.01f)
            {
                Vector3 dirToPlayer = (playerEyePos - npc.headBone.position);
                if (dirToPlayer.sqrMagnitude > 0.001f)
                {
                    // Convert direction into NPC root local space to clamp realistic neck/head angles
                    Vector3 localDir = npc.root.InverseTransformDirection(dirToPlayer.normalized);
                    float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;

                    yaw = Mathf.Clamp(yaw, -maxHeadTurnYaw, maxHeadTurnYaw);
                    pitch = Mathf.Clamp(pitch, -maxHeadTurnPitch, maxHeadTurnPitch);

                    // Target world rotation for the head bone
                    Quaternion clampedWorldRot = npc.root.rotation * Quaternion.Euler(pitch, yaw, 0f);

                    // Add subtle unsettling micro-tremor when staring intensely at close range
                    if (dist < 2.6f && !npc.isFaculty)
                    {
                        float tremorPitch = Mathf.Sin(Time.time * 23f + i) * 1.4f;
                        float tremorRoll = Mathf.Cos(Time.time * 19f + i * 2f) * 1.8f;
                        clampedWorldRot *= Quaternion.Euler(tremorPitch, 0f, tremorRoll);
                    }

                    // Blend from current animated pose to the creepy stare pose
                    npc.headBone.rotation = Quaternion.Slerp(npc.headBone.rotation, clampedWorldRot, npc.currentLookWeight);
                }
            }
        }
    }

    private void PlayEerieHallwayWhisper()
    {
        if (_whisperSource == null || _whisperSource.isPlaying) return;

        AudioClip clip = (Random.value < 0.6f && whisperMurmurClip != null) ? whisperMurmurClip : crazyMumbleClip;
        if (clip == null) clip = whisperMurmurClip;
        if (clip != null)
        {
            _lastWhisperTime = Time.time;
            _whisperSource.pitch = Random.Range(0.78f, 1.05f);
            _whisperSource.volume = Random.Range(0.32f, 0.50f);
            _whisperSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Called by PlayerMovement.OnControllerColliderHit when the player's CharacterController physically hits an NPC collider.
    /// Uses a forgiving distance check and routes through HandleTouchOrBlock so brushing past NPCs in hallways does not cause a 100% instant jumpscare.
    /// </summary>
    public void OnPlayerControllerHit(GameObject hitObj)
    {
        if (hitObj == null) return;
        OnPlayerControllerHitTransform(hitObj.transform);
    }

    public void OnPlayerControllerHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.transform == null) return;
        OnPlayerControllerHitTransform(hit.transform);
    }

    private void OnPlayerControllerHitTransform(Transform hitTransform)
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (GameModeManager.IsFreeRoamMode) return;
        if (_isScaring || hitTransform == null || _playerTransform == null) return;

        for (int i = 0; i < _cachedNPCs.Count; i++)
        {
            var npc = _cachedNPCs[i];
            if (npc.root == null || npc.isFaculty) continue;

            if (hitTransform == npc.root || hitTransform.IsChildOf(npc.root))
            {
                float dx = _playerTransform.position.x - npc.root.position.x;
                float dz = _playerTransform.position.z - npc.root.position.z;
                float planarDist = Mathf.Sqrt(dx * dx + dz * dz);

                if (planarDist <= touchDistanceThreshold)
                {
                    HandleTouchOrBlock(npc, planarDist);
                }
                break;
            }
        }
    }

    private void HandleTouchOrBlock(TrackedNPC npc, float planarDist)
    {
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver)) return;
        if (!CanTriggerScare()) return;
        if (Time.time < npc.nextTouchRollTime) return;

        // Require the player to actually be moving into the NPC or standing right inside their core (<= 0.45m)
        bool isMovingTowardNPC = false;
        if (_playerMovement != null && _playerMovement.IsMoving)
        {
            Vector3 toNPC = (npc.root.position - _playerTransform.position);
            toNPC.y = 0f;
            Vector3 camFwd = _playerCam != null ? _playerCam.transform.forward : _playerTransform.forward;
            camFwd.y = 0f;
            if (toNPC.sqrMagnitude > 0.001f && camFwd.sqrMagnitude > 0.001f)
            {
                isMovingTowardNPC = Vector3.Dot(camFwd.normalized, toNPC.normalized) > 0.2f;
            }
        }

        if (!isMovingTowardNPC && planarDist > 0.45f) return;

        npc.nextTouchRollTime = Time.time + touchRerollCooldown;

        if (Random.value <= touchScareChance)
        {
            // Trigger through the specific NPC's ProctorAI so THIS exact NPC chases or jumpscares the player
            if (npc.proctorAI != null)
            {
                npc.proctorAI.TriggerChase();
            }
            else
            {
                TriggerJumpscare(npc.smr);
            }
        }
    }

    private bool CanTriggerScare()
    {
        if (GameModeManager.IsFreeRoamMode) return false;
        if (DevDemoPanel.IsGodModeActive) return false;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return false;
        if (GameOverUI.IsCutsceneActive) return false;
        if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver) return false;
        if (_isScaring) return false;
        if (Time.time - _lastScareTime < jumpscareGlobalCooldown) return false;
        if (PauseMenu.GameIsPaused) return false;
        if (LockerHideManager.IsPlayerHidden) return false;

        // Do not let passive hallway wandering NPCs interrupt an open puzzle, safe cracking minigame, or modal
        if (PauseMenu.IsAnyActivePuzzleOrModalOpen) return false;

        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return false;

        return true;
    }

    /// <summary>
    /// Can be called by puzzles, lockpicking, or faculty when the player makes a mistake or fails a check.
    /// Rolls a chance to trigger an immediate terrifying jumpscare.
    /// </summary>
    public void PunishPlayerError(string reason, float probability = 0.55f)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (GameOverUI.IsCutsceneActive) return;
        if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver) return;
        if (_isScaring) return;

        // Shorter cooldown for error punishment (6s instead of full global cooldown)
        if (Time.time - _lastScareTime < 6.0f) return;

        if (Random.value <= probability)
        {
            // Pick the closest NPC (including faculty if the player is right in front of a signatory window!)
            SkinnedMeshRenderer closestSMR = null;
            float bestDist = float.MaxValue;

            if (_playerTransform != null)
            {
                for (int i = 0; i < _cachedNPCs.Count; i++)
                {
                    var npc = _cachedNPCs[i];
                    if (npc.root == null || npc.smr == null) continue;
                    float d = Vector3.Distance(_playerTransform.position, npc.root.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        closestSMR = npc.smr;
                    }
                }
            }

            Debug.Log($"[NPCJumpscareManager] Error Punishment Jumpscare triggered! Reason: {reason}");
            TriggerJumpscare(closestSMR);
        }
    }

    /// <summary>
    /// Immediately aborts any ongoing jumpscare and cleans up the 3D face rig and audio.
    /// Called when Game Over / Clearance Terminated triggers.
    /// </summary>
    public void AbortActiveJumpscare()
    {
        if (_activeScareCoroutine != null)
        {
            StopCoroutine(_activeScareCoroutine);
            _activeScareCoroutine = null;
        }

        if (_audioSource != null && _audioSource.isPlaying) _audioSource.Stop();
        if (_secondaryScreamSource != null && _secondaryScreamSource.isPlaying) _secondaryScreamSource.Stop();
        if (_heartbeatSource != null && _heartbeatSource.isPlaying) _heartbeatSource.Stop();

        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);
        if (_scareRig != null) _scareRig.SetActive(false);
        if (_activeBakedMesh != null)
        {
            Destroy(_activeBakedMesh);
            _activeBakedMesh = null;
        }

        if (_playerMovement != null)
        {
            _playerMovement.isJumpscareCameraOverride = false;
        }

        _isScaring = false;
    }

    /// <summary>
    /// Triggers the 3D in-your-face jumpscare using the specified NPC's SkinnedMeshRenderer
    /// (or picks a random student if null).
    /// </summary>
    public void TriggerJumpscare(SkinnedMeshRenderer targetSMR = null, bool isGuard = false)
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (DevDemoPanel.IsGodModeActive) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;
        if (GameOverUI.IsCutsceneActive) return;
        if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver) return;
        if (_isScaring) return;

        // Force-close any active puzzle, minigame, inspection modal, or dialogue immediately!
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        _activeScareCoroutine = StartCoroutine(JumpscareRoutine(targetSMR, isGuard));
    }

    private IEnumerator JumpscareRoutine(SkinnedMeshRenderer targetSMR, bool isGuardCatch = false)
    {
        _isScaring = true;
        _lastScareTime = Time.time;

        if (_playerCam == null) FindPlayerReferences();
        if (_scareRig == null) SetupScareRig();

        // Pick an NPC mesh if none was provided
        SkinnedMeshRenderer chosenSMR = targetSMR;
        if (chosenSMR == null)
        {
            if (_cachedNPCs.Count == 0) RefreshNPCCache();
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

        // 5. Increment Anxiety Meter! (Forgiving low impact: Guard catch +10, wandering NPC +2.5)
        float anxietyToAdd = isGuardCatch ? 4.0f : jumpscareAnxietyIncrease;
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(anxietyToAdd);
            Debug.Log($"[NPCJumpscareManager] Added +{anxietyToAdd} anxiety on NPC jumpscare! Current: {AnxietyManager.Instance.CurrentAnxiety:F1}");
        }

        // If this jumpscare pushed Anxiety to 100% (Game Over), immediately abort so the Game Over cutscene plays cleanly!
        if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
        {
            if (_scareRig != null) _scareRig.SetActive(false);
            if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);
            if (_activeBakedMesh != null) { Destroy(_activeBakedMesh); _activeBakedMesh = null; }
            _isScaring = false;
            _activeScareCoroutine = null;
            yield break;
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
                if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
                {
                    yield break;
                }

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
                if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
                {
                    yield break;
                }

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

            bool isGameOverNow = GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver);

            // Exact camera reset (only if Game Over cutscene hasn't taken over the camera)
            if (_playerCam != null && !isGameOverNow)
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

            // Restore player movement controls and camera look (only if not in Game Over)
            if (_playerMovement != null && !isGameOverNow)
            {
                _playerMovement.isJumpscareCameraOverride = false;
                _playerMovement.SetControlsEnabled(true);
            }

            _isScaring = false;
            _activeScareCoroutine = null;
        }
    }
}
