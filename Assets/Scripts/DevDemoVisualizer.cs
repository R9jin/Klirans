using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DevDemoVisualizer — High-performance immediate-mode 3D spatial visualizer for KLIRANS.
/// Renders 3D vision cones, hearing/detection radii floor discs, line-of-sight rays, NavMesh paths,
/// overhead billboard badges, and top-left telemetry directly in the Game View.
/// </summary>
public class DevDemoVisualizer : MonoBehaviour
{
    private static Material _lineMaterial;
    private Camera _cam;
    private Transform _playerTransform;

    // Telemetry tracking
    private float _fpsUpdateTimer = 0f;
    private float _currentFps = 60f;

    // UI Styles
    private GUIStyle _hudBoxStyle;
    private GUIStyle _hudHeaderStyle;
    private GUIStyle _hudTextStyle;
    private GUIStyle _badgeBoxStyle;
    private GUIStyle _badgeTitleStyle;
    private GUIStyle _badgeStateFrozenStyle;
    private GUIStyle _badgeStateActiveStyle;
    private bool _guiStylesInit = false;

    private static void CreateLineMaterial()
    {
        if (_lineMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // Always render through walls
        }
    }

    private void Awake()
    {
        FindReferences();
    }

    private void Update()
    {
        if (_cam == null || _playerTransform == null)
        {
            FindReferences();
        }

        // FPS calculate
        _fpsUpdateTimer += Time.unscaledDeltaTime;
        if (_fpsUpdateTimer >= 0.25f)
        {
            _currentFps = 1.0f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsUpdateTimer = 0f;
        }
    }

