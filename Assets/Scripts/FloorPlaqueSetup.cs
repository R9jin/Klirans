using UnityEngine;
using TMPro;

public class FloorPlaqueSetup : MonoBehaviour
{
    [Header("Plaque Settings")]
    public Color plaqueColor = new Color(0.85f, 0.75f, 0.55f, 1f);
    public Color textColor = new Color(0.1f, 0.05f, 0.0f, 1f);
    public float plaqueOffsetAboveDoor = 0.15f;
    public float plaqueWidth = 0.35f;
    public float plaqueHeight = 0.12f;
    public string fontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [Header("Debug")]
    public bool showGizmos = true;

    private void Awake()
    {
        SetupPlaques();
    }

    [ContextMenu("Regenerate Plaques")]
    public void SetupPlaques()
    {
        DoorInteract[] doors = FindObjectsByType<DoorInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        // Find or create a container for all room numbers to keep the hierarchy clean
        GameObject container = GameObject.Find("RoomNumbersContainer");
        if (container != null)
        {
            if (Application.isPlaying) Destroy(container);
            else DestroyImmediate(container);
        }
        container = new GameObject("RoomNumbersContainer");

        foreach (DoorInteract doorInteract in doors)
        {
            GameObject door = doorInteract.gameObject;

            // Parse room number from "Door_101A"
            string roomNumber = "";
            if (door.name.StartsWith("Door_"))
            {
                string suffix = door.name.Substring(5);
                foreach (char c in suffix)
                {
                    if (char.IsDigit(c))
                    {
                        roomNumber += c;
                    }
                    else if (roomNumber.Length > 0)
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(roomNumber)) continue;

            // Create Text parent object
            GameObject textObj = new GameObject("RoomNumberText_" + roomNumber);
            textObj.transform.SetParent(container.transform, false);

            // Center the text over the doorway, not the door hinge
            float centerZ = door.transform.position.z;
            Collider col = door.GetComponentInChildren<Collider>();
            if (col != null)
            {
                centerZ = col.bounds.center.z;
            }

            // Position (2.25 above door transform, and slightly offset to align with wall surface)
            textObj.transform.position = new Vector3(door.transform.position.x, door.transform.position.y + 2.25f, centerZ);

            // Rotation based on X position (which side of the hallway)
            if (door.transform.position.x < 0)
            {
                textObj.transform.rotation = Quaternion.Euler(0, 90, 0); // Left wall, face +X
                // Pull it out slightly from the wall surface
                textObj.transform.position += new Vector3(0.02f, 0, 0);
            }
            else
            {
                textObj.transform.rotation = Quaternion.Euler(0, 270, 0); // Right wall, face -X
                textObj.transform.position += new Vector3(-0.02f, 0, 0);
            }

            // Create TextMeshPro Component
            TextMeshPro textComponent = textObj.AddComponent<TextMeshPro>();
            textComponent.text = "ROOM " + roomNumber; // Format as "ROOM 101"
            textComponent.enableAutoSizing = true;
            textComponent.fontSizeMin = 0.5f;
            textComponent.fontSizeMax = 2.0f; // Allow larger text
            textComponent.color = textColor;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.fontStyle = FontStyles.Bold;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.8f, 0.3f); // Wider area for "ROOM 101"
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        DoorInteract[] doors = FindObjectsByType<DoorInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (doors == null) return;

        Gizmos.color = Color.yellow;
        foreach (var door in doors)
        {
            if (door != null)
            {
                Vector3 pos = door.transform.position;
                pos.y += 2.25f;
                Gizmos.DrawWireCube(pos, new Vector3(0.2f, 0.1f, 0.2f));
            }
        }
    }
#endif
}
