using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ProctorJumpscareQTE — Interactive FNAF / Outlast style struggle QTE when caught by The Proctor!
/// 
/// Mechanics:
/// - While The Proctor's face is right in front of the player camera, the player must rapidly mash [Q]
///   to fill the escape progress bar (0% -> 100%).
/// - Every second spent in The Proctor's grasp continuously increases the player's Anxiety Meter.
/// - If the player fills the [Q] bar to 100% before Anxiety reaches 100%, they shove The Proctor back
///   and break free!
/// - If the player fails to mash [Q] fast enough and Anxiety hits 100%, the AnxietyManager automatically
///   triggers the total panic breakdown Game Over sequence.
/// </summary>
public class ProctorJumpscareQTE : MonoBehaviour
{
    public static ProctorJumpscareQTE Instance { get; private set; }

    [Header("Struggle QTE Balancing")]
    [Tooltip("Key the player must rapidly press to break free.")]
    public KeyCode struggleKey = KeyCode.Q;
    public KeyCode qteKey = KeyCode.Q;
    public KeyCode alternateKey = KeyCode.Space;
    public int minRequiredPresses = 12;
    public int maxRequiredPresses = 18;
    public float anxietyIncreaseRate = 14.0f;
    public float progressDecayRate = 1.0f;
    public float decayGracePeriod = 0.28f;

    [Tooltip("Escape progress gained (0..1) per [Q] key press.")]
    public float progressPerPress = 0.135f;

    [Tooltip("Passive decay of the escape bar per second (The Proctor pushing back).")]
    public float progressDecayPerSecond = 0.18f;

    [Tooltip("Initial instant anxiety spike upon getting grabbed by The Proctor.")]
    public float initialGrabAnxietySpike = 12f;

    [Tooltip("Continuous anxiety added per second while struggling in The Proctor's grasp.")]
    public float struggleAnxietyPerSecond = 16.5f;

    [Header("Audio Feedback")]
    public AudioClip keyTapClip;
    public AudioClip breakFreeClip;
    public AudioClip heartbeatAudio;
    public AudioClip strugglePressAudio;
    public AudioClip breakFreeAudio;

    private GameObject _qteRoot;
    private Image _fillBar;
    private RectTransform _keyBadgeRT;
    private Image _keyBadgeBg;
    private Text _promptText;
    private AudioSource _audioSource;
    private bool _isQTEActive = false;

