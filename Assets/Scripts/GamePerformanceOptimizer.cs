using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// High-performance rendering distance, light culler, and memory optimizer.
/// Solves gameplay stutter and memory pressure:
/// 1. Lowers camera farClipPlane to 42m (down from 1000m) so GPU ignores distant geometry.
/// 2. Applies atmospheric horror fog so the 35-42m boundary fades seamlessly into darkness.
/// 3. Zero-allocation floor and light culling (disables lights and room details on distant floors/corridors).
/// 4. Optimizes URP shadow distance to prevent shadow map thrashing.
/// </summary>
public class GamePerformanceOptimizer : MonoBehaviour
{
    public static GamePerformanceOptimizer Instance { get; private set; }

    [Header("Camera & Fog Distance")]
    [Tooltip("Maximum camera viewing distance. Clips geometry beyond this distance.")]
    public float renderDistance = 42f;
    public float nearClip = 0.08f;

    [Tooltip("Fog color blending distant geometry into darkness.")]
    public Color fogColor = new Color(0.04f, 0.035f, 0.04f, 1f);
    public float fogDensity = 0.032f;

    [Header("Dynamic Light Culling")]
    [Tooltip("Horizontal distance beyond which lights are disabled.")]
    public float lightCullHorizontalDist = 22f;

    [Tooltip("Vertical distance beyond which lights on other floors are disabled.")]
    public float lightCullVerticalDist = 5.0f;

    [Tooltip("Interval in seconds between culling updates.")]
    public float cullingInterval = 0.35f;

    [Header("Shadow Distance")]
    public float optimizedShadowDistance = 22f;

    // Cache
    private Transform _playerTransform;
    private Camera _playerCamera;
    private Light[] _allLights;
    private Vector3[] _lightPositions;
    private bool[] _isLightDirectional;
    private int _lightCount;
    private float _sqrHorizDist;

    // Floor root transforms for multi-story culling
    private GameObject _rooms1F;
    private GameObject _rooms2F;
    private GameObject _rooms3F;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Enforce smooth 60 FPS pacing and prevent unbounded GPU thermal throttling in standalone builds
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        _sqrHorizDist = lightCullHorizontalDist * lightCullHorizontalDist;

        // Find Player
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) playerGO = GameObject.Find("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;
        else _playerTransform = transform;

        // Find Camera
        _playerCamera = Camera.main;
        if (_playerCamera == null && playerGO != null)
            _playerCamera = playerGO.GetComponentInChildren<Camera>();

        // Apply Camera Distance & Near Clip
        if (_playerCamera != null)
        {
            _playerCamera.farClipPlane = renderDistance;
            _playerCamera.nearClipPlane = nearClip;
        }

        // Apply Fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        // Optimize URP Shadow Distance
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.shadowDistance = optimizedShadowDistance;
        }

        // Cache Floor roots
        _rooms1F = GameObject.Find("Rooms/1stFloor");
        _rooms2F = GameObject.Find("Rooms/2ndFloor");
        _rooms3F = GameObject.Find("Rooms/3rdFloor");

        // Cache all lights in scene
        CacheLights();

        // Start culling loop
        StartCoroutine(CullingLoop());
    }

    /// <summary>
    /// Pre-caches light positions to avoid transform access during culling loops.
    /// </summary>
    public void CacheLights()
    {
        var rawLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        if (rawLights == null) return;

        var list = new System.Collections.Generic.List<Light>(rawLights.Length);
        for (int i = 0; i < rawLights.Length; i++)
        {
            var l = rawLights[i];
            if (l == null) continue;

            // Punctual point/spot lights in the school do not need heavy geometric shadows (e.g. 6-pass cubemaps)
            // which thrash the shadow atlas and cause buffer overflows
            if (l.type != LightType.Directional && l.gameObject.name != "FlashlightLight")
            {
                l.shadows = LightShadows.None;
            }

            if (l.gameObject.name != "FlashlightLight")
            {
                list.Add(l);
            }
        }

        _allLights = list.ToArray();
        _lightCount = _allLights.Length;
        _lightPositions = new Vector3[_lightCount];
        _isLightDirectional = new bool[_lightCount];

        for (int i = 0; i < _lightCount; i++)
        {
            if (_allLights[i] != null)
            {
                _lightPositions[i] = _allLights[i].transform.position;
                _isLightDirectional[i] = (_allLights[i].type == LightType.Directional);
            }
        }
    }

    private IEnumerator CullingLoop()
    {
        var wait = new WaitForSeconds(cullingInterval);

        while (true)
        {
            if (_playerTransform != null)
            {
                Vector3 pPos = _playerTransform.position;
                float pX = pPos.x;
                float pY = pPos.y;
                float pZ = pPos.z;

                // 1. Light Culling
                // Skip light culling while BlackoutManager has a blackout active —
                // BlackoutManager exclusively controls light state during blackouts.
                bool blackoutActive = BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive;
                if (!blackoutActive)
                {
                    for (int i = 0; i < _lightCount; i++)
                    {
                        Light l = _allLights[i];
                        if (l == null) continue;

                        // Directional lights are global (never cull)
                        if (_isLightDirectional[i]) continue;

                        Vector3 lPos = _lightPositions[i];

                        // Floor check (vertical distance)
                        float dy = Mathf.Abs(lPos.y - pY);
                        if (dy > lightCullVerticalDist)
                        {
                            if (l.enabled) l.enabled = false;
                            continue;
                        }

                        // Horizontal distance check
                        float dx = lPos.x - pX;
                        float dz = lPos.z - pZ;
                        float sqrDist = (dx * dx) + (dz * dz);

                        bool shouldEnable = (sqrDist <= _sqrHorizDist);
                        if (l.enabled != shouldEnable)
                        {
                            l.enabled = shouldEnable;
                        }
                    }
                }

                // 2. Distant Floor Object Culling
                // Floor 1: Y ~ 2 to 5.5
                // Floor 2: Y ~ 5.5 to 11.5
                // Floor 3: Y ~ 11.5 to 18
                if (pY < 5.5f) // On Floor 1
                {
                    // Deactivate Floor 3 rooms (far above concrete ceiling)
                    if (_rooms3F != null && _rooms3F.activeSelf) _rooms3F.SetActive(false);
                    if (_rooms1F != null && !_rooms1F.activeSelf) _rooms1F.SetActive(true);
                }
                else if (pY > 11.5f) // On Floor 3
                {
                    // Deactivate Floor 1 rooms (far below concrete floor)
                    if (_rooms1F != null && _rooms1F.activeSelf) _rooms1F.SetActive(false);
                    if (_rooms3F != null && !_rooms3F.activeSelf) _rooms3F.SetActive(true);
                }
                else // On Floor 2 / Stairwells
                {
                    // Keep all floors available when in transit
                    if (_rooms1F != null && !_rooms1F.activeSelf) _rooms1F.SetActive(true);
                    if (_rooms3F != null && !_rooms3F.activeSelf) _rooms3F.SetActive(true);
                }
            }

            yield return wait;
        }
    }

    private void OnDisable()
    {
        // Re-enable all lights on exit / disable
        if (_allLights != null)
        {
            for (int i = 0; i < _lightCount; i++)
            {
                if (_allLights[i] != null) _allLights[i].enabled = true;
            }
        }

        if (_rooms1F != null) _rooms1F.SetActive(true);
        if (_rooms2F != null) _rooms2F.SetActive(true);
        if (_rooms3F != null) _rooms3F.SetActive(true);
    }
}
