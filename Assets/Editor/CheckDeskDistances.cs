using System.Text;
using UnityEditor;
using UnityEngine;

public static class CheckDeskDistances
{
    [MenuItem("Tools/Klirans/Check Desk Distances")]
    public static void Check()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== CHECK DESK DISTANCES ===");

        var libDesk = GameObject.Find("LibrarianDesk");
        var libStaff = GameObject.Find("Signatory_Librarian");
        if (libDesk != null && libStaff != null)
        {
            sb.AppendLine($"LibrarianDesk pos: {libDesk.transform.position}");
            var rend = libDesk.GetComponentInChildren<Renderer>();
            if (rend != null) sb.AppendLine($"LibrarianDesk bounds: Center {rend.bounds.center}, Size {rend.bounds.size}");
            sb.AppendLine($"Signatory_Librarian pos: {libStaff.transform.position}");
            float d = Vector3.Distance(libDesk.transform.position, libStaff.transform.position);
            sb.AppendLine($"Distance between staff and desk center: {d}");
        }

        var r104Staff = GameObject.Find("Signatory_Registrar");
        if (r104Staff != null)
        {
            sb.AppendLine($"Signatory_Registrar pos: {r104Staff.transform.position}");
        }

        System.IO.File.WriteAllText("Assets/DeskDistanceAudit.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
