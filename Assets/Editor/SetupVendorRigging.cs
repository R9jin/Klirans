using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupVendorRigging
{
    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return Vector3.Distance(p, a + t * ab);
    }

    [MenuItem("Tools/Klirans/Rig and Setup Renz Vendor Prefab")]
    public static void Run()
    {
        Debug.Log("=== RIGGING AND CONFIGURING RENZ VENDOR PREFAB ===");

        if (!AssetDatabase.IsValidFolder("Assets/NPC Assets/RiggedMeshes"))
        {
            AssetDatabase.CreateFolder("Assets/NPC Assets", "RiggedMeshes");
        }

        string modelPath = "Assets/NPC Assets/Models/renz.fbx";
        string idleFbxPath = "Assets/NPC Assets/Animations/animateds/renz/IdleRenz.fbx";
        string matPath = "Assets/NPC Assets/Materials/Renz_Mat.mat";
        string ctrlPath = "Assets/NPC Assets/AnimatorControllers/Vendor_Renz_AnimCtrl.controller";
        string riggedMeshPath = "Assets/NPC Assets/RiggedMeshes/Vendor_Renz_Mesh.asset";
        string prefabPath = "Assets/Prefabs/Vendor_NaglalakoNgHamAndCheese.prefab";

        var idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(idleFbxPath);
        var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(modelPath);
        if (meshAsset == null)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (go != null)
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null) meshAsset = mf.sharedMesh;
            }
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (idlePrefab == null || meshAsset == null || mat == null)
        {
            Debug.LogError($"Missing prerequisites: idle={idlePrefab != null}, mesh={meshAsset != null}, mat={mat != null}");
            return;
        }

        // 1. Create or load AnimatorController with Idle clip
        var animCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (animCtrl == null)
        {
            animCtrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var rootSM = animCtrl.layers[0].stateMachine;

            AnimationClip idleClip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(idleFbxPath))
            {
                if (asset is AnimationClip c && !c.name.Contains("__preview__"))
                {
                    idleClip = c;
                    break;
                }
            }

            if (idleClip != null)
            {
                var idleState = rootSM.AddState("Idle");
                idleState.motion = idleClip;
                rootSM.defaultState = idleState;
            }
            EditorUtility.SetDirty(animCtrl);
            AssetDatabase.SaveAssets();
        }

        // 2. Instantiate temporary Armature for skinning calculation
        var tempArmature = Object.Instantiate(idlePrefab);
        tempArmature.name = "Armature";
        tempArmature.transform.position = Vector3.zero;
        tempArmature.transform.rotation = Quaternion.identity;
        tempArmature.transform.localScale = Vector3.one;

        var hips = tempArmature.transform.Find("mixamorig:Hips");
        var allTransforms = hips.GetComponentsInChildren<Transform>();
        var boneMap = new Dictionary<string, Transform>();
        boneMap["mixamorig:Hips"] = hips;
        foreach (var t in allTransforms) boneMap[t.name] = t;

        string[] boneNames = new[]
        {
            "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
            "mixamorig:Neck", "mixamorig:Head",
            "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
            "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot",
            "mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot"
        };

        var bones = new List<Transform>();
        foreach (var bn in boneNames)
        {
            if (boneMap.ContainsKey(bn)) bones.Add(boneMap[bn]);
        }

        // 3. Align raw mesh vertices to skeleton space
        Quaternion q = Quaternion.Euler(-90, 90, 0);
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

        // Soles sit flush on the floor (Y = 0)
        for (int i = 0; i < origVerts.Length; i++)
        {
            alignedVerts[i].y -= minY;
        }

        // Compute bone segment endpoints
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

        // 4. Calculate Anatomical Bone Weights
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

                // Left/right separation
                if (bName.Contains("Left") && p.x > 0.0002f)
                {
                    d += (p.x - 0.0002f) * 15f;
                }
                else if (bName.Contains("Right") && p.x < -0.0002f)
                {
                    d += (-p.x - 0.0002f) * 15f;
                }

                // Torso vs Arm separation
                if (bName.Contains("Arm") || bName.Contains("ForeArm") || bName.Contains("Hand"))
                {
                    if (Mathf.Abs(p.x) < 0.0032f)
                    {
                        d += (0.0032f - Mathf.Abs(p.x)) * 10f;
                    }
                }

                // Pelvis stability
                if (bName.Contains("Hips"))
                {
                    if (p.y > 0.016f && p.y < 0.026f && Mathf.Abs(p.x) < 0.0022f)
                    {
                        d *= 0.35f;
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

        // 5. Build Mesh Asset & Bindposes
        Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(riggedMeshPath);
        if (newMesh == null)
        {
            newMesh = new Mesh();
            newMesh.name = "Vendor_Renz_Mesh";
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
            bindposes[b] = bones[b].worldToLocalMatrix * tempArmature.transform.localToWorldMatrix;
        }
        newMesh.bindposes = bindposes;
        newMesh.RecalculateBounds();
        EditorUtility.SetDirty(newMesh);
        AssetDatabase.SaveAssets();

        Object.DestroyImmediate(tempArmature);
        Debug.Log($"[VendorRigging] Successfully created rigged mesh at {riggedMeshPath}");

        // 6. Ensure Material Assets Exist
        CreateMaterialAssets();

        // 7. Build the Complete Vendor Prefab
        BuildVendorPrefab(prefabPath, idlePrefab, newMesh, mat, animCtrl, boneNames);
    }

    private static void CreateMaterialAssets()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Styrofoam Material
        string styrofoamMatPath = "Assets/Materials/Styrofoam_Mat.mat";
        var styrofoamMat = AssetDatabase.LoadAssetAtPath<Material>(styrofoamMatPath);
        if (styrofoamMat == null)
        {
            styrofoamMat = new Material(litShader);
            styrofoamMat.name = "Styrofoam_Mat";
            styrofoamMat.color = new Color(0.96f, 0.96f, 0.94f, 1f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Styrofoam_Texture.png");
            if (tex != null)
            {
                styrofoamMat.mainTexture = tex;
                if (styrofoamMat.HasProperty("_BaseMap")) styrofoamMat.SetTexture("_BaseMap", tex);
                styrofoamMat.mainTextureScale = new Vector2(3f, 3f);
            }
            if (styrofoamMat.HasProperty("_Smoothness")) styrofoamMat.SetFloat("_Smoothness", 0.08f);
            AssetDatabase.CreateAsset(styrofoamMat, styrofoamMatPath);
        }

        // Sticker Material
        string stickerMatPath = "Assets/Materials/HamAndCheese_Sticker_Mat.mat";
        var stickerMat = AssetDatabase.LoadAssetAtPath<Material>(stickerMatPath);
        if (stickerMat == null)
        {
            stickerMat = new Material(litShader);
            stickerMat.name = "HamAndCheese_Sticker_Mat";
            stickerMat.color = Color.white;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/HamAndCheese_Sticker.png");
            if (tex != null)
            {
                stickerMat.mainTexture = tex;
                if (stickerMat.HasProperty("_BaseMap")) stickerMat.SetTexture("_BaseMap", tex);
            }
            if (stickerMat.HasProperty("_Smoothness")) stickerMat.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(stickerMat, stickerMatPath);
        }

        // Price Tag Material
        string priceTagMatPath = "Assets/Materials/PriceTag_Mat.mat";
        var priceTagMat = AssetDatabase.LoadAssetAtPath<Material>(priceTagMatPath);
        if (priceTagMat == null)
        {
            priceTagMat = new Material(litShader);
            priceTagMat.name = "PriceTag_Mat";
            priceTagMat.color = Color.white;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_3.png");
            if (tex != null)
            {
                priceTagMat.mainTexture = tex;
                if (priceTagMat.HasProperty("_BaseMap")) priceTagMat.SetTexture("_BaseMap", tex);
            }
            if (priceTagMat.HasProperty("_Smoothness")) priceTagMat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(priceTagMat, priceTagMatPath);
        }

        // Yellow Nylon Strap Material
        string strapMatPath = "Assets/Materials/YellowNylonStrap_Mat.mat";
        var strapMat = AssetDatabase.LoadAssetAtPath<Material>(strapMatPath);
        if (strapMat == null)
        {
            strapMat = new Material(litShader);
            strapMat.name = "YellowNylonStrap_Mat";
            strapMat.color = new Color(0.96f, 0.78f, 0.08f, 1f);
            if (strapMat.HasProperty("_Smoothness")) strapMat.SetFloat("_Smoothness", 0.20f);
            AssetDatabase.CreateAsset(strapMat, strapMatPath);
        }

        // Plastic Buckle Clip Material
        string clipMatPath = "Assets/Materials/PlasticBuckleClip_Mat.mat";
        var clipMat = AssetDatabase.LoadAssetAtPath<Material>(clipMatPath);
        if (clipMat == null)
        {
            clipMat = new Material(litShader);
            clipMat.name = "PlasticBuckleClip_Mat";
            clipMat.color = new Color(0.93f, 0.93f, 0.91f, 1f);
            if (clipMat.HasProperty("_Smoothness")) clipMat.SetFloat("_Smoothness", 0.50f);
            AssetDatabase.CreateAsset(clipMat, clipMatPath);
        }

        AssetDatabase.SaveAssets();
    }

    private static void BuildVendorPrefab(string prefabPath, GameObject idlePrefab, Mesh riggedMesh, Material mat, RuntimeAnimatorController animCtrl, string[] boneNames)
    {
        var rootGO = new GameObject("Vendor_NaglalakoNgHamAndCheese");

        // Collider & Physics
        var col = rootGO.AddComponent<CapsuleCollider>();
        col.radius = 0.30f;
        col.height = 1.68f;
        col.center = new Vector3(0f, 0.84f, 0f);

        var rb = rootGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // HamAndCheeseVendor Script
        var vendorComp = rootGO.AddComponent<HamAndCheeseVendor>();
        vendorComp.sandwichItemAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/HamAndCheeseSandwich.asset");
        vendorComp.vendorGreetingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/murmurSoundEffect.mp3");

        vendorComp.styrofoamTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Styrofoam_Texture.png");
        vendorComp.stickerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/HamAndCheese_Sticker.png");
        vendorComp.priceTag3Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_3.png");
        vendorComp.priceTag5Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_5.png");
        vendorComp.priceTag8Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_8.png");
        vendorComp.priceTag12Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_12.png");
        vendorComp.priceTagSoldOutTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_SoldOut.png");

        // Armature & Animator
        var armature = Object.Instantiate(idlePrefab, rootGO.transform);
        armature.name = "Armature";
        armature.transform.localPosition = Vector3.zero;
        armature.transform.localRotation = Quaternion.identity;
        armature.transform.localScale = Vector3.one * 37.0f; // Scale to ~1.68m height

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

        // SkinnedMesh
        var smrGO = new GameObject("SkinnedMesh");
        smrGO.transform.SetParent(armature.transform, false);
        var smr = smrGO.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = riggedMesh;
        smr.bones = bones.ToArray();
        smr.rootBone = hips;
        smr.sharedMaterial = mat;
        smr.updateWhenOffscreen = true;

        var anim = armature.GetComponent<Animator>();
        if (anim == null) anim = armature.AddComponent<Animator>();
        anim.runtimeAnimatorController = animCtrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.enabled = true;

        // Build Styrofoam Box Prop directly on prefab root
        vendorComp.BuildStyrofoamBoxProp();

        // Assign asset materials to the box parts for clean serialization
        var boxProp = rootGO.transform.Find("StyrofoamCoolerBox");
        if (boxProp != null)
        {
            var styrofoamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Styrofoam_Mat.mat");
            var stickerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HamAndCheese_Sticker_Mat.mat");
            var priceTagMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PriceTag_Mat.mat");
            var strapMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/YellowNylonStrap_Mat.mat");
            var clipMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlasticBuckleClip_Mat.mat");

            foreach (var r in boxProp.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                if (n.Contains("Sticker")) r.sharedMaterial = stickerMat;
                else if (n.Contains("PriceTag")) r.sharedMaterial = priceTagMat;
                else if (n.Contains("Clip")) r.sharedMaterial = clipMat;
                else if (n.Contains("Strap")) r.sharedMaterial = strapMat;
                else r.sharedMaterial = styrofoamMat;
            }
        }

        // Save Prefab
        PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);
        Object.DestroyImmediate(rootGO);
        Debug.Log($"[VendorRigging] Successfully built and saved vendor prefab at {prefabPath}");
    }
}
