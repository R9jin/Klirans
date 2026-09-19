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

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0.5f;
            }
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

            if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
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

            bool isFullyClear = ClearanceManager.Instance != null && ClearanceManager.Instance.IsFullyClear();
            bool isSubmitted = ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted;

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

            if (!isSubmitted)
            {
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
                return;
            }

            StartCoroutine(EscapeRoutine());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isEscaping) return;

            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                if (ClearanceManager.Instance != null && ClearanceManager.Instance.IsSlipSubmitted)
                {
                    StartCoroutine(EscapeRoutine());
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
            var hud = ObjectiveHUD.Instance ?? FindObjectOfType<ObjectiveHUD>();
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
                if (StorageManager.Instance != null && StorageManager.Instance.HasItem(blankItem)) hasPaper = true;
            }

            if (hasPaper)
            {
                hud.SetObjective("Head Librarian Clearance (1/6)", "Report to the Head Librarian in Room 308 (3rd Floor) to obtain your first signature.");
                return;
            }

            int fragCount = FragmentManager.Instance != null ? FragmentManager.Instance.GetCollectedCount() : 0;
            hud.SetObjective(
                $"Find Clearance Fragments ({fragCount}/4)",
                $"The main exit gates are under security lockdown. Search the building corridors for the 4 torn clearance slip fragments ({fragCount}/4)."
            );
        }

        private IEnumerator EscapeRoutine()
        {
            _isEscaping = true;
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
