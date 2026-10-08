using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class DeployAdditionalWalkingNPCs
{
    private struct NPCConfig
    {
        public string goName;
        public string modelAsset;
        public string idleFbx;
        public string walkFbx;
        public string talkFbx;
        public string inPlaceWalkPath;
        public string ctrlPath;
        public string matPath;
        public string texPath;
        public Quaternion meshRotation;
        public bool isFemale;
        public Vector3 startPos;
        public Vector3[] waypoints;
    }

    private const float SCALE = 37.0f;

    private static readonly NPCConfig[] Configs = new[]
    {
        new NPCConfig
        {
            goName = "ClearanceNPC_Rachel",
            modelAsset = "Assets/NPC Assets/Models/rachel.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/rachel/IdleRachel.fbx",
            walkFbx = "Assets/NPC Assets/Animations/animateds/rachel/WalkingRachel.fbx",
            talkFbx = "Assets/NPC Assets/Animations/animateds/rachel/TalkingRachel.fbx",
            inPlaceWalkPath = "Assets/NPC Assets/Animations/InPlace/Walkingrachel_InPlace.anim",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Rachel_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Rachel_Mat.mat",
            texPath = "Assets/NPC Assets/Texture/rachel_texture.png",
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = true,
            startPos = new Vector3(-84.00f, 2.43f, -10.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 2.43f, -12.00f),
                new Vector3(-84.00f, 2.43f, -1.00f),
                new Vector3(-80.94f, 2.43f, 9.38f),
                new Vector3(-84.00f, 2.43f, 20.00f),
                new Vector3(-85.50f, 2.43f, 27.50f),
                new Vector3(-84.00f, 2.43f, 12.00f),
                new Vector3(-84.00f, 2.43f, 2.00f)
            }
        },
        new NPCConfig
        {
            goName = "ClearanceNPC_Bene",
            modelAsset = "Assets/NPC Assets/Models/bene.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/bene/IdleBene.fbx",
            walkFbx = "Assets/NPC Assets/Animations/animateds/bene/WalkingBene.fbx",
            talkFbx = "Assets/NPC Assets/Animations/animateds/bene/TalkingBene.fbx",
            inPlaceWalkPath = "Assets/NPC Assets/Animations/InPlace/Walkingbene_InPlace.anim",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Bene_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Bene_Mat.mat",
            texPath = "Assets/NPC Assets/Texture/bene-texture.png",
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = false,
            startPos = new Vector3(-84.00f, 8.35f, -10.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 8.35f, -12.00f),
                new Vector3(-84.00f, 8.35f, -1.00f),
                new Vector3(-84.00f, 8.35f, 11.00f),
                new Vector3(-84.00f, 8.35f, 22.00f),
                new Vector3(-84.00f, 8.35f, 29.50f),
                new Vector3(-84.00f, 8.35f, 16.00f),
                new Vector3(-84.00f, 8.35f, 4.00f)
            }
        },
        new NPCConfig
        {
            goName = "ClearanceNPC_JP",
            modelAsset = "Assets/NPC Assets/Models/jp.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/jp/IdleJP.fbx",
            walkFbx = "Assets/NPC Assets/Animations/animateds/jp/WalkingJP.fbx",
            talkFbx = "Assets/NPC Assets/Animations/animateds/jp/TalkingJP.fbx",
            inPlaceWalkPath = "Assets/NPC Assets/Animations/InPlace/Walkingjp_InPlace.anim",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_JP_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/JP_Mat.mat",
            texPath = "Assets/NPC Assets/Texture/jp_texture.png",
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = false,
            startPos = new Vector3(-84.00f, 14.34f, -10.00f),
            waypoints = new[]
            {
                new Vector3(-84.00f, 14.34f, -10.00f),
                new Vector3(-84.00f, 14.34f, 6.00f),
                new Vector3(-84.00f, 14.34f, 21.00f),
                new Vector3(-84.00f, 14.34f, 36.00f),
                new Vector3(-84.00f, 14.34f, 27.00f),
                new Vector3(-84.00f, 14.34f, 12.00f),
                new Vector3(-84.00f, 14.34f, -1.00f)
            }
        }
    };

    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return Vector3.Distance(p, a + t * ab);
    }

    [MenuItem("Tools/Klirans/Deploy Additional Walking NPCs")]
    public static void Deploy()
    {
        Debug.Log("=== DEPLOYING ADDITIONAL WALKING NPCS (RACHEL, BENE, JP) ===");

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        // Ensure directories exist
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/RiggedMeshes"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "RiggedMeshes");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/AnimatorControllers"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "AnimatorControllers");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/Materials"))
            AssetDatabase.CreateFolder("Assets/NPC Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/Animations/InPlace"))
            AssetDatabase.CreateFolder("Assets/NPC Assets/Animations", "InPlace");

        // Find ClearanceNPCs parent
        var parentGO = GameObject.Find("ClearanceNPCs");
        if (parentGO == null)
        {
            var lobby = GameObject.Find("LobbyArea");
            parentGO = new GameObject("ClearanceNPCs");
            if (lobby != null) parentGO.transform.SetParent(lobby.transform, false);
        }

        // Find or create ProctorWaypoints parent
        var wpRoot = GameObject.Find("ProctorWaypoints");
        if (wpRoot == null) wpRoot = new GameObject("ProctorWaypoints");

        string[] boneNames = new[]
        {
            "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
            "mixamorig:Neck", "mixamorig:Head",
            "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
            "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot",
            "mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot"
        };

        foreach (var cfg in Configs)
        {
            // 1. Ensure InPlace Walk Animation exists
            CreateInPlaceWalkClip(cfg.walkFbx, cfg.inPlaceWalkPath);

            // 2. Ensure Material exists
            Material mat = GetOrCreateMaterial(cfg.matPath, cfg.texPath);

            // 3. Ensure AnimatorController exists
            RuntimeAnimatorController animCtrl = GetOrCreateAnimatorController(cfg);

            // 4. Ensure Rigged Skinned Mesh exists
            Mesh riggedMesh = GetOrCreateRiggedMesh(cfg, boneNames);

            // 5. Create or configure Scene GameObject
            GameObject npcGO = GameObject.Find(cfg.goName);
            if (npcGO == null)
            {
                npcGO = new GameObject(cfg.goName);
                npcGO.transform.SetParent(parentGO.transform, false);
            }

            // Clean previous child objects
            var oldArmature = npcGO.transform.Find("Armature");
            if (oldArmature != null) Object.DestroyImmediate(oldArmature.gameObject);

            // 6. Instantiate Armature from Idle FBX
            var idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.idleFbx);
            var armature = Object.Instantiate(idlePrefab, npcGO.transform);
            armature.name = "Armature";
            armature.transform.localPosition = Vector3.zero;
            armature.transform.localRotation = Quaternion.identity;
            armature.transform.localScale = Vector3.one;

            var hips = armature.transform.Find("mixamorig:Hips");
            var allTransforms = hips.GetComponentsInChildren<Transform>();
            var boneMap = new Dictionary<string, Transform>();
            boneMap["mixamorig:Hips"] = hips;
            foreach (var t in allTransforms) boneMap[t.name] = t;

            var bones = new List<Transform>();
            foreach (var bn in boneNames)
            {
                if (boneMap.ContainsKey(bn)) bones.Add(boneMap[bn]);
            }

            // 7. Add SkinnedMesh child
            var smrGO = new GameObject("SkinnedMesh");
            smrGO.transform.SetParent(armature.transform, false);
            var smr = smrGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = riggedMesh;
            smr.bones = bones.ToArray();
            smr.rootBone = hips;
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;

            // 8. Scale Armature to human scale (~1.65m height)
            armature.transform.localScale = Vector3.one * SCALE;

            // 9. Configure Animator on Armature
            var anim = armature.GetComponent<Animator>();
            if (anim == null) anim = armature.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.enabled = true;

            // 10. Sample NavMesh start position
            Vector3 startPos = cfg.startPos;
            if (NavMesh.SamplePosition(startPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                startPos = hit.position;
            }
            npcGO.transform.position = startPos;
            npcGO.transform.rotation = Quaternion.identity;

            // 11. Configure NavMeshAgent
            var agent = npcGO.GetComponent<NavMeshAgent>();
            if (agent == null) agent = npcGO.AddComponent<NavMeshAgent>();
            agent.height = 1.6f;
            agent.radius = 0.28f;
            agent.speed = 1.4f;
            agent.angularSpeed = 240f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;
            agent.updateRotation = true;
            agent.baseOffset = 0f;
            agent.autoTraverseOffMeshLink = false;
            agent.enabled = true;
            agent.Warp(startPos);

            // 12. Create and hook up Waypoints
            var wpTransforms = new List<Transform>();
            for (int w = 0; w < cfg.waypoints.Length; w++)
            {
                string wpName = $"{cfg.goName}_WP_{w}";
                var existingWP = wpRoot.transform.Find(wpName);
                GameObject wpGO;
                if (existingWP != null)
                {
                    wpGO = existingWP.gameObject;
                }
                else
                {
                    wpGO = new GameObject(wpName);
                    wpGO.transform.SetParent(wpRoot.transform, false);
                }

                Vector3 wpPos = cfg.waypoints[w];
                if (NavMesh.SamplePosition(wpPos, out NavMeshHit wpHit, 4.0f, NavMesh.AllAreas))
                {
                    wpPos = wpHit.position;
                }
                wpGO.transform.position = wpPos;
                wpTransforms.Add(wpGO.transform);
            }

            // 13. Configure ProctorAI
            var ai = npcGO.GetComponent<ProctorAI>();
            if (ai == null) ai = npcGO.AddComponent<ProctorAI>();
            ai.waypoints = wpTransforms.ToArray();
            ai.walkSpeed = 1.4f;
            ai.enableProceduralWalk = false;
            ai.loopInOrder = true;
            ai.minIdleTime = 2f;
            ai.maxIdleTime = 4f;
            ai.footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
            ai.footstepVolume = 0.20f;
            ai.footstepMinDistance = 1.2f;
            ai.footstepMaxDistance = 14.0f;
            ai.enableCreepyStare = true;
            ai.stareChance = 0.85f;
            ai.stareMaxDistance = 11.0f;
            ai.maxHeadTurnAngle = 95.0f;
            ai.headTurnSpeed = 4.0f;

            // 14. Solid CapsuleCollider
            var col = npcGO.GetComponent<CapsuleCollider>();
            if (col == null) col = npcGO.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.28f;
            col.height = cfg.isFemale ? 1.64f : 1.72f;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            // 15. Kinematic Rigidbody
            var rb = npcGO.GetComponent<Rigidbody>();
            if (rb == null) rb = npcGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            EditorUtility.SetDirty(npcGO);
            Debug.Log($"[DEPLOYED WALKING NPC] {cfg.goName}: Rigged, Skinned, Scaled {SCALE}, Pos={startPos}, Waypoints={wpTransforms.Count}, AnimCtrl={animCtrl.name}");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        Debug.Log("=== ALL ADDITIONAL WALKING NPCS SUCCESSFULLY DEPLOYED! ===");
    }

    private static void CreateInPlaceWalkClip(string srcFbxPath, string outAnimPath)
    {
        if (File.Exists(outAnimPath)) return;

        var objs = AssetDatabase.LoadAllAssetsAtPath(srcFbxPath);
        AnimationClip srcClip = null;
        foreach (var o in objs)
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { srcClip = c; break; }
        }
        if (srcClip == null)
        {
            Debug.LogError("Source walk clip not found at " + srcFbxPath);
            return;
        }

        var newClip = new AnimationClip();
        newClip.name = Path.GetFileNameWithoutExtension(outAnimPath);

        var bindings = AnimationUtility.GetCurveBindings(srcClip);
        foreach (var b in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(srcClip, b);
            if (b.path == "mixamorig:Hips" && b.propertyName == "m_LocalPosition.z")
            {
                float initZ = curve.keys[0].value;
                curve = new AnimationCurve(new Keyframe(0f, initZ), new Keyframe(srcClip.length, initZ));
            }
            newClip.SetCurve(b.path, b.type, b.propertyName, curve);
        }

        var settings = AnimationUtility.GetAnimationClipSettings(newClip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(newClip, settings);

        AssetDatabase.CreateAsset(newClip, outAnimPath);
        EditorUtility.SetDirty(newClip);
    }

    private static Material GetOrCreateMaterial(string matPath, string texPath)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        return mat;
    }

    private static RuntimeAnimatorController GetOrCreateAnimatorController(NPCConfig cfg)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(cfg.ctrlPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(cfg.ctrlPath);
        }

        ctrl.parameters = new AnimatorControllerParameter[0];
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Talking", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        while (sm.states.Length > 0)
        {
            sm.RemoveState(sm.states[0].state);
        }

        AnimationClip idleClip = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(cfg.idleFbx))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { idleClip = c; break; }
        }

        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(cfg.inPlaceWalkPath);

        AnimationClip talkClip = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(cfg.talkFbx))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { talkClip = c; break; }
        }

        BlendTree bt;
        var locoState = ctrl.CreateBlendTreeInController("Locomotion", out bt, 0);
        bt.blendType = BlendTreeType.Simple1D;
        bt.blendParameter = "Speed";
        bt.AddChild(idleClip, 0f);
        bt.AddChild(walkClip, 1f);
        sm.defaultState = locoState;

        if (talkClip != null)
        {
            var talkState = sm.AddState("Talking");
            talkState.motion = talkClip;

            var toTalk = locoState.AddTransition(talkState);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            toTalk.duration = 0.15f;
            toTalk.hasExitTime = false;

            var toLoco = talkState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            toLoco.duration = 0.2f;
            toLoco.hasExitTime = false;
        }

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static Mesh GetOrCreateRiggedMesh(NPCConfig cfg, string[] boneNames)
    {
        string meshAssetPath = $"Assets/NPC Assets/RiggedMeshes/{cfg.goName}_Mesh.asset";
        Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
        if (newMesh != null) return newMesh;

        Mesh rawMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cfg.modelAsset);
        GameObject idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.idleFbx);
        if (rawMesh == null || idlePrefab == null)
        {
            Debug.LogError($"Missing raw mesh or idle prefab for {cfg.goName}!");
            return null;
        }

        var tempArmature = Object.Instantiate(idlePrefab);
        tempArmature.transform.position = Vector3.zero;
        tempArmature.transform.rotation = Quaternion.identity;
        tempArmature.transform.localScale = Vector3.one;

        var hips = tempArmature.transform.Find("mixamorig:Hips");
        var allTransforms = hips.GetComponentsInChildren<Transform>();
        var boneMap = new Dictionary<string, Transform>();
        boneMap["mixamorig:Hips"] = hips;
        foreach (var t in allTransforms) boneMap[t.name] = t;

        var bones = new List<Transform>();
        foreach (var bn in boneNames)
        {
            if (boneMap.ContainsKey(bn)) bones.Add(boneMap[bn]);
        }

        Quaternion q = cfg.meshRotation;
        Vector3[] origVerts = rawMesh.vertices;
        Vector3[] origNorms = rawMesh.normals;
        Vector3[] alignedVerts = new Vector3[origVerts.Length];
        Vector3[] alignedNorms = new Vector3[origNorms.Length];

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < origVerts.Length; i++)
        {
            Vector3 v = q * origVerts[i];
            alignedVerts[i] = v;
            alignedNorms[i] = q * origNorms[i];
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }

        for (int i = 0; i < origVerts.Length; i++)
        {
            alignedVerts[i].y -= minY;
        }

        Vector3[] segA = new Vector3[bones.Count];
        Vector3[] segB = new Vector3[bones.Count];
        for (int b = 0; b < bones.Count; b++)
        {
            Transform boneT = bones[b];
            segA[b] = tempArmature.transform.InverseTransformPoint(boneT.position);
            string bn = boneT.name;

            if (bn == "mixamorig:Hips")
            {
                Vector3 crotch = (boneMap.ContainsKey("mixamorig:LeftUpLeg") && boneMap.ContainsKey("mixamorig:RightUpLeg"))
                    ? (boneMap["mixamorig:LeftUpLeg"].position + boneMap["mixamorig:RightUpLeg"].position) * 0.5f
                    : boneT.position + Vector3.down * 0.002f;
                segB[b] = tempArmature.transform.InverseTransformPoint(crotch);
            }
            else if (bn == "mixamorig:Spine2" && boneMap.ContainsKey("mixamorig:Neck"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:Neck"].position);
            }
            else if (bn == "mixamorig:Head" && boneMap.ContainsKey("mixamorig:HeadTop_End"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:HeadTop_End"].position);
            }
            else if (bn == "mixamorig:LeftFoot" && boneMap.ContainsKey("mixamorig:LeftToeBase"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:LeftToeBase"].position);
            }
            else if (bn == "mixamorig:RightFoot" && boneMap.ContainsKey("mixamorig:RightToeBase"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:RightToeBase"].position);
            }
            else if (bn == "mixamorig:LeftHand" && boneMap.ContainsKey("mixamorig:LeftHandIndex1"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:LeftHandIndex1"].position);
            }
            else if (bn == "mixamorig:RightHand" && boneMap.ContainsKey("mixamorig:RightHandIndex1"))
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneMap["mixamorig:RightHandIndex1"].position);
            }
            else if (boneT.childCount > 0)
            {
                segB[b] = tempArmature.transform.InverseTransformPoint(boneT.GetChild(0).position);
            }
            else if (boneT.parent != null)
            {
                Vector3 dir = (boneT.position - boneT.parent.position).normalized;
                segB[b] = tempArmature.transform.InverseTransformPoint(boneT.position + dir * 0.003f);
            }
            else
            {
                segB[b] = segA[b] + Vector3.up * 0.003f;
            }
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

                if (bName.Contains("Left") && p.x > 0.0002f)
                {
                    d += (p.x - 0.0002f) * 15f;
                }
                else if (bName.Contains("Right") && p.x < -0.0002f)
                {
                    d += (-p.x - 0.0002f) * 15f;
                }

                if (bName.Contains("Arm") || bName.Contains("ForeArm") || bName.Contains("Hand"))
                {
                    if (Mathf.Abs(p.x) < 0.0032f)
                    {
                        d += (0.0032f - Mathf.Abs(p.x)) * 10f;
                    }
                }

                if (bName.Contains("Hips"))
                {
                    if (p.y > 0.016f && p.y < 0.026f && Mathf.Abs(p.x) < 0.0022f)
                    {
                        d *= 0.35f;
                    }
                }

                if (cfg.isFemale && (bName.Contains("UpLeg") || bName.Contains("Leg")))
                {
                    if (p.y > 0.015f && p.y < 0.024f)
                    {
                        d *= 1.7f;
                    }
                }

                if (d < bestD0)
                {
                    bestD1 = bestD0; bestB1 = bestB0;
                    bestD0 = d;      bestB0 = b;
                }
                else if (d < bestD1)
                {
                    bestD1 = d;      bestB1 = b;
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

        newMesh = new Mesh();
        newMesh.name = $"{cfg.goName}_Mesh";
        newMesh.vertices = alignedVerts;
        newMesh.normals = alignedNorms;
        newMesh.triangles = rawMesh.triangles;
        newMesh.uv = rawMesh.uv;
        newMesh.boneWeights = weights;

        Matrix4x4[] bindposes = new Matrix4x4[bones.Count];
        for (int b = 0; b < bones.Count; b++)
        {
            bindposes[b] = bones[b].worldToLocalMatrix * tempArmature.transform.localToWorldMatrix;
        }
        newMesh.bindposes = bindposes;
        newMesh.RecalculateBounds();

        AssetDatabase.CreateAsset(newMesh, meshAssetPath);
        EditorUtility.SetDirty(newMesh);

        Object.DestroyImmediate(tempArmature);
        return newMesh;
    }
}
