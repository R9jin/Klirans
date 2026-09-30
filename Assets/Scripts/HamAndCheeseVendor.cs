using System.Collections;
using UnityEngine;

/// <summary>
/// HamAndCheeseVendor — The roaming student vendor ("Naglalako ng Ham and Cheese")
/// who sells toasted sandwiches to students struggling with clearance stress and anxiety.
/// Carries an authentic Philippine styrofoam ice chest ("Polar Ice Chest") labeled "HAM & CHEESE"
/// with the sticker plastered completely flat against the box (no floating, no backwards text),
/// natural arm holding pose, and dynamic masking tape price tag.
/// </summary>
public class HamAndCheeseVendor : MonoBehaviour, IInteractable
{
    public static int TotalPurchasesAcrossRuns = 0;

    [Header("Item Asset")]
    public InventoryItem sandwichItemAsset;

    [Header("Pricing Escalation")]
    [Tooltip("Base price for the first sandwich purchase in coins.")]
    public int basePrice = 3;

    [Tooltip("Price increment per completed purchase.")]
    public int priceIncrement = 2;

    [Header("Audio")]
    public AudioClip vendorGreetingClip;

    [Header("State")]
    public bool hasBoughtThisEncounter = false;

    [Header("Textures")]
    public Texture2D styrofoamTex;
    public Texture2D stickerTex;
    public Texture2D priceTag3Tex;
    public Texture2D priceTag5Tex;
    public Texture2D priceTag8Tex;
    public Texture2D priceTag12Tex;
    public Texture2D priceTagSoldOutTex;

    private Transform _playerTransform;
    private AudioSource _audioSource;
    private GameObject _styrofoamBoxProp;
    private MeshRenderer _priceTagRenderer;

    // Bone references for procedural holding pose
    private Transform _spineBone;
    private Transform _leftArm;
    private Transform _leftForeArm;
    private Transform _leftHand;
    private Transform _rightArm;
    private Transform _rightForeArm;
    private Transform _rightHand;
    private bool _hasBones = false;

    public int CurrentPrice
    {
        get
        {
            if (TotalPurchasesAcrossRuns == 0) return basePrice;
            if (TotalPurchasesAcrossRuns == 1) return basePrice + priceIncrement;
            if (TotalPurchasesAcrossRuns == 2) return basePrice + priceIncrement + 3;
            return basePrice + priceIncrement + 3 + (TotalPurchasesAcrossRuns - 2) * 4;
        }
    }

    public bool HasBoughtThisEncounter => hasBoughtThisEncounter;

    private void Awake()
    {
        _audioSource = gameObject.GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1.0f;
        _audioSource.minDistance = 2.0f;
        _audioSource.maxDistance = 14.0f;
        _audioSource.playOnAwake = false;

        var col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.68f;
            col.radius = 0.32f;
            col.center = new Vector3(0f, 0.84f, 0f);
        }

        LoadTexturesAndAssets();
        CacheBoneReferences();
        BuildStyrofoamBoxProp();
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _playerTransform = player.transform;

