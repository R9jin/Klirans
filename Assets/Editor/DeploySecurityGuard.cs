using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeploySecurityGuard
{
    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return Vector3.Distance(p, a + t * ab);
    }

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

    private static Material GetOrCreateMaterial(string matPath, string texPath)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            mat = new Material(litShader);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Smoothness", 0.1f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static AnimatorController CreateOrGetAnimatorController(string ctrlPath, string idleFbx, string talkFbx)
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        }

        ctrl.parameters = new AnimatorControllerParameter[0];
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Talking", AnimatorControllerParameterType.Bool);

        var rootStateMachine = ctrl.layers[0].stateMachine;
        while (rootStateMachine.states.Length > 0)
        {
            rootStateMachine.RemoveState(rootStateMachine.states[0].state);
        }

        AnimationClip idleClip = LoadClipFromFBX(idleFbx);
        AnimationClip talkClip = LoadClipFromFBX(talkFbx);

        // Locomotion state with Idle
        var locomotionState = rootStateMachine.AddState("Locomotion");
        locomotionState.motion = idleClip;
        rootStateMachine.defaultState = locomotionState;

        // Talking State
        if (talkClip != null)
        {
            var talkState = rootStateMachine.AddState("Talking");
            talkState.motion = talkClip;

            // Locomotion -> Talking
            var toTalk = locomotionState.AddTransition(talkState);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            toTalk.duration = 0.2f;
            toTalk.hasExitTime = false;

            // Talking -> Locomotion
            var toLoco = talkState.AddTransition(locomotionState);
            toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            toLoco.duration = 0.2f;
            toLoco.hasExitTime = false;
        }

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    [MenuItem("Tools/Klirans/Deploy Security Guard Visuals and Animations")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/RiggedMeshes"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "RiggedMeshes");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/AnimatorControllers"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "AnimatorControllers");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/Materials"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "Materials");

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== DEPLOY SECURITY GUARD AUDIT ===");

        const string modelAssetPath = "Assets/NPC Assets/Animations/animateds/guard.fbx";
        const string idleFbxPath = "Assets/NPC Assets/Animations/animateds/drei/IdleDrei.fbx";
        const string talkFbxPath = "Assets/NPC Assets/Animations/animateds/drei/TalkingDrei.fbx";
        const string texPath = "Assets/NPC Assets/Animations/Texture/guard.png";
        const string matPath = "Assets/NPC Assets/Materials/SecurityGuard_Mat.mat";
        const string ctrlPath = "Assets/NPC Assets/AnimatorControllers/SecurityGuard_Ctrl.controller";
        const string riggedMeshPath = "Assets/NPC Assets/RiggedMeshes/SecurityGuard_Mesh.asset";

        GameObject guardGO = GameObject.Find("Security_Guard_NPC");
        if (guardGO == null)
        {
            Debug.LogError("Security_Guard_NPC not found in scene!");
            return;
        }

        // Clean up legacy primitive cap parts if present
        Transform cap = guardGO.transform.Find("Guard_Cap");
        if (cap != null) Object.DestroyImmediate(cap.gameObject);
        Transform capVisor = guardGO.transform.Find("Guard_CapVisor");
        if (capVisor != null) Object.DestroyImmediate(capVisor.gameObject);

        // Remove legacy Armature if present
        Transform oldArmature = guardGO.transform.Find("Armature");
        if (oldArmature != null) Object.DestroyImmediate(oldArmature.gameObject);

        // Load mesh & assets
        Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(modelAssetPath);
        GameObject idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(idleFbxPath);
        Material mat = GetOrCreateMaterial(matPath, texPath);
        AnimatorController ctrl = CreateOrGetAnimatorController(ctrlPath, idleFbxPath, talkFbxPath);

        if (meshAsset == null)
        {
            Debug.LogError("Guard meshAsset not found at " + modelAssetPath);
            return;
        }
        if (idlePrefab == null)
        {
            Debug.LogError("Idle prefab not found at " + idleFbxPath);
            return;
        }

        // Instantiate Armature from Idle FBX
        GameObject armature = Object.Instantiate(idlePrefab, guardGO.transform);
        armature.name = "Armature";
        armature.transform.localPosition = Vector3.zero;
        armature.transform.localRotation = Quaternion.identity;
        armature.transform.localScale = Vector3.one;

        Transform hips = armature.transform.Find("mixamorig:Hips");
        if (hips == null)
        {
            Debug.LogError("mixamorig:Hips not found in " + idleFbxPath);
            return;
        }

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

        // Align raw mesh vertices to skeleton space
        Vector3[] origVerts = meshAsset.vertices;
        Vector3[] origNorms = meshAsset.normals;
        Vector3[] alignedVerts = new Vector3[origVerts.Length];
        Vector3[] alignedNorms = new Vector3[origNorms.Length];

        Quaternion q = Quaternion.Euler(-90f, 90f, 0f);
        float minY = float.MaxValue;
        for (int i = 0; i < origVerts.Length; i++)
        {
            Vector3 v = q * origVerts[i];
            alignedVerts[i] = v;
            alignedNorms[i] = (origNorms != null && origNorms.Length > i) ? (q * origNorms[i]) : Vector3.up;
            if (v.y < minY) minY = v.y;
        }

        // Soles sit flush on the floor (Y = 0)
        for (int i = 0; i < origVerts.Length; i++)
        {
            alignedVerts[i].y -= minY;
        }

        // Bone segments for anatomical weighting
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
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:Neck"].position);
            }
            else if (bn == "mixamorig:Head" && boneMap.ContainsKey("mixamorig:HeadTop_End"))
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:HeadTop_End"].position);
            }
            else if (bn == "mixamorig:LeftFoot" && boneMap.ContainsKey("mixamorig:LeftToeBase"))
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftToeBase"].position);
            }
            else if (bn == "mixamorig:RightFoot" && boneMap.ContainsKey("mixamorig:RightToeBase"))
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightToeBase"].position);
            }
            else if (bn == "mixamorig:LeftHand" && boneMap.ContainsKey("mixamorig:LeftHandIndex1"))
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftHandIndex1"].position);
            }
            else if (bn == "mixamorig:RightHand" && boneMap.ContainsKey("mixamorig:RightHandIndex1"))
            {
                segB[b] = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightHandIndex1"].position);
            }
            else if (boneT.childCount > 0)
            {
                segB[b] = armature.transform.InverseTransformPoint(boneT.GetChild(0).position);
            }
            else if (boneT.parent != null)
            {
                Vector3 dir = (boneT.position - boneT.parent.position).normalized;
                segB[b] = armature.transform.InverseTransformPoint(boneT.position + dir * 0.003f);
            }
            else
            {
                segB[b] = segA[b] + Vector3.up * 0.003f;
            }
        }

        // Anatomical proximity bone weighting
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

                if (bName.Contains("Arm") || bName.Contains("ForeArm") || bName.Contains("Hand"))
                {
                    if (Mathf.Abs(p.x) < 0.0032f) d += (0.0032f - Mathf.Abs(p.x)) * 10f;
                }

                if (bName.Contains("Hips"))
                {
                    if (p.y > 0.016f && p.y < 0.026f && Mathf.Abs(p.x) < 0.0022f) d *= 0.35f;
                }

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

        // Save Rigged Mesh
        Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(riggedMeshPath);
        if (newMesh == null)
        {
            newMesh = new Mesh();
            newMesh.name = "SecurityGuard_Mesh";
            AssetDatabase.CreateAsset(newMesh, riggedMeshPath);
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
        smr.sharedMaterial = mat;
        smr.updateWhenOffscreen = true;

        // Scale Armature to match realistic height (~1.76m)
        float maxY = 0f;
        for (int i = 0; i < alignedVerts.Length; i++)
        {
            if (alignedVerts[i].y > maxY) maxY = alignedVerts[i].y;
        }
        float charScale = (maxY > 0.001f) ? (1.76f / maxY) : 37.0f;
        armature.transform.localScale = Vector3.one * charScale;

        // Configure Animator
        var anim = armature.GetComponent<Animator>();
        if (anim == null) anim = armature.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.enabled = true;

        // Configure root CapsuleCollider
        var col = guardGO.GetComponent<CapsuleCollider>();
        if (col == null) col = guardGO.AddComponent<CapsuleCollider>();
        col.isTrigger = false;
        col.radius = 0.30f;
        col.height = 1.76f;
        col.center = new Vector3(0f, 0.88f, 0f);

        // Adjust chair position slightly back so it doesn't clip with the standing guard
        var chairSeat = GameObject.Find("Chair_Seat");
        var chairBack = GameObject.Find("Chair_Back");
        var chairPost = GameObject.Find("Chair_Post");
        if (chairSeat != null && chairSeat.transform.position.x < -75.75f)
        {
            // Shift chair back by ~0.35m in +X (towards gate)
            float dx = 0.35f;
            chairSeat.transform.position += new Vector3(dx, 0f, 0f);
            if (chairBack != null) chairBack.transform.position += new Vector3(dx, 0f, 0f);
            if (chairPost != null) chairPost.transform.position += new Vector3(dx, 0f, 0f);
            EditorUtility.SetDirty(chairSeat);
            if (chairBack != null) EditorUtility.SetDirty(chairBack);
            if (chairPost != null) EditorUtility.SetDirty(chairPost);
        }

        EditorUtility.SetDirty(guardGO);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        log.AppendLine("Successfully deployed Security Guard visual rig, textures, animator controller, and updated scene!");
        Debug.Log(log.ToString());
    }
}
