using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DeployCashierPuzzle
{
    private const string PlayerAssetsDir = "Assets/PlayerAssets";
    private const string SoundDir = "Assets/Sounds";

    [MenuItem("Tools/Klirans/Deploy Cashier Balance Sheet Puzzle")]
    public static void Execute()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/Scenes/SampleScene.unity")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== DEPLOYING CASHIER BALANCE SHEET MATH PUZZLE ===");

        // ── 1. Generate Custom Textures ───────────────────────────────────────────
        if (!Directory.Exists(PlayerAssetsDir)) Directory.CreateDirectory(PlayerAssetsDir);

        string paperTexPath = $"{PlayerAssetsDir}/CashierPaperSheet.png";
        string stampTexPath = $"{PlayerAssetsDir}/PaidClearedStamp.png";
        string btnTexPath = $"{PlayerAssetsDir}/KeypadButton_Normal.png";
        string submitBtnTexPath = $"{PlayerAssetsDir}/KeypadButton_Submit.png";

        if (!File.Exists(paperTexPath))
        {
            Texture2D tex = BuildCashierLedgerTexture(1200, 900);
            File.WriteAllBytes(paperTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {paperTexPath}");
        }

        if (!File.Exists(stampTexPath))
        {
            Texture2D tex = BuildPaidStampTexture(512, 256);
            File.WriteAllBytes(stampTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {stampTexPath}");
        }

        if (!File.Exists(btnTexPath))
        {
            Texture2D tex = BuildKeypadButtonTexture(128, 128, new Color(0.92f, 0.88f, 0.82f), new Color(0.72f, 0.65f, 0.55f), new Color(0.35f, 0.28f, 0.22f));
            File.WriteAllBytes(btnTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {btnTexPath}");
        }

        if (!File.Exists(submitBtnTexPath))
        {
            Texture2D tex = BuildKeypadButtonTexture(128, 128, new Color(0.18f, 0.48f, 0.25f), new Color(0.12f, 0.35f, 0.18f), new Color(0.08f, 0.22f, 0.12f));
            File.WriteAllBytes(submitBtnTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"Generated {submitBtnTexPath}");
        }

        AssetDatabase.Refresh();

        ConfigureSpriteImporter(paperTexPath, 2048);
        ConfigureSpriteImporter(stampTexPath, 512);
        ConfigureSpriteImporter(btnTexPath, 128);
        ConfigureSpriteImporter(submitBtnTexPath, 128);

        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(paperTexPath);
        Sprite stampSprite = AssetDatabase.LoadAssetAtPath<Sprite>(stampTexPath);
        Sprite btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(btnTexPath);
        Sprite submitBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(submitBtnTexPath);

        // ── 2. Load Audio Clips ───────────────────────────────────────────────────
        AudioClip paperSlideAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/xeroxSoundEffect_Print.wav");
        if (paperSlideAudio == null) paperSlideAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/paper_scribble.wav");

        AudioClip keyClickAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/objective_chime.wav");
        AudioClip stampAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/xeroxSoundEffect.mp3");
        AudioClip errorAudio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/freesound_community-human_male_crazy-mumbles_1-30950.mp3");

        // Fonts
        Font horrorFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
        Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (standardFont == null) standardFont = horrorFont;

        // ── 3. Find HudCanvas & Hierarchy Setup ────────────────────────────────────
        GameObject hudCanvasGO = GameObject.Find("HudCanvas");
        if (hudCanvasGO == null)
        {
            Debug.LogError("[DeployCashierPuzzle] HudCanvas not found in scene!");
            return;
        }

        Transform existingMgr = hudCanvasGO.transform.Find("CashierPuzzleManager");
        GameObject mgrGO;
        if (existingMgr != null)
        {
            Object.DestroyImmediate(existingMgr.gameObject);
            log.AppendLine("Removed existing CashierPuzzleManager for clean rebuild.");
        }

        mgrGO = new GameObject("CashierPuzzleManager", typeof(RectTransform), typeof(CashierBalancePuzzle));
        mgrGO.transform.SetParent(hudCanvasGO.transform, false);

        RectTransform mgrRt = mgrGO.GetComponent<RectTransform>();
        mgrRt.anchorMin = Vector2.zero;
        mgrRt.anchorMax = Vector2.one;
        mgrRt.offsetMin = Vector2.zero;
        mgrRt.offsetMax = Vector2.zero;

        CashierBalancePuzzle puzzleComp = mgrGO.GetComponent<CashierBalancePuzzle>();

        // ── 4. Backdrop Overlay ───────────────────────────────────────────────────
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

        // ── 5. Cashier Puzzle Panel (Aged Ledger Paper) ───────────────────────────
        GameObject panelGO = new GameObject("CashierPuzzlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(mgrGO.transform, false);
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(1040f, 740f);

        Image panelImg = panelGO.GetComponent<Image>();
        if (paperSprite != null)
        {
            panelImg.sprite = paperSprite;
            panelImg.type = Image.Type.Simple;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        }

        CanvasGroup panelCg = panelGO.GetComponent<CanvasGroup>();
        panelGO.SetActive(false);

        // Subtle tilt
        panelRt.localRotation = Quaternion.Euler(0f, 0f, -0.6f);

        // ── 6. Header Section ─────────────────────────────────────────────────────
        GameObject headerTitleGO = new GameObject("HeaderTitle", typeof(RectTransform), typeof(Text), typeof(Shadow));
        headerTitleGO.transform.SetParent(panelGO.transform, false);
        RectTransform htRt = headerTitleGO.GetComponent<RectTransform>();
        htRt.anchorMin = new Vector2(0f, 1f);
        htRt.anchorMax = new Vector2(1f, 1f);
        htRt.pivot = new Vector2(0.5f, 1f);
        htRt.anchoredPosition = new Vector2(0f, -32f);
        htRt.sizeDelta = new Vector2(-100f, 56f);

        Text htText = headerTitleGO.GetComponent<Text>();
        htText.font = standardFont;
        htText.fontSize = 20;
        htText.fontStyle = FontStyle.Bold;
        htText.alignment = TextAnchor.MiddleCenter;
        htText.lineSpacing = 1.15f;
        htText.color = new Color(0.35f, 0.12f, 0.08f, 1f); // rich mahogany
        htText.text = "PAMANTASAN NG CABUYAO\nOFFICE OF THE UNIVERSITY CASHIER — STATEMENT OF ACCOUNT";

        Shadow htShadow = headerTitleGO.GetComponent<Shadow>();
        htShadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        htShadow.effectDistance = new Vector2(1f, -1f);

        // Window Slot Notice Banner
        GameObject noticeGO = new GameObject("WindowSlotNotice", typeof(RectTransform), typeof(Text));
        noticeGO.transform.SetParent(panelGO.transform, false);
        RectTransform notRt = noticeGO.GetComponent<RectTransform>();
        notRt.anchorMin = new Vector2(0f, 1f);
        notRt.anchorMax = new Vector2(1f, 1f);
        notRt.pivot = new Vector2(0.5f, 1f);
        notRt.anchoredPosition = new Vector2(0f, -92f);
        notRt.sizeDelta = new Vector2(-120f, 24f);

        Text notText = noticeGO.GetComponent<Text>();
        notText.font = standardFont;
        notText.fontSize = 13;
        notText.fontStyle = FontStyle.Italic;
        notText.alignment = TextAnchor.MiddleCenter;
        notText.color = new Color(0.48f, 0.38f, 0.28f, 1f);
        notText.text = "<i>(Assessment slip slid through Window 2 slot. Tally all pending line items below.)</i>";

        // Student Info Bar (Left: Student ID, Right: Assessment Period)
        GameObject studentIdGO = new GameObject("StudentIdText", typeof(RectTransform), typeof(Text));
        studentIdGO.transform.SetParent(panelGO.transform, false);
        RectTransform sidRt = studentIdGO.GetComponent<RectTransform>();
        sidRt.anchorMin = new Vector2(0f, 1f);
        sidRt.anchorMax = new Vector2(0.5f, 1f);
        sidRt.pivot = new Vector2(0f, 1f);
        sidRt.anchoredPosition = new Vector2(65f, -118f);
        sidRt.sizeDelta = new Vector2(380f, 22f);

        Text sidText = studentIdGO.GetComponent<Text>();
        sidText.font = standardFont;
        sidText.fontSize = 13;
        sidText.fontStyle = FontStyle.Bold;
        sidText.alignment = TextAnchor.MiddleLeft;
        sidText.color = new Color(0.25f, 0.20f, 0.16f, 1f);
        sidText.text = "STUDENT NO.: 2024-08912-CB";

        GameObject periodGO = new GameObject("AssessmentPeriodText", typeof(RectTransform), typeof(Text));
        periodGO.transform.SetParent(panelGO.transform, false);
        RectTransform perRt = periodGO.GetComponent<RectTransform>();
        perRt.anchorMin = new Vector2(0.5f, 1f);
        perRt.anchorMax = new Vector2(1f, 1f);
        perRt.pivot = new Vector2(1f, 1f);
        perRt.anchoredPosition = new Vector2(-65f, -118f);
        perRt.sizeDelta = new Vector2(380f, 22f);

        Text perText = periodGO.GetComponent<Text>();
        perText.font = standardFont;
        perText.fontSize = 13;
        perText.fontStyle = FontStyle.Bold;
        perText.alignment = TextAnchor.MiddleRight;
        perText.color = new Color(0.25f, 0.20f, 0.16f, 1f);
        perText.text = "1ST SEMESTER A.Y. 2024-2025";

        // ── 7. Table Header Row ───────────────────────────────────────────────────
        GameObject tableHeaderGO = new GameObject("TableHeaderRow", typeof(RectTransform), typeof(Image));
        tableHeaderGO.transform.SetParent(panelGO.transform, false);
        RectTransform thRt = tableHeaderGO.GetComponent<RectTransform>();
        thRt.anchorMin = new Vector2(0f, 1f);
        thRt.anchorMax = new Vector2(1f, 1f);
        thRt.pivot = new Vector2(0.5f, 1f);
        thRt.anchoredPosition = new Vector2(0f, -145f);
        thRt.sizeDelta = new Vector2(-120f, 28f);

        Image thImg = tableHeaderGO.GetComponent<Image>();
        thImg.color = new Color(0.38f, 0.25f, 0.16f, 0.92f); // dark mahogany header bar

        // Header column texts
        CreateTextChild(tableHeaderGO.transform, "Col_No", "#", standardFont, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 0f), new Vector2(0.08f, 1f), Vector2.zero);
        CreateTextChild(tableHeaderGO.transform, "Col_Desc", "UNSETTLED ASSESSMENT / FEE DESCRIPTION", standardFont, 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(0.10f, 0f), new Vector2(0.72f, 1f), Vector2.zero);
        CreateTextChild(tableHeaderGO.transform, "Col_Amt", "AMOUNT DUE (PHP)", standardFont, 13, FontStyle.Bold, TextAnchor.MiddleRight, Color.white, new Vector2(0.72f, 0f), new Vector2(0.96f, 1f), Vector2.zero);

        // ── 8. Line Items Container & Rows ────────────────────────────────────────
        GameObject lineItemsContainerGO = new GameObject("LineItemsContainer", typeof(RectTransform));
        lineItemsContainerGO.transform.SetParent(panelGO.transform, false);
        RectTransform licRt = lineItemsContainerGO.GetComponent<RectTransform>();
        licRt.anchorMin = new Vector2(0f, 1f);
        licRt.anchorMax = new Vector2(1f, 1f);
        licRt.pivot = new Vector2(0.5f, 1f);
        licRt.anchoredPosition = new Vector2(0f, -176f);
        licRt.sizeDelta = new Vector2(-120f, 175f);

        // Build 5 pre-baked line item rows
        for (int i = 0; i < 5; i++)
        {
            GameObject rowGO = new GameObject($"LineItemRow_{i + 1}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(lineItemsContainerGO.transform, false);
            RectTransform rowRt = rowGO.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -(i * 34f));
            rowRt.sizeDelta = new Vector2(0f, 32f);

            Image rowImg = rowGO.GetComponent<Image>();
            // Alternate subtle row shading
            rowImg.color = (i % 2 == 0) ? new Color(0.90f, 0.86f, 0.78f, 0.55f) : new Color(0.85f, 0.80f, 0.72f, 0.35f);

            CreateTextChild(rowGO.transform, "IndexText", $"{i + 1}.", standardFont, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.25f, 0.20f, 0.15f), new Vector2(0f, 0f), new Vector2(0.08f, 1f), Vector2.zero);
            CreateTextChild(rowGO.transform, "FeeNameText", "Fee Description Item Placeholder", standardFont, 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.15f, 0.12f, 0.10f), new Vector2(0.10f, 0f), new Vector2(0.72f, 1f), Vector2.zero);
            CreateTextChild(rowGO.transform, "AmountText", "₱ 000.00", standardFont, 15, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.20f, 0.15f, 0.10f), new Vector2(0.72f, 0f), new Vector2(0.96f, 1f), Vector2.zero);
        }

        // Horizontal accounting divider line
        GameObject dividerGO = new GameObject("AccountingDivider", typeof(RectTransform), typeof(Image));
        dividerGO.transform.SetParent(panelGO.transform, false);
        RectTransform divRt = dividerGO.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 1f);
        divRt.anchorMax = new Vector2(1f, 1f);
        divRt.pivot = new Vector2(0.5f, 1f);
        divRt.anchoredPosition = new Vector2(0f, -356f);
        divRt.sizeDelta = new Vector2(-120f, 3f);
        Image divImg = dividerGO.GetComponent<Image>();
        divImg.color = new Color(0.35f, 0.22f, 0.14f, 0.85f);

        // ── 9. Lower Section: Total Calculation (Left) & Numeric Keypad (Right) ───

        // Total Section Container
        GameObject totalSectionGO = new GameObject("TotalSection", typeof(RectTransform));
        totalSectionGO.transform.SetParent(panelGO.transform, false);
        RectTransform tsRt = totalSectionGO.GetComponent<RectTransform>();
        tsRt.anchorMin = new Vector2(0f, 0f);
        tsRt.anchorMax = new Vector2(0.52f, 0f);
        tsRt.pivot = new Vector2(0f, 0f);
        tsRt.anchoredPosition = new Vector2(60f, 40f);
        tsRt.sizeDelta = new Vector2(0f, 310f);

        // Total Label
        GameObject totalLabelGO = new GameObject("TotalLabel", typeof(RectTransform), typeof(Text));
        totalLabelGO.transform.SetParent(totalSectionGO.transform, false);
        RectTransform tlRt = totalLabelGO.GetComponent<RectTransform>();
        tlRt.anchorMin = new Vector2(0f, 1f);
        tlRt.anchorMax = new Vector2(1f, 1f);
        tlRt.pivot = new Vector2(0f, 1f);
        tlRt.anchoredPosition = new Vector2(0f, -10f);
        tlRt.sizeDelta = new Vector2(0f, 26f);

        Text tlText = totalLabelGO.GetComponent<Text>();
        tlText.font = standardFont;
        tlText.fontSize = 15;
        tlText.fontStyle = FontStyle.Bold;
        tlText.alignment = TextAnchor.MiddleLeft;
        tlText.color = new Color(0.38f, 0.22f, 0.15f);
        tlText.text = "TOTAL ASSESSMENT BALANCE DUE:";

        // Total Display Box (Ledger Box)
        GameObject totalBoxGO = new GameObject("TotalDisplayBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        totalBoxGO.transform.SetParent(totalSectionGO.transform, false);
        RectTransform tbRt = totalBoxGO.GetComponent<RectTransform>();
        tbRt.anchorMin = new Vector2(0f, 1f);
        tbRt.anchorMax = new Vector2(1f, 1f);
        tbRt.pivot = new Vector2(0f, 1f);
        tbRt.anchoredPosition = new Vector2(0f, -42f);
        tbRt.sizeDelta = new Vector2(-20f, 62f);

        Image tbImg = totalBoxGO.GetComponent<Image>();
        tbImg.color = new Color(0.98f, 0.96f, 0.92f, 0.95f); // clean ledger entry box

        Outline tbOutline = totalBoxGO.GetComponent<Outline>();
        tbOutline.effectColor = new Color(0.42f, 0.28f, 0.18f, 1f);
        tbOutline.effectDistance = new Vector2(2f, -2f);

        GameObject totalDisplayTextGO = new GameObject("TotalDisplayText", typeof(RectTransform), typeof(Text));
        totalDisplayTextGO.transform.SetParent(totalBoxGO.transform, false);
        RectTransform tdtRt = totalDisplayTextGO.GetComponent<RectTransform>();
        tdtRt.anchorMin = Vector2.zero;
        tdtRt.anchorMax = Vector2.one;
        tdtRt.offsetMin = new Vector2(15f, 0f);
        tdtRt.offsetMax = new Vector2(-15f, 0f);

        Text tdtText = totalDisplayTextGO.GetComponent<Text>();
        tdtText.font = standardFont;
        tdtText.fontSize = 28;
        tdtText.fontStyle = FontStyle.Bold;
        tdtText.alignment = TextAnchor.MiddleCenter;
        tdtText.color = new Color(0.15f, 0.12f, 0.10f);
        tdtText.text = "₱  <u>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</u> .00";

        // Instruction / Feedback text
        GameObject feedbackGO = new GameObject("FeedbackText", typeof(RectTransform), typeof(Text));
        feedbackGO.transform.SetParent(totalSectionGO.transform, false);
        RectTransform fbRt = feedbackGO.GetComponent<RectTransform>();
        fbRt.anchorMin = new Vector2(0f, 1f);
        fbRt.anchorMax = new Vector2(1f, 1f);
        fbRt.pivot = new Vector2(0f, 1f);
        fbRt.anchoredPosition = new Vector2(0f, -118f);
        fbRt.sizeDelta = new Vector2(-20f, 65f);

        Text fbText = feedbackGO.GetComponent<Text>();
        fbText.font = standardFont;
        fbText.fontSize = 13;
        fbText.fontStyle = FontStyle.Normal;
        fbText.alignment = TextAnchor.UpperLeft;
        fbText.lineSpacing = 1.2f;
        fbText.color = new Color(0.35f, 0.25f, 0.18f);
        fbText.text = "Calculate the total of all fees listed above. Enter amount on the keypad or your physical keyboard and press SUBMIT.";

        // University Seal watermark / stamp box placeholder
        GameObject sealBoxGO = new GameObject("CashierSealBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        sealBoxGO.transform.SetParent(totalSectionGO.transform, false);
        RectTransform sbRt = sealBoxGO.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0f, 0f);
        sbRt.anchorMax = new Vector2(1f, 0f);
        sbRt.pivot = new Vector2(0f, 0f);
        sbRt.anchoredPosition = new Vector2(0f, 15f);
        sbRt.sizeDelta = new Vector2(-20f, 95f);

        Image sbImg = sealBoxGO.GetComponent<Image>();
        sbImg.color = new Color(0.92f, 0.88f, 0.80f, 0.5f);

        Outline sbOutline = sealBoxGO.GetComponent<Outline>();
        sbOutline.effectColor = new Color(0.55f, 0.45f, 0.35f, 0.45f);
        sbOutline.effectDistance = new Vector2(1f, -1f);

        CreateTextChild(sealBoxGO.transform, "SealNote", "OFFICIAL VALIDATION SLOT\n(Stamp will be officially applied upon zero balance clearance)", standardFont, 11, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.48f, 0.38f, 0.28f, 0.8f), Vector2.zero, Vector2.one, Vector2.zero);

        // ── 10. Keypad Container (Right Side) ─────────────────────────────────────
        GameObject keypadGO = new GameObject("KeypadContainer", typeof(RectTransform));
        keypadGO.transform.SetParent(panelGO.transform, false);
        RectTransform kpRt = keypadGO.GetComponent<RectTransform>();
        kpRt.anchorMin = new Vector2(0.53f, 0f);
        kpRt.anchorMax = new Vector2(1f, 0f);
        kpRt.pivot = new Vector2(0f, 0f);
        kpRt.anchoredPosition = new Vector2(0f, 40f);
        kpRt.sizeDelta = new Vector2(-60f, 310f);

        // Keypad grid setup (3 columns x 4 rows for numbers + 1 submit bar)
        Button[] numButtons = new Button[10];
        Button btnClear = null;
        Button btnBack = null;
        Button btnSubmit = null;

        // Button dimensions
        float btnW = 125f;
        float btnH = 46f;
        float spacingX = 8f;
        float spacingY = 8f;
        float startX = 12f;
        float startY = 250f;

        // Layout numbers:
        // Row 1: 7, 8, 9
        // Row 2: 4, 5, 6
        // Row 3: 1, 2, 3
        // Row 4: C, 0, ⌫
        int[][] layout = new int[][]
        {
            new int[] { 7, 8, 9 },
            new int[] { 4, 5, 6 },
            new int[] { 1, 2, 3 }
        };

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                int val = layout[r][c];
                float x = startX + c * (btnW + spacingX);
                float y = startY - r * (btnH + spacingY);

                GameObject btnObj = CreateKeypadButton(keypadGO.transform, $"Btn_{val}", val.ToString(), btnSprite, standardFont, 20, FontStyle.Bold, new Color(0.18f, 0.15f, 0.12f), new Vector2(x, y), new Vector2(btnW, btnH));
                numButtons[val] = btnObj.GetComponent<Button>();
            }
        }

        // Row 4: Clear, 0, Backspace
        float row4Y = startY - 3 * (btnH + spacingY);

        // Clear button
        GameObject clearObj = CreateKeypadButton(keypadGO.transform, "Btn_Clear", "CLR", btnSprite, standardFont, 16, FontStyle.Bold, new Color(0.65f, 0.12f, 0.12f), new Vector2(startX, row4Y), new Vector2(btnW, btnH));
        btnClear = clearObj.GetComponent<Button>();

        // 0 button
        GameObject zeroObj = CreateKeypadButton(keypadGO.transform, "Btn_0", "0", btnSprite, standardFont, 20, FontStyle.Bold, new Color(0.18f, 0.15f, 0.12f), new Vector2(startX + (btnW + spacingX), row4Y), new Vector2(btnW, btnH));
        numButtons[0] = zeroObj.GetComponent<Button>();

        // Backspace button
        GameObject backObj = CreateKeypadButton(keypadGO.transform, "Btn_Backspace", "⌫ DEL", btnSprite, standardFont, 15, FontStyle.Bold, new Color(0.55f, 0.28f, 0.08f), new Vector2(startX + 2 * (btnW + spacingX), row4Y), new Vector2(btnW, btnH));
        btnBack = backObj.GetComponent<Button>();

        // Row 5: Large Submit / Confirm Payment Button
        float row5Y = startY - 4 * (btnH + spacingY) - 4f;
        float submitW = 3 * btnW + 2 * spacingX;
        GameObject submitObj = CreateKeypadButton(keypadGO.transform, "Btn_Submit", "CONFIRM & SUBMIT TOTAL", submitBtnSprite ?? btnSprite, standardFont, 16, FontStyle.Bold, Color.white, new Vector2(startX, row5Y), new Vector2(submitW, 50f));
        btnSubmit = submitObj.GetComponent<Button>();
        submitObj.GetComponent<Image>().color = new Color(0.15f, 0.48f, 0.22f); // deep emerald green

        // ── 11. Paid & Cleared Rubber Stamp Overlay ───────────────────────────────
        GameObject stampGO = new GameObject("PaidStampImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        stampGO.transform.SetParent(panelGO.transform, false);
        RectTransform stampRt = stampGO.GetComponent<RectTransform>();
        stampRt.anchorMin = new Vector2(0.5f, 0.5f);
        stampRt.anchorMax = new Vector2(0.5f, 0.5f);
        stampRt.pivot = new Vector2(0.5f, 0.5f);
        stampRt.anchoredPosition = new Vector2(-60f, -40f);
        stampRt.sizeDelta = new Vector2(460f, 220f);

        Image stampImg = stampGO.GetComponent<Image>();
        if (stampSprite != null)
        {
            stampImg.sprite = stampSprite;
            stampImg.type = Image.Type.Simple;
            stampImg.color = Color.white;
        }
        else
        {
            stampImg.color = new Color(0.7f, 0.1f, 0.1f, 0.85f);
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
        closeRt.anchoredPosition = new Vector2(-22f, -22f);
        closeRt.sizeDelta = new Vector2(36f, 36f);

        Image closeImg = closeBtnGO.GetComponent<Image>();
        closeImg.color = new Color(0.45f, 0.25f, 0.18f, 0.85f);

        Button closeBtn = closeBtnGO.GetComponent<Button>();

        CreateTextChild(closeBtnGO.transform, "CloseX", "✕", standardFont, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero);

        // ── 13. Wire up CashierBalancePuzzle References ───────────────────────────
        puzzleComp.backdropOverlay = backdropGO;
        puzzleComp.puzzlePanel = panelGO;
        puzzleComp.canvasGroup = panelCg;
        puzzleComp.headerTitleText = htText;
        puzzleComp.studentIdText = sidText;
        puzzleComp.assessmentPeriodText = perText;
        puzzleComp.windowSlotNoticeText = notText;
        puzzleComp.lineItemsContainer = lineItemsContainerGO.transform;
        puzzleComp.totalDisplayText = tdtText;
        puzzleComp.totalRowTransform = totalBoxGO.transform;
        puzzleComp.feedbackText = fbText;
        puzzleComp.numberButtons = numButtons;
        puzzleComp.clearButton = btnClear;
        puzzleComp.backspaceButton = btnBack;
        puzzleComp.submitButton = btnSubmit;
        puzzleComp.closeButton = closeBtn;
        puzzleComp.paidStampImage = stampImg;

        puzzleComp.paperSlideAudio = paperSlideAudio;
        puzzleComp.keyClickAudio = keyClickAudio;
        puzzleComp.stampAudio = stampAudio;
        puzzleComp.errorAudio = errorAudio;

        EditorUtility.SetDirty(puzzleComp);
        EditorUtility.SetDirty(mgrGO);

        // ── 14. Save Scene ────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        log.AppendLine("Successfully built and wired CashierBalancePuzzle UI under HudCanvas!");
        log.AppendLine("Scene saved successfully.");

        string outPath = Path.Combine(Application.dataPath, "CashierPuzzleOutput.txt");
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

    private static GameObject CreateKeypadButton(Transform parent, string name, string label, Sprite bgSprite, Font font, int fontSize, FontStyle style, Color textColor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnGO.GetComponent<Image>();
        if (bgSprite != null)
        {
            img.sprite = bgSprite;
            img.type = Image.Type.Sliced;
        }
        img.color = new Color(0.92f, 0.88f, 0.82f, 1f);

        Outline outline = btnGO.GetComponent<Outline>();
        outline.effectColor = new Color(0.38f, 0.28f, 0.20f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnGO.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.96f, 0.88f);
        cb.pressedColor = new Color(0.78f, 0.72f, 0.64f);
        btn.colors = cb;

        CreateTextChild(btnGO.transform, "BtnText", label, font, fontSize, style, TextAnchor.MiddleCenter, textColor, Vector2.zero, Vector2.one, Vector2.zero);

        return btnGO;
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
    /// Procedurally builds a high-res aged university accounting balance sheet paper texture.
    /// </summary>
    private static Texture2D BuildCashierLedgerTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        // Palette
        Color paperBase = new Color(0.94f, 0.90f, 0.82f, 1f); // warm aged parchment
        Color paperVignette = new Color(0.82f, 0.74f, 0.62f, 1f);
        Color ruledLineColor = new Color(0.78f, 0.72f, 0.64f, 0.45f);
        Color borderMaroon = new Color(0.38f, 0.16f, 0.10f, 1f);
        Color borderGold = new Color(0.68f, 0.52f, 0.32f, 0.75f);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;

                // Subtle Perlin noise fiber variation
                float noise = Mathf.PerlinNoise(nx * 8f, ny * 8f) * 0.08f + Mathf.PerlinNoise(nx * 32f, ny * 32f) * 0.04f;

                // Radial vignette for aged paper edge burn
                float dx = (nx - 0.5f) * 2f;
                float dy = (ny - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) * 0.7f;
                float vig = Mathf.Clamp01(dist * 0.65f);

                Color col = Color.Lerp(paperBase, paperVignette, vig) + new Color(noise, noise * 0.9f, noise * 0.7f, 0f);

                // Subtle watermark circle near center
                float cDist = Mathf.Sqrt((nx - 0.5f) * (nx - 0.5f) * 4f + (ny - 0.45f) * (ny - 0.45f) * 4f);
                if (cDist > 0.38f && cDist < 0.44f)
                {
                    col = Color.Lerp(col, new Color(0.75f, 0.68f, 0.58f, 1f), 0.22f);
                }

                // Faint horizontal accounting ledger ruling
                if (y > 220 && y < 650 && (y % 38 == 0 || y % 38 == 1))
                {
                    col = Color.Lerp(col, ruledLineColor, 0.6f);
                }

                pixels[y * w + x] = col;
            }
        }

        // Draw double outer border
        int bMargin = 20;
        int bWidth = 6;
        for (int x = bMargin; x < w - bMargin; x++)
        {
            for (int i = 0; i < bWidth; i++)
            {
                pixels[(bMargin + i) * w + x] = borderMaroon;
                pixels[(h - bMargin - 1 - i) * w + x] = borderMaroon;
            }
            // Inner thin line
            pixels[(bMargin + bWidth + 4) * w + x] = borderGold;
            pixels[(h - bMargin - bWidth - 5) * w + x] = borderGold;
        }

        for (int y = bMargin; y < h - bMargin; y++)
        {
            for (int i = 0; i < bWidth; i++)
            {
                pixels[y * w + (bMargin + i)] = borderMaroon;
                pixels[y * w + (w - bMargin - 1 - i)] = borderMaroon;
            }
            // Inner thin line
            pixels[y * w + (bMargin + bWidth + 4)] = borderGold;
            pixels[y * w + (w - bMargin - bWidth - 5)] = borderGold;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Procedurally builds the "PAID & CLEARED" red rubber stamp texture with distressed ink aesthetic.
    /// </summary>
    private static Texture2D BuildPaidStampTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Color inkRed = new Color(0.72f, 0.10f, 0.10f, 0.92f);
        Color inkLight = new Color(0.85f, 0.18f, 0.18f, 0.85f);

        int marginX = 24;
        int marginY = 18;
        int borderThick = 9;

        // Draw double rounded border with distressed noise
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
                    if (noise > 0.18f) // Distress specks
                    {
                        pixels[y * w + x] = (noise > 0.5f) ? inkRed : inkLight;
                    }
                }
            }
        }

        // Fill stamp body background with very faint translucent red ink haze
        for (int y = marginY + 20; y < h - marginY - 20; y++)
        {
            for (int x = marginX + 20; x < w - marginX - 20; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                if (n > 0.45f)
                {
                    pixels[y * w + x] = new Color(0.85f, 0.12f, 0.12f, (n - 0.45f) * 0.15f);
                }
            }
        }

        // Draw bold center block letter shapes for "PAID & CLEARED"
        DrawStampLettering(pixels, w, h, inkRed);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawStampLettering(Color[] pixels, int w, int h, Color inkColor)
    {
        // Draw decorative banner bars across the center
        int barY1 = h / 2 + 32;
        int barY2 = h / 2 - 32;

        for (int x = 60; x < w - 60; x++)
        {
            float n = Mathf.PerlinNoise(x * 0.18f, barY1 * 0.18f);
            if (n > 0.22f)
            {
                for (int t = 0; t < 3; t++)
                {
                    pixels[(barY1 + t) * w + x] = inkColor;
                    pixels[(barY2 + t) * w + x] = inkColor;
                }
            }
        }

        // Stars on sides (★)
        DrawSimpleStar(pixels, w, 85, h / 2, 14, inkColor);
        DrawSimpleStar(pixels, w, w - 85, h / 2, 14, inkColor);
    }

    private static void DrawSimpleStar(Color[] pixels, int w, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= radius || (Mathf.Abs(x) <= 2 && Mathf.Abs(y) <= radius) || (Mathf.Abs(y) <= 2 && Mathf.Abs(x) <= radius))
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

    /// <summary>
    /// Builds a beveled keypad button texture with 9-slice support.
    /// </summary>
    private static Texture2D BuildKeypadButtonTexture(int w, int h, Color fill, Color shadow, Color border)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isBorder = (x == 0 || x == w - 1 || y == 0 || y == h - 1);
                bool isTopBevel = (y >= h - 4 && x > 2 && x < w - 3);
                bool isBottomShadow = (y <= 5 && x > 2 && x < w - 3);

                if (isBorder)
                {
                    pixels[y * w + x] = border;
                }
                else if (isBottomShadow)
                {
                    pixels[y * w + x] = shadow;
                }
                else if (isTopBevel)
                {
                    pixels[y * w + x] = Color.Lerp(fill, Color.white, 0.35f);
                }
                else
                {
                    pixels[y * w + x] = fill;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
