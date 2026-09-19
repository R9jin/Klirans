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

        // Cache Player & Camera
        if (_playerMovement == null) _playerMovement = FindObjectOfType<PlayerMovement>();
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

    private IEnumerator CameraZoomInRoutine()
    {
        if (_playerCam == null || _targetNPC == null) yield break;

        Vector3 headPos = _targetNPC.position + Vector3.up * npcHeadHeight;
        float elapsed = 0f;
        float duration = 0.45f;

        float startFOV = _playerCam.fieldOfView;
        Quaternion startPlayerRot = _playerMovement != null ? _playerMovement.transform.rotation : transform.rotation;
        Quaternion startCamLocalRot = _playerCam.transform.localRotation;

        Vector3 toNpc = (headPos - _playerCam.transform.position);
        float targetYaw = Mathf.Atan2(toNpc.x, toNpc.z) * Mathf.Rad2Deg;
        float distH = Mathf.Sqrt(toNpc.x * toNpc.x + toNpc.z * toNpc.z);
        float targetPitch = Mathf.Clamp(-Mathf.Atan2(toNpc.y, distH) * Mathf.Rad2Deg, -60f, 60f);

        Quaternion targetPlayerRot = Quaternion.Euler(0f, targetYaw, 0f);
        Quaternion targetCamLocalRot = Quaternion.Euler(targetPitch, 0f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (_playerMovement != null)
            {
                _playerMovement.transform.rotation = Quaternion.Slerp(startPlayerRot, targetPlayerRot, t);
            }
            _playerCam.transform.localRotation = Quaternion.Slerp(startCamLocalRot, targetCamLocalRot, t);
            _playerCam.fieldOfView = Mathf.Lerp(startFOV, dialogueFOV, t);

            yield return null;
        }

        if (_playerMovement != null)
        {
            _playerMovement.transform.rotation = targetPlayerRot;
        }
        _playerCam.transform.localRotation = targetCamLocalRot;
        _playerCam.fieldOfView = dialogueFOV;
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

        // Restore player movement controls and camera override
        if (_playerMovement != null)
        {
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
    /// Builds or styles the horror-themed dialogue box on HudCanvas.
    /// </summary>
    public void EnsureDialogueUI()
    {
        if (_dialoguePanel != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        var horrorFont = Resources.Load<Font>("watch people die");
#if UNITY_EDITOR
        if (horrorFont == null)
            horrorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Main Menu/watch people die.ttf");
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

        // RectTransform: centered lower screen, comfortably above hotbar
        var rt = _dialoguePanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.18f, 0.20f);
        rt.anchorMax = new Vector2(0.82f, 0.38f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Background image
        _panelBg = _dialoguePanel.GetComponent<Image>() ?? _dialoguePanel.AddComponent<Image>();
        _panelBg.color = new Color(0.06f, 0.05f, 0.07f, 0.94f); // deep weathered slate

        // Crimson Top Accent Bar
        GameObject barGO = CreateUIChild("TopBar", _dialoguePanel.transform);
        var barRt = barGO.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0.96f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.offsetMin = Vector2.zero;
        barRt.offsetMax = Vector2.zero;
        var barImg = barGO.GetComponent<Image>() ?? barGO.AddComponent<Image>();
        barImg.color = new Color(0.85f, 0.12f, 0.12f, 1f); // blood red trim

        // NPC Title Text
        GameObject nameGO = CreateUIChild("NPCNameText", _dialoguePanel.transform);
        var nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.03f, 0.72f);
        nameRt.anchorMax = new Vector2(0.60f, 0.94f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        _npcNameText = nameGO.GetComponent<Text>() ?? nameGO.AddComponent<Text>();
        if (horrorFont != null) _npcNameText.font = horrorFont;
        _npcNameText.fontSize = 24;
        _npcNameText.color = new Color(0.95f, 0.20f, 0.20f, 1f); // vivid crimson horror
        _npcNameText.alignment = TextAnchor.MiddleLeft;

        // Department / Room Location Subtitle
        GameObject deptGO = CreateUIChild("DeptSubtitle", _dialoguePanel.transform);
        var deptRt = deptGO.GetComponent<RectTransform>();
        deptRt.anchorMin = new Vector2(0.40f, 0.72f);
        deptRt.anchorMax = new Vector2(0.97f, 0.94f);
        deptRt.offsetMin = Vector2.zero;
        deptRt.offsetMax = Vector2.zero;

        _npcDeptText = deptGO.GetComponent<Text>() ?? deptGO.AddComponent<Text>();
        _npcDeptText.fontSize = 12;
        _npcDeptText.fontStyle = FontStyle.Bold;
        _npcDeptText.color = new Color(0.68f, 0.64f, 0.60f, 0.9f);
        _npcDeptText.alignment = TextAnchor.MiddleRight;

        // Body Text
        GameObject bodyGO = CreateUIChild("DialogueText", _dialoguePanel.transform);
        var bodyRt = bodyGO.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.03f, 0.12f);
        bodyRt.anchorMax = new Vector2(0.97f, 0.70f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        _bodyText = bodyGO.GetComponent<Text>() ?? bodyGO.AddComponent<Text>();
        _bodyText.fontSize = 18;
        _bodyText.lineSpacing = 1.15f;
        _bodyText.color = new Color(0.96f, 0.94f, 0.90f, 1f); // warm legible off-white
        _bodyText.alignment = TextAnchor.UpperLeft;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Truncate;

        // Subtle Drop Shadow on Text
        var shadow = bodyGO.GetComponent<Shadow>() ?? bodyGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Blinking Continue Indicator
        GameObject promptGO = CreateUIChild("PromptIndicator", _dialoguePanel.transform);
        var promptRt = promptGO.GetComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0.70f, 0.02f);
        promptRt.anchorMax = new Vector2(0.98f, 0.18f);
        promptRt.offsetMin = Vector2.zero;
        promptRt.offsetMax = Vector2.zero;

        _promptIndicator = promptGO.GetComponent<Text>() ?? promptGO.AddComponent<Text>();
        _promptIndicator.text = "▼ [E: Continue / Close]";
        _promptIndicator.fontSize = 12;
        _promptIndicator.color = new Color(0.95f, 0.25f, 0.25f, 1f);
        _promptIndicator.alignment = TextAnchor.LowerRight;

        _dialoguePanel.SetActive(false);
    }
}
