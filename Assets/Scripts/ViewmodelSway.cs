using UnityEngine;

/// <summary>
/// Adds realistic viewmodel mouse sway and walking step wobble/bobbing to held items like the flashlight.
/// </summary>
public class ViewmodelSway : MonoBehaviour
{
    [Header("Mouse Sway Settings")]
    [Tooltip("Amount of mouse sway lag.")]
    public float mouseSwayAmount = 0.015f;
    public float maxMouseSway = 0.05f;
    public float mouseSwaySmoothness = 8.0f;

    [Header("Walking Wobble / Step Bobbing")]
    [Tooltip("Frequency speed of walking step wobble.")]
    public float walkBobSpeed = 10.0f;

    [Tooltip("Vertical wobble height when taking steps.")]
    public float walkBobAmountY = 0.018f;

    [Tooltip("Horizontal wobble width when taking steps.")]
    public float walkBobAmountX = 0.012f;

    [Tooltip("Rotational tilt wobble (in degrees) with each step.")]
    public float walkBobRotationAmount = 2.5f;

    [Tooltip("Smoothness of bobbing movement.")]
    public float bobSmoothness = 10.0f;

    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;
    private CharacterController characterController;
    private float timer = 0f;

    private void Awake()
    {
        defaultLocalPosition = transform.localPosition;
        defaultLocalRotation = transform.localRotation;
    }

    private void Start()
    {
        characterController = GetComponentInParent<CharacterController>();
        
        // Auto-disable if this item is just sitting in the world as a pickup
        if (characterController == null && GetComponentInParent<Camera>() == null)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused) return;
        HandleMouseSwayAndBobbing();
    }

    private void HandleMouseSwayAndBobbing()
    {
        // 1. Calculate Mouse Sway
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSwayAmount;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSwayAmount;

        mouseX = Mathf.Clamp(mouseX, -maxMouseSway, maxMouseSway);
        mouseY = Mathf.Clamp(mouseY, -maxMouseSway, maxMouseSway);

        Vector3 mouseSwayOffset = new Vector3(-mouseX, -mouseY, 0f);

        // 2. Calculate Walking Step Wobble
        bool isMoving = false;
        if (characterController != null)
        {
            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0, characterController.velocity.z);
            isMoving = horizontalVelocity.magnitude > 0.2f && characterController.isGrounded;
        }
        else
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            isMoving = (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f);
        }

        Vector3 walkBobPosOffset = Vector3.zero;
        Quaternion walkBobRotOffset = Quaternion.identity;

        if (isMoving)
        {
            timer += Time.deltaTime * walkBobSpeed;

            float waveY = Mathf.Sin(timer) * walkBobAmountY;
            float waveX = Mathf.Cos(timer * 0.5f) * walkBobAmountX;
            float waveRotZ = Mathf.Sin(timer * 0.5f) * walkBobRotationAmount;
            float waveRotX = Mathf.Cos(timer) * (walkBobRotationAmount * 0.5f);

            walkBobPosOffset = new Vector3(waveX, waveY, 0f);
            walkBobRotOffset = Quaternion.Euler(waveRotX, 0f, waveRotZ);
        }
        else
        {
            timer = 0f;
        }

        // Combine offsets smoothly with default transform
        Vector3 targetPos = defaultLocalPosition + mouseSwayOffset + walkBobPosOffset;
        Quaternion targetRot = defaultLocalRotation * walkBobRotOffset;

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * bobSmoothness);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * bobSmoothness);
    }

    /// <summary>
    /// Update default transform baseline if position/rotation is adjusted.
    /// </summary>
    public void SetBaseline(Vector3 pos, Quaternion rot)
    {
        defaultLocalPosition = pos;
        defaultLocalRotation = rot;
    }
}
