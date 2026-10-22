using UnityEngine;

/// <summary>
/// Controls ground pickup item visual behavior.
/// Animation/Spinning disabled per user request for realistic grounded items.
/// </summary>
public class FloatingItemAnimation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation in degrees per second (Set to 0 for realistic static items).")]
    public float rotationSpeed = 0.0f;

    [Tooltip("Axis to rotate around.")]
    public Vector3 rotationAxis = Vector3.up;

    [Header("Floating / Bobbing Settings")]
    [Tooltip("Height amplitude of the floating sine wave (Set to 0 for realistic static items).")]
    public float floatAmplitude = 0.0f;

    [Tooltip("Frequency/speed of the floating sine wave.")]
    public float floatFrequency = 0.0f;

    private void Start()
    {
        // Disable component so Update never runs and items rest realistically on the ground
        enabled = false;
    }
}

