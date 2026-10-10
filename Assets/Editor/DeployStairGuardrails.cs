using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DeployStairGuardrails — Generates sleek modern architectural open-air horizontal-bar
/// stair guardrails with light oak wood top handrails, matching the user reference photo.
///
/// Features:
/// 1. Matte black square steel posts with beveled base mounting flanges.
/// 2. Natural light oak rectangular top cap handrail (OfficeWood_LightOak).
/// 3. Slim matte black steel sub-rail directly beneath wood cap (OfficeMetal_Charcoal).
/// 4. Five (5) horizontal black steel infill bars running parallel to the flight slope (OPEN-AIR).
/// 5. Matching horizontal landing guardrails along floor openings.
/// 6. Sleek wall-mounted handrail with standoff brackets along outer walls.
/// 7. Invisible anti-jump box colliders physically preventing jumping between flights.
/// 8. ZERO solid walls or spandrel blocks — 100% open, transparent, and aesthetically accurate.
/// </summary>
public static class DeployStairGuardrails
{
    private struct FlightConfig
    {
        public string flightName;
        public float yBot;
        public float yTp;
        public float yTop;
    }

    private struct StairwellConfig
    {
        public string id;
        public float zDiv;
        public float zWallLower;
        public float zWallUpper;
        public float xMin;
        public float xMax;
        public float xLanding;
        public FlightConfig[] flights;
    }

    [MenuItem("Tools/Klirans/Deploy Stair Guardrails")]
    public static void RunDeploy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        Debug.Log("=== DEPLOYING MODERN HORIZONTAL-BAR STAIR GUARDRAILS (MATCHING REFERENCE PHOTO) ===");

        // Materials
        Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OfficeMetal_Charcoal.mat");
        Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OfficeWood_LightOak.mat");

        if (metalMat == null)
        {
            metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Entrance/Entrance_DarkTrim.mat");
        }
        if (woodMat == null)
        {
            woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OfficeWood_Mahogany.mat") ?? metalMat;
        }

        StairwellConfig[] stairwells = new StairwellConfig[]
        {
            new StairwellConfig
            {
                id = "MainStairs",
                zDiv = 16.64f,
                zWallLower = 8.85f,
                zWallUpper = 24.45f,
                xMin = -89.90f,
                xMax = -85.90f,
                xLanding = -85.00f,
                flights = new FlightConfig[]
                {
                    new FlightConfig { flightName = "Flight_1stFloor", yBot = 2.35f, yTp = 5.30f, yTop = 8.35f },
                    new FlightConfig { flightName = "Flight_2ndFloor", yBot = 8.35f, yTp = 11.25f, yTop = 14.35f },
                    new FlightConfig { flightName = "Flight_3rdFloor", yBot = 14.35f, yTp = 17.25f, yTop = 20.35f }
                }
            },
            new StairwellConfig
            {
                id = "RightStairs",
                zDiv = 49.305f,
                zWallLower = 47.50f,
                zWallUpper = 51.10f,
                xMin = -89.92f,
                xMax = -85.92f,
                xLanding = -85.00f,
                flights = new FlightConfig[]
                {
                    new FlightConfig { flightName = "Flight_1stFloor", yBot = 2.31f, yTp = 5.30f, yTop = 8.35f },
                    new FlightConfig { flightName = "Flight_2ndFloor", yBot = 8.35f, yTp = 11.25f, yTop = 14.34f },
                    new FlightConfig { flightName = "Flight_3rdFloor", yBot = 14.34f, yTp = 17.24f, yTop = 20.30f }
                }
            },
            new StairwellConfig
            {
                id = "LeftStairs",
                zDiv = -16.345f,
                zWallLower = -18.15f,
                zWallUpper = -14.55f,
                xMin = -89.92f,
                xMax = -85.92f,
                xLanding = -85.00f,
                flights = new FlightConfig[]
                {
                    new FlightConfig { flightName = "Flight_1stFloor", yBot = 2.36f, yTp = 5.35f, yTop = 8.40f },
                    new FlightConfig { flightName = "Flight_2ndFloor", yBot = 8.40f, yTp = 11.30f, yTop = 14.39f },
                    new FlightConfig { flightName = "Flight_3rdFloor", yBot = 14.39f, yTp = 17.29f, yTop = 20.35f }
                }
            }
        };

        // Remove old container
        GameObject oldRoot = GameObject.Find("StairGuardrails");
        if (oldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }

        // Create new root container with (1, 1, 1) scale
        GameObject rootContainer = new GameObject("StairGuardrails");
        rootContainer.transform.position = Vector3.zero;
        rootContainer.transform.rotation = Quaternion.identity;
        rootContainer.transform.localScale = Vector3.one;

