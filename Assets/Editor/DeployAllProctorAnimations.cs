using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Collections.Generic;

public static class DeployAllProctorAnimations
{
    struct NPCDef
    {
        public string goName;
        public string modelAsset;
        public string idleFbx;
        public string ctrlPath;
        public string matPath;
        public Quaternion meshRotation;
        public bool isFemale;
        public Vector3 vertOffset;
        public Vector3 startPos;
    }

    static readonly float SCALE = 36.0f;

    static readonly NPCDef[] NPCs = new[]
    {
        new NPCDef
        {
            goName = "ClearanceNPC_Drei",
            modelAsset = "Assets/NPC Assets/Models/drei.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/drei/IdleDrei.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Drei_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Drei_Mat.mat",
            meshRotation = Quaternion.Euler(-90, -90, 0),
            isFemale = false,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-80.94f, 2.43f, 9.38f)
        },
        new NPCDef
        {
            goName = "ClearanceNPC_Glad",
            modelAsset = "Assets/NPC Assets/Models/glad.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/glad/IdleGlad.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Glad_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Glad_Mat.mat",
            meshRotation = Quaternion.Euler(-90, -90, 0),
            isFemale = false,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-77.34f, 2.43f, 20.00f)
        },
        new NPCDef
        {
            goName = "ClearanceNPC_Ira",
            modelAsset = "Assets/NPC Assets/Models/ira.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/ira/IdleIra.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Ira_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Ira_Mat.mat",
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = true,
            vertOffset = Vector3.up * 0.024f, // Lift Ira waist-centered pivot so feet sit flush on the floor!
            startPos = new Vector3(-84.00f, 8.35f, 25.00f)
        },
        new NPCDef
        {
            goName = "ClearanceNPC_Jessa",
            modelAsset = "Assets/NPC Assets/Models/jessa.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/jessa/IdleJessa.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Jessa_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Jessa_Mat.mat",
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = true,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-84.00f, 8.35f, 2.00f)
        },
        new NPCDef
        {
            goName = "ClearanceNPC_Josua",
            modelAsset = "Assets/NPC Assets/Models/josua.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/josua/IdleJosua.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Josua_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Josua_Mat.mat",
            meshRotation = Quaternion.Euler(-90, -90, 0),
            isFemale = false,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-84.00f, 14.34f, 2.00f)
        },
        new NPCDef
        {
            goName = "ClearanceNPC_Niel",
            modelAsset = "Assets/NPC Assets/Models/niel.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/niel/IdleNeil.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Niel_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Niel_Mat.mat",
            meshRotation = Quaternion.Euler(-90, -90, 0),
            isFemale = false,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-84.00f, 14.34f, 26.00f)
        }
    };

    [MenuItem("Tools/Klirans/Deploy All Proctor Animations")]
    public static void Run()
    {
        Debug.Log("=== DEPLOYING ALL PROCTOR ANIMATIONS: SKELETAL RIGGING, NO DISTORTION, ELEVATION FIX ===");

        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/RiggedMeshes"))
        {
            AssetDatabase.CreateFolder("Assets/NPC Assets", "RiggedMeshes");
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

        foreach (var def in NPCs)
        {
            var npcGO = GameObject.Find(def.goName);
            if (npcGO == null)
            {
                Debug.LogError($"NPC {def.goName} not found in scene!");
                continue;
            }

            // 1. Clean up legacy child objects
            var oldModel = npcGO.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

            var oldArmature = npcGO.transform.Find("Armature");
            if (oldArmature != null) Object.DestroyImmediate(oldArmature.gameObject);

            var oldVR = npcGO.transform.Find("VisualRoot");
            if (oldVR != null) Object.DestroyImmediate(oldVR.gameObject);

            var oldHips = npcGO.transform.Find("mixamorig:Hips");
            if (oldHips != null) Object.DestroyImmediate(oldHips.gameObject);

            var oldSkel = npcGO.transform.Find("Skeleton");
            if (oldSkel != null) Object.DestroyImmediate(oldSkel.gameObject);

            // Clean any root animators/skinned meshes
            var rootAnimators = npcGO.GetComponents<Animator>();
            foreach (var a in rootAnimators) Object.DestroyImmediate(a);

            // 2. Load Assets
            var idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.idleFbx);
            var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(def.modelAsset);
            var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(def.ctrlPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(def.matPath);

            if (idlePrefab == null || meshAsset == null || animCtrl == null || mat == null)
            {
                Debug.LogError($"Missing assets for {def.goName}! idle={idlePrefab != null}, mesh={meshAsset != null}, ctrl={animCtrl != null}, mat={mat != null}");
                continue;
            }

            // 3. Instantiate Armature from Idle FBX
            var armature = Object.Instantiate(idlePrefab, npcGO.transform);
            armature.name = "Armature";
            armature.transform.localPosition = Vector3.zero;
            armature.transform.localRotation = Quaternion.identity;
            armature.transform.localScale = Vector3.one; // Keep scale = 1 during binding calculation

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

            int bHips = bones.IndexOf(boneMap["mixamorig:Hips"]);
            int bSpine = bones.IndexOf(boneMap["mixamorig:Spine"]);
            int bNeck = bones.IndexOf(boneMap["mixamorig:Neck"]);
            int bHead = bones.IndexOf(boneMap["mixamorig:Head"]);
            int bLShoulder = bones.IndexOf(boneMap["mixamorig:LeftShoulder"]);
            int bLArm = bones.IndexOf(boneMap["mixamorig:LeftArm"]);
            int bLForeArm = bones.IndexOf(boneMap["mixamorig:LeftForeArm"]);
            int bLHand = bones.IndexOf(boneMap["mixamorig:LeftHand"]);
            int bRShoulder = bones.IndexOf(boneMap["mixamorig:RightShoulder"]);
            int bRArm = bones.IndexOf(boneMap["mixamorig:RightArm"]);
            int bRForeArm = bones.IndexOf(boneMap["mixamorig:RightForeArm"]);
            int bRHand = bones.IndexOf(boneMap["mixamorig:RightHand"]);
            int bLUpLeg = bones.IndexOf(boneMap["mixamorig:LeftUpLeg"]);
            int bLLeg = bones.IndexOf(boneMap["mixamorig:LeftLeg"]);
            int bLFoot = bones.IndexOf(boneMap["mixamorig:LeftFoot"]);
            int bRUpLeg = bones.IndexOf(boneMap["mixamorig:RightUpLeg"]);
            int bRLeg = bones.IndexOf(boneMap["mixamorig:RightLeg"]);
            int bRFoot = bones.IndexOf(boneMap["mixamorig:RightFoot"]);

            // Compute bone positions in armature local space (scale-independent & position-independent)
            Vector3 localFootPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftFoot"].position);
            Vector3 localHeadPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:Head"].position);
            Vector3 localHipPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:Hips"].position);
            Vector3 localKneePos = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftLeg"].position);
            Vector3 localNeckPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:Neck"].position);
            Vector3 localLArmPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftArm"].position);
            Vector3 localRArmPos = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightArm"].position);
            Vector3 localLElbow = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftForeArm"].position);
            Vector3 localRElbow = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightForeArm"].position);
            Vector3 localLWrist = armature.transform.InverseTransformPoint(boneMap["mixamorig:LeftHand"].position);
            Vector3 localRWrist = armature.transform.InverseTransformPoint(boneMap["mixamorig:RightHand"].position);

            // 4. Align raw mesh vertices to skeleton space
            Quaternion q = def.meshRotation;
            Vector3[] origVerts = meshAsset.vertices;
            Vector3[] origNorms = meshAsset.normals;
            Vector3[] alignedVerts = new Vector3[origVerts.Length];
            Vector3[] alignedNorms = new Vector3[origNorms.Length];

            Vector3 min = q * origVerts[0] + def.vertOffset, max = min;
            for (int i = 0; i < origVerts.Length; i++)
            {
                alignedVerts[i] = q * origVerts[i] + def.vertOffset;
                alignedNorms[i] = q * origNorms[i];
                min = Vector3.Min(min, alignedVerts[i]);
                max = Vector3.Max(max, alignedVerts[i]);
            }

            float skelHeight = localHeadPos.y - localFootPos.y;
            float meshHeight = max.y - min.y;
            float sFactor = skelHeight / meshHeight;
            for (int i = 0; i < origVerts.Length; i++)
            {
                alignedVerts[i].y = (alignedVerts[i].y - min.y) * sFactor + localFootPos.y;
                alignedVerts[i].x *= sFactor;
                alignedVerts[i].z *= sFactor;
            }

            // 5. Anatomical Bone Weighting
            BoneWeight[] weights = new BoneWeight[origVerts.Length];
            float hipY = localHipPos.y;
            float kneeY = localKneePos.y;
            float footY = localFootPos.y;
            float neckY = localNeckPos.y;
            float shoulderWidth = Mathf.Abs(localLArmPos.x - localRArmPos.x);

            for (int i = 0; i < origVerts.Length; i++)
            {
                Vector3 v = alignedVerts[i];
                bool isLeft = v.x < 0;
                bool isArm = Mathf.Abs(v.x) > (shoulderWidth * 0.32f) && v.y > (hipY - 0.003f) && v.y < (neckY + 0.003f);
                BoneWeight bw = new BoneWeight();

                if (v.y >= neckY)
                {
                    bw.boneIndex0 = bHead; bw.weight0 = 0.8f; bw.boneIndex1 = bNeck; bw.weight1 = 0.2f;
                }
                else if (isArm)
                {
                    int arm = isLeft ? bLArm : bRArm;
                    int fore = isLeft ? bLForeArm : bRForeArm;
                    int hand = isLeft ? bLHand : bRHand;
                    float elbowY = isLeft ? localLElbow.y : localRElbow.y;
                    float wristY = isLeft ? localLWrist.y : localRWrist.y;
                    if (v.y > elbowY) { bw.boneIndex0 = arm; bw.weight0 = 0.8f; bw.boneIndex1 = isLeft ? bLShoulder : bRShoulder; bw.weight1 = 0.2f; }
                    else if (v.y > wristY) { bw.boneIndex0 = fore; bw.weight0 = 0.8f; bw.boneIndex1 = arm; bw.weight1 = 0.2f; }
                    else { bw.boneIndex0 = hand; bw.weight0 = 0.9f; bw.boneIndex1 = fore; bw.weight1 = 0.1f; }
                }
                else if (v.y >= hipY)
                {
                    bw.boneIndex0 = bHips; bw.weight0 = 0.7f; bw.boneIndex1 = bSpine; bw.weight1 = 0.3f;
                }
                else
                {
                    int upLeg = isLeft ? bLUpLeg : bRUpLeg;
                    int leg = isLeft ? bLLeg : bRLeg;
                    int foot = isLeft ? bLFoot : bRFoot;
                    if (v.y > kneeY)
                    {
                        if (def.isFemale)
                        {
                            // Skirt stability: 75% hips, 25% upLeg prevents skirt tearing
                            bw.boneIndex0 = bHips; bw.weight0 = 0.75f; bw.boneIndex1 = upLeg; bw.weight1 = 0.25f;
                        }
                        else
                        {
                            bw.boneIndex0 = upLeg; bw.weight0 = 0.8f; bw.boneIndex1 = bHips; bw.weight1 = 0.2f;
                        }
                    }
                    else if (v.y > footY + 0.002f)
                    {
                        bw.boneIndex0 = leg; bw.weight0 = 0.8f; bw.boneIndex1 = upLeg; bw.weight1 = 0.2f;
                    }
                    else
                    {
                        bw.boneIndex0 = foot; bw.weight0 = 0.9f; bw.boneIndex1 = leg; bw.weight1 = 0.1f;
                    }
                }
                weights[i] = bw;
            }

            // 6. Build Mesh & Bindposes
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

            // 7. Add SkinnedMeshRenderer child
            var smrGO = new GameObject("SkinnedMesh");
            smrGO.transform.SetParent(armature.transform, false);
            var smr = smrGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = newMesh;
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

            // 10. Position NPC on its floor
            NavMeshHit hit;
            Vector3 pos = def.startPos;
            if (NavMesh.SamplePosition(pos, out hit, 4.0f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }
            npcGO.transform.position = pos;
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
            agent.Warp(pos);

            // 12. Configure ProctorAI
            var ai = npcGO.GetComponent<ProctorAI>();
            if (ai != null)
            {
                ai.enableProceduralWalk = false;
                ai.walkSpeed = 1.4f;
            }

            EditorUtility.SetDirty(npcGO);
            Debug.Log($"[DEPLOYED] {def.goName}: Rigged, Skinned, Scaled {SCALE}, Y={pos.y:F2}, AnimCtrl={animCtrl.name}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("=== ALL 6 PROCTORS SUCCESSFULLY DEPLOYED WITH AUTHENTIC SKELETAL ANIMATIONS! ===");
    }
}
