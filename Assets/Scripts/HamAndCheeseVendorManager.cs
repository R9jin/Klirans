using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HamAndCheeseVendorManager — Manages the wandering spawn and relocation lifecycle
/// of the Ham & Cheese vendor student ("Naglalako ng Ham and Cheese").
/// Spawns Renz in hallway alcoves and corridors outside the player's direct line of sight.
/// </summary>
public class HamAndCheeseVendorManager : MonoBehaviour
{
    public static HamAndCheeseVendorManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [Tooltip("Prefab or template for the Ham and Cheese vendor.")]
    public GameObject vendorPrefab;

    [Tooltip("Delay before first spawn at game start.")]
    public float initialSpawnDelay = 3.0f;

    [Tooltip("Time vendor stays in one location before roaming to another spot.")]
    public float relocateInterval = 100.0f;

    [Tooltip("Relocation delay after player makes a purchase.")]
    public float postPurchaseRelocateDelay = 20.0f;

    [Header("Runtime State")]
    public HamAndCheeseVendor activeVendor;
    public int currentSpawnIndex = -1;

    // Candidate hallway spawn spots across 1F, 2F, and 3F
    private readonly Vector3[] _spawnSpots = new Vector3[]
    {
        // 1st Floor
        new Vector3(-84.00f, 2.43f, -4.00f),  // 1F South Hallway
        new Vector3(-84.00f, 2.43f, 15.00f),  // 1F Mid Hallway
        new Vector3(-78.50f, 2.43f, 16.00f),  // 1F Lobby Alcove
        new Vector3(-84.00f, 2.43f, 24.00f),  // 1F North Hallway

        // 2nd Floor
        new Vector3(-84.00f, 8.35f, 4.00f),   // 2F South Hallway
        new Vector3(-84.00f, 8.35f, 16.00f),  // 2F Mid Hallway
        new Vector3(-84.00f, 8.35f, 28.00f),  // 2F North Hallway

        // 3rd Floor
        new Vector3(-84.00f, 14.34f, 2.00f),  // 3F South Hallway
        new Vector3(-84.00f, 14.34f, 16.00f), // 3F Mid Hallway
        new Vector3(-84.00f, 14.34f, 32.00f), // 3F North Hallway
    };

    private Coroutine _roamLoopCoroutine;
    private Camera _playerCam;
    private Transform _playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if UNITY_EDITOR
        if (vendorPrefab == null)
        {
            vendorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vendor_NaglalakoNgHamAndCheese.prefab");
        }
#endif
        Debug.Log("[HamAndCheeseVendorManager] Awake - initialized.");
    }

    private void Start()
    {
        FindPlayer();
        Debug.Log($"[HamAndCheeseVendorManager] Started - will spawn vendor in {initialSpawnDelay}s.");
        _roamLoopCoroutine = StartCoroutine(VendorRoamLifecycle());
    }

    private void FindPlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerCam = player.GetComponentInChildren<Camera>();
        }
        if (_playerCam == null) _playerCam = Camera.main;
    }

    private IEnumerator VendorRoamLifecycle()
    {
        // Initial delay before first appearance
        yield return new WaitForSeconds(initialSpawnDelay);

        while (true)
        {
            SpawnOrRelocateVendor();

            // Wait for duration or until purchase
            float timer = relocateInterval;
            while (timer > 0f)
            {
                if (activeVendor != null && activeVendor.HasBoughtThisEncounter)
                {
                    // Player bought! Wait a short grace period then roam to next spot with fresh stock
                    yield return new WaitForSeconds(postPurchaseRelocateDelay);
                    break;
                }

                timer -= Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(4.0f);
        }
    }

    public void SpawnOrRelocateVendor()
    {
        if (_playerTransform == null) FindPlayer();

        Vector3 targetSpot = PickBestSpawnSpot();

        if (activeVendor == null)
        {
            // Spawn vendor GameObject
            GameObject go = null;
            if (vendorPrefab != null)
            {
                go = Instantiate(vendorPrefab, targetSpot, Quaternion.identity);
            }
            else
            {
                go = ConstructVendorGameObject(targetSpot);
            }

            if (go != null)
            {
                activeVendor = go.GetComponent<HamAndCheeseVendor>();
            }
        }
        else
        {
            // Move existing vendor to new spot and reset stock
            activeVendor.transform.position = targetSpot;
            activeVendor.ResetStockForNewEncounter();
        }

        Debug.Log($"[HamAndCheeseVendorManager] Renz relocated to {targetSpot} with fresh Ham & Cheese stock!");
    }

    private Vector3 PickBestSpawnSpot()
    {
        if (_playerTransform == null)
            return _spawnSpots[Random.Range(0, _spawnSpots.Length)];

        Vector3 playerPos = _playerTransform.position;
        List<int> validIndices = new List<int>();

        for (int i = 0; i < _spawnSpots.Length; i++)
        {
            if (i == currentSpawnIndex) continue;

            Vector3 spot = _spawnSpots[i];
            float dist = Vector3.Distance(playerPos, spot);

            // Avoid spawning right on top of player (< 8m) or ridiculously far (> 45m)
            if (dist < 8.0f || dist > 45.0f) continue;

            // Check if spot is in direct camera line of sight (avoid popping into view)
            if (_playerCam != null)
            {
                Vector3 viewportPoint = _playerCam.WorldToViewportPoint(spot);
                bool inFrustum = viewportPoint.z > 0 &&
                                 viewportPoint.x >= 0.1f && viewportPoint.x <= 0.9f &&
                                 viewportPoint.y >= 0.1f && viewportPoint.y <= 0.9f;
                if (inFrustum && dist < 20.0f)
                {
                    continue; // Skip spot if player is directly looking at it
                }
            }

            validIndices.Add(i);
        }

        if (validIndices.Count > 0)
        {
            int chosen = validIndices[Random.Range(0, validIndices.Count)];
            currentSpawnIndex = chosen;
            return _spawnSpots[chosen];
        }

        // Fallback: pick any spot distinct from current
        int fallback = (currentSpawnIndex + 1) % _spawnSpots.Length;
        currentSpawnIndex = fallback;
        return _spawnSpots[fallback];
    }

    private GameObject ConstructVendorGameObject(Vector3 pos)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vendor_NaglalakoNgHamAndCheese.prefab");
        if (prefab != null)
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            return go;
        }
#endif
        var fallbackGO = new GameObject("Vendor_NaglalakoNgHamAndCheese");
        fallbackGO.transform.position = pos;
        fallbackGO.AddComponent<HamAndCheeseVendor>();
        return fallbackGO;
    }
}
