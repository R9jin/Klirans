using UnityEngine;

/// <summary>
/// Makes ground pickup items float up and down gently and continuously rotate in place like GTA pickup items.
/// Automatically disables itself if the item is equipped in the player's hand (parented under a Camera).
/// </summary>
public class FloatingItemAnimation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation in degrees per second.")]
    public float rotationSpeed = 60.0f;

    [Tooltip("Axis to rotate around (Vector3.up for Y-axis rotation).")]
    public Vector3 rotationAxis = Vector3.up;

    [Header("Floating / Bobbing Settings")]
    [Tooltip("Height amplitude of the floating sine wave.")]
    public float floatAmplitude = 0.12f;

    [Tooltip("Frequency/speed of the floating sine wave.")]
    public float floatFrequency = 2.0f;

    private Vector3 startPosition;

    private void Start()
    {
        // Do not animate if this object is equipped in hand (parented under a Camera or Player viewmodel)
        if (GetComponentInParent<Camera>() != null || (transform.parent != null && transform.parent.name.ToLower().Contains("camera")))
        {
            enabled = false;
            return;
        }

        startPosition = transform.position;
    }

    private void Update()
    {
        // Do not run if parented under Camera
        if (transform.parent != null && transform.parent.GetComponent<Camera>() != null)
        {
            return;
        }

        // Continuous GTA-style spinning in place
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);

        // Smooth sine-wave floating up and down
        float newY = startPosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
