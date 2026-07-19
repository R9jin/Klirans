using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    [Header("UI")]
    public Image staminaFill;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;

    [Header("Drain & Recovery")]
    public float drainRate = 20f;
    public float recoveryRate = 15f;

    public bool CanSprint => currentStamina > 0f;

    void Start()
    {
        currentStamina = maxStamina;
        UpdateUI();
    }

    public void Drain()
    {
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

    void UpdateUI()
    {
        if (staminaFill != null)
        {
            staminaFill.fillAmount = currentStamina / maxStamina;
        }
    }
}