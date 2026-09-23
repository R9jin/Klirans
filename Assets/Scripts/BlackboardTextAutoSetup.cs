using UnityEngine;
using TMPro;

/// <summary>
/// Automatically creates and aligns the riddle text on the single target Blackboard in World Space.
/// Prevents text mirror flipping, distortion, or duplication.
/// </summary>
[ExecuteAlways]
public class BlackboardTextAutoSetup : MonoBehaviour
{
    [Header("Riddle Text Content")]
    [TextArea(6, 12)]
    public string riddleText = 
        "Four edges torn, your exit denied,\n" +
        "Top, bottom, and side to side.\n\n" +
        "To make it whole and leave this place,\n" +
        "Search the shadows of this space.\n\n" +
        "When the room has given up its share,\n" +
        "Check the darkness beneath the stair.";

    [Header("Appearance & Font Settings")]
    public float fontSize = 1.05f;
    public FontStyles fontStyle = FontStyles.Bold;
    public Color textMarkerColor = new Color(0.10f, 0.10f, 0.12f, 1.0f);
    public Vector2 textContainerSize = new Vector2(2.8f, 1.2f);
    [Tooltip("Offset in front of board writing surface in meters to prevent Z-fighting without floating.")]
    public float surfaceOffsetZ = -0.0025f;

    [Header("Targeting")]
    [Tooltip("If true, only allows setup on this specific blackboard and removes duplicate text elsewhere.")]
    public bool isPrimaryBlackboard = true;

    private const string TEXT_OBJECT_PREFIX = "Blackboard_SingleRiddleText";

    private void Start()
    {
        SetupText();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying)
            {
                SetupText();
            }
        };
#endif
    }

    [ContextMenu("Force Refresh Riddle Text")]
    public void SetupText()
    {
        if (!isPrimaryBlackboard) return;

        // Target the inner board writing surface mesh rather than the outer frame
        MeshRenderer boardFaceRenderer = null;
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains(".001") || 
                r.gameObject.name.ToLower().Contains("glass") || 
                (r.sharedMaterial != null && r.sharedMaterial.name.Contains("TemperedGlass")))
            {
                boardFaceRenderer = r;
                break;
            }
        }
        if (boardFaceRenderer == null && renderers.Length > 1)
        {
            boardFaceRenderer = renderers[1];
        }
        else if (boardFaceRenderer == null && renderers.Length > 0)
        {
            boardFaceRenderer = renderers[0];
        }

        if (boardFaceRenderer == null) return;

        // Clean up any old duplicate text objects in scene
        CleanupOldTextObjects();

        GameObject textGo = GameObject.Find(TEXT_OBJECT_PREFIX);
        if (textGo == null)
        {
            textGo = new GameObject(TEXT_OBJECT_PREFIX);
        }

        // Parent under the furniture container to stay organized in the scene hierarchy
        if (transform.parent != null)
        {
            textGo.transform.SetParent(transform.parent, true);
        }

        // Align in World Space:
        // Use the board face's center X & Y, and front surface Z
        Vector3 center = boardFaceRenderer.bounds.center;
        float faceFrontZ = boardFaceRenderer.bounds.min.z;

        // Position flush on the board surface
        textGo.transform.position = new Vector3(center.x, center.y, faceFrontZ + surfaceOffsetZ);
        
        // Rotation (0, 0, 0) displays the front face of TextMeshPro directly facing into the room
        textGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        textGo.transform.localScale = Vector3.one;

        // Add or update TextMeshPro 3D
        TextMeshPro tmp = textGo.GetComponent<TextMeshPro>();
        if (tmp == null)
        {
            tmp = textGo.AddComponent<TextMeshPro>();
        }

        tmp.text = riddleText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = textMarkerColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Truncate;

        RectTransform rt = textGo.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = textContainerSize;
        }

        tmp.ForceMeshUpdate();
    }

    private void CleanupOldTextObjects()
    {
        var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allGo)
        {
            if (go.name.StartsWith("Blackboard_RiddleText_") || go.name == "Blackboard_AutoRiddleText" || go.name == "RiddleText")
            {
                DestroyImmediate(go);
            }
        }
    }
}
