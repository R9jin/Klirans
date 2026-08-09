using UnityEngine;

/// <summary>
/// Editor-time helper: finds every ComfortRoom_* GameObject in the scene and
/// adds a RestroomDripAudio emitter inside it if one does not already exist.
/// Also lets you remove all drip emitters in one click.
/// This component destroys itself at runtime (it is editor-only scaffolding).
/// </summary>
public class RestroomDripSpawner : MonoBehaviour
{
    [Header("Clip")]
    [Tooltip("Drag the 'restroom droplets' AudioClip here.")]
    public AudioClip dripClip;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume      = 0.55f;
    public float minDistance = 1.0f;
    public float maxDistance = 12.0f;

    // Destroyed at runtime; this component only exists to hold the editor menu
    private void Awake() { Destroy(this); }

    // ----------------------------------------------------------------
    [ContextMenu("Spawn Drip Emitters in All Restrooms")]
    public void SpawnAll()
    {
        if (dripClip == null)
        {
            Debug.LogWarning("[RestroomDripSpawner] No dripClip assigned!");
            return;
        }

        var all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int created = 0;
        foreach (var go in all)
        {
            if (!go.name.StartsWith("ComfortRoom_")) continue;
            // Only process top-level restroom parents (depth = 1 child nesting max)
            if (go.transform.parent != null && go.transform.parent.name.StartsWith("ComfortRoom_")) continue;

            // Skip if already has a drip emitter
            if (go.GetComponentInChildren<RestroomDripAudio>() != null) continue;

            CreateEmitter(go);
            created++;
        }

        Debug.Log("[RestroomDripSpawner] Created " + created + " drip emitters.");
    }

    // ----------------------------------------------------------------
    [ContextMenu("Remove All Drip Emitters")]
    public void RemoveAll()
    {
        var emitters = Object.FindObjectsByType<RestroomDripAudio>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var e in emitters)
        {
            // Remove the whole host GameObject if we made it, else just the component
            if (e.gameObject.name == "DripAudioEmitter")
                DestroyImmediate(e.gameObject);
            else
                DestroyImmediate(e);
        }
        Debug.Log("[RestroomDripSpawner] Removed " + emitters.Length + " drip emitters.");
    }

    // ----------------------------------------------------------------
    private void CreateEmitter(GameObject restroomParent)
    {
        var host = new GameObject("DripAudioEmitter");
        host.transform.SetParent(restroomParent.transform, false);

        // Place emitter slightly inside the restroom (past the door)
        // Restrooms at south end are at z ~ -18, north end at z ~ 51
        // We push the emitter 1 unit deeper into the room from the door centre
        var doorColliders = restroomParent.GetComponentsInChildren<Collider>();
        UnityEngine.Bounds bounds = new UnityEngine.Bounds(
            restroomParent.transform.position, Vector3.zero);
        foreach (var c in doorColliders) bounds.Encapsulate(c.bounds);

        // Push into room along Z (north rooms push +Z, south rooms push -Z)
        float zPush = (bounds.center.z > 0) ? 1.5f : -1.5f;
        host.transform.position = new Vector3(
            bounds.center.x,
            bounds.center.y,
            bounds.center.z + zPush);

        var drip         = host.AddComponent<RestroomDripAudio>();
        drip.dripClip    = dripClip;
        drip.volume      = volume;
        drip.minDistance = minDistance;
        drip.maxDistance = maxDistance;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(restroomParent);
#endif
    }
}
