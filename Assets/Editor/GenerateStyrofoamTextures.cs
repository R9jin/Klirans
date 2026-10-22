using UnityEngine;
using UnityEditor;
using System.IO;

public static class GenerateStyrofoamTextures
{
    [MenuItem("Tools/Klirans/Generate Realistic Styrofoam Textures")]
    public static void Generate()
    {
        Debug.Log("=== GENERATING REALISTIC STYROFOAM & STICKER TEXTURES ===");

        if (!Directory.Exists("Assets/Textures"))
        {
            Directory.CreateDirectory("Assets/Textures");
        }

        // 1. Generate Authentic Styrofoam Foam Bead Texture
        GenerateStyrofoamBeadTexture();

        // 2. Generate Polar-Style Printed Sticker
        GenerateStickerTexture();

        // 3. Generate Masking Tape Price Tags
        GeneratePriceTags();

        AssetDatabase.Refresh();
        Debug.Log("=== STYROFOAM TEXTURES GENERATED SUCCESSFULLY ===");
    }

    private static void GenerateStyrofoamBeadTexture()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color baseWhite = new Color(0.95f, 0.95f, 0.93f, 1f);

        // Fill with base styrofoam color
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.04f - 0.02f;
                Color c = baseWhite + new Color(noise, noise, noise, 0f);
                tex.SetPixel(x, y, c);
            }
        }

        // Draw hundreds of interlocking expanded polystyrene (EPS) foam beads
        Random.InitState(42);
        for (int i = 0; i < 450; i++)
        {
            int cx = Random.Range(0, size);
            int cy = Random.Range(0, size);
            int radius = Random.Range(4, 9);
            float shade = Random.Range(-0.06f, 0.06f);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                    {
                        int px = (cx + dx + size) % size;
                        int py = (cy + dy + size) % size;

                        // Bead rim shading for 3D cellular styrofoam look
                        float rim = dist / radius;
                        float rimShadow = Mathf.Pow(rim, 2.5f) * -0.05f;
                        float beadColor = shade + rimShadow;

                        Color existing = tex.GetPixel(px, py);
                        Color blended = new Color(
                            Mathf.Clamp01(existing.r + beadColor),
                            Mathf.Clamp01(existing.g + beadColor),
                            Mathf.Clamp01(existing.b + beadColor),
                            1f
                        );
                        tex.SetPixel(px, py, blended);
                    }
                }
            }
        }

        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        string path = "Assets/Textures/Styrofoam_Texture.png";
        File.WriteAllBytes(path, bytes);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log($"[Styrofoam] Saved foam texture to {path}");
    }

    private static void GenerateStickerTexture()
    {
        int width = 512;
        int height = 256;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color white = new Color(0.97f, 0.97f, 0.96f, 1f);
        Color redBorder = new Color(0.82f, 0.12f, 0.12f, 1f);
        Color redFill = new Color(0.78f, 0.10f, 0.10f, 1f);
        Color navyBlue = new Color(0.10f, 0.20f, 0.45f, 1f);
        Color darkGray = new Color(0.18f, 0.18f, 0.18f, 1f);

        // 1. Fill base white sticker paper
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, y, white);
            }
        }

        // 2. Outer Red Border (thickness ~7px)
        int borderThickness = 7;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < borderThickness || x >= width - borderThickness ||
                    y < borderThickness || y >= height - borderThickness)
                {
                    tex.SetPixel(x, y, redBorder);
                }
            }
        }

        // 3. Inner thin red pinstripe (gap 4px, width 2px)
        int pinMargin = 13;
        for (int y = pinMargin; y < height - pinMargin; y++)
        {
            for (int x = pinMargin; x < width - pinMargin; x++)
            {
                if (x == pinMargin || x == width - pinMargin - 1 ||
                    y == pinMargin || y == height - pinMargin - 1 ||
                    x == pinMargin + 1 || x == width - pinMargin - 2 ||
                    y == pinMargin + 1 || y == height - pinMargin - 2)
                {
                    tex.SetPixel(x, y, redBorder);
                }
            }
        }

        // 4. Top Header Red Banner Bar (like POLAR ICE CHEST banner)
        int bannerYStart = height - 60;
        int bannerYEnd = height - 20;
        for (int y = bannerYStart; y <= bannerYEnd; y++)
        {
            for (int x = 20; x < width - 20; x++)
            {
                tex.SetPixel(x, y, redFill);
            }
        }

        // 5. Draw Barcode in bottom right corner (X: 380 to 490, Y: 22 to 62)
        Random.InitState(101);
        int barX = 380;
        while (barX < 490)
        {
            int barW = Random.Range(1, 4);
            bool isBlack = Random.value > 0.4f;
            Color barCol = isBlack ? darkGray : white;

            for (int bx = 0; bx < barW && (barX + bx) < 490; bx++)
            {
                for (int by = 22; by <= 62; by++)
                {
                    tex.SetPixel(barX + bx, by, barCol);
                }
            }
            barX += barW;
        }

        // 6. Draw Polar Ice Chest styled emblem box on left (X: 28 to 90, Y: 22 to 64)
        for (int y = 22; y <= 64; y++)
        {
            for (int x = 28; x <= 90; x++)
            {
                if (x == 28 || x == 90 || y == 22 || y == 64 ||
                    x == 29 || x == 89 || y == 23 || y == 63)
                {
                    tex.SetPixel(x, y, redBorder);
                }
            }
        }

        tex.Apply();

        // Now render clean text on top using a temporary Render Texture & Canvas
        RenderTextOntoSticker(tex);

        byte[] finalBytes = tex.EncodeToPNG();
        string path = "Assets/Textures/HamAndCheese_Sticker.png";
        File.WriteAllBytes(path, finalBytes);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log($"[Styrofoam] Saved printed sticker texture to {path}");
    }

    private static void RenderTextOntoSticker(Texture2D baseTex)
    {
        int w = baseTex.width;
        int h = baseTex.height;

        RenderTexture rt = new RenderTexture(w, h, 24);
        RenderTexture.active = rt;

        // Draw base texture
        Graphics.Blit(baseTex, rt);

        // Create temporary UI tree
        var rootGO = new GameObject("TempStickerCanvas");
        var canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var camGO = new GameObject("TempCam");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Nothing;
        cam.orthographic = true;
        cam.orthographicSize = h * 0.5f;
        cam.targetTexture = rt;
        canvas.worldCamera = cam;

        var canvasRT = rootGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(w, h);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (customFont != null) font = customFont;

        // 1. Top Banner Text: "POLAR ICE CHEST • FRESH & TOASTED"
        CreateText(rootGO, "Header", new Vector2(0f, 85f), new Vector2(460f, 32f),
            "POLAR ICE CHEST • FRESH & TOASTED", font, 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        // 2. Main Title: "HAM & CHEESE"
        CreateText(rootGO, "TitleShadow", new Vector2(2f, 22f), new Vector2(460f, 64f),
            "HAM & CHEESE", font, 48, FontStyle.Bold, new Color(0.3f, 0.05f, 0.05f, 0.8f), TextAnchor.MiddleCenter);
        CreateText(rootGO, "Title", new Vector2(0f, 24f), new Vector2(460f, 64f),
            "HAM & CHEESE", font, 48, FontStyle.Bold, new Color(0.85f, 0.10f, 0.10f, 1f), TextAnchor.MiddleCenter);

        // 3. Subtitle: "TOASTED SPECIAL SANDWICH"
        CreateText(rootGO, "Subtitle", new Vector2(0f, -24f), new Vector2(460f, 28f),
            "TOASTED SPECIAL SANDWICH", font, 18, FontStyle.Bold, new Color(0.12f, 0.22f, 0.48f, 1f), TextAnchor.MiddleCenter);

        // 4. Tagline: "Pampalubag-Loob sa Clearance • Masarap & Mainit"
        CreateText(rootGO, "Tagline", new Vector2(0f, -52f), new Vector2(460f, 22f),
            "Pampalubag-Loob sa Clearance • Masarap & Mainit", font, 14, FontStyle.Italic, new Color(0.25f, 0.25f, 0.25f, 1f), TextAnchor.MiddleCenter);

        // 5. Left badge text: "MEDIUM\nSIZE"
        CreateText(rootGO, "SizeBadge", new Vector2(-196f, -86f), new Vector2(60f, 38f),
            "MED\nSIZE", font, 11, FontStyle.Bold, new Color(0.80f, 0.10f, 0.10f, 1f), TextAnchor.MiddleCenter);

        // 6. Right barcode label: "GENUINE 042918"
        CreateText(rootGO, "BarcodeLabel", new Vector2(178f, -94f), new Vector2(100f, 16f),
            "* 0 4 2 9 1 8 *", font, 10, FontStyle.Normal, new Color(0.3f, 0.3f, 0.3f, 1f), TextAnchor.MiddleCenter);

        // Force Canvas to update
        Canvas.ForceUpdateCanvases();
        cam.Render();

        // Read pixels back into baseTex
        baseTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        baseTex.Apply();

        RenderTexture.active = null;
        Object.DestroyImmediate(rootGO);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(rt);
    }

    private static void CreateText(GameObject parent, string name, Vector2 pos, Vector2 size, string text, Font font, int sizePt, FontStyle style, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var t = go.GetComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.font = font;
        t.fontSize = sizePt;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
    }

    private static void GeneratePriceTags()
    {
        GenerateSinglePriceTag("₱3", "Assets/Textures/PriceTag_3.png", new Color(0.08f, 0.20f, 0.65f, 1f));
        GenerateSinglePriceTag("₱5", "Assets/Textures/PriceTag_5.png", new Color(0.08f, 0.20f, 0.65f, 1f));
        GenerateSinglePriceTag("₱8", "Assets/Textures/PriceTag_8.png", new Color(0.08f, 0.20f, 0.65f, 1f));
        GenerateSinglePriceTag("₱12", "Assets/Textures/PriceTag_12.png", new Color(0.08f, 0.20f, 0.65f, 1f));
        GenerateSinglePriceTag("UBOS NA", "Assets/Textures/PriceTag_SoldOut.png", new Color(0.85f, 0.10f, 0.10f, 1f));
    }

    private static void GenerateSinglePriceTag(string text, string path, Color textColor)
    {
        int w = 128;
        int h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        Color tapeColor = new Color(0.93f, 0.89f, 0.76f, 1f); // Masking tape paper beige
        Color edgeColor = new Color(0.85f, 0.80f, 0.65f, 1f);

        // Fill with tape texture and ragged edges
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Ragged left/right torn edges
                if (x < 3 || x >= w - 3 || y < 2 || y >= h - 2)
                {
                    tex.SetPixel(x, y, edgeColor);
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.05f - 0.025f;
                    tex.SetPixel(x, y, tapeColor + new Color(noise, noise, noise, 0f));
                }
            }
        }

        tex.Apply();

        // Render text on tape
        RenderTextOntoTape(tex, text, textColor);

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log($"[Styrofoam] Saved price tag to {path}");
    }

    private static void RenderTextOntoTape(Texture2D baseTex, string text, Color textColor)
    {
        int w = baseTex.width;
        int h = baseTex.height;

        RenderTexture rt = new RenderTexture(w, h, 24);
        RenderTexture.active = rt;

        Graphics.Blit(baseTex, rt);

        var rootGO = new GameObject("TempTapeCanvas");
        var canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var camGO = new GameObject("TempTapeCam");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Nothing;
        cam.orthographic = true;
        cam.orthographicSize = h * 0.5f;
        cam.targetTexture = rt;
        canvas.worldCamera = cam;

        var canvasRT = rootGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(w, h);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (customFont != null) font = customFont;

        int fontSize = text.Length > 4 ? 20 : 34;
        CreateText(rootGO, "PriceTagText", Vector2.zero, new Vector2(w, h), text, font, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);

        Canvas.ForceUpdateCanvases();
        cam.Render();

        baseTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        baseTex.Apply();

        RenderTexture.active = null;
        Object.DestroyImmediate(rootGO);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(rt);
    }
}
