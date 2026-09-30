using System;
using UnityEngine;

/// <summary>
/// CoinManager — Manages player currency (Philippine Pesos / Coins).
/// Coins are scattered across classrooms, desks, shelves, and hallway nooks to reward exploration.
/// Used to buy limited Ham and Cheese sandwiches from the wandering student vendor ("Naglalako ng Ham and Cheese").
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    [Header("Currency Settings")]
    [Tooltip("Starting coins for the player.")]
    [SerializeField] private int currentCoins = 0;

    [Header("Audio")]
    [Tooltip("Coin collection chime audio clip.")]
    public AudioClip coinChimeClip;

    [Tooltip("Purchase success chime audio clip.")]
    public AudioClip purchaseChimeClip;

    private AudioSource _audioSource;

    public int CurrentCoins => currentCoins;

    /// <summary>
    /// Event fired whenever coin total changes: Action(currentCoins).
    /// </summary>
    public event Action<int> OnCoinsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D UI sound
        _audioSource.playOnAwake = false;

        LoadAudioAssets();
    }

    private void Start()
    {
        OnCoinsChanged?.Invoke(currentCoins);
    }

    private void LoadAudioAssets()
    {
#if UNITY_EDITOR
        if (coinChimeClip == null)
            coinChimeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
        if (purchaseChimeClip == null)
            purchaseChimeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
#endif
    }

    /// <summary>
    /// Adds coins to the player's wallet and fires events.
    /// </summary>
    public void AddCoins(int amount = 1, bool playSound = true)
    {
        if (amount <= 0) return;
        currentCoins += amount;

        if (playSound && coinChimeClip != null && _audioSource != null)
        {
            _audioSource.pitch = UnityEngine.Random.Range(1.15f, 1.30f); // Bright metallic coin ring
            _audioSource.PlayOneShot(coinChimeClip, 0.85f);
        }

        Debug.Log($"[CoinManager] Picked up {amount} coin(s). Total: ₱{currentCoins}");
        OnCoinsChanged?.Invoke(currentCoins);
    }

    /// <summary>
    /// Checks whether player has at least requiredAmount of coins.
    /// </summary>
    public bool HasCoins(int requiredAmount)
    {
        return currentCoins >= requiredAmount;
    }

    /// <summary>
    /// Deducts coins if sufficient balance exists. Returns true if purchase succeeded.
    /// </summary>
    public bool SpendCoins(int amount, bool playSound = true)
    {
        if (amount <= 0) return true;
        if (currentCoins < amount) return false;

        currentCoins -= amount;

        if (playSound && purchaseChimeClip != null && _audioSource != null)
        {
            _audioSource.pitch = 1.0f;
            _audioSource.PlayOneShot(purchaseChimeClip, 0.90f);
        }

        Debug.Log($"[CoinManager] Spent ₱{amount}. Remaining: ₱{currentCoins}");
        OnCoinsChanged?.Invoke(currentCoins);
        return true;
    }

    /// <summary>
    /// Resets currency back to starting amount (e.g. on new run).
    /// </summary>
    public void ResetCoins(int amount = 0)
    {
        currentCoins = amount;
        OnCoinsChanged?.Invoke(currentCoins);
    }
}
