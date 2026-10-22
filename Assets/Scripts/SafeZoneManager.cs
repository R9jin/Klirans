using UnityEngine;

/// <summary>
/// SafeZoneManager — Tracks the player's last verified safe checkpoint.
/// When caught by The Proctor, the player is returned to this safe zone.
/// Defaults to the 1st Floor Lobby entrance.
/// </summary>
public class SafeZoneManager : MonoBehaviour
{
    public static SafeZoneManager Instance { get; private set; }

    [Header("Default Safe Zone (1F Lobby)")]
    public Vector3 defaultSafePosition = new Vector3(-80.94f, 2.43f, 9.38f);
    public float defaultSafeYaw = 0f;

    [Header("Current Safe Point")]
    public Vector3 lastSafePosition;
    public float lastSafeYaw;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        lastSafePosition = defaultSafePosition;
        lastSafeYaw = defaultSafeYaw;
    }

    /// <summary>
    /// Updates the player's safe respawn point (e.g. upon entering an office or floor landing).
    /// </summary>
    public void SetSafePoint(Vector3 pos, float yaw)
    {
        lastSafePosition = pos;
        lastSafeYaw = yaw;
        Debug.Log($"[SafeZoneManager] Safe checkpoint updated to {pos}, yaw={yaw}°");
    }

    /// <summary>
    /// Teleports the player cleanly to the last safe zone, resetting CharacterController to avoid collisions.
    /// </summary>
    public void TeleportToSafeZone(GameObject player)
    {
        if (player == null) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = lastSafePosition;
        player.transform.rotation = Quaternion.Euler(0f, lastSafeYaw, 0f);

        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.SyncPitch(0f);
        }

        if (cc != null) cc.enabled = true;

        Debug.Log($"[SafeZoneManager] Player returned to safe zone at {lastSafePosition}");
    }
}
