using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CreatePauseMenuUI : EditorWindow
{
    [MenuItem("Tools/Create Pause Menu UI")]
    public static void CreateUI()
    {
        // 1. EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 2. Canvas
        GameObject canvasObj = new GameObject("PauseCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create PauseCanvas");

        // 3. PauseMenu script
        PauseMenu pauseMenu = canvasObj.AddComponent<PauseMenu>();

        // 4. Pause Menu UI Panel
        GameObject panelObj = new GameObject("PauseMenuPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        pauseMenu.pauseMenuUI = panelObj;
        
        // 5. Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "PAUSED";
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 80;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 200);
        titleRect.sizeDelta = new Vector2(400, 100);

        // 6. Resume Button
        GameObject resumeBtnObj = new GameObject("ResumeButton");
        resumeBtnObj.transform.SetParent(panelObj.transform, false);
        Image resumeBtnImage = resumeBtnObj.AddComponent<Image>();
        resumeBtnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        RectTransform resumeRect = resumeBtnObj.GetComponent<RectTransform>();
        resumeRect.anchoredPosition = new Vector2(0, 50);
        resumeRect.sizeDelta = new Vector2(250, 70);

        GameObject resumeTextObj = new GameObject("Text");
        resumeTextObj.transform.SetParent(resumeBtnObj.transform, false);
        Text resumeText = resumeTextObj.AddComponent<Text>();
        resumeText.text = "Resume";
        resumeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        resumeText.fontSize = 30;
        resumeText.alignment = TextAnchor.MiddleCenter;
        resumeText.color = Color.white;
        resumeTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 70);
        resumeTextObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(resumeBtn.onClick, pauseMenu.Resume);

        // 7. Exit Button
        GameObject exitBtnObj = new GameObject("ExitButton");
        exitBtnObj.transform.SetParent(panelObj.transform, false);
        Image exitBtnImage = exitBtnObj.AddComponent<Image>();
        exitBtnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        Button exitBtn = exitBtnObj.AddComponent<Button>();
        RectTransform exitRect = exitBtnObj.GetComponent<RectTransform>();
        exitRect.anchoredPosition = new Vector2(0, -50);
        exitRect.sizeDelta = new Vector2(250, 70);

        GameObject exitTextObj = new GameObject("Text");
        exitTextObj.transform.SetParent(exitBtnObj.transform, false);
        Text exitText = exitTextObj.AddComponent<Text>();
        exitText.text = "Quit Game";
        exitText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        exitText.fontSize = 30;
        exitText.alignment = TextAnchor.MiddleCenter;
        exitText.color = Color.white;
        exitTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 70);
        exitTextObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(exitBtn.onClick, pauseMenu.QuitGame);

        // Hide it by default
        panelObj.SetActive(false);

        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("Pause Menu successfully generated!");
    }
}
