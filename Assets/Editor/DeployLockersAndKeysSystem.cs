using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DeployLockersAndKeysSystem
{
    [MenuItem("Klirans/Deploy Lockers and Keys System")]
    public static void Deploy()
    {
        Debug.Log("[DeployLockersAndKeysSystem] Starting deployment...");

        // 1. Create Brass Key Icon
        Sprite keyIcon = CreateOrLoadKeyIcon();

        // 2. Create Key Material
        Material keyMat = CreateOrLoadKeyMaterial();

        // 3. Create LockerKey ScriptableObject
        InventoryItem lockerKeyItem = CreateOrLoadLockerKeyItem(keyIcon);

        // 4. Create LockerKeyPickup Prefab
        GameObject keyPrefab = CreateOrLoadKeyPrefab(lockerKeyItem, keyMat);
        if (lockerKeyItem.itemPrefab == null)
        {
            lockerKeyItem.itemPrefab = keyPrefab;
            EditorUtility.SetDirty(lockerKeyItem);
        }

        // 5. Deploy Floor 1 Hallway Lockers
        DeployFloor1Lockers();

        // 6. Configure LockerHide on Floors 1, 2, 3
        ConfigureLockerHideOnFloors(lockerKeyItem);

        // 7. Scatter 6 Locker Keys across desks
        ScatterLockerKeys(keyPrefab);

        // 8. Configure GameManager LockerHideManager
        ConfigureLockerHideManager();

        // 9. Save assets & scene
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("[DeployLockersAndKeysSystem] Deployment successfully completed!");
    }

    private static Sprite CreateOrLoadKeyIcon()
    {
        string iconPath = "Assets/Items/Icons/LockerKey_Icon.png";
        if (!File.Exists(iconPath))
        {
            int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);
            Color brassLight = new Color(0.96f, 0.85f, 0.42f, 1f);
            Color brassDark = new Color(0.68f, 0.52f, 0.18f, 1f);
            Color outline = new Color(0.25f, 0.18f, 0.05f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // Draw brass key:
            // Bow center at (28, 68), outer radius 18, inner radius 8
            Vector2 bowCenter = new Vector2(28, 68);
            float outerR = 19f;
            float innerR = 8f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float d = Vector2.Distance(pt, bowCenter);

                    // Bow circle
                    if (d <= outerR && d >= innerR)
                    {
                        if (d >= outerR - 1.5f || d <= innerR + 1.5f)
                            tex.SetPixel(x, y, outline);
                        else
                            tex.SetPixel(x, y, Color.Lerp(brassLight, brassDark, (float)y / size));
                    }

                    // Shaft running diagonally from (40, 56) to (76, 20)
                    // Line equation: y - 56 = -1 * (x - 40) => x + y = 96
                    float lineDist = Mathf.Abs(x + y - 96) / 1.414f;
                    float shaftProg = (x - 40f) / 36f;
                    if (shaftProg >= 0f && shaftProg <= 1.0f && lineDist <= 4.0f)
                    {
                        if (lineDist >= 3.0f)
                            tex.SetPixel(x, y, outline);
                        else
                            tex.SetPixel(x, y, Color.Lerp(brassLight, brassDark, shaftProg));
                    }

                    // Key bittings (teeth) extending downward-left near the tip
                    if (x >= 64 && x <= 76)
                    {
                        // Tooth 1
                        if (x >= 64 && x <= 69 && y >= 20 && y <= 27)
                            tex.SetPixel(x, y, brassDark);
                        // Tooth 2
                        if (x >= 71 && x <= 76 && y >= 13 && y <= 22)
                            tex.SetPixel(x, y, brassDark);
                    }
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(iconPath, bytes);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    }

    private static Material CreateOrLoadKeyMaterial()
    {
        string matPath = "Assets/Materials/LockerKeyMat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "LockerKeyMat";
            mat.SetColor("_BaseColor", new Color(0.88f, 0.72f, 0.28f, 1.0f));
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.65f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        return mat;
    }

    private static InventoryItem CreateOrLoadLockerKeyItem(Sprite icon)
    {
        string itemPath = "Assets/Items/LockerKey.asset";
        InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            item.itemName = "Locker Key";
            item.description = "A heavy stamped brass locker key. Can unlock locked hallway lockers to hide inside during blackouts.";
            item.itemType = InventoryItem.ItemType.KeyItem;
            item.isStackable = true;
            item.maxStack = 6;
            item.canBeDropped = false;
            item.icon = icon;
            AssetDatabase.CreateAsset(item, itemPath);
        }
        else
        {
            item.itemName = "Locker Key";
            item.itemType = InventoryItem.ItemType.KeyItem;
            item.isStackable = true;
            item.maxStack = 6;
            item.canBeDropped = false;
            if (icon != null) item.icon = icon;
            EditorUtility.SetDirty(item);
        }
        return item;
    }

    private static GameObject CreateOrLoadKeyPrefab(InventoryItem item, Material mat)
    {
        string prefabPath = "Assets/Prefabs/LockerKeyPickup.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null) return existingPrefab;

        // Build key model
        GameObject root = new GameObject("LockerKeyPickup");
        root.layer = LayerMask.NameToLayer("Default");

        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.01f, 0f);
        bc.size = new Vector3(0.12f, 0.04f, 0.24f);
        bc.isTrigger = false;

        PickupItem pickup = root.AddComponent<PickupItem>();
        pickup.itemData = item;
        pickup.amount = 1;

        FloatingItemAnimation anim = root.AddComponent<FloatingItemAnimation>();
        anim.enabled = false;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Visual child meshes
        GameObject bow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bow.name = "KeyBow";
        bow.transform.SetParent(root.transform, false);
        bow.transform.localPosition = new Vector3(0f, 0.005f, -0.06f);
        bow.transform.localScale = new Vector3(0.07f, 0.005f, 0.07f);
        Object.DestroyImmediate(bow.GetComponent<Collider>());
        bow.GetComponent<MeshRenderer>().sharedMaterial = mat;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "KeyShaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.005f, 0.02f);
        shaft.transform.localScale = new Vector3(0.016f, 0.008f, 0.11f);
        Object.DestroyImmediate(shaft.GetComponent<Collider>());
        shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;

        GameObject bit1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bit1.name = "KeyBit1";
        bit1.transform.SetParent(root.transform, false);
        bit1.transform.localPosition = new Vector3(0.014f, 0.005f, 0.05f);
        bit1.transform.localScale = new Vector3(0.016f, 0.007f, 0.016f);
        Object.DestroyImmediate(bit1.GetComponent<Collider>());
        bit1.GetComponent<MeshRenderer>().sharedMaterial = mat;

        GameObject bit2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bit2.name = "KeyBit2";
        bit2.transform.SetParent(root.transform, false);
        bit2.transform.localPosition = new Vector3(0.012f, 0.005f, 0.07f);
        bit2.transform.localScale = new Vector3(0.014f, 0.007f, 0.014f);
        Object.DestroyImmediate(bit2.GetComponent<Collider>());
        bit2.GetComponent<MeshRenderer>().sharedMaterial = mat;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void DeployFloor1Lockers()
    {
        GameObject floor1 = GameObject.Find("1stFloor");
        if (floor1 == null)
        {
            Debug.LogError("[DeployLockersAndKeysSystem] 1stFloor not found!");
            return;
        }

        Transform f1LockersT = floor1.transform.Find("Lockers");
        GameObject f1LockersGO;
        if (f1LockersT == null)
        {
            f1LockersGO = new GameObject("Lockers");
            f1LockersGO.transform.SetParent(floor1.transform, false);
            f1LockersGO.transform.localPosition = Vector3.zero;
        }
        else
        {
            f1LockersGO = f1LockersT.gameObject;
        }

        if (f1LockersGO.transform.childCount > 0)
        {
            Debug.Log("[DeployLockersAndKeysSystem] 1stFloor Lockers already exist (" + f1LockersGO.transform.childCount + " children).");
            return;
        }

        GameObject f2LockersGO = GameObject.Find("2ndFloor/Lockers");
        if (f2LockersGO == null)
        {
            Debug.LogError("[DeployLockersAndKeysSystem] 2ndFloor/Lockers not found to copy from!");
            return;
        }

        Debug.Log("[DeployLockersAndKeysSystem] Cloning " + f2LockersGO.transform.childCount + " lockers from 2ndFloor to 1stFloor...");
        for (int i = 0; i < f2LockersGO.transform.childCount; i++)
        {
            Transform f2Locker = f2LockersGO.transform.GetChild(i);
            GameObject newLocker = Object.Instantiate(f2Locker.gameObject, f1LockersGO.transform);
            newLocker.name = f2Locker.name;
            // Floor 1 locker Y is 3.10 (Floor 2 is 9.10, exactly 6.0m lower)
            newLocker.transform.position = new Vector3(f2Locker.position.x, 3.10f, f2Locker.position.z);
            newLocker.transform.rotation = f2Locker.rotation;
            newLocker.transform.localScale = f2Locker.localScale;
        }
        Debug.Log("[DeployLockersAndKeysSystem] Successfully deployed 24 lockers to 1stFloor!");
    }

    private static void ConfigureLockerHideOnFloors(InventoryItem lockerKey)
    {
        AudioClip unlockClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/yodguard-door-opening-slowly-1-535465.mp3");
        AudioClip rattleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");

        string[] floorLockerPaths = new string[]
        {
            "1stFloor/Lockers",
            "2ndFloor/Lockers",
            "3rdFloor/Lockers"
        };

        for (int f = 0; f < floorLockerPaths.Length; f++)
        {
            string path = floorLockerPaths[f];
            GameObject parent = GameObject.Find(path);
            if (parent == null)
            {
                Debug.LogWarning("[DeployLockersAndKeysSystem] Could not find " + path);
                continue;
            }

            int floorNum = f + 1;
            // Configure 2 unlocked and 2 locked lockers per floor:
            // Unlocked 1: West 4.0 (central hallway)
            // Unlocked 2: East 28.0 (north hallway)
            // Locked 1: East 4.0 (central hallway) - requires Locker Key
            // Locked 2: West 34.2 (north hallway) - requires Locker Key

            SetupLocker(parent.transform.Find("Locker_West_4.0"), floorNum, isLocked: false, lockerKey, unlockClip, rattleClip);
            SetupLocker(parent.transform.Find("Locker_East_28.0"), floorNum, isLocked: false, lockerKey, unlockClip, rattleClip);
            SetupLocker(parent.transform.Find("Locker_East_4.0"), floorNum, isLocked: true, lockerKey, unlockClip, rattleClip);
            SetupLocker(parent.transform.Find("Locker_West_34.2"), floorNum, isLocked: true, lockerKey, unlockClip, rattleClip);
        }
    }

    private static void SetupLocker(Transform lockerT, int floorNum, bool isLocked, InventoryItem lockerKey, AudioClip unlockClip, AudioClip rattleClip)
    {
        if (lockerT == null) return;
        GameObject locker = lockerT.gameObject;

        LockerHide lh = locker.GetComponent<LockerHide>();
        if (lh == null) lh = locker.AddComponent<LockerHide>();

        lh.isLocked = isLocked;
        lh.lockerLabel = isLocked ? $"Locked Locker ({floorNum}F)" : $"Hallway Locker ({floorNum}F)";
        lh.requiredKey = lockerKey;
        lh.consumeKeyOnUnlock = true;
        lh.unlockAudio = unlockClip;
        lh.lockedRattleAudio = rattleClip;

        // Ensure PeekAnchor
        Transform anchor = lockerT.Find("PeekAnchor");
        if (anchor == null)
        {
            GameObject anchorGO = new GameObject("PeekAnchor");
            anchorGO.transform.SetParent(lockerT, false);
            anchor = anchorGO.transform;
        }

        anchor.localPosition = new Vector3(0f, 0.45f, 0f);
        // Face toward corridor center (X = -84.0f)
        Vector3 corridorPoint = new Vector3(-84.0f, lockerT.position.y, lockerT.position.z);
        Vector3 faceDir = (corridorPoint - lockerT.position).normalized;
        anchor.rotation = Quaternion.LookRotation(faceDir, Vector3.up);

        lh.peekAnchor = anchor;
        EditorUtility.SetDirty(lh);
        Debug.Log($"[DeployLockersAndKeysSystem] Configured {lh.lockerLabel} on {locker.name} (locked={isLocked}).");
    }

    private static void ScatterLockerKeys(GameObject keyPrefab)
    {
        GameObject container = GameObject.Find("LockerKeysContainer");
        if (container == null)
        {
            container = new GameObject("LockerKeysContainer");
        }

        // Clean up previous keys in container
        while (container.transform.childCount > 0)
        {
            Object.DestroyImmediate(container.transform.GetChild(0).gameObject);
        }

        // 6 Desk Locations:
        var spots = new (string name, Vector3 pos)[]
        {
            ("LockerKey_1F_LibraryDesk", new Vector3(-81.20f, 3.03f, 8.12f)),
            ("LockerKey_1F_LobbyCounter", new Vector3(-82.52f, 3.06f, 4.90f)),
            ("LockerKey_2F_Class201TeacherDesk", new Vector3(-89.51f, 9.09f, -5.07f)),
            ("LockerKey_2F_Class203TeacherDesk", new Vector3(-79.49f, 9.12f, 31.96f)),
            ("LockerKey_3F_Class301TeacherDesk", new Vector3(-89.51f, 15.11f, 6.96f)),
            ("LockerKey_3F_LibrarianStudyDesk", new Vector3(-76.85f, 15.03f, 44.15f))
        };

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject keyInstance = PrefabUtility.InstantiatePrefab(keyPrefab) as GameObject;
            keyInstance.name = spots[i].name;
            keyInstance.transform.SetParent(container.transform, false);
            keyInstance.transform.position = spots[i].pos;
            keyInstance.transform.rotation = Quaternion.Euler(0f, 35f * (i + 1), 0f);
            Debug.Log($"[DeployLockersAndKeysSystem] Spawned {spots[i].name} at {spots[i].pos}");
        }
    }

    private static void ConfigureLockerHideManager()
    {
        LockerHideManager mgr = Object.FindAnyObjectByType<LockerHideManager>();
        if (mgr == null)
        {
            GameObject gm = GameObject.Find("GameManager") ?? new GameObject("LockerHideManager");
            mgr = gm.GetComponent<LockerHideManager>() ?? gm.AddComponent<LockerHideManager>();
        }

        AudioClip heartbeat = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");
        if (heartbeat != null)
        {
            mgr.heartbeatAudio = heartbeat;
            EditorUtility.SetDirty(mgr);
            Debug.Log("[DeployLockersAndKeysSystem] Configured heartbeatAudio on LockerHideManager.");
        }
    }
}
