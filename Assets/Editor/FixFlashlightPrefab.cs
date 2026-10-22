using UnityEngine;
using UnityEditor;

public static class FixFlashlightPrefab
{
    [MenuItem("Tools/Klirans/Fix Flashlight Prefab Colliders")]
    public static void Fix()
    {
        string path = "Assets/PlayerAssets/Flashlight.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        if (prefabRoot != null)
        {
            var meshCols = prefabRoot.GetComponentsInChildren<MeshCollider>(true);
            for (int i = meshCols.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(meshCols[i]);
            }

            var sc = prefabRoot.GetComponent<SphereCollider>();
            if (sc == null) sc = prefabRoot.AddComponent<SphereCollider>();
            sc.radius = 0.16f;
            sc.center = Vector3.zero;

            // Remove rogue child PickupItems
            var childPickups = prefabRoot.GetComponentsInChildren<PickupItem>(true);
            for (int i = childPickups.Length - 1; i >= 0; i--)
            {
                if (childPickups[i].gameObject != prefabRoot)
                {
                    Object.DestroyImmediate(childPickups[i].gameObject == prefabRoot.transform.Find("PickupItem")?.gameObject ? childPickups[i].gameObject : childPickups[i]);
                }
            }

            // Ensure single root PickupItem
            var rootPickup = prefabRoot.GetComponent<PickupItem>();
            if (rootPickup == null) rootPickup = prefabRoot.AddComponent<PickupItem>();
            rootPickup.itemData = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Flashlight.asset");
            rootPickup.amount = 1;

            prefabRoot.layer = LayerMask.NameToLayer("Default");
            foreach (Transform t in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = LayerMask.NameToLayer("Default");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log("[FixFlashlightPrefab] Successfully cleaned Flashlight.prefab colliders and layers!");
        }
    }
}
