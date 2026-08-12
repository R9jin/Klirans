using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Panels")]
    public GameObject pauseMenuUI;

    [Header("Settings Controls")]
    public Slider volumeSlider;
    public Text volumeValueText;
    public Slider fovSlider;
    public Text fovValueText;

    [Header("References")]
    public PlayerMovement playerMovement;

    void Start()
    {
        GameIsPaused = false;
        Time.timeScale = 1f;

        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<PlayerMovement>();
        }

        // Initialize Master Volume from PlayerPrefs (default 1.0)
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

        // Initialize FOV from PlayerPrefs or PlayerMovement (default 60)
        float defaultFOV = (playerMovement != null) ? playerMovement.normalFOV : 60f;
        float savedFOV = PlayerPrefs.GetFloat("CameraFOV", defaultFOV);
        ApplyFOV(savedFOV);

        if (fovSlider != null)
        {
            fovSlider.minValue = 50f;
            fovSlider.maxValue = 110f;
            fovSlider.value = savedFOV;
            fovSlider.onValueChanged.AddListener(OnFOVChanged);
        }
        UpdateFOVText(savedFOV);

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        GameIsPaused = false;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Pause()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        GameIsPaused = true;
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
        UpdateVolumeText(value);
    }

    public void OnFOVChanged(float value)
    {
        ApplyFOV(value);
        PlayerPrefs.SetFloat("CameraFOV", value);
        PlayerPrefs.Save();
        UpdateFOVText(value);
    }

    private void ApplyFOV(float fov)
    {
        if (playerMovement != null)
        {
            playerMovement.normalFOV = fov;
            if (playerMovement.playerCamera != null)
            {
                playerMovement.playerCamera.fieldOfView = fov;
            }
        }
    }

    private void UpdateVolumeText(float val)
    {
        if (volumeValueText != null)
        {
            volumeValueText.text = Mathf.RoundToInt(val * 100f) + "%";
        }
    }

    private void UpdateFOVText(float val)
    {
        if (fovValueText != null)
        {
            fovValueText.text = Mathf.RoundToInt(val) + "°";
        }
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}

