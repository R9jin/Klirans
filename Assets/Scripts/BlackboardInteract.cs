using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// System 2: Interactable Blackboard Script
/// Triggers the Riddle UI and handles smooth CanvasGroup fade-in / fade-out when interacted with.
/// </summary>
public class BlackboardInteract : MonoBehaviour, IInteractable
{
    [Header("UI Canvas Group & Text Settings")]
    [Tooltip("The UI Panel CanvasGroup for the blackboard riddle overlay.")]
    public CanvasGroup riddleCanvasGroup;

    [Tooltip("Text element displaying the riddle poem.")]
    public Text riddleText;

    [Tooltip("Optional text element showing press 'E' or 'Esc' to close prompt.")]
    public Text closePromptText;

    [Header("Riddle Content")]
    [TextArea(4, 8)]
    public string riddleMessage = "Your clearance is fractured, your exit denied. Seek the four corners where lost papers hide. Look beneath the steps where the dark shadows crawl, and search the cold rooms to gather them all.";

    [Header("Interaction Options")]
    public string promptText = "Press E to Read Blackboard";
    public float fadeDuration = 0.4f;

    private bool isRiddleActive = false;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        AutoAssignUIReferences();

        // Ensure UI starts hidden
        if (riddleCanvasGroup != null)
        {
            riddleCanvasGroup.alpha = 0f;
            riddleCanvasGroup.interactable = false;
            riddleCanvasGroup.blocksRaycasts = false;
            riddleCanvasGroup.gameObject.SetActive(true);
        }

        if (riddleText != null)
        {
            riddleText.text = riddleMessage;
        }

        if (closePromptText != null)
        {
            closePromptText.text = "Press [E] or [ESC] to close";
        }
    }

    private void AutoAssignUIReferences()
    {
        if (riddleCanvasGroup == null || riddleText == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Transform panel = canvas.transform.Find("RiddlePanel");
                if (panel != null)
                {
                    if (riddleCanvasGroup == null) riddleCanvasGroup = panel.GetComponent<CanvasGroup>();
                    if (riddleText == null)
                    {
                        Transform txt = panel.Find("RiddleText");
                        if (txt != null) riddleText = txt.GetComponent<Text>();
                    }
                    if (closePromptText == null)
                    {
                        Transform closeTxt = panel.Find("ClosePromptText");
                        if (closeTxt != null) closePromptText = closeTxt.GetComponent<Text>();
                    }
                }
            }
        }
    }

    private void Update()
    {
        // Allow player to close riddle UI using Esc or E key when active
        if (isRiddleActive)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                CloseRiddleUI();
            }
        }
    }

    public string GetPrompt()
    {
        return isRiddleActive ? "" : promptText;
    }

    public void Interact()
    {
        if (!isRiddleActive)
        {
            OpenRiddleUI();
        }
        else
        {
            CloseRiddleUI();
        }
    }

    public void OpenRiddleUI()
    {
        AutoAssignUIReferences();
        isRiddleActive = true;
        if (riddleText != null)
        {
            riddleText.text = riddleMessage;
        }

        FadeCanvasGroup(1f);
    }

    public void CloseRiddleUI()
    {
        isRiddleActive = false;
        FadeCanvasGroup(0f);
    }

    private void FadeCanvasGroup(float targetAlpha)
    {
        if (riddleCanvasGroup == null) return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = riddleCanvasGroup.alpha;
        float elapsed = 0f;

        riddleCanvasGroup.interactable = targetAlpha > 0.5f;
        riddleCanvasGroup.blocksRaycasts = targetAlpha > 0.5f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            riddleCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        riddleCanvasGroup.alpha = targetAlpha;
    }
}
