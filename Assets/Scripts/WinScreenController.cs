using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Controls the Win / Clearance Granted screen.
/// Matches the MainMenu horror theme: 3D lobby-style camera, KLIRANS font,
/// dark vignette overlay, animated title reveal, and a Return to Main Menu button.
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup rootCanvasGroup;
    public Text        titleText;        // "CLEARANCE GRANTED"
    public Text        subtitleText;     // flavor text
    public Text        statsText;        // optional – e.g. playtime
    public Button      mainMenuButton;
    public Text        mainMenuBtnText;

    [Header("Audio")]
    public AudioClip winMusic;
    public AudioClip buttonClickClip;

    [Header("Timing")]
    public float fadeInDuration   = 1.8f;
    public float titleDelaySeconds = 0.6f;

    private AudioSource _bgm;
    private AudioSource _sfx;

    private void Awake()
    {
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.playOnAwake = false;
        _bgm.loop        = true;
        _bgm.spatialBlend = 0f;
        _bgm.volume      = 0f;

        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f;
        _sfx.volume      = 0.85f;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnReturnToMainMenu);

        if (winMusic != null)
        {
            _bgm.clip = winMusic;
            _bgm.Play();
        }

        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        // Fade the whole canvas in
        if (rootCanvasGroup != null)
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                rootCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInDuration);

                // Fade BGM in with canvas
                if (_bgm != null) _bgm.volume = Mathf.Lerp(0f, 0.45f, t / fadeInDuration);

                yield return null;
            }
            rootCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(titleDelaySeconds);

        // Typewriter effect on subtitle if present
        if (subtitleText != null)
        {
            string full = subtitleText.text;
            subtitleText.text = string.Empty;
            foreach (char c in full)
            {
                subtitleText.text += c;
                yield return new WaitForSeconds(0.03f);
            }
        }
    }

    public void OnReturnToMainMenu()
    {
        if (_sfx != null && buttonClickClip != null)
            _sfx.PlayOneShot(buttonClickClip, 0.85f);

        StartCoroutine(FadeOutAndLoad("MainMenu"));
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        if (rootCanvasGroup != null)
        {
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                rootCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.6f);
                yield return null;
            }
        }
        SceneManager.LoadScene(sceneName);
    }
}
