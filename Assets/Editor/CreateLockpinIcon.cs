using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility that programmatically generates a Lockpin inventory icon
/// matching the dark metal lockpick prefab shape, then assigns it to Lockpin.asset.
///
/// Run via:  Klirans > Setup > Generate Lockpin Icon
/// </summary>
public static class CreateLockpinIcon
{
    private const string IconDir   = "Assets/Items/Icons";
    private const string IconPath  = IconDir + "/Lockpin_Icon.png";
    private const string AssetPath = "Assets/Items/Lockpin.asset";

    [MenuItem("Klirans/Setup/Generate Lockpin Icon")]
    public static void GenerateLockpinIcon()
    {
        // ── 1. Ensure output directory exists ────────────────────────
        if (!AssetDatabase.IsValidFolder(IconDir))
            AssetDatabase.CreateFolder("Assets/Items", "Icons");

        // ── 2. Generate the icon texture ─────────────────────────────
        Texture2D tex = BuildLockpinTexture(96, 96);
        byte[] png    = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(Path.GetFullPath(IconPath), png);
        AssetDatabase.Refresh();

        // ── 3. Set import settings: Sprite, no mipmaps, point filter ─
        TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType          = TextureImporterType.Sprite;
            importer.spriteImportMode     = SpriteImportMode.Single;
            importer.alphaIsTransparency  = true;
            importer.filterMode           = FilterMode.Bilinear;
            importer.mipmapEnabled        = false;
            importer.maxTextureSize       = 128;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        // ── 4. Assign sprite to Lockpin.asset ────────────────────────
        Sprite        sprite      = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        InventoryItem lockpinItem = AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetPath);

        if (sprite == null)
        {
            Debug.LogError("[CreateLockpinIcon] Failed to load generated sprite at: " + IconPath);
            return;
        }
        if (lockpinItem == null)
        {
            Debug.LogError("[CreateLockpinIcon] Lockpin.asset not found at: " + AssetPath);
            return;
        }

        lockpinItem.icon = sprite;
        EditorUtility.SetDirty(lockpinItem);
        AssetDatabase.SaveAssets();

        Debug.Log("[CreateLockpinIcon] ✔ Lockpin icon generated and assigned to Lockpin.asset!");
        EditorUtility.DisplayDialog(
            "Lockpin Icon Created",
            "Lockpin_Icon.png has been generated in Assets/Items/Icons/\nand assigned to the Lockpin inventory item.",
            "OK");
    }

    // ─── Texture builder ─────────────────────────────────────────────

    /// <summary>
    /// Draws a top-down lockpick silhouette on a transparent 96x96 canvas.
    /// Resembles the LockpinPrefab: dark cylindrical handle + thin steel shank
    /// with a small angled pick tip on the right end.
    /// </summary>
    private static Texture2D BuildLockpinTexture(int w, int h)
    {
        Texture2D tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels   = new Color[w * h];

        // Start fully transparent
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        // Palette matching the dark-metal prefab ──────────────────────
        Color bgClear      = Color.clear;
        Color handleDark   = new Color(0.12f, 0.12f, 0.14f, 1f); // near-black steel handle
        Color handleMid    = new Color(0.20f, 0.20f, 0.23f, 1f); // body shadow
        Color handleLight  = new Color(0.30f, 0.30f, 0.34f, 1f); // handle highlight ridge
        Color steelDark    = new Color(0.40f, 0.40f, 0.42f, 1f); // shank shadow
        Color steelMid     = new Color(0.62f, 0.62f, 0.60f, 1f); // shank body
        Color steelLight   = new Color(0.82f, 0.82f, 0.80f, 1f); // shank highlight

        int midY = h / 2; // 48

        // ── Helper: set pixel safely ─────────────────────────────────
        void Pix(int x, int y, Color c)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
                pixels[y * w + x] = c;
        }

        // Filled box helper (inclusive)
        void Box(int x0, int y0, int x1, int y1, Color c)
        {
            for (int bx = x0; bx <= x1; bx++)
                for (int by = y0; by <= y1; by++)
                    Pix(bx, by, c);
        }

        // ── Handle section (left side, x: 6-30, y: 38-58) ───────────
        // Outer shape
        Box(6, 38, 30, 58, handleMid);

        // Rounded corners (2px trim)
        Pix(6, 38, bgClear); Pix(7, 38, bgClear);
        Pix(6, 58, bgClear); Pix(7, 58, bgClear);
        Pix(6, 39, bgClear);
        Pix(6, 57, bgClear);

        // Top highlight band
        for (int x = 8; x <= 30; x++) Pix(x, 58, handleLight);

        // Bottom shadow band
        for (int x = 8; x <= 30; x++) Pix(x, 38, handleDark);

        // Grip texture: vertical grooves
        for (int g = 0; g < 5; g++)
        {
            int gx = 10 + g * 4;
            for (int gy = 40; gy <= 56; gy++)
                Pix(gx, gy, handleDark);
        }

        // ── Collar / transition (x: 30-38) ───────────────────────────
        Box(30, 43, 38, 53, handleMid);
        for (int x = 30; x <= 38; x++) { Pix(x, 53, handleLight); Pix(x, 43, handleDark); }

        // ── Shank / body (x: 38-78, y: 45-51) ────────────────────────
        Box(38, 45, 78, 51, steelMid);
        // Top highlight
        for (int x = 38; x <= 78; x++) Pix(x, 51, steelLight);
        // Bottom shadow
        for (int x = 38; x <= 78; x++) Pix(x, 45, steelDark);

        // ── Pick tip / angled end (x: 78-90) ─────────────────────────
        // Taper from 7px tall down to 3px, then a small angled hook
        for (int x = 78; x <= 84; x++)
        {
            float t = (x - 78) / 6f;
            int halfH = (int)Mathf.Lerp(3, 1, t);
            for (int y = midY - halfH; y <= midY + halfH; y++)
                Pix(x, y, steelMid);
            // Highlight
            Pix(x, midY + halfH, steelLight);
        }
        // Tip pixel
        Pix(85, midY, steelMid);
        Pix(85, midY + 1, steelLight);

        // Small upward hook at the very tip (matches the lockpick pick end)
        for (int y = midY + 1; y <= midY + 4; y++) Pix(84, y, steelMid);
        Pix(83, midY + 4, steelMid);
        Pix(82, midY + 4, steelLight);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
