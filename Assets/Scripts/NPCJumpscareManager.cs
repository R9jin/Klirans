using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Triggers atmospheric, random NPC face jumpscares while exploring the campus:
/// 1. Clones the face of an NPC (Proctor, Drei, Librarian, Cashier, etc.) right up to the player's face (0.65m away).
/// 2. Plays a sudden horror screech/sting, camera shake, and flashing chromatic lighting.
/// 3. The apparition violently lunges and flickers before vanishing into thin air.
/// 4. Drains a small chunk of stamina and plays a gasping breath audio.
/// </summary>
public class NPCJumpscareManager : MonoBehaviour
{
    public static NPCJumpscareManager Instance { get; private set; }

    [Header("Timing Settings")]
    [Tooltip("Minimum seconds between random jumpscares.")]
    public float minInterval = 65f;

    [Tooltip("Maximum seconds between random jumpscares.")]
    public float maxInterval = 140f;

    [Header("Jumpscare Tuning")]
    [Tooltip("Distance from camera to face in meters.")]
    public float faceDistance = 0.60f;

    [Tooltip("Duration of the jumpscare in seconds.")]
    public float scareDuration = 0.75f;

    [Tooltip("Stamina penalty when jumpscared.")]
    public float staminaDrain = 25f;

    [Header("Audio Clips")]
    public AudioClip scareStingClip;
    public AudioClip staticHissClip;
    public AudioClip gaspBreathClip;

    // Internal References
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private StaminaSystem _staminaSystem;
    private AudioSource _audioSource;

    // Jumpscare 3D Rig
    private GameObject _scareRig;
    private MeshFilter _scareMeshFilter;
    private MeshRenderer _scareMeshRenderer;
    private Light _scareLight;
    private Image _flashOverlay;

    private float _nextScareTime = 0f;
    private bool _isScaring = false;

    // Available NPC mesh and material templates
    private struct NPCTemplate
    {
        public Mesh mesh;
        public Material material;
    }
    private List<NPCTemplate> _templates = new List<NPCTemplate>();

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

        // Auto-load horror audio if unassigned
        LoadDefaultAudio();

        // Collect NPC templates from scene
        CollectNPCTemplates();

        // Build Jumpscare 3D Rig parented to camera
        BuildScareRig();

        // Schedule first scare
        ScheduleNextScare();
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (scareStingClip == null)
            scareStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
        if (scareStingClip == null)
            scareStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");

        if (staticHissClip == null)
            staticHissClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");

        if (gaspBreathClip == null)
            gaspBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");
#endif
    }

    private void CollectNPCTemplates()
    {
        _templates.Clear();
        var smrs = FindObjectsOfType<SkinnedMeshRenderer>();
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            Material mat = smr.sharedMaterial;
            if (mat == null && smr.sharedMaterials.Length > 0) mat = smr.sharedMaterials[0];
            if (mat != null)
            {
                _templates.Add(new NPCTemplate { mesh = smr.sharedMesh, material = mat });
            }
        }
    }

    private void BuildScareRig()
    {
        if (_playerCam == null) return;

        _scareRig = new GameObject("NPC_JumpscareRig");
        _scareRig.transform.SetParent(_playerCam.transform, false);
        _scareRig.transform.localPosition = new Vector3(0f, -0.45f, faceDistance);
        _scareRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Facing directly into camera
        _scareRig.transform.localScale = Vector3.one * 1.35f;

        _scareMeshFilter = _scareRig.AddComponent<MeshFilter>();
        _scareMeshRenderer = _scareRig.AddComponent<MeshRenderer>();
        _scareMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _scareMeshRenderer.receiveShadows = false;

        // Eerie under-lighting attached to the face
        var lightGO = new GameObject("ScareLight");
        lightGO.transform.SetParent(_scareRig.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, -0.3f, 0.25f);
        _scareLight = lightGO.AddComponent<Light>();
        _scareLight.type = LightType.Point;
        _scareLight.color = new Color(0.95f, 0.25f, 0.20f, 1f); // bloody under-glow
        _scareLight.intensity = 5.0f;
        _scareLight.range = 3.0f;
        _scareLight.shadows = LightShadows.None;

        // Flash overlay on HudCanvas
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
            _flashOverlay.color = new Color(0.85f, 0.05f, 0.05f, 0f);
            _flashOverlay.raycastTarget = false;
            flashGO.SetActive(false);
        }

        _scareRig.SetActive(false);
    }

    private void Update()
    {
        if (_isScaring) return;

        // Check if conditions permit a scare
        if (Time.time >= _nextScareTime)
        {
            if (CanTriggerScare())
            {
                TriggerJumpscare();
            }
            else
            {
                // Delay slightly and check again
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

        return true;
    }

    public void TriggerJumpscare()
    {
        if (_isScaring || _scareRig == null || _templates.Count == 0) return;
        StartCoroutine(JumpscareSequence());
    }

    private IEnumerator JumpscareSequence()
    {
        _isScaring = true;

        // Select a random NPC face
        int idx = Random.Range(0, _templates.Count);
        NPCTemplate template = _templates[idx];

        if (_scareMeshFilter != null) _scareMeshFilter.sharedMesh = template.mesh;
        if (_scareMeshRenderer != null) _scareMeshRenderer.sharedMaterial = template.material;

        // Play loud horror sting audio
        if (_audioSource != null && scareStingClip != null)
        {
            _audioSource.PlayOneShot(scareStingClip, 1.0f);
        }

        // Show rig and flash
        _scareRig.SetActive(true);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(true);

        // Deduct stamina
        if (_staminaSystem != null)
        {
            _staminaSystem.currentStamina = Mathf.Max(5f, _staminaSystem.currentStamina - staminaDrain);
        }

        Vector3 originalLocalPos = new Vector3(0f, -0.45f, faceDistance);
        float elapsed = 0f;

        // Violent screen shake and jitter
        while (elapsed < scareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scareDuration;

            // Camera shake jitter
            float shakeX = Random.Range(-0.06f, 0.06f) * (1f - t);
            float shakeY = Random.Range(-0.06f, 0.06f) * (1f - t);
            float lungeZ = Mathf.Lerp(faceDistance, faceDistance - 0.12f, Mathf.Sin(t * Mathf.PI));

            _scareRig.transform.localPosition = new Vector3(shakeX, originalLocalPos.y + shakeY, lungeZ);

            // Light flicker
            if (_scareLight != null)
            {
                _scareLight.intensity = Random.Range(3.5f, 7.0f);
            }

            // Red flash fade
            if (_flashOverlay != null)
            {
                float flashAlpha = Mathf.Lerp(0.55f, 0f, t);
                _flashOverlay.color = new Color(0.85f, 0.05f, 0.05f, flashAlpha);
            }

            yield return null;
        }

        // Vanish
        _scareRig.SetActive(false);
        if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false);

        // Play static hiss and gasp for air
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
