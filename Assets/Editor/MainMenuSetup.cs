#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections.Generic;

public class MainMenuSetup
{
    [MenuItem("Tools/Setup Main Menu")]
    public static void SetupMainMenu()
    {
        // 1. Ensure Scenes folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        string scenePath = "Assets/Scenes/MainMenu.unity";

        // Load custom font
        Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
        if (customFont == null) 
        {
            customFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("Custom font not found, falling back to default.");
        }

        // 2. Create new scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        newScene.name = "MainMenu";

        // 3. Create Main Camera & Video Player Background
        GameObject cameraObj = new GameObject("Main Camera");
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cameraObj.tag = "MainCamera";

        VideoPlayer vp = cameraObj.AddComponent<VideoPlayer>();
        vp.renderMode = VideoRenderMode.CameraFarPlane;
        vp.targetCameraAlpha = 1f;
        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Main Menu/KLIRANS_centered_static_loop_1080p_202607291603.mp4");
        if (clip != null)
        {
            vp.clip = clip;
        }
        else
        {
            Debug.LogWarning("Video not found at Assets/Main Menu/KLIRANS_centered_static_loop_1080p_202607291603.mp4");
        }
        vp.isLooping = true;
        vp.playOnAwake = true;

        // 4. Create EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();

        // 5. Create Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 6. Create Menu Controller
        GameObject controllerObj = new GameObject("MainMenuController");
        MainMenuController menuController = controllerObj.AddComponent<MainMenuController>();

        // 7. Create Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "KLIRANS";
        titleText.font = customFont;
        titleText.fontSize = 120;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.8f, 0.1f, 0.1f); // Dark red theme
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(800, 200);
        titleRect.anchoredPosition = new Vector2(0, 350);

        // 8. Create Play Button
        GameObject buttonObj = new GameObject("PlayButton");
        buttonObj.transform.SetParent(canvasObj.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0f, 0f, 0f, 0f); // Transparent background
        Button playButton = buttonObj.AddComponent<Button>();

        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(200, 60);
        btnRect.anchoredPosition = new Vector2(0, -100);

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(buttonObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.text = "PLAY";
        btnText.font = customFont;
        btnText.fontSize = 40;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = new Color(0.8f, 0.1f, 0.1f);
        
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        // 9. Connect Button Event
        UnityEventTools.AddPersistentListener(playButton.onClick, new UnityAction(menuController.PlayGame));

        // 10. Save Scene
        EditorSceneManager.SaveScene(newScene, scenePath);

        // 11. Add to Build Settings
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(scenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        };
        
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path != scenePath && s.path != "Assets/Scenes/SampleScene.unity")
            {
                buildScenes.Add(s);
            }
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();

        Debug.Log("Main Menu generated with video background and custom font! The MainMenu scene has been saved and added to Build Settings.");
    }
}
#endif
