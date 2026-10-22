using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using System.IO;
using System.Text;

public static class FixAllPathingAndSpeed
{
    struct StaffPost
    {
        public string name;
        public Vector3 postPos;
        public Quaternion postRot;
    }

    [MenuItem("Tools/Klirans/Fix All Pathing and Speed")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== FIX ALL PATHING AND SPEED AUDIT ===");

        // ── 1. Fix Staff Proctors ────────────────────────────────────────────────
        // Staff should remain at their assigned desk/window stations, greeting students
        // when they approach, with NO erratic in-room wander or self-parented waypoints.

        StaffPost[] staffPosts = new[]
        {
            new StaffPost { name = "Signatory_Librarian", postPos = new Vector3(-76.0f, 14.30f, 44.15f), postRot = Quaternion.Euler(0f, 270f, 0f) },
            new StaffPost { name = "Signatory_GuidanceCounselor", postPos = new Vector3(-89.5f, 8.35f, 42.20f), postRot = Quaternion.Euler(0f, 90f, 0f) },
            new StaffPost { name = "Signatory_Registrar", postPos = new Vector3(-81.3f, 2.35f, 4.90f), postRot = Quaternion.Euler(0f, 270f, 0f) },
            new StaffPost { name = "Signatory_CCSDean", postPos = new Vector3(-77.8f, 8.35f, -3.60f), postRot = Quaternion.Euler(0f, 180f, 0f) },
            new StaffPost { name = "Signatory_Cashier", postPos = new Vector3(-81.3f, 2.35f, -7.10f), postRot = Quaternion.Euler(0f, 270f, 0f) },
            new StaffPost { name = "Signatory_EVP", postPos = new Vector3(-88.5f, 2.35f, 7.80f), postRot = Quaternion.Euler(0f, 180f, 0f) }
        };

        foreach (var sp in staffPosts)
        {
            var go = GameObject.Find(sp.name);
            if (go == null)
            {
                log.AppendLine($"Staff '{sp.name}': NOT FOUND!");
                continue;
            }

            // Remove legacy self-parented waypoints
            Transform wpChild = go.transform.Find("Waypoints");
            if (wpChild != null)
            {
                Object.DestroyImmediate(wpChild.gameObject);
                log.AppendLine($"  Removed self-parented Waypoints from '{sp.name}'.");
            }

            // Remove any NavMeshAgent on stationary staff
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                Object.DestroyImmediate(agent);
                log.AppendLine($"  Removed NavMeshAgent from stationary staff '{sp.name}'.");
            }

            // Reset transform to exact post
            go.transform.position = sp.postPos;
            go.transform.rotation = sp.postRot;

            var staffAI = go.GetComponent<RoomStaffAI>();
            if (staffAI != null)
            {
                staffAI.awarenessRadius = 3.5f;
                staffAI.facePlayerTurnSpeed = 3.5f;
                staffAI.ResetHomeTransform();
            }

            // Reset animator speed parameter to 0 (Idle)
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Talking", false);
            }

            EditorUtility.SetDirty(go);
            log.AppendLine($"Staff '{sp.name}': anchored to station at {sp.postPos}, rot={sp.postRot.eulerAngles.y} deg.");
        }

        // ── 2. Fix Wandering Student NPCs ─────────────────────────────────────────
        string[] studentNames = new[]
        {
            "ClearanceNPC_Drei", "ClearanceNPC_Glad", "ClearanceNPC_Ira",
            "ClearanceNPC_Jessa", "ClearanceNPC_Josua", "ClearanceNPC_Niel"
        };

        foreach (var name in studentNames)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                log.AppendLine($"Student '{name}': NOT FOUND!");
                continue;
            }

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();

            // Configure smooth, natural walking agent
            agent.speed = 1.15f;           // Realistic walking speed (not sprinting)
            agent.angularSpeed = 480.0f;   // Fast, clean turning (eliminates wide circular orbits)
            agent.acceleration = 10.0f;    // Smooth acceleration
            agent.stoppingDistance = 0.25f;// Stops well within waypointTolerance
            agent.radius = 0.28f;
            agent.height = 1.6f;
            agent.autoBraking = true;
            agent.updateRotation = true;
            agent.autoTraverseOffMeshLink = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = 50;

            var ai = go.GetComponent<ProctorAI>();
            if (ai != null)
            {
                ai.walkSpeed = 1.15f;
                ai.waypointTolerance = 1.1f; // Large enough arrival bubble to cleanly trigger
                ai.minIdleTime = 1.5f;
                ai.maxIdleTime = 3.5f;
                ai.stareChance = 0.35f;
                ai.maxHeadTurnAngle = 60.0f;
                ai.headTurnSpeed = 3.0f;

                // Snap all waypoints to NavMesh
                if (ai.waypoints != null)
                {
                    for (int i = 0; i < ai.waypoints.Length; i++)
                    {
                        var wp = ai.waypoints[i];
                        if (wp != null)
                        {
                            NavMeshHit hit;
                            if (NavMesh.SamplePosition(wp.position, out hit, 3.0f, NavMesh.AllAreas))
                            {
                                wp.position = hit.position;
                            }
                        }
                    }
                }
            }

            // Snap NPC position to NavMesh
            NavMeshHit startHit;
            if (NavMesh.SamplePosition(go.transform.position, out startHit, 3.0f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                go.transform.position = startHit.position;
                agent.enabled = true;
                agent.Warp(startHit.position);
            }

            EditorUtility.SetDirty(go);
            log.AppendLine($"Student '{name}': Agent configured (speed=1.15, angularSpeed=480, stoppingDist=0.25, tol=1.1). Snapped to NavMesh at {go.transform.position}.");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        File.WriteAllText("Assets/PathingFixOutput.txt", log.ToString());
        Debug.Log(log.ToString());
    }
}
