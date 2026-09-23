using UnityEngine;

/// <summary>
/// LockerHide — Attach to a locker GameObject in the scene to make it a hideable spot.
///
/// SETUP per locker:
///   1. Add this component to the locker root GameObject.
///   2. Create a child empty named "PeekAnchor" — position it inside the locker
///      at eye-level, with its forward direction pointing OUT through the door gap.
///      (i.e. looking at the hallway through the crack between the doors.)
///   3. Make sure the locker has a BoxCollider set as a Trigger on the Interactable layer,
///      so PlayerInteract's SphereCast can find it.
///
/// Only a limited number of lockers in the scene should have this component.
/// The interaction prompt is suppressed when the player is already hidden elsewhere.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LockerHide : MonoBehaviour, IInteractable
{
    [Header("Peek Anchor")]
    [Tooltip("A child Transform positioned inside the locker at the door gap (eye level). " +
             "Its forward (+Z) should face the hallway — the direction the player peeks toward.")]
    public Transform peekAnchor;

    [Header("Locker Identity")]
    [Tooltip("Optional label shown in the interaction prompt, e.g. 'Staff Locker', '2F Locker'.")]
    public string lockerLabel = "Locker";

    // ── IInteractable ────────────────────────────────────────────────────────

    public string GetPrompt()
    {
        // Don't show prompt if player is already hiding somewhere
        if (LockerHideManager.IsPlayerHidden) return string.Empty;

        return $"[E] Hide in {lockerLabel}";
    }

    public void Interact()
    {
        // Already hidden — treat E as exit if the player is interacting with
        // the same locker they're in (edge case: they shouldn't reach another
        // locker's collider while inside one anyway)
        if (LockerHideManager.IsPlayerHidden)
        {
            LockerHideManager.Instance?.ExitLocker();
            return;
        }

        if (LockerHideManager.Instance == null)
        {
            Debug.LogWarning("[LockerHide] No LockerHideManager found in the scene!");
            return;
        }

        // Auto-find or create PeekAnchor if not assigned
        Transform anchor = peekAnchor;
        if (anchor == null)
        {
            anchor = transform.Find("PeekAnchor");
            if (anchor == null)
            {
                // Create a fallback anchor at the locker's center facing outward
                var anchorGO = new GameObject("PeekAnchor");
                anchorGO.transform.SetParent(transform, false);
                // Place it slightly in from the front face, at eye height
                anchorGO.transform.localPosition = new Vector3(0f, 1.55f, 0.05f);
                anchorGO.transform.localRotation = Quaternion.identity; // forward = locker forward
                anchor = anchorGO.transform;
                peekAnchor = anchor;
                Debug.LogWarning($"[LockerHide] No PeekAnchor found on {name}. Created a default one — please adjust it in the scene.");
            }
        }

        Debug.Log($"[LockerHide] Player entering locker: {lockerLabel} on {gameObject.name}");
        LockerHideManager.Instance.EnterLocker(this, anchor);
    }

    // ── Editor Gizmos ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Draw a green eye-slit icon at the peek anchor
        Transform anchor = peekAnchor != null ? peekAnchor : transform.Find("PeekAnchor");
        if (anchor == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(anchor.position, 0.06f);
        // Draw the peek direction (the direction the player looks outward)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(anchor.position, anchor.forward * 0.5f);

        // Label bounding box of the locker
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
