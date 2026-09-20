using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupClearancePiecesHUD
{
    [MenuItem("Tools/Klirans/Setup Clearance Pieces HUD")]
    public static void Setup()
    {
        var hudCanvas = GameObject.Find("HudCanvas");
        if (hudCanvas == null)
        {
            Debug.LogError("HudCanvas not found in scene!");
            return;
        }

        // 1. Deactivate old full ClearanceSlip on HudCanvas
        var oldSlip = hudCanvas.transform.Find("ClearanceSlip");
        if (oldSlip != null)
        {
            oldSlip.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldSlip.gameObject);
        }

        // 2. Clear startingItems on InventoryManager
        var inv = Object.FindAnyObjectByType<InventoryManager>();
        if (inv != null)
        {
            inv.startingItems = new InventoryItem[0];
            EditorUtility.SetDirty(inv);
        }

        // 3. Load Fragment Sprites
        Sprite topSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Fragments/Top_Piece.png");
        Sprite bottomSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Fragments/Bottom_Piece.png");
        Sprite leftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Fragments/Left_Piece.png");
        Sprite rightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/Fragments/Right_Piece.png");

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 4. Create or find ClearancePiecesHUD GameObject
        Transform hudTrans = hudCanvas.transform.Find("ClearancePiecesHUD");
        GameObject hudGO;
        if (hudTrans == null)
        {
            hudGO = new GameObject("ClearancePiecesHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            hudGO.transform.SetParent(hudCanvas.transform, false);
        }
        else
        {
            hudGO = hudTrans.gameObject;
        }

        var rootRt = hudGO.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 0f);
        rootRt.anchorMax = new Vector2(0f, 0f);
        rootRt.pivot = new Vector2(0f, 0f);
        rootRt.anchoredPosition = new Vector2(30f, 30f);
        rootRt.sizeDelta = new Vector2(320f, 440f);

        // Background panel
        var bgImage = hudGO.GetComponent<Image>();
        if (bgImage == null) bgImage = hudGO.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);

        // Outline on background
        var bgOutline = hudGO.GetComponent<Outline>();
        if (bgOutline == null) bgOutline = hudGO.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.85f, 0.75f, 0.45f, 0.6f);
        bgOutline.effectDistance = new Vector2(2f, -2f);

        // Header Title Text
        Transform titleTrans = hudGO.transform.Find("TitleText");
        GameObject titleGO = titleTrans != null ? titleTrans.gameObject : new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(hudGO.transform, false);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);
        titleRt.sizeDelta = new Vector2(-20f, 28f);

        var titleTxt = titleGO.GetComponent<Text>();
        titleTxt.font = font;
        titleTxt.fontSize = 15;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = new Color(1f, 0.88f, 0.5f, 1f);
        titleTxt.text = "<b>CLEARANCE SLIP FRAGMENTS</b>";

        // Assembly Frame Container (holds the 4 pieces)
        Transform frameTrans = hudGO.transform.Find("PuzzleFrame");
        GameObject frameGO = frameTrans != null ? frameTrans.gameObject : new GameObject("PuzzleFrame", typeof(RectTransform), typeof(Image));
        frameGO.transform.SetParent(hudGO.transform, false);
        var frameRt = frameGO.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.pivot = new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = new Vector2(0f, 10f);
        frameRt.sizeDelta = new Vector2(230f, 298f); // 818 x 1059 aspect ratio

        var frameImg = frameGO.GetComponent<Image>();
        frameImg.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);

        // Helper to setup piece image
        System.Func<string, Sprite, Vector2, Vector2, Vector2, Vector2, Image> createPiece = (name, spr, aMin, aMax, offMin, offMax) =>
        {
            Transform pTrans = frameGO.transform.Find(name);
            GameObject pGO = pTrans != null ? pTrans.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            pGO.transform.SetParent(frameGO.transform, false);
            var prt = pGO.GetComponent<RectTransform>();
            prt.anchorMin = aMin;
            prt.anchorMax = aMax;
            prt.offsetMin = offMin;
            prt.offsetMax = offMax;
            var pImg = pGO.GetComponent<Image>();
            pImg.sprite = spr;
            pImg.preserveAspect = true;
            pImg.color = new Color(0.25f, 0.25f, 0.25f, 0.25f);
            return pImg;
        };

        // Top piece: top half
        Image topImg = createPiece("TopPiece", topSprite, new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        // Bottom piece: bottom half
        Image bottomImg = createPiece("BottomPiece", bottomSprite, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        // Left piece: left side overlay
        Image leftImg = createPiece("LeftPiece", leftSprite, new Vector2(0f, 0f), new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);
        // Right piece: right side overlay
        Image rightImg = createPiece("RightPiece", rightSprite, new Vector2(0.45f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // Status Message Text
        Transform statusTrans = hudGO.transform.Find("StatusMessageText");
        GameObject statusGO = statusTrans != null ? statusTrans.gameObject : new GameObject("StatusMessageText", typeof(RectTransform), typeof(Text));
        statusGO.transform.SetParent(hudGO.transform, false);
        var statusRt = statusGO.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 0f);
        statusRt.anchorMax = new Vector2(1f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.anchoredPosition = new Vector2(0f, 32f);
        statusRt.sizeDelta = new Vector2(-20f, 24f);

        var statusTxt = statusGO.GetComponent<Text>();
        statusTxt.font = font;
        statusTxt.fontSize = 13;
        statusTxt.alignment = TextAnchor.MiddleCenter;
        statusTxt.color = new Color(0.9f, 0.95f, 1f, 1f);
        statusTxt.text = "Pick up fragments to assemble slip";

        // Counter Text
        Transform counterTrans = hudGO.transform.Find("CounterText");
        GameObject counterGO = counterTrans != null ? counterTrans.gameObject : new GameObject("CounterText", typeof(RectTransform), typeof(Text));
        counterGO.transform.SetParent(hudGO.transform, false);
        var counterRt = counterGO.GetComponent<RectTransform>();
        counterRt.anchorMin = new Vector2(0f, 0f);
        counterRt.anchorMax = new Vector2(1f, 0f);
        counterRt.pivot = new Vector2(0.5f, 0f);
        counterRt.anchoredPosition = new Vector2(0f, 8f);
        counterRt.sizeDelta = new Vector2(-20f, 22f);

        var counterTxt = counterGO.GetComponent<Text>();
        counterTxt.font = font;
        counterTxt.fontSize = 13;
        counterTxt.alignment = TextAnchor.MiddleCenter;
        counterTxt.color = new Color(1f, 0.85f, 0.3f, 1f);
        counterTxt.text = "Fragments: 0 / 4";

        // Attach & configure ClearancePiecesHUD component
        var hudComp = hudGO.GetComponent<ClearancePiecesHUD>();
        if (hudComp == null) hudComp = hudGO.AddComponent<ClearancePiecesHUD>();
        hudComp.hudRoot = hudGO;
        hudComp.topPieceImage = topImg;
        hudComp.bottomPieceImage = bottomImg;
        hudComp.leftPieceImage = leftImg;
        hudComp.rightPieceImage = rightImg;
        hudComp.titleText = titleTxt;
        hudComp.statusMessageText = statusTxt;
        hudComp.counterText = counterTxt;
        hudComp.displayDuration = 5.0f;

        // Hide by default
        hudGO.SetActive(false);

        EditorUtility.SetDirty(hudGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("Clearance Pieces HUD setup successfully completed and scene saved!");
    }
}
