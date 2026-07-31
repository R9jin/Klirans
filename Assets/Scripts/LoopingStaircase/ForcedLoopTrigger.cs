using UnityEngine;

/// <summary>
/// A boundary box trigger placed at a stairwell TurningPoint.
/// When the player enters this zone, they are IMMEDIATELY forced to loop
/// to the destination floor — no velocity check, no probability roll.
///
/// Place these at the TurningPoint of any floor where you want a hard loop
/// boundary (e.g. 3rd floor → 1st floor to prevent escaping the map).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ForcedLoopTrigger : MonoBehaviour
{
    [Header("Loop Definition")]
    [Tooltip("Which stairwell this boundary belongs to (must match LoopingStaircaseSystem IDs).")]
    public string stairwellID = "MainStairs";

    [Tooltip("Which floor the player is ON when they hit this boundary.")]
    public int fromFloor = 3;

    [Tooltip("Which floor to send the player to when the boundary is hit.")]
    public int destinationFloor = 1;

    [Header("Debug")]
    public bool showGizmo = true;

    private LoopingStaircaseSystem _loopSystem;
    private BoxCollider _col;

    private void Awake()
    {
        _col = GetComponent<BoxCollider>();
        _col.isTrigger = true;
    }

    private void Start()
    {
        var systems = UnityEngine.Object.FindObjectsByType<LoopingStaircaseSystem>(FindObjectsInactive.Include);
        if (systems != null && systems.Length > 0)
            _loopSystem = systems[0];

        if (_loopSystem == null)
            Debug.LogWarning($"[ForcedLoopTrigger] No LoopingStaircaseSystem found in scene! " +
                             $"Boundary on '{name}' will not function.");
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTrigger(other);
    }

    private void TryTrigger(Collider other)
    {
        if (_loopSystem == null) return;
        if (!IsPlayer(other)) return;

        Debug.Log($"[ForcedLoopTrigger] 🚨 Boundary triggered on '{name}': " +
                  $"{stairwellID} F{fromFloor} → F{destinationFloor}");

        _loopSystem.TriggerForcedLoop(stairwellID, fromFloor, destinationFloor);
    }

    private bool IsPlayer(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponentInParent<PlayerMovement>() != null) return true;
        if (col.gameObject.name.Contains("Player")) return true;
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmo) return;
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(bc.center, bc.size);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
#endif
}
