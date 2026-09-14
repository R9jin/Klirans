using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using System.Collections.Generic;

public static class RestoreNormalProctors
{
    struct OriginalData
    {
        public string goName;
        public string modelAsset; // e.g. Assets/NPC Assets/Models/drei.fbx
        public string matName;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 startPos;
        public Vector3[] waypoints;
    }

    // Scale down from original (50, 50, 50) to (36, 36, 36) so they are human height (~1.60m)
    // and easily fit inside classroom doorways.
    static readonly float SCALE = 36.0f;

    // Actual building floor elevations:
    // 1st Floor: Y = 2.43
    // 2nd Floor: Y = 8.35
    // 3rd Floor: Y = 14.34
    static readonly OriginalData[] NPCs = new[]
    {
        // 1. Drei: 1st Floor & travels up stairs to 2nd Floor and back
        new OriginalData
        {
            goName = "ClearanceNPC_Drei",
            modelAsset = "Assets/NPC Assets/Models/drei.fbx",
            matName = "Drei_Mat",
            localRotation = new Quaternion(-0.5388191f, 0.44077247f, 0.45455813f, 0.55567133f),
            localScale = new Vector3(SCALE * 2.0f, SCALE * 2.0f, SCALE * 1.894f),
            startPos = new Vector3(-80.94f, 2.43f, 9.38f),
            waypoints = new[]
            {
                new Vector3(-80.94f, 2.43f, 9.38f),   // 1F Lobby
                new Vector3(-84.00f, 2.43f, -2.00f),  // 1F Hallway South
                new Vector3(-84.00f, 2.43f, 6.00f),   // 1F Hallway Mid
                new Vector3(-85.50f, 2.43f, 12.65f),  // 1F MainStairs Bottom
                new Vector3(-85.50f, 8.35f, 20.63f),  // 2F MainStairs Top
                new Vector3(-84.00f, 8.35f, 30.00f),  // 2F Hallway North
                new Vector3(-84.00f, 8.35f, 0.00f),   // 2F Hallway South
                new Vector3(-85.50f, 8.35f, 20.63f),  // 2F MainStairs Top (return)
                new Vector3(-85.50f, 2.43f, 12.65f),  // 1F MainStairs Bottom (return)
                new Vector3(-80.94f, 2.43f, 9.38f),   // 1F Lobby return
            }
        },

        // 2. Glad: 1st Floor North & Classrooms 105, 108
        new OriginalData
        {
            goName = "ClearanceNPC_Glad",
            modelAsset = "Assets/NPC Assets/Models/glad.fbx",
            matName = "Glad_Mat",
            localRotation = new Quaternion(-0.72964656f, -0.078530654f, -0.07269202f, 0.6753997f),
            localScale = new Vector3(SCALE, SCALE, SCALE),
            startPos = new Vector3(-77.34f, 2.43f, 20.00f),
            waypoints = new[]
            {
                new Vector3(-77.34f, 2.43f, 20.00f),  // 1F Lobby North
                new Vector3(-77.34f, 2.43f, 13.00f),  // 1F Lobby South
                new Vector3(-80.94f, 2.43f, 9.38f),   // 1F Lobby Entrance
                new Vector3(-84.00f, 2.43f, 5.00f),   // 1F Hallway South
                new Vector3(-84.00f, 2.43f, -5.00f),  // 1F Hallway Far South
                new Vector3(-84.00f, 2.43f, 15.00f),  // 1F Hallway Center
                new Vector3(-84.00f, 2.43f, 26.00f),  // 1F Hallway North
            }
        },

        // 3. Ira: Starts on 2nd Floor North & travels up stairs to 3rd Floor and back
        new OriginalData
        {
            goName = "ClearanceNPC_Ira",
            modelAsset = "Assets/NPC Assets/Models/ira.fbx",
            matName = "Ira_Mat",
            localRotation = new Quaternion(-0.6775131f, -0.28200567f, -0.26103926f, 0.6271422f),
            localScale = new Vector3(SCALE, SCALE, SCALE),
            startPos = new Vector3(-84.00f, 8.35f, 25.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 8.35f, 30.00f),  // 2F North Hallway
                new Vector3(-84.00f, 8.35f, 18.00f),  // 2F Hallway Mid
                new Vector3(-85.50f, 8.35f, 12.65f),  // 2F MainStairs Bottom
                new Vector3(-85.50f, 14.34f, 20.63f), // 3F MainStairs Top
                new Vector3(-84.00f, 14.34f, 30.00f), // 3F North Hallway
                new Vector3(-84.00f, 14.34f, 42.00f), // 3F Far North Hallway
                new Vector3(-85.50f, 14.34f, 20.63f), // 3F MainStairs Top (return)
                new Vector3(-85.50f, 8.35f, 12.65f),  // 2F MainStairs Bottom (return)
            }
        },

