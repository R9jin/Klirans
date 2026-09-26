using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public static class UpdateTheProctorPrefab
{
    [MenuItem("Tools/Klirans/Update The Proctor Prefab Stats & Audio")]
    public static void UpdatePrefab()
    {
        string prefabPath = "Assets/Prefabs/TheProctor.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[UpdateTheProctorPrefab] Could not load prefab at {prefabPath}");
            return;
        }

        float height = 2.60f;
        float pSpeed = 3.2f;
        float cSpeed = 5.2f;
        float catchDist = 1.4f;
        float accel = 12.0f;
        float angSpeed = 420.0f;

        // NavMeshAgent
        var agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = cSpeed;
            agent.height = height;
            agent.acceleration = accel;
            agent.angularSpeed = angSpeed;
            agent.radius = 0.45f;
            agent.stoppingDistance = 0.5f;
            agent.autoTraverseOffMeshLink = false;
        }

        // CapsuleCollider
        var cap = root.GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.height = height;
            cap.center = new Vector3(0f, height * 0.5f, 0f);
            cap.radius = 0.45f;
        }

        // Armature
        Transform armature = root.transform.Find("Armature");
        if (armature != null)
        {
            float scale = (height / 2.18f) * 49.745f;
            armature.localScale = Vector3.one * scale;
        }

        // TheProctorAI
        var proctorAI = root.GetComponent<TheProctorAI>();
        if (proctorAI != null)
        {
            proctorAI.targetHeightMeters = height;
            proctorAI.patrolSpeed = pSpeed;
            proctorAI.chaseSpeed = cSpeed;
            proctorAI.catchDistance = catchDist;
            proctorAI.acceleration = accel;
            proctorAI.angularSpeed = angSpeed;

            // Load and serialize AudioClips
            proctorAI.alertStingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/encountering a proctor.mp3");
            proctorAI.jumpscareScreamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/you died (lobotomy sound).mp3");
            proctorAI.footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
            proctorAI.staticHissClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/vhs static.mp3");
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[UpdateTheProctorPrefab] Successfully updated TheProctor.prefab with Slenderman height (2.60m), 5.2 chase speed, and serialized audio clips!");
    }
}
