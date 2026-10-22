using UnityEngine;

/// <summary>
/// WorldNoteInteract — Interactive environmental storytelling note / document in the 3D world.
/// Allows the player to approach memos, corkboard notices, diary pages, chalkboard scrawls, and exam slips,
/// pressing [E] to read them in the survival horror ItemInspectionModal.
/// </summary>
[DisallowMultipleComponent]
public class WorldNoteInteract : MonoBehaviour, IInteractable
{
    [Header("Note Header & Identity")]
    [Tooltip("The title displayed at the top of the parchment inspection viewer.")]
    public string noteTitle = "Campus Notice";

    [Tooltip("Verb shown in the interaction prompt (e.g. 'Read Note', 'Read Memo', 'Inspect Scrawl').")]
    public string promptVerb = "Read Note";

    [Tooltip("Optional icon sprite for the document header.")]
    public Sprite noteIcon;

    [Header("Lore Content")]
    [TextArea(8, 26)]
    [Tooltip("Full body text and lore content displayed inside the note modal.")]
    public string noteContent = "";

    [Header("Audio & Audio Feedback")]
    [Tooltip("Optional custom audio cue played when opening this specific note (e.g. ambient whisper, creepy chime).")]
    public AudioClip customAudio;

    [Header("Highlight & Visuals")]
    [Tooltip("Automatically attaches ItemVisualHighlight with document styling if not already present.")]
    public bool autoAddHighlight = true;

    [Tooltip("Glow color for the subtle point light halo.")]
    public Color glowColor = new Color(0.90f, 0.95f, 1.0f, 1.0f);

    [Header("State")]
    [Tooltip("Whether this note has been inspected at least once by the player.")]
    public bool isRead = false;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (customAudio != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake = false;
        }

        if (autoAddHighlight && GetComponent<ItemVisualHighlight>() == null)
        {
            var hl = gameObject.AddComponent<ItemVisualHighlight>();
            hl.category = ItemVisualHighlight.ItemCategory.DocumentOrPaper;
            hl.glowColor = glowColor;
        }
    }

    public string GetPrompt()
    {
        if (ItemInspectionModal.IsOpen) return "";

        if (string.IsNullOrEmpty(noteTitle))
        {
            return $"Press [E] to {promptVerb}";
        }

        return $"Press [E] to {promptVerb}: \"{noteTitle}\"";
    }

    public void Interact()
    {
        isRead = true;

        if (customAudio != null)
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
                _audioSource.playOnAwake = false;
            }
            _audioSource.PlayOneShot(customAudio, 0.75f);
        }

        ItemInspectionModal.Instance.OpenModal(noteTitle, noteContent, noteIcon);
    }
}
