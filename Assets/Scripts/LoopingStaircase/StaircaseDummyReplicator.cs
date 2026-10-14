using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// StaircaseDummyReplicator — Psychological Liminal Staircase Illusion
/// 
/// Replicates the player's movement, animations, facing direction, and flashlight
/// across three identical floors (Floor 1, Floor 2, Floor 3) in the active stairwell.
/// 
/// KEY DESIGN PRINCIPLES:
/// 1. Floor Invariance:
///    The dummy position on floor F is computed as:
///      Y_dummy = floorBaseY + (F - 1) * floorHeight + ((player.y - floorBaseY) % floorHeight)
///    Because this depends only on the relative height within the flight (modulo 6m),
///    when the player teleports between floors (e.g. 3F -> 1F or 2F -> 1F), none of the
///    dummies jump, pop, or reset! The player seamlessly swaps places with the destination dummy.
/// 
/// 2. Active Floor Concealment:
///    The dummy on the player's current floor is deactivated to prevent self-collision or clipping.
///    The dummies on the other two floors remain visible above and below.
/// 
/// 3. Audio & Visual Parity:
///    Animators mirror Idle / Walk / Run locomotion.
///    Flashlight spotlights replicate state and camera pitch.
///    Faint 3D spatial footstep echoes emanate from other floors.
/// </summary>
public class StaircaseDummyReplicator : MonoBehaviour
{
    public static StaircaseDummyReplicator Instance { get; private set; }

    [Header("Floor Geometry")]
    [Tooltip("Base Y coordinate of Floor 1 stairs entrance (default ~2.35m).")]
    public float floorBaseY = 2.35f;

    [Tooltip("Exact vertical distance between floors (default 6.00m).")]
    public float floorHeight = 6.00f;

    [Tooltip("Total number of floors with replicated stairwells.")]
    public int floorCount = 3;

    [Header("Stairwell Bounding Boxes")]
    public Vector3 mainStairsMin = new Vector3(-93.5f, 1.5f, 8.0f);
    public Vector3 mainStairsMax = new Vector3(-83.5f, 23.0f, 25.5f);

    public Vector3 leftStairsMin = new Vector3(-93.5f, 1.5f, -19.5f);
    public Vector3 leftStairsMax = new Vector3(-83.5f, 23.0f, -13.0f);

    public Vector3 rightStairsMin = new Vector3(-93.5f, 1.5f, 46.5f);
    public Vector3 rightStairsMax = new Vector3(-83.5f, 23.0f, 52.5f);

    [Header("Player References")]
    public Transform playerTransform;
    public PlayerMovement playerMovement;
    public Camera playerCamera;
    public Light playerFlashlight;

    [Header("Dummy Asset")]
    [Tooltip("Prefab instantiated for each floor dummy.")]
    public GameObject dummyPrefab;

    [Tooltip("Keep dummies visually invisible. Teleportation and spatial tracking work seamlessly as invisible body anchors.")]
    public bool makeDummiesInvisible = true;

    [Header("Audio & Flashlight Features")]
    public bool enableFootstepEchoes = true;
    [Range(0f, 1f)] public float footstepEchoVolume = 0.25f;
    public bool syncFlashlight = true;

    [Serializable]
    public class ReplicatorDummy
    {
        public string stairwellID;
        public int floorNumber; // 1, 2, 3
        public GameObject instance;
        public Animator animator;
        public Light flashlight;
        public AudioSource audioSource;
    }

    [Header("Managed Dummies (Auto-populated or Inspector assigned)")]
    public List<ReplicatorDummy> dummies = new List<ReplicatorDummy>();

    // Runtime state
    private string currentStairwell = null;
    private int currentPlayerFloor = 1;
    private float smoothedSpeed = 0f;

