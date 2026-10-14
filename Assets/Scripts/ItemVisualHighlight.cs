using UnityEngine;

/// <summary>
/// ItemVisualHighlight — Survival horror visual enhancement for collectible items.
/// 
/// Solves the issue where items become hard to spot in the dark and under the VHS/Pixelize filter,
/// while keeping them atmospheric, subtle, and realistic:
/// 
/// 1. MUCH DIMMER: Soft localized ambient glow (peaks at ~0.04-0.05 intensity, ~0.22m range)
///    that only softly lights the item and surface beneath it, not walls or ceilings.
/// 2. SLOW STROBE: Rhythmic, slow pulsation (cycle of 2.6s - 3.0s) that gently swells and fades,
///    acting like a faint phosphorescent beacon rather than a constant bright bulb.
/// 3. NOT OBVIOUS FROM AFAR: Beyond 3.5m, the light smoothly attenuates, and beyond 7.0m it is
///    100% culled and disabled so items cannot be spotted across hallways or from distant rooms.
/// 4. MICRO-SHIMMER: Tiny starburst sparkle synchronized with the slow strobe peak, billboarded
///    to camera, fading to zero with distance.
/// 5. IMMEDIATE VANISH: Instantly turns off and self-cleans on pickup.
/// </summary>
[DisallowMultipleComponent]
public class ItemVisualHighlight : MonoBehaviour
{
    public enum ItemCategory
    {
        AutoDetect,
        Coin,
        Key,
        DocumentOrPaper,
        ToolOrEquipment,
        Consumable
    }

    [Header("Item Category & Styling")]
    [Tooltip("Visual styling category. AutoDetect inspects attached components and names.")]
    public ItemCategory category = ItemCategory.AutoDetect;

    [Tooltip("Color of the ambient point light halo and sparkle shimmer.")]
    public Color glowColor = new Color(1.0f, 0.85f, 0.40f, 1.0f);

    [Header("Slow Strobe / Pulse Settings")]
    [Tooltip("Cycle period for the slow strobe in seconds (e.g. 2.6s - 3.2s).")]
    public float strobePeriod = 2.8f;

    [Tooltip("Minimum intensity at the trough of the strobe (subtle dim floor).")]
    public float strobeMinIntensity = 0.003f;

    [Tooltip("Maximum peak intensity at the crest of the slow strobe (dim, subtle glow).")]
    public float strobePeakIntensity = 0.046f;

    [Tooltip("Range of the soft localized point light.")]
    public float lightRange = 0.24f;

    [Header("Distance Fade (Hidden from afar)")]
    [Tooltip("Distance at which the light begins fading out.")]
    public float fadeStartDistance = 3.5f;

    [Tooltip("Distance beyond which the light is 100% culled and disabled.")]
    public float maxActiveDistance = 7.0f;

    [Header("Micro-Shimmer Glint")]
    [Tooltip("Maximum world-space size of the sparkle quad at peak pulse.")]
    public float sparkleScale = 0.024f;

    [Tooltip("Vertical height offset of the sparkle above the item.")]
    public float sparkleHeightOffset = 0.028f;

    // Backward-compatibility properties for existing scripts/inspectors
    public float lightBaseIntensity { get => strobePeakIntensity; set => strobePeakIntensity = value; }
    public float glintInterval { get => strobePeriod; set => strobePeriod = value; }
    public float glintDuration = 0.40f;
    public float lightGlintFlashBoost = 0.015f;

    // ── Internal State ────────────────────────────────────────────────────────
    private Light _light;
    private GameObject _sparkleGO;
    private Transform _sparkleTransform;
    private MeshRenderer _sparkleRenderer;
    private Material _sparkleMatInstance;

    private Transform _camTransform;
    private CoinPickup _coinPickup;
    private float _phaseOffset;
    private bool _isCulled = false;
    private bool _wasCoinCollected = false;
    private static Mesh _cachedQuadMesh;

    private void Awake()
    {
        _coinPickup = GetComponent<CoinPickup>() ?? GetComponentInParent<CoinPickup>();
        // Organic phase offset so multiple items in a room don't strobe in perfect unison
        _phaseOffset = Random.Range(0f, strobePeriod);

        AutoConfigureCategory();
        SetupLight();
        SetupSparkle();
    }

    private void Start()
    {
        FindCamera();
    }

