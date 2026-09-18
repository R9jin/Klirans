using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectStaffInteraction
{
    [MenuItem("Tools/Klirans/Inspect Staff Interaction")]
    public static void Inspect()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== INSPECT STAFF INTERACTION ===");

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            sb.AppendLine("ERROR: Player with tag 'Player' not found!");
        }
        else
        {
            var pi = player.GetComponent<PlayerInteract>();
            sb.AppendLine($"Player: '{player.name}' at {player.transform.position}");
            if (pi != null)
            {
                sb.AppendLine($"  interactRange: {pi.interactRange}");
                sb.AppendLine($"  interactRadius: {pi.interactRadius}");
                sb.AppendLine($"  interactableLayer mask: {pi.interactableLayer.value} (Names: {LayerMaskToString(pi.interactableLayer)})");
                sb.AppendLine($"  promptText: {(pi.promptText != null ? pi.promptText.name : "NULL")}");
            }
            else
            {
                sb.AppendLine("  PlayerInteract component NOT found on Player!");
            }
        }

        string[] staffNames = new string[]
        {
            "Signatory_Librarian",
            "Signatory_GuidanceCounselor",
            "Signatory_Registrar",
            "Signatory_CCSDean",
            "Signatory_Cashier",
            "Signatory_EVP"
        };

        foreach (var sName in staffNames)
        {
            var go = GameObject.Find(sName);
            if (go == null)
            {
                sb.AppendLine($"\nStaff '{sName}': NOT FOUND IN SCENE!");
                continue;
            }

            sb.AppendLine($"\nStaff '{sName}':");
            sb.AppendLine($"  Position: {go.transform.position}, Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
            
            var colliders = go.GetComponentsInChildren<Collider>();
            sb.AppendLine($"  Colliders count in self and children: {colliders.Length}");
            foreach (var col in colliders)
            {
                sb.AppendLine($"    Col '{col.name}' ({col.GetType().Name}): enabled={col.enabled}, isTrigger={col.isTrigger}, layer={LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer}), bounds={col.bounds}");
            }

            var cNpc = go.GetComponent<ClearanceNPC>();
            if (cNpc != null)
            {
                sb.AppendLine($"  ClearanceNPC: npcName='{cNpc.npcName}', sigIndex={cNpc.signatureIndex}, range={cNpc.interactionRange}");
                sb.AppendLine($"  blankPaperItem: {(cNpc.blankPaperItem != null ? cNpc.blankPaperItem.name : "NULL")}");
                sb.AppendLine($"  unsignedDialogue: \"{cNpc.unsignedDialogue}\"");
                sb.AppendLine($"  notYourTurnDialogue: \"{cNpc.notYourTurnDialogue}\"");
                
                string testPrompt = cNpc.GetPrompt();
                sb.AppendLine($"  Current GetPrompt() returns: '{testPrompt}'");
            }
            else
            {
                sb.AppendLine("  ClearanceNPC component NOT found!");
            }
        }

        System.IO.File.WriteAllText("Assets/StaffInteractionAudit.txt", sb.ToString());
        Debug.Log("Staff interaction audit saved to Assets/StaffInteractionAudit.txt\n" + sb.ToString());
    }

    private static string LayerMaskToString(LayerMask mask)
    {
        var list = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
            {
                string n = LayerMask.LayerToName(i);
                list.Add(string.IsNullOrEmpty(n) ? $"Layer{i}" : n);
            }
        }
        return string.Join(", ", list);
    }
}
