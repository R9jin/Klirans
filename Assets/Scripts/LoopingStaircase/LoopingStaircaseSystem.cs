using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LoopingStaircaseSystem — The brain of the psychological staircase illusion.
///
/// HOW IT WORKS
/// ─────────────
/// Each StairTriggerZone sits at the mid-point of a stair flight. When the player
/// walks through one, this system:
///   1. Detects whether the player is going UP or DOWN (via CharacterController velocity).
///   2. Looks up the matching StaircaseConnection rule (keyed by stairwell + fromFloor + direction).
///   3. Rolls the probability dice to pick a destination floor.
///   4. If the destination differs from the "normal" one, waits for a camera-concealment
///      window then seamlessly teleports the player.
///   5. Restores the player's facing direction so the transition is imperceptible.
///
/// SETUP CHECKLIST
/// ─────────────────
///  □ Attach this component to a persistent, empty GameObject (e.g. "StaircaseManager").
///  □ Populate "Staircase Connections" — one entry per directed floor-pair per stairwell.
///    Each entry needs a unique (stairwellID + fromFloor + goingUp) combination.
///  □ Add a StairTriggerZone (BoxCollider, IsTrigger=true) at the mid-point of every
///    stair flight. Set its stairwellID and fromFloor.
///  □ Add a FloorSpawnPoint at each floor landing for each stairwell.
///  □ Tag your Player GameObject as "Player" in the Unity Inspector.
///  □ Assign playerMovement and playerCamera in the Inspector (or let auto-find).
/// </summary>
public class LoopingStaircaseSystem : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    //  Inspector-Exposed Settings
    // ══════════════════════════════════════════════════════════════════════

    [Header("Staircase Connection Rules")]
    [Tooltip("Add one entry per directed floor pair per stairwell.\n" +
             "Each (stairwellID + fromFloor + goingUp) must be unique.\n" +
             "The system supports any number of floors and stairwells.")]
    public StaircaseConnection[] staircaseConnections = new StaircaseConnection[]
    {
        // ─────────────────────────────────────────────────────────────────
        //  MAIN STAIRS
        // ─────────────────────────────────────────────────────────────────

        // 1F → 2F (going up)
        new StaircaseConnection {
            stairwellID = "MainStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
            }
        },
        // 2F → 3F (going up)
        new StaircaseConnection {
            stairwellID = "MainStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.125f },
            }
        },
        // 3F → 2F (going down)
        new StaircaseConnection {
            stairwellID = "MainStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
            }
        },
        // 2F → 1F (going down)
        new StaircaseConnection {
            stairwellID = "MainStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.375f },
            }
        },

        // ─────────────────────────────────────────────────────────────────
        //  RIGHT STAIRS
        // ─────────────────────────────────────────────────────────────────

        new StaircaseConnection {
            stairwellID = "RightStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
            }
        },
        new StaircaseConnection {
            stairwellID = "RightStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.125f },
            }
        },
        new StaircaseConnection {
            stairwellID = "RightStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
            }
        },
        new StaircaseConnection {
            stairwellID = "RightStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.375f },
            }
        },

        // ─────────────────────────────────────────────────────────────────
        //  LEFT STAIRS
        // ─────────────────────────────────────────────────────────────────

        new StaircaseConnection {
            stairwellID = "LeftStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
            }
        },
        new StaircaseConnection {
            stairwellID = "LeftStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.125f },
            }
        },
        new StaircaseConnection {
            stairwellID = "LeftStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 1, weight = 0.375f },
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
            }
        },
        new StaircaseConnection {
            stairwellID = "LeftStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.50f,
            alternateOutcomes = new [] {
                new AlternateOutcome { destinationFloor = 3, weight = 0.125f },
                new AlternateOutcome { destinationFloor = 2, weight = 0.375f },
            }
        },
        
        // ─────────────────────────────────────────────────────────────────
        //  FORCED 3F → 1F (UPWARD LOOP)
        // ─────────────────────────────────────────────────────────────────
        
        // 3F → forced back to 1F (going up — no 4th floor exists)
        new StaircaseConnection {
            stairwellID = "MainStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
            alternateOutcomes = new AlternateOutcome[0], forceLoop = true
        },
        new StaircaseConnection {
            stairwellID = "RightStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
            alternateOutcomes = new AlternateOutcome[0], forceLoop = true
        },
        new StaircaseConnection {
            stairwellID = "LeftStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
            alternateOutcomes = new AlternateOutcome[0], forceLoop = true
        },
    };

    [Header("Cooldown")]
    [Tooltip("Minimum seconds between looping transitions. Prevents the player from " +
             "getting trapped in rapid-fire loops.")]
    [Range(2f, 30f)]
    public float transitionCooldown = 8f;

    [Tooltip("If true, the very first stair traversal after scene load is always the " +
             "normal destination, giving the player time to learn the layout.")]
    public bool safeFirstTraversal = true;

    [Header("Camera-Concealment")]
    [Tooltip("How many degrees from straight-up the camera must be looking before we " +
             "consider it 'ceiling-facing' and safe to teleport.")]
    [Range(20f, 80f)]
    public float ceilingAngleThreshold = 45f;

    [Tooltip("How many degrees from straight-down counts as 'floor-facing'.")]
    [Range(20f, 80f)]
    public float floorAngleThreshold = 60f;

    [Tooltip("Maximum seconds to wait for a concealment window before forcing the " +
             "teleport anyway. Safety net against the player staring perfectly forward.")]
    [Range(0f, 5f)]
    public float maxWaitForConcealment = 0.5f;

    [Tooltip("Always play a very brief screen blink on EVERY teleport, regardless of " +
             "camera angle. Recommended ON — makes the transition imperceptible even " +
             "when the player turns fast.")]
    public bool alwaysBlink = true;

    [Tooltip("Duration of the full-black micro-blink in seconds. Keep very short (0.1-0.2s) " +
             "— long enough to hide the position change, short enough to feel like eye-blink.")]
    [Range(0.05f, 0.5f)]
    public float blinkDuration = 0.15f;

    [Tooltip("Seconds of full-black emergency blink used if concealment window is never found. " +
             "Only used when alwaysBlink is OFF.")]
    [Range(0f, 0.25f)]
    public float emergencyBlinkDuration = 0.12f;

    [Header("References")]
    [Tooltip("The root player GameObject. Auto-found by tag 'Player' if left empty.")]
    public GameObject playerObject;

    [Tooltip("The PlayerMovement component. Auto-found if left empty.")]
    public PlayerMovement playerMovement;

    [Tooltip("The player's camera. Auto-found via PlayerMovement if left empty.")]
    public Camera playerCamera;

    [Tooltip("Optional: a full-screen black Image/RawImage for emergency micro-blink. " +
             "It must already exist in the Canvas with alpha=0. If null, blink is skipped.")]
    public UnityEngine.UI.Graphic blinkOverlay;

    // ══════════════════════════════════════════════════════════════════════
    //  Runtime State
    // ══════════════════════════════════════════════════════════════════════

    private float lastTransitionTime = -999f;
    private bool  firstTraversalDone = false;
    private bool  transitionInProgress = false;

    /// <summary>Connection lookup keyed by (stairwellID, fromFloor, goingUp).</summary>
    private Dictionary<(string stairwell, int fromFloor, bool goingUp), StaircaseConnection> connectionMap;

    /// <summary>Spawn-point lookup keyed by (stairwellID, floorNumber, usedWhenArrivingUp).</summary>
    private Dictionary<(string stairwell, int floor, bool up), FloorSpawnPoint> spawnMap;

    // ══════════════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        BuildLookupTables();
        AutoFindReferences();
    }

    private void Start()
    {
        // ── Auto-create blink overlay if none is assigned ─────────────────
        if (blinkOverlay == null)
            blinkOverlay = CreateBlinkOverlay();

        if (blinkOverlay != null)
        {
            Color c = blinkOverlay.color;
            c.a = 0f;
            blinkOverlay.color = c;
            blinkOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Programmatically creates a fullscreen black Canvas + Image that sits
    /// in front of the camera, used as the blink overlay.
    /// </summary>
    private UnityEngine.UI.Graphic CreateBlinkOverlay()
    {
        // Create a world-space Canvas as a child of the player camera
        var canvasGO = new GameObject("[LoopingStaircase_BlinkOverlay]");
        canvasGO.transform.SetParent(null);  // top-level — not a camera child

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;            // on top of everything

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var imgGO = new GameObject("BlinkImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        var img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        var imgRT = imgGO.GetComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;

        Debug.Log("[LoopingStaircase] Auto-created blink overlay.");
        return img;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Public API (called by StairTriggerZone)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by a ForcedLoopTrigger boundary box when the player crosses it.
    /// Skips all probability rolls and immediately loops the player to the
    /// destination floor. Used as a hard boundary failsafe at stairwell tops.
    /// </summary>
    public void TriggerForcedLoop(string stairwellID, int fromFloor, int destinationFloor)
    {
        if (transitionInProgress) return;

        // Prefer the ArrivingUp spawn (player was going up when caught by boundary)
        FloorSpawnPoint spawn = null;
        if (!spawnMap.TryGetValue((stairwellID, destinationFloor, true),  out spawn))
             spawnMap.TryGetValue((stairwellID, destinationFloor, false), out spawn);

        if (spawn == null)
        {
            Debug.LogWarning($"[LoopingStaircase] TriggerForcedLoop: no spawn found for " +
                             $"{stairwellID} floor {destinationFloor}");
            return;
        }

        Debug.Log($"[LoopingStaircase] 🚨 BOUNDARY FORCED LOOP: {stairwellID} F{fromFloor}→F{destinationFloor}");
        StartCoroutine(ExecuteSeamlessTransition(spawn, stairwellID, fromFloor, destinationFloor));
    }

    /// <summary>
    /// Called by a StairTriggerZone when the player enters the mid-stair trigger.
    /// Determines travel direction from the CharacterController's vertical velocity.
    /// </summary>
    public void OnPlayerEnteredStairTrigger(string stairwellID, int fromFloor, GameObject player)
    {
        if (transitionInProgress) return;

        // ── Detect travel direction via vertical velocity ─────────────────
        CharacterController cc = player.GetComponent<CharacterController>();
        bool goingUp = true; // default assumption

        if (cc != null)
        {
            float vertVel = cc.velocity.y;
            if (Mathf.Abs(vertVel) > 0.2f)
                goingUp = vertVel > 0f;
            else
                goingUp = (fromFloor == 1 || fromFloor == 2);
        }

        // Look up the connection rule (direction-aware)
        var key = (stairwellID, fromFloor, goingUp);
        if (!connectionMap.TryGetValue(key, out StaircaseConnection connection))
        {
            var fallbackKey = (stairwellID, fromFloor, !goingUp);
            if (!connectionMap.TryGetValue(fallbackKey, out connection))
            {
                Debug.LogWarning($"[LoopingStaircase] No rule found for stairwell='{stairwellID}' " +
                                 $"fromFloor={fromFloor} goingUp={goingUp}. Check your connections.");
                return;
            }
            goingUp = !goingUp;
        }

        // Force loop connections (e.g. 3F -> 1F up) bypass safeFirstTraversal & cooldown
        if (connection.forceLoop)
        {
            int forceDest = connection.normalDestination;
            FloorSpawnPoint forceSpawn = FindSpawn(stairwellID, forceDest, true)
                                      ?? FindSpawn(stairwellID, forceDest, false);
            if (forceSpawn != null)
            {
                Debug.Log($"[LoopingStaircase] 🌀 FORCED CONNECTION LOOP: {stairwellID} F{fromFloor}→F{forceDest}");
                StartCoroutine(ExecuteSeamlessTransition(forceSpawn, stairwellID, fromFloor, forceDest));
            }
            return;
        }

        // Cooldown guard for normal non-forced triggers
        if (Time.time - lastTransitionTime < transitionCooldown)
        {
            Debug.Log($"[LoopingStaircase] Trigger on cooldown. Skipping. ({stairwellID} F{fromFloor})");
            return;
        }

        // Safe first traversal (only for normal random loops)
        if (safeFirstTraversal && !firstTraversalDone)
        {
            firstTraversalDone = true;
            Debug.Log("[LoopingStaircase] Safe first traversal – no redirect.");
            lastTransitionTime = Time.time;
            return;
        }

        // Roll destination
        int destination = connection.PickDestination();

        if (!connection.forceLoop && destination == connection.normalDestination)
        {
            string dirArrow = goingUp ? "↑" : "↓";
            Debug.Log($"[LoopingStaircase] Normal traversal: {stairwellID} F{fromFloor}→F{destination} ({dirArrow})");
            lastTransitionTime = Time.time;
            firstTraversalDone = true;
            return; // No redirect needed
        }

        // Redirect needed!
        Debug.Log($"[LoopingStaircase] 👻 LOOPING: {stairwellID} F{fromFloor}→F{destination} " +
                  $"(normal was F{connection.normalDestination}, going {(goingUp ? "UP" : "DOWN")})");

        // Find spawn point: player arrives going up if destination > fromFloor
        bool arrivingUp = destination > fromFloor;
        FloorSpawnPoint spawn = FindSpawn(stairwellID, destination, arrivingUp)
                             ?? FindSpawn(stairwellID, destination, !arrivingUp);

        if (spawn == null)
        {
            Debug.LogError($"[LoopingStaircase] No FloorSpawnPoint found for " +
                           $"stairwell='{stairwellID}' floor={destination}. " +
                           "Add FloorSpawnPoint GameObjects at each stair landing.", this);
            return;
        }

        StartCoroutine(ExecuteSeamlessTransition(spawn, stairwellID, fromFloor, destination));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Core Transition Logic
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteSeamlessTransition(FloorSpawnPoint targetSpawn,
                                                   string stairwellID, int fromFloor, int toFloor)
    {
        transitionInProgress = true;

        // ── Step 1: Capture full player orientation before teleport ───────
        float savedPlayerYaw = playerObject.transform.eulerAngles.y;
        float savedPitch     = playerCamera != null
            ? playerCamera.transform.localEulerAngles.x
            : 0f;

        CharacterController cc = playerObject.GetComponent<CharacterController>();

        // ── Step 2: Blink screen to black (always, so no fast-turn reveal) ─
        if (alwaysBlink && blinkOverlay != null)
        {
            // Instantly black out the screen
            yield return StartCoroutine(BlinkToBlack());
        }
        else
        {
            // Fallback: wait for natural concealment window
            float waitStart = Time.time;
            bool concealmentFound = false;
            while (Time.time - waitStart < maxWaitForConcealment)
            {
                if (IsCameraConcealed()) { concealmentFound = true; break; }
                yield return null;
            }
            if (!concealmentFound && emergencyBlinkDuration > 0f && blinkOverlay != null)
                yield return StartCoroutine(MicroBlink());
        }

        // ── Step 3: Perform teleport (screen is black) ────────────────────
        PerformTeleport(cc, savedPlayerYaw, savedPitch, stairwellID, fromFloor, toFloor, targetSpawn);

        // ── Step 4: One frame for physics to settle ───────────────────────
        yield return null;

        // ── Step 5: Fade screen back in ───────────────────────────────────
        if (alwaysBlink && blinkOverlay != null)
            yield return StartCoroutine(FadeBlinkOut());
        else if (blinkOverlay != null)
            yield return StartCoroutine(FadeBlinkOut());

        lastTransitionTime = Time.time;
        firstTraversalDone = true;
        transitionInProgress = false;

        Debug.Log($"[LoopingStaircase] Transition complete: {stairwellID} F{fromFloor}→F{toFloor}");
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Teleport Execution (Seamless Relative Offset)
    // ──────────────────────────────────────────────────────────────────────

    private void PerformTeleport(CharacterController cc, float savedPlayerYaw, float savedPitch,
                                  string stairwellID, int fromFloor, int toFloor, FloorSpawnPoint fallbackSpawn)
    {
        if (cc != null) cc.enabled = false;

        // Move to target floor landing spawn point if available, otherwise relative shift
        if (fallbackSpawn != null)
        {
            playerObject.transform.position = fallbackSpawn.transform.position;
            playerObject.transform.rotation = Quaternion.Euler(0f, fallbackSpawn.spawnFacingYaw, 0f);
        }
        else
        {
            float yDelta = GetVerticalFloorOffset(stairwellID, fromFloor, toFloor);
            playerObject.transform.position += new Vector3(0f, yDelta, 0f);
            playerObject.transform.rotation = Quaternion.Euler(0f, savedPlayerYaw, 0f);
        }

        // Preserve exact camera pitch so head look angle doesn't snap
        if (playerCamera != null)
        {
            Vector3 euler = playerCamera.transform.localEulerAngles;
            euler.x = savedPitch;
            playerCamera.transform.localEulerAngles = euler;
        }

        if (cc != null) cc.enabled = true;
    }

    private float GetVerticalFloorOffset(string stairwellID, int fromFloor, int toFloor)
    {
        string pathFrom = GetFlightPath(stairwellID, fromFloor);
        string pathTo   = GetFlightPath(stairwellID, toFloor);

        var goFrom = GameObject.Find(pathFrom);
        var goTo   = GameObject.Find(pathTo);

        if (goFrom != null && goTo != null)
        {
            return goTo.transform.position.y - goFrom.transform.position.y;
        }

        return (toFloor - fromFloor) * 5.997195f;
    }

    private string GetFlightPath(string stairwellID, int floor)
    {
        string floorName = floor == 1 ? "Flight_1stFloor" : (floor == 2 ? "Flight_2ndFloor" : "Flight_3rdFloor");
        return $"Stairs/{stairwellID}/{floorName}";
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Concealment Detection
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the camera angle or nearby wall means the player
    /// cannot clearly see the destination environment.
    /// </summary>
    private bool IsCameraConcealed()
    {
        if (playerCamera == null) return false;

        Vector3 camForward = playerCamera.transform.forward;
        float upDot = Vector3.Dot(camForward, Vector3.up);

        float lookUpAngle   = Mathf.Asin(Mathf.Clamp( upDot, -1f, 1f)) * Mathf.Rad2Deg;
        float lookDownAngle = Mathf.Asin(Mathf.Clamp(-upDot, -1f, 1f)) * Mathf.Rad2Deg;

        if (lookUpAngle   >= ceilingAngleThreshold) return true;
        if (lookDownAngle >= floorAngleThreshold)   return true;

        // Wall within arm's reach directly in front
        if (Physics.Raycast(playerCamera.transform.position, camForward, out RaycastHit hit, 0.9f))
        {
            if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.3f)
                return true; // vertical wall face
        }

        return false;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Micro-Blink Coroutines
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Snap the screen to full black instantly.</summary>
    private IEnumerator BlinkToBlack()
    {
        if (blinkOverlay == null) yield break;
        blinkOverlay.gameObject.SetActive(true);
        Color c = blinkOverlay.color;
        c.a = 1f;
        blinkOverlay.color = c;
        // Hold black for half the blink duration before teleporting
        yield return new WaitForSeconds(blinkDuration * 0.4f);
    }

    /// <summary>Legacy emergency blink — used when alwaysBlink is OFF.</summary>
    private IEnumerator MicroBlink()
    {
        if (blinkOverlay == null) yield break;
        blinkOverlay.gameObject.SetActive(true);
        Color c = blinkOverlay.color;
        c.a = 1f;
        blinkOverlay.color = c;
        yield return new WaitForSeconds(emergencyBlinkDuration * 0.5f);
    }

    /// <summary>Fade the black overlay back to transparent after teleport.</summary>
    private IEnumerator FadeBlinkOut()
    {
        if (blinkOverlay == null) yield break;
        float elapsed = 0f;
        float dur = blinkDuration;   // fade-in matches the configured blink time
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            Color c = blinkOverlay.color;
            c.a = Mathf.Lerp(1f, 0f, elapsed / dur);
            blinkOverlay.color = c;
            yield return null;
        }
        Color fin = blinkOverlay.color;
        fin.a = 0f;
        blinkOverlay.color = fin;
        blinkOverlay.gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Lookup Table Helpers
    // ──────────────────────────────────────────────────────────────────────

    private void BuildLookupTables()
    {
        connectionMap = new Dictionary<(string, int, bool), StaircaseConnection>();

        if (staircaseConnections == null) return;

        foreach (var conn in staircaseConnections)
        {
            var key = (conn.stairwellID, conn.fromFloor, conn.goingUp);
            if (connectionMap.ContainsKey(key))
            {
                Debug.LogWarning($"[LoopingStaircase] Duplicate connection: " +
                                 $"stairwell='{conn.stairwellID}' from={conn.fromFloor} " +
                                 $"goingUp={conn.goingUp}. Only first entry used.");
                continue;
            }
            connectionMap[key] = conn;
        }

        // Build spawn-point map
        spawnMap = new Dictionary<(string, int, bool), FloorSpawnPoint>();
        var allSpawns = FindObjectsByType<FloorSpawnPoint>(FindObjectsInactive.Include);

        foreach (var sp in allSpawns)
        {
            var spawnKey = (sp.stairwellID, sp.floorNumber, sp.usedWhenArrivingUp);
            if (!spawnMap.ContainsKey(spawnKey))
                spawnMap[spawnKey] = sp;
            else
                Debug.LogWarning($"[LoopingStaircase] Duplicate FloorSpawnPoint: " +
                                 $"stairwell='{sp.stairwellID}' floor={sp.floorNumber} " +
                                 $"up={sp.usedWhenArrivingUp}", sp);
        }

        Debug.Log($"[LoopingStaircase] Built map: {connectionMap.Count} connections, " +
                  $"{spawnMap.Count} spawn points.");
    }

    private FloorSpawnPoint FindSpawn(string stairwellID, int floor, bool arrivingUp)
    {
        spawnMap.TryGetValue((stairwellID, floor, arrivingUp), out FloorSpawnPoint result);
        return result;
    }

    private void AutoFindReferences()
    {
        if (playerObject == null)
        {
            playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
                Debug.LogError("[LoopingStaircase] Could not find a GameObject tagged 'Player'. " +
                               "Please tag your player or assign it manually.", this);
        }
        if (playerObject != null && playerMovement == null)
            playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null && playerCamera == null)
            playerCamera = playerMovement.playerCamera;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Editor Gizmos
    // ══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (staircaseConnections == null) return;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.2f,
            $"LoopingStaircaseSystem\nConnections: {staircaseConnections.Length}"
        );
    }
#endif
}
