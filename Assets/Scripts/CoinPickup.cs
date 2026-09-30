using System.Collections;
using UnityEngine;

/// <summary>
/// CoinPickup — Realistic miniscule Philippine peso coin found resting on surfaces
/// such as student desks, teacher tables, shelves, and hallway floors.
/// Lies flat on whatever surface it is placed on (like an actual droppable item, NOT spinning or floating like Sonic).
/// Can be picked up by pressing [E] or walking over it.
/// Supports balanced periodic respawning when player is away from the area,
/// replenishing exploration currency across the night without allowing camp-farming.
/// </summary>
public class CoinPickup : MonoBehaviour, IInteractable
{
    [Header("Coin Value")]
    [Tooltip("Amount of currency awarded on pickup.")]
    public int coinValue = 1;

    [Header("Respawn Balancing")]
    [Tooltip("If true, this coin can respawn after being collected when player is away.")]
    public bool canRespawn = true;

    [Tooltip("Minimum seconds before this coin can respawn (default 3 minutes).")]
    public float minRespawnTime = 180f;

    [Tooltip("Maximum seconds before this coin can respawn (default 5 minutes).")]
    public float maxRespawnTime = 300f;

    [Tooltip("Minimum distance (meters) the player must be from this spot to respawn.")]
    public float minPlayerDistanceToRespawn = 9.0f;

    [Header("Audio")]
    public AudioClip customPickupSound;

    private bool _isCollected = false;
    private SphereCollider _collider;
    private Coroutine _respawnCoroutine;
    private Transform _playerTransform;

    public bool IsCollected => _isCollected;

    private void Awake()
    {
        _collider = GetComponent<SphereCollider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
        }
        _collider.isTrigger = true;
        _collider.radius = 0.35f;
        _collider.center = new Vector3(0f, 0.06f, 0f);

        EnsureVisualMesh();
        SnapToSurface();
    }

    private void Start()
    {
        SnapToSurface();
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }

    /// <summary>
    /// Snaps the coin flush onto the surface directly beneath it (tabletop, desk, shelf, or floor)
    /// with realistic orientation and subtle natural yaw, preventing hovering or floating.
    /// </summary>
    public void SnapToSurface()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.6f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Place coin sitting flat on top of the surface with half-thickness offset (~1.6mm)
            transform.position = hit.point + hit.normal * 0.0016f;

            // Orient flush against the surface normal with a randomized subtle yaw
            float randomYaw = Random.Range(0f, 360f);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, randomYaw, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;

        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponent<PlayerMovement>() != null)
        {
            CollectCoin();
        }
    }

    // ── IInteractable Implementation ──────────────────────────────────────────

    public string GetPrompt()
    {
        if (_isCollected) return string.Empty;
        return $"[E] Pick up Coin (₱{coinValue})";
    }

    public void Interact()
    {
        if (_isCollected) return;
        CollectCoin();
    }

    // ── Collection & Respawn Logic ────────────────────────────────────────────

    public void CollectCoin()
    {
        if (_isCollected) return;
        _isCollected = true;

        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(coinValue, playSound: true);
        }

        if (CoinDisplayUI.Instance != null)
        {
            CoinDisplayUI.Instance.ShowFloater($"+₱{coinValue}");
        }

        if (canRespawn)
        {
            // Hide visual and disable collider while waiting to respawn
            SetVisualActive(false);
            if (_collider != null) _collider.enabled = false;

            if (_respawnCoroutine != null) StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // 1. Wait balanced cooldown interval (e.g. 3 to 5 minutes)
        float cooldown = Random.Range(minRespawnTime, maxRespawnTime);
        yield return new WaitForSeconds(cooldown);

        // 2. Wait until player is away from this room/spot (preventing spawning in player's face)
        while (true)
        {
            if (_playerTransform == null) FindPlayer();

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist >= minPlayerDistanceToRespawn)
                {
                    break; // Player is far enough away; safe to respawn!
                }
            }
            else
            {
                break;
            }

            yield return new WaitForSeconds(5.0f);
        }

        // 3. Re-orient on surface with subtle random yaw
        SnapToSurface();

        // 4. Re-enable visuals and interaction
        SetVisualActive(true);
        if (_collider != null) _collider.enabled = true;
        _isCollected = false;
        _respawnCoroutine = null;
    }

    private void SetVisualActive(bool active)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = active;
        }
    }

    private void EnsureVisualMesh()
    {
        // Check if visual mesh already exists
        var existingMesh = transform.Find("CoinDiscMesh");
        if (existingMesh != null)
        {
            existingMesh.localScale = new Vector3(0.045f, 0.0016f, 0.045f);
            existingMesh.localRotation = Quaternion.identity;
            existingMesh.localPosition = Vector3.zero;
            return;
        }

        if (GetComponentInChildren<MeshRenderer>() != null) return;

        // Create 3D coin cylinder disc
        var discGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGO.name = "CoinDiscMesh";
        discGO.transform.SetParent(transform, false);
        discGO.transform.localPosition = Vector3.zero;
        discGO.transform.localScale = new Vector3(0.045f, 0.0016f, 0.045f);
        discGO.transform.localRotation = Quaternion.identity;

        var primCol = discGO.GetComponent<Collider>();
        if (primCol != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(primCol);
            else Destroy(primCol);
#else
            Destroy(primCol);
#endif
        }

        var mr = discGO.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = "Coin_Realistic_Mat";
            mat.color = new Color(0.92f, 0.83f, 0.54f, 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.94f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.75f);
            mr.sharedMaterial = mat;
        }
    }
}
