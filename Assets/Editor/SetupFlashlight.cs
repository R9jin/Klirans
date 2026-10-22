using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class SetupFlashlight
{
    [MenuItem("Tools/Setup Flashlight")]
    public static void Run()
    {
        // 1. Create the InventoryItem asset
        InventoryItem flashlightAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Flashlight.asset");
        if (flashlightAsset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Items"))
            {
                AssetDatabase.CreateFolder("Assets", "Items");
            }
            flashlightAsset = ScriptableObject.CreateInstance<InventoryItem>();
            flashlightAsset.itemName = "Flashlight";
            flashlightAsset.description = "Press F to toggle on/off.";
            flashlightAsset.itemType = InventoryItem.ItemType.Tool;
            flashlightAsset.isStackable = false;
            
            AssetDatabase.CreateAsset(flashlightAsset, "Assets/Items/Flashlight.asset");
            AssetDatabase.SaveAssets();
        }

        // 2. Set up the UI Prompt on HudCanvas
        GameObject hudCanvasObj = GameObject.Find("HudCanvas");
        Text promptText = null;
        if (hudCanvasObj != null)
        {
            Transform promptTransform = hudCanvasObj.transform.Find("InteractPrompt");
            if (promptTransform == null)
            {
                GameObject promptObj = new GameObject("InteractPrompt");
                promptObj.transform.SetParent(hudCanvasObj.transform, false);
                promptText = promptObj.AddComponent<Text>();
                promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                promptText.fontSize = 24;
                promptText.alignment = TextAnchor.MiddleCenter;
                promptText.color = Color.white;
                
                Outline outline = promptObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);

                RectTransform rect = promptObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -50); 
                rect.sizeDelta = new Vector2(400, 50);
                
                promptObj.SetActive(false);
            }
            else
            {
                promptText = promptTransform.GetComponent<Text>();
            }
        }

        // 3. Set up the Player
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            PlayerInteract interact = playerObj.GetComponent<PlayerInteract>();
            if (interact == null)
            {
                interact = playerObj.AddComponent<PlayerInteract>();
            }
            interact.interactableLayer = LayerMask.GetMask("Default");
            interact.promptText = promptText;
            
            FlashlightController fc = playerObj.GetComponent<FlashlightController>();
            if (fc == null)
            {
                fc = playerObj.AddComponent<FlashlightController>();
            }
            fc.flashlightItemData = flashlightAsset;
            
            Camera cam = playerObj.GetComponentInChildren<Camera>();
            if (cam != null) {
                Transform camTransform = cam.transform;
                Transform spotlightTransform = camTransform.Find("FlashlightLight");
                Light spotlight;
                if (spotlightTransform == null)
                {
                    GameObject spotlightObj = new GameObject("FlashlightLight");
                    spotlightObj.transform.SetParent(camTransform, false);
                    spotlightObj.transform.localPosition = new Vector3(0, 0, 0); 
                    spotlightObj.transform.localRotation = Quaternion.identity;
                    
                    spotlight = spotlightObj.AddComponent<Light>();
                    spotlight.type = LightType.Spot;
                    spotlight.range = 30f;
                    spotlight.spotAngle = 45f;
                    spotlight.intensity = 2f;
                    spotlight.enabled = false;
                }
                else
                {
                    spotlight = spotlightTransform.GetComponent<Light>();
                }
                fc.flashlightLight = spotlight;
            }
        }

        // 4. Create the Flashlight cube in the world
        GameObject existingCube = GameObject.Find("FlashlightPickup");
        if (existingCube == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FlashlightPickup";
            cube.transform.localScale = new Vector3(0.2f, 0.2f, 0.6f);
            
            if (playerObj != null)
            {
                cube.transform.position = playerObj.transform.position + playerObj.transform.forward * 2f + Vector3.up * 1f;
            }
            
            PickupItem pickup = cube.AddComponent<PickupItem>();
            pickup.itemData = flashlightAsset;
            pickup.amount = 1;
            
            cube.layer = LayerMask.NameToLayer("Default");
        }
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("Flashlight setup complete!");
    }
}
