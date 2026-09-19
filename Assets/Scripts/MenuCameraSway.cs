using UnityEngine;

/// <summary>
/// Attached to the Main Menu 3D camera to give an organic, cinematic breathing sway
/// and subtle responsive mouse parallax effect looking into the campus lobby.
/// </summary>
public class MenuCameraSway : MonoBehaviour
{
    [Header("Breathing Sway Settings")]
    [Tooltip("Speed of the organic sway cycle.")]
    public float swaySpeed = 0.5f;

    [Tooltip("Positional sway amplitude in meters.")]
    public float positionSwayAmount = 0.035f;

    [Tooltip("Rotational sway amplitude in degrees.")]
    public float rotationSwayAmount = 0.45f;

    [Header("Mouse Parallax Settings")]
    [Tooltip("Whether moving the mouse subtly pans the camera.")]
    public bool enableMouseParallax = true;

    [Tooltip("Strength of the mouse parallax angle.")]
    public float mouseParallaxFactor = 1.25f;

    [Tooltip("Smoothing speed for mouse tracking.")]
    public float mouseSmoothSpeed = 4.0f;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector2 _currentMouseOffset;

    private void Start()
    {
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
    }

    private void Update()
    {
        float time = Time.time * swaySpeed;

        // Subtle organic Perlin/sine breathing motion
        float sin1 = Mathf.Sin(time);
        float cos1 = Mathf.Cos(time * 0.85f);
        float sin2 = Mathf.Sin(time * 0.65f);

        Vector3 posOffset = new Vector3(
            cos1 * positionSwayAmount * 0.6f,
            sin1 * positionSwayAmount,
            sin2 * positionSwayAmount * 0.4f
        );

        Vector3 rotOffset = new Vector3(
            sin1 * rotationSwayAmount * 0.7f,
            cos1 * rotationSwayAmount,
            sin2 * rotationSwayAmount * 0.35f
        );

        // Optional mouse parallax
        if (enableMouseParallax)
        {
            float normX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float normY = (Input.mousePosition.y / Screen.height) - 0.5f;

            Vector2 targetOffset = new Vector2(normX, normY) * mouseParallaxFactor;
            _currentMouseOffset = Vector2.Lerp(_currentMouseOffset, targetOffset, Time.deltaTime * mouseSmoothSpeed);

            rotOffset.y += _currentMouseOffset.x;
            rotOffset.x -= _currentMouseOffset.y;
        }

        transform.localPosition = _initialPosition + posOffset;
        transform.localRotation = _initialRotation * Quaternion.Euler(rotOffset);
    }
}
