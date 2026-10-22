using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Klirans.Environment
{
    [RequireComponent(typeof(Collider))]
    public class SchoolExitDoor : MonoBehaviour, IInteractable
    {
        [Header("Exit Settings")]
        [Tooltip("Name of the victory scene to load upon escaping.")]
        public string winSceneName = "WinScene";

        [Tooltip("Delay in seconds before loading win scene (to allow audio/fade).")]
        public float exitDelay = 1.0f;

        [Header("Sound Effects")]
        public AudioClip exitUnlockedSound;
        public AudioClip exitLockedSound;

        private bool _isEscaping = false;
        private AudioSource _audioSource;
        private BoxCollider _solidBlockerCollider;
        private GameObject _rightBarredGateRoot;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0.5f;
            }

            EnsureExitBarredGatesAndSolidBlocker();
        }

        private void Start()
        {
            EnsureExitBarredGatesAndSolidBlocker();
        }

        private void Update()
        {
            if (_solidBlockerCollider != null)
            {
                bool canEscape = (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
                    || (SecurityGuardEndgameManager.Instance != null && SecurityGuardEndgameManager.Instance.isEndgameActive)
                    || _isEscaping;

                // Keep solid physical barrier active whenever clearance is not yet validated
                if (_solidBlockerCollider.enabled == canEscape)
                {
                    _solidBlockerCollider.enabled = !canEscape;
                }
            }
        }

        /// <summary>
        /// Models the iron barred security gate at the right part of the main entrance (and left side portal)
        /// and installs a solid non-trigger BoxCollider across the entire entrance so the player cannot walk past the exit before winning.
        /// </summary>
        private void EnsureExitBarredGatesAndSolidBlocker()
        {
            // Expand the interaction trigger so player can press [E] across the full entrance width in front of the solid bars
            var triggerCol = GetComponent<BoxCollider>();
            if (triggerCol != null && triggerCol.isTrigger)
            {
                triggerCol.size = new Vector3(1.60f, 2.60f, 6.20f);
                triggerCol.center = new Vector3(-0.35f, 0f, 0f);
            }

            // 1. Solid physical blocker spanning the entire Main Entrance (Left, Center, and Right portals)
            Transform existingBlocker = transform.Find("LobbyExit_SolidGateBlocker");
            GameObject blockerGO;
            if (existingBlocker != null)
            {
                blockerGO = existingBlocker.gameObject;
            }
            else
            {
                blockerGO = new GameObject("LobbyExit_SolidGateBlocker");
                blockerGO.transform.SetParent(transform, true);
            }
            blockerGO.layer = 0; // Default layer so CharacterController and raycasts collide with it
            blockerGO.transform.position = new Vector3(-75.48f, 3.65f, 16.50f);
            blockerGO.transform.rotation = Quaternion.identity;
            blockerGO.transform.localScale = Vector3.one;

            _solidBlockerCollider = blockerGO.GetComponent<BoxCollider>();
            if (_solidBlockerCollider == null)
            {
                _solidBlockerCollider = blockerGO.AddComponent<BoxCollider>();
            }
            _solidBlockerCollider.isTrigger = false;
            _solidBlockerCollider.center = Vector3.zero;
            // Spans Z = 12.60 to 20.40 (7.8m wide), Y = 2.05 to 5.25 (3.2m tall), X = 0.42m thick
            _solidBlockerCollider.size = new Vector3(0.42f, 3.20f, 7.80f);

            // 2. Model the Iron Barred Gate at the Right Part of the Entrance (RightPortal: Z = 13.80 to 15.35, X = -75.48, Y = 2.35 to 4.65)
            //    and matching iron bars on the LeftPortal (Z = 17.65 to 19.20) so there are no open unbarred gaps.
            GameObject existingRightGate = GameObject.Find("RightEntrance_BarredGate");
            if (existingRightGate == null)
            {
                _rightBarredGateRoot = new GameObject("RightEntrance_BarredGate");
                _rightBarredGateRoot.transform.position = Vector3.zero;
                _rightBarredGateRoot.transform.rotation = Quaternion.identity;

                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material ironMat = new Material(litShader);
                ironMat.name = "ExitGate_WroughtIron";
                Color ironCol = new Color(0.11f, 0.12f, 0.13f, 1f);
                if (ironMat.HasProperty("_BaseColor")) ironMat.SetColor("_BaseColor", ironCol);
                if (ironMat.HasProperty("_Color")) ironMat.SetColor("_Color", ironCol);
                if (ironMat.HasProperty("_Metallic")) ironMat.SetFloat("_Metallic", 0.78f);
                if (ironMat.HasProperty("_Smoothness")) ironMat.SetFloat("_Smoothness", 0.42f);

                Material brassMat = new Material(litShader);
                brassMat.name = "ExitGate_LockBrass";
                Color brassCol = new Color(0.68f, 0.53f, 0.18f, 1f);
                if (brassMat.HasProperty("_BaseColor")) brassMat.SetColor("_BaseColor", brassCol);
                if (brassMat.HasProperty("_Color")) brassMat.SetColor("_Color", brassCol);
                if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.85f);
                if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.55f);

                // Build Right Entrance Barred Gate (Z = 13.78 to 15.36, Y = 2.35 to 4.65)
                BuildPortalBarredGate(_rightBarredGateRoot.transform, "RightPortal", -75.48f, 13.78f, 15.36f, 2.35f, 4.65f, ironMat, brassMat);

                // Also build matching Left Entrance Barred Gate (Z = 17.64 to 19.22, Y = 2.35 to 4.65)
                BuildPortalBarredGate(_rightBarredGateRoot.transform, "LeftPortal", -75.48f, 17.64f, 19.22f, 2.35f, 4.65f, ironMat, brassMat);
            }
        }

        private void BuildPortalBarredGate(Transform parent, string prefix, float xPos, float zMin, float zMax, float yMin, float yMax, Material ironMat, Material brassMat)
        {
            float zWidth = zMax - zMin;
            float zCenter = (zMin + zMax) * 0.5f;
            float yHeight = yMax - yMin;
            float yCenter = (yMin + yMax) * 0.5f;

            // Outer vertical frame jambs
            CreateGatePart(parent, $"{prefix}_FrameLeft", new Vector3(xPos, yCenter, zMin + 0.03f), new Vector3(0.065f, yHeight, 0.06f), ironMat, true);
            CreateGatePart(parent, $"{prefix}_FrameRight", new Vector3(xPos, yCenter, zMax - 0.03f), new Vector3(0.065f, yHeight, 0.06f), ironMat, true);

            // Center vertical meeting stile (double-leaf gate look)
            CreateGatePart(parent, $"{prefix}_CenterStile", new Vector3(xPos, yCenter, zCenter), new Vector3(0.05f, yHeight, 0.05f), ironMat, false);

            // Horizontal crossbars (bottom sill, lower rail, middle lock rail, upper transom rail, top header)
            CreateGatePart(parent, $"{prefix}_BottomRail", new Vector3(xPos, yMin + 0.04f, zCenter), new Vector3(0.06f, 0.06f, zWidth), ironMat, false);
            CreateGatePart(parent, $"{prefix}_LowerRail", new Vector3(xPos, yMin + 0.35f, zCenter), new Vector3(0.048f, 0.042f, zWidth), ironMat, false);
            CreateGatePart(parent, $"{prefix}_MiddleLockRail", new Vector3(xPos, yMin + 1.12f, zCenter), new Vector3(0.055f, 0.055f, zWidth), ironMat, false);
            CreateGatePart(parent, $"{prefix}_UpperTransomRail", new Vector3(xPos, yMax - 0.32f, zCenter), new Vector3(0.05f, 0.045f, zWidth), ironMat, false);
            CreateGatePart(parent, $"{prefix}_TopHeader", new Vector3(xPos, yMax - 0.03f, zCenter), new Vector3(0.065f, 0.06f, zWidth), ironMat, false);

            // Vertical iron bars across the portal width
            int barCount = 11;
            float innerZMin = zMin + 0.12f;
            float innerZMax = zMax - 0.12f;
            for (int i = 0; i < barCount; i++)
            {
                float t = barCount > 1 ? (float)i / (barCount - 1) : 0.5f;
                float z = Mathf.Lerp(innerZMin, innerZMax, t);
                if (Mathf.Abs(z - zCenter) < 0.04f) continue; // Center stile already occupies midpoint

                // Vertical wrought-iron bar
                CreateGatePart(parent, $"{prefix}_Bar_{i + 1}", new Vector3(xPos, yCenter, z), new Vector3(0.032f, yHeight - 0.08f, 0.032f), ironMat, false);

                // Spear finial tip near the top rail
                var finial = CreateGatePart(parent, $"{prefix}_Finial_{i + 1}", new Vector3(xPos, yMax - 0.16f, z), new Vector3(0.044f, 0.075f, 0.044f), ironMat, false);
                finial.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }

            // Heavy steel lockbox & brass padlock at center lock rail
            CreateGatePart(parent, $"{prefix}_LockBox", new Vector3(xPos - 0.01f, yMin + 1.12f, zCenter), new Vector3(0.085f, 0.18f, 0.16f), ironMat, false);
            CreateGatePart(parent, $"{prefix}_PadlockBody", new Vector3(xPos - 0.055f, yMin + 1.09f, zCenter), new Vector3(0.035f, 0.075f, 0.060f), brassMat, false);
            CreateGatePart(parent, $"{prefix}_PadlockShackle", new Vector3(xPos - 0.055f, yMin + 1.145f, zCenter), new Vector3(0.022f, 0.045f, 0.042f), ironMat, false);

            // Solid BoxCollider on this portal's barred gate as an extra physical barrier
            var gateBarrier = CreateGatePart(parent, $"{prefix}_SolidCollider", new Vector3(xPos, yCenter, zCenter), new Vector3(0.35f, yHeight, zWidth), ironMat, true);
            var mr = gateBarrier.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        private GameObject CreateGatePart(Transform parent, string partName, Vector3 worldPos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;

            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            var col = go.GetComponent<Collider>();
            if (!keepCollider && col != null)
            {
                Destroy(col);
            }
            return go;
        }

        public string GetPrompt()
        {
            if (_isEscaping) return string.Empty;

            // Trigger objective update when player looks at/reads the exit gate prompt
            if (!ObjectiveHUD.HasDiscoveredExitLockdown)
            {
                ObjectiveHUD.HasDiscoveredExitLockdown = true;
                TriggerDiscoveredLockdownObjective();
            }

            bool canEscape = (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
                || (SecurityGuardEndgameManager.Instance != null && SecurityGuardEndgameManager.Instance.isEndgameActive);

            if (canEscape)
            {
                return "Press E to Exit Campus (Clearance Validated)";
            }

            if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear())
            {
                return "Campus Main Gate (Locked - Submit slip to Registrar in Room 104 first)";
            }

            int count = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
            return $"Campus Main Gate (Locked - {count}/6 Signatures Collected)";
        }

        public void Interact()
        {
            if (_isEscaping) return;

            bool canEscape = (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
                || (SecurityGuardEndgameManager.Instance != null && SecurityGuardEndgameManager.Instance.isEndgameActive);

            if (canEscape)
            {
                _isEscaping = true;
                if (_solidBlockerCollider != null) _solidBlockerCollider.enabled = false;
                if (WinCutsceneDirector.Instance != null)
                {
                    WinCutsceneDirector.Instance.PlayWinCutscene();
                }
                else
                {
                    StartCoroutine(EscapeRoutine());
                }
                return;
            }

            bool isFullyClear = ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear();

            if (!isFullyClear)
            {
                if (exitLockedSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(exitLockedSound);
                }

                ObjectiveHUD.HasDiscoveredExitLockdown = true;
                TriggerDiscoveredLockdownObjective();
                return;
            }

            // Fully clear but not submitted yet
            if (exitLockedSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(exitLockedSound);
            }

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Submit Clearance Slip (6/6)",
                    "All 6 department signatures gathered. Return to the University Registrar in Room 104 on the ground floor to authorize gate release."
                );
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isEscaping) return;

            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                bool canEscape = (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
                    || (SecurityGuardEndgameManager.Instance != null && SecurityGuardEndgameManager.Instance.isEndgameActive);

                if (canEscape)
                {
                    _isEscaping = true;
                    if (_solidBlockerCollider != null) _solidBlockerCollider.enabled = false;
                    if (WinCutsceneDirector.Instance != null)
                    {
                        WinCutsceneDirector.Instance.PlayWinCutscene();
                    }
                    else
                    {
                        StartCoroutine(EscapeRoutine());
                    }
                }
                else if (!ObjectiveHUD.HasDiscoveredExitLockdown)
                {
                    ObjectiveHUD.HasDiscoveredExitLockdown = true;
                    TriggerDiscoveredLockdownObjective();
                }
            }
        }

        /// <summary>
        /// Updates the Objective HUD based on current clearance progress upon approaching the exit gate.
        /// </summary>
        private void TriggerDiscoveredLockdownObjective()
        {
            var hud = ObjectiveHUD.Instance ?? FindAnyObjectByType<ObjectiveHUD>();
            if (hud == null) return;

            if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
            {
                hud.SetObjective("Escape The Campus", "Clearance officially validated. The campus main gates are unlocked—escape now!");
                return;
            }

            if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear())
            {
                hud.SetObjective("Submit Clearance Slip (6/6)", "All 6 department signatures gathered. Return to the University Registrar in Room 104 on the ground floor to authorize gate release.");
                return;
            }

            int count = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
            if (count > 0)
            {
                hud.SetObjectiveForSignature(count);
                return;
            }

            bool hasPaper = false;
            var blankItem = Resources.Load<InventoryItem>("Blank_Paper");
