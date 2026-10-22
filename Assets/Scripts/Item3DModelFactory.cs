using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Item3DModelFactory — High-Fidelity Procedural 3D Model & Texture Generator for KLIRANS.
///
/// Builds intricate, finely sculpted 3D models with smooth normals, bevels, chamfers,
/// lathe-turned curves, reeded serrations, and high-resolution PBR textures for the
/// Resident Evil-style 3D Item Inspection System.
/// </summary>
public static class Item3DModelFactory
{
    private const string PrefabDir = "Assets/Prefabs/ItemModels";
    private const string TextureDir = "Assets/Textures/ItemInspectTextures";
    private const string MeshDir = "Assets/Meshes/ItemMeshes";
    private const string MaterialDir = "Assets/Materials/ItemMaterials";
    private const string PuzzlePrefabDir = "Assets/Prefabs/PuzzleModels";

#if UNITY_EDITOR
    [MenuItem("KLIRANS/Generate All 3D Item Models")]
#endif
    public static void GenerateAllModels()
    {
#if UNITY_EDITOR
        if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
        if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);
        if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);
        if (!Directory.Exists(PuzzlePrefabDir)) Directory.CreateDirectory(PuzzlePrefabDir);

        Debug.Log("[Item3DModelFactory] Starting high-fidelity 3D remodeling for all inventory items and stations...");

        // Generate Models
        GenerateLockerKeyModel();
        GenerateExecutiveSealStampModel();
        GenerateComLabFormModel();
        GenerateLibraryVoucherModel();
        GenerateClearanceSlipModel();
        GenerateTornFragmentModels();
        GenerateHamAndCheeseSandwichModel();
        GenerateFlashlightModel();
        GeneratePrinterPaperModel();
        GenerateStudentHandbookModel();
        GenerateLockpinModel();
        GenerateCoinModel();
        GenerateVGACableModel();
        GenerateCeramicFuseModel();
        GenerateCassetteTapeModel();
        GenerateGuardLogbookModel();
        GenerateMedicalSlipModel();

        // Generate Puzzle Stations
        GenerateCircuitBreakerStationModel();
        GenerateComLabTerminalStationModel();
        GenerateVendingMachineStationModel();
        GenerateGuidanceSafeStationModel();
        GenerateLobbyKeypadStationModel();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Link models to InventoryItem assets
        LinkModelsToInventoryItemAssets();

        Debug.Log("[Item3DModelFactory] All high-detail 3D models and textures generated and linked successfully!");
#endif
    }

#if UNITY_EDITOR
    [MenuItem("Klirans/Generate All 3D Item Models (High Fidelity)")]
    public static void MenuGenerateAll()
    {
        GenerateAllModels();
    }
