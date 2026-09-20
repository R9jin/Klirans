using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cinematic Horror Dialogue System:
/// 1. Smoothly pans and zooms the player's camera to the NPC's face.
/// 2. Plays a character-by-character typewriter text animation with instant skip on click/E.
/// 3. Renders a stylized, retro survival-horror dialogue frame with crimson department headers.
/// </summary>
public class NPCDialogueSystem : MonoBehaviour
{
    public static NPCDialogueSystem Instance { get; private set; }

    [Header("Camera Face Zoom Settings")]
    public float dialogueFOV = 40f;
    public float zoomSpeed = 5.0f;
    public float npcHeadHeight = 1.52f;

    [Header("Typewriter Settings")]
    public float typeSpeed = 0.022f;

    [Header("Audio")]
    public AudioClip typeTickClip;

    // UI References on HudCanvas
    private GameObject _dialoguePanel;
    private Text _npcNameText;
    private Text _npcDeptText;
    private Text _bodyText;
    private Text _promptIndicator;
    private Image _panelBg;

    // State
    private bool _isDialogueActive = false;
    private bool _isTyping = false;
    private string _currentFullMessage = "";
    private Coroutine _typewriterCoroutine;
    private Coroutine _cameraZoomCoroutine;

    // Camera restore cache
    private Camera _playerCam;
    private PlayerMovement _playerMovement;
    private float _savedFOV = 60f;
    private Quaternion _savedCamRot;
    private Quaternion _savedPlayerRot;
    private Transform _targetNPC;
    private ClearanceNPC _activeNPC;