    private static readonly string[] StairwellIDs = new string[] { "MainStairs", "LeftStairs", "RightStairs" };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoFindPlayerReferences();
        EnsureDummiesExist();
    }

    private void Start()
    {
        AutoFindPlayerReferences();
        EnsureDummiesExist();
    }

    public void AutoFindPlayerReferences()
    {
        if (playerTransform == null)
        {
            var pObj = GameObject.FindWithTag("Player");
            if (pObj != null) playerTransform = pObj.transform;
        }

        if (playerTransform != null)
        {
            if (playerMovement == null)
                playerMovement = playerTransform.GetComponent<PlayerMovement>();

            if (playerCamera == null)
                playerCamera = playerTransform.GetComponentInChildren<Camera>();

            if (playerFlashlight == null)
            {
                var lights = playerTransform.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Spot)
                    {
                        playerFlashlight = l;
                        break;
                    }
                }
            }
        }
    }

    public void EnsureDummiesExist()
    {
        if (dummies != null && dummies.Count >= 9)
        {
            // Verify existing instances
            bool allValid = true;
            foreach (var d in dummies)
            {
                if (d.instance == null) { allValid = false; break; }
            }
            if (allValid) return;
        }

        if (dummyPrefab == null)
        {
#if UNITY_EDITOR
            dummyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StaircasePlayerDummy.prefab");
#endif
            if (dummyPrefab == null)
            {
                Debug.LogWarning("[StaircaseDummyReplicator] dummyPrefab is null and could not be loaded!");
                return;
            }
        }

        if (dummies == null) dummies = new List<ReplicatorDummy>();

        Transform container = transform.Find("[StaircaseDummies]");
        if (container == null)
        {
            var cGO = new GameObject("[StaircaseDummies]");
            cGO.transform.SetParent(transform, false);
            container = cGO.transform;
        }

        foreach (string sId in StairwellIDs)
        {
            for (int f = 1; f <= floorCount; f++)
            {
                ReplicatorDummy existing = dummies.Find(d => d.stairwellID == sId && d.floorNumber == f);
                if (existing != null && existing.instance != null) continue;

                string dName = $"Dummy_{sId}_Floor{f}";
                Transform existingChild = container.Find(dName);
                GameObject dGO = existingChild != null ? existingChild.gameObject : null;

                if (dGO == null)
                {
                    dGO = Instantiate(dummyPrefab, container);
                    dGO.name = dName;
                }

                dGO.layer = 2; // Ignore Raycast
                foreach (var t in dGO.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = 2;
                }

                var dummy = new ReplicatorDummy
                {
                    stairwellID = sId,
                    floorNumber = f,
                    instance = dGO,
                    animator = dGO.GetComponentInChildren<Animator>(),
                    audioSource = dGO.GetComponentInChildren<AudioSource>()
                };

                var spot = dGO.GetComponentInChildren<Light>(true);
                dummy.flashlight = spot;

                if (existing != null)
                {
                    dummies.Remove(existing);
                }
                dummies.Add(dummy);
                dGO.SetActive(false);
            }
        }

        Debug.Log($"[StaircaseDummyReplicator] {dummies.Count} staircase dummies ready across {StairwellIDs.Length} stairwells.");
    }

    private void LateUpdate()
    {
        if (playerTransform == null)
        {
            AutoFindPlayerReferences();
            if (playerTransform == null) return;
        }

        Vector3 pPos = playerTransform.position;
        currentStairwell = DetermineStairwell(pPos);

        if (string.IsNullOrEmpty(currentStairwell))
        {
            // Player is outside all stairwells — conceal all dummies
            ConcealAllDummies();
            return;
        }

        UpdatePlayerFloor(pPos.y);
        SynchronizeDummies(pPos);
    }

    private string DetermineStairwell(Vector3 pos)
    {
        if (pos.x >= mainStairsMin.x && pos.x <= mainStairsMax.x &&
            pos.y >= mainStairsMin.y && pos.y <= mainStairsMax.y &&
            pos.z >= mainStairsMin.z && pos.z <= mainStairsMax.z)
            return "MainStairs";

        if (pos.x >= leftStairsMin.x && pos.x <= leftStairsMax.x &&
            pos.y >= leftStairsMin.y && pos.y <= leftStairsMax.y &&
            pos.z >= leftStairsMin.z && pos.z <= leftStairsMax.z)
            return "LeftStairs";

        if (pos.x >= rightStairsMin.x && pos.x <= rightStairsMax.x &&
            pos.y >= rightStairsMin.y && pos.y <= rightStairsMax.y &&
            pos.z >= rightStairsMin.z && pos.z <= rightStairsMax.z)
            return "RightStairs";

        return null;
    }

    private void UpdatePlayerFloor(float pY)
    {
        // 0.05m hysteresis prevents borderline threshold jitter
        float threshold12 = floorBaseY + floorHeight;        // 8.35m
        float threshold23 = floorBaseY + 2f * floorHeight;   // 14.35m

        if (pY >= threshold23 + 0.05f)
        {
            currentPlayerFloor = 3;
        }
        else if (pY <= threshold12 - 0.05f)
        {
            currentPlayerFloor = 1;
        }
        else
        {
            // Between thresholds [8.30m, 14.40m]
            if (currentPlayerFloor == 1 && pY >= threshold12 + 0.05f) currentPlayerFloor = 2;
            else if (currentPlayerFloor == 3 && pY <= threshold23 - 0.05f) currentPlayerFloor = 2;
            else if (currentPlayerFloor != 2) currentPlayerFloor = 2;
        }
    }

    private void SynchronizeDummies(Vector3 pPos)
    {
        // Compute relative height within the standard flight:
        float relY = Mathf.Repeat(pPos.y - floorBaseY, floorHeight);
        Quaternion pRot = Quaternion.Euler(0f, playerTransform.eulerAngles.y, 0f);

        // Movement speed for animation blend tree (0 = idle, 1 = walk, 2 = run)
        float targetSpeed = 0f;
        if (playerMovement != null)
        {
            if (playerMovement.IsRunning) targetSpeed = 2f;
            else if (playerMovement.IsMoving) targetSpeed = 1f;
        }
        else
        {
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                float vMag = cc.velocity.magnitude;
                if (vMag > 4.5f) targetSpeed = 2f;
                else if (vMag > 0.15f) targetSpeed = 1f;
            }
        }
        smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, targetSpeed, Time.deltaTime * 15f);

        // Flashlight status
        bool isFlashOn = false;
        if (FlashlightController.Instance != null)
        {
            isFlashOn = FlashlightController.Instance.IsLightOn;
        }
        else if (playerFlashlight != null)
        {
            isFlashOn = playerFlashlight.enabled && playerFlashlight.intensity > 0.01f;
        }

        float camPitch = playerCamera != null ? playerCamera.transform.localEulerAngles.x : 0f;

        for (int i = 0; i < dummies.Count; i++)
        {
            ReplicatorDummy d = dummies[i];
            if (d == null || d.instance == null) continue;

            bool isThisStairwell = d.stairwellID == currentStairwell;
            bool isPlayerFloor = d.floorNumber == currentPlayerFloor;

            // Show dummy ONLY if in active stairwell AND NOT on the player's current floor
            if (isThisStairwell && !isPlayerFloor)
            {
                if (!d.instance.activeSelf) d.instance.SetActive(true);

                // Target position on floor d.floorNumber
                float targetY = floorBaseY + (d.floorNumber - 1) * floorHeight + relY;
                Vector3 targetPos = new Vector3(pPos.x, targetY, pPos.z);

                d.instance.transform.position = targetPos;
                d.instance.transform.rotation = pRot;

                // Visibility toggle: keep dummies invisible anchors
                var rends = d.instance.GetComponentsInChildren<Renderer>(true);
                for (int rIdx = 0; rIdx < rends.Length; rIdx++)
                {
                    if (rends[rIdx] != null && rends[rIdx].enabled == makeDummiesInvisible)
                    {
                        rends[rIdx].enabled = !makeDummiesInvisible;
                    }
                }

                // Sync locomotion animator
                if (d.animator != null)
                {
                    d.animator.SetFloat("Speed", smoothedSpeed);
                }

                // Sync flashlight (disabled if dummies are invisible)
                if (d.flashlight != null)
                {
                    d.flashlight.enabled = !makeDummiesInvisible && syncFlashlight && isFlashOn;
                    if (d.flashlight.enabled)
                    {
                        d.flashlight.transform.localEulerAngles = new Vector3(camPitch, 0f, 0f);
                    }
                }

                // Sync 3D footstep echoes
                if (d.audioSource != null && enableFootstepEchoes)
                {
                    d.audioSource.volume = footstepEchoVolume;
                    if (smoothedSpeed > 0.1f)
                    {
                        d.audioSource.pitch = smoothedSpeed > 1.5f ? 1.35f : 1.0f;
                        if (!d.audioSource.isPlaying) d.audioSource.Play();
                    }
                    else
                    {
                        if (d.audioSource.isPlaying) d.audioSource.Stop();
                    }
                }
            }
            else
            {
                if (d.instance.activeSelf)
                {
                    if (d.audioSource != null && d.audioSource.isPlaying) d.audioSource.Stop();
                    d.instance.SetActive(false);
                }
            }
        }
    }

    private void ConcealAllDummies()
    {
        for (int i = 0; i < dummies.Count; i++)
        {
            var d = dummies[i];
            if (d != null && d.instance != null && d.instance.activeSelf)
            {
                if (d.audioSource != null && d.audioSource.isPlaying) d.audioSource.Stop();
                d.instance.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Called immediately when LoopingStaircaseSystem performs a seamless teleport.
    /// Resets the current floor without delay so dummy visibility switches seamlessly in that exact frame.
    /// </summary>
    public void NotifyTeleport(int fromFloor, int toFloor)
    {
        currentPlayerFloor = Mathf.Clamp(toFloor, 1, floorCount);
        if (playerTransform != null)
        {
            SynchronizeDummies(playerTransform.position);
        }
        Debug.Log($"[StaircaseDummyReplicator] Synchronized teleport F{fromFloor} -> F{toFloor}. Active floor dummy swapped.");
    }
}
