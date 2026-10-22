using System.Collections;
using UnityEngine;

/// <summary>
/// CircuitBreakerPuzzle — Main electrical distribution panel puzzle.
/// Restores power to the ComLab & Faculty Wing when the 15A Ceramic Cartridge Fuse is inserted.
///
/// Features:
/// - 4 circuit branches (Hallway, ComLab/Faculty, Guidance, Ventilation).
/// - Slot 2 is blown/tripped, requiring the 15A Ceramic Fuse.
/// - Inspecting the Ceramic Fuse in 3D reveals "15A - 250V // RATING: COMLAB & FACULTY WING GRID".
/// - Restoring power illuminates wing lights and activates Room 105 computer terminal.
/// </summary>
public class CircuitBreakerPuzzle : MonoBehaviour, IInteractable
{
    public static CircuitBreakerPuzzle Instance { get; private set; }

    [Header("Puzzle State")]
    public bool isFuseInstalled = false;
    public bool isPowerRestored = false;

    [Header("Item References")]
    [Tooltip("The 15A Ceramic Cartridge Fuse item required.")]
    public InventoryItem ceramicFuseItem;

    [Header("Visual Feedback")]
    [Tooltip("Status LED for Slot 2 (Red when blown, Green when fused).")]
    public Light slotStatusLight;

    [Tooltip("Wing lights group turned on when power is restored.")]
    public Transform wingLightsGroup;

    [Header("Audio")]
    public AudioClip fuseInsertAudio;
    public AudioClip switchThrowAudio;
    public AudioClip powerHumAudio;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.85f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 15f;
            _audioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        if (ceramicFuseItem == null)
            ceramicFuseItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Ceramic_Fuse.asset");
        if (fuseInsertAudio == null)
            fuseInsertAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/door_closing.mp3");
        if (switchThrowAudio == null)
            switchThrowAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/opening door.mp3");
        if (powerHumAudio == null)
            powerHumAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/FNAF_Power_On_Short.wav");
#endif

        RebuildBreakerBoxModelSeamless();
    }

    private void Start()
    {
        EnsureSpareFusePickupInGuardBooth();
    }

    private void EnsureSpareFusePickupInGuardBooth()
    {
        SpawnFusePickup("Ceramic_Fuse_Pickup_Lobby", new Vector3(-76.15f, 2.85f, 15.95f));
        SpawnFusePickup("Ceramic_Fuse_Pickup_Corridor", new Vector3(-84.2f, 2.35f, 21.0f));
    }

    private void SpawnFusePickup(string objectName, Vector3 spawnPos)
    {
        if (GameObject.Find(objectName) != null) return;

        // Ground raycast: snap perfectly flush to surface (floor or desk tabletop)
        RaycastHit groundHit;
        if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out groundHit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos.y = groundHit.point.y + 0.005f;
        }

        GameObject fuseGO = new GameObject(objectName);
        fuseGO.transform.position = spawnPos;

        GameObject fusePrefab = null;
#if UNITY_EDITOR
        fusePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemModels/Model_CeramicFuse.prefab");
