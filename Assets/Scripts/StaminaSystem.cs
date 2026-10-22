using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    [Header("UI")]
    public Image staminaFill;

    [Header("UI Auto-Hide")]
    [Tooltip("CanvasGroup on StaminaGauge for smooth fading in/out.")]
    public CanvasGroup staminaCanvasGroup;
    public float fadeSpeed = 3.5f;
    [Tooltip("How long stamina remains visible after fully recovering before fading.")]
    public float hideDelay = 1.8f;

    [Header("Audio")]
    public AudioSource breathingAudioSource;
    public float exhaustionThreshold = 30f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;

    [Header("Drain & Recovery")]
    public float drainRate = 20f;
    public float recoveryRate = 15f;

    public bool CanSprint => currentStamina > 0f;

    private float hideTimer = 0f;
    private bool isDraining = false;

    void Start()
    {
        currentStamina = maxStamina;

        // Auto-find StaminaGauge CanvasGroup if not assigned
        if (staminaCanvasGroup == null && staminaFill != null)
        {
            staminaCanvasGroup = staminaFill.GetComponentInParent<CanvasGroup>();
            if (staminaCanvasGroup == null)
            {
                Transform parent = staminaFill.transform.parent;
                if (parent != null) staminaCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Start hidden if at full stamina
        if (staminaCanvasGroup != null)
        {
            staminaCanvasGroup.alpha = 0f;
            staminaCanvasGroup.blocksRaycasts = false;
        }

        UpdateUI();
    }

    void Update()
    {
        UpdateAutoFade();
    }

    public void Drain()
    {
        isDraining = true;
        hideTimer = hideDelay;
        currentStamina -= drainRate * Time.deltaTime;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        UpdateUI();
    }

    public void Recover()
    {
        currentStamina += recoveryRate * Time.deltaTime;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        UpdateUI();
    }

    /// <summary>
    /// Smoothly fades the stamina bar in when running or depleted, and fades it out when recovered.
    /// </summary>
    private void UpdateAutoFade()
    {
        if (staminaCanvasGroup == null) return;

        bool needsDisplay = isDraining || (currentStamina < maxStamina - 0.5f);

        if (needsDisplay)
        {
            hideTimer = hideDelay;
        }
        else if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
        }

        float targetAlpha = (needsDisplay || hideTimer > 0f) ? 1f : 0f;
        staminaCanvasGroup.alpha = Mathf.MoveTowards(staminaCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        staminaCanvasGroup.blocksRaycasts = staminaCanvasGroup.alpha > 0.05f;

        // Reset draining flag at end of frame (Drain() is called in PlayerMovement Update)
        isDraining = false;
    }

    /// <summary>
    /// Forces stamina UI to wake up and display for the specified duration.
    /// </summary>
    public void PingVisibility(float duration = -1f)
    {
        hideTimer = duration > 0f ? duration : hideDelay;
    }

    void UpdateUI()
    {
        if (staminaFill != null)
        {
            staminaFill.fillAmount = currentStamina / maxStamina;
        }

        // Exhaustion Audio Logic
        if (breathingAudioSource != null)
        {
            if (currentStamina <= exhaustionThreshold)
            {
                if (!breathingAudioSource.isPlaying) breathingAudioSource.Play();
            }
            else
            {
                if (breathingAudioSource.isPlaying) breathingAudioSource.Pause();
            }
        }
    }
}