using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DeployLibrarianPuzzle
{
    private const string SoundDir = "Assets/Sounds";
    private const string IconDir = "Assets/Items/Icons";
    private const string ItemDir = "Assets/Items";

    [MenuItem("Tools/Klirans/Deploy Librarian Clearance Puzzle")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== LIBRARIAN CLEARANCE PUZZLE DEPLOYMENT ===");

        // ── 1. Create Audio Files ──────────────────────────────────────────────────
        string printerAudioPath = $"{SoundDir}/printer_operation.wav";
        string horrorCuePath = $"{SoundDir}/printer_creepy_cue.wav";
        string objChimePath = $"{SoundDir}/paper_scribble.wav";

        if (!Directory.Exists(SoundDir)) Directory.CreateDirectory(SoundDir);

        if (!File.Exists(printerAudioPath))
        {
            byte[] wavBytes = GeneratePrinterAudio(3.8f);
            File.WriteAllBytes(printerAudioPath, wavBytes);
            log.AppendLine($"Created {printerAudioPath}");
        }

        if (!File.Exists(horrorCuePath))
        {
            byte[] wavBytes = GenerateHorrorCue(2.5f);
            File.WriteAllBytes(horrorCuePath, wavBytes);
            log.AppendLine($"Created {horrorCuePath}");
        }

        if (!File.Exists(objChimePath))
        {
            byte[] wavBytes = GeneratePaperScribble(0.85f);
            File.WriteAllBytes(objChimePath, wavBytes);
            log.AppendLine($"Created {objChimePath}");
        }

        AssetDatabase.Refresh();

        AudioClip printerClip = AssetDatabase.LoadAssetAtPath<AudioClip>(printerAudioPath);
        AudioClip horrorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(horrorCuePath);
        AudioClip objChimeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(objChimePath);

        // ── 2. Create Icons ────────────────────────────────────────────────────────
        if (!Directory.Exists(IconDir)) Directory.CreateDirectory(IconDir);

        string paperIconPath = $"{IconDir}/PrinterPaper_Icon.png";
        string voucherIconPath = $"{IconDir}/LibraryVoucher_Icon.png";

        if (!File.Exists(paperIconPath))
        {
            Texture2D tex = BuildPaperReamTexture(96, 96);
            File.WriteAllBytes(paperIconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {paperIconPath}");
        }

        if (!File.Exists(voucherIconPath))
        {
            Texture2D tex = BuildVoucherTexture(96, 96);
            File.WriteAllBytes(voucherIconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {voucherIconPath}");
        }

        AssetDatabase.Refresh();

        ConfigureSpriteImporter(paperIconPath);
        ConfigureSpriteImporter(voucherIconPath);

        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(paperIconPath);
        Sprite voucherSprite = AssetDatabase.LoadAssetAtPath<Sprite>(voucherIconPath);

        // ── 3. Create ScriptableObjects ────────────────────────────────────────────
        string paperItemPath = $"{ItemDir}/Printer_Paper.asset";
        InventoryItem paperItem = AssetDatabase.LoadAssetAtPath<InventoryItem>(paperItemPath);
        if (paperItem == null)
        {
            paperItem = ScriptableObject.CreateInstance<InventoryItem>();
            paperItem.itemName = "Ream of Bond Paper";
            paperItem.description = "A heavy ream of blank 80gsm university bond paper. Needed to load the library printer tray.";
            paperItem.itemType = InventoryItem.ItemType.KeyItem;
            paperItem.icon = paperSprite;
            paperItem.isStackable = false;
            paperItem.canBeDropped = false;
            AssetDatabase.CreateAsset(paperItem, paperItemPath);
            log.AppendLine($"Created {paperItemPath}");
        }
        else
        {
            paperItem.icon = paperSprite;
            EditorUtility.SetDirty(paperItem);
        }

        string voucherItemPath = $"{ItemDir}/Library_Voucher.asset";
        InventoryItem voucherItem = AssetDatabase.LoadAssetAtPath<InventoryItem>(voucherItemPath);
        if (voucherItem == null)
        {
            voucherItem = ScriptableObject.CreateInstance<InventoryItem>();
            voucherItem.itemName = "Library Clearance Voucher";
            voucherItem.description = "Official university printout certifying that all past borrowing penalties and overdue records have been settled.";
            voucherItem.itemType = InventoryItem.ItemType.Document;
            voucherItem.icon = voucherSprite;
            voucherItem.isStackable = false;
            voucherItem.canBeDropped = false;
            AssetDatabase.CreateAsset(voucherItem, voucherItemPath);
            log.AppendLine($"Created {voucherItemPath}");
        }
        else
        {
            voucherItem.icon = voucherSprite;
            EditorUtility.SetDirty(voucherItem);
        }

        AssetDatabase.SaveAssets();

        // ── 4. Setup Room 308 Printer Workstation ─────────────────────────────────
        GameObject room308 = GameObject.Find("Rooms/3rdFloor/Room 308");
        if (room308 == null)
        {
            Debug.LogError("Room 308 not found!");
            return;
        }

        Transform furnitureParent = room308.transform.Find("LibraryFurniture");
        if (furnitureParent == null) furnitureParent = room308.transform;

        // Check or create PrinterStation root
        GameObject stationGO = GameObject.Find("PrinterStation_Room308");
        if (stationGO == null)
        {
            stationGO = new GameObject("PrinterStation_Room308");
            stationGO.transform.SetParent(furnitureParent, false);
        }
        stationGO.transform.position = new Vector3(-81.5f, 14.30f, 43.5f);
        stationGO.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // Station Table
        Transform tableT = stationGO.transform.Find("PrinterTable");
        if (tableT == null)
        {
            GameObject tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Furniture_ges1/glass_table/glass_table.FBX");
            GameObject tableInstance;
            if (tablePrefab != null)
            {
                tableInstance = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
            }
            else
            {
                tableInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tableInstance.transform.localScale = new Vector3(1.2f, 0.75f, 0.8f);
            }
            tableInstance.name = "PrinterTable";
            tableInstance.transform.SetParent(stationGO.transform, false);
            tableInstance.transform.localPosition = Vector3.zero;
            tableInstance.transform.localRotation = Quaternion.identity;

            // Ensure table has collider
            if (tableInstance.GetComponent<Collider>() == null)
            {
                var bc = tableInstance.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.2f, 0.8f, 0.8f);
                bc.center = new Vector3(0f, 0.4f, 0f);
            }
            tableT = tableInstance.transform;
        }

        // Printer Model
        Transform printerT = stationGO.transform.Find("Printer");
        if (printerT == null)
        {
            GameObject printerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Furniture_ges1/printer/printer.FBX");
            GameObject printerInstance;
            if (printerPrefab != null)
            {
                printerInstance = (GameObject)PrefabUtility.InstantiatePrefab(printerPrefab);
            }
            else
            {
                printerInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }
            printerInstance.name = "Printer";
            printerInstance.transform.SetParent(stationGO.transform, false);
            printerInstance.transform.localPosition = new Vector3(0f, 0.76f, 0f);
            printerInstance.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            printerInstance.transform.localScale = Vector3.one * 0.9f;

            printerT = printerInstance.transform;
        }

        // Configure Printer BoxCollider
        var printerCol = printerT.GetComponent<BoxCollider>();
        if (printerCol == null) printerCol = printerT.gameObject.AddComponent<BoxCollider>();
        printerCol.size = new Vector3(1.0f, 0.65f, 1.0f);
        printerCol.center = new Vector3(0f, 0.32f, 0f);
        printerCol.isTrigger = false;

        // Configure AudioSource
        var printerAudio = printerT.GetComponent<AudioSource>();
        if (printerAudio == null) printerAudio = printerT.gameObject.AddComponent<AudioSource>();
        printerAudio.spatialBlend = 1f;
        printerAudio.minDistance = 2f;
        printerAudio.maxDistance = 16f;
        printerAudio.playOnAwake = false;

        // Status LED Light
        Transform ledT = printerT.Find("StatusLED");
        Light ledLight = null;
        if (ledT == null)
        {
            GameObject ledGO = new GameObject("StatusLED");
            ledGO.transform.SetParent(printerT, false);
            ledGO.transform.localPosition = new Vector3(0.35f, 0.45f, 0.35f);
            ledLight = ledGO.AddComponent<Light>();
            ledLight.type = LightType.Point;
            ledLight.range = 1.2f;
            ledLight.intensity = 1.0f;
            ledLight.color = Color.red;
            ledT = ledGO.transform;
        }
        else
        {
            ledLight = ledT.GetComponent<Light>();
        }

        // Physical Voucher Model on Output Tray
        Transform voucherDocT = printerT.Find("VoucherDocumentModel");
        if (voucherDocT == null)
        {
            GameObject docGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            docGO.name = "VoucherDocumentModel";
            docGO.transform.SetParent(printerT, false);
            docGO.transform.localPosition = new Vector3(0.05f, 0.42f, 0.22f);
            docGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            docGO.transform.localScale = new Vector3(0.22f, 0.30f, 1f);

            var docCol = docGO.GetComponent<Collider>();
            if (docCol != null) Object.DestroyImmediate(docCol);

            // Material for printed voucher
            Material paperMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Held_Blank_Paper_Mat.mat");
            if (paperMat != null)
            {
                docGO.GetComponent<MeshRenderer>().sharedMaterial = paperMat;
            }
            docGO.SetActive(false);
            voucherDocT = docGO.transform;
        }

        // Configure LibraryPrinterInteract component
        var printerInteract = printerT.GetComponent<LibraryPrinterInteract>();
        if (printerInteract == null) printerInteract = printerT.gameObject.AddComponent<LibraryPrinterInteract>();
        printerInteract.paperItemData = paperItem;
        printerInteract.voucherItemData = voucherItem;
        printerInteract.statusLight = ledLight;
        printerInteract.physicalVoucherModel = voucherDocT.gameObject;
        printerInteract.printerAudioClip = printerClip;
        printerInteract.horrorCueClip = horrorClip;

        Transform lightsGroup = room308.transform.Find("ClassroomLights");
        printerInteract.classroomLightsGroup = lightsGroup;

        log.AppendLine("Configured LibraryPrinterInteract on PrinterStation_Room308.");

        // ── 5. Setup Paper Ream Pickup in Bookshelves ──────────────────────────────
        Transform bookShelvesArea = furnitureParent.Find("Bookshelves_Area");
        GameObject paperPickupGO = GameObject.Find("Pickup_BondPaperReam");
        if (paperPickupGO == null)
        {
            paperPickupGO = new GameObject("Pickup_BondPaperReam");
            if (bookShelvesArea != null) paperPickupGO.transform.SetParent(bookShelvesArea, false);
            else paperPickupGO.transform.SetParent(room308.transform, false);
        }

        // Placed in dark bookshelf aisle North-West
        paperPickupGO.transform.position = new Vector3(-81.5f, 14.80f, 49.5f);
        paperPickupGO.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

        Transform paperVisualT = paperPickupGO.transform.Find("PaperReamModel");
        if (paperVisualT == null)
        {
            GameObject visualGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualGO.name = "PaperReamModel";
            visualGO.transform.SetParent(paperPickupGO.transform, false);
            visualGO.transform.localPosition = Vector3.zero;
            visualGO.transform.localRotation = Quaternion.identity;
            visualGO.transform.localScale = new Vector3(0.24f, 0.08f, 0.32f);

            var cubeCol = visualGO.GetComponent<Collider>();
            if (cubeCol != null) Object.DestroyImmediate(cubeCol);

            // Subtle paper glow
            GameObject glintLight = new GameObject("PaperGlintLight");
            glintLight.transform.SetParent(paperPickupGO.transform, false);
            glintLight.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            var gLight = glintLight.AddComponent<Light>();
            gLight.type = LightType.Point;
            gLight.color = new Color(0.9f, 0.95f, 1.0f);
            gLight.intensity = 0.8f;
            gLight.range = 1.8f;
        }

        var paperCol = paperPickupGO.GetComponent<BoxCollider>();
        if (paperCol == null) paperCol = paperPickupGO.AddComponent<BoxCollider>();
        paperCol.size = new Vector3(0.5f, 0.4f, 0.5f);
        paperCol.center = Vector3.zero;
        paperCol.isTrigger = false;

        var paperPickupComp = paperPickupGO.GetComponent<PaperReamPickup>();
        if (paperPickupComp == null) paperPickupComp = paperPickupGO.AddComponent<PaperReamPickup>();
        paperPickupComp.paperItemData = paperItem;
        paperPickupComp.pickupSound = horrorClip;
        paperPickupComp.enableFloat = true;

        log.AppendLine($"Placed Pickup_BondPaperReam at {paperPickupGO.transform.position}");

        // ── 6. Wire Librarian ClearanceNPC ─────────────────────────────────────────
        GameObject librarianGO = GameObject.Find("Signatory_Librarian");
        if (librarianGO != null)
        {
            var cNpc = librarianGO.GetComponent<ClearanceNPC>();
            if (cNpc != null)
            {
                cNpc.libraryVoucherItem = voucherItem;
                EditorUtility.SetDirty(cNpc);
                log.AppendLine("Assigned Library Clearance Voucher to Signatory_Librarian.");
            }
        }
        else
        {
            log.AppendLine("WARNING: Signatory_Librarian not found!");
        }

        // ── 7. Setup Objectives UI on HudCanvas ───────────────────────────────────
        GameObject hud = GameObject.Find("HudCanvas");
        if (hud != null)
        {
            Font horrorFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
            Sprite scrambledPaperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");

            Transform existingObj = hud.transform.Find("ObjectivePanel");
            GameObject panelGO;
            if (existingObj != null)
            {
                panelGO = existingObj.gameObject;
            }
            else
            {
                panelGO = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(CanvasGroup));
                panelGO.transform.SetParent(hud.transform, false);
            }

            // Remove old accent bar if present
            Transform barT = panelGO.transform.Find("AccentBar");
            if (barT != null) Object.DestroyImmediate(barT.gameObject);

            RectTransform panelRt = panelGO.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(25f, -25f);
            panelRt.sizeDelta = new Vector2(400f, 135f);
            panelRt.localRotation = Quaternion.Euler(0f, 0f, -1.8f); // subtle eerie paper tilt

            var bgImg = panelGO.GetComponent<Image>();
            if (bgImg == null) bgImg = panelGO.AddComponent<Image>();
            if (scrambledPaperSprite != null)
            {
                bgImg.sprite = scrambledPaperSprite;
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Simple;
            }
            else
            {
                bgImg.color = new Color(0.85f, 0.80f, 0.70f, 0.95f);
            }

            // Category Header (e.g. "OBJECTIVE:")
            Transform headerT = panelGO.transform.Find("HeaderText");
            Text headerTextComp;
            if (headerT == null)
            {
                GameObject headerGO = new GameObject("HeaderText", typeof(RectTransform), typeof(Text));
                headerGO.transform.SetParent(panelGO.transform, false);
                headerTextComp = headerGO.GetComponent<Text>();
            }
            else
            {
                headerTextComp = headerT.GetComponent<Text>();
            }

            RectTransform headerRt = headerTextComp.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0f, 1f);
            headerRt.anchoredPosition = new Vector2(28f, -18f);
            headerRt.sizeDelta = new Vector2(-56f, 26f);

            headerTextComp.font = horrorFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerTextComp.fontSize = 20;
            headerTextComp.fontStyle = FontStyle.Normal;
            headerTextComp.alignment = TextAnchor.UpperLeft;
            headerTextComp.color = new Color(0.68f, 0.08f, 0.08f, 1f); // dried blood red
            headerTextComp.text = "OBJECTIVE:";

            var hShadow = headerTextComp.GetComponent<Shadow>();
            if (hShadow == null) hShadow = headerTextComp.gameObject.AddComponent<Shadow>();
            hShadow.effectColor = new Color(0.2f, 0f, 0f, 0.35f);
            hShadow.effectDistance = new Vector2(1f, -1f);

            // Title Text
            Transform titleT = panelGO.transform.Find("TitleText");
            Text titleTextComp;
            if (titleT == null)
            {
                GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                titleGO.transform.SetParent(panelGO.transform, false);
                titleTextComp = titleGO.GetComponent<Text>();
            }
            else
            {
                titleTextComp = titleT.GetComponent<Text>();
            }

            RectTransform titleRt = titleTextComp.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(28f, -46f);
            titleRt.sizeDelta = new Vector2(-56f, 28f);

            titleTextComp.font = horrorFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTextComp.fontSize = 21;
            titleTextComp.fontStyle = FontStyle.Normal;
            titleTextComp.alignment = TextAnchor.UpperLeft;
            titleTextComp.color = new Color(0.08f, 0.06f, 0.06f, 1f); // deep black ink
            titleTextComp.text = "Find Clearance Fragments";

            var tShadow = titleTextComp.GetComponent<Shadow>();
            if (tShadow == null) tShadow = titleTextComp.gameObject.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0.5f, 0.45f, 0.35f, 0.5f);
            tShadow.effectDistance = new Vector2(0.6f, -0.6f);

            // Detail Text
            Transform detailT = panelGO.transform.Find("DetailText");
            Text detailTextComp;
            if (detailT == null)
            {
                GameObject detailGO = new GameObject("DetailText", typeof(RectTransform), typeof(Text));
                detailGO.transform.SetParent(panelGO.transform, false);
                detailTextComp = detailGO.GetComponent<Text>();
            }
            else
            {
                detailTextComp = detailT.GetComponent<Text>();
            }

            RectTransform detailRt = detailTextComp.GetComponent<RectTransform>();
            detailRt.anchorMin = new Vector2(0f, 0f);
            detailRt.anchorMax = new Vector2(1f, 1f);
            detailRt.pivot = new Vector2(0f, 0f);
            detailRt.offsetMin = new Vector2(28f, 14f);
            detailRt.offsetMax = new Vector2(-28f, -76f);

            detailTextComp.font = horrorFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            detailTextComp.fontSize = 16;
            detailTextComp.fontStyle = FontStyle.Normal;
            detailTextComp.alignment = TextAnchor.UpperLeft;
            detailTextComp.color = new Color(0.20f, 0.16f, 0.16f, 0.95f); // graphite pencil
            detailTextComp.text = "Search the building corridors for 4 torn fragments.";

            // Configure ObjectiveHUD component
            var objHUD = panelGO.GetComponent<ObjectiveHUD>();
            if (objHUD == null) objHUD = panelGO.AddComponent<ObjectiveHUD>();
            objHUD.panelRoot = panelGO;
            objHUD.canvasGroup = panelGO.GetComponent<CanvasGroup>();
            objHUD.headerText = headerTextComp;
            objHUD.titleText = titleTextComp;
            objHUD.detailText = detailTextComp;
            objHUD.headerColor = new Color(0.68f, 0.08f, 0.08f, 1f);
            objHUD.titleColor = new Color(0.08f, 0.06f, 0.06f, 1f);
            objHUD.detailColor = new Color(0.20f, 0.16f, 0.16f, 0.95f);
            objHUD.updateSound = objChimeClip;

            log.AppendLine("Configured ObjectiveHUD with scrambled paper aesthetic & horror font.");
        }
        else
        {
            log.AppendLine("WARNING: HudCanvas not found in scene!");
        }

        // Save scene
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        log.AppendLine("Scene marked dirty and successfully saved!");

        string outPath = Path.Combine(Application.dataPath, "EditorOutput.txt");
        File.WriteAllText(outPath, log.ToString());
        Debug.Log("=== DEPLOYMENT COMPLETE ===\n" + log.ToString());
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 128;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    private static Texture2D BuildPaperReamTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        void Pix(int x, int y, Color c)
        {
            if (x >= 0 && x < w && y >= 0 && y < h) pixels[y * w + x] = c;
        }

        void Box(int x0, int y0, int x1, int y1, Color c)
        {
            for (int bx = x0; bx <= x1; bx++)
                for (int by = y0; by <= y1; by++)
                    Pix(bx, by, c);
        }

        Color paperWhite = new Color(0.96f, 0.96f, 0.94f, 1f);
        Color paperShadow = new Color(0.80f, 0.80f, 0.78f, 1f);
        Color bandBlue = new Color(0.18f, 0.35f, 0.65f, 1f);
        Color bandLight = new Color(0.28f, 0.48f, 0.80f, 1f);
        Color border = new Color(0.30f, 0.30f, 0.32f, 1f);

        // Thick stack of paper (x: 18-78, y: 16-76)
        Box(18, 16, 78, 76, paperWhite);

        // Page layers shadow on bottom and right
        Box(18, 16, 78, 22, paperShadow);
        Box(74, 16, 78, 76, paperShadow);

        // Border outline
        for (int x = 18; x <= 78; x++) { Pix(x, 16, border); Pix(x, 76, border); }
        for (int y = 16; y <= 76; y++) { Pix(18, y, border); Pix(78, y, border); }

        // Packaging wrapper band across the center
        Box(18, 38, 78, 54, bandBlue);
        for (int x = 18; x <= 78; x++) { Pix(x, 54, bandLight); Pix(x, 38, border); }

        // University seal / label on the band
        Box(36, 42, 60, 50, Color.white);
        Box(40, 44, 56, 48, new Color(0.8f, 0.1f, 0.1f, 1f));

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildVoucherTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        void Pix(int x, int y, Color c)
        {
            if (x >= 0 && x < w && y >= 0 && y < h) pixels[y * w + x] = c;
        }

        void Box(int x0, int y0, int x1, int y1, Color c)
        {
            for (int bx = x0; bx <= x1; bx++)
                for (int by = y0; by <= y1; by++)
                    Pix(bx, by, c);
        }

        Color sheetColor = new Color(0.97f, 0.95f, 0.88f, 1f); // Aged official paper
        Color border = new Color(0.45f, 0.42f, 0.38f, 1f);
        Color textLine = new Color(0.25f, 0.25f, 0.28f, 0.8f);
        Color stampRed = new Color(0.85f, 0.15f, 0.15f, 0.95f);

        // Certificate document body
        Box(16, 12, 80, 84, sheetColor);

        // Outline
        for (int x = 16; x <= 80; x++) { Pix(x, 12, border); Pix(x, 84, border); }
        for (int y = 12; y <= 84; y++) { Pix(16, y, border); Pix(80, y, border); }

        // Top decorative border
        for (int x = 20; x <= 76; x++) Pix(x, 80, border);

        // Document text lines
        for (int l = 0; l < 6; l++)
        {
            int ly = 68 - l * 8;
            for (int x = 22; x <= 62; x++) Pix(x, ly, textLine);
        }

        // Red circular clearance stamp in bottom-right
        int cx = 60;
        int cy = 30;
        int rad = 14;
        for (int y = cy - rad; y <= cy + rad; y++)
        {
            for (int x = cx - rad; x <= cx + rad; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (Mathf.Abs(d - rad) < 1.4f || d < 4.0f)
                {
                    Pix(x, y, stampRed);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ── Synthetic Audio Generators (16-bit PCM WAV) ───────────────────────────

    private static byte[] GeneratePrinterAudio(float duration)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;

            if (t < 0.6f)
            {
                // Roller pickup click & clunk
                float f = 120f + Mathf.Sin(t * 30f) * 40f;
                val = Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f;
                // Periodic gear click
                if ((i % 1200) < 100) val += Random.Range(-0.3f, 0.3f);
            }
            else if (t < 3.3f)
            {
                // Stepper motor whir + print carriage pass
                float motorFreq = 420f + Mathf.Sin(t * 12f) * 60f;
                float motor = Mathf.Sin(2f * Mathf.PI * motorFreq * t) * 0.35f;

                // High pitch dot matrix chirp
                float chirp = Mathf.Sin(2f * Mathf.PI * 1600f * t) * 0.12f;

                // Carriage swoop every 0.6s
                float carriagePhase = (t - 0.6f) % 0.65f;
                float swoosh = Mathf.Sin(carriagePhase * Mathf.PI / 0.65f) * Random.Range(-0.2f, 0.2f);

                val = motor + chirp + swoosh;
            }
            else
            {
                // Paper eject sweep + confirmation beep
                float sweep = Random.Range(-0.15f, 0.15f) * (1f - (t - 3.3f) / 0.5f);
                float beep = 0f;
                if (t >= 3.45f && t <= 3.65f)
                {
                    beep = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.25f;
                }
                val = sweep + beep;
            }

            samples[i] = (short)Mathf.Clamp(val * 32767f, -32768f, 32767f);
        }

        return EncodeWav(samples, sampleRate, 1);
    }

    private static byte[] GenerateHorrorCue(float duration)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Deep sub-bass swell
            float sub = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Sin(progress * Mathf.PI) * 0.5f;

            // Dissonant tritone harmonics
            float tri1 = Mathf.Sin(2f * Mathf.PI * 370f * t) * 0.15f;
            float tri2 = Mathf.Sin(2f * Mathf.PI * 523f * t) * 0.15f;

            // Eerie metallic flutter
            float flutter = Mathf.Sin(2f * Mathf.PI * 880f * t * (1f + Mathf.Sin(t * 8f) * 0.1f)) * 0.1f;

            // Subtle static hiss
            float noise = Random.Range(-0.04f, 0.04f);

            float val = (sub + (tri1 + tri2 + flutter) * (1f - progress) + noise) * (1f - progress * 0.4f);
            samples[i] = (short)Mathf.Clamp(val * 32767f, -32768f, 32767f);
        }

        return EncodeWav(samples, sampleRate, 1);
    }

    private static byte[] GenerateObjectiveChime(float duration)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 6.5f);

            // Clean bell chord (E5 & B5)
            float tone1 = Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.4f;
            float tone2 = Mathf.Sin(2f * Mathf.PI * 987.77f * t) * 0.25f;

            float val = (tone1 + tone2) * env;
            samples[i] = (short)Mathf.Clamp(val * 32767f, -32768f, 32767f);
        }

        return EncodeWav(samples, sampleRate, 1);
    }

    private static byte[] GeneratePaperScribble(float duration)
    {
        int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Fast scratchy pencil strokes (intermittent noise bursts)
            float strokePattern = Mathf.Sin(t * 50f) * Mathf.Sin(t * 14f);
            float scratch = 0f;
            if (strokePattern > 0.15f)
            {
                float noise = Random.Range(-0.35f, 0.35f);
                scratch = noise * Mathf.Sin(2f * Mathf.PI * Random.Range(1800f, 3200f) * t);
            }

            // Paper rustle friction (low-mid rumble)
            float rustle = Mathf.Sin(2f * Mathf.PI * 180f * t) * Random.Range(-0.08f, 0.08f);

            // Envelope: fade in, sustain scribbles, fade out
            float env = Mathf.Sin(progress * Mathf.PI);
            float val = (scratch * 0.75f + rustle * 0.25f) * env;

            samples[i] = (short)Mathf.Clamp(val * 32767f, -32768f, 32767f);
        }

        return EncodeWav(samples, sampleRate, 1);
    }

    private static byte[] EncodeWav(short[] samples, int sampleRate, int channels)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int subChunk1Size = 16;
            short audioFormat = 1; // PCM
            short bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int subChunk2Size = samples.Length * channels * bitsPerSample / 8;
            int chunkSize = 36 + subChunk2Size;

            // RIFF header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(chunkSize);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt subchunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(subChunk1Size);
            writer.Write(audioFormat);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // data subchunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(subChunk2Size);

            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write(samples[i]);
            }

            return stream.ToArray();
        }
    }
}