#endif

        if (fusePrefab != null)
        {
            var modelInst = Instantiate(fusePrefab, fuseGO.transform, false);
            modelInst.name = "FuseModel";
            modelInst.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            modelInst.transform.localRotation = Quaternion.Euler(0f, 45f, 90f);
            modelInst.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            // Strip stray PickupItems or Colliders on the visual model
            foreach (var p in modelInst.GetComponentsInChildren<PickupItem>(true)) Destroy(p);
            foreach (var c in modelInst.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
        else
        {
            // Visual cylinder cartridge fallback
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "FuseModel";
            cyl.transform.SetParent(fuseGO.transform, false);
            cyl.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            cyl.transform.localRotation = Quaternion.Euler(0f, 45f, 90f);
            cyl.transform.localScale = new Vector3(0.045f, 0.08f, 0.045f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Texture2D fuseTex = null;
#if UNITY_EDITOR
            fuseTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ItemInspectTextures/Tex_CeramicFuse.png");
#endif
            if (litShader != null)
            {
                Material mat = new Material(litShader);
                mat.name = "CeramicFuse_PickupMat";
                if (fuseTex != null) mat.mainTexture = fuseTex;
                cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            var col = cyl.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // Small industrial spare fuse carton base
        var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "FuseCartonTray";
        tray.transform.SetParent(fuseGO.transform, false);
        tray.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        tray.transform.localScale = new Vector3(0.14f, 0.02f, 0.14f);
        var trayCol = tray.GetComponent<Collider>();
        if (trayCol != null) Destroy(trayCol);

        Shader trayShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (trayShader != null)
        {
            Material trayMat = new Material(trayShader);
            trayMat.name = "FuseTray_Mat";
            trayMat.color = new Color(0.85f, 0.55f, 0.12f, 1f); // Industrial amber safety carton
            tray.GetComponent<MeshRenderer>().sharedMaterial = trayMat;
        }

        var boxCol = fuseGO.AddComponent<BoxCollider>();
        boxCol.isTrigger = false;
        boxCol.size = new Vector3(0.40f, 0.40f, 0.40f);

        // Pickup item component
        var pickup = fuseGO.AddComponent<PickupItem>();
        pickup.itemData = ceramicFuseItem;

        // Glint highlight
        fuseGO.AddComponent<ItemVisualHighlight>();
    }

    /// <summary>
    /// Repairs the Main Breaker Box 3D geometry in-place so the enclosure, door frame,
    /// switch panel, switches, fuse cradle, voltmeter, and caution plaque are 100% flush
    /// with zero air gaps and crisp industrial textures.
    /// </summary>
    private void RebuildBreakerBoxModelSeamless()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (litShader == null) return;

        Material cabinetMat = CreateLitMaterial(litShader, "Breaker_CabinetSteel", new Color(0.16f, 0.18f, 0.21f, 1f), 0.78f, 0.42f);
        Material doorFrameMat = CreateLitMaterial(litShader, "Breaker_DoorSteel", new Color(0.24f, 0.27f, 0.31f, 1f), 0.82f, 0.48f);
        Material interiorMat = CreateLitMaterial(litShader, "Breaker_RecessSlate", new Color(0.10f, 0.11f, 0.13f, 1f), 0.70f, 0.35f, CreateBreakerInteriorPanelTexture());
        Material switchBlackMat = CreateLitMaterial(litShader, "Breaker_SwitchBakelite", new Color(0.07f, 0.07f, 0.08f, 1f), 0.20f, 0.60f);
        Material switchTripRedMat = CreateLitMaterial(litShader, "Breaker_SwitchTripped", new Color(0.75f, 0.14f, 0.12f, 1f), 0.25f, 0.65f);
        Material brassMat = CreateLitMaterial(litShader, "Breaker_CopperBrass", new Color(0.86f, 0.68f, 0.24f, 1f), 0.92f, 0.78f);
        Material hazardMat = CreateLitMaterial(litShader, "Breaker_CautionPlaque", Color.white, 0.35f, 0.55f, CreateHazardPlaqueTexture());
        Material gaugeFaceMat = CreateLitMaterial(litShader, "Breaker_GaugeFace", Color.white, 0.20f, 0.70f, CreateVoltmeterDialTexture());

        Transform boxT = transform.Find("EnclosureBox");
        if (boxT == null) return;

        // 1. Replace inside-out hollow EnclosureBox mesh with watertight outward-facing beveled cabinet mesh
        var boxMF = boxT.GetComponent<MeshFilter>();
        if (boxMF != null)
        {
            boxMF.sharedMesh = Item3DModelFactory.CreateChamferedBoxMesh(0.82f, 1.12f, 0.26f, 0.014f);
        }
        var boxMR = boxT.GetComponent<MeshRenderer>();
        if (boxMR != null) boxMR.sharedMaterial = cabinetMat;

        // Solid flush back-weld plate so wall mounting never shows any seam
        EnsurePartCube(boxT, "CabinetBackplate", new Vector3(0f, 0f, -0.005f), new Vector3(0.80f, 1.10f, 0.25f), cabinetMat);

        // 2. Flush Front Door Frame Panel (overlaps cabinet front face Z = +0.13f seamlessly)
        Transform doorT = boxT.Find("DoorFrame");
        if (doorT != null)
        {
            doorT.localPosition = new Vector3(0f, 0f, 0.124f);
            doorT.localScale = new Vector3(0.78f, 1.08f, 0.032f);
            var mr = doorT.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = doorFrameMat;
        }

        // Top & Bottom Conduit Pipes connected flush to top/bottom of EnclosureBox
        EnsurePartCylinder(boxT, "ConduitPipe_Left", new Vector3(-0.22f, 0.68f, -0.02f), Quaternion.identity, new Vector3(0.055f, 0.16f, 0.055f), doorFrameMat);
        EnsurePartCylinder(boxT, "ConduitPipe_Right", new Vector3(0.22f, 0.68f, -0.02f), Quaternion.identity, new Vector3(0.055f, 0.16f, 0.055f), doorFrameMat);

        // 3. Flush Switch Interior Panel (no air gap between DoorFrame and SwitchInterior)
        Transform swPanelT = boxT.Find("SwitchInterior");
        if (swPanelT != null)
        {
            swPanelT.localPosition = new Vector3(0f, 0.06f, 0.134f);
            swPanelT.localScale = new Vector3(0.68f, 0.68f, 0.028f);
            var mr = swPanelT.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = interiorMat;

            // Update the 4 breaker switches so they sit solidly inside the switch slots
            string[] labels = new string[] { "HALLWAY", "COMLAB_FACULTY", "GUIDANCE", "VENTILATION" };
            for (int i = 0; i < labels.Length; i++)
            {
                float yPos = 0.28f - i * 0.18f;
                // Molded breaker module base
                EnsurePartCube(swPanelT, $"BreakerModuleBase_{i}", new Vector3(-0.18f, yPos, 0.42f), new Vector3(0.34f, 0.13f, 0.45f), doorFrameMat);

                Transform swT = swPanelT.Find($"Switch_{labels[i]}");
                if (swT != null)
                {
                    swT.localPosition = new Vector3(-0.18f, yPos, 0.68f);
                    swT.localScale = new Vector3(0.16f, 0.065f, 0.65f);
                    swT.localRotation = Quaternion.Euler(i == 1 ? -22f : 22f, 0f, 0f);
                    var swMR = swT.GetComponent<MeshRenderer>();
                    if (swMR != null) swMR.sharedMaterial = (i == 1) ? switchTripRedMat : switchBlackMat;
                }
            }

            // Fuse Holder Base Block behind FuseCradle_Left & Right so they don't float
            EnsurePartCube(swPanelT, "FuseHolderBlock", new Vector3(0.20f, 0.10f, 0.44f), new Vector3(0.24f, 0.14f, 0.48f), switchBlackMat);

            Transform fcL = swPanelT.Find("FuseCradle_Left");
            if (fcL != null)
            {
                fcL.localPosition = new Vector3(0.14f, 0.10f, 0.72f);
                fcL.localScale = new Vector3(0.055f, 0.25f, 0.055f);
                var mrL = fcL.GetComponent<MeshRenderer>();
                if (mrL != null) mrL.sharedMaterial = brassMat;
            }
            Transform fcR = swPanelT.Find("FuseCradle_Right");
            if (fcR != null)
            {
                fcR.localPosition = new Vector3(0.26f, 0.10f, 0.72f);
                fcR.localScale = new Vector3(0.055f, 0.25f, 0.055f);
                var mrR = fcR.GetComponent<MeshRenderer>();
                if (mrR != null) mrR.sharedMaterial = brassMat;
            }
        }

        // 4. Caution Sign — solid metal plaque flush against DoorFrame (Z = 0.140f)
        Transform signT = boxT.Find("CautionSign");
        if (signT != null)
        {
            signT.localPosition = new Vector3(0f, -0.38f, 0.141f);
            signT.localScale = new Vector3(0.66f, 0.19f, 1f);
            var mr = signT.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = hazardMat;
            EnsurePartCube(boxT, "CautionSign_BackingPlate", new Vector3(0f, -0.38f, 0.135f), new Vector3(0.68f, 0.21f, 0.012f), cabinetMat);
        }

        // 5. Voltmeter Gauge — flush bezel + printed dial face
        Transform meterT = boxT.Find("VoltmeterGauge");
        if (meterT != null)
        {
            meterT.localPosition = new Vector3(0.21f, 0.44f, 0.138f);
            meterT.localRotation = Quaternion.Euler(90f, 0f, 0f);
            meterT.localScale = new Vector3(0.13f, 0.012f, 0.13f);
            var mr = meterT.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = gaugeFaceMat;
        }
    }

    private static GameObject EnsurePartCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        return go;
    }

    private static GameObject EnsurePartCylinder(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        return go;
    }

    private static Material CreateLitMaterial(Shader shader, string name, Color color, float metallic, float smoothness, Texture2D tex = null)
    {
        Material m = new Material(shader) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }
        return m;
    }

    private static Texture2D CreateHazardPlaqueTexture()
    {
        int w = 512, h = 160;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color yellow = new Color(0.96f, 0.80f, 0.08f, 1f);
        Color black = new Color(0.10f, 0.10f, 0.12f, 1f);
        Color redHeader = new Color(0.82f, 0.12f, 0.10f, 1f);
        Color white = new Color(0.96f, 0.96f, 0.96f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isHazardBorder = (y < 24 || y > h - 24 || x < 24 || x > w - 24);
                if (isHazardBorder)
                {
                    bool stripe = ((x + y) / 18) % 2 == 0;
                    tex.SetPixel(x, y, stripe ? yellow : black);
                }
                else if (y > h - 68)
                {
                    tex.SetPixel(x, y, redHeader);
                    if (y > h - 54 && y < h - 36 && x > 140 && x < w - 140 && (x % 14 < 8))
                        tex.SetPixel(x, y, white);
                }
                else
                {
                    tex.SetPixel(x, y, yellow);
                    if ((y > 52 && y < 66 && x > 60 && x < w - 60 && (x % 12 < 7)) ||
                        (y > 34 && y < 44 && x > 95 && x < w - 95 && (x % 10 < 6)))
                        tex.SetPixel(x, y, black);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateBreakerInteriorPanelTexture()
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color slate = new Color(0.12f, 0.13f, 0.15f, 1f);
        Color border = new Color(0.26f, 0.28f, 0.32f, 1f);
        Color labelWhite = new Color(0.85f, 0.86f, 0.88f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = slate;
                if (x < 6 || x > w - 7 || y < 6 || y > h - 7) c = border;
                if (y % 48 == 0 && x > 16 && x < w - 16) c = border;
                if (x > 135 && x < 225 && (y % 48 > 18 && y % 48 < 28) && (x % 8 < 5)) c = labelWhite;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateVoltmeterDialTexture()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color face = new Color(0.94f, 0.93f, 0.88f, 1f);
        Color rim = new Color(0.18f, 0.20f, 0.22f, 1f);
        Color red = new Color(0.85f, 0.14f, 0.14f, 1f);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                Color c = d > 54f ? rim : face;
                if (d > 38f && d < 46f && y > center.y) c = rim;
                if (Mathf.Abs((x - center.x) - (y - center.y) * 0.6f) < 2.2f && d < 42f && y >= center.y - 4f) c = red;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    public string GetPrompt()
    {
        if (isPowerRestored)
        {
            return "[Main Breaker Box] (Aktibo ang Kuryente - Slot 2: 15A OK)";
        }

        bool hasFuse = PlayerHasCeramicFuse();
        bool knowsRating = ItemInspectionViewer3D.DiscoveredClueIDs.Contains("CeramicFuseRating");

        if (hasFuse)
        {
            return knowsRating
                ? "[E] Ilagay ang 15A Ceramic Fuse sa Slot 2 at I-on ang Switch"
                : "[E] Magkabit ng Ceramic Fuse sa Breaker Slot 2";
        }

        return "[Main Breaker Box] (Napatid ang Kuryente sa Slot 2 - Kailangan ng 15A Ceramic Fuse)";
    }

    public void Interact()
    {
        if (isPowerRestored)
        {
            Debug.Log("[CircuitBreakerPuzzle] Power is already fully restored to the wing.");
            return;
        }

        if (PlayerHasCeramicFuse())
        {
            StartCoroutine(RestorePowerSequence());
        }
        else
        {
            PlayAudio(fuseInsertAudio, 0.6f, 0.85f);
            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Hanapin ang 15A Ceramic Fuse",
                    "Kailangan ng 15A Ceramic Cartridge Fuse para maibalik ang kuryente sa ComLab. Kumuha ng 15A Fuse sa 1F Lobby Security Counter (katabi ng Locker Key) o sa 2F Guidance Reading Shelf."
                );
            }
        }
    }

    private IEnumerator RestorePowerSequence()
    {
        ConsumeCeramicFuse();
        isFuseInstalled = true;

        PlayAudio(fuseInsertAudio, 0.85f, 1.15f);
        yield return new WaitForSeconds(0.4f);

        PlayAudio(switchThrowAudio, 1.0f, 1.0f);
        yield return new WaitForSeconds(0.3f);

        PlayAudio(powerHumAudio, 0.95f, 1.0f);
        isPowerRestored = true;

        if (BlackoutManager.Instance != null)
        {
            BlackoutManager.Instance.OnBreakerGridStabilized();
        }

        if (PlayerThoughtVoice.Instance != null)
        {
            PlayerThoughtVoice.Instance.ShowThought("Ayan, medyo umayos ang kuryente. Lumakas ang ilaw at hindi na kumikislap nang sobra.");
        }

        if (slotStatusLight != null)
        {
            slotStatusLight.color = Color.green;
            slotStatusLight.intensity = 1.5f;
        }

        if (wingLightsGroup != null)
        {
            foreach (var light in wingLightsGroup.GetComponentsInChildren<Light>(true))
            {
                light.enabled = true;
            }
        }

        if (ObjectiveHUD.Instance != null)
        {
            ObjectiveHUD.Instance.SetObjective(
                "Kuryente sa ComLab Naibalik!",
                "Naibalik ang power grid sa 2nd Floor. Maaari nang puntahan ang Master Computer Terminal sa Room 105."
            );
        }

        Debug.Log("[CircuitBreakerPuzzle] 15A Ceramic Fuse installed. Grid power restored!");
    }

    private bool PlayerHasCeramicFuse()
    {
        if (InventoryManager.Instance != null)
        {
            if (ceramicFuseItem != null && InventoryManager.Instance.HasItem(ceramicFuseItem)) return true;
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Fuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private void ConsumeCeramicFuse()
    {
        if (InventoryManager.Instance != null)
        {
            if (ceramicFuseItem != null && InventoryManager.Instance.HasItem(ceramicFuseItem))
            {
                InventoryManager.Instance.RemoveItem(ceramicFuseItem, 1);
                return;
            }
            foreach (var slot in InventoryManager.Instance.Slots)
            {
                if (!slot.IsEmpty && slot.item != null && slot.item.itemName.IndexOf("Fuse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryManager.Instance.RemoveItem(slot.item, 1);
                    return;
                }
            }
        }
    }

    private void PlayAudio(AudioClip clip, float vol, float pitch)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, vol);
        }
    }
}
