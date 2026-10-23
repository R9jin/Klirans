using System;
using UnityEngine;

/// <summary>
/// AnxietyManager — Core survival horror panic/anxiety system for "KLIRANS".
/// 
/// Tracks the player's Anxiety level from 0 (Calm) to 100 (Total Panic / Breakdown).
/// 
/// Increases from:
///   - Darkness / Blackout events (reduced if holding Flashlight on)
///   - Being chased by or in close proximity to The Proctor
///   - Hallway student NPC jumpscares
///   - Puzzle / lockpicking errors
/// 
/// Decreases from:
///   - Standing in well-lit safe zones while not being chased
///   - Hiding safely inside a locker (slowly calms nerves once Proctor moves away)
///   - Obtaining a clearance signature (relief boost)
/// 
/// Triggers Game Over when Anxiety reaches 100%.
/// </summary>
public class AnxietyManager : MonoBehaviour
{
    private static AnxietyManager _instance;
    public static AnxietyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AnxietyManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Anxiety State")]
    [Range(0f, 100f)]
    [SerializeField] private float currentAnxiety = 0f;
    public float maxAnxiety = 100f;

    public float CurrentAnxiety => currentAnxiety;
    public float NormalizedAnxiety => Mathf.Clamp01(currentAnxiety / maxAnxiety);
    public bool IsGameOver { get; private set; } = false;

    [Header("Passive Buildup Rates (per second)")]
    [Tooltip("Anxiety gained per second during a campus blackout when flashlight is OFF.")]
    public float blackoutDarknessRate = 1.6f;

    [Tooltip("Anxiety gained per second during a campus blackout when flashlight is ON (much slower).")]
    public float blackoutWithFlashlightRate = 0.4f;

    [Tooltip("Anxiety gained per second while The Proctor is actively chasing the player.")]
    public float proctorChaseRate = 4.8f;

    [Tooltip("Anxiety gained per second when The Proctor is patrolling nearby (< 14m).")]
    public float proctorProximityRate = 1.4f;

    [Header("Instant Spike Amounts")]
    [Tooltip("Instant anxiety spike when startled by a wandering NPC jumpscare.")]
    public float jumpscareAnxietyIncrease = 2.0f;

    [Tooltip("Seconds after taking anxiety damage before passive calm recovery begins.")]
    public float calmRecoveryDelay = 1.5f;
    private float _lastDamageTime = -999f;

    [Tooltip("Instant anxiety relief when successfully obtaining a clearance signature.")]
    public float signatureReliefAmount = 25f;

    [Header("Recovery / Calming Rates (per second)")]
    [Tooltip("Anxiety reduced per second when lights are ON and no threat is chasing.")]
    public float normalCalmRecoveryRate = 2.5f;

    [Tooltip("Anxiety reduced per second when hiding inside a locker and Proctor is not right outside.")]
    public float lockerSafeRecoveryRate = 3.2f;

    [Header("Heartbeat & Breathing Audio")]
    public AudioClip heartbeatClip;
    public AudioClip heavyBreathingClip;
    public AudioClip breakdownStingClip;

    private AudioSource _heartbeatAudio;
    private AudioSource _breathingAudio;
    private Transform _playerTransform;

    /// <summary>Fired whenever anxiety changes. Passes (currentAnxiety, maxAnxiety).</summary>
    public event Action<float, float> OnAnxietyChanged;

    /// <summary>Fired once when anxiety hits 100% (Game Over).</summary>
    public event Action OnGameOver;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        jumpscareAnxietyIncrease = 2.0f;
        calmRecoveryDelay = 1.5f;
        blackoutDarknessRate = 1.6f;
        blackoutWithFlashlightRate = 0.4f;
        proctorChaseRate = 4.8f;
        proctorProximityRate = 1.4f;

