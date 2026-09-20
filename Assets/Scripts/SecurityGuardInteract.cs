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
    public float interactionRange = 3.8f;

    [Header("Dialogue Content")]
    [TextArea(2, 4)]
    public string lockdownDialogue = "HALT! The entire campus is under strict lockdown by executive order. The front gates are secured with high-grade magnetic seals. You cannot leave without an officially approved and stamped clearance slip signed by all six department heads. Do not attempt to force the doors.";

    [TextArea(2, 4)]
    public string fullyClearDialogue = "Hold on... All six department signatures are stamped on your slip, but the University Registrar in Room 104 must officially log and submit your clearance record before the gate lockdown override can be authorized.";

    [TextArea(2, 4)]
    public string escapedPermittedDialogue = "Official clearance verified and recorded on the terminal. The main campus gate is unlocked. Move quickly and stay safe out there.";

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
        _sitController = GetComponent<NPCSitController>();
        _animator = GetComponentInChildren<Animator>();
        _talkingHash = Animator.StringToHash("Talking");

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

        return $"Press E to talk to {guardName}";
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

        var diag = NPCDialogueSystem.Instance ?? FindObjectOfType<NPCDialogueSystem>();
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
