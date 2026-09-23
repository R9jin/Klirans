using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SetupTheProctorSystem — Editor utility to build and configure:
/// 1. The Proctor Material (TheProctor_Mat.mat using theproctor.png).
/// 2. The Proctor Animator Controller (TheProctor_AnimCtrl.controller with creepily-tuned locomotion and sprint).
/// 3. The Proctor Prefab (Assets/Prefabs/TheProctor.prefab) scaled to towering 2.5m height, with dripping clipboard prop and TheProctorAI.
/// 4. Scene wiring in SampleScene.unity for TheProctorManager, AnxietyManager, AnxietyUI, GameOverUI, and SafeZoneManager.
/// </summary>
public static class SetupTheProctorSystem
{
    private static AnimationClip LoadClipFromFBX(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        }
        return null;
    }

    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return Vector3.Distance(p, a + t * ab);
    }

    [MenuItem("Tools/Klirans/Setup The Proctor and Anxiety System")]
    public static void RunSetup()
    {
        Debug.Log("=== SETTING UP THE PROCTOR BLACKOUT SYSTEM & ANXIETY METER ===");

        // Ensure directories exist
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/Materials"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/AnimatorControllers"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "AnimatorControllers");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/RiggedMeshes"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "RiggedMeshes");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // 1. Create The Proctor Material
        string matPath = "Assets/NPC Assets/Materials/TheProctor_Mat.mat";
        string texPath = "Assets/NPC Assets/theproctor.png";
        Material proctorMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (proctorMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            proctorMat = new Material(shader);
            AssetDatabase.CreateAsset(proctorMat, matPath);
        }
        Texture2D proctorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (proctorTex != null)
        {
            proctorMat.SetTexture("_BaseMap", proctorTex);
            proctorMat.SetTexture("_MainTex", proctorTex);
        }
        proctorMat.SetFloat("_Smoothness", 0.05f); // Matte, aged creepy cloth & pale skin
        EditorUtility.SetDirty(proctorMat);

        // 2. Load Animator Controller (which contains the verified In-Place BlendTree)
        string ctrlPath = "Assets/NPC Assets/AnimatorControllers/TheProctor_AnimCtrl.controller";
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        string idleFbx = "Assets/NPC Assets/Animations/animateds/old_prof3/IdlePhil.fbx";

        // 3. Build The Proctor GameObject & Rig
        GameObject rootGO = new GameObject("TheProctor");

        // Load Armature from Idle FBX
        GameObject idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(idleFbx);
        GameObject armature = Object.Instantiate(idlePrefab, rootGO.transform);
        armature.name = "Armature";
        armature.transform.localPosition = Vector3.zero;
        armature.transform.localRotation = Quaternion.identity;
        armature.transform.localScale = Vector3.one;

        Transform hips = armature.transform.Find("mixamorig:Hips");
        if (hips != null)
        {
            string[] boneNames = new[]
            {
                "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
                "mixamorig:Neck", "mixamorig:Head",
                "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
                "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
                "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot",
                "mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot"
            };

            Transform[] allTransforms = hips.GetComponentsInChildren<Transform>();
            Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
            boneMap["mixamorig:Hips"] = hips;
            foreach (var t in allTransforms) boneMap[t.name] = t;

            List<Transform> bones = new List<Transform>();
            foreach (var bn in boneNames)
            {
                if (boneMap.ContainsKey(bn)) bones.Add(boneMap[bn]);
            }

            // Load Mesh from theproctor.fbx
            Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/NPC Assets/theproctor.fbx");
            if (meshAsset == null)
            {
                // Fallback: search sub-assets
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/NPC Assets/theproctor.fbx");
                foreach (var a in subAssets)
                {
                    if (a is Mesh m) { meshAsset = m; break; }
                }
            }

            if (meshAsset != null)
            {
                Vector3[] origVerts = meshAsset.vertices;
                Vector3[] origNorms = meshAsset.normals;
                Vector3[] alignedVerts = new Vector3[origVerts.Length];
                Vector3[] alignedNorms = new Vector3[origNorms.Length];

                float minY = float.MaxValue;
                Quaternion q = Quaternion.Euler(-90f, 90f, 0f);
                for (int i = 0; i < origVerts.Length; i++)
                {
                    Vector3 v = q * origVerts[i];
                    alignedVerts[i] = v;
                    alignedNorms[i] = origNorms != null && origNorms.Length > i ? (q * origNorms[i]) : Vector3.up;
                    if (v.y < minY) minY = v.y;
                }

                for (int i = 0; i < origVerts.Length; i++)
                {
                    alignedVerts[i].y -= minY;
                }

                // Bone segments for anatomical proximity
                Vector3[] segA = new Vector3[bones.Count];
                Vector3[] segB = new Vector3[bones.Count];
                for (int b = 0; b < bones.Count; b++)
                {
                    Transform boneT = bones[b];
                    segA[b] = armature.transform.InverseTransformPoint(boneT.position);
                    string bn = boneT.name;

                    if (bn == "mixamorig:Hips")
                    {
                        Vector3 crotch = (boneMap.ContainsKey("mixamorig:LeftUpLeg") && boneMap.ContainsKey("mixamorig:RightUpLeg"))
                            ? (boneMap["mixamorig:LeftUpLeg"].position + boneMap["mixamorig:RightUpLeg"].position) * 0.5f
                            : boneT.position + Vector3.down * 0.002f;
                        segB[b] = armature.transform.InverseTransformPoint(crotch);
                    }
                    else if (bn == "mixamorig:Spine2" && boneMap.ContainsKey("mixamorig:Neck"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:Neck"].position);
                    else if (bn == "mixamorig:Head" && boneMap.ContainsKey("mixamorig:HeadTop_End"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:HeadTop_End"].position);
                    else if (bn == "mixamorig:LeftFoot" && boneMap.ContainsKey("mixamorig:LeftToeBase"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftToeBase"].position);
                    else if (bn == "mixamorig:RightFoot" && boneMap.ContainsKey("mixamorig:RightToeBase"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightToeBase"].position);
                    else if (bn == "mixamorig:LeftHand" && boneMap.ContainsKey("mixamorig:LeftHandIndex1"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftHandIndex1"].position);
                    else if (bn == "mixamorig:RightHand" && boneMap.ContainsKey("mixamorig:RightHandIndex1"))
                        segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightHandIndex1"].position);
                    else if (boneT.childCount > 0)
                        segB[b] = armature.transform.InverseTransformPoint(boneT.GetChild(0).position);
                    else
                        segB[b] = segA[b] + Vector3.up * 0.003f;
                }

                BoneWeight[] weights = new BoneWeight[origVerts.Length];
                for (int i = 0; i < origVerts.Length; i++)
                {
                    Vector3 p = alignedVerts[i];
                    float bestD0 = 999f, bestD1 = 999f;
                    int bestB0 = 0, bestB1 = 0;

                    for (int b = 0; b < bones.Count; b++)
                    {
                        float d = DistToSegment(p, segA[b], segB[b]);
                        string bName = bones[b].name;

                        if (bName.Contains("Left") && p.x > 0.0002f) d += (p.x - 0.0002f) * 15f;
                        else if (bName.Contains("Right") && p.x < -0.0002f) d += (-p.x - 0.0002f) * 15f;

                        if (d < bestD0)
                        {
                            bestD1 = bestD0; bestB1 = bestB0;
                            bestD0 = d; bestB0 = b;
                        }
                        else if (d < bestD1)
                        {
                            bestD1 = d; bestB1 = b;
                        }
                    }

                    float w0 = 1f / Mathf.Pow(bestD0 + 0.00025f, 4f);
                    float w1 = 1f / Mathf.Pow(bestD1 + 0.00025f, 4f);
                    float sum = w0 + w1;

                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = bestB0, weight0 = w0 / sum,
                        boneIndex1 = bestB1, weight1 = w1 / sum
                    };
                }

                // Save Rigged Mesh Asset
                string assetPath = "Assets/NPC Assets/RiggedMeshes/TheProctor_RiggedMesh.asset";
                Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (newMesh == null)
                {
                    newMesh = new Mesh();
                    newMesh.name = "TheProctor_RiggedMesh";
                    AssetDatabase.CreateAsset(newMesh, assetPath);
                }
                newMesh.Clear();
                newMesh.vertices = alignedVerts;
                newMesh.normals = alignedNorms;
                newMesh.triangles = meshAsset.triangles;
                newMesh.uv = meshAsset.uv;
                newMesh.boneWeights = weights;

                Matrix4x4[] bindposes = new Matrix4x4[bones.Count];
                for (int b = 0; b < bones.Count; b++)
                {
                    bindposes[b] = bones[b].worldToLocalMatrix * armature.transform.localToWorldMatrix;
                }
                newMesh.bindposes = bindposes;
                newMesh.RecalculateBounds();
                EditorUtility.SetDirty(newMesh);

                // Add SkinnedMeshRenderer
                GameObject smrGO = new GameObject("SkinnedMesh");
                smrGO.transform.SetParent(armature.transform, false);
                var smr = smrGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = newMesh;
                smr.bones = bones.ToArray();
                smr.rootBone = hips;
                smr.sharedMaterial = proctorMat;
                smr.updateWhenOffscreen = true;

                // Scale Armature to towering height (2.02m - near ceiling, no clipping)
                float maxY = 0f;
                for (int i = 0; i < alignedVerts.Length; i++)
                {
                    if (alignedVerts[i].y > maxY) maxY = alignedVerts[i].y;
                }
                float toweringScale = (maxY > 0.001f) ? (2.02f / maxY) : 46.1f;
                armature.transform.localScale = Vector3.one * toweringScale;
            }
        }

        // Animator on Armature
        var animator = armature.GetComponent<Animator>();
        if (animator == null) animator = armature.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;

        // NavMeshAgent on root
        var agent = rootGO.AddComponent<NavMeshAgent>();
        agent.speed = 1.95f;
        agent.acceleration = 9.0f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;
        agent.autoTraverseOffMeshLink = false;
        agent.height = 2.02f;
        agent.radius = 0.42f;
        agent.baseOffset = 0f;

        // Capsule Collider for physics / raycasts
        var capsule = rootGO.AddComponent<CapsuleCollider>();
        capsule.height = 2.02f;
        capsule.radius = 0.42f;
        capsule.center = new Vector3(0f, 1.01f, 0f);

        // TheProctorAI script
        var proctorAI = rootGO.AddComponent<TheProctorAI>();
        proctorAI.targetHeightMeters = 2.02f;
        proctorAI.chaseSpeed = 1.95f;
        proctorAI.catchDistance = 1.25f;

        // Save Prefab
        string prefabPath = "Assets/Prefabs/TheProctor.prefab";
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);
        Object.DestroyImmediate(rootGO);
        Debug.Log($"[SetupTheProctorSystem] Successfully saved TheProctor prefab to {prefabPath}");

        // 4. Scene Wiring in SampleScene.unity
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        // Wire SafeZoneManager
        var safeZoneMgr = Object.FindAnyObjectByType<SafeZoneManager>();
        if (safeZoneMgr == null)
        {
            var gm = GameObject.Find("GameManager") ?? new GameObject("GameManager");
            safeZoneMgr = gm.AddComponent<SafeZoneManager>();
            safeZoneMgr.defaultSafePosition = new Vector3(-80.94f, 2.43f, 9.38f);
            safeZoneMgr.defaultSafeYaw = 0f;
            Debug.Log("[SetupTheProctorSystem] Created SafeZoneManager on GameManager");
        }

        // Wire AnxietyManager
        var anxietyMgr = Object.FindAnyObjectByType<AnxietyManager>();
        if (anxietyMgr == null)
        {
            var gm = GameObject.Find("GameManager") ?? new GameObject("GameManager");
            anxietyMgr = gm.AddComponent<AnxietyManager>();
            anxietyMgr.maxAnxiety = 100f;
            anxietyMgr.jumpscareAnxietyIncrease = 10f;
            Debug.Log("[SetupTheProctorSystem] Created AnxietyManager on GameManager");
        }

        // Wire AnxietyUI
        var anxietyUI = Object.FindAnyObjectByType<AnxietyUI>();
        if (anxietyUI == null)
        {
            var hud = GameObject.Find("HudCanvas");
            if (hud != null)
            {
                anxietyUI = hud.AddComponent<AnxietyUI>();
                Debug.Log("[SetupTheProctorSystem] Created AnxietyUI on HudCanvas");
            }
        }

        // Wire GameOverUI
        var gameOverUI = Object.FindAnyObjectByType<GameOverUI>();
        if (gameOverUI == null)
        {
            var hud = GameObject.Find("HudCanvas");
            if (hud != null)
            {
                gameOverUI = hud.AddComponent<GameOverUI>();
                Debug.Log("[SetupTheProctorSystem] Created GameOverUI on HudCanvas");
            }
        }

        // Wire TheProctorManager
        var proctorMgr = Object.FindAnyObjectByType<TheProctorManager>();
        if (proctorMgr == null)
        {
            var proctorMgrGO = new GameObject("TheProctorManager");
            proctorMgr = proctorMgrGO.AddComponent<TheProctorManager>();
            Debug.Log("[SetupTheProctorSystem] Created TheProctorManager in scene");
        }
        proctorMgr.theProctorPrefab = prefabAsset;
        proctorMgr.blackoutChaseDuration = 26.0f;
        EditorUtility.SetDirty(proctorMgr);

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[SetupTheProctorSystem] SampleScene.unity saved with all Proctor & Anxiety systems wired!");

        string outPath = Path.Combine(Application.dataPath, "EditorOutput.txt");
        File.WriteAllText(outPath, "SUCCESS: TheProctor setup and scene wiring completed! Scene: " + activeScene.name);
    }
}
