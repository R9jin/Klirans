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

    [Header("Headphone Notice")]
    public GameObject headphoneNoticePanel;
    public CanvasGroup headphoneCanvasGroup;
    public Text headphonePromptText;
    public float noticeAutoAdvanceTime = 5.0f;
    public float noticeFadeDuration = 0.7f;
    public static bool HasShownHeadphoneNotice = false;

    [Header("Audio Settings Controls")]
    public Slider volumeSlider;
    public Text volumeValueText;
    public Slider fovSlider;
    public Text fovValueText;
    public Slider sensitivitySlider;
    public Text sensitivityValueText;

    [Header("Audio Balancing")]
    [Tooltip("Volume level for the Main Menu background music.")]
    [Range(0f, 1f)]
    public float bgmVolume = 0.65f;

    [Tooltip("Volume level for the subtle hallway ambient bed.")]
    [Range(0f, 1f)]
    public float ambientVolume = 0.20f;

    [Tooltip("Volume level for UI clicks and interactions.")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.85f;

    [Header("Audio Clips")]
    public AudioClip menuBGM;
    public AudioClip clickClip;
    public AudioClip hallwayAmbientClip;

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;
    private AudioSource _ambientSource;
    private CanvasGroup _menuUIGroup;

    private void Awake()
    {
        // Enforce smooth 60 FPS pacing and prevent unbounded GPU thermal throttling in standalone builds
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;

        // Ensure an AudioListener exists so music and sound can be heard
        if (FindAnyObjectByType<AudioListener>() == null)
        {
            Camera cam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            else gameObject.AddComponent<AudioListener>();
        }

        // Auto-load main-menu-music.mp3 if menuBGM is not assigned
        if (menuBGM == null)
        {
#if UNITY_EDITOR
            menuBGM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/main-menu-music.mp3");
#endif
            if (menuBGM == null) menuBGM = Resources.Load<AudioClip>("main-menu-music");
        }

        // Setup BGM source (2D stereo background music)
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume = bgmVolume;

        // Setup SFX source
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = sfxVolume;

        // Setup hallway ambient source
        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.playOnAwake = false;
        _ambientSource.loop = true;
        _ambientSource.spatialBlend = 0f;
        _ambientSource.volume = ambientVolume;

        // Immediately hide notice in Awake if already shown this session to avoid any frame flicker
        if (HasShownHeadphoneNotice && headphoneNoticePanel != null)
        {
            headphoneNoticePanel.SetActive(false);
        }
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
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        // Locate CanvasGroup on MainMenu_UI
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            var menuUITrans = canvas.transform.Find("MainMenu_UI");
            if (menuUITrans != null)
            {
                _menuUIGroup = menuUITrans.GetComponent<CanvasGroup>();
                if (_menuUIGroup == null) _menuUIGroup = menuUITrans.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Headphone recommendation check (only on first load of the game session)
        if (!HasShownHeadphoneNotice && headphoneNoticePanel != null)
        {
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
            if (_menuUIGroup != null) _menuUIGroup.alpha = 0f;
            StartCoroutine(ShowHeadphoneNoticeRoutine());
        }
        else
        {
            // Already shown this session, or panel not assigned: immediately show Main Menu
            if (headphoneNoticePanel != null) headphoneNoticePanel.SetActive(false);
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
            if (_menuUIGroup != null) _menuUIGroup.alpha = 1f;

            if (menuBGM != null && _bgmSource != null)
            {
                _bgmSource.clip = menuBGM;
                _bgmSource.volume = bgmVolume;
                _bgmSource.Play();
            }

            if (hallwayAmbientClip != null && _ambientSource != null)
            {
                _ambientSource.clip = hallwayAmbientClip;
                _ambientSource.volume = ambientVolume;
                _ambientSource.Play();
            }
        }
    }

    private System.Collections.IEnumerator ShowHeadphoneNoticeRoutine()
    {
        headphoneNoticePanel.SetActive(true);
        if (headphoneCanvasGroup != null)
        {
            headphoneCanvasGroup.alpha = 1f;
            headphoneCanvasGroup.blocksRaycasts = true;
        }

        float timer = 0f;
        bool continueRequested = false;

        while (timer < noticeAutoAdvanceTime && !continueRequested)
        {
            timer += Time.unscaledDeltaTime;

            // Breathing pulse on prompt text
            if (headphonePromptText != null)
            {
                float pulse = 0.4f + 0.6f * Mathf.PingPong(timer * 1.6f, 1f);
                Color c = headphonePromptText.color;
                c.a = pulse;
                headphonePromptText.color = c;
            }

            // Allow skip after a brief delay (0.35s) so accidental taps don't instantly skip
            if (timer >= 0.35f)
            {
                if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    continueRequested = true;
                }
            }

            yield return null;
        }

        // Mark as shown for the remainder of this game session
        HasShownHeadphoneNotice = true;

        PlayClickSFX();

        // Fade in BGM
        if (menuBGM != null && _bgmSource != null)
        {
            _bgmSource.clip = menuBGM;
            _bgmSource.volume = 0f;
            _bgmSource.Play();
        }

        // Fade in hallway ambient alongside BGM
        if (hallwayAmbientClip != null && _ambientSource != null)
        {
            _ambientSource.clip = hallwayAmbientClip;
            _ambientSource.volume = 0f;
            _ambientSource.Play();
        }

        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);

        float fadeTimer = 0f;
        float targetBgmVolume = bgmVolume;

        float targetAmbientVolume = ambientVolume;

        while (fadeTimer < noticeFadeDuration)
        {
            fadeTimer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(fadeTimer / noticeFadeDuration);

            if (headphoneCanvasGroup != null)
            {
                headphoneCanvasGroup.alpha = 1f - progress;
            }

            if (_menuUIGroup != null)
            {
                _menuUIGroup.alpha = progress;
            }

            if (_bgmSource != null)
            {
                _bgmSource.volume = progress * targetBgmVolume;
            }

            if (_ambientSource != null)
            {
                _ambientSource.volume = progress * targetAmbientVolume;
            }

            yield return null;
        }

        if (headphoneCanvasGroup != null)
        {
            headphoneCanvasGroup.alpha = 0f;
            headphoneCanvasGroup.blocksRaycasts = false;
        }
        if (headphoneNoticePanel != null)
        {
            headphoneNoticePanel.SetActive(false);
        }
        if (_menuUIGroup != null)
        {
            _menuUIGroup.alpha = 1f;
        }
        if (_bgmSource != null)
        {
            _bgmSource.volume = targetBgmVolume;
        }
        if (_ambientSource != null)
        {
            _ambientSource.volume = targetAmbientVolume;
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
        Debug.Log("[MainMenuController] Starting game -> SampleScene (via LoadingScreen)");
        LoadingScreen.LoadScene("SampleScene");
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
