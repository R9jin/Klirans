using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaceLobbyRoundTable
{
    private const string FbxPath = "Assets/InteriorAssets/round-table-and-chairs/Round_table_and_chairs.fbx";
    private const string MatPath = "Assets/InteriorAssets/round-table-and-chairs/Materials/Round_Table_Mat.mat";
    private const string PrefabPath = "Assets/InteriorAssets/round-table-and-chairs/Lobby_RoundTable.prefab";

    [MenuItem("Tools/Klirans/Place Round Table in Main Lobby")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== PLACING ROUND TABLE IN LOBBY CENTER ===");

        // 1. Ensure Material is loaded
        Material tableMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (tableMat == null)
        {
            log.AppendLine("ERROR: Round_Table_Mat.mat not found!");
            Debug.LogError(log.ToString());
            return;
        }

        // 2. Load FBX asset
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset == null)
        {
            log.AppendLine("ERROR: Round_table_and_chairs.fbx not found!");
            Debug.LogError(log.ToString());
            return;
        }

        // 3. Find Mesh inside FBX
        Mesh tableMesh = null;
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        foreach (var a in allAssets)
        {
            if (a is Mesh m && m.name == "Round_table")
            {
                tableMesh = m;
                break;
            }
        }

        if (tableMesh == null)
        {
            log.AppendLine("ERROR: Round_table mesh not found in FBX!");
            Debug.LogError(log.ToString());
            return;
        }

        // 4. Create or update Prefab with ONLY the round table (no chairs)
        GameObject prefabRoot = new GameObject("Lobby_RoundTable", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        prefabRoot.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
        prefabRoot.transform.localScale = Vector3.one * 103.2707f;

        MeshFilter mf = prefabRoot.GetComponent<MeshFilter>();
        mf.sharedMesh = tableMesh;

        MeshRenderer mr = prefabRoot.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tableMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        MeshCollider mc = prefabRoot.GetComponent<MeshCollider>();
        mc.sharedMesh = tableMesh;

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);
        log.AppendLine($"Created standalone round table prefab at {PrefabPath}");

        // 5. Place in Scene under LobbyArea
        GameObject lobbyArea = GameObject.Find("LobbyArea");
        Transform existingTable = lobbyArea != null ? lobbyArea.transform.Find("Lobby_RoundTable") : null;
        if (existingTable == null)
        {
            GameObject inScene = GameObject.Find("Lobby_RoundTable");
            if (inScene != null) existingTable = inScene.transform;
        }

        if (existingTable != null)
        {
            Object.DestroyImmediate(existingTable.gameObject);
            log.AppendLine("Removed existing Lobby_RoundTable instance for clean placement.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject tableInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tableInstance.name = "Lobby_RoundTable";

        if (lobbyArea != null)
        {
            tableInstance.transform.SetParent(lobbyArea.transform, true);
        }

        // Exact physical center of the ground-floor main lobby:
        // X = -84.50 (corridor center between west wall -93.50 and east main entrance -75.25)
        // Y = 2.30 (flush on ground floor tile surface)
        // Z = 16.65 (center between south wall 8.70 and north wall 24.60)
        tableInstance.transform.position = new Vector3(-84.50f, 2.30f, 16.65f);
        tableInstance.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
        tableInstance.transform.localScale = Vector3.one * 103.2707f;
        tableInstance.isStatic = true;

        EditorUtility.SetDirty(tableInstance);
        log.AppendLine($"Placed Lobby_RoundTable at {tableInstance.transform.position} (static = true)");

        // 6. Adjust Player and PlayerSpawnPoint so player spawns standing right in front of the table
        Vector3 playerSpawnPos = new Vector3(-86.50f, 2.35f, 16.65f);
        Quaternion playerSpawnRot = Quaternion.Euler(0f, 90f, 0f); // facing East toward the round table and grand entrance

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = playerSpawnPos;
            player.transform.rotation = playerSpawnRot;
            EditorUtility.SetDirty(player);
            log.AppendLine($"Updated Player position to {playerSpawnPos} facing table.");
        }

        GameObject spawnPoint = GameObject.Find("PlayerSpawnPoint");
        if (spawnPoint != null)
        {
            spawnPoint.transform.position = playerSpawnPos;
            spawnPoint.transform.rotation = playerSpawnRot;
            EditorUtility.SetDirty(spawnPoint);
            log.AppendLine($"Updated PlayerSpawnPoint position to {playerSpawnPos}.");
        }

        // 7. Save Scene
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        log.AppendLine("Scene marked dirty and saved successfully!");

        string outPath = Path.Combine(Application.dataPath, "RoundTablePlacementOutput.txt");
        File.WriteAllText(outPath, log.ToString());
        Debug.Log(log.ToString());
    }
}
