using System.Collections;
using UnityEngine;

/// <summary>
/// TheProctorManager — Singleton controller governing The Proctor's lifecycle during blackouts.
///
/// Responsibilities:
/// 1. Enforces ONLY ONE Proctor spawning at any time.
/// 2. Spawns The Proctor strictly in the hallway of the floor the player is on when blackout begins.
/// 3. Manages the blackout chase countdown timer (blackoutChaseDuration).
/// 4. Handles Timer Expiration: clean despawn, power restoration, 0 anxiety.
/// 5. Handles Player Catch callback: ensures race-condition safety so only one ending executes.
/// 6. Auto-despawns The Proctor whenever power is restored.
/// </summary>
public class TheProctorManager : MonoBehaviour
{
    public static TheProctorManager Instance { get; private set; }

    [Header("Blackout & Proctor Balancing")]
    [Tooltip("How long (seconds) The Proctor stalks the hallway before power returns and it despawns.")]
    public float blackoutChaseDuration = 26.0f;

    [Tooltip("The Proctor Prefab (configured with TheProctorAI, NavMeshAgent, Model & Audio).")]
    public GameObject theProctorPrefab;

    [Header("Hallway Spawn Distance")]
    [Tooltip("Distance along hallway corridor ahead/behind player where Proctor spawns.")]
    public float spawnDistance = 16.0f;

    [Header("Runtime State (Read Only)")]
    public string proctorAssignedHallway = "None";
    public int proctorAssignedFloor = 1;
    public float currentTimer = 0f;
    public bool isEncounterActive = false;

    // Mutex / Atomic flag preventing timer expiration and player-catch from running simultaneously
    private bool _isBlackoutEnding = false;
    private Coroutine _chaseTimerCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Hook into BlackoutManager events
        if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart += HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd += HandleBlackoutEnd;
        }

#if UNITY_EDITOR
        if (theProctorPrefab == null)
        {
            theProctorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TheProctor.prefab");
        }