        foreach (var sw in stairwells)
        {
            GameObject swGO = new GameObject(sw.id);
            swGO.transform.SetParent(rootContainer.transform, false);

            for (int f = 0; f < sw.flights.Length; f++)
            {
                var fc = sw.flights[f];
                GameObject flightGO = new GameObject(fc.flightName);
                flightGO.transform.SetParent(swGO.transform, false);

                BuildModernHorizontalBarFlight(flightGO.transform, sw, fc, metalMat, woodMat);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[DeployStairGuardrails] Successfully generated modern open horizontal-bar guardrails across all 9 stair flights matching reference photo!");
    }

    private static void BuildModernHorizontalBarFlight(
        Transform root,
        StairwellConfig sw,
        FlightConfig fc,
        Material metalMat,
        Material woodMat)
    {
        float zDiv = sw.zDiv;
        float xMin = sw.xMin;
        float xMax = sw.xMax;
        float xLanding = sw.xLanding;

        // ══════════════════════════════════════════════════════════════════════
        // 1. LOWER FLIGHT RAILING (along 1stStair: rises from xMax to xMin)
        // ══════════════════════════════════════════════════════════════════════
        GameObject lowerRailingGO = new GameObject("LowerFlight_Guardrail");
        lowerRailingGO.transform.SetParent(root, false);

        Vector3 p0_low = new Vector3(xMax, fc.yBot, zDiv);
        Vector3 p1_low = new Vector3((xMin + xMax) * 0.5f, (fc.yBot + fc.yTp) * 0.5f, zDiv);
        Vector3 p2_low = new Vector3(xMin, fc.yTp, zDiv);

        BuildSlopedRailAssembly(lowerRailingGO.transform, p0_low, p1_low, p2_low, metalMat, woodMat);

        // Wall Handrail for lower flight (along outer wall)
        if (Mathf.Abs(sw.zWallLower) > 0.1f)
        {
            Vector3 w0_low = new Vector3(xMax, fc.yBot + 0.88f, sw.zWallLower);
            Vector3 w1_low = new Vector3((xMin + xMax) * 0.5f, (fc.yBot + fc.yTp) * 0.5f + 0.88f, sw.zWallLower);
            Vector3 w2_low = new Vector3(xMin, fc.yTp + 0.88f, sw.zWallLower);
            BuildWallHandrail(lowerRailingGO.transform, w0_low, w1_low, w2_low, metalMat);
        }

        // Anti-jump invisible collider for lower flight
        CreateAntiJumpCollider(lowerRailingGO.transform, "JumpBlocker_Lower", p0_low, p2_low);

        // ══════════════════════════════════════════════════════════════════════
        // 2. UPPER FLIGHT RAILING (along 2ndStair: rises from xMin to xMax)
        // ══════════════════════════════════════════════════════════════════════
        GameObject upperRailingGO = new GameObject("UpperFlight_Guardrail");
        upperRailingGO.transform.SetParent(root, false);

        Vector3 p0_up = new Vector3(xMin, fc.yTp, zDiv);
        Vector3 p1_up = new Vector3((xMin + xMax) * 0.5f, (fc.yTp + fc.yTop) * 0.5f, zDiv);
        Vector3 p2_up = new Vector3(xMax, fc.yTop, zDiv);

        BuildSlopedRailAssembly(upperRailingGO.transform, p0_up, p1_up, p2_up, metalMat, woodMat);

        // Wall Handrail for upper flight (along outer wall)
        if (Mathf.Abs(sw.zWallUpper) > 0.1f)
        {
            Vector3 w0_up = new Vector3(xMin, fc.yTp + 0.88f, sw.zWallUpper);
            Vector3 w1_up = new Vector3((xMin + xMax) * 0.5f, (fc.yTp + fc.yTop) * 0.5f + 0.88f, sw.zWallUpper);
            Vector3 w2_up = new Vector3(xMax, fc.yTop + 0.88f, sw.zWallUpper);
            BuildWallHandrail(upperRailingGO.transform, w0_up, w1_up, w2_up, metalMat);
        }

        // Anti-jump invisible collider for upper flight
        CreateAntiJumpCollider(upperRailingGO.transform, "JumpBlocker_Upper", p0_up, p2_up);

        // ══════════════════════════════════════════════════════════════════════
        // 3. TOP LANDING GUARDRAIL (horizontal extension along floor opening)
        // ══════════════════════════════════════════════════════════════════════
        GameObject landingRailingGO = new GameObject("Landing_Guardrail");
        landingRailingGO.transform.SetParent(root, false);

        Vector3 pLandStart = new Vector3(xMax, fc.yTop, zDiv);
        Vector3 pLandEnd = new Vector3(xLanding, fc.yTop, zDiv);

        BuildHorizontalLandingAssembly(landingRailingGO.transform, pLandStart, pLandEnd, metalMat, woodMat);
        CreateAntiJumpCollider(landingRailingGO.transform, "JumpBlocker_Landing", pLandStart, pLandEnd);
    }

    /// <summary>
    /// Builds the modern open-air sloped railing assembly matching Picture 2:
    /// - 3 square black posts with base mounting flanges
    /// - 1 light natural oak rectangular top cap handrail
    /// - 1 slim black metal top sub-rail
    /// - 5 horizontal black steel infill bars running parallel to the flight slope
    /// </summary>
    private static void BuildSlopedRailAssembly(
        Transform parent,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Material metalMat,
        Material woodMat)
    {
        float postHeight = 0.94f;
        float postThick = 0.050f;
        float flangeSize = 0.090f;
        float flangeHeight = 0.015f;

        // 1. Posts with base flanges
        CreatePostWithFlange(parent, "Post_Start", p0, postHeight, postThick, flangeSize, flangeHeight, metalMat);
        CreatePostWithFlange(parent, "Post_Mid",   p1, postHeight, postThick, flangeSize, flangeHeight, metalMat);
        CreatePostWithFlange(parent, "Post_End",   p2, postHeight, postThick, flangeSize, flangeHeight, metalMat);

        // 2. Slim Metal Sub-Rail (underneath the wood cap)
        Vector3 subStart = p0 + Vector3.up * (postHeight - 0.010f);
        Vector3 subEnd   = p2 + Vector3.up * (postHeight - 0.010f);
        CreateBeam(parent, "MetalSubRail", subStart, subEnd, 0.042f, 0.016f, metalMat);

        // 3. Natural Light Oak Wood Top Handrail (rectangular cap)
        Vector3 capStart = p0 + Vector3.up * (postHeight + 0.016f);
        Vector3 capEnd   = p2 + Vector3.up * (postHeight + 0.016f);
        CreateBeam(parent, "WoodTopCap", capStart, capEnd, 0.065f, 0.035f, woodMat);

        // 4. Five (5) Horizontal Black Steel Infill Bars (evenly spaced open-air design)
        int numBars = 5;
        float startElevation = 0.16f;
        float maxElevation = postHeight - 0.12f;
        float stepElevation = (maxElevation - startElevation) / (numBars - 1);

        for (int i = 0; i < numBars; i++)
        {
            float elev = startElevation + i * stepElevation;
            Vector3 barStart = p0 + Vector3.up * elev;
            Vector3 barEnd   = p2 + Vector3.up * elev;
            CreateBeam(parent, $"HorizontalBar_{i + 1}", barStart, barEnd, 0.018f, 0.018f, metalMat);
        }
    }

    /// <summary>
    /// Builds the matching horizontal floor landing railing assembly.
    /// </summary>
    private static void BuildHorizontalLandingAssembly(
        Transform parent,
        Vector3 start,
        Vector3 end,
        Material metalMat,
        Material woodMat)
    {
        float postHeight = 0.94f;
        float postThick = 0.050f;
        float flangeSize = 0.090f;
        float flangeHeight = 0.015f;

        CreatePostWithFlange(parent, "LandingPost_Start", start, postHeight, postThick, flangeSize, flangeHeight, metalMat);
        CreatePostWithFlange(parent, "LandingPost_End",   end,   postHeight, postThick, flangeSize, flangeHeight, metalMat);

        // Sub-rail & Wood Cap
        Vector3 subStart = start + Vector3.up * (postHeight - 0.010f);
        Vector3 subEnd   = end   + Vector3.up * (postHeight - 0.010f);
        CreateBeam(parent, "LandingSubRail", subStart, subEnd, 0.042f, 0.016f, metalMat);

        Vector3 capStart = start + Vector3.up * (postHeight + 0.016f);
        Vector3 capEnd   = end   + Vector3.up * (postHeight + 0.016f);
        CreateBeam(parent, "LandingWoodCap", capStart, capEnd, 0.065f, 0.035f, woodMat);

        // 5 Horizontal Infill Bars
        int numBars = 5;
        float startElevation = 0.16f;
        float maxElevation = postHeight - 0.12f;
        float stepElevation = (maxElevation - startElevation) / (numBars - 1);

        for (int i = 0; i < numBars; i++)
        {
            float elev = startElevation + i * stepElevation;
            Vector3 barStart = start + Vector3.up * elev;
            Vector3 barEnd   = end   + Vector3.up * elev;
            CreateBeam(parent, $"LandingBar_{i + 1}", barStart, barEnd, 0.018f, 0.018f, metalMat);
        }
    }

    /// <summary>
    /// Builds sleek wall-mounted handrail with standoff brackets (as seen in Picture 2).
    /// </summary>
    private static void BuildWallHandrail(Transform parent, Vector3 w0, Vector3 w1, Vector3 w2, Material metalMat)
    {
        GameObject wallRail = new GameObject("WallHandrail");
        wallRail.transform.SetParent(parent, false);

        // Continuous cylindrical handrail tube
        CreateBeam(wallRail.transform, "WallRail_Tube", w0, w2, 0.040f, 0.040f, metalMat);

        // Standoff wall brackets
        Vector3[] bracketPoints = new Vector3[] { w0, w1, w2 };
        for (int b = 0; b < bracketPoints.Length; b++)
        {
            var bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = $"WallBracket_{b}";
            bracket.transform.SetParent(wallRail.transform, false);
            bracket.transform.position = bracketPoints[b] - Vector3.up * 0.025f;
            bracket.transform.localScale = new Vector3(0.040f, 0.060f, 0.060f);

            var col = bracket.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            var mr = bracket.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = metalMat;
        }
    }

    /// <summary>
    /// Creates a square metal post with a base mounting flange plate.
    /// </summary>
    private static void CreatePostWithFlange(
        Transform parent,
        string name,
        Vector3 basePos,
        float height,
        float thickness,
        float flangeSize,
        float flangeHeight,
        Material mat)
    {
        GameObject postGO = new GameObject(name);
        postGO.transform.SetParent(parent, false);

        // Base Flange Plate
        var flange = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flange.name = "BaseFlange";
        flange.transform.SetParent(postGO.transform, false);
        flange.transform.position = basePos + Vector3.up * (flangeHeight * 0.5f);
        flange.transform.rotation = Quaternion.identity;
        flange.transform.localScale = new Vector3(flangeSize, flangeHeight, flangeSize);

        var fc = flange.GetComponent<Collider>();
        if (fc != null) UnityEngine.Object.DestroyImmediate(fc);
        var fmr = flange.GetComponent<MeshRenderer>();
        if (fmr != null) fmr.sharedMaterial = mat;

        // Post Column Stem
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stem.name = "Stem";
        stem.transform.SetParent(postGO.transform, false);
        stem.transform.position = basePos + Vector3.up * (height * 0.5f);
        stem.transform.rotation = Quaternion.identity;
        stem.transform.localScale = new Vector3(thickness, height, thickness);

        var sc = stem.GetComponent<Collider>();
        if (sc != null) UnityEngine.Object.DestroyImmediate(sc);
        var smr = stem.GetComponent<MeshRenderer>();
        if (smr != null) smr.sharedMaterial = mat;
    }

    /// <summary>
    /// Creates an aligned architectural beam/bar running precisely from startPos to endPos.
    /// Utilizes LookRotation with (width, thickness, length) to achieve 100% mathematical precision.
    /// </summary>
    private static void CreateBeam(
        Transform parent,
        string name,
        Vector3 startPos,
        Vector3 endPos,
        float width,
        float thickness,
        Material mat)
    {
        Vector3 dir = endPos - startPos;
        float len = dir.magnitude;
        if (len < 0.001f) return;

        Vector3 mid = (startPos + endPos) * 0.5f;

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.position = mid;
        beam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        beam.transform.localScale = new Vector3(width, thickness, len);

        var col = beam.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);

        var mr = beam.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    /// <summary>
    /// Creates an invisible physical anti-jump collider covering the railing plane.
    /// Visual rendering remains 100% open-air, while physics prohibits vaulting between flights.
    /// </summary>
    private static void CreateAntiJumpCollider(
        Transform parent,
        string name,
        Vector3 startPos,
        Vector3 endPos)
    {
        Vector3 dir = endPos - startPos;
        float len = dir.magnitude;
        if (len < 0.001f) return;

        float barrierHeight = 1.35f;
        Vector3 mid = (startPos + endPos) * 0.5f + Vector3.up * (barrierHeight * 0.5f);

        GameObject blocker = new GameObject(name);
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = mid;
        blocker.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        var bc = blocker.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.08f, barrierHeight, len);
        bc.isTrigger = false;
    }
}
