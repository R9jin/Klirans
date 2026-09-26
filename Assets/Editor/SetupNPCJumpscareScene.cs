using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupNPCJumpscareScene
{
    [MenuItem("Tools/Klirans/Configure Jumpscares and Dropped Items")]
    public static void Configure()
    {
        // 1. Fix Flashlight prefab
        FixFlashlightPrefab.Fix();

        // 2. Update The Proctor prefab
        UpdateTheProctorPrefab.UpdatePrefab();

        // 3. Configure NPCJumpscareManager in active scene
        var jumpscareMgr = Object.FindAnyObjectByType<NPCJumpscareManager>();
        if (jumpscareMgr != null)
        {
            jumpscareMgr.scareStingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");
            jumpscareMgr.proctorStingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
            jumpscareMgr.staticHissClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");
            jumpscareMgr.gaspBreathClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");

            jumpscareMgr.jumpscareVolume = 1.0f;
            jumpscareMgr.jumpscareAnxietyIncrease = 12.0f;
            jumpscareMgr.touchDistanceThreshold = 1.30f;
            jumpscareMgr.touchScareChance = 0.50f;
            jumpscareMgr.jumpscareGlobalCooldown = 18.0f;
            jumpscareMgr.touchRerollCooldown = 5.0f;
            jumpscareMgr.flashlightInFaceRadius = 3.5f;
            jumpscareMgr.flashlightBlindingThreshold = 1.0f;

            EditorUtility.SetDirty(jumpscareMgr);
            Debug.Log("[SetupNPCJumpscareScene] NPCJumpscareManager in scene configured with audio clips and error punishment settings!");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupNPCJumpscareScene] Configuration completed and saved successfully!");
    }
}
