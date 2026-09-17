using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.IO;
using System.Text;

public static class InspectPathing
{
    [MenuItem("Tools/Klirans/Inspect Pathing")]
    public static void Run()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== PATHING & WAYPOINT AUDIT ===");

        string[] studentNames = new[]
        {
            "ClearanceNPC_Drei", "ClearanceNPC_Glad", "ClearanceNPC_Ira",
            "ClearanceNPC_Jessa", "ClearanceNPC_Josua", "ClearanceNPC_Niel"
        };

        foreach (var name in studentNames)
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.AppendLine($"Student '{name}': NOT FOUND"); continue; }

            var agent = go.GetComponent<NavMeshAgent>();
            var ai = go.GetComponent<ProctorAI>();

            sb.AppendLine($"\n[Student NPC] {name}:");
            sb.AppendLine($"  Position: {go.transform.position}");
            if (agent != null)
            {
                sb.AppendLine($"  Agent: speed={agent.speed}, angularSpeed={agent.angularSpeed}, accel={agent.acceleration}, stoppingDist={agent.stoppingDistance}, radius={agent.radius}");
            }
            if (ai != null)
            {
                sb.AppendLine($"  ProctorAI: walkSpeed={ai.walkSpeed}, tolerance={ai.waypointTolerance}, loopInOrder={ai.loopInOrder}");
                sb.AppendLine($"  Waypoints Count: {ai.waypoints?.Length ?? 0}");
                if (ai.waypoints != null)
                {
                    for (int i = 0; i < ai.waypoints.Length; i++)
                    {
                        var wp = ai.waypoints[i];
                        if (wp == null) { sb.AppendLine($"    WP[{i}]: NULL"); continue; }
                        NavMeshHit hit;
                        bool onNav = NavMesh.SamplePosition(wp.position, out hit, 1.0f, NavMesh.AllAreas);
                        sb.AppendLine($"    WP[{i}]: '{wp.name}' at {wp.position} (OnNavMesh={onNav}, distToNav={Vector3.Distance(wp.position, hit.position):F2})");
                    }
                }
            }
        }

        string[] staffNames = new[]
        {
            "Signatory_Librarian", "Signatory_GuidanceCounselor", "Signatory_Registrar",
            "Signatory_CCSDean", "Signatory_Cashier", "Signatory_EVP"
        };

        foreach (var name in staffNames)
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.AppendLine($"Staff '{name}': NOT FOUND"); continue; }

            var staffAI = go.GetComponent<RoomStaffAI>();
            var agent = go.GetComponent<NavMeshAgent>();

            sb.AppendLine($"\n[Staff Member] {name}:");
            sb.AppendLine($"  Position: {go.transform.position}, Rotation: {go.transform.eulerAngles}");
            sb.AppendLine($"  Has NavMeshAgent: {agent != null}");
            if (staffAI != null)
            {
                sb.AppendLine($"  StaffAI: role='{staffAI.staffRole}', awarenessRadius={staffAI.awarenessRadius}, moveSpeed={staffAI.moveSpeed}");
                sb.AppendLine($"  Waypoints Count: {staffAI.waypoints?.Length ?? 0}");
                if (staffAI.waypoints != null)
                {
                    for (int i = 0; i < staffAI.waypoints.Length; i++)
                    {
                        var wp = staffAI.waypoints[i];
                        if (wp == null) continue;
                        sb.AppendLine($"    WP[{i}]: '{wp.name}' at {wp.position}, Parent: '{wp.parent?.name}'");
                    }
                }
            }
        }

        File.WriteAllText("Assets/PathingAudit.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
