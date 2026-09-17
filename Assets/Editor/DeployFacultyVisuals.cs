using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeployFacultyVisuals
{
    struct FacultyDef
    {
        public string goName;
        public string roleTitle;
        public Vector3 position;
        public Quaternion rotation;
        public string modelAsset;
        public string idleFbx;
        public string walkFbx;
        public string talkFbx;
        public string texPath;
        public string matPath;
        public string ctrlPath;
        public bool isFemale;
    }

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
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static AnimatorController CreateOrGetAnimatorController(string ctrlPath, string idleFbx, string walkFbx, string talkFbx)
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        }

        // Setup parameters
        ctrl.parameters = new AnimatorControllerParameter[0];
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Talking", AnimatorControllerParameterType.Bool);

        var rootStateMachine = ctrl.layers[0].stateMachine;

        while (rootStateMachine.states.Length > 0)
        {
            rootStateMachine.RemoveState(rootStateMachine.states[0].state);
        }

        AnimationClip idleClip = LoadClipFromFBX(idleFbx);
        AnimationClip walkClip = LoadClipFromFBX(walkFbx);
        AnimationClip talkClip = LoadClipFromFBX(talkFbx);

        // Locomotion BlendTree State
        var locomotionState = rootStateMachine.AddState("Locomotion");
        var blendTree = new BlendTree
        {
            name = "LocomotionBlend",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        if (idleClip != null) blendTree.AddChild(idleClip, 0f);
        if (walkClip != null) blendTree.AddChild(walkClip, 1f);
        else if (idleClip != null) blendTree.AddChild(idleClip, 1f);

        locomotionState.motion = blendTree;
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

    [MenuItem("Tools/Klirans/Deploy Faculty Visuals and Rigs")]
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
        log.AppendLine("=== DEPLOY FACULTY VISUALS & RIGS AUDIT ===");

        FacultyDef[] faculties = new[]
        {
            // 1. Room 308 - Head Librarian (3F)
            new FacultyDef
            {
                goName = "Signatory_Librarian",
                roleTitle = "Head Librarian",
                position = new Vector3(-76.0f, 14.30f, 44.15f),
                rotation = Quaternion.Euler(0f, 270f, 0f),
                modelAsset = "Assets/NPC Assets/Models/drei.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/old_prof3/IdlePhil.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/old_prof3/WalkingPhil.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/old_prof3/TalkingPhil.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/old_prof_texture3.png",
                matPath = "Assets/NPC Assets/Materials/Librarian_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_Librarian_Ctrl.controller",
                isFemale = false
            },
            // 2. Room 207 - Guidance Counselor (2F)
            new FacultyDef
            {
                goName = "Signatory_GuidanceCounselor",
                roleTitle = "Guidance Counselor",
                position = new Vector3(-89.5f, 8.35f, 42.20f),
                rotation = Quaternion.Euler(0f, 90f, 0f),
                modelAsset = "Assets/NPC Assets/Models/ira.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/rachel/IdleRachel.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/rachel/WalkingRachel.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/rachel/TalkingRachel.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/rachel_texture.png",
                matPath = "Assets/NPC Assets/Materials/GuidanceCounselor_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_Guidance_Ctrl.controller",
                isFemale = true
            },
            // 3. Room 104 - University Registrar (1F)
            // Behind service window workstation (-81.3, 2.35, 4.90), facing West (-X)
            new FacultyDef
            {
                goName = "Signatory_Registrar",
                roleTitle = "University Registrar",
                position = new Vector3(-81.3f, 2.35f, 4.90f),
                rotation = Quaternion.Euler(0f, 270f, 0f),
                modelAsset = "Assets/NPC Assets/Models/drei.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/old_prof/IdleProf1.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/old_prof/CatwalkProf1.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/old_prof/TalkingProf1.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/old_prof_texture1.png",
                matPath = "Assets/NPC Assets/Materials/Registrar_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_Registrar_Ctrl.controller",
                isFemale = false
            },
            // 4. Room 202 - CCS Dean (2F)
            new FacultyDef
            {
                goName = "Signatory_CCSDean",
                roleTitle = "CCS Dean / Dept Head",
                position = new Vector3(-77.8f, 8.35f, -3.60f),
                rotation = Quaternion.Euler(0f, 180f, 0f),
                modelAsset = "Assets/NPC Assets/Models/drei.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/renz/IdleRenz.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/renz/WalkingRenz.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/renz/TalkingRenz.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/renz_texture.png",
                matPath = "Assets/NPC Assets/Materials/CCSDean_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_CCSDean_Ctrl.controller",
                isFemale = false
            },
            // 5. Room 102 - University Cashier (1F)
            // Behind service window workstation (-81.3, 2.35, -7.10), facing West (-X)
            new FacultyDef
            {
                goName = "Signatory_Cashier",
                roleTitle = "University Cashier",
                position = new Vector3(-81.3f, 2.35f, -7.10f),
                rotation = Quaternion.Euler(0f, 270f, 0f),
                modelAsset = "Assets/NPC Assets/Models/drei.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/old_prof2/IdleProf2.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/old_prof2/CatwalkProf2.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/old_prof2/TalkingProf2.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/old_prof_texture2.png",
                matPath = "Assets/NPC Assets/Materials/Cashier_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_Cashier_Ctrl.controller",
                isFemale = false
            },
            // 6. Room 103 - Executive Vice President (1F)
            new FacultyDef
            {
                goName = "Signatory_EVP",
                roleTitle = "Executive Vice President",
                position = new Vector3(-88.5f, 2.35f, 7.80f),
                rotation = Quaternion.Euler(0f, 180f, 0f),
                modelAsset = "Assets/NPC Assets/Models/drei.fbx",
                idleFbx = "Assets/NPC Assets/Animations/animateds/old_prof/IdleProf1.fbx",
                walkFbx = "Assets/NPC Assets/Animations/animateds/old_prof/CatwalkProf1.fbx",
                talkFbx = "Assets/NPC Assets/Animations/animateds/old_prof/TalkingProf1.fbx",
                texPath = "Assets/NPC Assets/Animations/Texture/old_prof_texture.png",
                matPath = "Assets/NPC Assets/Materials/EVP_Mat.mat",
                ctrlPath = "Assets/NPC Assets/AnimatorControllers/Signatory_EVP_Ctrl.controller",
                isFemale = false
            }
        };

        string[] boneNames = new[]
        {
            "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
            "mixamorig:Neck", "mixamorig:Head",
            "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
            "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot",
            "mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot"
        };

        const float SCALE = 37.0f;

        foreach (var def in faculties)
        {
            GameObject staffGO = GameObject.Find(def.goName);
            if (staffGO == null)
            {
                log.AppendLine($"ERROR: {def.goName} not found in scene!");
                continue;
            }

            // Set proper position and rotation
            staffGO.transform.position = def.position;
            staffGO.transform.rotation = def.rotation;

            // Remove legacy VisualModel if present
            Transform oldVM = staffGO.transform.Find("VisualModel");
            if (oldVM != null) Object.DestroyImmediate(oldVM.gameObject);

            // Remove legacy Armature if present
            Transform oldArmature = staffGO.transform.Find("Armature");
            if (oldArmature != null) Object.DestroyImmediate(oldArmature.gameObject);

            // Load Mesh & Assets
            Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(def.modelAsset);
            GameObject idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.idleFbx);
            Material mat = GetOrCreateMaterial(def.matPath, def.texPath);
            AnimatorController ctrl = CreateOrGetAnimatorController(def.ctrlPath, def.idleFbx, def.walkFbx, def.talkFbx);

            if (meshAsset == null)
            {
                log.AppendLine($"ERROR: meshAsset not found at {def.modelAsset} for {def.goName}!");
                continue;
            }
            if (idlePrefab == null)
            {
                log.AppendLine($"ERROR: idlePrefab not found at {def.idleFbx} for {def.goName}!");
                continue;
            }

            // Instantiate Armature from Idle FBX
            GameObject armature = Object.Instantiate(idlePrefab, staffGO.transform);
            armature.name = "Armature";
            armature.transform.localPosition = Vector3.zero;
            armature.transform.localRotation = Quaternion.identity;
            armature.transform.localScale = Vector3.one;

            Transform hips = armature.transform.Find("mixamorig:Hips");
            if (hips == null)
            {
                log.AppendLine($"ERROR: mixamorig:Hips not found in {def.idleFbx}!");
                continue;
            }

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
            Quaternion q = Quaternion.Euler(-90f, 90f, 0f);
            Vector3[] origVerts = meshAsset.vertices;
            Vector3[] origNorms = meshAsset.normals;
            Vector3[] alignedVerts = new Vector3[origVerts.Length];
            Vector3[] alignedNorms = new Vector3[origNorms.Length];

            float minY = float.MaxValue;
            for (int i = 0; i < origVerts.Length; i++)
            {
                Vector3 v = q * origVerts[i];
                alignedVerts[i] = v;
                alignedNorms[i] = q * origNorms[i];
                if (v.y < minY) minY = v.y;
            }

            // Soles of shoes sit flush on the floor (Y = 0)
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

                    if (def.isFemale && (bName.Contains("UpLeg") || bName.Contains("Leg")))
                    {
                        if (p.y > 0.015f && p.y < 0.024f) d *= 1.7f;
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
            string assetPath = $"Assets/NPC Assets/RiggedMeshes/{def.goName}_Mesh.asset";
            Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (newMesh == null)
            {
                newMesh = new Mesh();
                newMesh.name = $"{def.goName}_Mesh";
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
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;

            // Scale Armature to match human height (~1.65m)
            armature.transform.localScale = Vector3.one * SCALE;

            // Configure Animator
            var anim = armature.GetComponent<Animator>();
            if (anim == null) anim = armature.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.enabled = true;

            // CapsuleCollider on root
            var col = staffGO.GetComponent<CapsuleCollider>();
            if (col == null) col = staffGO.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.30f;
            col.height = def.isFemale ? 1.64f : 1.72f;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            EditorUtility.SetDirty(staffGO);
            log.AppendLine($"Successfully deployed 3D visual rig, textures, and anim controller for {def.goName} ({def.roleTitle}) at {def.position}!");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        File.WriteAllText("Assets/FacultyVisualsOutput.txt", log.ToString());
        Debug.Log(log.ToString());
    }
}