    private void FindCamera()
    {
        if (_camTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null) _camTransform = cam.transform;
        }
    }

    private void AutoConfigureCategory()
    {
        if (category == ItemCategory.AutoDetect)
        {
            string objName = gameObject.name.ToLower();

            if (GetComponent<CoinPickup>() != null || GetComponentInParent<CoinPickup>() != null || objName.Contains("coin"))
            {
                category = ItemCategory.Coin;
            }
            else if (objName.Contains("key"))
            {
                category = ItemCategory.Key;
            }
            else if (GetComponent<FragmentPickup>() != null || GetComponent<PaperReamPickup>() != null ||
                     objName.Contains("paper") || objName.Contains("fragment") || objName.Contains("voucher") || objName.Contains("slip") || objName.Contains("note"))
            {
                category = ItemCategory.DocumentOrPaper;
            }
            else if (objName.Contains("flashlight") || objName.Contains("lockpin") || objName.Contains("tool") || objName.Contains("seal") || objName.Contains("stamp"))
            {
                category = ItemCategory.ToolOrEquipment;
            }
            else
            {
                category = ItemCategory.Consumable;
            }
        }

        switch (category)
        {
            case ItemCategory.Coin:
                glowColor = new Color(1.0f, 0.82f, 0.35f, 1.0f); // Warm coin amber
                lightRange = 0.20f;
                strobePeakIntensity = 0.038f;
                strobeMinIntensity = 0.003f;
                strobePeriod = 2.8f;
                sparkleScale = 0.020f;
                sparkleHeightOffset = 0.018f;
                break;
            case ItemCategory.Key:
                glowColor = new Color(1.0f, 0.84f, 0.30f, 1.0f); // Warm brass gold
                lightRange = 0.23f;
                strobePeakIntensity = 0.045f;
                strobeMinIntensity = 0.004f;
                strobePeriod = 2.6f;
                sparkleScale = 0.024f;
                sparkleHeightOffset = 0.028f;
                break;
            case ItemCategory.DocumentOrPaper:
                glowColor = new Color(0.85f, 0.94f, 1.0f, 1.0f); // Clean luminous paper white/cyan
                lightRange = 0.25f;
                strobePeakIntensity = 0.048f;
                strobeMinIntensity = 0.004f;
                strobePeriod = 2.9f;
                sparkleScale = 0.026f;
                sparkleHeightOffset = 0.032f;
                break;
            case ItemCategory.ToolOrEquipment:
                glowColor = new Color(0.92f, 0.96f, 1.0f, 1.0f); // Neutral steel/silver
                lightRange = 0.24f;
                strobePeakIntensity = 0.044f;
                strobeMinIntensity = 0.004f;
                strobePeriod = 2.7f;
                sparkleScale = 0.024f;
                sparkleHeightOffset = 0.028f;
                break;
            default:
                glowColor = new Color(1.0f, 0.88f, 0.65f, 1.0f);
                lightRange = 0.22f;
                strobePeakIntensity = 0.042f;
                strobeMinIntensity = 0.003f;
                strobePeriod = 2.8f;
                sparkleScale = 0.022f;
                sparkleHeightOffset = 0.024f;
                break;
        }
    }

    private void SetupLight()
    {
        Transform existingLightT = transform.Find("ItemGlintLight");
        GameObject lightGO;
        if (existingLightT != null)
        {
            lightGO = existingLightT.gameObject;
            _light = lightGO.GetComponent<Light>();
        }
        else
        {
            lightGO = new GameObject("ItemGlintLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = new Vector3(0f, sparkleHeightOffset * 0.8f, 0f);
            _light = lightGO.AddComponent<Light>();
        }

        if (_light != null)
        {
            _light.type = LightType.Point;
            _light.range = lightRange;
            _light.color = glowColor;
            _light.intensity = strobeMinIntensity;
            _light.shadows = LightShadows.None; // Zero shadow computation overhead
            _light.renderMode = LightRenderMode.Auto; // Subtle ambient blend
            _light.bounceIntensity = 0f;
        }
    }

    private void SetupSparkle()
    {
        Transform existingSparkleT = transform.Find("ItemGlintSparkle");
        if (existingSparkleT != null)
        {
            _sparkleGO = existingSparkleT.gameObject;
            _sparkleTransform = existingSparkleT;
            _sparkleRenderer = _sparkleGO.GetComponent<MeshRenderer>();
        }
        else
        {
            _sparkleGO = new GameObject("ItemGlintSparkle");
            _sparkleGO.transform.SetParent(transform, false);
            _sparkleGO.transform.localPosition = new Vector3(0f, sparkleHeightOffset, 0f);
            _sparkleGO.transform.localScale = Vector3.zero;
            _sparkleTransform = _sparkleGO.transform;

            var mf = _sparkleGO.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreateQuadMesh();

            _sparkleRenderer = _sparkleGO.AddComponent<MeshRenderer>();
        }

        // Load or create additive starburst material
        Material baseMat = null;
#if UNITY_EDITOR
        baseMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ItemGlint_Mat.mat");
#endif
        if (baseMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            baseMat = new Material(shader);
            baseMat.SetFloat("_Surface", 1.0f);
            baseMat.SetFloat("_Blend", 1.0f);
        }

        if (Application.isPlaying)
        {
            _sparkleMatInstance = new Material(baseMat);
            _sparkleMatInstance.name = "ItemGlint_Instanced";
            Color hdrColor = new Color(glowColor.r * 1.4f, glowColor.g * 1.4f, glowColor.b * 1.4f, 1.0f);
            _sparkleMatInstance.SetColor("_BaseColor", hdrColor);
            _sparkleRenderer.sharedMaterial = _sparkleMatInstance;
        }
        else
        {
            _sparkleRenderer.sharedMaterial = baseMat;
        }
        _sparkleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _sparkleRenderer.receiveShadows = false;
    }

    /// <summary>
    /// Explicit initialization invoked by editor setup scripts to configure light & sparkle parameters.
    /// </summary>
    public void InitializeInEditor()
    {
        AutoConfigureCategory();
        SetupLight();
        SetupSparkle();
    }

    private Mesh GetOrCreateQuadMesh()
    {
        if (_cachedQuadMesh != null) return _cachedQuadMesh;

        _cachedQuadMesh = new Mesh();
        _cachedQuadMesh.name = "Procedural_Glint_Quad";
        _cachedQuadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        _cachedQuadMesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        _cachedQuadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        _cachedQuadMesh.RecalculateNormals();
        return _cachedQuadMesh;
    }

    private void LateUpdate()
    {
        // 1. If parent CoinPickup is currently collected & waiting to respawn, keep hidden
        if (_coinPickup == null)
        {
            _coinPickup = GetComponent<CoinPickup>() ?? GetComponentInParent<CoinPickup>();
        }

        if (_coinPickup != null)
        {
            if (_coinPickup.IsCollected)
            {
                SetHighlightVisible(false);
                _wasCoinCollected = true;
                return;
            }
            else if (_wasCoinCollected)
            {
                _wasCoinCollected = false;
                if (!_isCulled) SetHighlightVisible(true);
            }
        }

        if (_camTransform == null)
        {
            FindCamera();
            if (_camTransform == null) return;
        }

        // 2. Distance check: prevents items from being spotted from afar
        float dist = Vector3.Distance(transform.position, _camTransform.position);
        if (dist >= maxActiveDistance)
        {
            if (!_isCulled)
            {
                SetHighlightVisible(false);
                _isCulled = true;
            }
            return;
        }

        if (_isCulled)
        {
            SetHighlightVisible(true);
            _isCulled = false;
        }

        // 3. Smooth distance attenuation:
        // Fully visible within fadeStartDistance (e.g. 3.5m), smoothly fades to 0 at maxActiveDistance (7.0m)
        float distanceFade = 1.0f;
        if (dist > fadeStartDistance)
        {
            float norm = (dist - fadeStartDistance) / Mathf.Max(0.1f, maxActiveDistance - fadeStartDistance);
            distanceFade = Mathf.SmoothStep(1.0f, 0.0f, Mathf.Clamp01(norm));
        }

        // 4. Slow, atmospheric strobe pulse:
        // Period of ~2.8s: long, quiet resting floor -> slow smooth swell -> subtle peak -> gentle fade
        float cycle = (Time.time + _phaseOffset) / Mathf.Max(strobePeriod, 0.5f);
        float sinVal = Mathf.Sin(cycle * Mathf.PI * 2f);
        // Normalized power curve (2.2 power keeps it dim most of the time with a soft slow crest)
        float pulse01 = Mathf.Pow(Mathf.Clamp01(sinVal * 0.5f + 0.5f), 2.2f);

        float currentLightIntensity = Mathf.Lerp(strobeMinIntensity, strobePeakIntensity, pulse01) * distanceFade;

        // Apply light intensity
        if (_light != null && _light.enabled)
        {
            _light.intensity = currentLightIntensity;
        }

        // Subtle micro-shimmer scale billboarded to camera
        if (_sparkleTransform != null && _sparkleRenderer != null)
        {
            float curScale = sparkleScale * pulse01 * distanceFade;
            if (curScale > 0.001f)
            {
                _sparkleRenderer.enabled = true;
                _sparkleTransform.localScale = new Vector3(curScale, curScale, curScale);
                _sparkleTransform.rotation = Quaternion.LookRotation(_camTransform.forward, _camTransform.up);
            }
            else
            {
                _sparkleRenderer.enabled = false;
                _sparkleTransform.localScale = Vector3.zero;
            }
        }
    }

    public void SetHighlightVisible(bool visible)
    {
        if (_light != null) _light.enabled = visible;
        if (_sparkleRenderer != null) _sparkleRenderer.enabled = visible;
        if (!visible && _sparkleTransform != null) _sparkleTransform.localScale = Vector3.zero;
    }

    /// <summary>
    /// Explicitly called upon item collection to ensure lights and sparkles vanish immediately and stay off.
    /// </summary>
    public void OnPickedUp()
    {
        SetHighlightVisible(false);
        if (_light != null) _light.enabled = false;
        if (_sparkleRenderer != null) _sparkleRenderer.enabled = false;
        enabled = false;

        // Clean up child highlight objects immediately
        if (_sparkleGO != null) Destroy(_sparkleGO);
        Transform glintLightT = transform.Find("ItemGlintLight");
        if (glintLightT != null) Destroy(glintLightT.gameObject);
    }

    private void OnDisable()
    {
        SetHighlightVisible(false);
    }

    private void OnEnable()
    {
        SetHighlightVisible(true);
    }

    private void OnDestroy()
    {
        if (_sparkleMatInstance != null)
        {
            Destroy(_sparkleMatInstance);
        }
    }
}