        // 4. Jessa: Starts on 2nd Floor South & travels down stairs to 1st Floor and back
        new OriginalData
        {
            goName = "ClearanceNPC_Jessa",
            modelAsset = "Assets/NPC Assets/Models/jessa.fbx",
            matName = "Jessa_Mat",
            localRotation = new Quaternion(-0.6878669f, 0.25571516f, 0.23670359f, 0.63672626f),
            localScale = new Vector3(SCALE, SCALE, SCALE),
            startPos = new Vector3(-84.00f, 8.35f, 2.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 8.35f, -5.00f),  // 2F South Hallway
                new Vector3(-84.00f, 8.35f, 10.00f),  // 2F Mid Hallway
                new Vector3(-85.50f, 8.35f, 20.63f),  // 2F MainStairs Top
                new Vector3(-85.50f, 2.43f, 12.65f),  // 1F MainStairs Bottom
                new Vector3(-80.94f, 2.43f, 9.38f),   // 1F Lobby
                new Vector3(-84.00f, 2.43f, -2.00f),  // 1F Hallway South
                new Vector3(-85.50f, 2.43f, 12.65f),  // 1F MainStairs Bottom (return)
                new Vector3(-85.50f, 8.35f, 20.63f),  // 2F MainStairs Top (return)
            }
        },

        // 5. Josua: Starts on 3rd Floor South & travels down stairs to 2nd Floor and back
        new OriginalData
        {
            goName = "ClearanceNPC_Josua",
            modelAsset = "Assets/NPC Assets/Models/josua.fbx",
            matName = "Josua_Mat",
            localRotation = new Quaternion(-0.7330281f, -0.03494421f, -0.032346092f, 0.6785296f),
            localScale = new Vector3(SCALE, SCALE, SCALE),
            startPos = new Vector3(-84.00f, 14.34f, 2.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 14.34f, -5.00f), // 3F South Hallway
                new Vector3(-84.00f, 14.34f, 10.00f), // 3F Mid Hallway
                new Vector3(-85.50f, 14.34f, 20.63f), // 3F MainStairs Top
                new Vector3(-85.50f, 8.35f, 12.65f),  // 2F MainStairs Bottom
                new Vector3(-84.00f, 8.35f, 0.00f),   // 2F South Hallway
                new Vector3(-85.50f, 8.35f, 12.65f),  // 2F MainStairs Bottom (return)
                new Vector3(-85.50f, 14.34f, 20.63f), // 3F MainStairs Top (return)
            }
        },

        // 6. Niel: Starts on 3rd Floor North & patrols 3rd Floor hallways
        new OriginalData
        {
            goName = "ClearanceNPC_Niel",
            modelAsset = "Assets/NPC Assets/Models/niel.fbx",
            matName = "Niel_Mat",
            localRotation = new Quaternion(-0.5632355f, -0.47044343f, -0.4354673f, 0.5213607f),
            localScale = new Vector3(SCALE, SCALE, SCALE),
            startPos = new Vector3(-84.00f, 14.34f, 26.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 14.34f, 35.00f), // 3F North Hallway
                new Vector3(-84.00f, 14.34f, 45.00f), // 3F Far North Hallway
                new Vector3(-84.00f, 14.34f, 18.00f), // 3F Mid Hallway
                new Vector3(-84.00f, 14.34f, -5.00f), // 3F South Hallway
                new Vector3(-84.00f, 14.34f, -15.00f),// 3F Far South Hallway
                new Vector3(-84.00f, 14.34f, 10.00f), // 3F Mid Hallway return
            }
        }
    };

    [MenuItem("Tools/Klirans/Restore Normal Proctors")]
    public static void Run()
    {
        Debug.Log("=== RESTORING PROCTORS: CLEAN MODELS, NO DISTORTION, STAIR NAVIGATION & MULTI-FLOOR PATROL ===");

        // 1. Build Physical Incline Stair NavMeshLinks
        SetupStairLinks();

        // 2. Setup Waypoint Container
        var wpParent = GameObject.Find("ProctorWaypoints");
        if (wpParent == null) wpParent = new GameObject("ProctorWaypoints");
        while (wpParent.transform.childCount > 0)
        {
            Object.DestroyImmediate(wpParent.transform.GetChild(0).gameObject);
        }

        // 3. Process Each NPC
        foreach (var data in NPCs)
        {
            var npcGO = GameObject.Find(data.goName);
            if (npcGO == null)
            {
                Debug.LogError($"NPC {data.goName} not found!");
                continue;
            }

            // A. Remove any deformed rigs, Armatures, SkinnedMeshes, or Animators
            var oldArmature = npcGO.transform.Find("Armature");
            if (oldArmature != null) Object.DestroyImmediate(oldArmature.gameObject);

            var oldVR = npcGO.transform.Find("VisualRoot");
            if (oldVR != null) Object.DestroyImmediate(oldVR.gameObject);

            var oldHips = npcGO.transform.Find("mixamorig:Hips");
            if (oldHips != null) Object.DestroyImmediate(oldHips.gameObject);

            var oldSkel = npcGO.transform.Find("Skeleton");
            if (oldSkel != null) Object.DestroyImmediate(oldSkel.gameObject);

            var oldSmr = npcGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in oldSmr) Object.DestroyImmediate(s.gameObject);

            var animators = npcGO.GetComponentsInChildren<Animator>(true);
            foreach (var a in animators) Object.DestroyImmediate(a);

            // B. Clean old Model child
            var oldModel = npcGO.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

            // C. Load original FBX mesh and authentic material
            Mesh originalMesh = null;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(data.modelAsset);
            foreach (var a in allAssets) if (a is Mesh m) { originalMesh = m; break; }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/NPC Assets/Materials/{data.matName}.mat");

            // D. Create clean Model child (rigid transform, pristine mesh and textures)
            GameObject modelGO = new GameObject("Model");
            modelGO.transform.SetParent(npcGO.transform, false);
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = data.localRotation;
            modelGO.transform.localScale = data.localScale;

            var mf = modelGO.AddComponent<MeshFilter>();
            mf.sharedMesh = originalMesh;

            var mr = modelGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            modelGO.SetActive(true);

            // E. Position NPC on its assigned floor NavMesh
            NavMeshHit hit;
            Vector3 startPos = data.startPos;
            if (NavMesh.SamplePosition(startPos, out hit, 4.0f, NavMesh.AllAreas))
            {
                startPos = hit.position;
            }
            npcGO.transform.position = startPos;
            npcGO.transform.rotation = Quaternion.identity;

            // F. Configure NavMeshAgent
            var agent = npcGO.GetComponent<NavMeshAgent>();
            if (agent == null) agent = npcGO.AddComponent<NavMeshAgent>();
            agent.height = 1.55f;
            agent.radius = 0.28f;
            agent.speed = 1.4f;
            agent.angularSpeed = 240f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;
            agent.updateRotation = true;
            agent.autoTraverseOffMeshLink = false; // ProctorAI handles smooth stair incline traversal!
            agent.enabled = true;
            agent.Warp(startPos);

            // G. Setup Multi-Floor Waypoints
            List<Transform> wpTransforms = new List<Transform>();
            for (int i = 0; i < data.waypoints.Length; i++)
            {
                Vector3 targetPos = data.waypoints[i];
                if (NavMesh.SamplePosition(targetPos, out hit, 4.0f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                GameObject wp = new GameObject($"{data.goName}_WP_{i}");
                wp.transform.SetParent(wpParent.transform);
                wp.transform.position = targetPos;
                wpTransforms.Add(wp.transform);
            }

            // H. Configure ProctorAI with procedural locomotion
            var ai = npcGO.GetComponent<ProctorAI>();
            if (ai == null) ai = npcGO.AddComponent<ProctorAI>();
            ai.waypoints = wpTransforms.ToArray();
            ai.loopInOrder = true;
            ai.walkSpeed = 1.4f;
            ai.minIdleTime = 2f;
            ai.maxIdleTime = 4f;
            ai.waypointTolerance = 0.5f;
            ai.enableProceduralWalk = true;
            ai.walkBobFrequency = 7.5f;
            ai.walkBobAmount = 0.04f;
            ai.walkSwayAngle = 3.5f;
            ai.idleBreatheRate = 2f;
            ai.idleBreatheAmount = 0.012f;

            EditorUtility.SetDirty(npcGO);
            Debug.Log($"[RESTORED] {data.goName} upright, clean texture {data.matName}, Y={startPos.y:F2}, Waypoints={wpTransforms.Count}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("\n=== ALL 6 PROCTORS RESTORED & MULTI-FLOOR STAIR NAVIGATION ACTIVE! ===");
    }

    private static void SetupStairLinks()
    {
        var oldLinks = GameObject.Find("StairNavMeshLinks");
        if (oldLinks != null) Object.DestroyImmediate(oldLinks);

        GameObject linksHolder = new GameObject("StairNavMeshLinks");

        // 1F -> 2F
        // Flight A: 1F bottom (-85.50, 2.43, 12.65) -> Mid-Landing (-91.40, 5.25, 16.64)
        var go1A = new GameObject("MainStairs_1F_FlightA");
        go1A.transform.SetParent(linksHolder.transform);
        go1A.transform.position = new Vector3(-85.5f, 2.43f, 12.65f);
        var link1A = go1A.AddComponent<NavMeshLink>();
        link1A.startPoint = Vector3.zero;
        link1A.endPoint = new Vector3(-91.4f - (-85.5f), 5.25f - 2.43f, 16.64f - 12.65f);
        link1A.width = 1.5f;
        link1A.bidirectional = true;

        // Flight B: Mid-Landing (-91.40, 5.25, 16.64) -> 2F top (-85.50, 8.35, 20.63)
        var go1B = new GameObject("MainStairs_1F_FlightB");
        go1B.transform.SetParent(linksHolder.transform);
        go1B.transform.position = new Vector3(-91.4f, 5.25f, 16.64f);
        var link1B = go1B.AddComponent<NavMeshLink>();
        link1B.startPoint = Vector3.zero;
        link1B.endPoint = new Vector3(-85.5f - (-91.4f), 8.35f - 5.25f, 20.63f - 16.64f);
        link1B.width = 1.5f;
        link1B.bidirectional = true;

        // 2F -> 3F
        // Flight A: 2F bottom (-85.50, 8.35, 12.65) -> Mid-Landing (-91.40, 11.25, 16.64)
        var go2A = new GameObject("MainStairs_2F_FlightA");
        go2A.transform.SetParent(linksHolder.transform);
        go2A.transform.position = new Vector3(-85.5f, 8.35f, 12.65f);
        var link2A = go2A.AddComponent<NavMeshLink>();
        link2A.startPoint = Vector3.zero;
        link2A.endPoint = new Vector3(-91.4f - (-85.5f), 11.25f - 8.35f, 16.64f - 12.65f);
        link2A.width = 1.5f;
        link2A.bidirectional = true;

        // Flight B: Mid-Landing (-91.40, 11.25, 16.64) -> 3F top (-85.50, 14.34, 20.63)
        var go2B = new GameObject("MainStairs_2F_FlightB");
        go2B.transform.SetParent(linksHolder.transform);
        go2B.transform.position = new Vector3(-91.4f, 11.25f, 16.64f);
        var link2B = go2B.AddComponent<NavMeshLink>();
        link2B.startPoint = Vector3.zero;
        link2B.endPoint = new Vector3(-85.5f - (-91.4f), 14.34f - 11.25f, 20.63f - 16.64f);
        link2B.width = 1.5f;
        link2B.bidirectional = true;

        Debug.Log("[STAIRS] NavMeshLinks created for 1F <-> 2F <-> 3F along physical staircase steps.");
    }
}
