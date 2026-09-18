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

                int count = ClearanceManager.Instance != null ? ClearanceManager.Instance.SignatureCount : 0;
                ShowExitNotice($"The campus main gates are locked. You only have {count}/6 required clearance signatures. Finish collecting all department signatures before attempting to leave.");
                return;
            }

            if (!isSubmitted)
            {
                if (exitLockedSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(exitLockedSound);
                }

                ShowExitNotice("All 6 signatures collected, but your slip is not yet submitted! Return to the Office of the University Registrar in Room 104 to officially submit and validate your clearance.");
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
            }
        }

        private IEnumerator EscapeRoutine()
        {
            _isEscaping = true;
            Debug.Log("[SchoolExitDoor] Player successfully escaped the campus!");

            if (exitUnlockedSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(exitUnlockedSound);
            }

            ShowExitNotice("Clearance Verified. Escaping campus...");

            yield return new WaitForSeconds(exitDelay);

            SceneManager.LoadScene(winSceneName);
        }

        private void ShowExitNotice(string message)
        {
            GameObject hud = GameObject.Find("HudCanvas");
            if (hud != null)
            {
                Transform diagTrans = hud.transform.Find("DialogueBox");
                if (diagTrans != null)
                {
                    diagTrans.gameObject.SetActive(true);
                    var txt = diagTrans.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (txt != null)
                    {
                        txt.text = $"<b>[CAMPUS SECURITY EXIT]</b>\n\"{message}\"";
                    }

                    StartCoroutine(HideNoticeRoutine(diagTrans.gameObject, 5.0f));
                }
            }
        }

        private IEnumerator HideNoticeRoutine(GameObject box, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (box != null && !_isEscaping)
            {
                box.SetActive(false);
            }
        }
    }
}
