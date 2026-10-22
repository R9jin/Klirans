using UnityEngine;

/// <summary>
/// GameModeManager — Global mode switcher between Standard (Survival Horror) and Free Roam (Peaceful Exploration).
/// Persists across scenes and game restarts via PlayerPrefs.
/// </summary>
public static class GameModeManager
{
    private const string FreeRoamPrefKey = "KLIRANS_FREE_ROAM_MODE";
    private static bool? _isFreeRoamCached = null;

    /// <summary>
    /// True when the player selected Free Roam mode from the Main Menu.
    /// When active:
    /// - Enemy AI (The Proctor, Weeping Student) does not spawn.
    /// - Jumpscares (NPC collision scares, QTEs) are deactivated.
    /// - Anxiety stays at 0 (no cardiac arrest / Game Over).
    /// - Campus blackouts and door lockdown traps are suppressed.
    /// - All quest objectives, clearance signatures, puzzles, lockers, and items remain 100% playable.
    /// </summary>
    public static bool IsFreeRoamMode
    {
        get
        {
            if (!_isFreeRoamCached.HasValue)
            {
                _isFreeRoamCached = PlayerPrefs.GetInt(FreeRoamPrefKey, 0) == 1;
            }
            return _isFreeRoamCached.Value;
        }
    }

    /// <summary>
    /// Sets the active game mode.
    /// </summary>
    /// <param name="enabled">True for Free Roam (peaceful), False for Standard (survival horror).</param>
    public static void SetFreeRoamMode(bool enabled)
    {
        _isFreeRoamCached = enabled;
        PlayerPrefs.SetInt(FreeRoamPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[GameModeManager] Active game mode set to: {(enabled ? "FREE ROAM (Peaceful Exploration)" : "STANDARD (Survival Horror)")}");
    }
}
