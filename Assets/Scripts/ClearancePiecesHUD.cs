using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays individual clearance slip fragments when collected in the world.
/// Shows progress as pieces are found, assembling them visually before combining
/// into the completed Blank_Paper clearance slip.
/// </summary>
public class ClearancePiecesHUD : MonoBehaviour
{
    public static ClearancePiecesHUD Instance { get; private set; }

    [Header("UI Hierarchy")]
    public GameObject hudRoot;
    public Image topPieceImage;
    public Image bottomPieceImage;
    public Image leftPieceImage;
    public Image rightPieceImage;

    public Text titleText;
    public Text counterText;
    public Text statusMessageText;

    [Header("Display Settings")]
    public float displayDuration = 5.0f;
    public float fadeSpeed = 3.0f;

    private HashSet<string> _collectedPieces = new HashSet<string>();
    private Coroutine _hideCoroutine;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (hudRoot == null) hudRoot = gameObject;
    }

    private void Start()
    {
        // Must remain hidden at game start until a piece is actually collected
        if (hudRoot != null) hudRoot.SetActive(false);
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        UpdatePieceVisuals();
    }

    /// <summary>
    /// Triggered whenever a clearance slip fragment is collected in the world.
    /// </summary>
    public void OnFragmentCollected(string pieceID, int currentCount, int totalRequired)
    {
        string normalizedID = NormalizePieceID(pieceID);
        _collectedPieces.Add(normalizedID);

        if (hudRoot != null) hudRoot.SetActive(true);
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        UpdatePieceVisuals();

        string pieceDisplayName = GetPieceDisplayName(normalizedID);
        if (titleText != null)
            titleText.text = "<b>CLEARANCE SLIP FRAGMENT</b>";

        if (statusMessageText != null)
            statusMessageText.text = $"Found <b>{pieceDisplayName}</b>!";

        if (counterText != null)
            counterText.text = $"Fragments: <b>{currentCount} / {totalRequired}</b>";

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideAfterDelay(displayDuration));
    }

    /// <summary>
    /// Triggered when all fragments are collected and combined into the clearance slip.
    /// </summary>
    public void OnPuzzleCompleted()
    {
        _collectedPieces.Add("TopPiece");
        _collectedPieces.Add("BottomPiece");
        _collectedPieces.Add("LeftPiece");
        _collectedPieces.Add("RightPiece");

        if (hudRoot != null) hudRoot.SetActive(true);
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        UpdatePieceVisuals();

        if (titleText != null)
            titleText.text = "<b>CLEARANCE SLIP COMPLETE!</b>";

        if (statusMessageText != null)
            statusMessageText.text = "<color=#98FB98>All 4 fragments assembled into Official Clearance Slip!</color>";

        if (counterText != null)
            counterText.text = "<b>Status: Ready for Department Signatures (LIBRARY on 3rd Floor First)</b>";

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideAfterDelay(6.0f));
    }

    private void UpdatePieceVisuals()
    {
        SetPieceState(topPieceImage, _collectedPieces.Contains("TopPiece"));
        SetPieceState(bottomPieceImage, _collectedPieces.Contains("BottomPiece"));
        SetPieceState(leftPieceImage, _collectedPieces.Contains("LeftPiece"));
        SetPieceState(rightPieceImage, _collectedPieces.Contains("RightPiece"));
    }

    private void SetPieceState(Image img, bool collected)
    {
        if (img == null) return;
        img.gameObject.SetActive(true);
        if (collected)
        {
            img.color = new Color(1f, 1f, 1f, 1f);
        }
        else
        {
            // Faint translucent silhouette to indicate missing puzzle piece
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.22f);
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_canvasGroup != null)
        {
            while (_canvasGroup.alpha > 0.01f)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        if (hudRoot != null) hudRoot.SetActive(false);
        _hideCoroutine = null;
    }

    private string NormalizePieceID(string id)
    {
        string l = id.ToLower();
        if (l.Contains("top")) return "TopPiece";
        if (l.Contains("bottom")) return "BottomPiece";
        if (l.Contains("left")) return "LeftPiece";
        if (l.Contains("right")) return "RightPiece";
        return id;
    }

    private string GetPieceDisplayName(string normalizedID)
    {
        switch (normalizedID)
        {
            case "TopPiece": return "Top Header Piece";
            case "BottomPiece": return "Bottom Footer Piece";
            case "LeftPiece": return "Left Margin Piece";
            case "RightPiece": return "Right Department Piece";
            default: return normalizedID;
        }
    }
}
