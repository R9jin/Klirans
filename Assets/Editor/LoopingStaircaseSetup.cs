using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that auto-creates StairTriggerZones and FloorSpawnPoints
/// based on the scene's existing stair geometry (MainStairs, RightStairs, LeftStairs).
///
/// Usage: Menu → Tools → Klirans → Setup Looping Staircase
///
/// This is non-destructive: it will skip creation if a trigger/spawn already exists
/// at that location. Re-running it is safe.
/// </summary>
public class LoopingStaircaseSetup : EditorWindow
{
    // ──────────────────────────────────────────────────────────────────────
    //  Floor Y positions measured from the scene hierarchy
    //  1st Floor hallway Y ≈ -2.70,  2nd ≈ 3.30,  3rd ≈ 9.29
    // ──────────────────────────────────────────────────────────────────────

    private struct FloorInfo
    {
        public int    number;
        public float  hallwayY;       // Y of the hallway floor surface
        public float  spawnOffsetY;   // extra Y offset above hallway Y for spawn point
    }

    private static readonly FloorInfo[] Floors = new FloorInfo[]
    {
        new FloorInfo { number = 1, hallwayY = -2.70f, spawnOffsetY = 1.0f },
        new FloorInfo { number = 2, hallwayY =  3.30f, spawnOffsetY = 1.0f },
        new FloorInfo { number = 3, hallwayY =  9.29f, spawnOffsetY = 1.0f },
    };

    // ──────────────────────────────────────────────────────────────────────
    //  Per-stairwell data: trigger zone positions & spawn positions
    //  These X/Z values are derived from the scene hierarchy data above.
    // ──────────────────────────────────────────────────────────────────────

    private struct StairwellInfo
    {
        public string id;
        // World XZ of the stairwell (center of the trigger zones)
        public float  centerX;
        public float  centerZ;
        // Direction the player faces when arriving on each floor (world Y-axis degrees)
        public float  arrivalYawUp;       // facing after arriving going upward
        public float  arrivalYawDown;     // facing after arriving going downward
        // Half-height of the trigger box (placed at the mid-stair Y)
        public float  triggerHalfHeight;
        public Vector3 triggerSize;
    }

    private static readonly StairwellInfo[] Stairwells = new StairwellInfo[]
    {
        new StairwellInfo
        {
            id               = "MainStairs",
            centerX          = -4.7f,
            centerZ          =  1.95f,
            arrivalYawUp     =  90f,   // Player exits into hallway facing +X direction
            arrivalYawDown   = 270f,
            triggerSize      = new Vector3(3.5f, 1.2f, 4.5f),
        },
        new StairwellInfo
        {
            id               = "RightStairs",
            centerX          = -2.75f,
            centerZ          =  2.05f,
            arrivalYawUp     =  90f,
            arrivalYawDown   = 270f,
            triggerSize      = new Vector3(3.5f, 1.2f, 4.5f),
        },
        new StairwellInfo
        {
            id               = "LeftStairs",
            centerX          = -2.75f,
            centerZ          =  2.05f,
            arrivalYawUp     =  90f,
            arrivalYawDown   = 270f,
            triggerSize      = new Vector3(3.5f, 1.2f, 4.5f),
        },
    };

