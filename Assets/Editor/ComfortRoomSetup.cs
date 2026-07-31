#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public class ComfortRoomSetup
{
    private class PropTemplateData
    {
        public GameObject templateObject;
        public string cleanName;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [MenuItem("Tools/Setup All Comfort Rooms (Props & Alternating Doors)")]
    public static void SetupComfortRooms()
    {
        // 1. Load door models
        GameObject mensDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BathroomAssets/models/mens_door.fbx");
        GameObject womensDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BathroomAssets/models/womens_door.fbx");

        if (mensDoorPrefab == null || womensDoorPrefab == null)
        {
            Debug.LogError("[ComfortRoomSetup] Could not find mens_door.fbx or womens_door.fbx in Assets/BathroomAssets/models/.");
            return;
        }

        // 2. Find all ComfortRoom root objects in the scene
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        List<GameObject> comfortRooms = new List<GameObject>();
        GameObject sourceRoom = null;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("ComfortRoom"))
            {
                comfortRooms.Add(obj);
                if (obj.name.Equals("ComfortRoom_South") || obj.name.Equals("ComfortRoom_South_Men") || obj.name.Equals("ComfortRoom_South_Women"))
                {
                    if (sourceRoom == null || obj.name.Equals("ComfortRoom_South_Men"))
                    {
                        sourceRoom = obj;
                    }
                }
            }
        }

        if (comfortRooms.Count == 0)
        {
            Debug.LogError("[ComfortRoomSetup] No GameObjects starting with 'ComfortRoom' found in scene!");
            return;
        }

        if (sourceRoom == null)
        {
            sourceRoom = comfortRooms[0];
            Debug.LogWarning($"[ComfortRoomSetup] Target template not found, defaulting to '{sourceRoom.name}' as template source.");
        }

        Debug.Log($"[ComfortRoomSetup] Found {comfortRooms.Count} comfort rooms. Using '{sourceRoom.name}' as template source.");

        // Cache template door transform details from sourceRoom
        Transform templateDoorTransform = null;
        foreach (Transform child in sourceRoom.transform)
        {
            if (child.name.ToLower().Contains("door"))
            {
                templateDoorTransform = child;
                break;
            }
        }

        Vector3 templateDoorPos = templateDoorTransform != null ? templateDoorTransform.localPosition : Vector3.zero;
        Quaternion templateDoorRot = templateDoorTransform != null ? templateDoorTransform.localRotation : Quaternion.identity;
        Vector3 templateDoorScale = templateDoorTransform != null ? templateDoorTransform.localScale : Vector3.one;

        // Record Undo state
        Undo.RegisterCompleteObjectUndo(comfortRooms.ToArray(), "Setup Comfort Rooms");

        // Cache template children data BEFORE modifying any room
        List<PropTemplateData> propTemplates = new List<PropTemplateData>();
        foreach (Transform child in sourceRoom.transform)
        {
            string cleanName = CleanAssetName(child.gameObject.name);
            child.gameObject.name = cleanName;

            // Skip door objects in generic prop template (doors managed separately)
            if (cleanName.Equals("MensDoor") || cleanName.Equals("WomensDoor") || cleanName.ToLower().Contains("door"))
                continue;

            propTemplates.Add(new PropTemplateData
            {
                templateObject = child.gameObject,
                cleanName = cleanName,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
        }

        // 3. Process each comfort room
        bool isMenNext = true; // Toggle for alternating doors

        for (int i = 0; i < comfortRooms.Count; i++)
        {
            GameObject room = comfortRooms[i];
            bool isSource = (room == sourceRoom);
            bool isNorthRoom = room.name.Contains("North");

            Quaternion targetRotationOffset = isNorthRoom ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            // Replicate props to non-source target rooms
            if (!isSource)
            {
                // Clear existing non-door children in target room
                List<GameObject> childrenToDestroy = new List<GameObject>();
                foreach (Transform child in room.transform)
                {
                    string childName = CleanAssetName(child.gameObject.name);
                    if (!childName.Equals("MensDoor") && !childName.Equals("WomensDoor") && !childName.ToLower().Contains("door"))
                    {
                        childrenToDestroy.Add(child.gameObject);
                    }
                }
                foreach (GameObject child in childrenToDestroy)
                {
                    Undo.DestroyObjectImmediate(child);
                }

                // Copy template props from sourceRoom to target room
                foreach (PropTemplateData prop in propTemplates)
                {
                    if (prop.templateObject == null) continue;

                    GameObject copiedProp = Object.Instantiate(prop.templateObject, room.transform);
                    copiedProp.name = prop.cleanName;

                    if (isNorthRoom)
                    {
                        // Rotate local position and orientation by 180 degrees to face opposite hallway direction
                        copiedProp.transform.localPosition = targetRotationOffset * prop.localPosition;
                        copiedProp.transform.localRotation = targetRotationOffset * prop.localRotation;
                    }
                    else
                    {
                        copiedProp.transform.localPosition = prop.localPosition;
                        copiedProp.transform.localRotation = prop.localRotation;
                    }

                    copiedProp.transform.localScale = prop.localScale;
                    Undo.RegisterCreatedObjectUndo(copiedProp, "Copy Prop");
                }
            }

            // 4. Snap and eliminate wall gaps inside the comfort room
            SnapWallSegments(room.transform);

            // 5. Door setup (Men's / Women's door swap) & clean naming
            GameObject doorPrefabToUse = isMenNext ? mensDoorPrefab : womensDoorPrefab;
            string doorTypeTag = isMenNext ? "Men" : "Women";

            // Update room name to reflect gender CR designation cleanly
            string[] nameParts = room.name.Split('_');
            string baseRoomName = nameParts[0] + "_" + (nameParts.Length > 1 ? nameParts[1] : "");
            room.name = $"{baseRoomName}_{doorTypeTag}";

            // Clear old door objects in room
            List<GameObject> oldDoors = new List<GameObject>();
            foreach (Transform child in room.transform)
            {
                if (child.name.ToLower().Contains("door"))
                {
                    oldDoors.Add(child.gameObject);
                }
            }
            foreach (GameObject oldDoor in oldDoors)
            {
                Undo.DestroyObjectImmediate(oldDoor);
            }

            // Create new door with correct orientation and location for North/South rooms
            GameObject newDoor = Object.Instantiate(doorPrefabToUse, room.transform);
            newDoor.name = isMenNext ? "MensDoor" : "WomensDoor";
            newDoor.transform.localPosition = targetRotationOffset * templateDoorPos;
            newDoor.transform.localRotation = targetRotationOffset * templateDoorRot;
            newDoor.transform.localScale = templateDoorScale;
            
            Undo.AddComponent<DoorInteract>(newDoor);
            
            Undo.RegisterCreatedObjectUndo(newDoor, "Swap/Add Door");

            // Ensure all children in room have clean names
            foreach (Transform child in room.transform)
            {
                child.gameObject.name = CleanAssetName(child.gameObject.name);
            }

            // 6. Ensure all bathroom assets have MeshColliders attached so player collides with them
            EnsureCollidersOnProps(room.transform);

            Debug.Log($"[ComfortRoomSetup] Configured {room.name} with colliders (North flipped: {isNorthRoom}).");

            // Alternate door designation for adjacent room
            isMenNext = !isMenNext;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[ComfortRoomSetup] Successfully added colliders to all bathroom assets!");
    }

    private static void EnsureCollidersOnProps(Transform roomTransform)
    {
        MeshRenderer[] renderers = roomTransform.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer mr in renderers)
        {
            GameObject obj = mr.gameObject;
            Collider col = obj.GetComponent<Collider>();
            if (col == null)
            {
                MeshFilter mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider meshCol = Undo.AddComponent<MeshCollider>(obj);
                    meshCol.sharedMesh = mf.sharedMesh;
                }
                else
                {
                    Undo.AddComponent<BoxCollider>(obj);
                }
            }
        }
    }

    private static string CleanAssetName(string rawName)
    {
        string name = rawName.Replace("(Clone)", "").Trim();
        int parenIdx = name.IndexOf('(');
        if (parenIdx > 0) name = name.Substring(0, parenIdx).Trim();

        string lower = name.ToLower();
        if (lower.Contains("mens_door") || lower.Contains("mensdoor")) return "MensDoor";
        if (lower.Contains("womens_door") || lower.Contains("womensdoor")) return "WomensDoor";
        if (lower.Contains("toilet")) return "Toilet";
        if (lower.Contains("sink")) return "Sink";
        if (lower.Contains("mirror")) return "Mirror";
        if (lower.Contains("dryer") || lower.Contains("handryer")) return "HandDryer";
        if (lower.Contains("soap")) return "SoapDispenser";
        if (lower.Contains("paper") || lower.Contains("hangar")) return "TissueHangar";
        if (lower.Contains("trash") || lower.Contains("bin")) return "TrashBin";

        return name;
    }

    private static void SnapWallSegments(Transform roomTransform)
    {
        foreach (Transform child in roomTransform)
        {
            if (child.name.ToLower().Contains("wall"))
            {
                Vector3 pos = child.localPosition;
                pos.x = Mathf.Round(pos.x * 20f) / 20f;
                pos.y = Mathf.Round(pos.y * 20f) / 20f;
                pos.z = Mathf.Round(pos.z * 20f) / 20f;
                child.localPosition = pos;
            }
        }
    }
}
#endif
