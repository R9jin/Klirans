using System;
using UnityEngine;

/// <summary>
/// InspectableClueMarker — Attached to 3D item models to define hidden clues,
/// codes, and notes that the player can discover by rotating the item in the
/// Resident Evil-style 3D Inspection Viewer.
/// </summary>
public class InspectableClueMarker : MonoBehaviour
{
    [Header("Clue Identity")]
    public string clueID = "Clue_Default";
    public string clueTitle = "Nakatagong Clue";
    [TextArea(2, 5)]
    public string clueDescription = "May nakasulat na impormasyon sa bahaging ito ng gamit.";

    [Header("Angle Detection")]
    [Tooltip("The local direction on the model that must face the inspection camera to reveal the clue.")]
    public Vector3 revealDirection = Vector3.forward;

    [Tooltip("Detection cone half-angle in degrees. Clue is visible within this angle.")]
    [Range(10f, 90f)]
    public float detectionAngle = 45.0f;

    [Header("State")]
    public bool isDiscovered = false;

    // Events
    public static event Action<InspectableClueMarker> OnClueDiscovered;

    /// <summary>
    /// Checks whether this clue's surface is currently facing toward the inspection camera.
    /// </summary>
    public bool IsFacingCamera(Transform cameraTransform, Transform modelRootTransform)
    {
        if (cameraTransform == null || modelRootTransform == null) return false;

        // Calculate world-space normal of the clue's face
        Vector3 worldRevealDir = modelRootTransform.TransformDirection(revealDirection.normalized);

        // Vector from the model towards the camera
        Vector3 towardsCamera = (cameraTransform.position - modelRootTransform.position).normalized;

        // Dot product: 1 = directly facing camera, 0 = 90 deg, -1 = facing away
        float dot = Vector3.Dot(worldRevealDir, towardsCamera);
        float threshold = Mathf.Cos(detectionAngle * Mathf.Deg2Rad);

        return dot >= threshold;
    }

    /// <summary>
    /// Triggers the discovery of the clue.
    /// </summary>
    public void Discover()
    {
        if (!isDiscovered)
        {
            isDiscovered = true;
            Debug.Log($"[InspectableClueMarker] Clue Discovered: {clueTitle} ({clueID})");
            OnClueDiscovered?.Invoke(this);
        }
    }
}
