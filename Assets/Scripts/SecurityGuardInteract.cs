using UnityEngine;

/// <summary>
/// Interaction controller for Security_Guard_NPC in the main lobby.
/// Implements IInteractable:
/// - Sits in the lobby security station chair by default.
/// - Prompts player with "Press E to talk to Security Guard".
/// - When interacted with, stands up from the chair and faces the player.
/// - Displays survival horror dialogue advising the student about the building lockdown.
/// - Once dialogue is closed, sits back down in the security chair.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SecurityGuardInteract : MonoBehaviour, IInteractable
{
    [Header("Identity & Prompts")]
    public string guardName = "Campus Security Guard";
    public float interactionRange = 4.25f;

    [Header("Dialogue Content")]
    [TextArea(2, 4)]
    public string lockdownDialogue = "Huy, teka lang! Bawal lumabas, naka-lockdown ang buong building by executive order! Naka-lock ang main gate ng magnetic seal, hindi bubukas 'yan hangga't walang kompletong pirma 'yang clearance mo sa anim na department heads. 'Wag mo nang piliting buksan 'yung pinto, baka mapahamak ka lang diyan.";

    [TextArea(2, 4)]
    public string fullyClearDialogue = "Teka... kompleto na nga ang anim na pirma sa slip mo ah! Pero kailangan mo munang ipasa 'yan kay Registrar doon sa Room 104. Sila lang ang may authority mag-log niyan sa system bago ko ma-override 'tong gate para mabuksan.";

    [TextArea(2, 4)]
    public string escapedPermittedDialogue = "Ayan, na-verify at na-record na sa terminal 'yung clearance mo. Bukas na 'yung main campus gate sa lobby. Bilisan mo nang lumabas at mag-ingat ka diyan!";

    [Header("Audio")]
    public AudioClip guardVoiceClip;

    private Transform _playerTransform;
    private NPCSitController _sitController;
    private Animator _animator;
    private AudioSource _audioSource;
    private bool _isInteracting = false;
    private int _talkingHash;

    private void Awake()
    {
        if (interactionRange < 4.25f)
        {
            interactionRange = 4.25f;
        }

        _sitController = GetComponent<NPCSitController>();
        _animator = GetComponentInChildren<Animator>();
        _talkingHash = Animator.StringToHash("Talking");

        EnsureStaffInteractionZone();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.5f;
            _audioSource.playOnAwake = false;
        }

        if (guardVoiceClip == null)
        {
            guardVoiceClip = Resources.Load<AudioClip>("freesound_community-human_male_crazy-mumbles_1-30950");
#if UNITY_EDITOR
            if (guardVoiceClip == null)
            {
                guardVoiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-human_male_crazy-mumbles_1-30950.mp3");
            }
#endif
        }
    }

    /// <summary>
    /// Creates an upper-body interaction trigger box extending across the security booth desk
    /// so the player's crosshair raycast reliably hits the guard even when seated behind the counter.
    /// </summary>
    private void EnsureStaffInteractionZone()
    {
        Transform existing = transform.Find("StaffInteractionZone");
        GameObject zoneObj;
        if (existing != null)
        {
            zoneObj = existing.gameObject;
        }
        else
        {
            zoneObj = new GameObject("StaffInteractionZone");
            zoneObj.transform.SetParent(transform, false);
        }

        zoneObj.layer = gameObject.layer;
        zoneObj.transform.localPosition = Vector3.zero;
        zoneObj.transform.localRotation = Quaternion.identity;
        zoneObj.transform.localScale = Vector3.one;

        var box = zoneObj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = zoneObj.AddComponent<BoxCollider>();
        }
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.15f, 0.45f);
        box.size = new Vector3(1.50f, 1.90f, 1.75f);
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;
    }

    private void Update()
    {
        if (_isInteracting && _playerTransform != null)
        {
            Vector3 targetDir = _playerTransform.position - transform.position;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }
    }

    public string GetPrompt()
    {
        if (_playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > interactionRange) return string.Empty;
        }

        return $"Pindutin ang E para kausapin si {guardName}";
    }

    public void Interact()
    {
        if (_playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist > interactionRange) return;
        }

        if (_isInteracting) return;
        _isInteracting = true;

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null)
        {
            _sitController.StandUp();
        }

        if (_animator != null)
        {
            _animator.SetBool(_talkingHash, true);
        }

        // Determine dialogue
        string message = lockdownDialogue;
        if (ClearanceManager.Instance != null)
        {
            if (ClearanceManager.Instance.IsSlipSubmitted)
            {
                message = escapedPermittedDialogue;
            }
            else if (ClearanceManager.Instance.IsFullyClear())
            {
                message = fullyClearDialogue;
            }
        }

        // Play mumble audio
        if (_audioSource != null && guardVoiceClip != null)
        {
            _audioSource.clip = guardVoiceClip;
            _audioSource.pitch = 0.88f;
            _audioSource.volume = 0.85f;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        var diag = NPCDialogueSystem.Instance ?? FindAnyObjectByType<NPCDialogueSystem>();
        if (diag != null)
        {
            diag.StartDialogue(transform, guardName, message, "CAMPUS SECURITY CHECKPOINT — MAIN LOBBY", OnDialogueEnded);
        }
    }

    private void OnDialogueEnded()
    {
        _isInteracting = false;

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_animator != null)
        {
            _animator.SetBool(_talkingHash, false);
        }

        if (_sitController == null) _sitController = GetComponent<NPCSitController>();
        if (_sitController != null)
        {
            _sitController.SitDown();
        }
    }
}