#if UNITY_EDITOR
            if (blankItem == null)
                blankItem = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
#endif
            if (blankItem != null)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(blankItem)) hasPaper = true;
            }

            if (hasPaper)
            {
                hud.SetObjective("Head Librarian Clearance (1/6)", "Report to the Head Librarian in Room 308 (3rd Floor) to obtain your first signature.");
                return;
            }

            int fragCount = FragmentManager.Instance != null ? FragmentManager.Instance.GetCollectedCount() : 0;
            hud.SetObjective(
                $"Find or Craft Clearance Slip ({fragCount}/4)",
                $"Search the school corridors for the 4 torn clearance slip fragments ({fragCount}/4) to craft your clearance paper."
            );
        }

        private IEnumerator EscapeRoutine()
        {
            _isEscaping = true;
            if (_solidBlockerCollider != null) _solidBlockerCollider.enabled = false;
            Debug.Log("[SchoolExitDoor] Player successfully escaped the campus!");

            if (exitUnlockedSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(exitUnlockedSound);
            }

            if (ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.SetObjective(
                    "Escape The Campus",
                    "Clearance officially validated. The main gates are open—escaping school grounds..."
                );
            }

            yield return new WaitForSeconds(exitDelay);

            SceneManager.LoadScene(winSceneName);
        }
    }
}
