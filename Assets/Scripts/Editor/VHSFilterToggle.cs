#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class VHSFilterToggle
{
    private const string RENDERER_PATH = "Assets/Settings/PC_Renderer.asset";
    private const string MENU_PATH = "Tools/Toggle VHS (Pixelize) Filter";

    [MenuItem(MENU_PATH, false, 100)]
    public static void ToggleFilter()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RENDERER_PATH);
        if (data == null)
        {
            Debug.LogError("[VHSFilterToggle] Could not find renderer asset at " + RENDERER_PATH);
            return;
        }

        bool newState = false;
        bool found = false;

        foreach (var feature in data.rendererFeatures)
        {
            if (feature != null && feature.name == "DoomPixelize")
            {
                newState = !feature.isActive;
                feature.SetActive(newState);
                found = true;
                break;
            }
        }

        if (found)
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VHSFilterToggle] VHS / Pixelize filter is now <b>{(newState ? "ENABLED (ON)" : "DISABLED (OFF)")}</b>.");
        }
        else
        {
            Debug.LogWarning("[VHSFilterToggle] 'DoomPixelize' feature not found in PC_Renderer.asset");
        }
    }

    [MenuItem(MENU_PATH, true)]
    public static bool ToggleFilterValidate()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RENDERER_PATH);
        if (data != null)
        {
            foreach (var feature in data.rendererFeatures)
            {
                if (feature != null && feature.name == "DoomPixelize")
                {
                    Menu.SetChecked(MENU_PATH, feature.isActive);
                    return true;
                }
            }
        }
        return false;
    }
}
#endif
