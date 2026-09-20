using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class InspectSceneNPCsAndChairs
{
    static InspectSceneNPCsAndChairs()
    {
        EditorApplication.delayCall += RunInspection;
    }

    [MenuItem("Tools/Klirans/Inspect NPCs and Chairs")]
    public static void RunInspection()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== NPC AND CHAIR AUDIT ===");

        // Find all chairs
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        var chairs = new System.Collections.Generic.List<GameObject>();
        var npcs = new System.Collections.Generic.List<GameObject>();

        foreach (var go in allGOs)
        {
            string lower = go.name.ToLower();
            if (lower.Contains("chair") || lower.Contains("seat") || lower.Contains("bench") || lower.Contains("stool"))
            {
                chairs.Add(go);
            }

            if (go.GetComponent<ClearanceNPC>() != null ||
                go.GetComponent<RoomStaffAI>() != null ||
                go.GetComponent<ProctorAI>() != null ||
                lower.Contains("npc") ||
                lower.Contains("signatory") ||
                lower.Contains("guard"))
            {
                // filter out child objects like Waypoints, etc.
                if (go.GetComponent<ClearanceNPC>() != null ||
                    go.GetComponent<RoomStaffAI>() != null ||
                    go.GetComponent<ProctorAI>() != null ||
                    go.name.StartsWith("Signatory_") ||
                    go.name.StartsWith("ClearanceNPC_") ||
                    go.name == "Security_Guard_NPC")
                {
                    if (!npcs.Contains(go)) npcs.Add(go);
                }
            }
        }

        sb.AppendLine($"\nTotal NPCs found: {npcs.Count}");
        foreach (var npc in npcs)
        {
            sb.AppendLine($"\nNPC: '{npc.name}' at {npc.transform.position}, rot={npc.transform.rotation.eulerAngles}");
            var animator = npc.GetComponentInChildren<Animator>();
            sb.AppendLine($"  Animator: {(animator != null ? animator.name + " (runtimeController=" + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null") + ")" : "NONE")}");
            var cNPC = npc.GetComponent<ClearanceNPC>();
            sb.AppendLine($"  ClearanceNPC: {(cNPC != null ? "YES (name=" + cNPC.npcName + ", sig=" + cNPC.signatureIndex + ")" : "NO")}");
            var staffAI = npc.GetComponent<RoomStaffAI>();
            sb.AppendLine($"  RoomStaffAI: {(staffAI != null ? "YES (role=" + staffAI.staffRole + ")" : "NO")}");
            var proctorAI = npc.GetComponent<ProctorAI>();
            sb.AppendLine($"  ProctorAI: {(proctorAI != null ? "YES" : "NO")}");

            // Find closest chair
            GameObject closestChair = null;
            float minDist = float.MaxValue;
            foreach (var chair in chairs)
            {
                // ignore chair parts if parent is chair
                float d = Vector3.Distance(npc.transform.position, chair.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closestChair = chair;
                }
            }
            if (closestChair != null)
            {
                sb.AppendLine($"  Closest Chair: '{closestChair.name}' at {closestChair.transform.position}, dist={minDist:F2}m (dxz={Vector2.Distance(new Vector2(npc.transform.position.x, npc.transform.position.z), new Vector2(closestChair.transform.position.x, closestChair.transform.position.z)):F2}m, dy={npc.transform.position.y - closestChair.transform.position.y:F2}m)");
            }
        }

        sb.AppendLine($"\nTotal Chair objects in scene: {chairs.Count}");
        // List unique root chair objects or interesting chairs near NPCs
        foreach (var chair in chairs)
        {
            // Only log if near any NPC or high-level
            bool nearAnyNpc = false;
            foreach (var npc in npcs)
            {
                if (Vector3.Distance(npc.transform.position, chair.transform.position) < 3.0f)
                {
                    nearAnyNpc = true;
                    break;
                }
            }
            if (nearAnyNpc || chair.name == "Chair_Seat" || chair.name.Contains("OfficeChair") || chair.name.Contains("StaffChair"))
            {
                sb.AppendLine($"  Chair: '{chair.name}' (parent='{(chair.transform.parent != null ? chair.transform.parent.name : "null")}') pos={chair.transform.position}, rot={chair.transform.rotation.eulerAngles}");
            }
        }

        File.WriteAllText("Assets/NPCAndChairAudit.txt", sb.ToString());
        Debug.Log("NPC and Chair audit completed:\n" + sb.ToString());
    }
}
