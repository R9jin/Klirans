using UnityEngine;

/// <summary>
/// Placed at the mid-point of a stair flight (the "point of no return").
/// When the player enters this trigger, the LoopingStaircaseSystem evaluates
/// whether a looping transition should occur.
///
/// Setup:
///   1. Add a BoxCollider (set Is Trigger = true) sized to cover the mid-stair area.
///   2. Assign the stairwellID matching the parent StaircaseConnection rule.
///   3. Set fromFloor to the floor the player is ascending FROM.
///   4. Assign the LoopingStaircaseSystem reference (or let it be auto-found).
///
/// The trigger fires only once per traversal thanks to the cooldown on the system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class StairTriggerZone : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Must match the stairwellID in the corresponding StaircaseConnection.")]
    public string stairwellID = "MainStairs";

    [Tooltip("The floor the player is coming FROM when they enter this trigger.")]
    public int fromFloor = 1;

    [Header("References")]
    [Tooltip("Drag the LoopingStaircaseSystem GameObject here, or leave empty to auto-find.")]
    public LoopingStaircaseSystem staircaseSystem;

    private void Awake()
    {
        // Ensure the collider is a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[StairTriggerZone] Collider on '{gameObject.name}' was not a trigger – auto-fixed.", this);
        }
    }

    private void Start()
    {
        if (staircaseSystem == null)
        {
            staircaseSystem = FindAnyObjectByType<LoopingStaircaseSystem>();
            if (staircaseSystem == null)
                Debug.LogError($"[StairTriggerZone] '{gameObject.name}' could not find a LoopingStaircaseSystem in the scene.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (staircaseSystem == null) return;

        // Only affect the Player (tag-based check – set Player tag in Unity)
        if (!other.CompareTag("Player")) return;

        staircaseSystem.OnPlayerEnteredStairTrigger(stairwellID, fromFloor, other.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawSphere(sphere.center, sphere.radius);
    }
}
