using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupItemVisualHighlights
{
    [MenuItem("Klirans/Setup Item Visual Highlights")]
    public static void SetupAll()
    {
        Debug.Log("[SetupItemVisualHighlights] Attaching visual highlights to all items...");

        int count = 0;

        // 1. PickupItem in scene
        var pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
        foreach (var p in pickups)
        {
            if (p.GetComponent<ItemVisualHighlight>() == null)
            {
                p.gameObject.AddComponent<ItemVisualHighlight>();
                EditorUtility.SetDirty(p.gameObject);
                count++;
            }
        }

        // 2. CoinPickup in scene
        var coins = Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Include);
        foreach (var c in coins)
        {
            if (c.GetComponent<ItemVisualHighlight>() == null)
            {
                c.gameObject.AddComponent<ItemVisualHighlight>();
                EditorUtility.SetDirty(c.gameObject);
                count++;
            }
        }

        // 3. FragmentPickup in scene
        var fragments = Object.FindObjectsByType<FragmentPickup>(FindObjectsInactive.Include);
        foreach (var f in fragments)
        {
            if (f.GetComponent<ItemVisualHighlight>() == null)
            {
                f.gameObject.AddComponent<ItemVisualHighlight>();
                EditorUtility.SetDirty(f.gameObject);
                count++;
            }
        }

        // 4. PaperReamPickup in scene
        var papers = Object.FindObjectsByType<PaperReamPickup>(FindObjectsInactive.Include);
        foreach (var pr in papers)
        {
            if (pr.GetComponent<ItemVisualHighlight>() == null)
            {
                pr.gameObject.AddComponent<ItemVisualHighlight>();
                EditorUtility.SetDirty(pr.gameObject);
                count++;
            }
        }

        // 5. Update Prefabs
        UpdatePrefab("Assets/Prefabs/LockerKeyPickup.prefab");
        UpdatePrefab("Assets/Prefabs/LockpinPrefab.prefab");

        // 6. Explicitly initialize all ItemVisualHighlight in the scene
        var allHighlights = Object.FindObjectsByType<ItemVisualHighlight>(FindObjectsInactive.Include);
        foreach (var h in allHighlights)
        {
            h.maxActiveDistance = 7.0f;
            h.fadeStartDistance = 3.5f;
            h.InitializeInEditor();
            EditorUtility.SetDirty(h.gameObject);
        }

        // Save scene & assets
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[SetupItemVisualHighlights] Finished! Configured ItemVisualHighlight on {allHighlights.Length} items in scene and updated item prefabs.");
    }

    private static void UpdatePrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(assetPath))
        {
            GameObject root = scope.prefabContentsRoot;
            if (root.GetComponent<ItemVisualHighlight>() == null)
            {
                root.AddComponent<ItemVisualHighlight>();
                Debug.Log($"[SetupItemVisualHighlights] Added ItemVisualHighlight to prefab: {prefabPath}");
            }
        }
    }
}