    public bool IsQTEActive => _isQTEActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
    }

    public static void AbortActiveQTE()
    {
        if (Instance != null)
        {
            Instance.StopAllCoroutines();
            if (Instance._qteRoot != null)
            {
                Destroy(Instance._qteRoot);
                Instance._qteRoot = null;
            }
            Instance._isQTEActive = false;
        }
    }

    /// <summary>
    /// Runs the interactive struggle QTE loop while keeping The Proctor lunging in front of the camera.
    /// Invokes onComplete(true) if the player broke free, or onComplete(false) if Anxiety hit 100% (Game Over).
    /// </summary>
    public IEnumerator RunStruggleQTE(
        TheProctorAI proctor,
        Camera playerCam,
        Transform headBone,
        Vector3 baseTargetPos,
        Vector3 faceDir,
        float headOffsetY,
        Vector3 camBaseLocalPos,
        Action<bool> onComplete)
    {
        // Force-close any open puzzle or modal so nothing blocks [Q] struggle inputs
        PauseMenu.ForceCloseAllModalsAndPuzzles();

        _isQTEActive = true;
        BuildQTEUI();

        // Initial anxiety jolt upon being grabbed
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.AddAnxiety(initialGrabAnxietySpike);
        }

        float progress = 0.12f; // Start with a tiny visible sliver so the player immediately understands the bar
        float elapsed = 0f;
        float badgeScale = 1f;

        while (progress < 1.0f)
        {
            // Check if Anxiety hit 100% (Game Over triggered)
            if (GameOverUI.IsCutsceneActive || (AnxietyManager.Instance != null && AnxietyManager.Instance.IsGameOver))
            {
                if (_qteRoot != null) Destroy(_qteRoot);
                _isQTEActive = false;
                if (proctor != null) proctor.Despawn();
                onComplete?.Invoke(false);
                yield break;
            }

            // Pause menu check
            if (PauseMenu.GameIsPaused)
            {
                yield return null;
                continue;
            }

            float dt = Time.deltaTime;
            elapsed += dt;

            // 1. Continuous Anxiety buildup while trapped in The Proctor's grasp!
            if (AnxietyManager.Instance != null)
            {
                AnxietyManager.Instance.AddAnxiety(struggleAnxietyPerSecond * dt);
                if (GameOverUI.IsCutsceneActive || AnxietyManager.Instance.IsGameOver)
                {
                    if (_qteRoot != null) Destroy(_qteRoot);
                    _isQTEActive = false;
                    if (proctor != null) proctor.Despawn();
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            // 2. Check [Q] key mash input
            bool pressed = Input.GetKeyDown(struggleKey);
            if (pressed)
            {
                progress += progressPerPress;
                badgeScale = 1.32f;

                if (_keyBadgeBg != null)
                {
                    _keyBadgeBg.color = new Color(1f, 0.92f, 0.35f, 1f); // Flash bright gold/white on press
                }
            }
            else
            {
                progress = Mathf.Max(0f, progress - progressDecayPerSecond * dt);
            }

            progress = Mathf.Clamp01(progress);

            // 3. Update QTE UI visuals
            if (_fillBar != null)
            {
                _fillBar.fillAmount = progress;
                // Color shifts from desperate crimson to adrenaline amber/green as bar fills
                _fillBar.color = Color.Lerp(new Color(0.88f, 0.12f, 0.12f, 1f), new Color(0.25f, 0.92f, 0.42f, 1f), progress);
            }

            badgeScale = Mathf.MoveTowards(badgeScale, 1.0f + Mathf.Sin(elapsed * 18f) * 0.08f, dt * 4.5f);
            if (_keyBadgeRT != null)
            {
                _keyBadgeRT.localScale = Vector3.one * badgeScale;
            }

            if (_keyBadgeBg != null && !pressed)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 16f);
                _keyBadgeBg.color = Color.Lerp(new Color(0.65f, 0.06f, 0.06f, 0.95f), new Color(0.95f, 0.18f, 0.12f, 1f), pulse);
            }

            // 4. Keep The Proctor's face menacingly right in front of the camera lens,
            //    pushing him slightly back as progress increases, or leaning closer if progress drops!
            if (proctor != null && playerCam != null)
            {
                float pushBackOffset = Mathf.Lerp(-0.04f, 0.22f, progress);
                Vector3 struggleJitter = new Vector3(
                    UnityEngine.Random.Range(-0.015f, 0.015f),
                    UnityEngine.Random.Range(-0.015f, 0.015f),
                    0f
                );

                // faceDir points from Proctor toward player camera (-camFwd), so -faceDir pushes Proctor away from camera
                proctor.transform.position = baseTargetPos - faceDir * pushBackOffset + struggleJitter;

                // Keep camera locked on The Proctor's true face center (eyes & nose bridge, 0.10m below top skull bone)
                Vector3 currentFacePos = (headBone != null)
                    ? (headBone.position - Vector3.up * 0.10f)
                    : (proctor.transform.position + Vector3.up * headOffsetY);
                Vector3 lookDir = currentFacePos - playerCam.transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    playerCam.transform.rotation = Quaternion.LookRotation(lookDir);
                }

                // Continuous violent camera struggle shake
                float camShakeIntensity = Mathf.Lerp(0.026f, 0.012f, progress);
                playerCam.transform.localPosition = camBaseLocalPos + new Vector3(
                    UnityEngine.Random.Range(-camShakeIntensity, camShakeIntensity),
                    UnityEngine.Random.Range(-camShakeIntensity, camShakeIntensity),
                    0f
                );
            }

            yield return null;
        }

        // Player reached 100% and broke free!
        if (_qteRoot != null) Destroy(_qteRoot);
        _isQTEActive = false;
        onComplete?.Invoke(true);
    }

    private void BuildQTEUI()
    {
        if (_qteRoot != null) Destroy(_qteRoot);

        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _qteRoot = new GameObject("ProctorStruggleQTE_UI", typeof(RectTransform));
        _qteRoot.transform.SetParent(hud.transform, false);
        var rootRT = _qteRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Container positioned at lower-center of screen so The Proctor's screaming face is unobstructed above
        var panelGO = new GameObject("QTEContainer", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(_qteRoot.transform, false);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.16f);
        panelRT.anchorMax = new Vector2(0.5f, 0.16f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(460f, 125f);

        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.04f, 0.02f, 0.02f, 0.88f);
        panelImg.raycastTarget = false;

        var panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.85f, 0.08f, 0.08f, 0.9f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        // Pulsing [Q] Key Badge on the left/top
        var badgeGO = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(panelGO.transform, false);
        _keyBadgeRT = badgeGO.GetComponent<RectTransform>();
        _keyBadgeRT.anchorMin = new Vector2(0.5f, 1f);
        _keyBadgeRT.anchorMax = new Vector2(0.5f, 1f);
        _keyBadgeRT.pivot = new Vector2(0.5f, 0.5f);
        _keyBadgeRT.anchoredPosition = new Vector2(0f, 10f);
        _keyBadgeRT.sizeDelta = new Vector2(68f, 68f);

        _keyBadgeBg = badgeGO.GetComponent<Image>();
        _keyBadgeBg.color = new Color(0.85f, 0.12f, 0.12f, 1f);
        _keyBadgeBg.raycastTarget = false;

        var badgeOutline = badgeGO.AddComponent<Outline>();
        badgeOutline.effectColor = Color.white;
        badgeOutline.effectDistance = new Vector2(2.5f, -2.5f);

        var keyTxtGO = new GameObject("KeyLetter", typeof(RectTransform), typeof(Text));
        keyTxtGO.transform.SetParent(badgeGO.transform, false);
        var keyTxtRT = keyTxtGO.GetComponent<RectTransform>();
        keyTxtRT.anchorMin = Vector2.zero;
        keyTxtRT.anchorMax = Vector2.one;
        keyTxtRT.offsetMin = Vector2.zero;
        keyTxtRT.offsetMax = Vector2.zero;

        var keyTxt = keyTxtGO.GetComponent<Text>();
        keyTxt.font = font;
        keyTxt.text = "Q";
        keyTxt.fontSize = 38;
        keyTxt.fontStyle = FontStyle.Bold;
        keyTxt.alignment = TextAnchor.MiddleCenter;
        keyTxt.color = Color.white;
        keyTxt.raycastTarget = false;

        // Prompt Instruction Text
        var promptGO = new GameObject("StrugglePromptText", typeof(RectTransform), typeof(Text));
        promptGO.transform.SetParent(panelGO.transform, false);
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.05f, 0.42f);
        promptRT.anchorMax = new Vector2(0.95f, 0.72f);
        promptRT.offsetMin = Vector2.zero;
        promptRT.offsetMax = Vector2.zero;

        _promptText = promptGO.GetComponent<Text>();
        _promptText.font = font;
        _promptText.text = "MASH [Q] RAPIDLY TO BREAK FREE!";
        _promptText.fontSize = 20;
        _promptText.fontStyle = FontStyle.Bold;
        _promptText.alignment = TextAnchor.MiddleCenter;
        _promptText.color = new Color(1f, 0.88f, 0.85f, 1f);
        _promptText.raycastTarget = false;

        // Progress Bar Background
        var barBgGO = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
        barBgGO.transform.SetParent(panelGO.transform, false);
        var barBgRT = barBgGO.GetComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.08f, 0.14f);
        barBgRT.anchorMax = new Vector2(0.92f, 0.36f);
        barBgRT.offsetMin = Vector2.zero;
        barBgRT.offsetMax = Vector2.zero;

        var barBgImg = barBgGO.GetComponent<Image>();
        barBgImg.color = new Color(0.15f, 0.05f, 0.05f, 1f);
        barBgImg.raycastTarget = false;

        // Progress Bar Fill
        var fillGO = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barBgGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2f, 2f);
        fillRT.offsetMax = new Vector2(-2f, -2f);

        _fillBar = fillGO.GetComponent<Image>();
        _fillBar.sprite = CreateWhiteBarSprite();
        _fillBar.type = Image.Type.Filled;
        _fillBar.fillMethod = Image.FillMethod.Horizontal;
        _fillBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillBar.fillAmount = 0.12f;
        _fillBar.color = new Color(0.88f, 0.12f, 0.12f, 1f);
        _fillBar.raycastTarget = false;
    }

    private static Sprite _cachedBarSprite;
    private static Sprite CreateWhiteBarSprite()
    {
        if (_cachedBarSprite != null) return _cachedBarSprite;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < 16; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        _cachedBarSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _cachedBarSprite;
    }
}
