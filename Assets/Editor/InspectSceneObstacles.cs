using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InspectSceneObstacles
{
    [MenuItem("Tools/Klirans/Inspect All Scene Obstacles")]
    public static void RunInspection()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DETAILED OBSTACLE COLLIDER INSPECTION ===");
        sb.AppendLine($"Time: {DateTime.Now}");

        var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        
        string[] targetKeywords = new[]
        {
            "chair", "desk", "table", "cabinet", "locker", "shelf", "bookshelf", "rack",
            "cart", "vendor", "cooler", "printer", "bench", "sofa", "podium", "counter", "bin", "trash"
        };

        int solidCount = 0;
        int triggerCount = 0;
        int noColCount = 0;

        List<string> uncollided = new List<string>();
        List<string> triggerOnly = new List<string>();

        foreach (var mr in allRenderers)
        {
            if (mr == null || !mr.gameObject.activeInHierarchy) continue;
            string nameLower = mr.gameObject.name.ToLower();
            string parentPath = GetHierarchyPath(mr.gameObject).ToLower();

            bool isMatch = false;
            foreach (var kw in targetKeywords)
            {
                if (nameLower.Contains(kw) || parentPath.Contains(kw))
                {
                    isMatch = true;
                    break;
                }
            }

            if (!isMatch) continue;

            // Skip tiny decorative sub-meshes like paper, pen, book on table, key, bulb, wire, line, border, label
            if (nameLower.Contains("line") || nameLower.Contains("paper") || nameLower.Contains("pen") || 
                nameLower.Contains("book") || nameLower.Contains("label") || nameLower.Contains("border") ||
                nameLower.Contains("card") || nameLower.Contains("face") || nameLower.Contains("key") ||
                nameLower.Contains("light") || nameLower.Contains("cable"))
            {
                continue;
            }

            // Check if bounds have significant physical obstacle volume (> 0.25m)
            Bounds b = mr.bounds;
            if (b.size.x < 0.2f && b.size.z < 0.2f) continue;
            if (b.size.y < 0.2f) continue;

            // Check colliders on this object, parent, or children
            Collider directCol = mr.GetComponent<Collider>();
            Collider parentCol = mr.GetComponentInParent<Collider>();
            Collider childCol = mr.GetComponentInChildren<Collider>();

            Collider activeCol = directCol ?? parentCol ?? childCol;

            if (activeCol == null)
            {
                noColCount++;
                uncollided.Add($"[NO COLLIDER] '{GetHierarchyPath(mr.gameObject)}' | bounds: center={b.center}, size={b.size}");
            }
            else if (activeCol.isTrigger)
            {
                // Check if there is ANY non-trigger collider
                var allCols = mr.GetComponentsInParent<Collider>();
                bool hasSolid = false;
                foreach (var c in allCols)
                {
                    if (c.enabled && !c.isTrigger) { hasSolid = true; break; }
                }
                if (!hasSolid)
                {
                    var childCols = mr.GetComponentsInChildren<Collider>();
                    foreach (var c in childCols)
                    {
                        if (c.enabled && !c.isTrigger) { hasSolid = true; break; }
                    }
                }

                if (!hasSolid)
                {
                    triggerCount++;
                    triggerOnly.Add($"[TRIGGER ONLY] '{GetHierarchyPath(mr.gameObject)}' | col={activeCol.GetType().Name} on '{activeCol.gameObject.name}'");
                }
                else
                {
                    solidCount++;
                }
            }
            else
            {
                solidCount++;
            }
        }

        sb.AppendLine($"Summary: Solid={solidCount}, TriggerOnly={triggerCount}, NoCollider={noColCount}");
        sb.AppendLine();
        sb.AppendLine("=== UNCOLLIDED OBSTACLES ===");
        foreach (var s in uncollided) sb.AppendLine(s);
        sb.AppendLine();
        sb.AppendLine("=== TRIGGER ONLY OBSTACLES ===");
        foreach (var s in triggerOnly) sb.AppendLine(s);

        string path = Path.Combine(Application.dataPath, "DetailedObstacleAudit.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[InspectSceneObstacles] Finished! Written to {path}");
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string p = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            p = t.name + "/" + p;
            t = t.parent;
        }
        return p;
    }
}
