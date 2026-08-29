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

    // ── Runtime ────────────────────────────────────────────────────────────────
    private HashSet<int> _collectedSignatures = new HashSet<int>();

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _collectedSignatures.Clear();
        RefreshHeldPaperTexture();
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
        Debug.Log($"[ClearanceManager] Signature {signatureIndex + 1} collected! ({_collectedSignatures.Count}/6)");

        RefreshHeldPaperTexture();
        return true;
    }

    public bool IsFullyClear()          => _collectedSignatures.Count >= 6;
    public int  SignatureCount          => _collectedSignatures.Count;
    public bool HasSignature(int index) => _collectedSignatures.Contains(index);

    // ── Private ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Swaps the held paper's main texture to reflect the highest signature
    /// collected so far, giving a clear visual progression:
    /// blank → signed1 → signed2 → … → signed6.
    /// </summary>
    private void RefreshHeldPaperTexture()
    {
        if (heldPaperMaterial == null) return;

        int highest = -1;
        foreach (int idx in _collectedSignatures)
            if (idx > highest) highest = idx;

        Texture target = null;
        if (highest < 0)
            target = blankTexture;
        else if (signedTextures != null && highest < signedTextures.Length)
            target = signedTextures[highest];

        if (target != null)
            heldPaperMaterial.mainTexture = target;
        else
            Debug.LogWarning($"[ClearanceManager] No texture for signedTextures[{highest}].");
    }
}
