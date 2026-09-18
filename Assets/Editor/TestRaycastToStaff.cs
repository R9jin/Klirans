using System.Text;
using UnityEditor;
using UnityEngine;

public static class TestRaycastToStaff
{
    [MenuItem("Tools/Klirans/Test Raycast To Staff")]
    public static void Test()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== TEST RAYCAST TO STAFF ===");

        var player = GameObject.FindGameObjectWithTag("Player");
        var cam = player != null ? player.GetComponentInChildren<Camera>() : null;

        // Test positions for player standing in front of each office
        var tests = new (string staffName, Vector3 playerPos)[]
        {
            ("Signatory_Librarian", new Vector3(-78.2f, 15.0f, 44.15f)),
            ("Signatory_GuidanceCounselor", new Vector3(-87.0f, 9.0f, 42.20f)),
            ("Signatory_Registrar", new Vector3(-84.0f, 3.0f, 4.90f)),
            ("Signatory_CCSDean", new Vector3(-77.8f, 9.0f, -1.8f)),
            ("Signatory_Cashier", new Vector3(-84.0f, 3.0f, -7.10f)),
            ("Signatory_EVP", new Vector3(-88.5f, 3.0f, 5.8f))
        };

        foreach (var t in tests)
        {
            var staff = GameObject.Find(t.staffName);
            if (staff == null)
            {
                sb.AppendLine($"Staff '{t.staffName}' not found!");
                continue;
            }

            Vector3 target = staff.transform.position + Vector3.up * 1.2f;
            Vector3 origin = t.playerPos;
            Vector3 dir = (target - origin).normalized;
            float dist = Vector3.Distance(origin, target);

            sb.AppendLine($"\nTesting '{t.staffName}' from {origin} to target {target} (dist={dist:F2}m):");

            Ray ray = new Ray(origin, dir);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.4f, dist + 0.5f, ~0, QueryTriggerInteraction.Collide);
            sb.AppendLine($"  SphereCastAll hit count: {hits.Length}");
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var interactable = h.collider.GetComponentInParent<IInteractable>() ?? h.collider.GetComponentInChildren<IInteractable>();
                sb.AppendLine($"    Hit: '{h.collider.name}' (Go: '{h.collider.gameObject.name}') at dist={h.distance:F2}m, Layer={LayerMask.LayerToName(h.collider.gameObject.layer)}, HasInteractable={interactable != null}");
            }
        }

        System.IO.File.WriteAllText("Assets/RaycastTestOutput.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
