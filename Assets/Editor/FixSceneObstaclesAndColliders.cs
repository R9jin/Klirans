using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixSceneObstaclesAndColliders
{
    [MenuItem("Tools/Klirans/Fix Scene Obstacles and Colliders")]
    public static void RunFix()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        Debug.Log("=== STARTING SCENE OBSTACLES AND COLLIDERS AUTO-FIX ===");
        int collidersAddedCount = 0;

        // 1. Fix Student Chairs across all classrooms
        var allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string lowerName = go.name.ToLower();

            // Check if it's a student chair
            if (lowerName.StartsWith("student_chair") || lowerName == "plastic_chair")
            {
                if (!HasSolidCollider(go))
                {
                    var mr = go.GetComponentInChildren<MeshRenderer>();
                    if (mr != null)
                    {
                        var bc = go.GetComponent<BoxCollider>();
                        if (bc == null) bc = go.AddComponent<BoxCollider>();
                        ConfigureBoxColliderFromRenderer(bc, mr, new Vector3(0.55f, 0.85f, 0.55f));
                        bc.isTrigger = false;
                        collidersAddedCount++;
                        EditorUtility.SetDirty(go);
                    }
                }
            }
        }
        Debug.Log($"[FixSceneObstacles] Added solid colliders to student chairs (Running total: {collidersAddedCount})");

        // 2. Fix Lockers (LargeLocker / GrandCasier)
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string lowerName = go.name.ToLower();

            if (lowerName.Contains("largelocker") || lowerName.Contains("grandcasier"))
            {
                if (!HasSolidCollider(go))
                {
                    var mr = go.GetComponentInChildren<MeshRenderer>();
                    if (mr != null)
                    {
                        // If it has a trigger collider for LockerHide, add a solid collider child or second collider
                        var existingCol = go.GetComponent<Collider>();
                        if (existingCol != null && existingCol.isTrigger)
                        {
                            // Add a dedicated child GameObject for physical blocking
                            var solidChild = go.transform.Find("PhysicalSolidCollider");
                            if (solidChild == null)
                            {
                                var childGO = new GameObject("PhysicalSolidCollider");
                                childGO.transform.SetParent(go.transform, false);
                                var bc = childGO.AddComponent<BoxCollider>();
                                ConfigureBoxColliderFromRenderer(bc, mr, new Vector3(1.10f, 2.00f, 0.50f));
                                bc.isTrigger = false;
                                collidersAddedCount++;
                                EditorUtility.SetDirty(childGO);
                            }
                        }
                        else
                        {
                            var bc = go.GetComponent<BoxCollider>();
                            if (bc == null) bc = go.AddComponent<BoxCollider>();
                            ConfigureBoxColliderFromRenderer(bc, mr, new Vector3(1.10f, 2.00f, 0.50f));
                            bc.isTrigger = false;
                            collidersAddedCount++;
                            EditorUtility.SetDirty(go);
                        }
                    }
                }
            }
        }
        Debug.Log($"[FixSceneObstacles] Added solid colliders to Lockers (Running total: {collidersAddedCount})");

        // 3. Fix Office & Classroom Desks, Cabinets, Chairs, Tables
        string[] targetKeywords = new[]
        {
            "staffdesk", "teacherdesk", "informationdesk", "librariandesk", "workstation_lobby",
            "filingcabinet", "staffchair", "adminchair", "facultychair", "counselorexecutivechair",
            "librarianchair", "evp_executivechair", "vp_executivechair", "chair_north", "chair_south",
            "chair_post", "officetrashcan", "studytable"
        };

        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string lowerName = go.name.ToLower();

            bool isTarget = false;
            foreach (var kw in targetKeywords)
            {
                if (lowerName.Contains(kw))
                {
                    isTarget = true;
                    break;
                }
            }

            if (isTarget && !HasSolidCollider(go))
            {
                var mr = go.GetComponent<MeshRenderer>() ?? go.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    // Filter out tiny items like paper, pen, book, key
                    if (lowerName.Contains("book") || lowerName.Contains("paper") || lowerName.Contains("keyboard") || lowerName.Contains("monitor"))
                        continue;

                    var bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                    ConfigureBoxColliderFromRenderer(bc, mr, new Vector3(0.30f, 0.30f, 0.30f));
                    bc.isTrigger = false;
                    collidersAddedCount++;
                    EditorUtility.SetDirty(go);
                }
            }
        }
        Debug.Log($"[FixSceneObstacles] Added solid colliders to Desks, Chairs, and Cabinets (Total added: {collidersAddedCount})");

        // 4. Ensure LockerHideManager is on GameManager
        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            var lhm = gm.GetComponent<LockerHideManager>();
            if (lhm == null)
            {
                lhm = gm.AddComponent<LockerHideManager>();
                EditorUtility.SetDirty(gm);
                Debug.Log("[FixSceneObstacles] Attached LockerHideManager to GameManager!");
            }
        }

        // 5. Adjust Player CharacterController physical presence (calibrated to fit 1.85m door lintels)
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.radius = 0.25f;
                cc.height = 1.50f;
                cc.center = new Vector3(0f, 0.75f, 0f);
                cc.skinWidth = 0.03f;
                cc.stepOffset = 0.30f;
                EditorUtility.SetDirty(player);
                Debug.Log("[FixSceneObstacles] Configured Player CharacterController to radius=0.25, height=1.50!");
            }

            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.defaultHeight = 1.50f;
                pm.crouchHeight = 1.10f;
                EditorUtility.SetDirty(player);
            }
        }

        // 6. Fix Vendor Prefab Colliders
        FixVendorPrefabColliders();

        // 7. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"=== FINISHED FIX: Added {collidersAddedCount} solid obstacle colliders and saved scene! ===");
    }

    private static bool HasSolidCollider(GameObject go)
    {
        var cols = go.GetComponents<Collider>();
        foreach (var c in cols)
        {
            if (c.enabled && !c.isTrigger) return true;
        }
        var childCols = go.GetComponentsInChildren<Collider>();
        foreach (var c in childCols)
        {
            if (c.enabled && !c.isTrigger) return true;
        }
        return false;
    }

    private static void ConfigureBoxColliderFromRenderer(BoxCollider bc, MeshRenderer mr, Vector3 minDimensions)
    {
        Bounds b = mr.bounds;
        Vector3 localCenter = bc.transform.InverseTransformPoint(b.center);
        Vector3 lossyScale = bc.transform.lossyScale;

        float sx = lossyScale.x != 0f ? Mathf.Abs(lossyScale.x) : 1f;
        float sy = lossyScale.y != 0f ? Mathf.Abs(lossyScale.y) : 1f;
        float sz = lossyScale.z != 0f ? Mathf.Abs(lossyScale.z) : 1f;

        Vector3 localSize = new Vector3(
            Mathf.Max(minDimensions.x, b.size.x) / sx,
            Mathf.Max(minDimensions.y, b.size.y) / sy,
            Mathf.Max(minDimensions.z, b.size.z) / sz
        );

        bc.center = localCenter;
        bc.size = localSize;
    }

    private static void FixVendorPrefabColliders()
    {
        string prefabPath = "Assets/Prefabs/Vendor_NaglalakoNgHamAndCheese.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        bool changed = false;
        var existingCols = prefab.GetComponentsInChildren<Collider>();
        bool hasSolid = false;
        foreach (var c in existingCols)
        {
            if (c.enabled && !c.isTrigger) { hasSolid = true; break; }
        }

        if (!hasSolid)
        {
            // Add a capsule or box collider to root
            var bc = prefab.GetComponent<BoxCollider>();
            if (bc == null) bc = prefab.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.75f, 0f);
            bc.size = new Vector3(0.9f, 1.5f, 0.9f);
            bc.isTrigger = false;
            changed = true;
            Debug.Log("[FixSceneObstacles] Added solid BoxCollider to Vendor prefab!");
        }

        if (changed)
        {
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }
}