        UpdateBoxLabel();
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            return;
        }

        // Smoothly rotate to face player when player is nearby (< 6m)
        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        if (dist < 6.0f)
        {
            Vector3 lookDir = _playerTransform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
            }
        }
    }

    private void LateUpdate()
    {
        // Procedural arm holding pose: hold the styrofoam cooler naturally
        if (_hasBones)
        {
            ApplyArmHoldingPose();
        }
    }

    private void CacheBoneReferences()
    {
        var hips = transform.Find("Armature/mixamorig:Hips");
        if (hips != null)
        {
            _spineBone = hips.Find("mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
            if (_spineBone == null) _spineBone = hips.Find("mixamorig:Spine");

            _leftArm = transform.Find("Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm");
            if (_leftArm != null)
            {
                _leftForeArm = _leftArm.Find("mixamorig:LeftForeArm");
                if (_leftForeArm != null) _leftHand = _leftForeArm.Find("mixamorig:LeftHand");
            }

            _rightArm = transform.Find("Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm");
            if (_rightArm != null)
            {
                _rightForeArm = _rightArm.Find("mixamorig:RightForeArm");
                if (_rightForeArm != null) _rightHand = _rightForeArm.Find("mixamorig:RightHand");
            }

            _hasBones = (_leftArm != null && _leftForeArm != null && _rightArm != null && _rightForeArm != null);
        }
    }

    private void ApplyArmHoldingPose()
    {
        // Natural vendor holding pose: hands grip the side handles of the styrofoam ice chest
        if (_leftArm != null)
        {
            _leftArm.localRotation = Quaternion.Euler(15f, -10f, -60f);
        }
        if (_leftForeArm != null)
        {
            _leftForeArm.localRotation = Quaternion.Euler(55f, 0f, 0f);
        }
        if (_leftHand != null)
        {
            _leftHand.localRotation = Quaternion.Euler(10f, 15f, 20f);
        }

        if (_rightArm != null)
        {
            _rightArm.localRotation = Quaternion.Euler(15f, 10f, 60f);
        }
        if (_rightForeArm != null)
        {
            _rightForeArm.localRotation = Quaternion.Euler(55f, 0f, 0f);
        }
        if (_rightHand != null)
        {
            _rightHand.localRotation = Quaternion.Euler(10f, -15f, -20f);
        }
    }

    private void LoadTexturesAndAssets()
    {
#if UNITY_EDITOR
        if (sandwichItemAsset == null)
            sandwichItemAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/HamAndCheeseSandwich.asset");
        if (vendorGreetingClip == null)
            vendorGreetingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/murmurSoundEffect.mp3");

        if (styrofoamTex == null)
            styrofoamTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Styrofoam_Texture.png");
        if (stickerTex == null)
            stickerTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/HamAndCheese_Sticker.png");
        if (priceTag3Tex == null)
            priceTag3Tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_3.png");
        if (priceTag5Tex == null)
            priceTag5Tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_5.png");
        if (priceTag8Tex == null)
            priceTag8Tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_8.png");
        if (priceTag12Tex == null)
            priceTag12Tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_12.png");
        if (priceTagSoldOutTex == null)
            priceTagSoldOutTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PriceTag_SoldOut.png");
#endif
    }

    // ── IInteractable Implementation ──────────────────────────────────────────

    public string GetPrompt()
    {
        if (hasBoughtThisEncounter)
            return "[E] Kausapin si Renz (Naubusan ng Stock)";

        return $"[E] Bili ng Ham & Cheese (₱{CurrentPrice})";
    }

    public void Interact()
    {
        if (vendorGreetingClip != null && _audioSource != null)
        {
            _audioSource.pitch = UnityEngine.Random.Range(1.02f, 1.12f);
            _audioSource.PlayOneShot(vendorGreetingClip, 0.75f);
        }

        if (HamAndCheeseVendorUI.Instance != null)
        {
            HamAndCheeseVendorUI.Instance.OpenUI(this);
        }
    }

    public void RegisterPurchase()
    {
        hasBoughtThisEncounter = true;
        TotalPurchasesAcrossRuns++;
        UpdateBoxLabel();
    }

    public void ResetStockForNewEncounter()
    {
        hasBoughtThisEncounter = false;
        UpdateBoxLabel();
    }

    // ── Styrofoam Ice Chest Visual Construction (Matching Reference Image) ──

    public void UpdateBoxLabel()
    {
        if (_priceTagRenderer != null)
        {
            Texture2D targetTex = priceTag3Tex;
            if (hasBoughtThisEncounter)
            {
                targetTex = priceTagSoldOutTex;
            }
            else
            {
                int p = CurrentPrice;
                if (p <= 3) targetTex = priceTag3Tex;
                else if (p <= 5) targetTex = priceTag5Tex;
                else if (p <= 8) targetTex = priceTag8Tex;
                else targetTex = priceTag12Tex;
            }

            if (targetTex != null)
            {
                _priceTagRenderer.material.mainTexture = targetTex;
            }
        }
    }

    public void BuildStyrofoamBoxProp()
    {
        var existingBox = transform.Find("StyrofoamCoolerBox");
        if (existingBox != null)
        {
            _styrofoamBoxProp = existingBox.gameObject;
            var pt = _styrofoamBoxProp.transform.Find("PriceTagQuad");
            if (pt != null) _priceTagRenderer = pt.GetComponent<MeshRenderer>();
            return;
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. Styrofoam Material (Off-white EPS foam bead texture for both body & lid)
        var styrofoamMat = new Material(litShader);
        styrofoamMat.name = "EPS_Styrofoam_Mat";
        styrofoamMat.color = new Color(0.96f, 0.96f, 0.94f, 1f);
        if (styrofoamTex != null)
        {
            styrofoamMat.mainTexture = styrofoamTex;
            if (styrofoamMat.HasProperty("_BaseMap")) styrofoamMat.SetTexture("_BaseMap", styrofoamTex);
            styrofoamMat.mainTextureScale = new Vector2(3f, 3f);
        }
        if (styrofoamMat.HasProperty("_Smoothness")) styrofoamMat.SetFloat("_Smoothness", 0.08f);

        // 2. Printed Sticker Material (Flat glossy vinyl label)
        var stickerMat = new Material(litShader);
        stickerMat.name = "HamAndCheese_Sticker_Mat";
        stickerMat.color = Color.white;
        if (stickerTex != null)
        {
            stickerMat.mainTexture = stickerTex;
            if (stickerMat.HasProperty("_BaseMap")) stickerMat.SetTexture("_BaseMap", stickerTex);
        }
        if (stickerMat.HasProperty("_Smoothness")) stickerMat.SetFloat("_Smoothness", 0.35f);

        // 3. Masking Tape Price Tag Material
        var priceTagMat = new Material(litShader);
        priceTagMat.name = "PriceTag_Tape_Mat";
        priceTagMat.color = Color.white;
        if (priceTag3Tex != null)
        {
            priceTagMat.mainTexture = priceTag3Tex;
            if (priceTagMat.HasProperty("_BaseMap")) priceTagMat.SetTexture("_BaseMap", priceTag3Tex);
        }
        if (priceTagMat.HasProperty("_Smoothness")) priceTagMat.SetFloat("_Smoothness", 0.15f);

        // 4. Yellow Nylon Strap Material
        var strapMat = new Material(litShader);
        strapMat.name = "YellowNylonStrap_Mat";
        strapMat.color = new Color(0.96f, 0.78f, 0.08f, 1f); // Vibrant woven nylon yellow
        if (strapMat.HasProperty("_Smoothness")) strapMat.SetFloat("_Smoothness", 0.20f);

        // 5. White Plastic Buckle Clip Material
        var clipMat = new Material(litShader);
        clipMat.name = "PlasticBuckleClip_Mat";
        clipMat.color = new Color(0.93f, 0.93f, 0.91f, 1f);
        if (clipMat.HasProperty("_Smoothness")) clipMat.SetFloat("_Smoothness", 0.50f);

        // ── Root Container ──
        _styrofoamBoxProp = new GameObject("StyrofoamCoolerBox");
        _styrofoamBoxProp.transform.SetParent(transform, false);
        // Positioned comfortably resting against vendor's torso/belly
        _styrofoamBoxProp.transform.localPosition = new Vector3(0f, 0.88f, 0.35f);
        _styrofoamBoxProp.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

        // A. Main Styrofoam Box Body (White EPS cooler)
        var boxBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxBody.name = "CoolerBody";
        boxBody.transform.SetParent(_styrofoamBoxProp.transform, false);
        boxBody.transform.localPosition = Vector3.zero;
        boxBody.transform.localScale = new Vector3(0.44f, 0.30f, 0.28f);
        DestroyCollider(boxBody);
        boxBody.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        // B. Cooler Lid (Matching White EPS styrofoam with slight lip overhang)
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "CoolerLid";
        lid.transform.SetParent(_styrofoamBoxProp.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.165f, 0f);
        lid.transform.localScale = new Vector3(0.46f, 0.045f, 0.30f);
        DestroyCollider(lid);
        lid.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        // B2. Lid Molded Ridge (Authentic molded grip ridge on lid top)
        var lidRidge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lidRidge.name = "CoolerLidRidge";
        lidRidge.transform.SetParent(_styrofoamBoxProp.transform, false);
        lidRidge.transform.localPosition = new Vector3(0f, 0.19f, 0f);
        lidRidge.transform.localScale = new Vector3(0.40f, 0.015f, 0.24f);
        DestroyCollider(lidRidge);
        lidRidge.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        // C. Left & Right Molded Styrofoam Handle Lugs
        var leftLug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLug.name = "LeftHandleLug";
        leftLug.transform.SetParent(_styrofoamBoxProp.transform, false);
        leftLug.transform.localPosition = new Vector3(-0.235f, 0.04f, 0f);
        leftLug.transform.localScale = new Vector3(0.035f, 0.06f, 0.12f);
        DestroyCollider(leftLug);
        leftLug.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        var rightLug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLug.name = "RightHandleLug";
        rightLug.transform.SetParent(_styrofoamBoxProp.transform, false);
        rightLug.transform.localPosition = new Vector3(0.235f, 0.04f, 0f);
        rightLug.transform.localScale = new Vector3(0.035f, 0.06f, 0.12f);
        DestroyCollider(rightLug);
        rightLug.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        // D. White Plastic Strap Buckle Clips
        var leftClip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftClip.name = "LeftStrapClip";
        leftClip.transform.SetParent(_styrofoamBoxProp.transform, false);
        leftClip.transform.localPosition = new Vector3(-0.252f, 0.04f, 0f);
        leftClip.transform.localScale = new Vector3(0.012f, 0.04f, 0.035f);
        DestroyCollider(leftClip);
        leftClip.GetComponent<MeshRenderer>().sharedMaterial = clipMat;

        var rightClip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightClip.name = "RightStrapClip";
        rightClip.transform.SetParent(_styrofoamBoxProp.transform, false);
        rightClip.transform.localPosition = new Vector3(0.252f, 0.04f, 0f);
        rightClip.transform.localScale = new Vector3(0.012f, 0.04f, 0.035f);
        DestroyCollider(rightClip);
        rightClip.GetComponent<MeshRenderer>().sharedMaterial = clipMat;

        // E. Yellow Nylon Straps (Looping around neck and sides)
        var strapLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strapLeft.name = "StrapLeftSegment";
        strapLeft.transform.SetParent(_styrofoamBoxProp.transform, false);
        strapLeft.transform.localPosition = new Vector3(-0.19f, 0.22f, -0.09f);
        strapLeft.transform.localScale = new Vector3(0.025f, 0.36f, 0.008f);
        strapLeft.transform.localRotation = Quaternion.Euler(32f, -12f, 15f);
        DestroyCollider(strapLeft);
        strapLeft.GetComponent<MeshRenderer>().sharedMaterial = strapMat;

        var strapRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strapRight.name = "StrapRightSegment";
        strapRight.transform.SetParent(_styrofoamBoxProp.transform, false);
        strapRight.transform.localPosition = new Vector3(0.19f, 0.22f, -0.09f);
        strapRight.transform.localScale = new Vector3(0.025f, 0.36f, 0.008f);
        strapRight.transform.localRotation = Quaternion.Euler(32f, 12f, -15f);
        DestroyCollider(strapRight);
        strapRight.GetComponent<MeshRenderer>().sharedMaterial = strapMat;

        var strapNeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strapNeck.name = "StrapNeckLoop";
        strapNeck.transform.SetParent(_styrofoamBoxProp.transform, false);
        strapNeck.transform.localPosition = new Vector3(0f, 0.36f, -0.20f);
        strapNeck.transform.localScale = new Vector3(0.28f, 0.025f, 0.008f);
        strapNeck.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        DestroyCollider(strapNeck);
        strapNeck.GetComponent<MeshRenderer>().sharedMaterial = strapMat;

        // Slack strap loop hanging loosely across front (authentic detail from reference image)
        var strapFrontSlack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strapFrontSlack.name = "StrapFrontSlack";
        strapFrontSlack.transform.SetParent(_styrofoamBoxProp.transform, false);
        strapFrontSlack.transform.localPosition = new Vector3(0f, -0.05f, 0.155f);
        strapFrontSlack.transform.localScale = new Vector3(0.32f, 0.020f, 0.006f);
        strapFrontSlack.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        DestroyCollider(strapFrontSlack);
        strapFrontSlack.GetComponent<MeshRenderer>().sharedMaterial = strapMat;

        // F. Molded Vertical Recesses / Ribs on Front Face
        var ribLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ribLeft.name = "FrontRibLeft";
        ribLeft.transform.SetParent(_styrofoamBoxProp.transform, false);
        ribLeft.transform.localPosition = new Vector3(-0.17f, -0.02f, 0.1402f);
        ribLeft.transform.localScale = new Vector3(0.015f, 0.22f, 0.004f);
        DestroyCollider(ribLeft);
        ribLeft.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        var ribRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ribRight.name = "FrontRibRight";
        ribRight.transform.SetParent(_styrofoamBoxProp.transform, false);
        ribRight.transform.localPosition = new Vector3(0.17f, -0.02f, 0.1402f);
        ribRight.transform.localScale = new Vector3(0.015f, 0.22f, 0.004f);
        DestroyCollider(ribRight);
        ribRight.GetComponent<MeshRenderer>().sharedMaterial = styrofoamMat;

        // G. "HAM & CHEESE" PLASTERED VINYL STICKER (Flat on box face, zero floating, un-mirrored)
        // Box front is at Z = +0.14m. We place the quad at Z = +0.1405m (+0.5mm offset to prevent z-fighting)
        // Rotation (0, 180, 0) ensures normal points +Z (outward) and text reads left-to-right correctly!
        var stickerGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stickerGO.name = "HamAndCheeseStickerQuad";
        stickerGO.transform.SetParent(_styrofoamBoxProp.transform, false);
        stickerGO.transform.localPosition = new Vector3(0f, 0.02f, 0.1405f);
        stickerGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        stickerGO.transform.localScale = new Vector3(0.26f, 0.13f, 1f);
        DestroyCollider(stickerGO);
        stickerGO.GetComponent<MeshRenderer>().sharedMaterial = stickerMat;

        // H. MASKING TAPE PRICE TAG (Plastered on upper-right corner of front face)
        var priceTagGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        priceTagGO.name = "PriceTagQuad";
        priceTagGO.transform.SetParent(_styrofoamBoxProp.transform, false);
        priceTagGO.transform.localPosition = new Vector3(0.125f, 0.105f, 0.1408f);
        priceTagGO.transform.localRotation = Quaternion.Euler(0f, 180f, -4.5f); // Natural slight tilt
        priceTagGO.transform.localScale = new Vector3(0.085f, 0.045f, 1f);
        DestroyCollider(priceTagGO);
        _priceTagRenderer = priceTagGO.GetComponent<MeshRenderer>();
        _priceTagRenderer.sharedMaterial = priceTagMat;
    }

    private static void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Object.DestroyImmediate(c);
            else Object.Destroy(c);
#else
            Object.Destroy(c);
#endif
        }
    }
}
