using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that tracks which clearance signatures the player has collected
/// and updates the HELD PAPER's texture to match the latest signed version.
///
/// HOW TO SET UP IN INSPECTOR:
/// 1. Keep this ClearanceManager GameObject in the scene (already set up).
/// 2. Signed Textures (size 6):
///      [0] = signed1 texture   [1] = signed2   [2] = signed3
///      [3] = signed4           [4] = signed5   [5] = signed6
/// 3. Blank Texture = Blank_paper texture (the starting state).
/// 4. Held Paper Material = the material on Held_Blank_Paper prefab
///    (named "Held_Blank_Paper_Mat").
///
/// When the player equips the Blank_Paper item the material's main texture
/// automatically reflects however many signatures have been collected.
/// </summary>
public class ClearanceManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static ClearanceManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Held Paper Material")]
    [Tooltip("The material on the Held_Blank_Paper prefab (Held_Blank_Paper_Mat). " +
             "This material's main texture will be swapped when signatures are collected.")]
    public Material heldPaperMaterial;

    [Header("Blank Texture (no signatures)")]
    [Tooltip("The Blank_paper texture — shown before any NPC has signed.")]
    public Texture blankTexture;

    [Header("Signed Textures (Index 0 = signed1 … Index 5 = signed6)")]
    [Tooltip("Assign the 6 signed textures in order.")]
    public Texture[] signedTextures = new Texture[6];

    [Header("HUD Clearance Slip (UI Image on HudCanvas)")]
    [Tooltip("UI Image for the on-screen clearance slip. Auto-found if unassigned.")]
    public UnityEngine.UI.Image clearanceSlipUI;

    [Tooltip("Blank sprite for the HUD slip (shown before any signatures).")]
    public Sprite blankSprite;

    [Tooltip("Signed sprites for the HUD slip (Index 0 = signed1 … Index 5 = signed6).")]
    public Sprite[] signedSprites = new Sprite[6];

    // ── Staff Quest Multi-Stage Progression ──────────────────────────────────
    [Header("Staff Quest Progression")]
    public bool GuidanceTapeDelivered = false;
    public bool RegistrarChasedAndCalmed = false;
    public bool CashierFeePaid = false;

    // ── Runtime ────────────────────────────────────────────────────────────────
    private HashSet<int> _collectedSignatures = new HashSet<int>();

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsurePlayerAssetsSlipTexturesLoaded();
    }

    private void EnsurePlayerAssetsSlipTexturesLoaded()
    {
#if UNITY_EDITOR
        // Always ensure signed0.png in Assets/PlayerAssets is the default complete slip texture after piecing fragments together
        Texture2D s0Tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PlayerAssets/signed0.png");
        if (s0Tex != null)
        {
            blankTexture = s0Tex;
            Sprite s0Sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/signed0.png");
            if (s0Sp == null)
                s0Sp = Sprite.Create(s0Tex, new Rect(0, 0, s0Tex.width, s0Tex.height), new Vector2(0.5f, 0.5f), 100f);
            blankSprite = s0Sp;
        }

        if (signedTextures == null || signedTextures.Length < 6)
            signedTextures = new Texture[6];
        if (signedSprites == null || signedSprites.Length < 6)
            signedSprites = new Sprite[6];

        for (int i = 0; i < 6; i++)
        {
            string path = $"Assets/PlayerAssets/signed{i + 1}.png";
            Texture2D sTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (sTex != null)
            {
                signedTextures[i] = sTex;
                Sprite sSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sSp == null)
                    sSp = Sprite.Create(sTex, new Rect(0, 0, sTex.width, sTex.height), new Vector2(0.5f, 0.5f), 100f);
                signedSprites[i] = sSp;
            }
        }