#endif

    // ═══════════════════════════════════════════════════════════════════════════
    // PROCEDURAL MESH GENERATORS (Available in both Editor and Runtime)
    // ═══════════════════════════════════════════════════════════════════════════

    public static Mesh CreateTorusMesh(float mainRadius, float tubeRadius, int radialSegments = 24, int tubeSegments = 16)
    {
        Mesh mesh = new Mesh { name = "Torus" };
        int vertCount = (radialSegments + 1) * (tubeSegments + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[radialSegments * tubeSegments * 6];

        int vIdx = 0;
        for (int i = 0; i <= radialSegments; i++)
        {
            float u = (float)i / radialSegments;
            float theta = u * Mathf.PI * 2f;
            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            for (int j = 0; j <= tubeSegments; j++)
            {
                float v = (float)j / tubeSegments;
                float phi = v * Mathf.PI * 2f;
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                float x = (mainRadius + tubeRadius * cosPhi) * cosTheta;
                float y = tubeRadius * sinPhi;
                float z = (mainRadius + tubeRadius * cosPhi) * sinTheta;

                vertices[vIdx] = new Vector3(x, y, z);
                normals[vIdx] = new Vector3(cosPhi * cosTheta, sinPhi, cosPhi * sinTheta);
                uvs[vIdx] = new Vector2(u, v);
                vIdx++;
            }
        }

        int tIdx = 0;
        for (int i = 0; i < radialSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int row1 = i * (tubeSegments + 1);
                int row2 = (i + 1) * (tubeSegments + 1);

                triangles[tIdx++] = row1 + j;
                triangles[tIdx++] = row2 + j;
                triangles[tIdx++] = row1 + j + 1;

                triangles[tIdx++] = row1 + j + 1;
                triangles[tIdx++] = row2 + j;
                triangles[tIdx++] = row2 + j + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh CreateLatheMesh(Vector2[] profile, int radialSegments = 24)
    {
        Mesh mesh = new Mesh { name = "LatheMesh" };
        int nProfiles = profile.Length;
        int vertCount = (radialSegments + 1) * nProfiles;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[radialSegments * (nProfiles - 1) * 6];

        int vIdx = 0;
        for (int i = 0; i <= radialSegments; i++)
        {
            float u = (float)i / radialSegments;
            float angle = u * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            for (int j = 0; j < nProfiles; j++)
            {
                float v = (float)j / (nProfiles - 1);
                float r = profile[j].x;
                float y = profile[j].y;
                vertices[vIdx] = new Vector3(r * cos, y, r * sin);
                uvs[vIdx] = new Vector2(u, v);
                vIdx++;
            }
        }

        int tIdx = 0;
        for (int i = 0; i < radialSegments; i++)
        {
            for (int j = 0; j < nProfiles - 1; j++)
            {
                int r1 = i * nProfiles;
                int r2 = (i + 1) * nProfiles;

                triangles[tIdx++] = r1 + j;
                triangles[tIdx++] = r2 + j;
                triangles[tIdx++] = r1 + j + 1;

                triangles[tIdx++] = r1 + j + 1;
                triangles[tIdx++] = r2 + j;
                triangles[tIdx++] = r2 + j + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh CreateReededCylinderMesh(float radius, float height, int numReeds, float reedDepth)
    {
        Mesh mesh = new Mesh { name = "ReededCylinder" };
        int totalSegments = numReeds * 2;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        verts.Add(new Vector3(0, height * 0.5f, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int topCenterIdx = 0;

        verts.Add(new Vector3(0, -height * 0.5f, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int botCenterIdx = 1;

        int topRimStart = verts.Count;
        for (int i = 0; i <= totalSegments; i++)
        {
            float angle = ((float)i / totalSegments) * Mathf.PI * 2f;
            float r = radius - ((i % 2 == 1) ? reedDepth : 0f);
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            verts.Add(new Vector3(x, height * 0.5f, z));
            uvs.Add(new Vector2(0.5f + (x / radius) * 0.5f, 0.5f + (z / radius) * 0.5f));
        }

        int botRimStart = verts.Count;
        for (int i = 0; i <= totalSegments; i++)
        {
            float angle = ((float)i / totalSegments) * Mathf.PI * 2f;
            float r = radius - ((i % 2 == 1) ? reedDepth : 0f);
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            verts.Add(new Vector3(x, -height * 0.5f, z));
            uvs.Add(new Vector2(0.5f + (x / radius) * 0.5f, 0.5f + (z / radius) * 0.5f));
        }

        for (int i = 0; i < totalSegments; i++)
        {
            tris.Add(topCenterIdx);
            tris.Add(topRimStart + i);
            tris.Add(topRimStart + i + 1);
        }

        for (int i = 0; i < totalSegments; i++)
        {
            tris.Add(botCenterIdx);
            tris.Add(botRimStart + i + 1);
            tris.Add(botRimStart + i);
        }

        for (int i = 0; i < totalSegments; i++)
        {
            int t1 = topRimStart + i;
            int t2 = topRimStart + i + 1;
            int b1 = botRimStart + i;
            int b2 = botRimStart + i + 1;

            tris.Add(t1);
            tris.Add(b1);
            tris.Add(t2);

            tris.Add(t2);
            tris.Add(b1);
            tris.Add(b2);
        }

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh CreateChamferedBoxMesh(float w, float h, float d, float bevel)
    {
        Mesh mesh = new Mesh { name = "ChamferedBox" };
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float hw = w * 0.5f, hh = h * 0.5f, hd = d * 0.5f;
        float b = Mathf.Clamp(bevel, 0.0005f, Mathf.Min(hw, Mathf.Min(hh, hd)) * 0.35f);
        float ix = hw - b, iy = hh - b, iz = hd - b;

        // 24 corner vertices: 6 faces * 4 vertices
        // Front (+Z) face vertices (viewed from +Z: +X is left, +Y is up -> clockwise: BL, TL, TR, BR)
        Vector3 fBL = new Vector3(-ix, -iy,  hd);
        Vector3 fTL = new Vector3(-ix,  iy,  hd);
        Vector3 fTR = new Vector3( ix,  iy,  hd);
        Vector3 fBR = new Vector3( ix, -iy,  hd);

        // Back (-Z) face vertices (viewed from -Z: -X is left, +Y is up -> clockwise: BR, TR, TL, BL in world X)
        Vector3 bBL = new Vector3(-ix, -iy, -hd);
        Vector3 bTL = new Vector3(-ix,  iy, -hd);
        Vector3 bTR = new Vector3( ix,  iy, -hd);
        Vector3 bBR = new Vector3( ix, -iy, -hd);

        // Top (+Y) face vertices
        Vector3 tFL = new Vector3(-ix,  hh,  iz);
        Vector3 tFR = new Vector3( ix,  hh,  iz);
        Vector3 tBR = new Vector3( ix,  hh, -iz);
        Vector3 tBL = new Vector3(-ix,  hh, -iz);

        // Bottom (-Y) face vertices
        Vector3 dFL = new Vector3(-ix, -hh,  iz);
        Vector3 dFR = new Vector3( ix, -hh,  iz);
        Vector3 dBR = new Vector3( ix, -hh, -iz);
        Vector3 dBL = new Vector3(-ix, -hh, -iz);

        // Left (-X) face vertices
        Vector3 lFB = new Vector3(-hw, -iy,  iz);
        Vector3 lFT = new Vector3(-hw,  iy,  iz);
        Vector3 lBT = new Vector3(-hw,  iy, -iz);
        Vector3 lBB = new Vector3(-hw, -iy, -iz);

        // Right (+X) face vertices
        Vector3 rFB = new Vector3( hw, -iy,  iz);
        Vector3 rFT = new Vector3( hw,  iy,  iz);
        Vector3 rBT = new Vector3( hw,  iy, -iz);
        Vector3 rBB = new Vector3( hw, -iy, -iz);

        // ── 6 Main Faces (Outward Clockwise Winding, Full 0..1 UVs) ──
        // Front (+Z): normal (0, 0, +1)
        AddQuad(verts, uvs, tris, fBR, fTR, fTL, fBL);
        // Back (-Z): normal (0, 0, -1)
        AddQuad(verts, uvs, tris, bBL, bTL, bTR, bBR);
        // Top (+Y): normal (0, +1, 0)
        AddQuad(verts, uvs, tris, tBL, tBR, tFR, tFL);
        // Bottom (-Y): normal (0, -1, 0)
        AddQuad(verts, uvs, tris, dFL, dFR, dBR, dBL);
        // Left (-X): normal (-1, 0, 0)
        AddQuad(verts, uvs, tris, lFB, lFT, lBT, lBB);
        // Right (+X): normal (+1, 0, 0)
        AddQuad(verts, uvs, tris, rBB, rBT, rFT, rFB);

        // ── 12 Bevel Edge Quads (Stitching all adjacent faces watertight) ──
        // Front 4 edges
        AddQuad(verts, uvs, tris, fTL, fTR, tFR, tFL); // Front-Top
        AddQuad(verts, uvs, tris, dFL, dFR, fBR, fBL); // Front-Bottom
        AddQuad(verts, uvs, tris, fBL, fTL, lFT, lFB); // Front-Left
        AddQuad(verts, uvs, tris, rFB, rFT, fTR, fBR); // Front-Right

        // Back 4 edges
        AddQuad(verts, uvs, tris, tBL, tBR, bTR, bTL); // Back-Top
        AddQuad(verts, uvs, tris, bBL, bBR, dBR, dBL); // Back-Bottom
        AddQuad(verts, uvs, tris, lBB, lBT, bTL, bBL); // Back-Left
        AddQuad(verts, uvs, tris, bBR, bTR, rBT, rBB); // Back-Right

        // Middle 4 edges (Top/Bottom to Left/Right)
        AddQuad(verts, uvs, tris, lFT, tFL, tBL, lBT); // Top-Left
        AddQuad(verts, uvs, tris, tFR, rFT, rBT, tBR); // Top-Right
        AddQuad(verts, uvs, tris, lBB, dBL, dFL, lFB); // Bottom-Left
        AddQuad(verts, uvs, tris, dFR, dBR, rBB, rFB); // Bottom-Right

        // ── 8 Bevel Corner Triangles (Watertight Corners) ──
        AddTri(verts, uvs, tris, fTL, tFL, lFT); // Front-Top-Left
        AddTri(verts, uvs, tris, fTR, rFT, tFR); // Front-Top-Right
        AddTri(verts, uvs, tris, fBL, lFB, dFL); // Front-Bottom-Left
        AddTri(verts, uvs, tris, fBR, dFR, rFB); // Front-Bottom-Right
        AddTri(verts, uvs, tris, bTL, lBT, tBL); // Back-Top-Left
        AddTri(verts, uvs, tris, bTR, tBR, rBT); // Back-Top-Right
        AddTri(verts, uvs, tris, bBL, dBL, lBB); // Back-Bottom-Left
        AddTri(verts, uvs, tris, bBR, rBB, dBR); // Back-Bottom-Right

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int start = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
        tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
    }

    private static void AddTri(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        int start = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.5f, 1)); uvs.Add(new Vector2(1, 0));
        tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
    }

#if UNITY_EDITOR
    // ═══════════════════════════════════════════════════════════════════════════
    // DETAILED 3D MODEL GENERATORS
    // ═══════════════════════════════════════════════════════════════════════════

    // ── 1. Locker Key ──────────────────────────────────────────────────────────
    private static void GenerateLockerKeyModel()
    {
        GameObject root = new GameObject("Model_LockerKey");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var brassMat = new Material(shader) { name = "Item_Brass_Mat" };
        brassMat.color = new Color(0.92f, 0.76f, 0.32f, 1f);
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.95f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.82f);

        var steelMat = new Material(shader) { name = "Item_Steel_Mat" };
        steelMat.color = new Color(0.85f, 0.86f, 0.90f, 1f);
        if (steelMat.HasProperty("_Metallic")) steelMat.SetFloat("_Metallic", 0.98f);
        if (steelMat.HasProperty("_Smoothness")) steelMat.SetFloat("_Smoothness", 0.88f);

        var tagTex = CreateKeyTagTexture();
        var tagMat = new Material(shader) { name = "Item_KeyTag_Mat", mainTexture = tagTex };
        if (tagMat.HasProperty("_Smoothness")) tagMat.SetFloat("_Smoothness", 0.65f);

        // 1. Key Bow (Round ring head with center hole)
        var bowMesh = CreateTorusMesh(mainRadius: 0.026f, tubeRadius: 0.0055f, radialSegments: 28, tubeSegments: 16);
        var bowGO = new GameObject("KeyBow", typeof(MeshFilter), typeof(MeshRenderer));
        bowGO.transform.SetParent(root.transform, false);
        bowGO.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        bowGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bowGO.GetComponent<MeshFilter>().sharedMesh = bowMesh;
        bowGO.GetComponent<MeshRenderer>().sharedMaterial = brassMat;

        // 2. Key Stem / Collar
        var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collar.name = "KeyCollar";
        collar.transform.SetParent(root.transform, false);
        collar.transform.localPosition = new Vector3(0f, 0.034f, 0f);
        collar.transform.localScale = new Vector3(0.016f, 0.008f, 0.016f);
        collar.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        CleanCollider(collar);

        // 3. Key Shaft / Blade
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "KeyBlade";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, -0.045f, 0f);
        shaft.transform.localScale = new Vector3(0.007f, 0.15f, 0.022f);
        shaft.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        CleanCollider(shaft);

        // 4. Stepped Key Bittings (Teeth notches along the blade)
        float[] bittingHeights = { 0.008f, 0.014f, 0.006f, 0.016f, 0.010f };
        for (int i = 0; i < bittingHeights.Length; i++)
        {
            var tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = $"KeyTooth_{i}";
            tooth.transform.SetParent(root.transform, false);
            tooth.transform.localPosition = new Vector3(0f, -0.015f - i * 0.022f, 0.016f);
            tooth.transform.localScale = new Vector3(0.0065f, 0.016f, bittingHeights[i]);
            tooth.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
            CleanCollider(tooth);
        }

        // 5. Split Key Ring (Steel spiral torus threading through bow hole)
        var ringMesh = CreateTorusMesh(mainRadius: 0.034f, tubeRadius: 0.0035f, radialSegments: 28, tubeSegments: 16);
        var ringGO = new GameObject("SplitKeyRing", typeof(MeshFilter), typeof(MeshRenderer));
        ringGO.transform.SetParent(root.transform, false);
        ringGO.transform.localPosition = new Vector3(0f, 0.098f, 0f);
        ringGO.transform.localRotation = Quaternion.Euler(75f, 20f, 0f);
        ringGO.GetComponent<MeshFilter>().sharedMesh = ringMesh;
        ringGO.GetComponent<MeshRenderer>().sharedMaterial = steelMat;

        // 6. Chamfered Acrylic Key Tag
        var tagMesh = CreateChamferedBoxMesh(w: 0.075f, h: 0.13f, d: 0.006f, bevel: 0.004f);
        var tagGO = new GameObject("KeyTag", typeof(MeshFilter), typeof(MeshRenderer));
        tagGO.transform.SetParent(root.transform, false);
        tagGO.transform.localPosition = new Vector3(0f, 0.18f, 0.008f);
        tagGO.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        tagGO.GetComponent<MeshFilter>().sharedMesh = tagMesh;
        tagGO.GetComponent<MeshRenderer>().sharedMaterial = tagMat;

        // Brass Grommet / Eyelet on Tag
        var eyeletMesh = CreateTorusMesh(mainRadius: 0.007f, tubeRadius: 0.0025f, radialSegments: 20, tubeSegments: 12);
        var eyeletGO = new GameObject("TagEyelet", typeof(MeshFilter), typeof(MeshRenderer));
        eyeletGO.transform.SetParent(tagGO.transform, false);
        eyeletGO.transform.localPosition = new Vector3(0f, 0.052f, 0f);
        eyeletGO.GetComponent<MeshFilter>().sharedMesh = eyeletMesh;
        eyeletGO.GetComponent<MeshRenderer>().sharedMaterial = brassMat;

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "LockerKeyCode";
        clue.clueTitle = "Nakatagong Numero sa Key Tag";
        clue.clueDescription = "May nakaukit na red ink code sa likod ng key tag: 'CAB-04 // CODE: 7-3-2' para sa 2nd Floor Faculty Records Locker!";
        clue.revealDirection = -Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_LockerKey.prefab");
    }

    private static Texture2D CreateKeyTagTexture()
    {
        int w = 256, h = 512;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bgColor = new Color(0.95f, 0.90f, 0.80f, 1f); // aged ivory
        Color inkNavy = new Color(0.12f, 0.18f, 0.32f, 1f);
        Color redStamp = new Color(0.82f, 0.12f, 0.12f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isBorder = (x < 14 || x > w - 14 || y < 14 || y > h - 14);
                Color c = isBorder ? inkNavy : bgColor;

                // Eyelet cutout area
                float distEyelet = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h - 45));
                if (distEyelet < 26) c = new Color(0.85f, 0.72f, 0.32f, 1f);

                // Front University Header lines
                if (y > 340 && y < 390 && x > 35 && x < w - 35)
                {
                    if (y % 18 < 6) c = inkNavy;
                }

                // Reverse Red Code Stamp: CAB-04 // CODE: 7-3-2
                if (y > 140 && y < 240 && x > 28 && x < w - 28)
                {
                    if (y % 22 < 8) c = redStamp;
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_KeyTag.png");
        return tex;
    }

    // ── 2. Official University Seal Stamp ─────────────────────────────────────
    private static void GenerateExecutiveSealStampModel()
    {
        GameObject root = new GameObject("Model_ExecutiveSealStamp");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var woodMat = new Material(shader) { name = "Item_MahoganyWood_Mat" };
        woodMat.color = new Color(0.42f, 0.18f, 0.10f, 1f); // Rich red mahogany
        if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0.72f);

        var brassMat = new Material(shader) { name = "Item_StampBrass_Mat" };
        brassMat.color = new Color(0.88f, 0.74f, 0.28f, 1f);
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.95f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.85f);

        var rubberTex = CreateEmbossingSealTexture();
        var rubberMat = new Material(shader) { name = "Item_RubberDie_Mat", mainTexture = rubberTex };
        rubberMat.color = new Color(0.72f, 0.14f, 0.14f, 1f); // Vulcanized red rubber
        if (rubberMat.HasProperty("_Smoothness")) rubberMat.SetFloat("_Smoothness", 0.35f);

        // 1. Turned Mahogany Handle (Sculpted with CreateLatheMesh)
        Vector2[] handleProfile = new Vector2[] {
            new Vector2(0.001f, 0.20f),
            new Vector2(0.038f, 0.185f), // crown bulb
            new Vector2(0.046f, 0.155f),
            new Vector2(0.032f, 0.12f),  // neck waist
            new Vector2(0.024f, 0.07f),  // grip
            new Vector2(0.028f, 0.035f),
            new Vector2(0.044f, 0.015f), // flared base
            new Vector2(0.048f, 0.00f),
            new Vector2(0.001f, 0.00f)
        };
        var handleMesh = CreateLatheMesh(handleProfile, radialSegments: 28);
        var handleGO = new GameObject("MahoganyHandle", typeof(MeshFilter), typeof(MeshRenderer));
        handleGO.transform.SetParent(root.transform, false);
        handleGO.GetComponent<MeshFilter>().sharedMesh = handleMesh;
        handleGO.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

        // 2. Knurled Brass Ferrule Collar Ring
        var collarMesh = CreateReededCylinderMesh(radius: 0.052f, height: 0.022f, numReeds: 36, reedDepth: 0.003f);
        var collarGO = new GameObject("BrassCollar", typeof(MeshFilter), typeof(MeshRenderer));
        collarGO.transform.SetParent(root.transform, false);
        collarGO.transform.localPosition = new Vector3(0f, -0.011f, 0f);
        collarGO.GetComponent<MeshFilter>().sharedMesh = collarMesh;
        collarGO.GetComponent<MeshRenderer>().sharedMaterial = brassMat;

        // 3. Octagonal Stamped Plinth
        var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.name = "BrassPlinth";
        plinth.transform.SetParent(root.transform, false);
        plinth.transform.localPosition = new Vector3(0f, -0.028f, 0f);
        plinth.transform.localScale = new Vector3(0.125f, 0.015f, 0.125f);
        plinth.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        CleanCollider(plinth);

        // 4. Vulcanized Rubber Stamp Die with 3D Embossed Mirror Seal
        var die = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        die.name = "RubberDie";
        die.transform.SetParent(root.transform, false);
        die.transform.localPosition = new Vector3(0f, -0.045f, 0f);
        die.transform.localScale = new Vector3(0.115f, 0.012f, 0.115f);
        die.GetComponent<MeshRenderer>().sharedMaterial = rubberMat;
        CleanCollider(die);

        // Clue: Look directly at the bottom rubber die
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "ExecutiveSealCode";
        clue.clueTitle = "Opisyal na University Seal Code";
        clue.clueDescription = "Nakatatak sa ilalim ang mirror seal: 'VERITAS ET EXCELLENTIA // EVP-AUTH-1994'. Ito ang opisyal na executive code ng pamantasan!";
        clue.revealDirection = -Vector3.up;
        clue.detectionAngle = 45.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_ExecutiveSealStamp.prefab");
    }

    private static Texture2D CreateEmbossingSealTexture()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color rubberRed = new Color(0.72f, 0.14f, 0.14f, 1f);
        Color reliefGold = new Color(0.98f, 0.85f, 0.40f, 1f);
        Color inkStain = new Color(0.18f, 0.06f, 0.06f, 1f);

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.44f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                Color c = rubberRed;

                if (d < radius + 10 && d > radius - 10) c = inkStain; // ink edge
                if (Mathf.Abs(d - radius) < 5f) c = reliefGold; // outer embossed ring
                if (Mathf.Abs(d - (radius - 32f)) < 3f) c = reliefGold; // inner ring

                // Circular motto text ring
                if (d > radius - 30f && d < radius - 8f)
                {
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    if (Mathf.Abs(Mathf.Sin(angle * 18f)) > 0.4f) c = reliefGold;
                }

                // Central Executive Shield
                if (d < radius - 55f)
                {
                    int dx = Mathf.Abs(x - (int)center.x);
                    int dy = y - (int)center.y;
                    if (dx < 65 && dy > -65 && dy < 65)
                    {
                        if (dx < 12 || Mathf.Abs(dy) < 12) c = reliefGold;
                    }
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_EmbossingSeal.png");
        return tex;
    }

    // ── 3. ComLab Accountability Form & Clipboard ─────────────────────────────
    private static void GenerateComLabFormModel()
    {
        GameObject root = new GameObject("Model_ComLabClearanceForm");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var boardMat = new Material(shader) { name = "Item_MasoniteBoard_Mat" };
        boardMat.color = new Color(0.48f, 0.35f, 0.22f, 1f);
        if (boardMat.HasProperty("_Smoothness")) boardMat.SetFloat("_Smoothness", 0.40f);

        var chromeMat = new Material(shader) { name = "Item_ChromeClip_Mat" };
        chromeMat.color = new Color(0.90f, 0.92f, 0.95f, 1f);
        if (chromeMat.HasProperty("_Metallic")) chromeMat.SetFloat("_Metallic", 0.98f);
        if (chromeMat.HasProperty("_Smoothness")) chromeMat.SetFloat("_Smoothness", 0.90f);

        var formTex = CreateComLabFormTexture();
        var formMat = new Material(shader) { name = "Item_ComLabForm_Mat", mainTexture = formTex };

        // 1. Chamfered Masonite Clipboard
        var boardMesh = CreateChamferedBoxMesh(w: 0.26f, h: 0.38f, d: 0.014f, bevel: 0.005f);
        var boardGO = new GameObject("ClipboardBase", typeof(MeshFilter), typeof(MeshRenderer));
        boardGO.transform.SetParent(root.transform, false);
        boardGO.GetComponent<MeshFilter>().sharedMesh = boardMesh;
        boardGO.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

        // 2. Heavy Chrome Spring Clip
        var clipMesh = CreateChamferedBoxMesh(w: 0.13f, h: 0.038f, d: 0.024f, bevel: 0.004f);
        var clipGO = new GameObject("ChromeClip", typeof(MeshFilter), typeof(MeshRenderer));
        clipGO.transform.SetParent(root.transform, false);
        clipGO.transform.localPosition = new Vector3(0f, 0.165f, 0.016f);
        clipGO.GetComponent<MeshFilter>().sharedMesh = clipMesh;
        clipGO.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;

        // Torsion Spring Wire on Clip
        var springMesh = CreateTorusMesh(mainRadius: 0.014f, tubeRadius: 0.003f, radialSegments: 20, tubeSegments: 12);
        var springGO = new GameObject("ClipTorsionSpring", typeof(MeshFilter), typeof(MeshRenderer));
        springGO.transform.SetParent(clipGO.transform, false);
        springGO.transform.localPosition = new Vector3(0f, 0.012f, 0f);
        springGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        springGO.GetComponent<MeshFilter>().sharedMesh = springMesh;
        springGO.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;

        // 3. Multi-Sheet Printed Form Paper
        var paperMesh = CreateChamferedBoxMesh(w: 0.225f, h: 0.335f, d: 0.003f, bevel: 0.002f);
        var paperGO = new GameObject("PaperSheet", typeof(MeshFilter), typeof(MeshRenderer));
        paperGO.transform.SetParent(root.transform, false);
        paperGO.transform.localPosition = new Vector3(0f, -0.01f, 0.009f);
        paperGO.GetComponent<MeshFilter>().sharedMesh = paperMesh;
        paperGO.GetComponent<MeshRenderer>().sharedMaterial = formMat;

        // 4. Yellow Sticky Note on Back
        var sticky = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sticky.name = "BackStickyNote";
        sticky.transform.SetParent(root.transform, false);
        sticky.transform.localPosition = new Vector3(0.04f, 0.06f, -0.008f);
        sticky.transform.localRotation = Quaternion.Euler(0f, 180f, 8f);
        sticky.transform.localScale = new Vector3(0.085f, 0.085f, 1f);

        Material stickyMat = new Material(shader) { color = new Color(0.98f, 0.94f, 0.42f, 1f) };
        sticky.GetComponent<MeshRenderer>().sharedMaterial = stickyMat;
        CleanCollider(sticky);

        // Clue: Look at back of clipboard
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "ComLabStickyNote";
        clue.clueTitle = "Dilaw na Sticky Note sa Likod";
        clue.clueDescription = "Sulat ng ComLab In-Charge sa sticky note: 'Sir, pakibalik yung VGA cable sa Server Room bago pirmahan clearance ng mga bata.'";
        clue.revealDirection = -Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_ComLabClearanceForm.prefab");
    }

    private static Texture2D CreateComLabFormTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color paperWhite = new Color(0.96f, 0.95f, 0.91f, 1f);
        Color inkNavy = new Color(0.1f, 0.15f, 0.32f, 1f);
        Color redStamp = new Color(0.85f, 0.15f, 0.15f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = paperWhite;
                // Header bar
                if (y > h - 70 && y < h - 25 && x > 40 && x < w - 40)
                {
                    if (y % 15 < 6) c = inkNavy;
                }
                // Grid table lines
                if (y > 180 && y < h - 140 && x > 35 && x < w - 35)
                {
                    if (y % 40 == 0 || x == 35 || x == w - 35 || x == 120 || x == 340) c = inkNavy;
                }
                // Red "PENDING EQUIPMENT CLEARANCE" stamp
                int dx = x - (w / 2);
                int dy = y - 110;
                if (Mathf.Abs(dy + (dx / 4)) < 24 && Mathf.Abs(dx) < 140)
                {
                    if (Mathf.Abs(dy + (dx / 4)) > 18 || (x * y) % 9 > 3) c = redStamp;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_ComLabForm.png");
        return tex;
    }

    // ── 4. Library Clearance Voucher ──────────────────────────────────────────
    private static void GenerateLibraryVoucherModel()
    {
        GameObject root = new GameObject("Model_LibraryVoucher");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var voucherTex = CreateLibraryVoucherTexture();
        var voucherMat = new Material(shader) { name = "Item_LibraryVoucher_Mat", mainTexture = voucherTex };
        if (voucherMat.HasProperty("_Smoothness")) voucherMat.SetFloat("_Smoothness", 0.18f);

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "PerforatedVoucherSheet";
        sheet.transform.SetParent(root.transform, false);
        sheet.transform.localScale = new Vector3(0.24f, 0.38f, 1f);
        sheet.GetComponent<MeshRenderer>().sharedMaterial = voucherMat;
        CleanCollider(sheet);

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "LibraryVoucherBarcode";
        clue.clueTitle = "Dot-Matrix Printout Timestamp";
        clue.clueDescription = "Serial No: LIB-94-0821 // 'Valid only until 10:00 PM. Overdue accounts will be detained for clearance.'";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 50.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_LibraryVoucher.prefab");
    }

    private static Texture2D CreateLibraryVoucherTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color dotPaper = new Color(0.92f, 0.94f, 0.88f, 1f); // greenish-ivory continuous stationery
        Color purpleRibbonInk = new Color(0.28f, 0.14f, 0.45f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = dotPaper;

                // Tractor feed sprocket margin holes
                if (x < 28 || x > w - 28)
                {
                    if (y % 45 < 16) c = new Color(0.1f, 0.1f, 0.12f, 1f); // sprocket hole
                }

                // Perforated margin tear line
                if (x == 38 || x == w - 38)
                {
                    if (y % 10 < 5) c = new Color(0.65f, 0.7f, 0.65f, 1f);
                }

                // Dot-matrix simulated text lines
                if (x > 60 && x < w - 60 && y > 120 && y < h - 90)
                {
                    if (y % 28 < 9 && ((x + y) % 3 != 0)) c = purpleRibbonInk;
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_LibraryVoucher.png");
        return tex;
    }

    // ── 5. Blank Paper / Clearance Slip ───────────────────────────────────────
    private static void GenerateClearanceSlipModel()
    {
        GameObject root = new GameObject("Model_BlankPaper");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Texture2D slipTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PlayerAssets/signed0.png");
        if (slipTex == null) slipTex = CreateClearanceSlipTexture();
        var slipMat = new Material(shader) { name = "Item_ClearanceSlip_Mat", mainTexture = slipTex };

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "ClearanceSlipSheet";
        sheet.transform.SetParent(root.transform, false);
        sheet.transform.localScale = new Vector3(0.26f, 0.38f, 1f);
        sheet.GetComponent<MeshRenderer>().sharedMaterial = slipMat;
        CleanCollider(sheet);

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "ClearanceSlipWatermark";
        clue.clueTitle = "Opisyal na Clearance Grid";
        clue.clueDescription = "Ang official clearance slip na may anim na signature boxes: Library, Guidance, CCS Dean, Registrar, Cashier, at Executive Vice President!";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 50.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_BlankPaper.prefab");
    }

    private static Texture2D CreateClearanceSlipTexture()
    {
        int w = 512, h = 768;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color paperIvory = new Color(0.95f, 0.93f, 0.88f, 1f);
        Color inkBlack = new Color(0.12f, 0.12f, 0.15f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = paperIvory;
                // Header
                if (y > h - 80 && y < h - 35 && x > 40 && x < w - 40 && y % 16 < 6) c = inkBlack;

                // 6 Signature Boxes Grid (2 columns, 3 rows)
                if (y > 100 && y < h - 140 && x > 40 && x < w - 40)
                {
                    int row = (y - 100) / 160;
                    int col = (x - 40) / 210;
                    int localY = (y - 100) % 160;
                    int localX = (x - 40) % 210;

                    if (localX == 0 || localX == 200 || localY == 0 || localY == 150) c = inkBlack;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_ClearanceSlip.png");
        return tex;
    }

    // ── 6-9. Torn Clearance Fragments (4 Pieces) ──────────────────────────────
    private static void GenerateTornFragmentModels()
    {
        string[] fragNames = { "Top", "Left", "Right", "Bottom" };
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        for (int i = 0; i < 4; i++)
        {
            GameObject root = new GameObject($"Model_Fragment_{fragNames[i]}");
            var tex = CreateFragmentTexture(i);
            var mat = new Material(shader) { name = $"Item_Fragment_{fragNames[i]}_Mat", mainTexture = tex };

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheet.name = $"TornPiece_{fragNames[i]}";
            sheet.transform.SetParent(root.transform, false);
            sheet.transform.localScale = new Vector3(0.15f, 0.20f, 1f);
            sheet.GetComponent<MeshRenderer>().sharedMaterial = mat;
            CleanCollider(sheet);

            var clue = root.AddComponent<InspectableClueMarker>();
            clue.clueID = $"TornFragmentClue_{i}";
            clue.clueTitle = $"Punit na Piraso ng Clearance ({i + 1}/4)";
            clue.clueDescription = $"Punit na piraso ({fragNames[i].ToUpper()}). May markings ng punit sa gilid. Kailangan ng 4 na piraso para mabuo ang opisyal na Clearance Slip.";
            clue.revealDirection = -Vector3.forward;
            clue.detectionAngle = 50.0f;

            SaveAsPrefab(root, $"{PrefabDir}/Model_Fragment_{fragNames[i]}.prefab");
        }
    }

    private static Texture2D CreateFragmentTexture(int index)
    {
        int w = 256, h = 340;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color paper = new Color(0.94f, 0.91f, 0.85f, 1f);
        Color ink = new Color(0.12f, 0.12f, 0.16f, 1f);
        Color pencilNote = new Color(0.28f, 0.28f, 0.32f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = paper;
                // Jagged torn edge simulation
                int jag = (int)(Mathf.Sin(y * 0.25f) * 8f);
                if (x < 12 + jag) c = Color.clear;

                if (x > 35 && x < w - 30 && y % 30 < 7) c = ink;
                if (y < 60 && x > 40 && x < w - 40 && y % 15 < 5) c = pencilNote;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_Fragment_{index}.png");
        return tex;
    }

    // ── 10. Ham & Cheese Sandwich ─────────────────────────────────────────────
    private static void GenerateHamAndCheeseSandwichModel()
    {
        GameObject root = new GameObject("Model_HamAndCheeseSandwich");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var toastMat = new Material(shader) { name = "Item_ToastBread_Mat" };
        toastMat.color = new Color(0.88f, 0.68f, 0.38f, 1f); // golden toasted crumb
        if (toastMat.HasProperty("_Smoothness")) toastMat.SetFloat("_Smoothness", 0.15f);

        var crustMat = new Material(shader) { name = "Item_ToastCrust_Mat" };
        crustMat.color = new Color(0.55f, 0.32f, 0.12f, 1f); // browned crust

        var cheeseMat = new Material(shader) { name = "Item_MeltedCheese_Mat" };
        cheeseMat.color = new Color(1.0f, 0.78f, 0.12f, 1f); // melted cheddar
        if (cheeseMat.HasProperty("_Smoothness")) cheeseMat.SetFloat("_Smoothness", 0.75f);

        var hamMat = new Material(shader) { name = "Item_SlicedHam_Mat" };
        hamMat.color = new Color(0.85f, 0.42f, 0.45f, 1f); // spiced ham

        var wrapTex = CreateSandwichTexture();
        var waxWrapMat = new Material(shader) { name = "Item_WaxPaperWrap_Mat", mainTexture = wrapTex };
        if (waxWrapMat.HasProperty("_Smoothness")) waxWrapMat.SetFloat("_Smoothness", 0.55f);

        // 1. Bottom Toast Slice (Chamfered)
        var botToastMesh = CreateChamferedBoxMesh(w: 0.16f, h: 0.024f, d: 0.16f, bevel: 0.008f);
        var botToastGO = new GameObject("BottomToast", typeof(MeshFilter), typeof(MeshRenderer));
        botToastGO.transform.SetParent(root.transform, false);
        botToastGO.transform.localPosition = new Vector3(0f, -0.022f, 0f);
        botToastGO.GetComponent<MeshFilter>().sharedMesh = botToastMesh;
        botToastGO.GetComponent<MeshRenderer>().sharedMaterial = toastMat;

        // 2. Melted Cheese Layer (Dripping over edges)
        var cheese = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cheese.name = "MeltedCheeseLayer";
        cheese.transform.SetParent(root.transform, false);
        cheese.transform.localPosition = new Vector3(0f, -0.006f, 0f);
        cheese.transform.localScale = new Vector3(0.175f, 0.012f, 0.175f);
        cheese.GetComponent<MeshRenderer>().sharedMaterial = cheeseMat;
        CleanCollider(cheese);

        // 3. Sliced Ham Layer
        var ham = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ham.name = "SpicedHamLayer";
        ham.transform.SetParent(root.transform, false);
        ham.transform.localPosition = new Vector3(0.005f, 0.005f, -0.005f);
        ham.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
        ham.transform.localScale = new Vector3(0.165f, 0.014f, 0.165f);
        ham.GetComponent<MeshRenderer>().sharedMaterial = hamMat;
        CleanCollider(ham);

        // 4. Top Toast Slice (Chamfered)
        var topToastGO = new GameObject("TopToast", typeof(MeshFilter), typeof(MeshRenderer));
        topToastGO.transform.SetParent(root.transform, false);
        topToastGO.transform.localPosition = new Vector3(0f, 0.022f, 0f);
        topToastGO.GetComponent<MeshFilter>().sharedMesh = botToastMesh;
        topToastGO.GetComponent<MeshRenderer>().sharedMaterial = toastMat;

        // 5. Wax Paper Wrapper Sleeve with Masking Tape Label
        var wrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wrap.name = "WaxPaperSleeve";
        wrap.transform.SetParent(root.transform, false);
        wrap.transform.localPosition = new Vector3(0f, 0f, 0.025f);
        wrap.transform.localScale = new Vector3(0.18f, 0.075f, 0.12f);
        wrap.GetComponent<MeshRenderer>().sharedMaterial = waxWrapMat;
        CleanCollider(wrap);

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "SandwichNutrition";
        clue.clueTitle = "Mainit-init na Ham & Cheese";
        clue.clueDescription = "Mula sa lumang cafeteria. May sticker: 'Best before today'. Nakakabawas ng Anxiety kung kakainin.";
        clue.revealDirection = Vector3.up;
        clue.detectionAngle = 52.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_HamAndCheeseSandwich.prefab");
    }

    private static Texture2D CreateSandwichTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color waxWhite = new Color(0.92f, 0.92f, 0.90f, 0.85f);
        Color tapeTan = new Color(0.85f, 0.78f, 0.58f, 1f);
        Color redStamp = new Color(0.82f, 0.14f, 0.14f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = waxWhite;
                // Masking tape strip
                if (y > 90 && y < 166 && x > 25 && x < size - 25)
                {
                    c = tapeTan;
                    // Stamp text on tape: "HOT HAM & CHEESE // CANTEEN"
                    if (y > 115 && y < 140 && x > 40 && x < size - 40 && (x % 14 < 6))
                        c = redStamp;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_SandwichWrapper.png");
        return tex;
    }

    // ── 11. Tactical Flashlight ───────────────────────────────────────────────
    private static void GenerateFlashlightModel()
    {
        GameObject root = new GameObject("Model_Flashlight");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var blackAluminumMat = new Material(shader) { name = "Item_BlackAluminum_Mat" };
        blackAluminumMat.color = new Color(0.12f, 0.13f, 0.15f, 1f);
        if (blackAluminumMat.HasProperty("_Metallic")) blackAluminumMat.SetFloat("_Metallic", 0.92f);
        if (blackAluminumMat.HasProperty("_Smoothness")) blackAluminumMat.SetFloat("_Smoothness", 0.65f);

        var reflectorMat = new Material(shader) { name = "Item_ChromeReflector_Mat" };
        reflectorMat.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        if (reflectorMat.HasProperty("_Metallic")) reflectorMat.SetFloat("_Metallic", 0.98f);
        if (reflectorMat.HasProperty("_Smoothness")) reflectorMat.SetFloat("_Smoothness", 0.95f);

        var bulbMat = new Material(shader) { name = "Item_KryptonBulb_Mat" };
        bulbMat.color = new Color(1.0f, 0.95f, 0.65f, 1f);

        var rubberSwitchMat = new Material(shader) { name = "Item_RubberSwitch_Mat" };
        rubberSwitchMat.color = new Color(0.85f, 0.12f, 0.12f, 1f); // red tactical switch

        // 1. Knurled Grip Barrel Handle (Reeded cylinder)
        var barrelMesh = CreateReededCylinderMesh(radius: 0.024f, height: 0.18f, numReeds: 32, reedDepth: 0.002f);
        var barrelGO = new GameObject("KnurledBarrel", typeof(MeshFilter), typeof(MeshRenderer));
        barrelGO.transform.SetParent(root.transform, false);
        barrelGO.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        barrelGO.GetComponent<MeshFilter>().sharedMesh = barrelMesh;
        barrelGO.GetComponent<MeshRenderer>().sharedMaterial = blackAluminumMat;

        // 2. Flared Head Bezel with Cooling Fins (CreateLatheMesh)
        Vector2[] headProfile = new Vector2[] {
            new Vector2(0.024f, 0.07f),
            new Vector2(0.038f, 0.09f),  // flare
            new Vector2(0.046f, 0.12f),  // strike bezel
            new Vector2(0.046f, 0.16f),
            new Vector2(0.040f, 0.16f),  // lens rim
            new Vector2(0.001f, 0.14f)   // parabolic reflector center
        };
        var headMesh = CreateLatheMesh(headProfile, radialSegments: 28);
        var headGO = new GameObject("TacticalHead", typeof(MeshFilter), typeof(MeshRenderer));
        headGO.transform.SetParent(root.transform, false);
        headGO.GetComponent<MeshFilter>().sharedMesh = headMesh;
        headGO.GetComponent<MeshRenderer>().sharedMaterial = blackAluminumMat;

        // 3. Chrome Reflector Dish
        var reflector = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        reflector.name = "ParabolicReflector";
        reflector.transform.SetParent(root.transform, false);
        reflector.transform.localPosition = new Vector3(0f, 0.148f, 0f);
        reflector.transform.localScale = new Vector3(0.072f, 0.006f, 0.072f);
        reflector.GetComponent<MeshRenderer>().sharedMaterial = reflectorMat;
        CleanCollider(reflector);

        // 4. Yellow Emitter Bulb Filament
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "EmitterBulb";
        bulb.transform.SetParent(root.transform, false);
        bulb.transform.localPosition = new Vector3(0f, 0.152f, 0f);
        bulb.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f);
        bulb.GetComponent<MeshRenderer>().sharedMaterial = bulbMat;
        CleanCollider(bulb);

        // 5. Tailcap with Machined Lanyard Ring & Scratched Serial
        var tailcap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tailcap.name = "Tailcap";
        tailcap.transform.SetParent(root.transform, false);
        tailcap.transform.localPosition = new Vector3(0f, -0.118f, 0f);
        tailcap.transform.localScale = new Vector3(0.052f, 0.022f, 0.052f);
        tailcap.GetComponent<MeshRenderer>().sharedMaterial = blackAluminumMat;
        CleanCollider(tailcap);

        // Red Rubber Clicky Switch Button
        var clickBtn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        clickBtn.name = "RubberTailSwitch";
        clickBtn.transform.SetParent(tailcap.transform, false);
        clickBtn.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        clickBtn.transform.localScale = new Vector3(0.45f, 0.25f, 0.45f);
        clickBtn.GetComponent<MeshRenderer>().sharedMaterial = rubberSwitchMat;
        CleanCollider(clickBtn);

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "FlashlightSerial";
        clue.clueTitle = "Serial Number sa Tailcap";
        clue.clueDescription = "May nakagasgas na serial number sa takip ng baterya sa ilalim: 'SG-2 DELA CRUZ'. Paboritong gamit ng security guard sa lobby entrance.";
        clue.revealDirection = -Vector3.up;
        clue.detectionAngle = 45.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_Flashlight.prefab");
    }

    // ── 12. Ream of Bond Paper ────────────────────────────────────────────────
    private static void GeneratePrinterPaperModel()
    {
        GameObject root = new GameObject("Model_PrinterPaper");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var paperTex = CreatePrinterPaperTexture();
        var wrapMat = new Material(shader) { name = "Item_PrinterPaperWrap_Mat", mainTexture = paperTex };

        var blockMesh = CreateChamferedBoxMesh(w: 0.24f, h: 0.085f, d: 0.34f, bevel: 0.006f);
        var blockGO = new GameObject("PaperReamBlock", typeof(MeshFilter), typeof(MeshRenderer));
        blockGO.transform.SetParent(root.transform, false);
        blockGO.GetComponent<MeshFilter>().sharedMesh = blockMesh;
        blockGO.GetComponent<MeshRenderer>().sharedMaterial = wrapMat;

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "PaperSpecs";
        clue.clueTitle = "Kaha ng Bond Paper";
        clue.clueDescription = "Substance 20 / 70 GSM / 500 Sheets. Puwedeng gamitin sa photocopier o printer sa 2nd Floor Admin Office.";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 50.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_PrinterPaper.prefab");
    }

    private static Texture2D CreatePrinterPaperTexture()
    {
        int w = 512, h = 512;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color kraftBlue = new Color(0.15f, 0.38f, 0.72f, 1f);
        Color brandWhite = new Color(0.98f, 0.98f, 0.98f, 1f);
        Color redAccent = new Color(0.85f, 0.15f, 0.15f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = kraftBlue;
                // Center brand badge
                if (x > 90 && x < w - 90 && y > 140 && y < h - 140)
                {
                    c = brandWhite;
                    if (y > 230 && y < 275 && (x % 16 < 8)) c = kraftBlue;
                    if (y > 170 && y < 200 && (x % 14 < 7)) c = redAccent;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_PrinterPaper.png");
        return tex;
    }

    // ── 13. Student Handbook & Notes ──────────────────────────────────────────
    private static void GenerateStudentHandbookModel()
    {
        GameObject root = new GameObject("Model_StudentHandbook");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Texture2D coverTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ItemInspectTextures/Tex_StudentHandbook.png");
        if (coverTex == null) coverTex = CreateStudentHandbookTexture();
        var bookMat = new Material(shader) { name = "Item_StudentHandbook_Mat", mainTexture = coverTex };

        var ribbonMat = new Material(shader) { name = "Item_RibbonBookmark_Mat" };
        ribbonMat.color = new Color(0.78f, 0.12f, 0.18f, 1f); // burgundy red satin

        // 1. Chamfered Book Cover
        var bookMesh = CreateChamferedBoxMesh(w: 0.22f, h: 0.30f, d: 0.045f, bevel: 0.005f);
        var bookGO = new GameObject("HandbookBody", typeof(MeshFilter), typeof(MeshRenderer));
        bookGO.transform.SetParent(root.transform, false);
        bookGO.GetComponent<MeshFilter>().sharedMesh = bookMesh;
        bookGO.GetComponent<MeshRenderer>().sharedMaterial = bookMat;

        // 2. Satin Ribbon Bookmark Draping from Bottom Pages
        var ribbon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ribbon.name = "RibbonBookmark";
        ribbon.transform.SetParent(root.transform, false);
        ribbon.transform.localPosition = new Vector3(0.03f, -0.17f, 0f);
        ribbon.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        ribbon.transform.localScale = new Vector3(0.024f, 0.09f, 1f);
        ribbon.GetComponent<MeshRenderer>().sharedMaterial = ribbonMat;
        CleanCollider(ribbon);

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "HandbookNotes";
        clue.clueTitle = "Sulat-Kamay sa Likod ng Handbook";
        clue.clueDescription = "Babala mula sa dating estudyante sa likod ng cover: 'Huwag mong tatalikuran ang estatwa sa Diliman Wing. Kapag tumingin ka, huwag kang kukurap!'";
        clue.revealDirection = -Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_StudentHandbook.prefab");
    }

    private static Texture2D CreateStudentHandbookTexture()
    {
        int w = 512, h = 512;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color navyLeather = new Color(0.08f, 0.12f, 0.25f, 1f);
        Color goldFoil = new Color(0.95f, 0.82f, 0.32f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = navyLeather;
                // Gold foil border frame
                if ((x == 30 || x == w - 30 || y == 30 || y == h - 30) ||
                    (x == 38 || x == w - 38 || y == 38 || y == h - 38))
                {
                    c = goldFoil;
                }
                // Center gold emblem
                float distCenter = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.55f));
                if (distCenter < 55)
                {
                    if (Mathf.Abs(distCenter - 50) < 4f || Mathf.Abs(distCenter - 30) < 3f) c = goldFoil;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_StudentHandbook.png");
        return tex;
    }

    // ── 14. Professional Spring-Steel Lockpick & Tension Wrench Toolset ─────────
    private static void GenerateLockpinModel()
    {
        GameObject root = new GameObject("Model_Lockpin");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var steelMat = new Material(shader) { name = "Item_SpringSteel_Mat" };
        steelMat.color = new Color(0.72f, 0.75f, 0.78f, 1f); // Machined silver spring steel
        if (steelMat.HasProperty("_Metallic")) steelMat.SetFloat("_Metallic", 0.95f);
        if (steelMat.HasProperty("_Smoothness")) steelMat.SetFloat("_Smoothness", 0.82f);

        var handleMat = new Material(shader) { name = "Item_KnurledGrip_Mat" };
        handleMat.color = new Color(0.12f, 0.13f, 0.15f, 1f); // Dark textured polymer grip
        if (handleMat.HasProperty("_Smoothness")) handleMat.SetFloat("_Smoothness", 0.28f);

        var chromeMat = new Material(shader) { name = "Item_ChromeFerrule_Mat" };
        chromeMat.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        if (chromeMat.HasProperty("_Metallic")) chromeMat.SetFloat("_Metallic", 0.98f);
        if (chromeMat.HasProperty("_Smoothness")) chromeMat.SetFloat("_Smoothness", 0.92f);

        // ================= TOOL 1: HOOK LOCKPICK =================
        var pickGO = new GameObject("Lockpick_Tool");
        pickGO.transform.SetParent(root.transform, false);
        pickGO.transform.localPosition = new Vector3(-0.015f, 0f, 0f);

        // 1. Grip Handle (Molded ergonomic handle)
        var handleMesh = CreateChamferedBoxMesh(w: 0.014f, h: 0.082f, d: 0.0055f, bevel: 0.0016f);
        var handle = new GameObject("RubberHandle", typeof(MeshFilter), typeof(MeshRenderer));
        handle.transform.SetParent(pickGO.transform, false);
        handle.transform.localPosition = new Vector3(0f, -0.045f, 0f);
        handle.GetComponent<MeshFilter>().sharedMesh = handleMesh;
        handle.GetComponent<MeshRenderer>().sharedMaterial = handleMat;

        // Grip Ribs / Texture ridges
        for (int r = 0; r < 5; r++)
        {
            var rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = $"GripRib_{r}";
            rib.transform.SetParent(handle.transform, false);
            rib.transform.localPosition = new Vector3(0f, -0.026f + r * 0.013f, 0f);
            rib.transform.localScale = new Vector3(0.0146f, 0.0022f, 0.0062f);
            rib.GetComponent<MeshRenderer>().sharedMaterial = handleMat;
            CleanCollider(rib);
        }

        // 2. Chrome Ferrule Collar
        var ferrule = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ferrule.name = "ChromeFerrule";
        ferrule.transform.SetParent(pickGO.transform, false);
        ferrule.transform.localPosition = new Vector3(0f, 0.001f, 0f);
        ferrule.transform.localScale = new Vector3(0.008f, 0.006f, 0.008f);
        ferrule.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
        CleanCollider(ferrule);

        // 3. Spring Steel Pick Shank
        var shank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shank.name = "PickShank";
        shank.transform.SetParent(pickGO.transform, false);
        shank.transform.localPosition = new Vector3(0f, 0.046f, 0f);
        shank.transform.localScale = new Vector3(0.0032f, 0.084f, 0.0014f);
        shank.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
        CleanCollider(shank);

        // 4. Hook Tip
        var hookTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hookTip.name = "HookTip";
        hookTip.transform.SetParent(pickGO.transform, false);
        hookTip.transform.localPosition = new Vector3(0.0035f, 0.090f, 0f);
        hookTip.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
        hookTip.transform.localScale = new Vector3(0.0028f, 0.014f, 0.0014f);
        hookTip.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
        CleanCollider(hookTip);

        // ================= TOOL 2: L-SHAPED TENSION WRENCH =================
        var wrenchGO = new GameObject("Tension_Wrench");
        wrenchGO.transform.SetParent(root.transform, false);
        wrenchGO.transform.localPosition = new Vector3(0.018f, -0.010f, 0f);

        // Long Torque Handle Arm
        var wrenchHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wrenchHandle.name = "TorqueArm";
        wrenchHandle.transform.SetParent(wrenchGO.transform, false);
        wrenchHandle.transform.localPosition = new Vector3(0f, -0.025f, 0f);
        wrenchHandle.transform.localScale = new Vector3(0.0042f, 0.095f, 0.0016f);
        wrenchHandle.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
        CleanCollider(wrenchHandle);

        // 90-degree Insertion Blade Tip
        var wrenchTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wrenchTip.name = "KeywayInsertionBlade";
        wrenchTip.transform.SetParent(wrenchGO.transform, false);
        wrenchTip.transform.localPosition = new Vector3(0.012f, 0.024f, 0f);
        wrenchTip.transform.localScale = new Vector3(0.024f, 0.0042f, 0.0016f);
        wrenchTip.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
        CleanCollider(wrenchTip);

        // Clue 1: Hook pick tip
        var clue1 = root.AddComponent<InspectableClueMarker>();
        clue1.clueID = "LockpickHook";
        clue1.clueTitle = "Precision Hook Pick & Wrench";
        clue1.clueDescription = "Propesyonal na spring-steel lockpick at tension wrench. Ginagamit pang-angat ng tumbler pins hanggang sa shear line habang pinipihit ang cylinder plug.";
        clue1.revealDirection = Vector3.forward;
        clue1.detectionAngle = 55.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_Lockpin.prefab");
    }

    // ── 15. Philippine 1-Peso Coin ────────────────────────────────────────────
    private static void GenerateCoinModel()
    {
        GameObject root = new GameObject("Model_Coin");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var coinTex = CreateCoinTexture();
        var coinMat = new Material(shader) { name = "Item_NickelCoin_Mat", mainTexture = coinTex };
        coinMat.color = new Color(0.92f, 0.92f, 0.94f, 1f);
        if (coinMat.HasProperty("_Metallic")) coinMat.SetFloat("_Metallic", 0.96f);
        if (coinMat.HasProperty("_Smoothness")) coinMat.SetFloat("_Smoothness", 0.86f);

        // Reeded edge coin mesh (48 serrations)
        var coinMesh = CreateReededCylinderMesh(radius: 0.10f, height: 0.014f, numReeds: 48, reedDepth: 0.004f);
        var coinGO = new GameObject("ReededCoinBody", typeof(MeshFilter), typeof(MeshRenderer));
        coinGO.transform.SetParent(root.transform, false);
        coinGO.GetComponent<MeshFilter>().sharedMesh = coinMesh;
        coinGO.GetComponent<MeshRenderer>().sharedMaterial = coinMat;

        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "PesoCoinInfo";
        clue.clueTitle = "1-Piso Pilipino (BSP Coin)";
        clue.clueDescription = "1995 Bangko Sentral ng Pilipinas nickel-brass coin. May mukha ni Dr. José Rizal sa harap at BSP seal sa likod.";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 50.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_Coin.prefab");
    }

    private static Texture2D CreateCoinTexture()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color nickelSilver = new Color(0.88f, 0.88f, 0.90f, 1f);
        Color reliefHighlight = new Color(0.98f, 0.98f, 1.0f, 1f);
        Color shadeRing = new Color(0.70f, 0.70f, 0.74f, 1f);

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                Color c = nickelSilver;

                if (Mathf.Abs(d - radius) < 8f) c = reliefHighlight; // raised rim
                if (Mathf.Abs(d - (radius - 12f)) < 3f) c = shadeRing; // rim groove

                // Circular lettering ring: "REPUBLIKA NG PILIPINAS * 1995 *"
                if (d > radius - 45f && d < radius - 15f)
                {
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    if (Mathf.Abs(Mathf.Sin(angle * 22f)) > 0.45f) c = reliefHighlight;
                }

                // Center José Rizal sculpted portrait silhouette
                if (d < radius - 55f)
                {
                    int dx = x - (int)center.x;
                    int dy = y - (int)center.y;
                    if (dx > -50 && dx < 45 && dy > -80 && dy < 75)
                    {
                        if (dx * dy % 7 > 2) c = reliefHighlight;
                    }
                }

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_PesoCoin.png");
        return tex;
    }

    // ── 16. VGA Monitor Cable ─────────────────────────────────────────────────
    private static void GenerateVGACableModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material blueRubberMat = new Material(litShader) { color = new Color(0.12f, 0.28f, 0.68f) };
        if (blueRubberMat.HasProperty("_Smoothness")) blueRubberMat.SetFloat("_Smoothness", 0.45f);

        Material metalMat = new Material(litShader) { color = new Color(0.88f, 0.88f, 0.92f) };
        if (metalMat.HasProperty("_Metallic")) metalMat.SetFloat("_Metallic", 0.96f);
        if (metalMat.HasProperty("_Smoothness")) metalMat.SetFloat("_Smoothness", 0.85f);

        Material goldPinMat = new Material(litShader) { color = new Color(1.0f, 0.84f, 0.25f) };
        if (goldPinMat.HasProperty("_Metallic")) goldPinMat.SetFloat("_Metallic", 0.98f);
        if (goldPinMat.HasProperty("_Smoothness")) goldPinMat.SetFloat("_Smoothness", 0.90f);

        GameObject root = new GameObject("Model_VGACable");

        // 1. Helical Coiled Cable Loop (CreateTorusMesh)
        var coilMesh = CreateTorusMesh(mainRadius: 0.12f, tubeRadius: 0.016f, radialSegments: 32, tubeSegments: 16);
        var coilGO = new GameObject("CableCoil", typeof(MeshFilter), typeof(MeshRenderer));
        coilGO.transform.SetParent(root.transform, false);
        coilGO.GetComponent<MeshFilter>().sharedMesh = coilMesh;
        coilGO.GetComponent<MeshRenderer>().sharedMaterial = blueRubberMat;

        // 2. Molded Connector Hood (Chamfered Box)
        var hoodMesh = CreateChamferedBoxMesh(w: 0.10f, h: 0.046f, d: 0.14f, bevel: 0.008f);
        var hoodGO = new GameObject("ConnectorHood", typeof(MeshFilter), typeof(MeshRenderer));
        hoodGO.transform.SetParent(root.transform, false);
        hoodGO.transform.localPosition = new Vector3(0.13f, 0.035f, 0.06f);
        hoodGO.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        hoodGO.GetComponent<MeshFilter>().sharedMesh = hoodMesh;
        hoodGO.GetComponent<MeshRenderer>().sharedMaterial = blueRubberMat;

        // 3. Segmented Accordion Strain Relief Boot
        for (int i = 0; i < 3; i++)
        {
            var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rib.name = $"StrainRib_{i}";
            rib.transform.SetParent(hoodGO.transform, false);
            rib.transform.localPosition = new Vector3(0f, 0f, -0.075f - i * 0.016f);
            rib.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rib.transform.localScale = new Vector3(0.042f - i * 0.006f, 0.006f, 0.042f - i * 0.006f);
            rib.GetComponent<MeshRenderer>().sharedMaterial = blueRubberMat;
            CleanCollider(rib);
        }

        // 4. D-Sub 15-Pin Beveled Shield (Chamfered Box)
        var shieldMesh = CreateChamferedBoxMesh(w: 0.076f, h: 0.032f, d: 0.026f, bevel: 0.004f);
        var shieldGO = new GameObject("DSubShield", typeof(MeshFilter), typeof(MeshRenderer));
        shieldGO.transform.SetParent(hoodGO.transform, false);
        shieldGO.transform.localPosition = new Vector3(0f, 0f, 0.078f);
        shieldGO.GetComponent<MeshFilter>().sharedMesh = shieldMesh;
        shieldGO.GetComponent<MeshRenderer>().sharedMaterial = metalMat;

        // 5. 15 Individual Gold Contact Pins in 3 Staggered Rows!
        for (int row = -1; row <= 1; row++)
        {
            int cols = 5;
            for (int col = 0; col < cols; col++)
            {
                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = $"GoldPin_{row}_{col}";
                pin.transform.SetParent(shieldGO.transform, false);
                float posX = (col - 2) * 0.011f + (row == 0 ? 0.0055f : 0f);
                float posY = row * 0.007f;
                pin.transform.localPosition = new Vector3(posX, posY, 0.008f);
                pin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pin.transform.localScale = new Vector3(0.0022f, 0.007f, 0.0022f);
                pin.GetComponent<MeshRenderer>().sharedMaterial = goldPinMat;
                CleanCollider(pin);
            }
        }

        // 6. Dual Knurled Thumbscrews with Slotted Heads
        for (int i = -1; i <= 1; i += 2)
        {
            var screwMesh = CreateReededCylinderMesh(radius: 0.012f, height: 0.035f, numReeds: 24, reedDepth: 0.002f);
            var screwGO = new GameObject($"Thumbscrew_{i}", typeof(MeshFilter), typeof(MeshRenderer));
            screwGO.transform.SetParent(hoodGO.transform, false);
            screwGO.transform.localPosition = new Vector3(i * 0.052f, 0f, 0.045f);
            screwGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            screwGO.GetComponent<MeshFilter>().sharedMesh = screwMesh;
            screwGO.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
        }

        // Label Texture on Connector
        var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
        label.name = "ConnectorLabel";
        label.transform.SetParent(hoodGO.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.024f, 0f);
        label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        label.transform.localScale = new Vector3(0.07f, 0.045f, 1f);

        Texture2D vgaTex = CreateVGACableTexture();
        Material labelMat = new Material(litShader) { mainTexture = vgaTex };
        label.GetComponent<MeshRenderer>().sharedMaterial = labelMat;
        CleanCollider(label);

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "VGACableSerial";
        clue.clueTitle = "VGA Connector Serial Code";
        clue.clueDescription = "May nakatatak sa connector boot: 'CCS-SRV-TERMINAL-01 // 800x600 60Hz'. Para ito sa Master Database Terminal sa Room 105 Computer Lab!";
        clue.revealDirection = Vector3.up;
        clue.detectionAngle = 50.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_VGACable.prefab");
    }

    private static Texture2D CreateVGACableTexture()
    {
        int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bg = new Color(0.12f, 0.14f, 0.18f, 1f);
        Color ink = new Color(0.92f, 0.94f, 0.98f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                if (x > 12 && x < w - 12 && y > 10 && y < h - 10)
                {
                    if (y % 16 < 4) c = ink;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_VGACable.png");
        return tex;
    }

    // ── 17. Ceramic Cartridge Fuse 15A ────────────────────────────────────────
    private static void GenerateCeramicFuseModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material ceramicMat = new Material(litShader) { color = new Color(0.94f, 0.92f, 0.88f) };
        if (ceramicMat.HasProperty("_Smoothness")) ceramicMat.SetFloat("_Smoothness", 0.65f);

        Material brassMat = new Material(litShader) { color = new Color(0.88f, 0.72f, 0.25f) };
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.96f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.85f);

        Material glassMat = new Material(litShader) { color = new Color(0.85f, 0.90f, 0.95f, 0.6f) };
        Material wireMat = new Material(litShader) { color = new Color(0.92f, 0.25f, 0.25f) };

        GameObject root = new GameObject("Model_CeramicFuse");

        // 1. Ceramic Insulator Cylinder Body
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CeramicBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.09f, 0.16f, 0.09f);
        body.GetComponent<MeshRenderer>().sharedMaterial = ceramicMat;
        CleanCollider(body);

        // 2. Stamped Brass End Caps with Crimped Rings
        for (int i = -1; i <= 1; i += 2)
        {
            var capMesh = CreateReededCylinderMesh(radius: 0.048f, height: 0.042f, numReeds: 32, reedDepth: 0.002f);
            var capGO = new GameObject($"BrassEndCap_{i}", typeof(MeshFilter), typeof(MeshRenderer));
            capGO.transform.SetParent(root.transform, false);
            capGO.transform.localPosition = new Vector3(0f, i * 0.165f, 0f);
            capGO.GetComponent<MeshFilter>().sharedMesh = capMesh;
            capGO.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        }

        // 3. Central Inspection Glass Window
        var window = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        window.name = "InspectionWindow";
        window.transform.SetParent(root.transform, false);
        window.transform.localPosition = new Vector3(0f, 0f, -0.042f);
        window.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        window.transform.localScale = new Vector3(0.024f, 0.008f, 0.024f);
        window.GetComponent<MeshRenderer>().sharedMaterial = glassMat;
        CleanCollider(window);

        // Fuse Link Wire inside window
        var wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wire.name = "FuseElementWire";
        wire.transform.SetParent(window.transform, false);
        wire.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
        wire.GetComponent<MeshRenderer>().sharedMaterial = wireMat;
        CleanCollider(wire);

        // 4. Stamped Specs Label
        var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
        label.name = "SpecsLabel";
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, 0.046f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        label.transform.localScale = new Vector3(0.075f, 0.13f, 1f);

        Texture2D fuseTex = CreateFuseTexture();
        Material labelMat = new Material(litShader) { mainTexture = fuseTex };
        label.GetComponent<MeshRenderer>().sharedMaterial = labelMat;
        CleanCollider(label);

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "CeramicFuseRating";
        clue.clueTitle = "Electrical Fuse Amperage Rating";
        clue.clueDescription = "Nakaimprenta sa ceramic body: '15 AMPERES // 250V // RATING: COMLAB & FACULTY WING GRID'. Ito ang kailangan para ibalik ang kuryente sa distribution breaker box!";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_CeramicFuse.prefab");
    }

    private static Texture2D CreateFuseTexture()
    {
        int w = 128, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color ceramicBg = new Color(0.94f, 0.92f, 0.88f, 1f);
        Color redInk = new Color(0.85f, 0.12f, 0.12f, 1f);
        Color blackInk = new Color(0.1f, 0.1f, 0.1f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = ceramicBg;
                if (x > 18 && x < w - 18 && y > 80 && y < 176)
                {
                    if (y > 115 && y < 145) c = redInk;
                    else c = blackInk;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_CeramicFuse.png");
        return tex;
    }

    // ── 18. Counseling Audio Cassette Tape ────────────────────────────────────
    private static void GenerateCassetteTapeModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material acrylicMat = new Material(litShader) { color = new Color(0.16f, 0.18f, 0.20f) };
        if (acrylicMat.HasProperty("_Smoothness")) acrylicMat.SetFloat("_Smoothness", 0.88f);

        Material whiteSpoolMat = new Material(litShader) { color = new Color(0.95f, 0.95f, 0.96f) };
        Material tapeBrownMat = new Material(litShader) { color = new Color(0.24f, 0.14f, 0.08f) };
        if (tapeBrownMat.HasProperty("_Smoothness")) tapeBrownMat.SetFloat("_Smoothness", 0.70f);

        Material screwMat = new Material(litShader) { color = new Color(0.88f, 0.88f, 0.92f) };
        if (screwMat.HasProperty("_Metallic")) screwMat.SetFloat("_Metallic", 0.98f);

        GameObject root = new GameObject("Model_CassetteTape");

        // 1. Chamfered Compact Cassette Shell
        var shellMesh = CreateChamferedBoxMesh(w: 0.32f, h: 0.20f, d: 0.036f, bevel: 0.005f);
        var shellGO = new GameObject("CassetteShell", typeof(MeshFilter), typeof(MeshRenderer));
        shellGO.transform.SetParent(root.transform, false);
        shellGO.GetComponent<MeshFilter>().sharedMesh = shellMesh;
        shellGO.GetComponent<MeshRenderer>().sharedMaterial = acrylicMat;

        // 2. Magnetic Tape Ribbon Core
        var centerTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerTape.name = "MagneticTapeSpool";
        centerTape.transform.SetParent(root.transform, false);
        centerTape.transform.localScale = new Vector3(0.18f, 0.082f, 0.038f);
        centerTape.GetComponent<MeshRenderer>().sharedMaterial = tapeBrownMat;
        CleanCollider(centerTape);

        // 3. Dual 6-Toothed Drive Hubs
        for (int i = -1; i <= 1; i += 2)
        {
            var hubMesh = CreateReededCylinderMesh(radius: 0.026f, height: 0.040f, numReeds: 6, reedDepth: 0.006f);
            var hubGO = new GameObject($"DriveHub_{i}", typeof(MeshFilter), typeof(MeshRenderer));
            hubGO.transform.SetParent(root.transform, false);
            hubGO.transform.localPosition = new Vector3(i * 0.075f, 0f, 0f);
            hubGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hubGO.GetComponent<MeshFilter>().sharedMesh = hubMesh;
            hubGO.GetComponent<MeshRenderer>().sharedMaterial = whiteSpoolMat;
        }

        // 4. Tiny Corner Assembly Screws (5 screws)
        Vector2[] screwPositions = {
            new Vector2(-0.14f, 0.085f),
            new Vector2(0.14f, 0.085f),
            new Vector2(-0.14f, -0.085f),
            new Vector2(0.14f, -0.085f),
            new Vector2(0f, 0.085f)
        };
        for (int i = 0; i < screwPositions.Length; i++)
        {
            var screw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            screw.name = $"AssemblyScrew_{i}";
            screw.transform.SetParent(root.transform, false);
            screw.transform.localPosition = new Vector3(screwPositions[i].x, screwPositions[i].y, 0f);
            screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            screw.transform.localScale = new Vector3(0.007f, 0.039f, 0.007f);
            screw.GetComponent<MeshRenderer>().sharedMaterial = screwMat;
            CleanCollider(screw);
        }

        // Side A Label (Front)
        var labelFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
        labelFront.name = "Label_SideA";
        labelFront.transform.SetParent(root.transform, false);
        labelFront.transform.localPosition = new Vector3(0f, 0.015f, -0.019f);
        labelFront.transform.localScale = new Vector3(0.26f, 0.12f, 1f);

        Texture2D tapeTexFront = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ItemInspectTextures/Tex_CassetteTape_A.png");
        if (tapeTexFront == null) tapeTexFront = CreateTapeTexture(isSideB: false);
        Material matA = new Material(litShader) { mainTexture = tapeTexFront };
        labelFront.GetComponent<MeshRenderer>().sharedMaterial = matA;
        CleanCollider(labelFront);

        // Side B Label (Back - Contains Clue!)
        var labelBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
        labelBack.name = "Label_SideB_Clue";
        labelBack.transform.SetParent(root.transform, false);
        labelBack.transform.localPosition = new Vector3(0f, 0.015f, 0.019f);
        labelBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        labelBack.transform.localScale = new Vector3(0.26f, 0.12f, 1f);

        Texture2D tapeTexBack = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ItemInspectTextures/Tex_CassetteTape_B.png");
        if (tapeTexBack == null) tapeTexBack = CreateTapeTexture(isSideB: true);
        Material matB = new Material(litShader) { mainTexture = tapeTexBack };
        labelBack.GetComponent<MeshRenderer>().sharedMaterial = matB;
        CleanCollider(labelBack);

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "CassetteSafeCode";
        clue.clueTitle = "Lihim na Code sa Tape Reel";
        clue.clueDescription = "May sulat-kamay gamit ang ballpen sa likod ng cassette label: 'SAFE CODE: 4-1-9'. Ito ang combination sa Guidance Wall Safe sa likod ng frame sa Room 207!";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_CassetteTape.prefab");
    }

    private static Texture2D CreateTapeTexture(bool isSideB)
    {
        int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color creamPaper = new Color(0.92f, 0.90f, 0.82f, 1f);
        Color blueInk = new Color(0.12f, 0.22f, 0.55f, 1f);
        Color redInk = new Color(0.78f, 0.12f, 0.12f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = creamPaper;
                if (y > h - 24) c = isSideB ? redInk : blueInk;
                if (y > 20 && y < h - 40 && x > 50 && x < w - 50) c = new Color(0.2f, 0.2f, 0.22f, 1f);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_CassetteTape_{(isSideB ? "B" : "A")}.png");
        return tex;
    }

    // ── 19. Security Guard Incident Logbook ───────────────────────────────────
    private static void GenerateGuardLogbookModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material coverMat = new Material(litShader) { color = new Color(0.08f, 0.09f, 0.11f) };
        if (coverMat.HasProperty("_Smoothness")) coverMat.SetFloat("_Smoothness", 0.35f);

        Material goldMat = new Material(litShader) { color = new Color(0.92f, 0.78f, 0.25f) };
        if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.95f);

        GameObject root = new GameObject("Model_GuardLogbook");

        // 1. Chamfered Hardcover Book Body
        var coverMesh = CreateChamferedBoxMesh(w: 0.25f, h: 0.34f, d: 0.046f, bevel: 0.005f);
        var coverGO = new GameObject("BookCover", typeof(MeshFilter), typeof(MeshRenderer));
        coverGO.transform.SetParent(root.transform, false);
        coverGO.GetComponent<MeshFilter>().sharedMesh = coverMesh;
        coverGO.GetComponent<MeshRenderer>().sharedMaterial = coverMat;

        // 2. Embossed Security Star Shield Badge on Front
        var badge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        badge.name = "SecurityBadge";
        badge.transform.SetParent(coverGO.transform, false);
        badge.transform.localPosition = new Vector3(0f, 0.06f, -0.024f);
        badge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        badge.transform.localScale = new Vector3(0.065f, 0.003f, 0.065f);
        badge.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
        CleanCollider(badge);

        // 3. Open Incident Log Page on Back Cover
        var logSheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        logSheet.name = "IncidentLogPage";
        logSheet.transform.SetParent(coverGO.transform, false);
        logSheet.transform.localPosition = new Vector3(0f, 0f, 0.024f);
        logSheet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        logSheet.transform.localScale = new Vector3(0.22f, 0.31f, 1f);

        Texture2D logTex = CreateGuardLogTexture();
        Material logMat = new Material(litShader) { mainTexture = logTex };
        logSheet.GetComponent<MeshRenderer>().sharedMaterial = logMat;
        CleanCollider(logSheet);

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "GuardLogbookCode";
        clue.clueTitle = "Emergency Keypad Code sa Logbook";
        clue.clueDescription = "Sulat-kamay ni Guard Dela Cruz: 'Emergency Lobby Override Keypad: 9-4-2-1. Kapag nag-lockdown ang magnetic exit gates, ito lang ang magpapabukas sa control relay!'";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_GuardLogbook.prefab");
    }

    private static Texture2D CreateGuardLogTexture()
    {
        int w = 256, h = 340;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color agedPaper = new Color(0.92f, 0.88f, 0.78f, 1f);
        Color redLine = new Color(0.78f, 0.22f, 0.22f, 1f);
        Color inkNavy = new Color(0.1f, 0.15f, 0.35f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = agedPaper;
                if (x == 32) c = redLine;
                if (y % 18 == 0) c = new Color(0.65f, 0.72f, 0.85f, 1f);
                if (x > 40 && x < w - 20 && y % 18 > 4 && y % 18 < 9)
                {
                    if ((x * y) % 7 > 2) c = inkNavy;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_GuardLogbook.png");
        return tex;
    }

    // ── 20. Clinic Medical Record Slip ────────────────────────────────────────
    private static void GenerateMedicalSlipModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material aluminumMat = new Material(litShader) { color = new Color(0.85f, 0.86f, 0.89f) };
        if (aluminumMat.HasProperty("_Metallic")) aluminumMat.SetFloat("_Metallic", 0.95f);
        if (aluminumMat.HasProperty("_Smoothness")) aluminumMat.SetFloat("_Smoothness", 0.85f);

        GameObject root = new GameObject("Model_MedicalSlip");

        // 1. Chamfered Aluminum Medical Chart Board
        var boardMesh = CreateChamferedBoxMesh(w: 0.25f, h: 0.36f, d: 0.012f, bevel: 0.004f);
        var boardGO = new GameObject("AluminumChartBase", typeof(MeshFilter), typeof(MeshRenderer));
        boardGO.transform.SetParent(root.transform, false);
        boardGO.GetComponent<MeshFilter>().sharedMesh = boardMesh;
        boardGO.GetComponent<MeshRenderer>().sharedMaterial = aluminumMat;

        // 2. Chrome Spring Clasp
        var clipMesh = CreateChamferedBoxMesh(w: 0.12f, h: 0.035f, d: 0.020f, bevel: 0.003f);
        var clipGO = new GameObject("MedicalClip", typeof(MeshFilter), typeof(MeshRenderer));
        clipGO.transform.SetParent(boardGO.transform, false);
        clipGO.transform.localPosition = new Vector3(0f, 0.16f, 0.012f);
        clipGO.GetComponent<MeshFilter>().sharedMesh = clipMesh;
        clipGO.GetComponent<MeshRenderer>().sharedMaterial = aluminumMat;

        // 3. Clinical Chart Paper Quad
        var chart = GameObject.CreatePrimitive(PrimitiveType.Quad);
        chart.name = "ClinicalChart";
        chart.transform.SetParent(boardGO.transform, false);
        chart.transform.localPosition = new Vector3(0f, -0.01f, 0.007f);
        chart.transform.localScale = new Vector3(0.22f, 0.32f, 1f);

        Texture2D chartTex = CreateMedicalChartTexture();
        Material chartMat = new Material(litShader) { mainTexture = chartTex };
        chart.GetComponent<MeshRenderer>().sharedMaterial = chartMat;
        CleanCollider(chart);

        // Attach InspectableClueMarker
        var clue = root.AddComponent<InspectableClueMarker>();
        clue.clueID = "ClinicMedCabinetCode";
        clue.clueTitle = "Medical Cabinet Code sa Chart";
        clue.clueDescription = "Nakatatak sa ibaba ng clinical chart: 'EMERGENCY DISPENSARY CABINET: CAB-MED: 9-0-4'. Para sa first aid kit at smelling salts sa clinic!";
        clue.revealDirection = Vector3.forward;
        clue.detectionAngle = 48.0f;

        SaveAsPrefab(root, $"{PrefabDir}/Model_MedicalSlip.prefab");
    }

    private static Texture2D CreateMedicalChartTexture()
    {
        int w = 256, h = 340;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color clinicalWhite = new Color(0.95f, 0.95f, 0.94f, 1f);
        Color medicalCrossRed = new Color(0.85f, 0.12f, 0.15f, 1f);
        Color chartInk = new Color(0.12f, 0.14f, 0.18f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = clinicalWhite;
                // Medical Cross icon at top
                if (y > h - 45 && y < h - 15 && x > 20 && x < 50)
                {
                    if (Mathf.Abs(x - 35) < 4 || Mathf.Abs(y - (h - 30)) < 4) c = medicalCrossRed;
                }
                // Grid lines
                if (y < h - 60 && y > 30 && (y % 15 == 0 || x % 25 == 0))
                {
                    c = new Color(0.82f, 0.88f, 0.84f, 1f);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        SaveTexture(tex, $"{TextureDir}/Tex_MedicalSlip.png");
        return tex;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void CleanCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    private static void SaveAsPrefab(GameObject go, string path)
    {
        if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

        string prefabName = Path.GetFileNameWithoutExtension(path);

        // 1. Serialize all in-memory procedural meshes as disk assets
        var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            var mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (string.IsNullOrEmpty(assetPath))
            {
                // In-memory procedural mesh
                string cleanNodeName = mf.gameObject.name;
                foreach (char c in Path.GetInvalidFileNameChars()) cleanNodeName = cleanNodeName.Replace(c, '_');
                cleanNodeName = cleanNodeName.Replace("/", "_").Replace("\\", "_").Replace(" ", "_").Replace("(", "").Replace(")", "");
                string meshAssetPath = $"{MeshDir}/{prefabName}_{cleanNodeName}_{i}.asset";

                Mesh diskMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
                if (diskMesh == null)
                {
                    diskMesh = Object.Instantiate(mf.sharedMesh);
                    diskMesh.name = mf.sharedMesh.name;
                    AssetDatabase.CreateAsset(diskMesh, meshAssetPath);
                }
                else
                {
                    diskMesh.Clear();
                    diskMesh.vertices = mf.sharedMesh.vertices;
                    diskMesh.normals = mf.sharedMesh.normals;
                    diskMesh.uv = mf.sharedMesh.uv;
                    diskMesh.triangles = mf.sharedMesh.triangles;
                    diskMesh.RecalculateBounds();
                    EditorUtility.SetDirty(diskMesh);
                }
                mf.sharedMesh = diskMesh;
            }
        }

        // 2. Serialize all in-memory procedural materials as disk assets
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int rIdx = 0; rIdx < renderers.Length; rIdx++)
        {
            var r = renderers[rIdx];
            if (r == null) continue;

            var mats = r.sharedMaterials;
            bool matsModified = false;
            for (int mIdx = 0; mIdx < mats.Length; mIdx++)
            {
                var mat = mats[mIdx];
                if (mat == null) continue;

                string matAssetPath = AssetDatabase.GetAssetPath(mat);
                if (string.IsNullOrEmpty(matAssetPath))
                {
                    // In-memory procedural material
                    string cleanNodeName = r.gameObject.name;
                    foreach (char c in Path.GetInvalidFileNameChars()) cleanNodeName = cleanNodeName.Replace(c, '_');
                    cleanNodeName = cleanNodeName.Replace("/", "_").Replace("\\", "_").Replace(" ", "_").Replace("(", "").Replace(")", "");

                    string cleanMatName = string.IsNullOrEmpty(mat.name) ? "Mat" : mat.name.Replace("(Instance)", "").Trim();
                    foreach (char c in Path.GetInvalidFileNameChars()) cleanMatName = cleanMatName.Replace(c, '_');
                    cleanMatName = cleanMatName.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");

                    string diskMatPath = $"{MaterialDir}/{prefabName}_{cleanNodeName}_{cleanMatName}_{mIdx}.mat";

                    Material diskMat = AssetDatabase.LoadAssetAtPath<Material>(diskMatPath);
                    if (diskMat == null)
                    {
                        diskMat = new Material(mat);
                        AssetDatabase.CreateAsset(diskMat, diskMatPath);
                    }
                    else
                    {
                        diskMat.CopyPropertiesFromMaterial(mat);
                        EditorUtility.SetDirty(diskMat);
                    }
                    mats[mIdx] = diskMat;
                    matsModified = true;
                }
            }
            if (matsModified)
            {
                r.sharedMaterials = mats;
            }
        }

        AssetDatabase.SaveAssets();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[Item3DModelFactory] Created high-detail prefab with serialized assets at: {path}");
    }

    private static Texture2D SaveTexture(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var diskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return diskTex != null ? diskTex : tex;
    }

    // ── Station Model Generators ──────────────────────────────────────────────

    private static void GenerateCircuitBreakerStationModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material darkMetalMat = new Material(litShader) { color = new Color(0.18f, 0.20f, 0.22f) };
        if (darkMetalMat.HasProperty("_Metallic")) darkMetalMat.SetFloat("_Metallic", 0.85f);
        if (darkMetalMat.HasProperty("_Smoothness")) darkMetalMat.SetFloat("_Smoothness", 0.40f);

        Material steelDoorMat = new Material(litShader) { color = new Color(0.28f, 0.32f, 0.35f) };
        if (steelDoorMat.HasProperty("_Metallic")) steelDoorMat.SetFloat("_Metallic", 0.90f);
        if (steelDoorMat.HasProperty("_Smoothness")) steelDoorMat.SetFloat("_Smoothness", 0.55f);

        Material brassMat = new Material(litShader) { color = new Color(0.88f, 0.72f, 0.28f) };
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.95f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.80f);

        Material switchBlackMat = new Material(litShader) { color = new Color(0.08f, 0.08f, 0.08f) };

        GameObject root = new GameObject("Model_CircuitBreakerStation");

        // 1. Outer Enclosure Box
        var boxMesh = CreateChamferedBoxMesh(w: 0.80f, h: 1.10f, d: 0.25f, bevel: 0.02f);
        var boxGO = new GameObject("EnclosureBox", typeof(MeshFilter), typeof(MeshRenderer));
        boxGO.transform.SetParent(root.transform, false);
        boxGO.GetComponent<MeshFilter>().sharedMesh = boxMesh;
        boxGO.GetComponent<MeshRenderer>().sharedMaterial = darkMetalMat;

        // 2. Front Door Frame & Panel
        var doorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorFrame.name = "DoorFrame";
        doorFrame.transform.SetParent(boxGO.transform, false);
        doorFrame.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        doorFrame.transform.localScale = new Vector3(0.76f, 1.05f, 0.025f);
        doorFrame.GetComponent<MeshRenderer>().sharedMaterial = steelDoorMat;
        CleanCollider(doorFrame);

        // 3. Recessed Switch Panel
        var switchPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        switchPanel.name = "SwitchInterior";
        switchPanel.transform.SetParent(boxGO.transform, false);
        switchPanel.transform.localPosition = new Vector3(0f, 0.05f, 0.13f);
        switchPanel.transform.localScale = new Vector3(0.65f, 0.65f, 0.02f);
        switchPanel.GetComponent<MeshRenderer>().sharedMaterial = darkMetalMat;
        CleanCollider(switchPanel);

        // 4. Circuit Breaker Switches
        string[] labels = new string[] { "HALLWAY", "COMLAB_FACULTY", "GUIDANCE", "VENTILATION" };
        for (int i = 0; i < 4; i++)
        {
            var sw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sw.name = $"Switch_{labels[i]}";
            sw.transform.SetParent(switchPanel.transform, false);
            float yPos = 0.22f - i * 0.14f;
            sw.transform.localPosition = new Vector3(-0.15f, yPos, 0.03f);
            sw.transform.localScale = new Vector3(0.12f, 0.05f, 0.04f);
            sw.transform.localRotation = Quaternion.Euler(i == 1 ? -25f : 25f, 0f, 0f);
            sw.GetComponent<MeshRenderer>().sharedMaterial = switchBlackMat;
            CleanCollider(sw);
        }

        // 5. Blown Fuse Cradle Socket for Slot 2 (ComLab)
        var fuseCradleL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuseCradleL.name = "FuseCradle_Left";
        fuseCradleL.transform.SetParent(switchPanel.transform, false);
        fuseCradleL.transform.localPosition = new Vector3(0.15f, 0.08f, 0.03f);
        fuseCradleL.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fuseCradleL.transform.localScale = new Vector3(0.035f, 0.015f, 0.035f);
        fuseCradleL.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        CleanCollider(fuseCradleL);

        var fuseCradleR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuseCradleR.name = "FuseCradle_Right";
        fuseCradleR.transform.SetParent(switchPanel.transform, false);
        fuseCradleR.transform.localPosition = new Vector3(0.25f, 0.08f, 0.03f);
        fuseCradleR.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fuseCradleR.transform.localScale = new Vector3(0.035f, 0.015f, 0.035f);
        fuseCradleR.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        CleanCollider(fuseCradleR);

        // 6. Hazard Stripes Caution Sign
        var sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sign.name = "CautionSign";
        sign.transform.SetParent(boxGO.transform, false);
        sign.transform.localPosition = new Vector3(0f, -0.38f, 0.135f);
        sign.transform.localScale = new Vector3(0.60f, 0.18f, 1f);

        Texture2D hazardTex = CreateHazardStripesTexture();
        Material signMat = new Material(litShader) { mainTexture = hazardTex };
        sign.GetComponent<MeshRenderer>().sharedMaterial = signMat;
        CleanCollider(sign);

        // 7. Voltmeter Gauge Dial
        var meter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        meter.name = "VoltmeterGauge";
        meter.transform.SetParent(boxGO.transform, false);
        meter.transform.localPosition = new Vector3(0.18f, 0.36f, 0.135f);
        meter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        meter.transform.localScale = new Vector3(0.12f, 0.015f, 0.12f);
        meter.GetComponent<MeshRenderer>().sharedMaterial = steelDoorMat;
        CleanCollider(meter);

        // 8. Slot 2 Status Light Indicator
        var lightGO = new GameObject("Slot2_StatusLED");
        lightGO.transform.SetParent(boxGO.transform, false);
        lightGO.transform.localPosition = new Vector3(0.20f, 0.08f, 0.14f);
        var led = lightGO.AddComponent<Light>();
        led.type = LightType.Point;
        led.color = Color.red;
        led.range = 0.8f;
        led.intensity = 0.5f;

        SaveAsPrefab(root, $"{PuzzlePrefabDir}/Model_CircuitBreakerStation.prefab");
    }

    private static Texture2D CreateHazardStripesTexture()
    {
        int w = 256, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color yellow = new Color(0.95f, 0.80f, 0.10f, 1f);
        Color black = new Color(0.12f, 0.12f, 0.12f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool stripe = ((x + y) / 16) % 2 == 0;
                tex.SetPixel(x, y, stripe ? yellow : black);
            }
        }
        tex.Apply();
        return SaveTexture(tex, $"{TextureDir}/Tex_HazardStripes.png");
    }

    private static void GenerateComLabTerminalStationModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material deskWoodMat = new Material(litShader) { color = new Color(0.35f, 0.22f, 0.14f) };
        if (deskWoodMat.HasProperty("_Smoothness")) deskWoodMat.SetFloat("_Smoothness", 0.40f);

        Material beigePlasticMat = new Material(litShader) { color = new Color(0.82f, 0.78f, 0.70f) };
        if (beigePlasticMat.HasProperty("_Smoothness")) beigePlasticMat.SetFloat("_Smoothness", 0.35f);

        Material darkBezelMat = new Material(litShader) { color = new Color(0.15f, 0.16f, 0.18f) };

        GameObject root = new GameObject("Model_ComLabTerminalStation");

        // 1. Desk Surface
        var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = "DeskSurface";
        desk.transform.SetParent(root.transform, false);
        desk.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        desk.transform.localScale = new Vector3(1.20f, 0.05f, 0.80f);
        desk.GetComponent<MeshRenderer>().sharedMaterial = deskWoodMat;
        CleanCollider(desk);

        // Desk Legs
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = $"Leg_{x}_{z}";
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(x * 0.54f, 0.36f, z * 0.34f);
                leg.transform.localScale = new Vector3(0.04f, 0.36f, 0.04f);
                leg.GetComponent<MeshRenderer>().sharedMaterial = darkBezelMat;
                CleanCollider(leg);
            }
        }

        // 2. Vintage Beige CRT Monitor
        var crtBoxMesh = CreateChamferedBoxMesh(w: 0.50f, h: 0.42f, d: 0.44f, bevel: 0.02f);
        var crtGO = new GameObject("CRTMonitor", typeof(MeshFilter), typeof(MeshRenderer));
        crtGO.transform.SetParent(root.transform, false);
        crtGO.transform.localPosition = new Vector3(0f, 0.98f, 0.05f);
        crtGO.GetComponent<MeshFilter>().sharedMesh = crtBoxMesh;
        crtGO.GetComponent<MeshRenderer>().sharedMaterial = beigePlasticMat;

        // Curved CRT Screen
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "CRTScreen";
        screen.transform.SetParent(crtGO.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.02f, -0.222f);
        screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        screen.transform.localScale = new Vector3(0.40f, 0.30f, 1f);

        Texture2D crtTex = CreateCRTScreenTexture();
        Material screenMat = new Material(litShader) { mainTexture = crtTex };
        if (screenMat.HasProperty("_EmissionColor"))
        {
            screenMat.EnableKeyword("_EMISSION");
            screenMat.SetColor("_EmissionColor", new Color(0.05f, 0.35f, 0.15f));
            screenMat.SetTexture("_EmissionMap", crtTex);
        }
        screen.GetComponent<MeshRenderer>().sharedMaterial = screenMat;
        CleanCollider(screen);

        // 3. Desktop Tower PC Unit
        var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tower.name = "DesktopTower";
        tower.transform.SetParent(root.transform, false);
        tower.transform.localPosition = new Vector3(0.42f, 0.94f, 0.05f);
        tower.transform.localScale = new Vector3(0.18f, 0.38f, 0.42f);
        tower.GetComponent<MeshRenderer>().sharedMaterial = beigePlasticMat;
        CleanCollider(tower);

        // 4. Keyboard
        var kb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        kb.name = "Keyboard";
        kb.transform.SetParent(root.transform, false);
        kb.transform.localPosition = new Vector3(0f, 0.755f, -0.22f);
        kb.transform.localScale = new Vector3(0.42f, 0.02f, 0.15f);
        kb.GetComponent<MeshRenderer>().sharedMaterial = beigePlasticMat;
        CleanCollider(kb);

        // 5. Terminal CRT Glow Light
        var termLight = new GameObject("TerminalGreenGlow");
        termLight.transform.SetParent(crtGO.transform, false);
        termLight.transform.localPosition = new Vector3(0f, 0f, -0.35f);
        var lt = termLight.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = new Color(0.2f, 0.95f, 0.4f);
        lt.intensity = 0.4f;
        lt.range = 1.2f;

        SaveAsPrefab(root, $"{PuzzlePrefabDir}/Model_ComLabTerminalStation.prefab");
    }

    private static Texture2D CreateCRTScreenTexture()
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color darkGreen = new Color(0.02f, 0.08f, 0.03f, 1f);
        Color phosphorGreen = new Color(0.25f, 0.92f, 0.35f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = darkGreen;
                if (y % 4 == 0) c *= 0.75f;
                if (y > 210 && y < 225 && x > 20 && x < 235) c = phosphorGreen;
                if (y > 175 && y < 185 && x > 20 && x < 180) c = phosphorGreen;
                if (y > 140 && y < 150 && x > 20 && x < 210) c = phosphorGreen;
                if (y > 80 && y < 95 && x > 20 && x < 60) c = phosphorGreen;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return SaveTexture(tex, $"{TextureDir}/Tex_CRTScreen.png");
    }

    private static void GenerateVendingMachineStationModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material redMat = new Material(litShader) { color = new Color(0.58f, 0.12f, 0.14f) };
        if (redMat.HasProperty("_Smoothness")) redMat.SetFloat("_Smoothness", 0.65f);

        Material blackTrimMat = new Material(litShader) { color = new Color(0.10f, 0.10f, 0.10f) };
        if (blackTrimMat.HasProperty("_Smoothness")) blackTrimMat.SetFloat("_Smoothness", 0.50f);

        Material glassMat = new Material(litShader) { color = new Color(0.70f, 0.85f, 0.95f, 0.35f) };
        if (glassMat.HasProperty("_Smoothness")) glassMat.SetFloat("_Smoothness", 0.95f);

        Material chromeMat = new Material(litShader) { color = new Color(0.90f, 0.90f, 0.92f) };
        if (chromeMat.HasProperty("_Metallic")) chromeMat.SetFloat("_Metallic", 0.95f);
        if (chromeMat.HasProperty("_Smoothness")) chromeMat.SetFloat("_Smoothness", 0.85f);

        GameObject root = new GameObject("Model_VendingMachineStation");

        // 1. Cabinet Body
        var bodyMesh = CreateChamferedBoxMesh(w: 1.10f, h: 2.10f, d: 0.90f, bevel: 0.03f);
        var bodyGO = new GameObject("CabinetBody", typeof(MeshFilter), typeof(MeshRenderer));
        bodyGO.transform.SetParent(root.transform, false);
        bodyGO.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        bodyGO.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
        bodyGO.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        // Base Footer
        var footer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        footer.name = "BaseFooter";
        footer.transform.SetParent(bodyGO.transform, false);
        footer.transform.localPosition = new Vector3(0f, -1.0f, 0f);
        footer.transform.localScale = new Vector3(1.12f, 0.10f, 0.92f);
        footer.GetComponent<MeshRenderer>().sharedMaterial = blackTrimMat;
        CleanCollider(footer);

        // 2. Marquee Sign Header
        var marquee = GameObject.CreatePrimitive(PrimitiveType.Quad);
        marquee.name = "MarqueeSign";
        marquee.transform.SetParent(bodyGO.transform, false);
        marquee.transform.localPosition = new Vector3(0f, 0.88f, 0.452f);
        marquee.transform.localScale = new Vector3(0.95f, 0.22f, 1f);

        Texture2D marqueeTex = CreateVendingMarqueeTexture();
        Material marqueeMat = new Material(litShader) { mainTexture = marqueeTex };
        if (marqueeMat.HasProperty("_EmissionColor"))
        {
            marqueeMat.EnableKeyword("_EMISSION");
            marqueeMat.SetColor("_EmissionColor", new Color(0.85f, 0.75f, 0.60f) * 0.5f);
            marqueeMat.SetTexture("_EmissionMap", marqueeTex);
        }
        marquee.GetComponent<MeshRenderer>().sharedMaterial = marqueeMat;
        CleanCollider(marquee);

        // 3. Front Glass Window
        var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.name = "GlassWindow";
        glass.transform.SetParent(bodyGO.transform, false);
        glass.transform.localPosition = new Vector3(-0.16f, 0.12f, 0.452f);
        glass.transform.localScale = new Vector3(0.68f, 1.15f, 0.02f);
        glass.GetComponent<MeshRenderer>().sharedMaterial = glassMat;
        CleanCollider(glass);

        // 4. Interior Wire Shelves
        for (int i = 0; i < 3; i++)
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = $"Shelf_{i}";
            shelf.transform.SetParent(bodyGO.transform, false);
            shelf.transform.localPosition = new Vector3(-0.16f, 0.45f - i * 0.35f, 0.25f);
            shelf.transform.localScale = new Vector3(0.64f, 0.02f, 0.38f);
            shelf.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
            CleanCollider(shelf);
        }

        // 5. Coin Slot & Keypad Interface
        var coinPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        coinPanel.name = "CoinInterfacePanel";
        coinPanel.transform.SetParent(bodyGO.transform, false);
        coinPanel.transform.localPosition = new Vector3(0.35f, 0.15f, 0.455f);
        coinPanel.transform.localScale = new Vector3(0.24f, 0.85f, 0.02f);
        coinPanel.GetComponent<MeshRenderer>().sharedMaterial = blackTrimMat;
        CleanCollider(coinPanel);

        // Coin Slot Insert
        var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slot.name = "CoinSlot";
        slot.transform.SetParent(coinPanel.transform, false);
        slot.transform.localPosition = new Vector3(0f, 0.25f, 0.02f);
        slot.transform.localScale = new Vector3(0.08f, 0.02f, 0.02f);
        slot.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
        CleanCollider(slot);

        // 6. Push Dispenser Flap Door
        var flap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flap.name = "DispenseFlap";
        flap.transform.SetParent(bodyGO.transform, false);
        flap.transform.localPosition = new Vector3(0f, -0.68f, 0.455f);
        flap.transform.localScale = new Vector3(0.72f, 0.28f, 0.03f);
        flap.GetComponent<MeshRenderer>().sharedMaterial = blackTrimMat;
        CleanCollider(flap);

        SaveAsPrefab(root, $"{PuzzlePrefabDir}/Model_VendingMachineStation.prefab");
    }

    private static Texture2D CreateVendingMarqueeTexture()
    {
        int w = 256, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bg = new Color(0.12f, 0.04f, 0.05f, 1f);
        Color textYellow = new Color(0.98f, 0.85f, 0.20f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                if (y > 22 && y < 42 && x > 15 && x < 240) c = textYellow;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return SaveTexture(tex, $"{TextureDir}/Tex_VendingMarquee.png");
    }

    private static void GenerateGuidanceSafeStationModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material safeSteelMat = new Material(litShader) { color = new Color(0.16f, 0.18f, 0.20f) };
        if (safeSteelMat.HasProperty("_Metallic")) safeSteelMat.SetFloat("_Metallic", 0.90f);
        if (safeSteelMat.HasProperty("_Smoothness")) safeSteelMat.SetFloat("_Smoothness", 0.50f);

        Material chromeMat = new Material(litShader) { color = new Color(0.92f, 0.92f, 0.94f) };
        if (chromeMat.HasProperty("_Metallic")) chromeMat.SetFloat("_Metallic", 0.98f);
        if (chromeMat.HasProperty("_Smoothness")) chromeMat.SetFloat("_Smoothness", 0.88f);

        Material frameWoodMat = new Material(litShader) { color = new Color(0.38f, 0.18f, 0.08f) };
        if (frameWoodMat.HasProperty("_Smoothness")) frameWoodMat.SetFloat("_Smoothness", 0.45f);

        GameObject root = new GameObject("Model_GuidanceSafeStation");

        // 1. Recessed Safe Outer Box
        var boxMesh = CreateChamferedBoxMesh(w: 0.60f, h: 0.60f, d: 0.30f, bevel: 0.015f);
        var boxGO = new GameObject("SafeVaultBox", typeof(MeshFilter), typeof(MeshRenderer));
        boxGO.transform.SetParent(root.transform, false);
        boxGO.GetComponent<MeshFilter>().sharedMesh = boxMesh;
        boxGO.GetComponent<MeshRenderer>().sharedMaterial = safeSteelMat;

        // 2. Heavy Vault Door
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "VaultDoor";
        door.transform.SetParent(boxGO.transform, false);
        door.transform.localPosition = new Vector3(0f, 0f, 0.14f);
        door.transform.localScale = new Vector3(0.54f, 0.54f, 0.04f);
        door.GetComponent<MeshRenderer>().sharedMaterial = safeSteelMat;
        CleanCollider(door);

        // 3. 3 Rotary Tumblers (4 - 1 - 9)
        for (int i = 0; i < 3; i++)
        {
            var tumblerMesh = CreateReededCylinderMesh(radius: 0.045f, height: 0.035f, numReeds: 36, reedDepth: 0.003f);
            var dial = new GameObject($"RotaryTumbler_{i}", typeof(MeshFilter), typeof(MeshRenderer));
            dial.transform.SetParent(door.transform, false);
            float posX = -0.14f + i * 0.14f;
            dial.transform.localPosition = new Vector3(posX, 0.05f, 0.03f);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dial.GetComponent<MeshFilter>().sharedMesh = tumblerMesh;
            dial.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
        }

        // 4. Heavy Bolt T-Handle
        var handleBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handleBase.name = "THandle_Hub";
        handleBase.transform.SetParent(door.transform, false);
        handleBase.transform.localPosition = new Vector3(0f, -0.12f, 0.035f);
        handleBase.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        handleBase.transform.localScale = new Vector3(0.04f, 0.02f, 0.04f);
        handleBase.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
        CleanCollider(handleBase);

        var handleBar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handleBar.name = "THandle_Crossbar";
        handleBar.transform.SetParent(handleBase.transform, false);
        handleBar.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        handleBar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        handleBar.transform.localScale = new Vector3(0.015f, 0.08f, 0.015f);
        handleBar.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
        CleanCollider(handleBar);

        // 5. Concealing Certificate Picture Frame (Swung a bit to the side)
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "SwingingCertificateFrame";
        frame.transform.SetParent(boxGO.transform, false);
        frame.transform.localPosition = new Vector3(-0.28f, 0.05f, 0.22f);
        frame.transform.localRotation = Quaternion.Euler(0f, -48f, 0f);
        frame.transform.localScale = new Vector3(0.55f, 0.65f, 0.03f);
        frame.GetComponent<MeshRenderer>().sharedMaterial = frameWoodMat;
        CleanCollider(frame);

        SaveAsPrefab(root, $"{PuzzlePrefabDir}/Model_GuidanceSafeStation.prefab");
    }

    private static void GenerateLobbyKeypadStationModel()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material brushedSteelMat = new Material(litShader) { color = new Color(0.45f, 0.48f, 0.52f) };
        if (brushedSteelMat.HasProperty("_Metallic")) brushedSteelMat.SetFloat("_Metallic", 0.92f);
        if (brushedSteelMat.HasProperty("_Smoothness")) brushedSteelMat.SetFloat("_Smoothness", 0.60f);

        Material rubberButtonMat = new Material(litShader) { color = new Color(0.12f, 0.12f, 0.12f) };
        if (rubberButtonMat.HasProperty("_Smoothness")) rubberButtonMat.SetFloat("_Smoothness", 0.30f);

        Material lcdScreenMat = new Material(litShader) { color = new Color(0.06f, 0.18f, 0.08f) };
        if (lcdScreenMat.HasProperty("_EmissionColor"))
        {
            lcdScreenMat.EnableKeyword("_EMISSION");
            lcdScreenMat.SetColor("_EmissionColor", new Color(0.10f, 0.45f, 0.15f));
        }

        GameObject root = new GameObject("Model_LobbyKeypadStation");

        // 1. Angled Enclosure Box
        var caseMesh = CreateChamferedBoxMesh(w: 0.35f, h: 0.55f, d: 0.16f, bevel: 0.015f);
        var caseGO = new GameObject("KeypadEnclosure", typeof(MeshFilter), typeof(MeshRenderer));
        caseGO.transform.SetParent(root.transform, false);
        caseGO.GetComponent<MeshFilter>().sharedMesh = caseMesh;
        caseGO.GetComponent<MeshRenderer>().sharedMaterial = brushedSteelMat;

        // 2. LCD Display Screen
        var lcd = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lcd.name = "LCDScreen";
        lcd.transform.SetParent(caseGO.transform, false);
        lcd.transform.localPosition = new Vector3(0f, 0.18f, 0.082f);
        lcd.transform.localScale = new Vector3(0.24f, 0.08f, 1f);
        lcd.GetComponent<MeshRenderer>().sharedMaterial = lcdScreenMat;
        CleanCollider(lcd);

        // 3. 12 Numeric Rubber Keys (3 cols x 4 rows)
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = $"Key_{row}_{col}";
                btn.transform.SetParent(caseGO.transform, false);
                float posX = (col - 1) * 0.075f;
                float posY = 0.06f - row * 0.065f;
                btn.transform.localPosition = new Vector3(posX, posY, 0.085f);
                btn.transform.localScale = new Vector3(0.055f, 0.045f, 0.015f);
                btn.GetComponent<MeshRenderer>().sharedMaterial = rubberButtonMat;
                CleanCollider(btn);
            }
        }

        // 4. Status Indicator LED Light
        var ledGO = new GameObject("KeypadStatusLED");
        ledGO.transform.SetParent(caseGO.transform, false);
        ledGO.transform.localPosition = new Vector3(0.12f, 0.22f, 0.09f);
        var lt = ledGO.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = Color.red;
        lt.intensity = 0.6f;
        lt.range = 0.6f;

        SaveAsPrefab(root, $"{PuzzlePrefabDir}/Model_LobbyKeypadStation.prefab");
    }

    public static void LinkModelsToInventoryItemAssets()
    {
        var itemMap = new Dictionary<string, string>()
        {
            { "LockerKey.asset", $"{PrefabDir}/Model_LockerKey.prefab" },
            { "Executive_Seal_Stamp.asset", $"{PrefabDir}/Model_ExecutiveSealStamp.prefab" },
            { "ComLab_Clearance_Form.asset", $"{PrefabDir}/Model_ComLabClearanceForm.prefab" },
            { "Library_Voucher.asset", $"{PrefabDir}/Model_LibraryVoucher.prefab" },
            { "Blank_Paper.asset", $"{PrefabDir}/Model_BlankPaper.prefab" },
            { "Fragment_Top.asset", $"{PrefabDir}/Model_Fragment_Top.prefab" },
            { "Fragment_Left.asset", $"{PrefabDir}/Model_Fragment_Left.prefab" },
            { "Fragment_Right.asset", $"{PrefabDir}/Model_Fragment_Right.prefab" },
            { "Fragment_Bottom.asset", $"{PrefabDir}/Model_Fragment_Bottom.prefab" },
            { "HamAndCheeseSandwich.asset", $"{PrefabDir}/Model_HamAndCheeseSandwich.prefab" },
            { "Flashlight.asset", $"{PrefabDir}/Model_Flashlight.prefab" },
            { "Printer_Paper.asset", $"{PrefabDir}/Model_PrinterPaper.prefab" },
            { "Student_Handbook.asset", $"{PrefabDir}/Model_StudentHandbook.prefab" },
            { "Lockpin.asset", $"{PrefabDir}/Model_Lockpin.prefab" },
            { "VGA_Cable.asset", $"{PrefabDir}/Model_VGACable.prefab" },
            { "Ceramic_Fuse.asset", $"{PrefabDir}/Model_CeramicFuse.prefab" },
            { "Cassette_Tape.asset", $"{PrefabDir}/Model_CassetteTape.prefab" },
            { "Peso_Coin.asset", $"{PrefabDir}/Model_Coin.prefab" },
            { "Guard_Logbook.asset", $"{PrefabDir}/Model_GuardLogbook.prefab" },
            { "Medical_Record_Slip.asset", $"{PrefabDir}/Model_MedicalSlip.prefab" }
        };

        foreach (var pair in itemMap)
        {
            string itemPath = $"Assets/Items/{pair.Key}";
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(itemPath);
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);

            if (item != null && modelPrefab != null)
            {
                item.itemPrefab = modelPrefab;
                item.isInspectable = true;
                EditorUtility.SetDirty(item);
                Debug.Log($"[Item3DModelFactory] Linked {pair.Key} -> {pair.Value}");
            }
        }
        AssetDatabase.SaveAssets();
    }
#endif
}
