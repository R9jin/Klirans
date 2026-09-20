using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Triggers authentic FNAF-style NPC face jumpscares while exploring campus:
/// 1. Bakes the animated face of a campus NPC (Proctor, Drei, Cashier, Guard, etc.).
/// 2. Centers the terrifying, screaming face right in the camera viewport with FNAF-scale closeup.
/// 3. Direct horror lighting ensures facial features and expressions are clearly visible (no black silhouettes or solid red flashes).
/// 4. Violent high-frequency head twitching and lunging towards the camera glass.
/// 5. Plays loud "you died" / proctor horror sting audio, drains stamina, and plays gasping breaths.
/// 6. Safely blocked while player is interacting, solving puzzles, or climbing stairs.
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
    public float startDistance = 0.62f;

    [Tooltip("End distance of the lunging face at closest point.")]
    public float closestDistance = 0.38f;

    [Tooltip("FNAF face scale multiplier to fill the screen.")]
    public float faceScale = 1.95f;

    [Tooltip("Duration of the jumpscare in seconds.")]
    public float scareDuration = 0.85f;

    [Tooltip("Stamina penalty when jumpscared.")]
    public float staminaDrain = 25f;

    [Header("Audio Clips")]
    public AudioClip scareStingClip;
    public AudioClip proctorStingClip;
    public AudioClip staticHissClip;
    public AudioClip gaspBreathClip;

    // Internal References
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private StaminaSystem _staminaSystem;
    private AudioSource _audioSource;

    // Jumpscare 3D Rig parented to Camera
    private GameObject _scareRig;
    private MeshFilter _scareMeshFilter;
    private MeshRenderer _scareMeshRenderer;
    private Light _faceLight;
    private Image _flashOverlay;

    private float _nextScareTime = 0f;
    private bool _isScaring = false;

    // Cached NPC sources
    private List<SkinnedMeshRenderer> _cachedSMRs = new List<SkinnedMeshRenderer>();
    private Mesh _activeBakedMesh;

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
        _playerMovement = GetComponent<PlayerMovement>() ?? FindObjectOfType<PlayerMovement>();
        if (_playerMovement != null) _playerCam = _playerMovement.playerCamera;
        if (_playerCam == null) _playerCam = Camera.main;

        _staminaSystem = GetComponent<StaminaSystem>() ?? FindObjectOfType<StaminaSystem>();

        var audioGO = new GameObject("JumpscareAudioSource");
        audioGO.transform.SetParent(transform, false);
        _audioSource = audioGO.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1.0f;

        LoadDefaultAudio();
        CacheNPCSkinRenderers();
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

    private void CacheNPCSkinRenderers()
    {
        _cachedSMRs.Clear();
        var allSMRs = FindObjectsOfType<SkinnedMeshRenderer>();
        foreach (var smr in allSMRs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            // Ignore player's own mesh if any
            if (smr.transform.root.CompareTag("Player")) continue;
            _cachedSMRs.Add(smr);
        }
    }

    private void BuildScareRig()
    {
        if (_playerCam == null) return;

        _scareRig = new GameObject("NPC_FNAF_ScareRig");
        _scareRig.transform.SetParent(_playerCam.transform, false);
        _scareRig.transform.localPosition = new Vector3(0f, 0f, startDistance);
        _scareRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        _scareRig.transform.localScale = Vector3.one * faceScale;

        _scareMeshFilter = _scareRig.AddComponent<MeshFilter>();
        _scareMeshRenderer = _scareRig.AddComponent<MeshRenderer>();
        _scareMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _scareMeshRenderer.receiveShadows = false;

        // Bright horror front light aimed directly onto the face so features are crystal clear
        var lightGO = new GameObject("FaceHorrorLight");
        lightGO.transform.SetParent(_playerCam.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        _faceLight = lightGO.AddComponent<Light>();
        _faceLight.type = LightType.Point;
        _faceLight.color = new Color(1.0f, 0.95f, 0.90f, 1f); // Horror exposure
        _faceLight.intensity = 2.8f;
        _faceLight.range = 2.5f;
        _faceLight.shadows = LightShadows.None;
        _faceLight.enabled = false;

        // Subtle vignette / horror flash overlay (never fully opaque so face remains visible)
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
            _flashOverlay.color = new Color(0.70f, 0.05f, 0.05f, 0f);
            _flashOverlay.raycastTarget = false;
            flashGO.SetActive(false);
        }

        _scareRig.SetActive(false);
    }

    private void Update()
    {
        if (_isScaring) return;

        if (Time.time >= _nextScareTime)
        {
            if (CanTriggerScare())
            {
                TriggerJumpscare();
            }
            else
            {
                _nextScareTime = Time.time + 10f;
            }
        }
    }

    private bool CanTriggerScare()
    {
        if (PauseMenu.GameIsPaused) return false;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return false;

        var guidancePuzzle = GuidanceWordPuzzle.Instance;
        if (guidancePuzzle != null && guidancePuzzle.IsOpen) return false;

        var cashierPuzzle = CashierBalancePuzzle.Instance;
        if (cashierPuzzle != null && cashierPuzzle.IsOpen) return false;

        var registrarPuzzle = RegistrarDocumentSortPuzzle.Instance;
        if (registrarPuzzle != null && registrarPuzzle.IsOpen) return false;

        // Never scare while player is climbing or transitioning stairs
        if (IsPlayerOnStairs()) return false;

        return true;
    }

    private bool IsPlayerOnStairs()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return false;

        // Check if inside any StairTriggerZone
        var stairZones = FindObjectsOfType<StairTriggerZone>();
        Vector3 playerPos = playerGO.transform.position;
        foreach (var zone in stairZones)
        {
            var col = zone.GetComponent<Collider>();
            if (col != null && col.bounds.Contains(playerPos)) return true;
        }

        return false;
    }

    public void TriggerJumpscare()
    {
        if (_isScaring || _scareRig == null) return;
        StartCoroutine(FNAFJumpscareSequence());
    }

    private IEnumerator FNAFJumpscareSequence()
    {
        _isScaring = true;

        if (_cachedSMRs.Count == 0) CacheNPCSkinRenderers();
        if (_cachedSMRs.Count == 0)
        {
            _isScaring = false;
            yield break;
        }

        // Pick a random NPC to jumpscare
        int idx = Random.Range(0, _cachedSMRs.Count);
        SkinnedMeshRenderer chosenSMR = _cachedSMRs[idx];
        if (chosenSMR == null)
        {
            CacheNPCSkinRenderers();
            chosenSMR = _cachedSMRs.Count > 0 ? _cachedSMRs[0] : null;
        }
        if (chosenSMR == null)
        {
            _isScaring = false;
            yield break;
        }

        // Bake the current skinned mesh into a frozen snapshot
        if (_activeBakedMesh != null) Destroy(_activeBakedMesh);
        _activeBakedMesh = new Mesh();
        chosenSMR.BakeMesh(_activeBakedMesh);

        // Compute head center from the top 20% of vertices
        Vector3[] verts = _activeBakedMesh.vertices;
        float maxY = float.MinValue;
        float minY = float.MaxValue;
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y > maxY) maxY = verts[i].y;
            if (verts[i].y < minY) minY = verts[i].y;
        }

        float headCutoff = maxY - (maxY - minY) * 0.20f;
        Vector3 headCenter = Vector3.zero;
        int headCount = 0;
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y >= headCutoff)
            {
                headCenter += verts[i];
                headCount++;
            }
        }
        if (headCount > 0) headCenter /= headCount;

        // Apply mesh and material to rig
        _scareMeshFilter.sharedMesh = _activeBakedMesh;
        _scareMeshRenderer.sharedMaterial = chosenSMR.sharedMaterial;

        // Play horror sting audio: "you died" or "encountering a proctor"
        if (_audioSource != null)
        {
            AudioClip clipToPlay = (Random.value < 0.65f && scareStingClip != null) ? scareStingClip : proctorStingClip;
            if (clipToPlay == null) clipToPlay = scareStingClip;
            if (clipToPlay != null) _audioSource.PlayOneShot(clipToPlay, 1.0f);
        }

        // Enable rig, light, and overlay
        _scareRig.SetActive(true);
        if (_faceLight != null) _faceLight.enabled = true;
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(true);

        // Deduct stamina
        if (_staminaSystem != null)
        {
            _staminaSystem.currentStamina = Mathf.Max(5f, _staminaSystem.currentStamina - staminaDrain);
        }

        float elapsed = 0f;
        float s = faceScale;

        // FNAF violent lunging & screaming head shake loop
        while (elapsed < scareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scareDuration;

            // Fast forward lunge from startDistance to closestDistance (right against camera glass)
            float lungeZ = Mathf.Lerp(startDistance, closestDistance, Mathf.Sin(t * Mathf.PI * 0.5f));

            // Violent FNAF high-frequency jitter (positional twitch and angular shaking)
            float jitterStrength = 1.0f - (t * 0.35f);
            float jitterX = Random.Range(-0.035f, 0.035f) * jitterStrength;
            float jitterY = Random.Range(-0.035f, 0.035f) * jitterStrength;
            float jitterZ = Random.Range(-0.020f, 0.020f) * jitterStrength;

            float pitchJitter = Random.Range(-9f, 9f) * jitterStrength;
            float yawJitter = 180f + Random.Range(-12f, 12f) * jitterStrength;
            float rollJitter = Random.Range(-10f, 10f) * jitterStrength;

            // Align head center at (0, 0, lungeZ) in camera coordinates
            _scareRig.transform.localScale = Vector3.one * s;
            _scareRig.transform.localPosition = new Vector3(headCenter.x * s + jitterX, -headCenter.y * s + jitterY, lungeZ + headCenter.z * s + jitterZ);
            _scareRig.transform.localRotation = Quaternion.Euler(pitchJitter, yawJitter, rollJitter);

            // Light flicker
            if (_faceLight != null)
            {
                _faceLight.intensity = Random.Range(2.4f, 3.2f);
            }

            // Subtle red horror flash that does NOT mask the face (max alpha ~0.18)
            if (_flashOverlay != null)
            {
                float flashAlpha = Mathf.Lerp(0.18f, 0f, t);
                _flashOverlay.color = new Color(0.70f, 0.05f, 0.05f, flashAlpha);
            }

            yield return null;
        }

        // Clean up rig
        _scareRig.SetActive(false);
        if (_faceLight != null) _faceLight.enabled = false;
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);

        if (_activeBakedMesh != null)
        {
            Destroy(_activeBakedMesh);
            _activeBakedMesh = null;
        }

        // Post-scare audio: static hiss and gasping for air
        if (_audioSource != null)
        {
            if (staticHissClip != null) _audioSource.PlayOneShot(staticHissClip, 0.6f);
            if (gaspBreathClip != null) _audioSource.PlayOneShot(gaspBreathClip, 0.9f);
        }

        _isScaring = false;
        ScheduleNextScare();
    }

    private void ScheduleNextScare()
    {
        _nextScareTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
