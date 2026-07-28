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
    [TextArea(3, 5)]
    public string riddleText = "Find the left, right, top, and bottom pieces of the clearance slip hidden in the room and beneath the stairs.";

    [Header("Appearance & Font Settings")]
    public float fontSize = 2.4f;
    public Color textChalkColor = new Color(0.95f, 0.95f, 0.92f, 1.0f);
    public Vector2 textContainerSize = new Vector2(3.5f, 1.1f);
    public float surfaceOffsetZ = -0.05f;

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
        SetupText();
    }

    [ContextMenu("Force Refresh Riddle Text")]
    public void SetupText()
    {
        if (!isPrimaryBlackboard) return;

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null) return;

        // Clean up any old duplicate text objects in scene
        CleanupOldTextObjects();

        GameObject textGo = GameObject.Find(TEXT_OBJECT_PREFIX);
        if (textGo == null)
        {
            textGo = new GameObject(TEXT_OBJECT_PREFIX);
        }

        // Align in World Space
        Vector3 center = meshRenderer.bounds.center;

        // Position slightly in front of board surface
        textGo.transform.position = new Vector3(center.x, center.y, center.z + surfaceOffsetZ);
        
        // Rotation (0, 0, 0) displays the front face of TextMeshPro (Non-Mirrored, Left-to-Right)
        textGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        textGo.transform.localScale = Vector3.one; // Clean 1.0 scale

        // Add or update TextMeshPro 3D
        TextMeshPro tmp = textGo.GetComponent<TextMeshPro>();
        if (tmp == null)
        {
            tmp = textGo.AddComponent<TextMeshPro>();
        }

        tmp.text = riddleText;
        tmp.fontSize = fontSize;
        tmp.color = textChalkColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;

        RectTransform rt = textGo.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = textContainerSize;
        }
    }

    private void CleanupOldTextObjects()
    {
        var allGo = Object.FindObjectsOfType<GameObject>();
        foreach (var go in allGo)
        {
            if (go.name.StartsWith("Blackboard_RiddleText_") || go.name == "Blackboard_AutoRiddleText" || go.name == "RiddleText")
            {
                DestroyImmediate(go);
            }
        }
    }
}
