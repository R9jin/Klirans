using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupRoomStaff
{
    struct SignatoryConfig
    {
        public string goName;
        public string roleTitle;
        public int signatureIndex;
        public string roomPath;
        public Vector3 position;
        public Quaternion rotation;
        public float interactionRange;
        public string unsignedDialogue;
        public string alreadySignedDialogue;
        public string notYourTurnDialogue;
        public string fbxPath;
        public Vector3[] waypoints;
    }

    [MenuItem("Tools/Deploy Room Staff and Fix Spawn")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== ROOM STAFF & SPAWN POINT DEPLOYMENT AUDIT ===");

        // ── 1. Fix Player Spawn Point ──────────────────────────────────────────────
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("Player");

        Vector3 spawnPos = new Vector3(-91.0f, 2.35f, 16.65f);
        Quaternion spawnRot = Quaternion.Euler(0f, 90f, 0f);

        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPos;
            player.transform.rotation = spawnRot;

            if (cc != null) cc.enabled = true;
            log.AppendLine($"Player repositioned to Main Lobby Entrance: {spawnPos}, facing East.");
        }
        else
        {
            log.AppendLine("WARNING: Player GameObject not found in scene!");
        }

        // Ensure permanent PlayerSpawnPoint marker exists
        GameObject spawnMarker = GameObject.Find("PlayerSpawnPoint");
        if (spawnMarker == null)
        {
            spawnMarker = new GameObject("PlayerSpawnPoint");
            GameObject lobby = GameObject.Find("LobbyArea");
            if (lobby != null) spawnMarker.transform.SetParent(lobby.transform, false);
        }
        spawnMarker.transform.position = spawnPos;
        spawnMarker.transform.rotation = spawnRot;
        spawnMarker.tag = "Respawn";
        log.AppendLine($"PlayerSpawnPoint marker established at {spawnPos}.");

        // ── 2. Repurpose Student NPCs (Remove ClearanceNPC from them) ─────────────
        string[] studentNames = new[]
        {
            "ClearanceNPC_Drei", "ClearanceNPC_Glad", "ClearanceNPC_Ira",
            "ClearanceNPC_Jessa", "ClearanceNPC_Josua", "ClearanceNPC_Niel"
        };

        foreach (string sName in studentNames)
        {
            GameObject sGO = GameObject.Find(sName);
            if (sGO != null)
            {
                var cNPC = sGO.GetComponent<ClearanceNPC>();
                if (cNPC != null)
                {
                    Object.DestroyImmediate(cNPC);
                    log.AppendLine($"Removed ClearanceNPC from student '{sName}' (now ambient roamer).");
                }
            }
        }

        // ── 3. Load Blank_Paper Item Data ─────────────────────────────────────────
        InventoryItem blankPaper = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");

        // ── 4. Define 6 Faculty Signatories ───────────────────────────────────────
        SignatoryConfig[] staffList = new[]
        {
            new SignatoryConfig
            {
                goName = "Signatory_Librarian",
                roleTitle = "Head Librarian",
                signatureIndex = 0,
                roomPath = "Rooms/3rdFloor/Room 308",
                position = new Vector3(-76.0f, 14.30f, 44.15f),
                rotation = Quaternion.Euler(0f, -90f, 0f),
                interactionRange = 2.8f,
                unsignedDialogue = "Keep your voice down, this is the university library. Let me see your clearance slip... Very well, the library records are cleared. Next, report to Guidance and Counseling in Room 207 on the 2nd floor.",
                alreadySignedDialogue = "You are already cleared here. Head to Guidance in Room 207 on the 2nd floor.",
                notYourTurnDialogue = "You must present an official clearance slip first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/old_prof3/IdlePhil.fbx",
                waypoints = new[]
                {
                    new Vector3(-76.0f, 14.30f, 44.15f),
                    new Vector3(-78.5f, 14.30f, 44.15f),
                    new Vector3(-78.5f, 14.30f, 41.50f)
                }
            },
            new SignatoryConfig
            {
                goName = "Signatory_GuidanceCounselor",
                roleTitle = "Guidance Counselor",
                signatureIndex = 1,
                roomPath = "Rooms/2ndFloor/Room 207",
                position = new Vector3(-89.5f, 8.35f, 42.20f),
                rotation = Quaternion.Euler(0f, 90f, 0f),
                interactionRange = 2.8f,
                unsignedDialogue = "Welcome to Guidance. Have you reflected on your conduct this semester? Your behavioral records look satisfactory. Guidance is cleared. Proceed to the Registrar in Room 104 on the ground floor.",
                alreadySignedDialogue = "Your guidance records are cleared. Go to the Registrar in Room 104 on the 1st floor.",
                notYourTurnDialogue = "I cannot sign until the Library in Room 308 has signed your clearance slip first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/rachel/IdleRachel.fbx",
                waypoints = new[]
                {
                    new Vector3(-89.5f, 8.35f, 42.20f),
                    new Vector3(-88.0f, 8.35f, 42.20f),
                    new Vector3(-89.5f, 8.35f, 39.50f)
                }
            },
            new SignatoryConfig
            {
                goName = "Signatory_Registrar",
                roleTitle = "University Registrar",
                signatureIndex = 2,
                roomPath = "Rooms/1stFloor/Room 104",
                position = new Vector3(-81.7f, 2.35f, 4.90f),
                rotation = Quaternion.Euler(0f, 90f, 0f),
                interactionRange = 3.5f, // Extended so player can interact through the glass reception window from hallway
                unsignedDialogue = "Window 2, Evaluation and Records. Let me inspect your credentials... Grades verified, no incomplete deficiencies. Now head up to Room 202 on the 2nd floor for the College of Computing Studies.",
                alreadySignedDialogue = "The Registrar's office has already signed. Go to Room 202 on the 2nd floor.",
                notYourTurnDialogue = "You are missing the Guidance clearance. Return to Room 207 on the 2nd floor first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/old_prof/IdleProf1.fbx",
                waypoints = new[]
                {
                    new Vector3(-81.7f, 2.35f, 4.90f),
                    new Vector3(-81.7f, 2.35f, 6.70f),
                    new Vector3(-84.0f, 2.35f, 4.90f)
                }
            },
            new SignatoryConfig
            {
                goName = "Signatory_CCSDean",
                roleTitle = "CCS Dean / Dept Head",
                signatureIndex = 3,
                roomPath = "Rooms/2ndFloor/Room 202",
                position = new Vector3(-77.8f, 8.35f, -3.60f),
                rotation = Quaternion.Euler(0f, 180f, 0f),
                interactionRange = 2.8f,
                unsignedDialogue = "College of Computing Studies clearance. Let's see your curriculum checklist... Approved. Take this down to the University Cashier in Room 102.",
                alreadySignedDialogue = "CCS clearance is completed. Go to the Cashier in Room 102 on the 1st floor.",
                notYourTurnDialogue = "The Registrar must verify your units before the Dean can sign. Check Room 104 first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/renz/IdleRenz.fbx",
                waypoints = new[]
                {
                    new Vector3(-77.8f, 8.35f, -3.60f),
                    new Vector3(-79.5f, 8.35f, -5.50f),
                    new Vector3(-77.8f, 8.35f, -7.50f)
                }
            },
            new SignatoryConfig
            {
                goName = "Signatory_Cashier",
                roleTitle = "University Cashier",
                signatureIndex = 4,
                roomPath = "Rooms/1stFloor/Room 102",
                position = new Vector3(-81.7f, 2.35f, -7.10f),
                rotation = Quaternion.Euler(0f, 90f, 0f),
                interactionRange = 3.5f, // Extended for reception service window counter
                unsignedDialogue = "Window 2, Cashier Department. Checking your assessment and fees... Zero balance, payment settled. You're almost done. Final sign-off is at the Executive Office in Room 103.",
                alreadySignedDialogue = "Your fees are cleared. Head to Room 103 for the final Executive clearance.",
                notYourTurnDialogue = "Assessment not verified. You need your College Dean's signature from Room 202 first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/old_prof2/IdleProf2.fbx",
                waypoints = new[]
                {
                    new Vector3(-81.7f, 2.35f, -7.10f),
                    new Vector3(-81.7f, 2.35f, -8.90f),
                    new Vector3(-84.0f, 2.35f, -7.10f)
                }
            },
            new SignatoryConfig
            {
                goName = "Signatory_EVP",
                roleTitle = "Executive Vice President",
                signatureIndex = 5,
                roomPath = "Rooms/1stFloor/Room 103",
                position = new Vector3(-88.5f, 2.35f, 7.80f),
                rotation = Quaternion.Euler(0f, 180f, 0f),
                interactionRange = 2.8f,
                unsignedDialogue = "All prerequisite departments have signed... Outstanding. By the authority of the Executive Office, you are hereby CLEARED. May you finally find your exit.",
                alreadySignedDialogue = "You are fully cleared. The campus can no longer hold you.",
                notYourTurnDialogue = "This is the final clearance office. Settle your obligations with the Cashier in Room 102 first.",
                fbxPath = "Assets/NPC Assets/Animations/animateds/old_prof/IdleProf1.fbx",
                waypoints = new[]
                {
                    new Vector3(-88.5f, 2.35f, 7.80f),
                    new Vector3(-88.5f, 2.35f, 4.50f),
                    new Vector3(-86.5f, 2.35f, 7.80f)
                }
            }
        };

        // ── 5. Instantiate or Update Each Signatory ───────────────────────────────
        foreach (var cfg in staffList)
        {
            GameObject roomGO = GameObject.Find(cfg.roomPath);
            if (roomGO == null)
            {
                log.AppendLine($"ERROR: Room path '{cfg.roomPath}' not found for {cfg.goName}!");
                continue;
            }

            GameObject staffGO = GameObject.Find(cfg.goName);
            if (staffGO == null)
            {
                staffGO = new GameObject(cfg.goName);
                staffGO.transform.SetParent(roomGO.transform, true);
            }

            staffGO.transform.position = cfg.position;
            staffGO.transform.rotation = cfg.rotation;

            // Physical Capsule Collider
            var col = staffGO.GetComponent<CapsuleCollider>();
            if (col == null) col = staffGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.86f, 0f);
            col.radius = 0.32f;
            col.height = 1.72f;
            col.isTrigger = false;

            // Rigidbody
            var rb = staffGO.GetComponent<Rigidbody>();
            if (rb == null) rb = staffGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // ClearanceNPC Component
            var cNPC = staffGO.GetComponent<ClearanceNPC>();
            if (cNPC == null) cNPC = staffGO.AddComponent<ClearanceNPC>();
            cNPC.npcName = cfg.roleTitle;
            cNPC.signatureIndex = cfg.signatureIndex;
            cNPC.blankPaperItem = blankPaper;
            cNPC.interactionRange = cfg.interactionRange;
            cNPC.unsignedDialogue = cfg.unsignedDialogue;
            cNPC.alreadySignedDialogue = cfg.alreadySignedDialogue;
            cNPC.notYourTurnDialogue = cfg.notYourTurnDialogue;

            // RoomStaffAI Component
            var staffAI = staffGO.GetComponent<RoomStaffAI>();
            if (staffAI == null) staffAI = staffGO.AddComponent<RoomStaffAI>();
            staffAI.staffRole = cfg.roleTitle;
            staffAI.awarenessRadius = 4.0f;

            // In-room waypoints
            List<Transform> wpList = new List<Transform>();
            Transform wpParent = staffGO.transform.Find("Waypoints");
            if (wpParent == null)
            {
                var wpPGO = new GameObject("Waypoints");
                wpPGO.transform.SetParent(staffGO.transform, false);
                wpParent = wpPGO.transform;
            }

            for (int i = 0; i < cfg.waypoints.Length; i++)
            {
                string wpName = $"WP_{i}";
                Transform wpT = wpParent.Find(wpName);
                if (wpT == null)
                {
                    var wpGO = new GameObject(wpName);
                    wpGO.transform.SetParent(wpParent, false);
                    wpT = wpGO.transform;
                }
                wpT.position = cfg.waypoints[i];
                wpList.Add(wpT);
            }
            staffAI.waypoints = wpList.ToArray();

            // Visual Model Attachment
            Transform modelChild = staffGO.transform.Find("VisualModel");
            if (modelChild == null)
            {
                GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.fbxPath);
                if (fbxPrefab != null)
                {
                    GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                    modelInstance.name = "VisualModel";
                    modelInstance.transform.SetParent(staffGO.transform, false);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;

                    // Ensure scale matches humanoid height (~1.72m)
                    // If FBX is 1:1, scale is 1. If Mixamo cm, scale is ~37 or 1 depending on importer.
                    // Check bounds to automatically normalize scale:
                    var renderers = modelInstance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds b = renderers[0].bounds;
                        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
                        float currentH = b.size.y;
                        if (currentH > 0.05f && currentH < 0.5f)
                        {
                            // Model is around 0.04m, needs scale ~37
                            modelInstance.transform.localScale = Vector3.one * 37.0f;
                        }
                        else if (currentH > 50f)
                        {
                            modelInstance.transform.localScale = Vector3.one * 0.01f;
                        }
                        else
                        {
                            modelInstance.transform.localScale = Vector3.one;
                        }
                    }
                    modelChild = modelInstance.transform;
                    log.AppendLine($"Attached visual model '{cfg.fbxPath}' to {cfg.goName}.");
                }
                else
                {
                    log.AppendLine($"WARNING: FBX not found at '{cfg.fbxPath}' for {cfg.goName}!");
                }
            }

            log.AppendLine($"Deployed {cfg.goName} ({cfg.roleTitle}) in {cfg.roomPath} at {cfg.position} (Slot: signed{cfg.signatureIndex + 1}).");
        }

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        log.AppendLine("Scene marked dirty and successfully saved to disk!");

        string outPath = Path.Combine(Application.dataPath, "EditorOutput.txt");
        File.WriteAllText(outPath, log.ToString());
        Debug.Log("=== SETUP COMPLETE ===");
    }
}