    public bool IsDialogueActive => _isDialogueActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnsureDialogueUI();
    }

    private void Update()
    {
        if (!_isDialogueActive) return;

        // Player input: E, Space, or Left Mouse Click to advance or skip
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            if (_isTyping)
            {
                // Instant skip to end of text
                SkipTypewriter();
            }
            else
            {
                // Close dialogue
                EndDialogue();
            }
        }
    }

    private System.Action _onDialogueClosedCallback;

    /// <summary>
    /// Starts dialogue with an NPC: zooms to their face and types out message.
    /// </summary>
    public void StartDialogue(ClearanceNPC npc, string message, string locationSubtitle = "")
    {
        EnsureDialogueUI();

        _activeNPC = npc;
        _targetNPC = npc != null ? npc.transform : null;
        _currentFullMessage = message;
        _isDialogueActive = true;
        _onDialogueClosedCallback = null;

        // Cache Player & Camera
        if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerCam == null) _playerCam = Camera.main;
        if (_playerCam == null && _playerMovement != null) _playerCam = _playerMovement.playerCamera;

        // Disable player movement & mouse look during cinematic conversation
        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
            _playerMovement.dialogueTargetFOV = dialogueFOV;
            _savedFOV = _playerMovement.normalFOV > 0 ? _playerMovement.normalFOV : 60f;
        }

        // Set Headers
        if (_npcNameText != null && npc != null)
        {
            _npcNameText.text = $"[ {npc.npcName.ToUpper()} ]";
        }
        if (_npcDeptText != null)
        {
            if (string.IsNullOrEmpty(locationSubtitle) && npc != null)
            {
                locationSubtitle = GetDepartmentSubtitle(npc.signatureIndex);
            }
            _npcDeptText.text = locationSubtitle;
        }

        if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

        // Start Camera Zoom
        if (_cameraZoomCoroutine != null) StopCoroutine(_cameraZoomCoroutine);
        _cameraZoomCoroutine = StartCoroutine(CameraZoomInRoutine());

        // Start Typewriter
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(message));
    }

    /// <summary>
    /// Overload for generic NPCs (e.g. Security Guard) without ClearanceNPC.
    /// </summary>
    public void StartDialogue(Transform targetNPC, string npcTitle, string message, string locationSubtitle = "", System.Action onClose = null)
    {
        EnsureDialogueUI();

        _activeNPC = null;
        _targetNPC = targetNPC;
        _currentFullMessage = message;
        _isDialogueActive = true;
        _onDialogueClosedCallback = onClose;

        if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerCam == null) _playerCam = Camera.main;
        if (_playerCam == null && _playerMovement != null) _playerCam = _playerMovement.playerCamera;

        if (_playerMovement != null)
        {
            _playerMovement.SetControlsEnabled(false);
            _playerMovement.isDialogueCameraOverride = true;
            _playerMovement.dialogueTargetFOV = dialogueFOV;
            _savedFOV = _playerMovement.normalFOV > 0 ? _playerMovement.normalFOV : 60f;
        }

        if (_npcNameText != null)
        {
            _npcNameText.text = $"[ {npcTitle.ToUpper()} ]";
        }
        if (_npcDeptText != null)
        {
            _npcDeptText.text = locationSubtitle;
        }

        if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

        if (_cameraZoomCoroutine != null) StopCoroutine(_cameraZoomCoroutine);
        _cameraZoomCoroutine = StartCoroutine(CameraZoomInRoutine());

        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(message));
    }

    private string GetDepartmentSubtitle(int sigIndex)
    {
        switch (sigIndex)
        {
            case 0: return "CAMPUS LIBRARY — 3RD FLOOR (RM 308)";
            case 1: return "GUIDANCE & COUNSELING — 2ND FLOOR (RM 207)";
            case 2: return "COLLEGE OF COMPUTING STUDIES — 2ND FLOOR (RM 202)";
            case 3: return "UNIVERSITY REGISTRAR — GROUND FLOOR (RM 104)";
            case 4: return "UNIVERSITY CASHIER — GROUND FLOOR (RM 102)";
            case 5: return "OFFICE OF THE EXECUTIVE VICE PRESIDENT — GROUND FLOOR (RM 103)";
            default: return "FACULTY CLEARANCE OFFICE";
        }
    }

    /// <summary>
    /// Locates the head transform on an NPC (checking humanoid bones or mixamo bone hierarchy).
    /// </summary>
    public static Transform FindNPCHeadTransform(Transform root)
    {
        if (root == null) return null;

        var anim = root.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var h = anim.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return h;
        }

        var allTransforms = root.GetComponentsInChildren<Transform>();
        Transform candidate = null;
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.EndsWith(":head") || lower == "head")
            {
                return t;
            }
            if (candidate == null && lower.Contains("head"))
            {
                candidate = t;
            }
        }
        return candidate;
    }

    private IEnumerator CameraZoomInRoutine()
    {
        if (_playerCam == null || _targetNPC == null) yield break;

        Transform headBone = FindNPCHeadTransform(_targetNPC);

        float elapsed = 0f;
        float duration = 0.40f;
        float startFOV = _playerCam.fieldOfView;
        Quaternion startPlayerRot = _playerMovement != null ? _playerMovement.transform.rotation : transform.rotation;
        Quaternion startCamLocalRot = _playerCam.transform.localRotation;

        // Initial smooth transition to NPC's head
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            Vector3 headPos = headBone != null ? headBone.position : (_targetNPC.position + Vector3.up * 1.45f);
            Vector3 toHead = headPos - _playerCam.transform.position;
            float distH = Mathf.Sqrt(toHead.x * toHead.x + toHead.z * toHead.z);

            if (distH > 0.01f)
            {
                float targetYaw = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
                float targetPitch = Mathf.Clamp(-Mathf.Atan2(toHead.y, distH) * Mathf.Rad2Deg, -60f, 60f);

                Quaternion targetPlayerRot = Quaternion.Euler(0f, targetYaw, 0f);
                Quaternion targetCamRot = Quaternion.Euler(targetPitch, 0f, 0f);

                if (_playerMovement != null)
                {
                    _playerMovement.transform.rotation = Quaternion.Slerp(startPlayerRot, targetPlayerRot, t);
                }
                _playerCam.transform.localRotation = Quaternion.Slerp(startCamLocalRot, targetCamRot, t);
            }

            _playerCam.fieldOfView = Mathf.Lerp(startFOV, dialogueFOV, t);
            yield return null;
        }

        _playerCam.fieldOfView = dialogueFOV;

        // Continuous tracking loop: locks directly on the NPC's head for the remainder of dialogue
        while (_isDialogueActive)
        {
            if (_playerCam == null || _targetNPC == null) yield break;

            Vector3 headPos = headBone != null ? headBone.position : (_targetNPC.position + Vector3.up * 1.45f);
            Vector3 toHead = headPos - _playerCam.transform.position;
            float distH = Mathf.Sqrt(toHead.x * toHead.x + toHead.z * toHead.z);

            if (distH > 0.01f)
            {
                float targetYaw = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
                float targetPitch = Mathf.Clamp(-Mathf.Atan2(toHead.y, distH) * Mathf.Rad2Deg, -60f, 60f);

                Quaternion targetPlayerRot = Quaternion.Euler(0f, targetYaw, 0f);
                Quaternion targetCamRot = Quaternion.Euler(targetPitch, 0f, 0f);

                if (_playerMovement != null)
                {
                    _playerMovement.transform.rotation = Quaternion.Slerp(_playerMovement.transform.rotation, targetPlayerRot, Time.deltaTime * 10f);
                }
                _playerCam.transform.localRotation = Quaternion.Slerp(_playerCam.transform.localRotation, targetCamRot, Time.deltaTime * 10f);
            }

            yield return null;
        }
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        _isTyping = true;
        if (_bodyText != null) _bodyText.text = "";
        if (_promptIndicator != null) _promptIndicator.gameObject.SetActive(false);

        var wait = new WaitForSeconds(typeSpeed);

        for (int i = 0; i <= fullText.Length; i++)
        {
            if (!_isTyping) yield break;

            if (_bodyText != null)
            {
                _bodyText.text = fullText.Substring(0, i);
            }
            yield return wait;
        }

        _isTyping = false;
        if (_promptIndicator != null) _promptIndicator.gameObject.SetActive(true);
    }

    private void SkipTypewriter()
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _isTyping = false;
        if (_bodyText != null) _bodyText.text = _currentFullMessage;
        if (_promptIndicator != null) _promptIndicator.gameObject.SetActive(true);
    }

    public void EndDialogue()
    {
        _isDialogueActive = false;
        _isTyping = false;

        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

        // Reset talking animations & audio on NPC
        if (_activeNPC != null)
        {
            _activeNPC.StopVoiceAudio();
        }

        if (_onDialogueClosedCallback != null)
        {
            var cb = _onDialogueClosedCallback;
            _onDialogueClosedCallback = null;
            cb.Invoke();
        }

        // Camera restore
        if (_cameraZoomCoroutine != null) StopCoroutine(_cameraZoomCoroutine);
        _cameraZoomCoroutine = StartCoroutine(CameraZoomOutRoutine());
    }

    private IEnumerator CameraZoomOutRoutine()
    {
        if (_playerCam == null) yield break;

        float elapsed = 0f;
        float duration = 0.35f;
        float startFOV = _playerCam.fieldOfView;

        // Easing back to normal FOV
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _playerCam.fieldOfView = Mathf.Lerp(startFOV, _savedFOV, t);
            yield return null;
        }

        _playerCam.fieldOfView = _savedFOV;

        // Restore player movement controls and camera override with smooth pitch sync
        if (_playerMovement != null)
        {
            float curPitch = _playerCam.transform.localEulerAngles.x;
            if (curPitch > 180f) curPitch -= 360f;
            _playerMovement.SyncPitch(curPitch);
            _playerMovement.isDialogueCameraOverride = false;
            _playerMovement.SetControlsEnabled(true);
        }
    }

    private GameObject CreateUIChild(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// Builds or styles the paper-themed horror dialogue box on HudCanvas.
    /// Positioned low on screen so it never obstructs NPCs or environments.
    /// </summary>
    public void EnsureDialogueUI()
    {
        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        var horrorFont = Resources.Load<Font>("watch people die");
#if UNITY_EDITOR
        if (horrorFont == null)
            horrorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
#endif

        var standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        Sprite paperSprite = Resources.Load<Sprite>("ScrambledPaper_HUD");
#if UNITY_EDITOR
        if (paperSprite == null)
            paperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PlayerAssets/ScrambledPaper_HUD.png");
#endif

        // Find or create DialogueBox root
        Transform existing = hud.transform.Find("DialogueBox");
        if (existing != null)
        {
            _dialoguePanel = existing.gameObject;
        }
        else
        {
            _dialoguePanel = new GameObject("DialogueBox", typeof(RectTransform));
            _dialoguePanel.transform.SetParent(hud.transform, false);
        }

        // RectTransform: positioned comfortably at the bottom of the screen with generous room for large readable text
        var rt = _dialoguePanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.02f);
        rt.anchorMax = new Vector2(0.92f, 0.26f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Aged paper background
        _panelBg = _dialoguePanel.GetComponent<Image>() ?? _dialoguePanel.AddComponent<Image>();
        if (paperSprite != null)
        {
            _panelBg.sprite = paperSprite;
            _panelBg.type = Image.Type.Simple;
            _panelBg.color = new Color(0.98f, 0.96f, 0.92f, 0.98f);
        }
        else
        {
            _panelBg.color = new Color(0.92f, 0.88f, 0.82f, 0.96f);
        }

        // Disable legacy neon TopBar if present (authentic paper has torn/tape edges)
        Transform topBarT = _dialoguePanel.transform.Find("TopBar");
        if (topBarT != null)
        {
            topBarT.gameObject.SetActive(false);
        }

        // NPC Title Text (Crimson Stamped Ink - Large & Prominent)
        GameObject nameGO = CreateUIChild("NPCNameText", _dialoguePanel.transform);
        var nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.05f, 0.68f);
        nameRt.anchorMax = new Vector2(0.55f, 0.94f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        _npcNameText = nameGO.GetComponent<Text>() ?? nameGO.AddComponent<Text>();
        if (horrorFont != null) _npcNameText.font = horrorFont;
        _npcNameText.fontSize = 32;
        _npcNameText.color = new Color(0.72f, 0.06f, 0.06f, 1f); // stamped crimson horror ink
        _npcNameText.alignment = TextAnchor.MiddleLeft;
        _npcNameText.verticalOverflow = VerticalWrapMode.Overflow;

        var typewriterFont = Resources.Load<Font>("Fonts/Typewriter_Bold");
        if (typewriterFont == null) typewriterFont = Resources.Load<Font>("Typewriter_Bold");
#if UNITY_EDITOR
        if (typewriterFont == null)
            typewriterFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
        if (typewriterFont == null)
            typewriterFont = standardFont;

        // Department / Room Location Subtitle (Crisp Weathered Charcoal Typewriter)
        GameObject deptGO = CreateUIChild("DeptSubtitle", _dialoguePanel.transform);
        var deptRt = deptGO.GetComponent<RectTransform>();
        deptRt.anchorMin = new Vector2(0.50f, 0.68f);
        deptRt.anchorMax = new Vector2(0.95f, 0.94f);
        deptRt.offsetMin = Vector2.zero;
        deptRt.offsetMax = Vector2.zero;

        _npcDeptText = deptGO.GetComponent<Text>() ?? deptGO.AddComponent<Text>();
        _npcDeptText.font = typewriterFont;
        _npcDeptText.fontSize = 16;
        _npcDeptText.fontStyle = FontStyle.Bold;
        _npcDeptText.color = new Color(0.25f, 0.20f, 0.20f, 1f);
        _npcDeptText.alignment = TextAnchor.MiddleRight;
        _npcDeptText.verticalOverflow = VerticalWrapMode.Overflow;

        // Body Text (Large Bold Typewriter Ink on Paper - Highly Readable & Thematic)
        GameObject bodyGO = CreateUIChild("DialogueText", _dialoguePanel.transform);
        var bodyRt = bodyGO.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.05f, 0.16f);
        bodyRt.anchorMax = new Vector2(0.95f, 0.66f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        _bodyText = bodyGO.GetComponent<Text>() ?? bodyGO.AddComponent<Text>();
        _bodyText.font = typewriterFont;
        _bodyText.fontSize = 24;
        _bodyText.fontStyle = FontStyle.Bold;
        _bodyText.lineSpacing = 1.25f;
        _bodyText.color = new Color(0.06f, 0.04f, 0.04f, 1f); // solid rich black ink
        _bodyText.alignment = TextAnchor.UpperLeft;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        var shadow = bodyGO.GetComponent<Shadow>();
        if (shadow != null) Destroy(shadow);

        // Continue Indicator
        GameObject promptGO = CreateUIChild("PromptIndicator", _dialoguePanel.transform);
        var promptRt = promptGO.GetComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0.65f, 0.03f);
        promptRt.anchorMax = new Vector2(0.95f, 0.18f);
        promptRt.offsetMin = Vector2.zero;
        promptRt.offsetMax = Vector2.zero;

        _promptIndicator = promptGO.GetComponent<Text>() ?? promptGO.AddComponent<Text>();
        _promptIndicator.text = "▼ [E / Space: Continue]";
        _promptIndicator.font = typewriterFont;
        _promptIndicator.fontSize = 15;
        _promptIndicator.fontStyle = FontStyle.Bold;
        _promptIndicator.color = new Color(0.70f, 0.08f, 0.08f, 1f);
        _promptIndicator.alignment = TextAnchor.LowerRight;
        _promptIndicator.verticalOverflow = VerticalWrapMode.Overflow;

        if (!_isDialogueActive)
        {
            _dialoguePanel.SetActive(false);
        }
    }
}