    // ══════════════════════════════════════════════════════════════════════
    //  Menu Entry
    // ══════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Klirans/Setup Looping Staircase")]
    public static void Run()
    {
        int triggerCount = 0;
        int spawnCount   = 0;

        // ── 1. Ensure LoopingStaircaseSystem exists ────────────────────────
        LoopingStaircaseSystem system = Object.FindAnyObjectByType<LoopingStaircaseSystem>();
        if (system == null)
        {
            GameObject sysGO = new GameObject("LoopingStaircaseManager");
            system = sysGO.AddComponent<LoopingStaircaseSystem>();
            Undo.RegisterCreatedObjectUndo(sysGO, "Create LoopingStaircaseManager");
            Debug.Log("[StaircaseSetup] Created LoopingStaircaseManager.");
        }

        // ── 2. Find / create a parent container for the setup objects ──────
        GameObject container = GameObject.Find("LoopingStaircaseSetup");
        if (container == null)
        {
            container = new GameObject("LoopingStaircaseSetup");
            Undo.RegisterCreatedObjectUndo(container, "Create LoopingStaircaseSetup container");
        }

        // ── 3. Create triggers and spawn points for every stairwell × floor ─
        foreach (var sw in Stairwells)
        {
            // Find the stairwell parent GameObject
            GameObject swParent = GameObject.Find($"Stairs/{sw.id}");
            if (swParent == null)
            {
                Debug.LogWarning($"[StaircaseSetup] Could not find 'Stairs/{sw.id}' in scene. Skipping.");
                continue;
            }

            GameObject swContainer = GetOrCreateChild(container, sw.id);

            for (int fi = 0; fi < Floors.Length; fi++)
            {
                FloorInfo floor = Floors[fi];

                // ── Trigger zone: placed at mid-flight, roughly half-way up each flight ──
                // Mid-flight Y between this floor's hallway and the next floor's hallway
                float triggerY = floor.hallwayY + 3.0f; // ~half-way up a 6-unit flight
                if (fi < Floors.Length - 1)
                    triggerY = (floor.hallwayY + Floors[fi + 1].hallwayY) * 0.5f;

                string triggerName = $"Trigger_{sw.id}_From{floor.number}F";
                if (swContainer.transform.Find(triggerName) == null)
                {
                    GameObject trigGO = new GameObject(triggerName);
                    Undo.RegisterCreatedObjectUndo(trigGO, "Create StairTrigger");
                    trigGO.transform.SetParent(swContainer.transform);
                    trigGO.transform.position = new Vector3(sw.centerX, triggerY, sw.centerZ);

                    BoxCollider col = trigGO.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size      = sw.triggerSize;

                    StairTriggerZone zone = trigGO.AddComponent<StairTriggerZone>();
                    zone.stairwellID = sw.id;
                    zone.fromFloor   = floor.number;

                    triggerCount++;
                }

                // ── Spawn points (one for arriving-up, one for arriving-down) ──────────
                // Arriving-up spawn: player just climbed to this floor → place at TOP of stair landing
                string spawnUpName = $"Spawn_{sw.id}_Floor{floor.number}_ArrivingUp";
                if (swContainer.transform.Find(spawnUpName) == null)
                {
                    Vector3 spawnPos = new Vector3(
                        sw.centerX + 1.5f,              // slightly into the hallway
                        floor.hallwayY + floor.spawnOffsetY,
                        sw.centerZ
                    );

                    // Special case: 1st floor has no downward stair; place near hallway entrance
                    if (floor.number == 1)
                        spawnPos = new Vector3(sw.centerX + 2.0f, floor.hallwayY + floor.spawnOffsetY, sw.centerZ);

                    GameObject spawnGO = new GameObject(spawnUpName);
                    Undo.RegisterCreatedObjectUndo(spawnGO, "Create FloorSpawnPoint");
                    spawnGO.transform.SetParent(swContainer.transform);
                    spawnGO.transform.position = spawnPos;

                    FloorSpawnPoint sp = spawnGO.AddComponent<FloorSpawnPoint>();
                    sp.stairwellID       = sw.id;
                    sp.floorNumber       = floor.number;
                    sp.usedWhenArrivingUp = true;
                    sp.spawnFacingYaw    = sw.arrivalYawUp;

                    spawnCount++;
                }

                // Arriving-down spawn: player descended to this floor
                string spawnDownName = $"Spawn_{sw.id}_Floor{floor.number}_ArrivingDown";
                if (swContainer.transform.Find(spawnDownName) == null)
                {
                    Vector3 spawnPos = new Vector3(
                        sw.centerX + 1.5f,
                        floor.hallwayY + floor.spawnOffsetY,
                        sw.centerZ
                    );

                    if (floor.number == 1)
                        spawnPos = new Vector3(sw.centerX + 2.0f, floor.hallwayY + floor.spawnOffsetY, sw.centerZ);

                    GameObject spawnGO = new GameObject(spawnDownName);
                    Undo.RegisterCreatedObjectUndo(spawnGO, "Create FloorSpawnPoint");
                    spawnGO.transform.SetParent(swContainer.transform);
                    spawnGO.transform.position = spawnPos;

                    FloorSpawnPoint sp = spawnGO.AddComponent<FloorSpawnPoint>();
                    sp.stairwellID        = sw.id;
                    sp.floorNumber        = floor.number;
                    sp.usedWhenArrivingUp  = false;
                    sp.spawnFacingYaw     = sw.arrivalYawDown;

                    spawnCount++;
                }
            }
        }

        // ── 4. Ensure Player is tagged ─────────────────────────────────────
        GameObject player = GameObject.Find("Player");
        if (player != null && !player.CompareTag("Player"))
        {
            Undo.RecordObject(player, "Set Player Tag");
            player.tag = "Player";
            Debug.Log("[StaircaseSetup] Tagged 'Player' GameObject as 'Player'.");
        }

        // ── 5. Mark scene dirty ────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        );

        Debug.Log(
            $"[StaircaseSetup] Done! Created {triggerCount} trigger(s) and {spawnCount} spawn point(s). Scene marked dirty."
        );
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) return t.gameObject;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform);
        return go;
    }
}
