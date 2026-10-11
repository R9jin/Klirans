using UnityEngine;

/// <summary>
/// ItemVisualHighlight — Survival horror visual enhancement for collectible items.
/// Solves the issue where items become hard to spot in the dark and under the VHS/Pixelize filter.
/// 
/// Features:
/// 1. Resident Evil / Silent Hill style periodic starburst sparkle glint (blooms cleanly through pixel filters).
/// 2. Soft, atmospheric localized point light halo that illuminates the item and table/floor beneath it.
/// 3. Dynamic distance culling (sleeps when player is > 25m away for zero performance impact).
/// 4. Blackout-immune (lights stay visible in the dark during campus power cuts).
/// 5. Automatically adapts color and scale per item type (coins, keys, papers, lockpins, tools).
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

    [Header("Glint Sparkle Flare")]
    [Tooltip("Seconds between starburst sparkle glints.")]
    public float glintInterval = 2.4f;

    [Tooltip("Duration of a single sparkle flash (seconds).")]
    public float glintDuration = 0.42f;

    [Tooltip("Maximum world-space size of the sparkle quad at peak flash.")]
    public float sparkleScale = 0.22f;

    [Tooltip("Vertical height offset of the sparkle above the item.")]
    public float sparkleHeightOffset = 0.05f;

    [Header("Atmospheric Halo Light")]
    [Tooltip("Range of the soft localized point light.")]
    public float lightRange = 1.15f;

    [Tooltip("Base intensity of the soft ambient light.")]
    public float lightBaseIntensity = 0.65f;

    [Tooltip("Peak extra light intensity added during the glint flash.")]
    public float lightGlintFlashBoost = 0.85f;

    [Header("Optimization")]
    [Tooltip("Distance from player camera beyond which the light and sparkle are culled.")]
    public float maxActiveDistance = 25.0f;

    // ── Internal State ────────────────────────────────────────────────────────
    private Light _light;
    private GameObject _sparkleGO;
    private Transform _sparkleTransform;
    private MeshRenderer _sparkleRenderer;
    private Material _sparkleMatInstance;

    private Transform _camTransform;
    private CoinPickup _coinPickup;
    private float _phaseOffset;
    private float _nextGlintTime;
    private bool _isGlinting = false;
    private float _glintStartTime;
    private bool _isCulled = false;
    private bool _wasCoinCollected = false;
    private static Mesh _cachedQuadMesh;

    private void Awake()
    {
        _coinPickup = GetComponent<CoinPickup>() ?? GetComponentInParent<CoinPickup>();
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        _nextGlintTime = Time.time + Random.Range(0.2f, glintInterval);

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
        if (category != ItemCategory.AutoDetect) return;

        string objName = gameObject.name.ToLower();

        if (GetComponent<CoinPickup>() != null || GetComponentInParent<CoinPickup>() != null || objName.Contains("coin"))
        {
            category = ItemCategory.Coin;
            glowColor = new Color(1.0f, 0.86f, 0.42f, 1.0f);
            lightRange = 0.95f;
            lightBaseIntensity = 0.55f;
            sparkleScale = 0.17f;
            sparkleHeightOffset = 0.035f;
            glintInterval = 2.6f;
        }
        else if (objName.Contains("key"))
        {
            category = ItemCategory.Key;
            glowColor = new Color(1.0f, 0.84f, 0.32f, 1.0f); // Warm brass gold
            lightRange = 1.25f;
            lightBaseIntensity = 0.70f;
            sparkleScale = 0.24f;
            sparkleHeightOffset = 0.05f;
            glintInterval = 2.1f;
        }
        else if (GetComponent<FragmentPickup>() != null || GetComponent<PaperReamPickup>() != null ||
                 objName.Contains("paper") || objName.Contains("fragment") || objName.Contains("voucher") || objName.Contains("slip"))
        {
            category = ItemCategory.DocumentOrPaper;
            glowColor = new Color(0.85f, 0.94f, 1.0f, 1.0f); // Clean luminous paper white/cyan
            lightRange = 1.45f;
            lightBaseIntensity = 0.80f;
            sparkleScale = 0.28f;
            sparkleHeightOffset = 0.06f;
            glintInterval = 2.0f;
        }
        else if (objName.Contains("flashlight") || objName.Contains("lockpin") || objName.Contains("tool"))
        {
            category = ItemCategory.ToolOrEquipment;
            glowColor = new Color(0.92f, 0.96f, 1.0f, 1.0f);
            lightRange = 1.25f;
            lightBaseIntensity = 0.70f;
            sparkleScale = 0.23f;
            sparkleHeightOffset = 0.05f;
            glintInterval = 2.2f;
        }
        else
        {
            category = ItemCategory.Consumable;
            glowColor = new Color(1.0f, 0.88f, 0.65f, 1.0f);
            lightRange = 1.15f;
            lightBaseIntensity = 0.65f;
            sparkleScale = 0.22f;
            sparkleHeightOffset = 0.05f;
            glintInterval = 2.3f;
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
            _light.intensity = lightBaseIntensity;
            _light.shadows = LightShadows.None; // Zero shadow computation overhead
            _light.renderMode = LightRenderMode.ForcePixel; // Clean consistent illumination in URP
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
            Color hdrColor = new Color(glowColor.r * 1.6f, glowColor.g * 1.6f, glowColor.b * 1.6f, 1.0f);
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
        if (_coinPickup != null)
        {
            if (_coinPickup.IsCollected)
            {
                if (!_wasCoinCollected)
                {
                    SetHighlightVisible(false);
                    _wasCoinCollected = true;
                }
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

        // 2. Distance culling check
        float distSqr = (transform.position - _camTransform.position).sqrMagnitude;
        if (distSqr > maxActiveDistance * maxActiveDistance)
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

        // 3. Gentle ambient light breathing
        float breathing = 0.88f + 0.12f * Mathf.Sin(Time.time * 2.5f + _phaseOffset);
        float currentLightIntensity = lightBaseIntensity * breathing;

        // 4. Glint Sparkle Lifecycle
        if (!_isGlinting && Time.time >= _nextGlintTime)
        {
            _isGlinting = true;
            _glintStartTime = Time.time;
            _nextGlintTime = Time.time + glintInterval + Random.Range(-0.25f, 0.25f);
        }

        if (_isGlinting)
        {
            float elapsed = Time.time - _glintStartTime;
            if (elapsed >= glintDuration)
            {
                _isGlinting = false;
                if (_sparkleTransform != null) _sparkleTransform.localScale = Vector3.zero;
            }
            else
            {
                float prog = elapsed / glintDuration;
                // Smooth sine bell curve (0 -> 1 -> 0)
                float bell = Mathf.Sin(prog * Mathf.PI);

                // Ease scale
                float curScale = sparkleScale * bell;
                if (_sparkleTransform != null)
                {
                    _sparkleTransform.localScale = new Vector3(curScale, curScale, curScale);
                    // Billboard facing camera
                    _sparkleTransform.rotation = Quaternion.LookRotation(_camTransform.forward, _camTransform.up);
                }

                // Flash light intensity boost
                currentLightIntensity += lightGlintFlashBoost * bell;
            }
        }

        // Apply light intensity
        if (_light != null && _light.enabled)
        {
            _light.intensity = currentLightIntensity;
        }
    }

    public void SetHighlightVisible(bool visible)
    {
        if (_light != null) _light.enabled = visible;
        if (_sparkleRenderer != null) _sparkleRenderer.enabled = visible;
        if (!visible && _sparkleTransform != null) _sparkleTransform.localScale = Vector3.zero;
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
