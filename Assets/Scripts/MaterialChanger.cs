using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MaterialChanger : MonoBehaviour
{
    [Header("Materials")]
    [Tooltip("Add as many materials as you want. The first material is the default/reset material.")]
    public Material[] materials;

    [Header("Settings")]
    [Tooltip("How many seconds before the material resets to the first material.")]
    public float resetDelay = 5f;

    private Renderer objectRenderer;
    private int currentIndex = 0;

    private Coroutine resetCoroutine;

    private void Start()
    {
        objectRenderer = GetComponent<Renderer>();

        // Check if there are any materials assigned
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning("MaterialChanger: No materials have been assigned.", this);
            return;
        }

        // Start with the first material
        if (materials[0] != null)
        {
            objectRenderer.material = materials[0];
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react to the Player or Capsule
        if (other.CompareTag("Player") || other.gameObject.name == "Capsule")
        {
            ChangeToNextMaterial();
        }
    }

    private void ChangeToNextMaterial()
    {
        // Check if there are any materials assigned
        if (materials == null || materials.Length == 0)
        {
            return;
        }

        // Move to the next material
        currentIndex++;

        // If we reach the end of the material list,
        // go back to the first material
        if (currentIndex >= materials.Length)
        {
            currentIndex = 0;
        }

        // Change to the selected material
        if (materials[currentIndex] != null)
        {
            objectRenderer.material = materials[currentIndex];
        }

        // Stop the previous reset timer if one is already running
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
        }

        // Start a new reset timer
        resetCoroutine = StartCoroutine(ResetToFirstMaterialAfterDelay());
    }

    private IEnumerator ResetToFirstMaterialAfterDelay()
    {
        // Wait for the specified number of seconds
        yield return new WaitForSeconds(resetDelay);

        // Reset to the first material
        if (materials != null && materials.Length > 0)
        {
            if (materials[0] != null)
            {
                objectRenderer.material = materials[0];
            }
        }

        // Reset the index so the next trigger starts
        // from the second material again
        currentIndex = 0;

        resetCoroutine = null;
    }
}