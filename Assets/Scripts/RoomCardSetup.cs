using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates and positions professional room identification cards directly above each door,
/// mounted flush to the wall surface -- matching the "ROOM 309" reference style.
/// Card style: white rectangle, thin black border, green bold text.
/// Run via Awake or right-click > Regenerate Room Cards.
/// </summary>
public class RoomCardSetup : MonoBehaviour
{
    [Header("Card Dimensions")]
    public float cardWidth  = 0.55f;
    public float cardHeight = 0.13f;
    public float cardDepth  = 0.012f;

    [Header("Placement")]
    public float gapAboveDoor = 0.04f;
    public float wallOffset = 0.07f;

    [Header("Colors")]
    public Color cardFaceColor = Color.white;
    public Color borderColor   = new Color(0.05f, 0.05f, 0.05f, 1f);
    public Color textColor     = new Color(0.10f, 0.65f, 0.15f, 1f);

    [Header("Text")]
    public float fontSizeMin = 0.3f;
    public float fontSizeMax = 1.2f;

    // Cards are static scene objects — no runtime regeneration needed.
    // Use right-click → "Regenerate Room Cards" or Tools → Room Cards menu in the Editor.

    [ContextMenu("Regenerate Room Cards")]
    public void RegenerateCards()
    {
        RemoveOldSigns();
        CreateNewCards();
    }

    // ----------------------------------------------------------------
    // Remove all previous plaque / sign objects
    // ----------------------------------------------------------------
    private void RemoveOldSigns()
    {
        string[] roots = { "RoomNumbersContainer", "FloorPlaques", "RoomCardsContainer" };
        foreach (var r in roots)
        {
            var go = GameObject.Find(r);
            if (go != null) SafeDestroy(go);
        }

        // Kill Plaque_XXX objects that live as children of door GameObjects
        var allGOs = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            if (go.name.StartsWith("Plaque_") || go.name.StartsWith("RoomCard_"))
                SafeDestroy(go);
        }
    }

    // ----------------------------------------------------------------
    // Create new cards above every numbered door
    // ----------------------------------------------------------------
    private void CreateNewCards()
    {
        var doors = Object.FindObjectsByType<DoorInteract>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning("[RoomCardSetup] No DoorInteract objects found.");
            return;
        }

        var container = new GameObject("RoomCardsContainer");
        int count = 0;

        foreach (var doorInteract in doors)
        {
            if (doorInteract == null) continue;
            var door = doorInteract.gameObject;

            string roomNum = ParseRoomNumber(door.name);
            if (string.IsNullOrEmpty(roomNum)) continue;

            Bounds bounds = GetDoorBounds(door);

            // Determine which wall the door is on.
            // Left-wall doors have X ~ -86; right-wall doors have X ~ -83.
            bool isLeftWall = (door.transform.position.x < -84.5f);

            // Y: place card bottom just above door top + gap
            float cardCentreY = bounds.max.y + gapAboveDoor + cardHeight * 0.5f;
            float cardCentreZ = bounds.center.z;
            float cardCentreX;
            Quaternion cardRotation;

            if (isLeftWall)
            {
                // Left wall: hallway-facing side is bounds.MAX.x. Card sits in front of that
                // surface and faces -X (rot 270) so the player walking in the hallway reads it.
                cardCentreX = bounds.max.x + wallOffset;
                cardRotation = Quaternion.Euler(0f, 270f, 0f);
            }
            else
            {
                // Right wall: hallway-facing side is bounds.MIN.x. Card sits in front of that
                // surface and faces +X (rot 90) so the player walking in the hallway reads it.
                cardCentreX = bounds.min.x - wallOffset;
                cardRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            Vector3 cardPos = new Vector3(cardCentreX, cardCentreY, cardCentreZ);
            BuildCard(container.transform, door.name, roomNum, cardPos, cardRotation);
            count++;
        }

        Debug.Log("[RoomCardSetup] Created " + count + " room cards.");
    }

    // ----------------------------------------------------------------
    // Build one room card: border quad + white face quad + TMP label
    // ----------------------------------------------------------------
    private void BuildCard(Transform parent, string doorName, string roomNum,
                           Vector3 position, Quaternion rotation)
    {
        var cardRoot = new GameObject("RoomCard_" + doorName);
        cardRoot.transform.SetParent(parent, false);
        cardRoot.transform.SetPositionAndRotation(position, rotation);

        float bt = 0.012f; // border thickness

        // Black border (slightly larger quad behind the white face)
        MakeQuad("Border", cardRoot.transform,
            new Vector3(0f, 0f,  0.000f),
            cardWidth + bt * 2f, cardHeight + bt * 2f, borderColor);

        // White face
        MakeQuad("Face", cardRoot.transform,
            new Vector3(0f, 0f, -0.001f),
            cardWidth, cardHeight, cardFaceColor);

        // Text label
        var textObj = new GameObject("Label");
        textObj.transform.SetParent(cardRoot.transform, false);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        textObj.transform.localRotation = Quaternion.identity;

        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text             = "ROOM " + roomNum;
        tmp.color            = textColor;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = fontSizeMin;
        tmp.fontSizeMax      = fontSizeMax;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(cardWidth - 0.03f, cardHeight - 0.02f);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private void MakeQuad(string goName, Transform parent,
                          Vector3 localPos, float w, float h, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = new Vector3(w, h, 1f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildQuadMesh();

        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(FindUnlitShader());
        mat.color = color;
        mr.sharedMaterial = mat;
    }

    private static Mesh _sharedQuad;
    private static Mesh BuildQuadMesh()
    {
        if (_sharedQuad != null) return _sharedQuad;
        _sharedQuad = new Mesh { name = "RoomCardQuad" };
        _sharedQuad.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0)
        };
        _sharedQuad.uv = new Vector2[] {
            new Vector2(0,0), new Vector2(1,0),
            new Vector2(0,1), new Vector2(1,1)
        };
        _sharedQuad.triangles = new int[] { 0,2,1, 2,3,1 };
        _sharedQuad.RecalculateNormals();
        return _sharedQuad;
    }

    private static Shader FindUnlitShader()
    {
        var s = Shader.Find("Unlit/Color");
        return s != null ? s : Shader.Find("Legacy Shaders/Diffuse");
    }

    private static Bounds GetDoorBounds(GameObject door)
    {
        var cols = door.GetComponentsInChildren<Collider>();
        if (cols.Length > 0)
        {
            var b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }
        var rends = door.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
        return new Bounds(door.transform.position, Vector3.one);
    }

    private static string ParseRoomNumber(string doorName)
    {
        if (!doorName.StartsWith("Door_")) return "";
        string suffix = doorName.Substring(5);
        string digits = "";
        foreach (char c in suffix)
        {
            if (char.IsDigit(c)) digits += c;
            else if (digits.Length > 0) break;
        }
        return digits;
    }

    private static void SafeDestroy(Object obj)
    {
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
public static class RoomCardEditorMenu
{
    static RoomCardEditorMenu() { }

    [UnityEditor.MenuItem("Tools/Room Cards/Regenerate All Room Cards")]
    public static void RegenerateFromMenu()
    {
        var setup = Object.FindFirstObjectByType<RoomCardSetup>();
        if (setup == null)
        {
            UnityEditor.EditorUtility.DisplayDialog("Room Cards",
                "No RoomCardSetup found in scene. Add it to a GameObject first.", "OK");
            return;
        }
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Regenerate Room Cards");
        setup.RegenerateCards();
        Debug.Log("[RoomCardSetup] Done.");
    }
}
#endif
