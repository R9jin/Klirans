using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based renderer and light culling for performance optimization.
/// Attach to the Player. Disables renderers and lights beyond a set distance,
/// re-enables them when the player approaches. Runs on a configurable interval
/// to avoid per-frame overhead.
/// </summary>
public class PerformanceCuller : MonoBehaviour
{
    [Header("Culling Distances")]
    [Tooltip("Renderers beyond this distance are disabled.")]
    public float renderCullDistance = 30f;

    [Tooltip("Lights beyond this distance are disabled.")]
    public float lightCullDistance = 18f;

    [Tooltip("How often (seconds) the culling sweep runs. Lower = more responsive but more CPU.")]
    public float cullInterval = 0.25f;

    [Header("Fog (Distance Depth Cue)")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.06f, 0.05f, 0.05f, 1f); // near-black horror fog
    public float fogDensity = 0.035f;
    public FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Camera Clip Plane")]
    [Tooltip("Far clip plane for the player camera. Lower = less overdraw.")]
    public float cameraFarClip = 90f;

    // Internal
    private Renderer[] _allRenderers;
    private Light[]    _allLights;
    private Camera     _playerCam;
    private float      _sqRenderDist;
    private float      _sqLightDist;

    private void Start()
    {
        // Cache everything at start (avoids per-frame FindObjectsOfType)
        RefreshCache();

        _sqRenderDist = renderCullDistance * renderCullDistance;
        _sqLightDist  = lightCullDistance  * lightCullDistance;

        // Apply fog
        if (enableFog)
        {
            RenderSettings.fog        = true;
            RenderSettings.fogColor   = fogColor;
            RenderSettings.fogMode    = fogMode;
            RenderSettings.fogDensity = fogDensity;
        }

        // Apply camera far clip
        _playerCam = GetComponentInChildren<Camera>();
        if (_playerCam == null) _playerCam = Camera.main;
        if (_playerCam != null) _playerCam.farClipPlane = cameraFarClip;

        StartCoroutine(CullRoutine());
    }

    /// <summary>Rebuild renderer/light lists (call after scene changes or room loads).</summary>
    public void RefreshCache()
    {
        _allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        _allLights    = FindObjectsByType<Light>(FindObjectsInactive.Include);
    }

    private IEnumerator CullRoutine()
    {
        var wait = new WaitForSeconds(cullInterval);
        while (true)
        {
            Vector3 playerPos = transform.position;
            CullRenderers(playerPos);
            CullLights(playerPos);
            yield return wait;
        }
    }

    private void CullRenderers(Vector3 playerPos)
    {
        foreach (var r in _allRenderers)
        {
            if (r == null) continue;

            // Never cull UI renderers or the player's own body
            if (r.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
            if (r.transform.IsChildOf(transform)) continue;

            float sqDist = (r.bounds.center - playerPos).sqrMagnitude;
            bool shouldBeEnabled = sqDist <= _sqRenderDist;

            if (r.enabled != shouldBeEnabled)
                r.enabled = shouldBeEnabled;
        }
    }

    private void CullLights(Vector3 playerPos)
    {
        foreach (var l in _allLights)
        {
            if (l == null) continue;
            // Don't cull directional lights (they're global)
            if (l.type == LightType.Directional) continue;

            float sqDist = (l.transform.position - playerPos).sqrMagnitude;
            bool shouldBeEnabled = sqDist <= _sqLightDist;

            if (l.enabled != shouldBeEnabled)
                l.enabled = shouldBeEnabled;
        }
    }

    private void OnDisable()
    {
        // Re-enable everything when culling is turned off
        if (_allRenderers != null)
            foreach (var r in _allRenderers)
                if (r != null) r.enabled = true;

        if (_allLights != null)
            foreach (var l in _allLights)
                if (l != null) l.enabled = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, renderCullDistance);
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, lightCullDistance);
    }
#endif
}
