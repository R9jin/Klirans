using UnityEngine;

/// <summary>
/// DroppedItemFailsafe — Guarantees dropped items never fall through floor geometry
/// into the void, always settle cleanly on the floor surface, and remain fully
/// interactable for pickup with 'E'.
/// </summary>
[DisallowMultipleComponent]
public class DroppedItemFailsafe : MonoBehaviour
{
    private Rigidbody _rb;
    private Collider _col;
    private PickupItem _pickupItem;

    private float _knownFloorY = float.MinValue;
    private float _spawnTime;
    private bool _hasSettled = false;
    private const float GroundOffset = 0.08f;
    private const float RaycastUpOffset = 1.0f;
    private const float MaxFallDistance = 25.0f;

    public void Initialize(float floorY)
    {
        _knownFloorY = floorY;
    }

    private void Awake()
    {
        _spawnTime = Time.time;
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _pickupItem = GetComponent<PickupItem>() ?? GetComponentInChildren<PickupItem>();

        // Ensure root has a solid primitive collider
        if (_col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.16f;
            _col = sphere;
        }

        // Clean up invalid child MeshColliders so they don't break dynamic Rigidbody
        FixChildColliders();

        // Sample initial floor height beneath this object
        DetectFloorBelow();
    }

    private void DetectFloorBelow()
    {
        Vector3 origin = transform.position + Vector3.up * RaycastUpOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, MaxFallDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
            {
                _knownFloorY = hit.point.y;
            }
        }
    }

    private void FixChildColliders()
    {
        // Any child MeshCollider with dynamic Rigidbody must be convex and have a mesh, or be removed
        var meshCols = GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshCols)
        {
            if (mc.sharedMesh == null)
            {
                Destroy(mc);
            }
            else
            {
                mc.convex = true;
            }
        }

        // Ensure layer is Default (layer 0) so PlayerInteract SphereCast detects it
        gameObject.layer = LayerMask.NameToLayer("Default");
        foreach (Transform child in transform)
        {
            child.gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }

    private void Update()
    {
        // 1. Periodically update floor estimate while in flight
        if (!_hasSettled && Time.time - _spawnTime < 3.0f)
        {
            DetectFloorBelow();
        }

        // 2. Void failsafe: if item dropped below known floor level
        if (_knownFloorY > -100f && transform.position.y < (_knownFloorY - 0.12f))
        {
            ClampToFloor();
            return;
        }

        // Absolute abyss failsafe (if knownFloorY was not detected or void fell)
        if (transform.position.y < -5f)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            float recoveryY = player != null ? player.transform.position.y : 0.1f;
            transform.position = new Vector3(transform.position.x, recoveryY + 0.12f, transform.position.z);
            _knownFloorY = recoveryY;
            Settle();
            return;
        }

        // 3. Settle item after it stops moving or after timeout
        if (!_hasSettled)
        {
            if (Time.time - _spawnTime > 0.4f)
            {
                if (_rb != null && (_rb.linearVelocity.sqrMagnitude < 0.03f || Time.time - _spawnTime > 2.5f))
                {
                    Settle();
                }
            }
        }
    }

    private void ClampToFloor()
    {
        Vector3 pos = transform.position;
        pos.y = _knownFloorY + GroundOffset;
        transform.position = pos;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        Settle();
    }

    private void Settle()
    {
        _hasSettled = true;
        if (_rb != null)
        {
            _rb.isKinematic = true;
        }

        // Ensure PickupItem is on root and properly configured
        if (_pickupItem == null)
        {
            _pickupItem = GetComponent<PickupItem>() ?? GetComponentInChildren<PickupItem>();
        }
        if (_pickupItem != null && _pickupItem.gameObject != gameObject)
        {
            // If PickupItem was on a child, attach to root for easy PlayerInteract raycasting
            var rootPickup = GetComponent<PickupItem>();
            if (rootPickup == null) rootPickup = gameObject.AddComponent<PickupItem>();
            rootPickup.itemData = _pickupItem.itemData;
            rootPickup.amount = _pickupItem.amount;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                _knownFloorY = Mathf.Max(_knownFloorY, contact.point.y);
                break;
            }
        }
    }
}