        SetupAudio();
        LoadDefaultAudio();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
        }
    }

    private void SetupAudio()
    {
        _heartbeatAudio = gameObject.AddComponent<AudioSource>();
        _heartbeatAudio.spatialBlend = 0f;
        _heartbeatAudio.loop = true;
        _heartbeatAudio.playOnAwake = false;
        _heartbeatAudio.volume = 0f;

        _breathingAudio = gameObject.AddComponent<AudioSource>();
        _breathingAudio.spatialBlend = 0f;
        _breathingAudio.loop = true;
        _breathingAudio.playOnAwake = false;
        _breathingAudio.volume = 0f;
    }

    private void LoadDefaultAudio()
    {
#if UNITY_EDITOR
        if (heartbeatClip == null)
            heartbeatClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Heartbeat sound effect.mp3");
        if (heavyBreathingClip == null)
            heavyBreathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/out of breath.mp3");
        if (breakdownStingClip == null)
            breakdownStingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");
#endif
        if (heartbeatClip == null)
            heartbeatClip = Resources.Load<AudioClip>("Sounds/Heartbeat sound effect");
        if (heavyBreathingClip == null)
            heavyBreathingClip = Resources.Load<AudioClip>("Sounds/out of breath");
        if (breakdownStingClip == null)
            breakdownStingClip = Resources.Load<AudioClip>("Sounds/you died (lobotomy sound)");

        if (_heartbeatAudio != null && heartbeatClip != null)
            _heartbeatAudio.clip = heartbeatClip;
        if (_breathingAudio != null && heavyBreathingClip != null)
            _breathingAudio.clip = heavyBreathingClip;
    }

    private void Update()
    {
        if (GameModeManager.IsFreeRoamMode) return;
        if (IsGameOver) return;
        if (PauseMenu.GameIsPaused) return;
        if (OpeningCutsceneDirector.IsAnyCutscenePlaying) return;

        // Pause passive anxiety accumulation while solving desk puzzles or reading dialogue
        if (GuidanceWordPuzzle.Instance != null && GuidanceWordPuzzle.Instance.IsOpen) return;
        if (CashierBalancePuzzle.Instance != null && CashierBalancePuzzle.Instance.IsOpen) return;
        if (RegistrarDocumentSortPuzzle.Instance != null && RegistrarDocumentSortPuzzle.Instance.IsOpen) return;
        if (NPCDialogueSystem.Instance != null && NPCDialogueSystem.Instance.IsDialogueActive) return;

        if (_playerTransform == null) FindPlayer();

        float delta = 0f;
        bool isThreatActive = false;

        // 1. Check Blackout Darkness Stress
        bool isBlackout = (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive);
        bool isFlashlightOn = (FlashlightController.Instance != null && FlashlightController.Instance.IsLightOn);

        if (isBlackout && !LockerHideManager.IsPlayerHidden)
        {
            delta += isFlashlightOn ? blackoutWithFlashlightRate : blackoutDarknessRate;
            isThreatActive = true;
        }

        // 2. Check The Proctor Proximity & Chase Stress
        var proctor = TheProctorAI.ActiveProctor;
        if (proctor != null && proctor.gameObject.activeInHierarchy && _playerTransform != null)
        {
            float dist = Vector3.Distance(_playerTransform.position, proctor.transform.position);
            float yDiff = Mathf.Abs(_playerTransform.position.y - proctor.transform.position.y);

            if (proctor.currentState == TheProctorAI.ProctorState.Chasing && !LockerHideManager.IsPlayerHidden)
            {
                delta += proctorChaseRate;
                isThreatActive = true;
            }
            else if (yDiff < 2.2f && dist < 14f)
            {
                float proxFactor = 1f - Mathf.Clamp01(dist / 14f);
                if (LockerHideManager.IsPlayerHidden)
                {
                    // If hidden in locker and Proctor is right outside (< 4.5m), slight tension holds
                    if (dist < 4.5f)
                    {
                        delta += proctorProximityRate * 0.35f * proxFactor;
                        isThreatActive = true;
                    }
                }
                else
                {
                    delta += proctorProximityRate * proxFactor;
                    isThreatActive = true;
                }
            }
        }

        // 2b. Check Weeping Student (Statue Entity) Proximity & Stalking Stress
        var weeping = WeepingStudentAI.ActiveWeepingStudent;
        if (weeping != null && weeping.gameObject.activeInHierarchy && _playerTransform != null)
        {
            float dist = Vector3.Distance(_playerTransform.position, weeping.transform.position);
            float yDiff = Mathf.Abs(_playerTransform.position.y - weeping.transform.position.y);

            if (yDiff < 2.2f && dist < 15f && !LockerHideManager.IsPlayerHidden)
            {
                float proxFactor = 1f - Mathf.Clamp01(dist / 15f);
                if (!weeping.IsObserved)
                {
                    // Rapidly charging from behind in the dark!
                    delta += proctorChaseRate * 0.85f * proxFactor;
                }
                else
                {
                    // Frozen statue staring at the player — eerie psychological tension
                    delta += proctorProximityRate * 0.75f * proxFactor;
                }
                isThreatActive = true;
            }
        }

        // 3. Recovery when safe (only after calmRecoveryDelay cooldown)
        if (!isThreatActive)
        {
            if (LockerHideManager.IsPlayerHidden)
            {
                if (Time.time - _lastDamageTime >= 1.5f)
                {
                    delta -= lockerSafeRecoveryRate;
                }
            }
            else if (!isBlackout)
            {
                if (Time.time - _lastDamageTime >= calmRecoveryDelay)
                {
                    delta -= normalCalmRecoveryRate;
                }
            }
        }

        if (Mathf.Abs(delta) > 0.0001f)
        {
            ModifyAnxiety(delta * Time.deltaTime);
        }

        UpdateHeartbeatAudio();
    }

    /// <summary>
    /// Adds (or subtracts if negative) a specific amount of anxiety immediately.
    /// </summary>
    public void AddAnxiety(float amount)
    {
        if (GameModeManager.IsFreeRoamMode && amount > 0f) return;
        if (DevDemoPanel.IsGodModeActive && amount > 0f) return;
        if (IsGameOver) return;
        ModifyAnxiety(amount);
    }

    /// <summary>
    /// Alias for AddAnxiety used by items and menus.
    /// </summary>
    public void ChangeAnxiety(float delta)
    {
        if (IsGameOver) return;
        ModifyAnxiety(delta);
    }

    /// <summary>
    /// Sets anxiety directly to a specific value (0..maxAnxiety).
    /// </summary>
    public void SetAnxiety(float value)
    {
        currentAnxiety = Mathf.Clamp(value, 0f, maxAnxiety);
        if (currentAnxiety < maxAnxiety) IsGameOver = false;
        OnAnxietyChanged?.Invoke(currentAnxiety, maxAnxiety);
        if (currentAnxiety >= maxAnxiety && !IsGameOver)
        {
            TriggerBreakdownGameOver();
        }
    }

    /// <summary>
    /// Reduces anxiety by a specific amount.
    /// </summary>
    public void ReduceAnxiety(float amount)
    {
        if (IsGameOver) return;
        ModifyAnxiety(-Mathf.Abs(amount));
    }

    /// <summary>
    /// Resets anxiety back to 0 and clears Game Over flag.
    /// </summary>
    public void ResetAnxiety()
    {
        IsGameOver = false;
        currentAnxiety = 0f;
        OnAnxietyChanged?.Invoke(currentAnxiety, maxAnxiety);
        if (_heartbeatAudio != null) _heartbeatAudio.Stop();
        if (_breathingAudio != null) _breathingAudio.Stop();
    }

    private void ModifyAnxiety(float delta)
    {
        if (GameModeManager.IsFreeRoamMode && delta > 0f) return;
        if (DevDemoPanel.IsGodModeActive && delta > 0f) return;
        if (IsGameOver) return;

        if (delta > 0.05f)
        {
            _lastDamageTime = Time.time;
        }

        float prev = currentAnxiety;
        currentAnxiety = Mathf.Clamp(currentAnxiety + delta, 0f, maxAnxiety);

        if (Mathf.Abs(currentAnxiety - prev) > 0.001f)
        {
            OnAnxietyChanged?.Invoke(currentAnxiety, maxAnxiety);
        }

        if (currentAnxiety >= maxAnxiety && !IsGameOver)
        {
            TriggerBreakdownGameOver();
        }
    }

    private void UpdateHeartbeatAudio()
    {
        float norm = NormalizedAnxiety;

        // Start playing heartbeat once anxiety exceeds 28%
        if (_heartbeatAudio != null && heartbeatClip != null)
        {
            if (norm > 0.28f)
            {
                if (!_heartbeatAudio.isPlaying) _heartbeatAudio.Play();
                float t = Mathf.InverseLerp(0.28f, 1.0f, norm);
                _heartbeatAudio.volume = Mathf.Lerp(0.12f, 0.90f, t);
                _heartbeatAudio.pitch = Mathf.Lerp(0.85f, 1.55f, t);
            }
            else if (_heartbeatAudio.isPlaying)
            {
                _heartbeatAudio.volume = Mathf.MoveTowards(_heartbeatAudio.volume, 0f, Time.deltaTime * 0.5f);
                if (_heartbeatAudio.volume <= 0.01f) _heartbeatAudio.Stop();
            }
        }

        // Start heavy panic breathing above 65% anxiety
        if (_breathingAudio != null && heavyBreathingClip != null)
        {
            if (norm > 0.65f)
            {
                if (!_breathingAudio.isPlaying) _breathingAudio.Play();
                float t = Mathf.InverseLerp(0.65f, 1.0f, norm);
                _breathingAudio.volume = Mathf.Lerp(0.10f, 0.65f, t);
                _breathingAudio.pitch = Mathf.Lerp(0.95f, 1.20f, t);
            }
            else if (_breathingAudio.isPlaying)
            {
                _breathingAudio.volume = Mathf.MoveTowards(_breathingAudio.volume, 0f, Time.deltaTime * 0.5f);
                if (_breathingAudio.volume <= 0.01f) _breathingAudio.Stop();
            }
        }
    }

    private void TriggerBreakdownGameOver()
    {
        IsGameOver = true;
        Debug.Log("[AnxietyManager] ANXIETY REACHED 100%! Player suffered a total panic breakdown — GAME OVER.");

        if (_heartbeatAudio != null) _heartbeatAudio.Stop();
        if (_breathingAudio != null) _breathingAudio.Stop();

        if (breakdownStingClip != null && _heartbeatAudio != null)
        {
            _heartbeatAudio.loop = false;
            _heartbeatAudio.pitch = 1.0f;
            _heartbeatAudio.volume = 1.0f;
            _heartbeatAudio.PlayOneShot(breakdownStingClip, 1.0f);
        }

        // Freeze player controls
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.SetControlsEnabled(false);
        }

        OnGameOver?.Invoke();
        if (GameOverUI.Instance != null)
        {
            GameOverUI.Instance.HandleGameOver();
        }
    }
}
