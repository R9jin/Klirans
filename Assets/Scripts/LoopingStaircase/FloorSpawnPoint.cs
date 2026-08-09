using UnityEngine;

/// <summary>
/// Marks a world-space position where the player should be placed when
/// arriving at a particular floor via a particular stairwell.
/// 
/// Place one of these GameObjects at each stair landing for every floor.
/// The stairwell system will use these to seamlessly reposition the player
/// during a looping-staircase transition.
/// </summary>
public class FloorSpawnPoint : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Which stairwell this spawn belongs to. Must match the StaircaseConnection's stairwellID.")]
    public string stairwellID = "MainStairs";

    [Tooltip("Which floor number this spawn represents (1 = ground floor).")]
    [Range(1, 10)]
    public int floorNumber = 1;

    [Header("Spawn Settings")]
    [Tooltip("Direction the player should face after spawning here (in world Y-rotation degrees). " +
             "Usually pointing away from the stair wall and into the hallway.")]
    public float spawnFacingYaw = 0f;

    [Tooltip("If true, this spawn will be used when the player arrives going upward. " +
             "If false, it is used when arriving going downward.")]
    public bool usedWhenArrivingUp = true;

    private void OnDrawGizmos()
    {
        // Cyan sphere = upward-arrival spawn  |  Yellow sphere = downward-arrival spawn
        Gizmos.color = usedWhenArrivingUp ? new Color(0f, 1f, 1f, 0.6f) : new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawSphere(transform.position, 0.25f);

        // Draw facing direction arrow
        Gizmos.color = Color.white;
        Vector3 facing = Quaternion.Euler(0f, spawnFacingYaw, 0f) * Vector3.forward;
        Gizmos.DrawRay(transform.position, facing * 0.8f);
    }
}