#endif
    }

    public Texture GetCurrentSlipTexture()
    {
        EnsurePlayerAssetsSlipTexturesLoaded();
        int highest = -1;
        foreach (int idx in _collectedSignatures)
            if (idx > highest) highest = idx;

        if (highest < 0)
            return blankTexture;
        if (signedTextures != null && highest < signedTextures.Length && signedTextures[highest] != null)
            return signedTextures[highest];
        return blankTexture;
    }

    public Sprite GetCurrentSlipSprite()
    {
        EnsurePlayerAssetsSlipTexturesLoaded();
        int highest = -1;
        foreach (int idx in _collectedSignatures)
            if (idx > highest) highest = idx;

        if (highest < 0)
        {
            if (blankSprite != null) return blankSprite;
            if (blankTexture is Texture2D bTex)
                return Sprite.Create(bTex, new Rect(0, 0, bTex.width, bTex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        else
        {
            if (signedSprites != null && highest < signedSprites.Length && signedSprites[highest] != null)
                return signedSprites[highest];
            if (signedTextures != null && highest < signedTextures.Length && signedTextures[highest] is Texture2D sTex)
                return Sprite.Create(sTex, new Rect(0, 0, sTex.width, sTex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return blankSprite;
    }

    private void Start()
    {
        _collectedSignatures.Clear();
        if (clearanceSlipUI == null)
        {
            var slipGO = GameObject.Find("ClearanceSlip");
            if (slipGO != null) clearanceSlipUI = slipGO.GetComponent<UnityEngine.UI.Image>();
        }

        // Hide clearance slip UI at game start — player must piece together fragments first
        if (clearanceSlipUI != null)
        {
            clearanceSlipUI.gameObject.SetActive(false);
        }

        RefreshHeldPaperTexture();
    }

    /// <summary>
    /// Toggle visibility of the HUD clearance slip UI.
    /// </summary>
    public void SetSlipVisible(bool visible)
    {
        if (clearanceSlipUI != null)
        {
            clearanceSlipUI.gameObject.SetActive(visible);
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ClearanceNPC when the player interacts.
    /// signatureIndex is 0-based (0 = signed1 … 5 = signed6).
    /// Returns true if this was a new signature.
    /// </summary>
    public bool GrantSignature(int signatureIndex)
    {
        if (signatureIndex < 0 || signatureIndex >= 6)
        {
            Debug.LogWarning($"[ClearanceManager] signatureIndex {signatureIndex} out of range.");
            return false;
        }

        if (_collectedSignatures.Contains(signatureIndex)) return false;

        _collectedSignatures.Add(signatureIndex);
        int total = _collectedSignatures.Count;
        Debug.Log($"[ClearanceManager] Signature {signatureIndex + 1} collected! ({total}/6)");

        RefreshHeldPaperTexture();
        OnSignatureCollected?.Invoke(signatureIndex, total);
        return true;
    }

    public bool IsFullyClear()          => _collectedSignatures.Count >= 6;
    public int  SignatureCount          => _collectedSignatures.Count;
    public bool HasSignature(int index) => _collectedSignatures.Contains(index);

    /// <summary>True when the player has returned to the Registrar with all 6 signatures and submitted their slip.</summary>
    public bool IsSlipSubmitted { get; private set; } = false;

    /// <summary>Fired when a new signature is collected (signatureIndex 0-5, totalCollected 1-6).</summary>
    public static event System.Action<int, int> OnSignatureCollected;

    /// <summary>Fired when the final clearance slip is officially submitted, triggering the endgame.</summary>
    public static event System.Action OnSlipSubmittedEvent;

    /// <summary>
    /// Called when the player hands the fully-signed clearance slip back to the University Registrar in Room 104.
    /// </summary>
    public bool SubmitSlipToRegistrar()
    {
        if (!IsFullyClear())
        {
            Debug.LogWarning("[ClearanceManager] Cannot submit slip before all 6 signatures are collected.");
            return false;
        }

        IsSlipSubmitted = true;
        Debug.Log("[ClearanceManager] Clearance slip officially submitted to Registrar! Campus main gate unlocked.");
        OnSlipSubmittedEvent?.Invoke();
        return true;
    }

    // ── Private ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Swaps the held paper's main texture and HUD slip sprite to reflect the highest
    /// signature collected so far, giving a clear visual progression:
    /// signed0 (default) → signed1 → signed2 → … → signed6.
    /// </summary>
    public void RefreshHeldPaperTexture()
    {
        Texture targetTex = GetCurrentSlipTexture();
        Sprite targetSprite = GetCurrentSlipSprite();

        // 1. Update Held 3D viewmodel paper material
        if (heldPaperMaterial != null && targetTex != null)
        {
            heldPaperMaterial.mainTexture = targetTex;
            if (heldPaperMaterial.HasProperty("_BaseMap"))
                heldPaperMaterial.SetTexture("_BaseMap", targetTex);
            if (heldPaperMaterial.HasProperty("_MainTex"))
                heldPaperMaterial.SetTexture("_MainTex", targetTex);
            if (heldPaperMaterial.HasProperty("_BaseColor"))
                heldPaperMaterial.SetColor("_BaseColor", Color.white);
            if (heldPaperMaterial.HasProperty("_Color"))
                heldPaperMaterial.SetColor("_Color", Color.white);
        }

        // 2. Update HUD on-screen clearance slip image
        if (clearanceSlipUI != null && targetSprite != null)
        {
            clearanceSlipUI.sprite = targetSprite;
        }

        // 3. Update Blank_Paper InventoryItem icon & any active held viewmodel
#if UNITY_EDITOR
        var blankPaperItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
        if (blankPaperItem != null && targetSprite != null)
        {
            blankPaperItem.icon = targetSprite;
        }
#endif
        if (InventoryEquipController.Instance != null)
        {
            InventoryEquipController.Instance.RefreshEquippedClearanceSlipTexture(targetTex);
        }
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.NotifyChanged();
        }
    }
}
