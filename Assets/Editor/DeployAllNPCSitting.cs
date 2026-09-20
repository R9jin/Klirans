using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeployAllNPCSitting
{
    struct SeatedConfig
    {
        public string npcName;
        public string chairName;
        public Vector3 seatedPos;
        public Quaternion seatedRot;
        public Vector3 standingPos;
        public Quaternion standingRot;
        public float hipsDrop;
    }

    [MenuItem("Tools/Klirans/Deploy All NPC Sitting and Stand-on-Interact")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== DEPLOY ALL NPC SITTING & STAND-ON-INTERACTION AUDIT ===");

        SeatedConfig[] configs = new[]
        {
            // 1. Room 207 - Guidance Counselor (2F)
            new SeatedConfig
            {
                npcName = "Signatory_GuidanceCounselor",
                chairName = "StaffChair_2",
                seatedPos = new Vector3(-89.50f, 8.35f, 42.20f),
                seatedRot = Quaternion.Euler(0f, 90f, 0f),
                standingPos = new Vector3(-89.15f, 8.35f, 42.20f),
                standingRot = Quaternion.Euler(0f, 90f, 0f),
                hipsDrop = -0.0115f
            },
            // 2. Room 102 - University Cashier (1F)
            new SeatedConfig
            {
                npcName = "Signatory_Cashier",
                chairName = "StaffChair",
                seatedPos = new Vector3(-81.75f, 2.35f, -7.14f),
                seatedRot = Quaternion.Euler(0f, 270f, 0f),
                standingPos = new Vector3(-81.30f, 2.35f, -7.10f),
                standingRot = Quaternion.Euler(0f, 270f, 0f),
                hipsDrop = -0.0115f
            },
            // 3. Room 104 - University Registrar (1F)
            new SeatedConfig
            {
                npcName = "Signatory_Registrar",
                chairName = "StaffChair",
                seatedPos = new Vector3(-81.75f, 2.35f, 4.86f),
                seatedRot = Quaternion.Euler(0f, 270f, 0f),
                standingPos = new Vector3(-81.30f, 2.35f, 4.90f),
                standingRot = Quaternion.Euler(0f, 270f, 0f),
                hipsDrop = -0.0115f
            },
            // 4. Room 308 - Head Librarian (3F)
            new SeatedConfig
            {
                npcName = "Signatory_Librarian",
                chairName = "LibrarianChair",
                seatedPos = new Vector3(-76.15f, 14.30f, 44.15f),
                seatedRot = Quaternion.Euler(0f, 270f, 0f),
                standingPos = new Vector3(-76.60f, 14.30f, 44.15f),
                standingRot = Quaternion.Euler(0f, 270f, 0f),
                hipsDrop = -0.0115f
            },
            // 5. Room 202 - CCS Dean (2F)
            new SeatedConfig
            {
                npcName = "Signatory_CCSDean",
                chairName = "AdminChair",
                seatedPos = new Vector3(-77.80f, 8.35f, -3.75f),
                seatedRot = Quaternion.Euler(0f, 180f, 0f),
                standingPos = new Vector3(-77.80f, 8.35f, -4.10f),
                standingRot = Quaternion.Euler(0f, 180f, 0f),
                hipsDrop = -0.0115f
            },
            // 6. Room 103 - Executive Vice President (1F)
            new SeatedConfig
            {
                npcName = "Signatory_EVP",
                chairName = "VP_ExecutiveChair",
                seatedPos = new Vector3(-88.50f, 2.35f, 7.75f),
                seatedRot = Quaternion.Euler(0f, 180f, 0f),
                standingPos = new Vector3(-88.50f, 2.35f, 7.40f),
                standingRot = Quaternion.Euler(0f, 180f, 0f),
                hipsDrop = -0.0115f
            },
            // 7. Main Lobby - Campus Security Guard
            new SeatedConfig
            {
                npcName = "Security_Guard_NPC",
                chairName = "Chair_Seat",
                seatedPos = new Vector3(-75.75f, 2.36f, 15.95f),
                seatedRot = Quaternion.Euler(0f, 270f, 0f),
                standingPos = new Vector3(-76.15f, 2.36f, 15.95f),
                standingRot = Quaternion.Euler(0f, 270f, 0f),
                hipsDrop = -0.0115f
            }
        };

        foreach (var cfg in configs)
        {
            GameObject npcGO = GameObject.Find(cfg.npcName);
            if (npcGO == null)
            {
                log.AppendLine("ERROR: NPC not found: " + cfg.npcName);
                continue;
            }

            // Find closest chair to seated position
            var allChairs = Object.FindObjectsOfType<GameObject>();
            GameObject chairGO = null;
            float minD = 5.0f;
            foreach (var c in allChairs)
            {
                string l = c.name.ToLower();
                if (c.name.Equals(cfg.chairName, System.StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("chair") || l.Contains("seat") || l.Contains("stool"))
                {
                    // Ignore parent compound roots if checking sub-mesh, or ignore tiny sub-colliders
                    float d = Vector3.Distance(cfg.seatedPos, c.transform.position);
                    if (d < minD)
                    {
                        minD = d;
                        chairGO = c;
                    }
                }
            }

            // Align chair orientation to match seated rotation
            if (chairGO != null)
            {
                chairGO.transform.rotation = cfg.seatedRot;
                EditorUtility.SetDirty(chairGO);
            }

            // Configure NPCSitController
            var sitCtrl = npcGO.GetComponent<NPCSitController>();
            if (sitCtrl == null) sitCtrl = npcGO.AddComponent<NPCSitController>();
            sitCtrl.hipsDropOffset = cfg.hipsDrop;
            sitCtrl.thighPitchAngle = 78.0f;
            sitCtrl.kneePitchAngle = -85.0f;
            sitCtrl.footPitchAngle = 10.0f;
            sitCtrl.armPitchAngle = 40.0f;
            sitCtrl.standUpDuration = 0.45f;
            sitCtrl.sitDownDuration = 0.60f;
            sitCtrl.standForwardDistance = Vector3.Distance(cfg.seatedPos, cfg.standingPos);

            sitCtrl.enableHeadTracking = true;
            sitCtrl.ConfigureChairAnchor(chairGO, cfg.seatedPos, cfg.seatedRot, cfg.standingPos, cfg.standingRot);

            // Rebind animator so bone transforms in scene are pristine standing pose
            var anim = npcGO.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
            }

            // Special setup for Security Guard
            if (cfg.npcName == "Security_Guard_NPC")
            {
                var guardInteract = npcGO.GetComponent<SecurityGuardInteract>();
                if (guardInteract == null) guardInteract = npcGO.AddComponent<SecurityGuardInteract>();

                var rb = npcGO.GetComponent<Rigidbody>();
                if (rb == null) rb = npcGO.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                npcGO.layer = 0; // Default layer for PlayerInteract raycasting
            }

            EditorUtility.SetDirty(npcGO);
            log.AppendLine($"Deployed NPCSitController on {cfg.npcName}: Seated at {cfg.seatedPos} (rot={cfg.seatedRot.eulerAngles}), Standing at {cfg.standingPos}");
        }

        // Verify that roaming student proctors do NOT have chairs
        string[] roamers = new[]
        {
            "ClearanceNPC_Drei", "ClearanceNPC_Glad", "ClearanceNPC_Ira",
            "ClearanceNPC_Jessa", "ClearanceNPC_Josua", "ClearanceNPC_Niel"
        };
        foreach (var rName in roamers)
        {
            var rGO = GameObject.Find(rName);
            if (rGO != null)
            {
                var sit = rGO.GetComponent<NPCSitController>();
                if (sit != null) Object.DestroyImmediate(sit);
                log.AppendLine($"Verified roamer '{rName}' is walking/patrolling only (NPCSitController excluded).");
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        File.WriteAllText("Assets/NPCSittingDeployAudit.txt", log.ToString());
        Debug.Log(log.ToString());
    }
}