#endif
    }

    private void OnDestroy()
    {
        if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBlackoutStart -= HandleBlackoutStart;
            BlackoutManager.Instance.OnBlackoutEnd -= HandleBlackoutEnd;
        }
    }

    // ── Blackout Lifecycle ───────────────────────────────────────────────────

    private void HandleBlackoutStart()
    {
        // Prevent duplicate encounters if one is already running
        if (isEncounterActive || _isBlackoutEnding) return;

        // Never spawn during Game Over
        if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver) return;

        StartCoroutine(StartProctorEncounterRoutine());
    }

    private void HandleBlackoutEnd()
    {
        // Power restored — immediately clean up Proctor if still present
        CleanupProctor();
    }

    private IEnumerator StartProctorEncounterRoutine()
    {
        // Wait 1.5 seconds for lights to pop off and blackout sound to finish dropping
        yield return new WaitForSeconds(1.5f);

        if (BlackoutManager.Instance != null && !BlackoutManager.Instance.IsBlackoutActive)
            yield break;

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) yield break;

        Vector3 playerPos = playerGO.transform.position;

        // 1. Detect player's floor and hallway
        DetectPlayerHallway(playerPos);

        // 2. Compute safe hallway spawn location 14-20m away from player
        Vector3 spawnPos = CalculateHallwaySpawnPosition(playerPos);

        // 3. Spawn exactly ONE Proctor
        if (TheProctorAI.ActiveProctor != null)
        {
            TheProctorAI.ActiveProctor.Despawn();
        }

        GameObject proctorGO = null;
        if (theProctorPrefab != null)
        {
            proctorGO = Instantiate(theProctorPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback runtime construct if prefab is missing
            proctorGO = CreateFallbackProctor(spawnPos);
        }

        if (proctorGO != null)
        {
            var ai = proctorGO.GetComponent<TheProctorAI>();
            if (ai != null)
            {
                ai.assignedHallway = proctorAssignedHallway;
                ai.assignedFloor = proctorAssignedFloor;
                ConfigureHallwayBoundaries(ai, proctorAssignedFloor);
            }
        }

        isEncounterActive = true;
        _isBlackoutEnding = false;

        // 4. Start chase countdown timer
        if (_chaseTimerCoroutine != null) StopCoroutine(_chaseTimerCoroutine);
        _chaseTimerCoroutine = StartCoroutine(ChaseCountdownRoutine());
    }

    private IEnumerator ChaseCountdownRoutine()
    {
        currentTimer = blackoutChaseDuration;

        while (currentTimer > 0f)
        {
            // If encounter was resolved (e.g. player caught), exit timer loop
            if (!isEncounterActive || _isBlackoutEnding) yield break;

            currentTimer -= Time.deltaTime;
            yield return null;
        }

        // TIMER EXPIRED CONDITION:
        // Player successfully survived the blackout!
        // Despawn Proctor -> Power ON -> 0 Anxiety added.
        OnTimerExpired();
    }

    /// <summary>
    /// Called when the blackout timer expires without the player getting caught.
    /// Clean escape: despawns Proctor and restores power with 0 anxiety penalty.
    /// </summary>
    private void OnTimerExpired()
    {
        if (_isBlackoutEnding) return;
        _isBlackoutEnding = true;

        Debug.Log("[TheProctorManager] BLACKOUT TIMER EXPIRED! Player survived! Despawning Proctor and restoring power.");

        // Despawn Proctor cleanly
        CleanupProctor();

        // Restore campus power
        if (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive)
        {
            BlackoutManager.Instance.RestorePower();
        }

        isEncounterActive = false;
        _isBlackoutEnding = false;
    }

    /// <summary>
    /// Called by TheProctorAI when the player is caught and jumpscare sequence completes.
    /// Restores power and wraps up the encounter.
    /// </summary>
    public void OnProctorCompletedEncounter()
    {
        if (_isBlackoutEnding) return;
        _isBlackoutEnding = true;

        if (_chaseTimerCoroutine != null)
        {
            StopCoroutine(_chaseTimerCoroutine);
            _chaseTimerCoroutine = null;
        }

        Debug.Log("[TheProctorManager] Proctor caught player encounter completed. Restoring power.");

        // Restore campus power
        if (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive)
        {
            BlackoutManager.Instance.RestorePower();
        }

        isEncounterActive = false;
        _isBlackoutEnding = false;
    }

    private void CleanupProctor()
    {
        if (_chaseTimerCoroutine != null)
        {
            StopCoroutine(_chaseTimerCoroutine);
            _chaseTimerCoroutine = null;
        }

        TheProctorAI.DespawnActiveProctor();
        isEncounterActive = false;
    }

    // ── Hallway & Spawn Math ──────────────────────────────────────────────────

    private void DetectPlayerHallway(Vector3 playerPos)
    {
        if (playerPos.y < 5.5f)
        {
            proctorAssignedFloor = 1;
            proctorAssignedHallway = "Hallway_1F";
        }
        else if (playerPos.y < 11.5f)
        {
            proctorAssignedFloor = 2;
            proctorAssignedHallway = "Hallway_2F";
        }
        else
        {
            proctorAssignedFloor = 3;
            proctorAssignedHallway = "Hallway_3F";
        }

        Debug.Log($"[TheProctorManager] Player detected on Floor {proctorAssignedFloor} -> {proctorAssignedHallway}");
    }

    private void ConfigureHallwayBoundaries(TheProctorAI ai, int floor)
    {
        ai.hallwayCenterX = -84.0f;
        ai.hallwayHalfWidth = 2.4f;

        switch (floor)
        {
            case 1:
                ai.hallwayMinZ = -16.0f;
                ai.hallwayMaxZ = 28.0f;
                ai.hallwayHalfWidth = 3.6f; // Lobby area on 1F is wider
                break;
            case 2:
                ai.hallwayMinZ = -16.0f;
                ai.hallwayMaxZ = 32.0f;
                break;
            case 3:
                ai.hallwayMinZ = -16.0f;
                ai.hallwayMaxZ = 46.0f;
                break;
        }
    }

    private Vector3 CalculateHallwaySpawnPosition(Vector3 playerPos)
    {
        float floorY = 2.43f;
        float minZ = -16.0f;
        float maxZ = 28.0f;

        if (proctorAssignedFloor == 2) { floorY = 8.35f; minZ = -16.0f; maxZ = 32.0f; }
        else if (proctorAssignedFloor == 3) { floorY = 14.34f; minZ = -16.0f; maxZ = 46.0f; }

        // Choose to spawn North or South of the player along the corridor depending on available space
        float targetZ;
        if (playerPos.z < (minZ + maxZ) * 0.5f)
        {
            // Player is in southern half -> spawn to the North
            targetZ = Mathf.Clamp(playerPos.z + spawnDistance, minZ + 2f, maxZ - 2f);
        }
        else
        {
            // Player is in northern half -> spawn to the South
            targetZ = Mathf.Clamp(playerPos.z - spawnDistance, minZ + 2f, maxZ - 2f);
        }

        return new Vector3(-84.0f, floorY, targetZ);
    }

    private GameObject CreateFallbackProctor(Vector3 pos)
    {
        Debug.LogWarning("[TheProctorManager] theProctorPrefab not assigned — constructing runtime fallback entity.");
        var go = new GameObject("TheProctor", typeof(UnityEngine.AI.NavMeshAgent), typeof(TheProctorAI));
        go.transform.position = pos;
        return go;
    }
}
