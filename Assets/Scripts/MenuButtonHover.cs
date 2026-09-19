using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Provides retro PSX-style hover highlights, prefix arrows, and sound feedback
/// for buttons in the Main Menu.
/// </summary>
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Text & Styling")]
    public Text targetText;
    public Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color hoverColor = new Color(0.95f, 0.15f, 0.15f, 1f); // Vibrant horror crimson
    public string prefixOnHover = "> ";
    public bool boldOnHover = true;

    [Header("Audio")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    private string _originalText = string.Empty;
    private AudioSource _audioSource;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponentInChildren<Text>();

        if (targetText != null)
        {
            _originalText = targetText.text;
            targetText.color = normalColor;
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }

    private void OnEnable()
    {
        ResetToNormal();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetText != null)
        {
            targetText.color = hoverColor;
            targetText.text = $"{prefixOnHover}{_originalText}";
        }

        if (_audioSource != null && hoverSound != null)
        {
            _audioSource.PlayOneShot(hoverSound, 0.6f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetToNormal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_audioSource != null && clickSound != null)
        {
            _audioSource.PlayOneShot(clickSound, 0.85f);
        }
    }

    public void ResetToNormal()
    {
        if (targetText != null)
        {
            targetText.color = normalColor;
            if (!string.IsNullOrEmpty(_originalText))
                targetText.text = _originalText;
        }
    }
}
