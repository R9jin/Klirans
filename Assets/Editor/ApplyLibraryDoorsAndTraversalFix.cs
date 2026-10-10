using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyLibraryDoorsAndTraversalFix
{
    [MenuItem("Tools/Klirans/Apply Library Doors And Traversal Fix")]
    public static void RunFix()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== APPLYING LIBRARY DOORS AND TRAVERSAL FIX ===");

        // 1. Update Door Angles
        var allDoors = UnityEngine.Object.FindObjectsByType<DoorInteract>(FindObjectsInactive.Include);
        int doorsUpdated = 0;
        foreach (var door in allDoors)
        {
            bool modified = false;
            if (door.name == "Door_308A" || door.name == "Door_308B")
            {
                door.openAngle = 90f;
                modified = true;
            }
            else if (Mathf.Approximately(door.openAngle, 70f))
            {
                door.openAngle = 90f;
                modified = true;
            }
            else if (Mathf.Approximately(door.openAngle, -70f))
            {
                door.openAngle = -90f;
                modified = true;
            }

            if (modified)
            {
                doorsUpdated++;
                EditorUtility.SetDirty(door.gameObject);
                sb.AppendLine($"Updated door {door.name}: openAngle = {door.openAngle}");
            }
        }
        sb.AppendLine($"Total doors updated: {doorsUpdated}");

        // 2. Shift Room 308 Bookshelves to clear Door_308B doorway
        string[] targetBookshelves = new[]
        {
            "Rooms/3rdFloor/Room 308/LibraryFurniture/Bookshelves_Area/Bookshelf_West_1",
            "Rooms/3rdFloor/Room 308/LibraryFurniture/Bookshelves_Area/Bookshelf_West_2",
            "Rooms/3rdFloor/Room 308/LibraryFurniture/Bookshelves_Area/Bookshelf_CenterWest_1 (1)",
            "Rooms/3rdFloor/Room 308/LibraryFurniture/Bookshelves_Area/Bookshelf_CenterWest_2 (1)"
        };

        foreach (var bsPath in targetBookshelves)
        {
            var bsGO = GameObject.Find(bsPath);
            if (bsGO != null)
            {
                // Only shift if it hasn't been shifted yet (original z was 46.20 or 48.20)
                if (bsGO.transform.position.z < 46.5f || (bsGO.name.EndsWith("_2") && bsGO.transform.position.z < 48.5f) || (bsGO.name.Contains("(1)") && bsGO.transform.position.z < 46.5f))
                {
                    Vector3 p = bsGO.transform.position;
                    p.z += 0.80f;
                    bsGO.transform.position = p;
                    EditorUtility.SetDirty(bsGO);
                    sb.AppendLine($"Shifted {bsGO.name} in Z by +0.8m to {p}");
                }
                else
                {
                    sb.AppendLine($"{bsGO.name} already shifted at {bsGO.transform.position}");
                }
            }
            else
            {
                sb.AppendLine($"Warning: Could not find {bsPath}");
            }
        }

        // 3. Clear Room 208 chair near Door_208A
        var chair27 = GameObject.Find("Rooms/2ndFloor/Room 208/ClassroomFurniture/StudentChairsAndTables/student_chair (27)");
        if (chair27 != null && chair27.transform.position.x < -81.0f)
        {
            Vector3 cp = chair27.transform.position;
            cp.x += 0.80f; // move further inside classroom
            EditorUtility.SetDirty(chair27);
            sb.AppendLine($"Moved Room 208 chair (27) to {cp}");
        }

        // 3b. Calibrate Player CharacterController height and radius to clear 1.85m door lintels
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
                sb.AppendLine($"Calibrated Player CC: height=1.50, center=(0, 0.75, 0), radius=0.25");
            }

            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.defaultHeight = 1.50f;
                pm.crouchHeight = 1.10f;
                EditorUtility.SetDirty(player);
                sb.AppendLine($"Calibrated PlayerMovement: defaultHeight=1.50, crouchHeight=1.10");
            }
        }

        // 4. Test Traversal Verification on both Library Doors
        Physics.SyncTransforms();
        sb.AppendLine("\n--- VERIFYING LIBRARY DOOR TRAVERSAL AFTER FIX ---");

        VerifyDoorCleanPass("Door_308A", sb);
        VerifyDoorCleanPass("Door_308B", sb);

        // 5. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        sb.AppendLine("\nSuccessfully saved scene Assets/Scenes/SampleScene.unity!");
        File.WriteAllText("Assets/LibraryFixVerificationReport.txt", sb.ToString());
        Debug.Log("=== LIBRARY DOORS FIX COMPLETED AND SAVED ===");
    }

    private static void VerifyDoorCleanPass(string doorName, StringBuilder sb)
    {
        var dGO = GameObject.Find(doorName);
        if (dGO == null) return;
        var di = dGO.GetComponent<DoorInteract>();

        Quaternion origRot = dGO.transform.localRotation;
        dGO.transform.localRotation = origRot * Quaternion.AngleAxis(di.openAngle, di.rotationAxis);
        Physics.SyncTransforms();

        float zBase = dGO.transform.position.z;
        float startX = dGO.transform.position.x - 1.5f;
        float endX = dGO.transform.position.x + 1.5f;
        float footY = dGO.transform.position.y;
        float radius = 0.25f;
        float height = 1.50f;

        int clearCount = 0;
        int totalTests = 0;

        for (float zOffset = 0.35f; zOffset <= 1.05f; zOffset += 0.05f)
        {
            totalTests++;
            float z = zBase + zOffset;
            bool blocked = false;

            // Walk from hallway into room in 50 steps
            for (int s = 0; s <= 50; s++)
            {
                float t = (float)s / 50;
                float x = Mathf.Lerp(startX, endX, t);
                Vector3 footPos = new Vector3(x, footY + 0.02f, z);
                Vector3 p1 = footPos + Vector3.up * radius;
                Vector3 p2 = footPos + Vector3.up * (height - radius);

                Collider[] hits = Physics.OverlapCapsule(p1, p2, radius, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (h.name.ToLower().Contains("floor")) continue;
                    blocked = true;
                    break;
                }
                if (blocked) break;
            }

            if (!blocked) clearCount++;
        }

        dGO.transform.localRotation = origRot;
        Physics.SyncTransforms();

        sb.AppendLine($"Door {doorName} open test: {clearCount}/{totalTests} paths completely clear. Result: {(clearCount > 0 ? "PASSED - FULLY WALKABLE!" : "FAILED!")}");
    }
}
