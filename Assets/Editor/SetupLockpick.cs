using UnityEngine;
using UnityEditor;

public class SetupLockpick
{
    private static Material GetOrCreateMaterial(string assetName, Color color, float metallic = 0f, float smoothness = 0.5f)
    {
        string folderPath = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        string matPath = $"{folderPath}/{assetName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") 
                        ?? Shader.Find("URP/Lit") 
                        ?? Shader.Find("Lit") 
                        ?? Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(urpShader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = urpShader;
        }

        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", metallic);
        }
        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", smoothness);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    [MenuItem("Tools/Setup Lockpick & Locked Door")]
    public static void Run()
    {
        // 1. Ensure Prefabs directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 2. Load or Create Saved Material Assets
        Material handleMat = GetOrCreateMaterial("LockpinHandleMat", new Color(0.15f, 0.15f, 0.18f, 1f), 0.1f, 0.3f);
        Material steelMat = GetOrCreateMaterial("LockpinSteelMat", new Color(0.85f, 0.85f, 0.82f, 1f), 0.85f, 0.75f);

        // 3. Rebuild 3D Lockpick Model & Prefab with persistent Material Assets
        string prefabPath = "Assets/Prefabs/LockpinPrefab.prefab";
        GameObject modelRoot = new GameObject("LockpinModel");

        // Handle (Dark Grip)
        GameObject handleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handleObj.name = "Handle";
        handleObj.transform.SetParent(modelRoot.transform, false);
        handleObj.transform.localScale = new Vector3(0.015f, 0.03f, 0.12f);
        handleObj.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        handleObj.GetComponent<Renderer>().sharedMaterial = handleMat;

        // Shaft (Thin Metal Steel Rod)
        GameObject shaftObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaftObj.name = "Shaft";
        shaftObj.transform.SetParent(modelRoot.transform, false);
        shaftObj.transform.localScale = new Vector3(0.005f, 0.006f, 0.16f);
        shaftObj.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        shaftObj.GetComponent<Renderer>().sharedMaterial = steelMat;

        // Hook Tip (Angled Hook Tip at end)
        GameObject hookObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hookObj.name = "HookTip";
        hookObj.transform.SetParent(modelRoot.transform, false);
        hookObj.transform.localScale = new Vector3(0.005f, 0.012f, 0.025f);
        hookObj.transform.localPosition = new Vector3(0f, 0.006f, 0.165f);
        hookObj.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        hookObj.GetComponent<Renderer>().sharedMaterial = steelMat;

        // Master BoxCollider on Root
        BoxCollider col = modelRoot.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.03f);
        col.size = new Vector3(0.04f, 0.04f, 0.35f);

        GameObject lockpinPrefab = PrefabUtility.SaveAsPrefabAsset(modelRoot, prefabPath);
        Object.DestroyImmediate(modelRoot);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupLockpick] Rebuilt 3D LockpinPrefab with saved URP Material assets!");

        // 4. Ensure Lockpin item asset exists & links to 3D prefab
        InventoryItem lockpinAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Lockpin.asset");
        if (lockpinAsset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Items"))
            {
                AssetDatabase.CreateFolder("Assets", "Items");
            }
            lockpinAsset = ScriptableObject.CreateInstance<InventoryItem>();
            lockpinAsset.itemName = "Lockpin";
            lockpinAsset.description = "A sturdy metal lockpin used for picking door locks.";
            lockpinAsset.itemType = InventoryItem.ItemType.Tool;
            lockpinAsset.isStackable = true;
            lockpinAsset.maxStack = 10;
            
            AssetDatabase.CreateAsset(lockpinAsset, "Assets/Items/Lockpin.asset");
        }

        lockpinAsset.itemPrefab = lockpinPrefab;
        EditorUtility.SetDirty(lockpinAsset);
        AssetDatabase.SaveAssets();

        // 5. Find Player Spawn
        GameObject playerObj = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
        Vector3 spawnPos = playerObj != null ? playerObj.transform.position : Vector3.zero;
        Vector3 spawnForward = playerObj != null ? playerObj.transform.forward : Vector3.forward;

        // 6. Create or Replace Lockpin pickup in scene with 3D model & FloatingItemAnimation
        GameObject existingPickup = GameObject.Find("LockpinPickup");
        if (existingPickup != null)
        {
            Object.DestroyImmediate(existingPickup);
        }

        GameObject pickupObj = (GameObject)PrefabUtility.InstantiatePrefab(lockpinPrefab);
        pickupObj.name = "LockpinPickup";
        pickupObj.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

        PickupItem pickup = pickupObj.GetComponent<PickupItem>();
        if (pickup == null) pickup = pickupObj.AddComponent<PickupItem>();
        pickup.itemData = lockpinAsset;
        pickup.amount = 3;
        pickupObj.layer = LayerMask.NameToLayer("Default");

        // Attach FloatingItemAnimation for continuous GTA-style spinning & floating bobbing
        FloatingItemAnimation anim = pickupObj.GetComponent<FloatingItemAnimation>();
        if (anim == null) anim = pickupObj.AddComponent<FloatingItemAnimation>();
        anim.rotationSpeed = 60f;
        anim.floatAmplitude = 0.12f;
        anim.floatFrequency = 2.0f;

        if (playerObj != null)
        {
            pickupObj.transform.position = spawnPos + spawnForward * 1.5f + Vector3.up * 0.4f;
        }
        else
        {
            pickupObj.transform.position = new Vector3(0, 0.5f, 0);
        }
        Debug.Log($"[SetupLockpick] 3D LockpinPickup positioned at {pickupObj.transform.position} with FloatingItemAnimation.");

        // 7. Find closest door to Player spawn and lock it!
        DoorInteract[] doors = Object.FindObjectsByType<DoorInteract>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (doors != null && doors.Length > 0)
        {
            DoorInteract closestDoor = null;
            float minDistance = float.MaxValue;

            foreach (var door in doors)
            {
                float dist = Vector3.Distance(door.transform.position, spawnPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestDoor = door;
                }
            }

            if (closestDoor != null)
            {
                closestDoor.isLocked = true;
                closestDoor.requiredLockpinItem = lockpinAsset;
                Debug.Log($"[SetupLockpick] Locked closest door '{closestDoor.gameObject.name}' at distance {minDistance:F2}m from player spawn!");
            }
        }

        // 8. Ensure LockpickingMinigame object exists in scene hierarchy
        LockpickingMinigame mgr = Object.FindAnyObjectByType<LockpickingMinigame>();
        if (mgr == null)
        {
            GameObject minigameGO = new GameObject("LockpickingMinigame");
            minigameGO.AddComponent<LockpickingMinigame>();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("3D Lockpick, Saved URP Materials, Floating Animation & Minigame Manager setup complete!");
    }
}
