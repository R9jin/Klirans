using UnityEngine;
using UnityEngine.SceneManagement;

public class WinSceneController : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip winBGM;
    public AudioClip clickClip;

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private void Awake()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume = 0.4f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 0.85f;
    }

    private void Start()
    {
        // Ensure cursor is visible and free to click UI buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1.0f;

        if (winBGM != null && _bgmSource != null)
        {
            _bgmSource.clip = winBGM;
            _bgmSource.Play();
        }
    }

    public void PlayAgain()
    {
        PlayClickSFX();
        Debug.Log("[WinScene] Starting new game -> SampleScene");
        SceneManager.LoadScene("SampleScene");
    }

    public void MainMenu()
    {
        PlayClickSFX();
        Debug.Log("[WinScene] Returning to MainMenu");
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        PlayClickSFX();
        Debug.Log("[WinScene] Quitting game...");
        Application.Quit();
    }

    private void PlayClickSFX()
    {
        if (_sfxSource != null && clickClip != null)
        {
            _sfxSource.PlayOneShot(clickClip, 0.85f);
        }
    }
}
