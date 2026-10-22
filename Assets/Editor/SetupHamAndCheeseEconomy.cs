using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class SetupHamAndCheeseEconomy
{
    [MenuItem("Tools/Klirans/Setup Ham and Cheese & Coin Economy")]
    public static void Run()
    {
        Debug.Log("=== SETTING UP HAM & CHEESE VENDOR AND COIN ECONOMY ===");

        // 1. Create Renz Material if missing
        SetupRenzMaterial();

        // 2. Create Ham & Cheese Sandwich Icon & Asset
        SetupSandwichAsset();

        // 3. Setup Managers in Scene
        SetupSceneManagers();

        // 4. Scatter Coins across classrooms and hallways
        ScatterCoinsAcrossBuilding();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("=== HAM & CHEESE VENDOR AND COIN ECONOMY COMPLETE! ===");
    }

    private static void SetupRenzMaterial()
    {
        string matPath = "Assets/NPC Assets/Materials/Renz_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "Renz_Mat";

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NPC Assets/Texture/renz_texture.png");
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }

            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[EconomySetup] Created {matPath}");
        }
    }

    private static void SetupSandwichAsset()
    {
        // 1. Create 64x64 sandwich icon PNG if missing
        string iconPath = "Assets/Items/Icons/HamAndCheese_Icon.png";
        if (!File.Exists(iconPath))
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color crust = new Color(0.72f, 0.44f, 0.18f, 1f);
            Color bread = new Color(0.96f, 0.84f, 0.62f, 1f);
            Color cheese = new Color(1.00f, 0.78f, 0.12f, 1f);
            Color ham = new Color(0.88f, 0.38f, 0.42f, 1f);
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    // Draw a tasty diagonal toasted sandwich wedge
                    if (x > 8 && x < 56 && y > 10 && y < 54)
                    {
                        if (y >= 44 || y <= 18) tex.SetPixel(x, y, crust);
                        else if (y >= 36 || y <= 24) tex.SetPixel(x, y, bread);
                        else if (y >= 30) tex.SetPixel(x, y, cheese);
                        else tex.SetPixel(x, y, ham);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(iconPath, bytes);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);

            // Set sprite import settings
            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100f;
                importer.SaveAndReimport();
            }
            Debug.Log($"[EconomySetup] Created sandwich icon at {iconPath}");
        }

        // 2. Create or update ScriptableObject InventoryItem
        string assetPath = "Assets/Items/HamAndCheeseSandwich.asset";
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        item.itemName = "Ham & Cheese Sandwich";
        item.description = "Mainit-init na toasted sandwich na may makapal na savory ham at melted cheese. Pampalubag-loob sa clearance! Kumain para gumaan ang pakiramdam at bumaba ang anxiety (-30).";
        item.itemType = InventoryItem.ItemType.Consumable;
        item.anxietyChange = -30f;
        item.staminaChange = 25f;
        item.isStackable = true;
        item.maxStack = 5;
        item.canBeDropped = true;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (sprite != null) item.icon = sprite;

        EditorUtility.SetDirty(item);
        Debug.Log($"[EconomySetup] Configured {assetPath}");
    }

    private static void SetupSceneManagers()
    {
        // 1. CoinManager
        var coinMgrGO = GameObject.Find("CoinManager");
        if (coinMgrGO == null)
        {
            coinMgrGO = new GameObject("CoinManager");
        }
        var coinMgr = coinMgrGO.GetComponent<CoinManager>();
        if (coinMgr == null) coinMgr = coinMgrGO.AddComponent<CoinManager>();

        var chime = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/objective_chime.wav");
        if (chime != null)
        {
            coinMgr.coinChimeClip = chime;
            coinMgr.purchaseChimeClip = chime;
        }

        // 2. CoinDisplayUI on HudCanvas
        var hud = GameObject.Find("HudCanvas");
        if (hud != null)
        {
            var display = hud.GetComponent<CoinDisplayUI>();
            if (display == null) display = hud.AddComponent<CoinDisplayUI>();

            var vendorUI = hud.GetComponent<HamAndCheeseVendorUI>();
            if (vendorUI == null) vendorUI = hud.AddComponent<HamAndCheeseVendorUI>();
        }

        // 3. HamAndCheeseVendorManager
        var vendorMgrGO = GameObject.Find("HamAndCheeseVendorManager");
        if (vendorMgrGO == null)
        {
            vendorMgrGO = new GameObject("HamAndCheeseVendorManager");
        }
        var vendorMgr = vendorMgrGO.GetComponent<HamAndCheeseVendorManager>();
        if (vendorMgr == null) vendorMgr = vendorMgrGO.AddComponent<HamAndCheeseVendorManager>();

        vendorMgr.initialSpawnDelay = 3.0f;
        vendorMgr.relocateInterval = 90.0f;
        vendorMgr.postPurchaseRelocateDelay = 20.0f;

        // Build & assign Vendor Prefab
        string prefabPath = "Assets/Prefabs/Vendor_NaglalakoNgHamAndCheese.prefab";
        var vendorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var sandwichAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/HamAndCheeseSandwich.asset");

        if (vendorPrefab == null)
        {
            SetupVendorRigging.Run();
            vendorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        vendorMgr.vendorPrefab = vendorPrefab;
        EditorUtility.SetDirty(vendorMgr);
        EditorUtility.SetDirty(vendorMgrGO);

        // 4. HamAndCheeseItem
        var itemCtrl = GameObject.Find("HamAndCheeseController");
        if (itemCtrl == null) itemCtrl = new GameObject("HamAndCheeseController");
        var itemComp = itemCtrl.GetComponent<HamAndCheeseItem>();
        if (itemComp == null) itemComp = itemCtrl.AddComponent<HamAndCheeseItem>();
        if (sandwichAsset != null) itemComp.sandwichItemAsset = sandwichAsset;
        EditorUtility.SetDirty(itemComp);
        EditorUtility.SetDirty(itemCtrl);

        if (coinMgr != null) EditorUtility.SetDirty(coinMgr);
        if (hud != null) EditorUtility.SetDirty(hud);

        Debug.Log("[EconomySetup] Scene managers configured successfully.");
    }

    private static void ScatterCoinsAcrossBuilding()
    {
        var oldContainer = GameObject.Find("CoinsContainer");
        if (oldContainer != null) Object.DestroyImmediate(oldContainer);

        var coinsHolder = new GameObject("CoinsContainer");

        // Curated exploration coin locations across 1F, 2F, and 3F
        // Placed on classroom teacher tables, student desk rows, shelf corners, and hallway alcoves
        List<Vector3> coinPositions = new List<Vector3>
        {
            // ── 1ST FLOOR COINS (16 Coins) ──
            new Vector3(-80.50f, 3.50f, 7.50f),   // 1F Lobby bench
            new Vector3(-78.20f, 3.50f, 18.50f),  // 1F Lobby nook
            new Vector3(-84.00f, 3.50f, -8.00f),  // 1F South hallway end
            new Vector3(-84.00f, 3.50f, 27.50f),  // 1F North hallway alcove
            new Vector3(-84.00f, 3.50f, 12.00f),  // 1F Hallway mid
            new Vector3(-84.00f, 3.50f, 2.00f),   // 1F Hallway south mid
            new Vector3(-90.50f, 3.50f, 12.00f),  // 1F Room 101 front
            new Vector3(-91.00f, 3.50f, 8.00f),   // 1F Room 101 student chair
            new Vector3(-90.50f, 3.50f, 2.00f),   // 1F Room 102 desk
            new Vector3(-91.00f, 3.50f, -0.50f),  // 1F Room 102 student chair
            new Vector3(-90.50f, 3.50f, -6.00f),  // 1F Room 103 teacher table
            new Vector3(-91.00f, 3.50f, -10.00f), // 1F Room 103 student desk
            new Vector3(-90.50f, 3.50f, -14.00f), // 1F Room 104 corner
            new Vector3(-78.50f, 3.50f, 10.00f),  // 1F COMLAB 1 desk
            new Vector3(-79.50f, 3.50f, 5.00f),   // 1F COMLAB 1 student terminal
            new Vector3(-78.50f, 3.50f, -6.00f),  // 1F COMLAB 2 desk

            // ── 2ND FLOOR COINS (15 Coins) ──
            new Vector3(-84.00f, 9.50f, -10.00f), // 2F Far South hallway
            new Vector3(-84.00f, 9.50f, 4.00f),   // 2F South hallway
            new Vector3(-84.00f, 9.50f, 14.00f),  // 2F Mid hallway display
            new Vector3(-84.00f, 9.50f, 24.00f),  // 2F North hallway
            new Vector3(-84.00f, 9.50f, 31.00f),  // 2F North hallway end
            new Vector3(-89.51f, 9.50f, 31.92f),  // 2F Room 204 Teacher Desk
            new Vector3(-90.50f, 9.50f, 28.00f),  // 2F Room 204 Student chair
            new Vector3(-89.51f, 9.50f, 6.92f),   // 2F Room 201 Teacher Desk
            new Vector3(-90.50f, 9.50f, 4.00f),   // 2F Room 201 Student chair
            new Vector3(-89.51f, 9.50f, -5.08f),  // 2F Room 202 Teacher Desk
            new Vector3(-91.00f, 9.50f, -7.00f),  // 2F Room 202 Student chair
            new Vector3(-90.50f, 9.50f, -12.00f), // 2F Room 203 Shelf
            new Vector3(-79.49f, 9.50f, 31.95f),  // 2F Room 205 Teacher Desk
            new Vector3(-79.49f, 9.50f, 6.95f),   // 2F Room 206 Teacher Desk
            new Vector3(-79.49f, 9.50f, -5.03f),  // 2F Room 207 Teacher Desk

            // ── 3RD FLOOR COINS (15 Coins) ──
            new Vector3(-84.00f, 15.50f, -12.00f),// 3F South end
            new Vector3(-84.00f, 15.50f, 0.00f),  // 3F South mid hallway
            new Vector3(-84.00f, 15.50f, 8.00f),  // 3F Hallway center
            new Vector3(-84.00f, 15.50f, 20.00f), // 3F North mid hallway
            new Vector3(-84.00f, 15.50f, 32.00f), // 3F North hallway
            new Vector3(-84.00f, 15.50f, 44.00f), // 3F Far north lockdown gate
            new Vector3(-89.51f, 15.50f, 44.45f), // 3F Room 305 Teacher Desk
            new Vector3(-89.51f, 15.50f, 31.95f), // 3F Room 304 Teacher Desk
            new Vector3(-90.50f, 15.50f, 28.00f), // 3F Room 304 Student chair
            new Vector3(-89.51f, 15.50f, 6.95f),  // 3F Room 301 Teacher Desk
            new Vector3(-91.00f, 15.50f, 5.00f),  // 3F Room 301 Student chair
            new Vector3(-89.51f, 15.50f, -5.05f), // 3F Room 302 Teacher Desk
            new Vector3(-90.50f, 15.50f, -8.00f), // 3F Room 303 Shelf
            new Vector3(-79.59f, 15.50f, 31.97f), // 3F Room 307 Teacher Desk
            new Vector3(-79.59f, 15.50f, 6.97f)   // 3F Room 306 Teacher Desk
        };

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var coinMat = new Material(shader);
        coinMat.name = "Coin_Realistic_Mat";
        coinMat.color = new Color(0.92f, 0.83f, 0.54f, 1f);
        if (coinMat.HasProperty("_Metallic")) coinMat.SetFloat("_Metallic", 0.94f);
        if (coinMat.HasProperty("_Smoothness")) coinMat.SetFloat("_Smoothness", 0.75f);

        for (int i = 0; i < coinPositions.Count; i++)
        {
            var coinGO = new GameObject($"CoinPickup_{i + 1}");
            coinGO.transform.SetParent(coinsHolder.transform);

            Vector3 spawnPos = coinPositions[i];
            Quaternion spawnRot = Quaternion.identity;

            // Surface snap directly to surface below (desk, table, shelf, or floor)
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point + hit.normal * 0.0016f;
                float randomYaw = Random.Range(0f, 360f);
                spawnRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, randomYaw, 0f);
            }

            coinGO.transform.position = spawnPos;
            coinGO.transform.rotation = spawnRot;

            var pickup = coinGO.AddComponent<CoinPickup>();
            pickup.coinValue = 1;
            pickup.canRespawn = true;
            pickup.minRespawnTime = 180f; // 3 minutes
            pickup.maxRespawnTime = 300f; // 5 minutes
            pickup.minPlayerDistanceToRespawn = 9.0f; // Must leave room before it respawns

            // Visual mesh: miniscule flat horizontal disc (4.5cm diameter, 3.2mm thickness)
            var discGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            discGO.name = "CoinDiscMesh";
            discGO.transform.SetParent(coinGO.transform, false);
            discGO.transform.localPosition = Vector3.zero;
            discGO.transform.localRotation = Quaternion.identity;
            discGO.transform.localScale = new Vector3(0.045f, 0.0016f, 0.045f);

            var primCol = discGO.GetComponent<Collider>();
            if (primCol != null) Object.DestroyImmediate(primCol);

            var mr = discGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = coinMat;

            // Trigger pickup collider
            var rootCol = coinGO.AddComponent<SphereCollider>();
            rootCol.isTrigger = true;
            rootCol.radius = 0.35f;
            rootCol.center = new Vector3(0f, 0.06f, 0f);
        }

        Debug.Log($"[EconomySetup] Placed and surface-snapped {coinPositions.Count} exploration coins across 1F, 2F, and 3F!");
    }
}
