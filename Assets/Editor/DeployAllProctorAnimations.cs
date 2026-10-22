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

    static readonly float SCALE = 37.0f;

    static readonly NPCDef[] NPCs = new[]
    {
        new NPCDef
        {
            goName = "ClearanceNPC_Drei",
            modelAsset = "Assets/NPC Assets/Models/drei.fbx",
            idleFbx = "Assets/NPC Assets/Animations/animateds/drei/IdleDrei.fbx",
            ctrlPath = "Assets/NPC Assets/AnimatorControllers/ClearanceNPC_Drei_AnimCtrl.controller",
            matPath = "Assets/NPC Assets/Materials/Drei_Mat.mat",
            meshRotation = Quaternion.Euler(-90, 90, 0),
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
            meshRotation = Quaternion.Euler(-90, 90, 0),
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
            vertOffset = Vector3.zero,
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
            meshRotation = Quaternion.Euler(-90, 90, 0),
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
            meshRotation = Quaternion.Euler(-90, 90, 0),
            isFemale = false,
            vertOffset = Vector3.zero,
            startPos = new Vector3(-84.00f, 14.34f, 26.00f)
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

            // 4. Align raw mesh vertices to skeleton space
            Quaternion q = def.meshRotation;
            Vector3[] origVerts = meshAsset.vertices;
            Vector3[] origNorms = meshAsset.normals;
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

            // Soles of the shoes sit flush on the floor (Y = 0)
            for (int i = 0; i < origVerts.Length; i++)
            {
                alignedVerts[i].y -= minY;
            }

            // Compute bone segment endpoints in armature local space (scale = 1)
            Vector3[] segA = new Vector3[bones.Count];
            Vector3[] segB = new Vector3[bones.Count];
            for (int b = 0; b < bones.Count; b++)
            {
                Transform boneT = bones[b];
                segA[b] = armature.transform.InverseTransformPoint(boneT.position);
                string bn = boneT.name;

                if (bn == "mixamorig:Hips")
                {
                    // Centerline pelvis: hips downward towards crotch
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

            // 5. Anatomical Segment-Proximity Bone Weighting
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

                    // 1. Cross-side separation (prevent left side pulling right side & vice versa)
                    if (bName.Contains("Left") && p.x > 0.0002f)
                    {
                        d += (p.x - 0.0002f) * 15f;
                    }
                    else if (bName.Contains("Right") && p.x < -0.0002f)
                    {
                        d += (-p.x - 0.0002f) * 15f;
                    }

                    // 2. Torso vs Arm separation (protect ribs/torso from arm swings)
                    if (bName.Contains("Arm") || bName.Contains("ForeArm") || bName.Contains("Hand"))
                    {
                        if (Mathf.Abs(p.x) < 0.0032f)
                        {
                            d += (0.0032f - Mathf.Abs(p.x)) * 10f;
                        }
                    }

                    // 3. Pelvis / Crotch stability (centerline pelvis vertices must stay with Hips)
                    if (bName.Contains("Hips"))
                    {
                        if (p.y > 0.016f && p.y < 0.026f && Mathf.Abs(p.x) < 0.0022f)
                        {
                            d *= 0.35f;
                        }
                    }

                    // 4. Female skirt stability (Ira and Jessa)
                    if (def.isFemale && (bName.Contains("UpLeg") || bName.Contains("Leg")))
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
                ai.footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
                ai.footstepVolume = 0.20f;
                ai.footstepMinDistance = 1.2f;
                ai.footstepMaxDistance = 14.0f;
                ai.enableCreepyStare = true;
                ai.stareChance = 0.85f;
                ai.stareMaxDistance = 11.0f;
                ai.maxHeadTurnAngle = 95.0f;
                ai.headTurnSpeed = 4.0f;
            }

            // 13. Configure solid physical collider and kinematic Rigidbody (collides with player)
            var col = npcGO.GetComponent<CapsuleCollider>();
            if (col == null) col = npcGO.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.28f;
            col.height = def.isFemale ? 1.64f : 1.72f;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            var rb = npcGO.GetComponent<Rigidbody>();
            if (rb == null) rb = npcGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            EditorUtility.SetDirty(npcGO);
            Debug.Log($"[DEPLOYED] {def.goName}: Rigged, Skinned, Scaled {SCALE}, Y={pos.y:F2}, Solid Collider (h={col.height:F2}), AnimCtrl={animCtrl.name}");
        }

        // Ensure Player has NavMeshObstacle for NPC local avoidance
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var obs = player.GetComponent<NavMeshObstacle>();
            if (obs == null) obs = player.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.radius = 0.35f;
            obs.height = 1.8f;
            obs.center = new Vector3(0f, 0.9f, 0f);
            obs.carving = false;
            EditorUtility.SetDirty(player);
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("=== ALL 6 PROCTORS SUCCESSFULLY DEPLOYED WITH AUTHENTIC SKELETAL ANIMATIONS! ===");
    }
}
