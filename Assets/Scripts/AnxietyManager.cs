using System;
using UnityEngine;

/// <summary>
/// Authoritative manager for the player's Anxiety state.
/// Range: 0 (calm) to 100 (overwhelmed/game over).
/// Single source of truth across the game.
/// </summary>
public class AnxietyManager : MonoBehaviour
{
    public static AnxietyManager Instance { get; private set; }

    [Header("Anxiety Configuration")]
    [Tooltip("Maximum allowable anxiety before Game Over.")]
    public float maxAnxiety = 100f;

    [Tooltip("Current anxiety level (0 - 100).")]
    [SerializeField] private float currentAnxiety = 0f;

    [Tooltip("Amount of anxiety added per successful Proctor jumpscare.")]
    public float jumpscareAnxietyIncrease = 10f;

    [Tooltip("Current anxiety value (read-only property).")]
    public float CurrentAnxiety => currentAnxiety;

    [Tooltip("True once anxiety has hit max and Game Over has triggered.")]
    public bool IsGameOver { get; private set; } = false;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired whenever anxiety level changes: (currentAnxiety, maxAnxiety).</summary>
    public event Action<float, float> OnAnxietyChanged;

    /// <summary>Fired once when anxiety reaches maximum, triggering Game Over.</summary>
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentAnxiety = Mathf.Clamp(currentAnxiety, 0f, maxAnxiety);
    }

    private void Start()
    {
        // Broadcast initial state
        NotifyAnxietyChanged();
    }

    /// <summary>
    /// Increases anxiety by the specified amount (e.g. from Proctor jumpscare).
    /// </summary>
    public void AddAnxiety(float amount)
    {
        if (IsGameOver) return;
        if (amount <= 0f) return;

        SetAnxiety(currentAnxiety + amount);
    }

    /// <summary>
    /// Changes anxiety by a delta (positive adds anxiety, negative relieves anxiety e.g. consumables).
    /// </summary>
    public void ChangeAnxiety(float delta)
    {
        if (IsGameOver) return;
        SetAnxiety(currentAnxiety + delta);
    }

    /// <summary>
    /// Directly sets anxiety level, clamped between 0 and maxAnxiety.
    /// Triggers Game Over if maxAnxiety is reached.
    /// </summary>
    public void SetAnxiety(float newValue)
    {
        if (IsGameOver) return;

        float clamped = Mathf.Clamp(newValue, 0f, maxAnxiety);
        if (Mathf.Approximately(clamped, currentAnxiety)) return;

        currentAnxiety = clamped;
        NotifyAnxietyChanged();

        if (currentAnxiety >= maxAnxiety)
        {
            TriggerGameOver();
        }
    }

    /// <summary>
    /// Resets anxiety to 0 (e.g. on new game or debug reset).
    /// </summary>
    public void ResetAnxiety()
    {
        IsGameOver = false;
        currentAnxiety = 0f;
        NotifyAnxietyChanged();
    }

    private void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        Debug.LogWarning("[AnxietyManager] ANXIETY REACHED MAXIMUM (100) — GAME OVER!");
        OnGameOver?.Invoke();
    }

    private void NotifyAnxietyChanged()
    {
        OnAnxietyChanged?.Invoke(currentAnxiety, maxAnxiety);
    }
}
