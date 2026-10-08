using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class AuditSceneCollidersAndBugs
{
    static AuditSceneCollidersAndBugs()
    {
        EditorApplication.delayCall += RunAudit;
    }

    [MenuItem("Tools/Klirans/Audit Scene Colliders and Bugs")]
    public static void RunAudit()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== SCENE COLLIDERS AND BUGS AUDIT ===");
        report.AppendLine($"Time: {DateTime.Now}");
        report.AppendLine($"Scene: {scene.name} ({scene.path})");
        report.AppendLine();

        // 1. Audit Missing Scripts / Corrupt Components
        report.AppendLine("--- 1. MISSING SCRIPTS / MONOBEHAVIOUR AUDIT ---");
        int missingScriptCount = 0;
        var allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allGOs)
        {
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missingScriptCount++;
                    report.AppendLine($"  [MISSING SCRIPT] GameObject '{GetHierarchyPath(go)}' has null component at index {i}!");
                }
            }
        }
        report.AppendLine($"Total missing script components found: {missingScriptCount}");
        report.AppendLine();

        // 2. Audit Mesh Renderers Without Colliders
        report.AppendLine("--- 2. MESH RENDERERS WITHOUT COLLIDERS AUDIT ---");
        var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        report.AppendLine($"Total MeshRenderers in scene: {renderers.Length}");

        List<string> obstacleCandidates = new List<string>();
        List<string> triggerOnlyObstacles = new List<string>();

        string[] obstacleKeywords = new[]
        {
            "desk", "chair", "table", "cabinet", "locker", "shelf", "bookshelf", "rack", "bookcase",
            "printer", "fan", "vending", "cart", "cooler", "dispenser", "bench", "podium", "sofa",
            "wall", "door", "railing", "pillar", "column", "fence", "gate", "stair", "counter",
            "barrier", "trash", "bin", "filing", "cupboard", "board", "blackboard", "whiteboard"
        };

        foreach (var mr in renderers)
        {
            if (mr == null || !mr.gameObject.activeInHierarchy) continue;

            // Check if bounds are significant (> 0.15m size)
            Bounds b = mr.bounds;
            if (b.size.magnitude < 0.15f) continue;

            string lowerName = mr.gameObject.name.ToLower();
            string pathLower = GetHierarchyPath(mr.gameObject).ToLower();

            // Skip ceiling lights, ceiling meshes, roofs, sky
            if (lowerName.Contains("light") || lowerName.Contains("ceiling") || lowerName.Contains("roof") || lowerName.Contains("skybox"))
                continue;

            // Check colliders on this object or its ancestors/children
            Collider col = mr.GetComponent<Collider>();
            if (col == null)
            {
                col = mr.GetComponentInParent<Collider>();
            }
            if (col == null)
            {
                col = mr.GetComponentInChildren<Collider>();
            }

            bool matchesObstacle = false;
            foreach (var kw in obstacleKeywords)
            {
                if (lowerName.Contains(kw) || pathLower.Contains(kw))
                {
                    matchesObstacle = true;
                    break;
                }
            }

            if (col == null)
            {
                if (matchesObstacle)
                {
                    obstacleCandidates.Add($"  [NO COLLIDER] '{GetHierarchyPath(mr.gameObject)}' | bounds: center={b.center}, size={b.size}");
                }
            }
            else
            {
                if (col.isTrigger && matchesObstacle)
                {
                    var allCols = mr.GetComponentsInParent<Collider>();
                    bool hasSolid = false;
                    foreach (var c in allCols)
                    {
                        if (!c.isTrigger && c.enabled) { hasSolid = true; break; }
                    }
                    if (!hasSolid)
                    {
                        triggerOnlyObstacles.Add($"  [TRIGGER ONLY (NO SOLID)] '{GetHierarchyPath(mr.gameObject)}' | Collider: {col.GetType().Name} (isTrigger=true)");
                    }
                }
                else if (!col.enabled && matchesObstacle)
                {
                    obstacleCandidates.Add($"  [DISABLED COLLIDER] '{GetHierarchyPath(mr.gameObject)}' | Collider: {col.GetType().Name} (enabled=false)");
                }
            }
        }

        report.AppendLine($"Obstacle meshes with NO COLLIDER ({obstacleCandidates.Count}):");
        foreach (var s in obstacleCandidates) report.AppendLine(s);
        report.AppendLine();

        report.AppendLine($"Obstacle meshes with TRIGGER ONLY ({triggerOnlyObstacles.Count}):");
        foreach (var s in triggerOnlyObstacles) report.AppendLine(s);
        report.AppendLine();

        // 3. Audit Player Setup & Layers
        report.AppendLine("--- 3. PLAYER SETUP & PHYSICS AUDIT ---");
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            report.AppendLine("  [CRITICAL BUG] No GameObject found with tag 'Player'!");
        }
        else
        {
            report.AppendLine($"  Player found: '{playerGO.name}' on Layer {playerGO.layer} ({LayerMask.LayerToName(playerGO.layer)})");
            var cc = playerGO.GetComponent<CharacterController>();
            if (cc != null)
            {
                report.AppendLine($"  CharacterController: height={cc.height}, radius={cc.radius}, center={cc.center}, slopeLimit={cc.slopeLimit}, stepOffset={cc.stepOffset}, skinWidth={cc.skinWidth}");
            }
            else
            {
                report.AppendLine("  [CRITICAL BUG] Player has no CharacterController!");
            }

            var pm = playerGO.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                report.AppendLine($"  PlayerMovement: walkSpeed={pm.walkSpeed}, runSpeed={pm.runSpeed}, crouchSpeed={pm.crouchSpeed}");
            }

            var obs = playerGO.GetComponent<NavMeshObstacle>();
            report.AppendLine($"  Player NavMeshObstacle: {(obs != null ? "Present (carving=" + obs.carving + ")" : "MISSING")}");
        }
        report.AppendLine();

        // 4. Audit Key Scene Singletons & Game Logic
        report.AppendLine("--- 4. SINGLETONS & MANAGERS AUDIT ---");
        CheckManager<ClearanceManager>(report, "ClearanceManager");
        CheckManager<BlackoutManager>(report, "BlackoutManager");
        CheckManager<TheProctorManager>(report, "TheProctorManager");
        CheckManager<AnxietyManager>(report, "AnxietyManager");
        CheckManager<NPCJumpscareManager>(report, "NPCJumpscareManager");
        CheckManager<CoinManager>(report, "CoinManager");
        CheckManager<FragmentManager>(report, "FragmentManager");
        CheckManager<SafeZoneManager>(report, "SafeZoneManager");
        CheckManager<LockerHideManager>(report, "LockerHideManager");
        CheckManager<HamAndCheeseVendorManager>(report, "HamAndCheeseVendorManager");
        report.AppendLine();

        // 5. Audit Clearance Signatories
        report.AppendLine("--- 5. CLEARANCE SIGNATORIES AUDIT ---");
        var signatories = UnityEngine.Object.FindObjectsByType<ClearanceNPC>(FindObjectsInactive.Include);
        report.AppendLine($"Total ClearanceNPC signatories: {signatories.Length} (expected: 6)");
        foreach (var sig in signatories)
        {
            var col = sig.GetComponent<Collider>();
            var colChild = sig.GetComponentInChildren<Collider>();
            report.AppendLine($"  {sig.name} (signatureIndex={sig.signatureIndex}): pos={sig.transform.position}, collider={(col != null || colChild != null)}");
        }
        report.AppendLine();

        // 6. Audit Wandering NPCs (ProctorAI)
        report.AppendLine("--- 6. WANDERING PROCTOR NPCS AUDIT ---");
        var proctorAIs = UnityEngine.Object.FindObjectsByType<ProctorAI>(FindObjectsInactive.Include);
        report.AppendLine($"Total ProctorAI wandering NPCs: {proctorAIs.Length}");
        foreach (var ai in proctorAIs)
        {
            var agent = ai.GetComponent<NavMeshAgent>();
            var col = ai.GetComponent<Collider>();
            var smr = ai.GetComponentInChildren<SkinnedMeshRenderer>();
            report.AppendLine($"  {ai.name}: pos={ai.transform.position}, Agent={(agent != null && agent.enabled)}, Col={(col != null ? (!col.isTrigger ? "SOLID" : "TRIGGER") : "NONE")}, SMR={(smr != null ? smr.sharedMesh.name : "NONE")}, WPs={(ai.waypoints != null ? ai.waypoints.Length : 0)}");
        }

        string outPath = Path.Combine(Application.dataPath, "ColliderAndBugAudit.txt");
        File.WriteAllText(outPath, report.ToString());
        Debug.Log($"[AuditSceneCollidersAndBugs] Audit complete! Saved to {outPath}");
    }

    private static void CheckManager<T>(StringBuilder report, string name) where T : MonoBehaviour
    {
        var mgr = UnityEngine.Object.FindAnyObjectByType<T>();
        if (mgr != null)
        {
            report.AppendLine($"  {name}: PRESENT on '{mgr.gameObject.name}' (active={mgr.gameObject.activeInHierarchy})");
        }
        else
        {
            report.AppendLine($"  {name}: [MISSING]");
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
