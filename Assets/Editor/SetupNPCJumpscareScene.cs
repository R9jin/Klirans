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
            jumpscareMgr.startDistance = 0.58f;
            jumpscareMgr.closestDistance = 0.38f;
            jumpscareMgr.faceScale = 1.55f;
            jumpscareMgr.headHeightOffset = 0.03f;

            EditorUtility.SetDirty(jumpscareMgr);
            Debug.Log("[SetupNPCJumpscareScene] NPCJumpscareManager in scene configured with audio clips and error punishment settings!");
        }

        // 4. Configure ProctorJumpscareQTE
        var qte = Object.FindAnyObjectByType<ProctorJumpscareQTE>();
        if (qte == null)
        {
            var proctorMgr = Object.FindAnyObjectByType<TheProctorManager>();
            GameObject targetGO = proctorMgr != null ? proctorMgr.gameObject : new GameObject("ProctorJumpscareQTE");
            qte = targetGO.AddComponent<ProctorJumpscareQTE>();
        }
        if (qte != null)
        {
            qte.qteKey = KeyCode.Q;
            qte.alternateKey = KeyCode.Space;
            qte.minRequiredPresses = 12;
            qte.maxRequiredPresses = 18;
            qte.anxietyIncreaseRate = 14.0f;
            qte.progressDecayRate = 1.0f;
            qte.decayGracePeriod = 0.28f;

            qte.heartbeatAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/fast heart beat.mp3");
            qte.strugglePressAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/man gasping for air.mp3");
            qte.breakFreeAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");

            EditorUtility.SetDirty(qte);
            Debug.Log("[SetupNPCJumpscareScene] ProctorJumpscareQTE configured in scene with audio and struggle settings!");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupNPCJumpscareScene] Configuration completed and saved successfully!");
    }
}