    private void FindReferences()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
            var pm = playerGO.GetComponent<PlayerMovement>();
            if (pm != null && pm.playerCamera != null) _cam = pm.playerCamera;
        }
        if (_cam == null) _cam = Camera.main;
    }

    // ── 3D GL Immediate Mode Rendering ──────────────────────────────────────

    private void OnRenderObject()
    {
        if (!GameModeManager.IsFreeRoamMode || !DevDemoPanel.IsVisualizerEnabled) return;
        if (_cam == null) FindReferences();
        if (_cam == null) return;

        CreateLineMaterial();
        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);

        var weeping = WeepingStudentAI.ActiveWeepingStudent;
        var proctor = TheProctorAI.ActiveProctor;
        var guard = SecurityGuardStalkerAI.ActiveStalker;

        // 1. Draw Weeping Student Visuals
        if (weeping != null && weeping.gameObject.activeInHierarchy)
        {
            DrawWeepingVisuals(weeping);
        }

        // 2. Draw The Proctor Visuals
        if (proctor != null && proctor.gameObject.activeInHierarchy)
        {
            DrawProctorVisuals(proctor);
        }

        // 3. Draw Guard Stalker Visuals
        if (guard != null && guard.gameObject.activeInHierarchy)
        {
            DrawGuardVisuals(guard);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void DrawWeepingVisuals(WeepingStudentAI weeping)
    {
        Vector3 pos = weeping.transform.position;
        Vector3 fwd = weeping.transform.forward;
        Vector3 headPos = pos + Vector3.up * 1.6f;

        // Camera Eye to Weeping Eye Contact / LOS Ray
        if (DevDemoPanel.ShowLOSRays && _cam != null)
        {
            Vector3 camPos = _cam.transform.position;
            bool isObserved = weeping.IsObserved;

            Color rayColor = isObserved 
                ? new Color(0.1f, 0.95f, 1.0f, 0.95f) // Glowing Cyan = Freeze eye contact locked!
                : new Color(1.0f, 0.2f, 0.2f, 0.50f); // Red = Looking away or occluded

            DrawLine(camPos, headPos, rayColor);
            DrawCrosshairAt(headPos, rayColor, 0.18f);

            // Additional line to chest
            DrawLine(camPos, pos + Vector3.up * 1.0f, rayColor * 0.75f);
        }

        // Vision Cone (Where the student is facing)
        if (DevDemoPanel.ShowVisionCones)
        {
            Color coneColor = weeping.IsObserved
                ? new Color(0.2f, 0.85f, 1.0f, 0.40f)  // Cyan when frozen
                : new Color(1.0f, 0.15f, 0.15f, 0.65f); // Red when actively hunting unobserved
            DrawVisionCone(headPos, fwd, 14.0f, 75.0f, coneColor);
        }

        // Detection & Catch Radii
        if (DevDemoPanel.ShowHearingRadii)
        {
            // Silhouette Freeze Circle (Floor)
            DrawWireCircle(pos + Vector3.up * 0.05f, weeping.silhouetteRadius, new Color(0.7f, 0.2f, 0.9f, 0.75f), 32);

            // Catch / Attack Danger Circle
            DrawWireCircle(pos + Vector3.up * 0.08f, weeping.catchDistance, new Color(1.0f, 0.1f, 0.1f, 0.90f), 24);
        }

        // NavMesh Path Trajectory
        if (DevDemoPanel.ShowNavMeshPaths)
        {
            var agent = weeping.GetComponent<NavMeshAgent>();
            if (agent != null && agent.hasPath)
            {
                DrawNavPath(agent.path.corners, new Color(0.2f, 1.0f, 0.8f, 0.85f));
            }
        }
    }

    private void DrawProctorVisuals(TheProctorAI proctor)
    {
        Vector3 pos = proctor.transform.position;
        Vector3 fwd = proctor.transform.forward;
        Vector3 headPos = pos + Vector3.up * 2.1f;

        // Hearing Radii (Floor circles)
        if (DevDemoPanel.ShowHearingRadii)
        {
            // Sprint Hearing (Green/Cyan)
            DrawWireCircle(pos + Vector3.up * 0.05f, proctor.sprintHearingRange, new Color(0.2f, 0.9f, 0.4f, 0.65f), 36);

            // Walk Hearing (Yellow/Amber)
            DrawWireCircle(pos + Vector3.up * 0.07f, proctor.baseHearingRange, new Color(1.0f, 0.85f, 0.2f, 0.75f), 28);

            // Catch Danger Zone (Red)
            DrawWireCircle(pos + Vector3.up * 0.09f, proctor.catchDistance, new Color(1.0f, 0.1f, 0.1f, 0.90f), 20);
        }

        // Vision Cone (Blind forward listening zone)
        if (DevDemoPanel.ShowVisionCones)
        {
            Color coneCol = (proctor.currentState == TheProctorAI.ProctorState.Chasing)
                ? new Color(1.0f, 0.2f, 0.1f, 0.65f)
                : new Color(0.9f, 0.7f, 0.2f, 0.45f);
            DrawVisionCone(headPos, fwd, 16.0f, 85.0f, coneCol);
        }

        // NavMesh Path
        if (DevDemoPanel.ShowNavMeshPaths)
        {
            var agent = proctor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.hasPath)
            {
                DrawNavPath(agent.path.corners, new Color(1.0f, 0.5f, 0.1f, 0.85f));
            }
        }
    }

    private void DrawGuardVisuals(SecurityGuardStalkerAI guard)
    {
        Vector3 pos = guard.transform.position;
        Vector3 fwd = guard.transform.forward;
        Vector3 headPos = pos + Vector3.up * 1.8f;

        // Vision Cone (Guard flashlight beam reach)
        if (DevDemoPanel.ShowVisionCones)
        {
            Color coneCol = (guard.currentState == SecurityGuardStalkerAI.GuardState.Chasing)
                ? new Color(1.0f, 0.2f, 0.1f, 0.70f)
                : new Color(1.0f, 0.95f, 0.4f, 0.55f);
            DrawVisionCone(headPos, fwd, guard.visionMaxDistance, 70.0f, coneCol);
        }

        // Proximity Radii
        if (DevDemoPanel.ShowHearingRadii)
        {
            DrawWireCircle(pos + Vector3.up * 0.05f, guard.proximityAwarenessRadius, new Color(0.9f, 0.6f, 0.2f, 0.75f), 28);
            DrawWireCircle(pos + Vector3.up * 0.08f, guard.catchDistance, new Color(1.0f, 0.1f, 0.1f, 0.90f), 20);
        }

        // NavMesh Path
        if (DevDemoPanel.ShowNavMeshPaths)
        {
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent != null && agent.hasPath)
            {
                DrawNavPath(agent.path.corners, new Color(1.0f, 0.9f, 0.2f, 0.85f));
            }
        }
    }

    // ── Primitive Drawing Helpers (GL.LINES) ────────────────────────────────

    private void DrawLine(Vector3 start, Vector3 end, Color col)
    {
        GL.Color(col);
        GL.Vertex(start);
        GL.Vertex(end);
    }

    private void DrawWireCircle(Vector3 center, float radius, Color col, int segments)
    {
        GL.Color(col);
        float step = Mathf.PI * 2.0f / segments;
        Vector3 prev = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            GL.Vertex(prev);
            GL.Vertex(next);
            prev = next;
        }
    }

    private void DrawVisionCone(Vector3 origin, Vector3 forward, float distance, float fovAngle, Color col)
    {
        GL.Color(col);
        float halfFov = fovAngle * 0.5f * Mathf.Deg2Rad;
        float radius = distance * Mathf.Tan(halfFov);
        Vector3 coneCenter = origin + forward * distance;

        Quaternion rot = Quaternion.LookRotation(forward);
        Vector3 up = rot * Vector3.up;
        Vector3 right = rot * Vector3.right;

        int rimSegments = 16;
        float step = Mathf.PI * 2.0f / rimSegments;
        Vector3 prevRim = coneCenter + (Mathf.Cos(0) * right + Mathf.Sin(0) * up) * radius;

        // Rim circle & Rays from apex
        for (int i = 1; i <= rimSegments; i++)
        {
            float angle = i * step;
            Vector3 nextRim = coneCenter + (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * radius;

            // Rim edge
            GL.Vertex(prevRim);
            GL.Vertex(nextRim);

            // Apex line
            if (i % 2 == 0)
            {
                GL.Vertex(origin);
                GL.Vertex(nextRim);
            }

            prevRim = nextRim;
        }

        // Center spine ray
        GL.Vertex(origin);
        GL.Vertex(coneCenter);
    }

    private void DrawNavPath(Vector3[] corners, Color col)
    {
        if (corners == null || corners.Length < 2) return;
        GL.Color(col);
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 p0 = corners[i] + Vector3.up * 0.15f;
            Vector3 p1 = corners[i + 1] + Vector3.up * 0.15f;
            GL.Vertex(p0);
            GL.Vertex(p1);

            // Node marker cross
            DrawCrosshairAt(p1, col * 0.8f, 0.15f);
        }
    }

    private void DrawCrosshairAt(Vector3 center, Color col, float size)
    {
        GL.Color(col);
        GL.Vertex(center + Vector3.left * size);
        GL.Vertex(center + Vector3.right * size);
        GL.Vertex(center + Vector3.forward * size);
        GL.Vertex(center + Vector3.back * size);
        GL.Vertex(center + Vector3.up * size);
        GL.Vertex(center + Vector3.down * size);
    }

    // ── OnGUI World Badges & Telemetry Readout ───────────────────────────────

    private void InitGUIStyles()
    {
        if (_guiStylesInit) return;

        Texture2D boxBg = MakeSolidTex(2, 2, new Color(0.05f, 0.07f, 0.09f, 0.85f));

        _hudBoxStyle = new GUIStyle(GUI.skin.box);
        _hudBoxStyle.normal.background = boxBg;
        _hudBoxStyle.padding = new RectOffset(10, 10, 8, 8);

        _hudHeaderStyle = new GUIStyle(GUI.skin.label);
        _hudHeaderStyle.fontSize = 12;
        _hudHeaderStyle.fontStyle = FontStyle.Bold;
        _hudHeaderStyle.normal.textColor = new Color(0.35f, 0.90f, 0.80f);

        _hudTextStyle = new GUIStyle(GUI.skin.label);
        _hudTextStyle.fontSize = 11;
        _hudTextStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);

        _badgeBoxStyle = new GUIStyle(GUI.skin.box);
        _badgeBoxStyle.normal.background = boxBg;
        _badgeBoxStyle.padding = new RectOffset(6, 6, 4, 4);

        _badgeTitleStyle = new GUIStyle(GUI.skin.label);
        _badgeTitleStyle.fontSize = 11;
        _badgeTitleStyle.fontStyle = FontStyle.Bold;
        _badgeTitleStyle.normal.textColor = Color.white;
        _badgeTitleStyle.alignment = TextAnchor.MiddleCenter;

        _badgeStateFrozenStyle = new GUIStyle(GUI.skin.label);
        _badgeStateFrozenStyle.fontSize = 10;
        _badgeStateFrozenStyle.fontStyle = FontStyle.Bold;
        _badgeStateFrozenStyle.normal.textColor = new Color(0.2f, 0.95f, 1.0f);
        _badgeStateFrozenStyle.alignment = TextAnchor.MiddleCenter;

        _badgeStateActiveStyle = new GUIStyle(GUI.skin.label);
        _badgeStateActiveStyle.fontSize = 10;
        _badgeStateActiveStyle.fontStyle = FontStyle.Bold;
        _badgeStateActiveStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f);
        _badgeStateActiveStyle.alignment = TextAnchor.MiddleCenter;

        _guiStylesInit = true;
    }

    private Texture2D MakeSolidTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void OnGUI()
    {
        if (!GameModeManager.IsFreeRoamMode || !DevDemoPanel.IsVisualizerEnabled) return;
        InitGUIStyles();

        // 1. Draw Overhead 3D World Billboard Badges
        if (DevDemoPanel.ShowWorldBadges && _cam != null)
        {
            DrawWorldBadges();
        }

        // 2. Draw Top-Left Screen Telemetry HUD
        if (DevDemoPanel.ShowScreenHUD && !DevDemoPanel.IsOpen)
        {
            DrawTelemetryHUD();
        }
    }

    private void DrawWorldBadges()
    {
        var weeping = WeepingStudentAI.ActiveWeepingStudent;
        if (weeping != null && weeping.gameObject.activeInHierarchy)
        {
            DrawEntityBadge(weeping.transform.position + Vector3.up * 2.1f, "WEEPING STUDENT",
                weeping.IsObserved ? "FROZEN (GAZE LOCKED)" : "ENDO ADVANCING",
                weeping.IsObserved,
                Vector3.Distance(weeping.transform.position, _playerTransform.position));
        }

        var proctor = TheProctorAI.ActiveProctor;
        if (proctor != null && proctor.gameObject.activeInHierarchy)
        {
            DrawEntityBadge(proctor.transform.position + Vector3.up * 2.8f, "THE PROCTOR",
                proctor.currentState.ToString().ToUpper(),
                false,
                Vector3.Distance(proctor.transform.position, _playerTransform.position));
        }

        var guard = SecurityGuardStalkerAI.ActiveStalker;
        if (guard != null && guard.gameObject.activeInHierarchy)
        {
            DrawEntityBadge(guard.transform.position + Vector3.up * 2.2f, "SECURITY GUARD",
                guard.currentState.ToString().ToUpper(),
                false,
                Vector3.Distance(guard.transform.position, _playerTransform.position));
        }
    }

    private void DrawEntityBadge(Vector3 worldPos, string title, string state, bool isFrozen, float distance)
    {
        Vector3 sp = _cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0.2f) return; // Behind camera

        float guiY = Screen.height - sp.y;
        float w = 180f;
        float h = 48f;
        Rect r = new Rect(sp.x - w * 0.5f, guiY - h, w, h);

        GUI.Box(r, GUIContent.none, _badgeBoxStyle);
        GUI.Label(new Rect(r.x, r.y + 2, r.width, 18), $"<b>{title}</b>", _badgeTitleStyle);

        GUIStyle stStyle = isFrozen ? _badgeStateFrozenStyle : _badgeStateActiveStyle;
        GUI.Label(new Rect(r.x, r.y + 20, r.width, 16), $"[{state}]  {distance:F1}m", stStyle);
    }

    private void DrawTelemetryHUD()
    {
        float x = 16f;
        float y = 16f;
        float w = 290f;
        float h = 180f;

        bool isBlackout = (BlackoutManager.Instance != null && BlackoutManager.Instance.IsBlackoutActive);
        var weeping = WeepingStudentAI.ActiveWeepingStudent;
        var proctor = TheProctorAI.ActiveProctor;
        var guard = SecurityGuardStalkerAI.ActiveStalker;

        Rect r = new Rect(x, y, w, h);
        GUI.Box(r, GUIContent.none, _hudBoxStyle);

        GUILayout.BeginArea(r);
        GUILayout.Label("KLIRANS // DEV TELEMETRY (F1/F3/` to open)", _hudHeaderStyle);

        string fpsColor = _currentFps >= 50 ? "#44FF88" : (_currentFps >= 30 ? "#FFAA00" : "#FF4444");
        GUILayout.Label($"Engine: <color={fpsColor}>{_currentFps:F0} FPS</color>   |   GodMode: {(DevDemoPanel.IsGodModeActive ? "<color=#44FF88>ON</color>" : "OFF")}", _hudTextStyle);

        if (_playerTransform != null)
        {
            Vector3 p = _playerTransform.position;
            int floor = p.y > 11.5f ? 3 : (p.y > 5.5f ? 2 : 1);
            GUILayout.Label($"Player: Floor {floor} ({p.x:F1}, {p.y:F1}, {p.z:F1})", _hudTextStyle);
        }

        string gridStr = isBlackout ? "<color=#FF4444>BLACKOUT ACTIVE</color>" : "<color=#44FF88>GRID OK</color>";
        GUILayout.Label($"Power Grid: {gridStr} | Filter: {DevDemoPanel.CurrentEnemyFilter}", _hudTextStyle);

        // Hostile Status
        if (weeping != null)
        {
            string wpState = weeping.IsObserved ? "<color=#00FFFF>FROZEN (GAZE LOCKED)</color>" : "<color=#FF3333>ENDO TELEPORTING</color>";
            float d = _playerTransform != null ? Vector3.Distance(weeping.transform.position, _playerTransform.position) : 0f;
            GUILayout.Label($"Weeping Student: {wpState} ({d:F1}m)", _hudTextStyle);
        }
        else if (proctor != null)
        {
            float d = _playerTransform != null ? Vector3.Distance(proctor.transform.position, _playerTransform.position) : 0f;
            GUILayout.Label($"Proctor: <color=#FFAA00>{proctor.currentState}</color> ({d:F1}m)", _hudTextStyle);
        }
        else if (guard != null)
        {
            float d = _playerTransform != null ? Vector3.Distance(guard.transform.position, _playerTransform.position) : 0f;
            GUILayout.Label($"Guard Stalker: <color=#FFAA00>{guard.currentState}</color> ({d:F1}m)", _hudTextStyle);
        }
        else
        {
            GUILayout.Label("Hostile Entity: <color=#888888>None (Clear)</color>", _hudTextStyle);
        }

        if (DevDemoPanel.PlayerSpeedMultiplier > 1.05f || DevDemoPanel.IsInfiniteStaminaActive || DevDemoPanel.IsSuperFlashlightActive)
        {
            GUILayout.Label($"Active Cheats: Speed {DevDemoPanel.PlayerSpeedMultiplier:F1}x | Stamina: {(DevDemoPanel.IsInfiniteStaminaActive ? "INF" : "Norm")} | Light: {(DevDemoPanel.IsSuperFlashlightActive ? "SUPER" : "Norm")}", _hudTextStyle);
        }

        GUILayout.EndArea();
    }
}
