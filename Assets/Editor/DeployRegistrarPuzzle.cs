using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DeployRegistrarPuzzle
{
    private const string PlayerAssetsDir = "Assets/PlayerAssets";
    private const string SoundDir = "Assets/Sounds";

    [MenuItem("Tools/Klirans/Deploy Registrar Document Sort Puzzle")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== DEPLOYING REGISTRAR DOCUMENT SORT MINI-GAME ===");

        // ── 1. Relocate Player Spawn Point to Center of Lobby ─────────────────────
        Vector3 lobbyCenterPos = new Vector3(-84.50f, 2.35f, 16.65f);
        Quaternion lobbyCenterRot = Quaternion.Euler(0f, 90f, 0f); // facing East toward Grand Entrance

        GameObject playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            playerGO.transform.position = lobbyCenterPos;
            playerGO.transform.rotation = lobbyCenterRot;
            EditorUtility.SetDirty(playerGO);
            log.AppendLine($"Moved Player to lobby center: {lobbyCenterPos}");
        }

        GameObject spawnPointGO = GameObject.Find("PlayerSpawnPoint");
        if (spawnPointGO != null)
        {
            spawnPointGO.transform.position = lobbyCenterPos;
            spawnPointGO.transform.rotation = lobbyCenterRot;
            EditorUtility.SetDirty(spawnPointGO);
            log.AppendLine($"Moved PlayerSpawnPoint to lobby center: {lobbyCenterPos}");
        }

        // ── 2. Generate Custom Textures ───────────────────────────────────────────
        if (!Directory.Exists(PlayerAssetsDir)) Directory.CreateDirectory(PlayerAssetsDir);

        string deskMatPath = $"{PlayerAssetsDir}/RegistrarDeskMat.png";
        string gradeSheetPath = $"{PlayerAssetsDir}/StudentGradeSheet.png";
        string trayPath = $"{PlayerAssetsDir}/ArchivalTray_Normal.png";
        string stampPath = $"{PlayerAssetsDir}/ArchivedStamp.png";

        if (!File.Exists(deskMatPath))
        {
            Texture2D tex = BuildRegistrarDeskMatTexture(1200, 900);
            File.WriteAllBytes(deskMatPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {deskMatPath}");
        }

        if (!File.Exists(gradeSheetPath))
        {
            Texture2D tex = BuildStudentGradeSheetTexture(800, 1000);
            File.WriteAllBytes(gradeSheetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {gradeSheetPath}");
        }

        if (!File.Exists(trayPath))
        {
            Texture2D tex = BuildArchivalTrayTexture(380, 160);
            File.WriteAllBytes(trayPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {trayPath}");
        }

        if (!File.Exists(stampPath))
        {
            Texture2D tex = BuildArchivedStampTexture(512, 256);
            File.WriteAllBytes(stampPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {stampPath}");
        }

        AssetDatabase.Refresh();

        ConfigureSpriteImporter(deskMatPath, 2048);
        ConfigureSpriteImporter(gradeSheetPath, 1024);
        ConfigureSpriteImporter(trayPath, 512);
        ConfigureSpriteImporter(stampPath, 512);

        Sprite deskMatSprite = AssetDatabase.LoadAssetAtPath<Sprite>(deskMatPath);
        Sprite gradeSheetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(gradeSheetPath);
        Sprite traySprite = AssetDatabase.LoadAssetAtPath<Sprite>(trayPath);
        Sprite stampSprite = AssetDatabase.LoadAssetAtPath<Sprite>(stampPath);

        // ── 3. Load Audio Clips ───────────────────────────────────────────────────
        AudioClip paperSlideAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/xeroxSoundEffect_Print.wav");
        if (paperSlideAudio == null) paperSlideAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/paper_scribble.wav");

        AudioClip trayDropAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/objective_chime.wav");
        AudioClip stampAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/xeroxSoundEffect.mp3");
        AudioClip errorAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/freesound_community-human_male_crazy-mumbles_1-30950.mp3");

        // Fonts
        Font horrorFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
        Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (standardFont == null) standardFont = horrorFont;

        // ── 4. Find HudCanvas & Clean Setup ───────────────────────────────────────
        GameObject hudCanvasGO = GameObject.Find("HudCanvas");
        if (hudCanvasGO == null)
        {
            Debug.LogError("[DeployRegistrarPuzzle] HudCanvas not found in scene!");
            return;
        }

        Transform existingMgr = hudCanvasGO.transform.Find("RegistrarPuzzleManager");
        if (existingMgr != null)
        {
            Object.DestroyImmediate(existingMgr.gameObject);
            log.AppendLine("Removed existing RegistrarPuzzleManager for clean rebuild.");
        }

        GameObject mgrGO = new GameObject("RegistrarPuzzleManager", typeof(RectTransform), typeof(RegistrarDocumentSortPuzzle));
        mgrGO.transform.SetParent(hudCanvasGO.transform, false);

        RectTransform mgrRt = mgrGO.GetComponent<RectTransform>();
        mgrRt.anchorMin = Vector2.zero;
        mgrRt.anchorMax = Vector2.one;
        mgrRt.offsetMin = Vector2.zero;
        mgrRt.offsetMax = Vector2.zero;

        RegistrarDocumentSortPuzzle puzzleComp = mgrGO.GetComponent<RegistrarDocumentSortPuzzle>();

        // ── 5. Backdrop Overlay ───────────────────────────────────────────────────
        GameObject backdropGO = new GameObject("BackdropOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdropGO.transform.SetParent(mgrGO.transform, false);
        RectTransform bdRt = backdropGO.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        Image bdImg = backdropGO.GetComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.72f);
        bdImg.raycastTarget = true;
        backdropGO.SetActive(false);

        // ── 6. Puzzle Panel (Registrar Desk Organizer Surface) ────────────────────
        GameObject panelGO = new GameObject("RegistrarPuzzlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(mgrGO.transform, false);
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(1060f, 750f);

        Image panelImg = panelGO.GetComponent<Image>();
        if (deskMatSprite != null)
        {
            panelImg.sprite = deskMatSprite;
            panelImg.type = Image.Type.Simple;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color(0.24f, 0.16f, 0.12f, 1f);
        }

        CanvasGroup panelCg = panelGO.GetComponent<CanvasGroup>();
        panelGO.SetActive(false);

        // ── 7. Header Section ─────────────────────────────────────────────────────
        GameObject headerGO = new GameObject("HeaderTitle", typeof(RectTransform), typeof(Text), typeof(Shadow));
        headerGO.transform.SetParent(panelGO.transform, false);
        RectTransform htRt = headerGO.GetComponent<RectTransform>();
        htRt.anchorMin = new Vector2(0f, 1f);
        htRt.anchorMax = new Vector2(1f, 1f);
        htRt.pivot = new Vector2(0.5f, 1f);
        htRt.anchoredPosition = new Vector2(0f, -28f);
        htRt.sizeDelta = new Vector2(-80f, 32f);

        Text htText = headerGO.GetComponent<Text>();
        htText.font = standardFont;
        htText.fontSize = 20;
        htText.fontStyle = FontStyle.Bold;
        htText.alignment = TextAnchor.MiddleCenter;
        htText.color = new Color(0.96f, 0.88f, 0.72f); // parchment gold
        htText.text = "PAMANTASAN NG CABUYAO — OFFICE OF THE UNIVERSITY REGISTRAR";

        Shadow htShadow = headerGO.GetComponent<Shadow>();
        htShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        htShadow.effectDistance = new Vector2(1f, -1f);

        // Criteria Title Box
        GameObject critTitleGO = new GameObject("CriteriaTitle", typeof(RectTransform), typeof(Text));
        critTitleGO.transform.SetParent(panelGO.transform, false);
        RectTransform ctRt = critTitleGO.GetComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0f, 1f);
        ctRt.anchorMax = new Vector2(1f, 1f);
        ctRt.pivot = new Vector2(0.5f, 1f);
        ctRt.anchoredPosition = new Vector2(0f, -62f);
        ctRt.sizeDelta = new Vector2(-100f, 26f);

        Text ctText = critTitleGO.GetComponent<Text>();
        ctText.font = standardFont;
        ctText.fontSize = 15;
        ctText.fontStyle = FontStyle.Bold;
        ctText.alignment = TextAnchor.MiddleCenter;
        ctText.color = new Color(0.92f, 0.75f, 0.35f); // warm gold highlight
        ctText.text = "ARCHIVAL CRITERIA: SORT BY COLLEGE DEPARTMENT";

        // Criteria Hint Text
        GameObject critHintGO = new GameObject("CriteriaHint", typeof(RectTransform), typeof(Text));
        critHintGO.transform.SetParent(panelGO.transform, false);
        RectTransform chRt = critHintGO.GetComponent<RectTransform>();
        chRt.anchorMin = new Vector2(0f, 1f);
        chRt.anchorMax = new Vector2(1f, 1f);
        chRt.pivot = new Vector2(0.5f, 1f);
        chRt.anchoredPosition = new Vector2(0f, -88f);
        chRt.sizeDelta = new Vector2(-120f, 20f);

        Text chText = critHintGO.GetComponent<Text>();
        chText.font = standardFont;
        chText.fontSize = 12;
        chText.fontStyle = FontStyle.Italic;
        chText.alignment = TextAnchor.MiddleCenter;
        chText.color = new Color(0.78f, 0.72f, 0.65f);
        chText.text = "<i>File each student's official grade slip into their corresponding academic department tray.</i>";

        // ── 8. Archival Trays Container (Top Horizontal Row) ──────────────────────
        GameObject traysContainerGO = new GameObject("TraysContainer", typeof(RectTransform));
        traysContainerGO.transform.SetParent(panelGO.transform, false);
        RectTransform tcRt = traysContainerGO.GetComponent<RectTransform>();
        tcRt.anchorMin = new Vector2(0.5f, 1f);
        tcRt.anchorMax = new Vector2(0.5f, 1f);
        tcRt.pivot = new Vector2(0.5f, 1f);
        tcRt.anchoredPosition = new Vector2(0f, -118f);
        tcRt.sizeDelta = new Vector2(980f, 140f);

        List<RegistrarDocumentSortPuzzle.ArchivalTrayUI> trayList = new List<RegistrarDocumentSortPuzzle.ArchivalTrayUI>();

        float trayW = 230f;
        float trayH = 135f;
        float traySpacing = 16f;
        float totalTraysW = 4 * trayW + 3 * traySpacing;
        float trayStartX = -totalTraysW * 0.5f + trayW * 0.5f;

        for (int i = 0; i < 4; i++)
        {
            GameObject trayGO = new GameObject($"ArchivalTray_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            trayGO.transform.SetParent(traysContainerGO.transform, false);

            RectTransform tRt = trayGO.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 0.5f);
            tRt.anchorMax = new Vector2(0.5f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = new Vector2(trayStartX + i * (trayW + traySpacing), 0f);
            tRt.sizeDelta = new Vector2(trayW, trayH);

            Image tImg = trayGO.GetComponent<Image>();
            if (traySprite != null)
            {
                tImg.sprite = traySprite;
                tImg.type = Image.Type.Sliced;
            }
            tImg.color = new Color(0.22f, 0.35f, 0.45f, 1f);

            Outline tOutline = trayGO.GetComponent<Outline>();
            tOutline.effectColor = new Color(0.85f, 0.70f, 0.35f, 0.75f);
            tOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Button tBtn = trayGO.GetComponent<Button>();
            ColorBlock cb = tBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            tBtn.colors = cb;

            // Tray Title Text
            GameObject titleGO = new GameObject("TrayTitle", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(trayGO.transform, false);
            RectTransform titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(-16f, 28f);

            Text titleText = titleGO.GetComponent<Text>();
            titleText.font = standardFont;
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = $"[ {i + 1} ] TRAY {i + 1}";

            // Tray Detail Text
            GameObject detailGO = new GameObject("TrayDetail", typeof(RectTransform), typeof(Text));
            detailGO.transform.SetParent(trayGO.transform, false);
            RectTransform detRt = detailGO.GetComponent<RectTransform>();
            detRt.anchorMin = new Vector2(0f, 0f);
            detRt.anchorMax = new Vector2(1f, 1f);
            detRt.offsetMin = new Vector2(10f, 42f);
            detRt.offsetMax = new Vector2(-10f, -44f);

            Text detText = detailGO.GetComponent<Text>();
            detText.font = standardFont;
            detText.fontSize = 11;
            detText.fontStyle = FontStyle.Italic;
            detText.alignment = TextAnchor.MiddleCenter;
            detText.lineSpacing = 1.15f;
            detText.color = new Color(0.88f, 0.84f, 0.78f);
            detText.text = "Category Description";

            // Click to Sort Action Banner on bottom of tray
            GameObject actionBannerGO = new GameObject("ActionBanner", typeof(RectTransform), typeof(Image));
            actionBannerGO.transform.SetParent(trayGO.transform, false);
            RectTransform abRt = actionBannerGO.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0f, 0f);
            abRt.anchorMax = new Vector2(1f, 0f);
            abRt.pivot = new Vector2(0.5f, 0f);
            abRt.anchoredPosition = new Vector2(0f, 6f);
            abRt.sizeDelta = new Vector2(-16f, 28f);

            Image abImg = actionBannerGO.GetComponent<Image>();
            abImg.color = new Color(0.12f, 0.18f, 0.24f, 0.85f);

            CreateTextChild(actionBannerGO.transform, "ActionText", $"[ FILE HERE ({i + 1}) ]", standardFont, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 0.85f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero);

            trayList.Add(new RegistrarDocumentSortPuzzle.ArchivalTrayUI
            {
                trayObject = trayGO,
                trayRect = tRt,
                trayBackground = tImg,
                trayTitleText = titleText,
                trayDetailText = detText,
                sortButton = tBtn
            });
        }

        // ── 9. Document Desk Area (Center-Bottom) ──────────────────────────────────
        GameObject deskAreaGO = new GameObject("DocumentDeskArea", typeof(RectTransform));
        deskAreaGO.transform.SetParent(panelGO.transform, false);
        RectTransform daRt = deskAreaGO.GetComponent<RectTransform>();
        daRt.anchorMin = new Vector2(0f, 0f);
        daRt.anchorMax = new Vector2(1f, 1f);
        daRt.offsetMin = new Vector2(40f, 40f);
        daRt.offsetMax = new Vector2(-40f, -270f);

        // Underlay stack effect (subtle tilted white paper cards behind top document)
        GameObject underlay2 = new GameObject("StackUnderlay_2", typeof(RectTransform), typeof(Image));
        underlay2.transform.SetParent(deskAreaGO.transform, false);
        RectTransform u2Rt = underlay2.GetComponent<RectTransform>();
        u2Rt.anchorMin = new Vector2(0.5f, 0.5f);
        u2Rt.anchorMax = new Vector2(0.5f, 0.5f);
        u2Rt.anchoredPosition = new Vector2(6f, -6f);
        u2Rt.sizeDelta = new Vector2(580f, 350f);
        u2Rt.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
        underlay2.GetComponent<Image>().color = new Color(0.80f, 0.76f, 0.68f, 0.6f);

        GameObject underlay1 = new GameObject("StackUnderlay_1", typeof(RectTransform), typeof(Image));
        underlay1.transform.SetParent(deskAreaGO.transform, false);
        RectTransform u1Rt = underlay1.GetComponent<RectTransform>();
        u1Rt.anchorMin = new Vector2(0.5f, 0.5f);
        u1Rt.anchorMax = new Vector2(0.5f, 0.5f);
        u1Rt.anchoredPosition = new Vector2(-4f, -3f);
        u1Rt.sizeDelta = new Vector2(580f, 350f);
        u1Rt.localRotation = Quaternion.Euler(0f, 0f, -1.8f);
        underlay1.GetComponent<Image>().color = new Color(0.88f, 0.84f, 0.76f, 0.75f);

        // Active Sortable Document Card
        GameObject docCardGO = new GameObject("DocumentCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        docCardGO.transform.SetParent(deskAreaGO.transform, false);
        RectTransform cardRt = docCardGO.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(580f, 350f);

        Image cardImg = docCardGO.GetComponent<Image>();
        if (gradeSheetSprite != null)
        {
            cardImg.sprite = gradeSheetSprite;
            cardImg.type = Image.Type.Simple;
            cardImg.color = Color.white;
        }
        else
        {
            cardImg.color = new Color(0.96f, 0.94f, 0.88f, 1f);
        }

        Outline cardOutline = docCardGO.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.35f, 0.25f, 0.15f, 0.6f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        // Card Header Text
        CreateTextChild(docCardGO.transform, "CardHeaderBanner", "PAMANTASAN NG CABUYAO • OFFICIAL STUDENT GRADE RECORD", standardFont, 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.45f, 0.25f, 0.15f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 22f));
        docCardGO.transform.Find("CardHeaderBanner").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);

        // Student Name Text
        GameObject nameGO = new GameObject("StudentNameText", typeof(RectTransform), typeof(Text));
        nameGO.transform.SetParent(docCardGO.transform, false);
        RectTransform nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, -50f);
        nameRt.sizeDelta = new Vector2(-60f, 28f);

        Text nameText = nameGO.GetComponent<Text>();
        nameText.font = standardFont;
        nameText.fontSize = 18;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = new Color(0.15f, 0.12f, 0.10f);
        nameText.text = "<b>SANTOS, JUAN MIGUEL M.</b>";

        // Student ID Text
        GameObject sidGO = new GameObject("StudentIdText", typeof(RectTransform), typeof(Text));
        sidGO.transform.SetParent(docCardGO.transform, false);
        RectTransform sidRt = sidGO.GetComponent<RectTransform>();
        sidRt.anchorMin = new Vector2(0f, 1f);
        sidRt.anchorMax = new Vector2(1f, 1f);
        sidRt.pivot = new Vector2(0.5f, 1f);
        sidRt.anchoredPosition = new Vector2(0f, -80f);
        sidRt.sizeDelta = new Vector2(-60f, 22f);

        Text sidText = sidGO.GetComponent<Text>();
        sidText.font = standardFont;
        sidText.fontSize = 13;
        sidText.fontStyle = FontStyle.Normal;
        sidText.alignment = TextAnchor.MiddleCenter;
        sidText.color = new Color(0.35f, 0.30f, 0.25f);
        sidText.text = "STUDENT NO.: 2024-01928-CB";

        // Department & Degree Program Text (Highlighted Box)
        GameObject deptBoxGO = new GameObject("DeptBox", typeof(RectTransform), typeof(Image));
        deptBoxGO.transform.SetParent(docCardGO.transform, false);
        RectTransform dboxRt = deptBoxGO.GetComponent<RectTransform>();
        dboxRt.anchorMin = new Vector2(0f, 1f);
        dboxRt.anchorMax = new Vector2(1f, 1f);
        dboxRt.pivot = new Vector2(0.5f, 1f);
        dboxRt.anchoredPosition = new Vector2(0f, -112f);
        dboxRt.sizeDelta = new Vector2(-60f, 52f);

        Image dboxImg = deptBoxGO.GetComponent<Image>();
        dboxImg.color = new Color(0.88f, 0.84f, 0.74f, 0.65f);

        GameObject deptTextGO = new GameObject("DeptText", typeof(RectTransform), typeof(Text));
        deptTextGO.transform.SetParent(deptBoxGO.transform, false);
        RectTransform dtRt = deptTextGO.GetComponent<RectTransform>();
        dtRt.anchorMin = Vector2.zero;
        dtRt.anchorMax = Vector2.one;
        dtRt.offsetMin = new Vector2(12f, 4f);
        dtRt.offsetMax = new Vector2(-12f, -4f);

        Text deptText = deptTextGO.GetComponent<Text>();
        deptText.font = standardFont;
        deptText.fontSize = 13;
        deptText.alignment = TextAnchor.MiddleLeft;
        deptText.color = new Color(0.18f, 0.14f, 0.10f);
        deptText.text = "COLLEGE: <b>College of Computing Studies (CCS)</b>\nPROGRAM: BS Information Technology";

        // Year Level & Semester Row
        GameObject yearSemRowGO = new GameObject("YearSemRow", typeof(RectTransform));
        yearSemRowGO.transform.SetParent(docCardGO.transform, false);
        RectTransform ysRt = yearSemRowGO.GetComponent<RectTransform>();
        ysRt.anchorMin = new Vector2(0f, 1f);
        ysRt.anchorMax = new Vector2(1f, 1f);
        ysRt.pivot = new Vector2(0.5f, 1f);
        ysRt.anchoredPosition = new Vector2(0f, -172f);
        ysRt.sizeDelta = new Vector2(-60f, 26f);

        GameObject yrGO = new GameObject("YearText", typeof(RectTransform), typeof(Text));
        yrGO.transform.SetParent(yearSemRowGO.transform, false);
        RectTransform yrRt = yrGO.GetComponent<RectTransform>();
        yrRt.anchorMin = new Vector2(0f, 0f);
        yrRt.anchorMax = new Vector2(0.5f, 1f);
        yrRt.offsetMin = Vector2.zero;
        yrRt.offsetMax = Vector2.zero;
        Text yrText = yrGO.GetComponent<Text>();
        yrText.font = standardFont;
        yrText.fontSize = 13;
        yrText.alignment = TextAnchor.MiddleLeft;
        yrText.color = new Color(0.25f, 0.20f, 0.15f);
        yrText.text = "YEAR LEVEL: <b>3rd Year</b>";

        GameObject semGO = new GameObject("SemText", typeof(RectTransform), typeof(Text));
        semGO.transform.SetParent(yearSemRowGO.transform, false);
        RectTransform semRt = semGO.GetComponent<RectTransform>();
        semRt.anchorMin = new Vector2(0.5f, 0f);
        semRt.anchorMax = new Vector2(1f, 1f);
        semRt.offsetMin = Vector2.zero;
        semRt.offsetMax = Vector2.zero;
        Text semText = semGO.GetComponent<Text>();
        semText.font = standardFont;
        semText.fontSize = 13;
        semText.alignment = TextAnchor.MiddleRight;
        semText.color = new Color(0.25f, 0.20f, 0.15f);
        semText.text = "SEMESTER: <b>1st Semester</b>";

        // Grades Summary Box
        GameObject gradesBoxGO = new GameObject("GradesBox", typeof(RectTransform), typeof(Image));
        gradesBoxGO.transform.SetParent(docCardGO.transform, false);
        RectTransform gbRt = gradesBoxGO.GetComponent<RectTransform>();
        gbRt.anchorMin = new Vector2(0f, 1f);
        gbRt.anchorMax = new Vector2(1f, 1f);
        gbRt.pivot = new Vector2(0.5f, 1f);
        gbRt.anchoredPosition = new Vector2(0f, -206f);
        gbRt.sizeDelta = new Vector2(-60f, 75f);

        Image gbImg = gradesBoxGO.GetComponent<Image>();
        gbImg.color = new Color(0.92f, 0.89f, 0.82f, 0.75f);

        GameObject gradesTextGO = new GameObject("GradesText", typeof(RectTransform), typeof(Text));
        gradesTextGO.transform.SetParent(gradesBoxGO.transform, false);
        RectTransform gtRt = gradesTextGO.GetComponent<RectTransform>();
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = new Vector2(12f, 6f);
        gtRt.offsetMax = new Vector2(-12f, -6f);

        Text gradesText = gradesTextGO.GetComponent<Text>();
        gradesText.font = standardFont;
        gradesText.fontSize = 12;
        gradesText.alignment = TextAnchor.MiddleCenter;
        gradesText.color = new Color(0.20f, 0.16f, 0.12f);
        gradesText.text = "OFFICIAL GRADE RECORD:\n<i>IT311: 1.25 | CS302: 1.50 | GE104: 1.75</i>";

        // Drag Instructions Bar on Card Footer
        GameObject dragHintGO = new GameObject("DragHintText", typeof(RectTransform), typeof(Text));
        dragHintGO.transform.SetParent(docCardGO.transform, false);
        RectTransform dhRt = dragHintGO.GetComponent<RectTransform>();
        dhRt.anchorMin = new Vector2(0f, 0f);
        dhRt.anchorMax = new Vector2(1f, 0f);
        dhRt.pivot = new Vector2(0.5f, 0f);
        dhRt.anchoredPosition = new Vector2(0f, 12f);
        dhRt.sizeDelta = new Vector2(-60f, 24f);

        Text dhText = dragHintGO.GetComponent<Text>();
        dhText.font = standardFont;
        dhText.fontSize = 11;
        dhText.fontStyle = FontStyle.Italic;
        dhText.alignment = TextAnchor.MiddleCenter;
        dhText.color = new Color(0.45f, 0.35f, 0.25f);
        dhText.text = "(Drag card up into a tray, click [FILE HERE], or press [1 - 4] on keyboard)";

        // ── 10. Footer Section (Status, Stack Remaining, Feedback) ────────────────
        GameObject footerGO = new GameObject("FooterSection", typeof(RectTransform));
        footerGO.transform.SetParent(panelGO.transform, false);
        RectTransform ftRt = footerGO.GetComponent<RectTransform>();
        ftRt.anchorMin = new Vector2(0f, 0f);
        ftRt.anchorMax = new Vector2(1f, 0f);
        ftRt.pivot = new Vector2(0.5f, 0f);
        ftRt.anchoredPosition = new Vector2(0f, 16f);
        ftRt.sizeDelta = new Vector2(-80f, 40f);

        // Progress counter (left)
        GameObject progGO = new GameObject("ProgressCounterText", typeof(RectTransform), typeof(Text));
        progGO.transform.SetParent(footerGO.transform, false);
        RectTransform progRt = progGO.GetComponent<RectTransform>();
        progRt.anchorMin = new Vector2(0f, 0f);
        progRt.anchorMax = new Vector2(0.35f, 1f);
        progRt.offsetMin = Vector2.zero;
        progRt.offsetMax = Vector2.zero;

        Text progText = progGO.GetComponent<Text>();
        progText.font = standardFont;
        progText.fontSize = 13;
        progText.fontStyle = FontStyle.Bold;
        progText.alignment = TextAnchor.MiddleLeft;
        progText.color = new Color(0.92f, 0.84f, 0.65f);
        progText.text = "DOCUMENTS FILED: <b>0 / 6</b>";

        // Stack remaining (right)
        GameObject stackRemGO = new GameObject("StackRemainingText", typeof(RectTransform), typeof(Text));
        stackRemGO.transform.SetParent(footerGO.transform, false);
        RectTransform srRt = stackRemGO.GetComponent<RectTransform>();
        srRt.anchorMin = new Vector2(0.65f, 0f);
        srRt.anchorMax = new Vector2(1f, 1f);
        srRt.offsetMin = Vector2.zero;
        srRt.offsetMax = Vector2.zero;

        Text srText = stackRemGO.GetComponent<Text>();
        srText.font = standardFont;
        srText.fontSize = 13;
        srText.fontStyle = FontStyle.Bold;
        srText.alignment = TextAnchor.MiddleRight;
        srText.color = new Color(0.92f, 0.84f, 0.65f);
        srText.text = "REMAINING IN STACK: <b>6</b>";

        // Feedback Text (Center)
        GameObject fbGO = new GameObject("FeedbackText", typeof(RectTransform), typeof(Text));
        fbGO.transform.SetParent(footerGO.transform, false);
        RectTransform fbRt = fbGO.GetComponent<RectTransform>();
        fbRt.anchorMin = new Vector2(0.28f, 0f);
        fbRt.anchorMax = new Vector2(0.72f, 1f);
        fbRt.offsetMin = Vector2.zero;
        fbRt.offsetMax = Vector2.zero;

        Text fbText = fbGO.GetComponent<Text>();
        fbText.font = standardFont;
        fbText.fontSize = 13;
        fbText.alignment = TextAnchor.MiddleCenter;
        fbText.color = new Color(0.85f, 0.80f, 0.72f);
        fbText.text = "Drag the document or click a destination tray / press [1-4] to file it.";

        // ── 11. Official Rubber Stamp Overlay ─────────────────────────────────────
        GameObject stampGO = new GameObject("ArchivedStampImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        stampGO.transform.SetParent(panelGO.transform, false);
        RectTransform stampRt = stampGO.GetComponent<RectTransform>();
        stampRt.anchorMin = new Vector2(0.5f, 0.5f);
        stampRt.anchorMax = new Vector2(0.5f, 0.5f);
        stampRt.pivot = new Vector2(0.5f, 0.5f);
        stampRt.anchoredPosition = new Vector2(0f, -60f);
        stampRt.sizeDelta = new Vector2(480f, 240f);

        Image stampImg = stampGO.GetComponent<Image>();
        if (stampSprite != null)
        {
            stampImg.sprite = stampSprite;
            stampImg.type = Image.Type.Simple;
            stampImg.color = Color.white;
        }
        else
        {
            stampImg.color = new Color(0.12f, 0.28f, 0.55f, 0.9f);
        }
        stampImg.raycastTarget = false;
        stampGO.SetActive(false);

        // ── 12. Close Button ──────────────────────────────────────────────────────
        GameObject closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(panelGO.transform, false);
        RectTransform closeRt = closeBtnGO.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);
        closeRt.sizeDelta = new Vector2(36f, 36f);

        Image closeImg = closeBtnGO.GetComponent<Image>();
        closeImg.color = new Color(0.42f, 0.22f, 0.16f, 0.85f);

        Button closeBtn = closeBtnGO.GetComponent<Button>();
        CreateTextChild(closeBtnGO.transform, "CloseX", "✕", standardFont, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero);

        // ── 13. Wire up RegistrarDocumentSortPuzzle References ────────────────────
        puzzleComp.backdropOverlay = backdropGO;
        puzzleComp.puzzlePanel = panelGO;
        puzzleComp.canvasGroup = panelCg;
        puzzleComp.criteriaTitleText = ctText;
        puzzleComp.criteriaHintText = chText;
        puzzleComp.progressCounterText = progText;
        puzzleComp.feedbackText = fbText;
        puzzleComp.traysContainer = traysContainerGO.transform;
        puzzleComp.trayUIList = trayList;

        puzzleComp.documentCardTransform = cardRt;
        puzzleComp.docStudentNameText = nameText;
        puzzleComp.docStudentIdText = sidText;
        puzzleComp.docDepartmentText = deptText;
        puzzleComp.docYearLevelText = yrText;
        puzzleComp.docSemesterText = semText;
        puzzleComp.docGradesText = gradesText;
        puzzleComp.stackRemainingText = srText;

        puzzleComp.archivedStampImage = stampImg;
        puzzleComp.closeButton = closeBtn;

        puzzleComp.paperSlideAudio = paperSlideAudio;
        puzzleComp.trayDropAudio = trayDropAudio;
        puzzleComp.stampAudio = stampAudio;
        puzzleComp.errorAudio = errorAudio;

        EditorUtility.SetDirty(puzzleComp);
        EditorUtility.SetDirty(mgrGO);

        // ── 14. Save Scene ────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        log.AppendLine("Successfully deployed RegistrarDocumentSortPuzzle UI under HudCanvas!");
        log.AppendLine("Scene saved successfully.");

        string outPath = Path.Combine(Application.dataPath, "RegistrarPuzzleOutput.txt");
        File.WriteAllText(outPath, log.ToString());
        Debug.Log(log.ToString());
    }

    private static void CreateTextChild(Transform parent, string name, string content, Font font, int size, FontStyle style, TextAnchor anchor, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset)
    {
        GameObject textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(parent, false);
        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offset;
        rt.offsetMax = offset;

        Text t = textGO.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.text = content;
    }

    private static void ConfigureSpriteImporter(string path, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Procedurally builds a dark mahogany / archival leather desktop mat texture.
    /// </summary>
    private static Texture2D BuildRegistrarDeskMatTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        Color woodBase = new Color(0.18f, 0.12f, 0.08f, 1f); // dark mahogany
        Color leatherMat = new Color(0.24f, 0.16f, 0.11f, 1f); // desk organizer leather
        Color brassGold = new Color(0.72f, 0.55f, 0.28f, 1f); // corner brackets

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;

                float woodGrain = Mathf.PerlinNoise(nx * 20f, ny * 3f) * 0.08f;
                float leatherGrain = Mathf.PerlinNoise(nx * 45f, ny * 45f) * 0.04f;

                Color c = Color.Lerp(woodBase, leatherMat, 0.7f) + new Color(woodGrain + leatherGrain, woodGrain * 0.7f, woodGrain * 0.4f, 0f);

                // Brass corner accents (triangular corners)
                int cornerDist = 45;
                bool isCorner = (x < cornerDist && y < cornerDist) ||
                                (x > w - cornerDist && y < cornerDist) ||
                                (x < cornerDist && y > h - cornerDist) ||
                                (x > w - cornerDist && y > h - cornerDist);

                if (isCorner)
                {
                    c = Color.Lerp(c, brassGold, 0.65f);
                }

                // Inner embossed border
                int bMargin = 16;
                bool isBorder = (x >= bMargin && x <= bMargin + 3) || (x <= w - bMargin && x >= w - bMargin - 3) ||
                                (y >= bMargin && y <= bMargin + 3) || (y <= h - bMargin && y >= h - bMargin - 3);
                if (isBorder)
                {
                    c = Color.Lerp(c, brassGold, 0.45f);
                }

                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Procedurally builds an official student grade transcript / evaluation sheet texture.
    /// </summary>
    private static Texture2D BuildStudentGradeSheetTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        Color paperBase = new Color(0.96f, 0.94f, 0.88f, 1f); // warm ivory
        Color paperShadow = new Color(0.86f, 0.82f, 0.74f, 1f);
        Color ruling = new Color(0.82f, 0.78f, 0.70f, 0.5f);
        Color headerNavy = new Color(0.14f, 0.22f, 0.35f, 1f);
        Color borderGold = new Color(0.65f, 0.50f, 0.28f, 0.7f);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;
                float noise = Mathf.PerlinNoise(nx * 12f, ny * 12f) * 0.05f;

                // Edge shading
                float edgeDist = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny)) * 2f;
                float vig = Mathf.Clamp01(edgeDist * 4f);

                Color c = Color.Lerp(paperShadow, paperBase, vig) + new Color(noise, noise * 0.9f, noise * 0.7f, 0f);

                // Circular seal watermark in upper center
                float cx = nx - 0.5f;
                float cy = ny - 0.6f;
                float distFromCenter = Mathf.Sqrt(cx * cx + cy * cy);
                if (distFromCenter > 0.16f && distFromCenter < 0.20f)
                {
                    c = Color.Lerp(c, new Color(0.82f, 0.75f, 0.62f, 1f), 0.28f);
                }

                // Horizontal grade ledger ruling
                if (y > 180 && y < 650 && (y % 32 == 0))
                {
                    c = Color.Lerp(c, ruling, 0.7f);
                }

                pixels[y * w + x] = c;
            }
        }

        // Left margin hole punches
        DrawCircle(pixels, w, 28, h / 4, 10, new Color(0.18f, 0.12f, 0.08f, 0.8f));
        DrawCircle(pixels, w, 28, h / 2, 10, new Color(0.18f, 0.12f, 0.08f, 0.8f));
        DrawCircle(pixels, w, 28, 3 * h / 4, 10, new Color(0.18f, 0.12f, 0.08f, 0.8f));

        // Elegant double borders
        int bMargin = 14;
        for (int x = bMargin; x < w - bMargin; x++)
        {
            pixels[bMargin * w + x] = headerNavy;
            pixels[(h - bMargin) * w + x] = headerNavy;
            pixels[(bMargin + 3) * w + x] = borderGold;
            pixels[(h - bMargin - 3) * w + x] = borderGold;
        }
        for (int y = bMargin; y < h - bMargin; y++)
        {
            pixels[y * w + bMargin] = headerNavy;
            pixels[y * w + (w - bMargin)] = headerNavy;
            pixels[y * w + (bMargin + 3)] = borderGold;
            pixels[y * w + (w - bMargin - 3)] = borderGold;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Procedurally builds an archival in/out tray box texture.
    /// </summary>
    private static Texture2D BuildArchivalTrayTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        Color trayBase = new Color(0.20f, 0.30f, 0.40f, 1f);
        Color trayLip = new Color(0.28f, 0.42f, 0.55f, 1f);
        Color trayShadow = new Color(0.12f, 0.18f, 0.25f, 1f);
        Color metalGold = new Color(0.78f, 0.65f, 0.35f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isBorder = (x == 0 || x == w - 1 || y == 0 || y == h - 1);
                bool isTopBevel = (y >= h - 6);
                bool isBottomLip = (y <= 12);

                if (isBorder)
                {
                    pixels[y * w + x] = metalGold;
                }
                else if (isTopBevel)
                {
                    pixels[y * w + x] = trayLip;
                }
                else if (isBottomLip)
                {
                    pixels[y * w + x] = trayShadow;
                }
                else
                {
                    pixels[y * w + x] = trayBase;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Procedurally builds the "FILED & ARCHIVED" official Registrar rubber stamp texture.
    /// </summary>
    private static Texture2D BuildArchivedStampTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Color stampBlue = new Color(0.12f, 0.28f, 0.55f, 0.92f);
        Color stampLight = new Color(0.20f, 0.38f, 0.68f, 0.85f);

        int marginX = 24;
        int marginY = 18;
        int borderThick = 9;

        for (int y = marginY; y < h - marginY; y++)
        {
            for (int x = marginX; x < w - marginX; x++)
            {
                bool isOuterBorder = (x < marginX + borderThick || x >= w - marginX - borderThick ||
                                      y < marginY + borderThick || y >= h - marginY - borderThick);

                bool isInnerBorder = ((x >= marginX + 16 && x < marginX + 20) || (x < w - marginX - 16 && x >= w - marginX - 20) ||
                                      (y >= marginY + 14 && y < marginY + 18) || (y < h - marginY - 14 && y >= h - marginY - 18)) &&
                                     (x >= marginX + 16 && x <= w - marginX - 16 && y >= marginY + 14 && y <= h - marginY - 14);

                if (isOuterBorder || isInnerBorder)
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    if (noise > 0.18f)
                    {
                        pixels[y * w + x] = (noise > 0.5f) ? stampBlue : stampLight;
                    }
                }
            }
        }

        // Faint ink haze inside stamp
        for (int y = marginY + 20; y < h - marginY - 20; y++)
        {
            for (int x = marginX + 20; x < w - marginX - 20; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                if (n > 0.48f)
                {
                    pixels[y * w + x] = new Color(0.15f, 0.32f, 0.60f, (n - 0.48f) * 0.18f);
                }
            }
        }

        // Decorative horizontal bars
        int barY1 = h / 2 + 32;
        int barY2 = h / 2 - 32;
        for (int x = 60; x < w - 60; x++)
        {
            float n = Mathf.PerlinNoise(x * 0.18f, barY1 * 0.18f);
            if (n > 0.22f)
            {
                for (int t = 0; t < 3; t++)
                {
                    pixels[(barY1 + t) * w + x] = stampBlue;
                    pixels[(barY2 + t) * w + x] = stampBlue;
                }
            }
        }

        DrawCircle(pixels, w, 85, h / 2, 12, stampBlue);
        DrawCircle(pixels, w, w - 85, h / 2, 12, stampBlue);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawCircle(Color[] pixels, int w, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < w && py >= 0 && py < pixels.Length / w)
                    {
                        pixels[py * w + px] = color;
                    }
                }
            }
        }
    }
}
