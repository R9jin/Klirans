#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class AudioSetup
{
    [MenuItem("Tools/Setup Game Audio")]
    public static void SetupGameAudio()
    {
        // Find Player
        PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
        StaminaSystem staminaSystem = Object.FindFirstObjectByType<StaminaSystem>();

        if (playerMovement == null || staminaSystem == null)
        {
            Debug.LogError("PlayerMovement or StaminaSystem not found in the current scene. Please open the game scene (SampleScene) first.");
            return;
        }

        // Setup Footsteps
        if (playerMovement.footstepAudioSource == null)
        {
            AudioSource source = playerMovement.gameObject.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/footsteps walking & running.mp3");
            source.loop = true;
            source.playOnAwake = false;
            playerMovement.footstepAudioSource = source;
            EditorUtility.SetDirty(playerMovement);
        }

        // Setup Exhaustion
        if (staminaSystem.breathingAudioSource == null)
        {
            AudioSource source = staminaSystem.gameObject.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/male heavy breathing.mp3");
            source.loop = true;
            source.playOnAwake = false;
            staminaSystem.breathingAudioSource = source;
            EditorUtility.SetDirty(staminaSystem);
        }

        // Setup BGM
        BGMController existingBGM = Object.FindFirstObjectByType<BGMController>();
        if (existingBGM == null)
        {
            GameObject bgmObj = new GameObject("BGMManager");
            BGMController bgmController = bgmObj.AddComponent<BGMController>();
            AudioSource source = bgmObj.GetComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/hallway background sounds.mp3");
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.5f; // Lower volume for ambience
        }

        Debug.Log("Audio successfully hooked up to the player and scene! You can adjust the volumes of the AudioSource components if needed.");
    }
}
#endif
