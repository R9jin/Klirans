using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls Main Menu interactions, navigation, audio, settings, and credits modals.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainButtonsPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Audio Settings Controls")]
    public Slider volumeSlider;
    public Text volumeValueText;
    public Slider fovSlider;
    public Text fovValueText;
    public Slider sensitivitySlider;
    public Text sensitivityValueText;

    [Header("Audio Clips")]
    public AudioClip menuBGM;
    public AudioClip clickClip;

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private void Awake()
    {
        // Setup BGM source
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume = 0.5f;

        // Setup SFX source
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 0.8f;
    }

    private void Start()
    {
        // Ensure cursor is visible and unlocked in the menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Initialize Master Volume
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        AudioListener.volume = savedVolume;
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        UpdateVolumeText(savedVolume);

        // Initialize FOV
        float savedFOV = PlayerPrefs.GetFloat("CameraFOV", 60f);
        if (fovSlider != null)
        {
            fovSlider.minValue = 50f;
            fovSlider.maxValue = 110f;
            fovSlider.value = savedFOV;
            fovSlider.onValueChanged.AddListener(OnFOVChanged);
        }
        UpdateFOVText(savedFOV);

        // Initialize Sensitivity
        float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.5f;
            sensitivitySlider.maxValue = 5.0f;
            sensitivitySlider.value = savedSens;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        UpdateSensitivityText(savedSens);

        // Initial panel state
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        // Play BGM
        if (menuBGM != null && _bgmSource != null)
        {
            _bgmSource.clip = menuBGM;
            _bgmSource.Play();
        }
    }

    private void Update()
    {
        // ESC to close modals
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (creditsPanel != null && creditsPanel.activeSelf)
            {
                CloseCredits();
            }
        }
    }

    public void PlayGame()
    {
        PlayClickSFX();
        Debug.Log("[MainMenuController] Starting game -> SampleScene");
        SceneManager.LoadScene("SampleScene");
    }

    public void OpenSettings()
    {
        PlayClickSFX();
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        PlayClickSFX();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    public void OpenCredits()
    {
        PlayClickSFX();
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void CloseCredits()
    {
        PlayClickSFX();
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    public void QuitGame()
    {
        PlayClickSFX();
        Debug.Log("[MainMenuController] Quit Game Requested");
        Application.Quit();
    }

    public void OnVolumeChanged(float val)
    {
        AudioListener.volume = val;
        PlayerPrefs.SetFloat("MasterVolume", val);
        PlayerPrefs.Save();
        UpdateVolumeText(val);
    }

    public void OnFOVChanged(float val)
    {
        PlayerPrefs.SetFloat("CameraFOV", val);
        PlayerPrefs.Save();
        UpdateFOVText(val);
    }

    public void OnSensitivityChanged(float val)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", val);
        PlayerPrefs.Save();
        UpdateSensitivityText(val);
    }

    private void UpdateVolumeText(float val)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(val * 100f) + "%";
    }

    private void UpdateFOVText(float val)
    {
        if (fovValueText != null)
            fovValueText.text = Mathf.RoundToInt(val) + "°";
    }

    private void UpdateSensitivityText(float val)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = val.ToString("F1");
    }

    private void PlayClickSFX()
    {
        if (_sfxSource != null && clickClip != null)
        {
            _sfxSource.PlayOneShot(clickClip, 0.85f);
        }
    }
}
