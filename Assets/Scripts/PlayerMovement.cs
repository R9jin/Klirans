using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for the Universal Vignette

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public StaminaSystem staminaSystem;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 6.5f;
    public float jumpPower = 3.5f;
    public float gravity = 20f;

    [Header("Mouse Look")]
    public float lookSpeed = 1.8f;
    public float lookXLimit = 85f;

    [Header("Crouching")]
    public float defaultHeight = 1.5f;
    public float crouchHeight = 1.2f;
    public float crouchSpeed = 2f;

    [Header("Breathing Camera Effect")]
    public float normalFOV = 60f;
    public float fovBreathingIntensity = 2f;
    public float forwardBreathingIntensity = 0.15f;
    public float breathingSpeed = 3f;
    public float cameraSmoothSpeed = 8f;

    [Header("Vignette Effect (Universal)")]
    [Tooltip("Assign a UI Image with a black vignette texture. Works in both URP and Built-in.")]
    public Image vignetteImage;
    public float maxVignetteAlpha = 0.75f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0f;

    private CharacterController characterController;
    private bool canMove = true;

    private Vector3 standingCameraPosition;
    private Vector3 crouchingCameraPosition;

    private float defaultCenterY;
    private float crouchCenterY;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultCenterY = defaultHeight / 2f;
        crouchCenterY = crouchHeight / 2f;

        characterController.height = defaultHeight;
        characterController.center = new Vector3(0f, defaultCenterY, 0f);

        if (playerCamera != null)
        {
            standingCameraPosition = playerCamera.transform.localPosition;

            crouchingCameraPosition = new Vector3(
                standingCameraPosition.x,
                standingCameraPosition.y - (defaultHeight - crouchHeight),
                standingCameraPosition.z
            );

            playerCamera.fieldOfView = normalFOV;
        }

        if (vignetteImage != null)
        {
            // Ensure the vignette starts completely transparent
            Color vColor = vignetteImage.color;
            vColor.a = 0f;
            vignetteImage.color = vColor;
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        canMove = enabled;
    }

    void Update()
    {
        HandleMovement();
        HandleStamina();
        HandleCameraAndBreathing();
        HandleMouseLook();
    }

    private void HandleMovement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        bool isMoving = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && canMove && staminaSystem != null && staminaSystem.CanSprint;

        float currentSpeed;

        if (staminaSystem != null && !staminaSystem.CanSprint)
        {
            // Exhausted movement penalty
            currentSpeed = walkSpeed * 0.75f;
        }
        else if (isRunning)
        {
            currentSpeed = runSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
        }

        // Apply crouch speed modifier if crouching
        if (Input.GetKey(KeyCode.C) && canMove)
        {
            currentSpeed = crouchSpeed;
            characterController.height = crouchHeight;
            characterController.center = new Vector3(0f, crouchCenterY, 0f);
        }
        else
        {
            characterController.height = defaultHeight;
            characterController.center = new Vector3(0f, defaultCenterY, 0f);
        }

        float curSpeedX = canMove ? currentSpeed * Input.GetAxis("Vertical") : 0f;
        float curSpeedY = canMove ? currentSpeed * Input.GetAxis("Horizontal") : 0f;
        float movementDirectionY = moveDirection.y;

        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleStamina()
    {
        if (staminaSystem == null) return;

        bool isMoving = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && canMove && staminaSystem.CanSprint;

        if (isRunning)
        {
            staminaSystem.Drain();
        }
        else
        {
            staminaSystem.Recover();
        }
    }

    private void HandleCameraAndBreathing()
    {
        if (playerCamera == null) return;

        bool isCrouching = Input.GetKey(KeyCode.C) && canMove;
        Vector3 targetCameraPosition = isCrouching ? crouchingCameraPosition : standingCameraPosition;
        float targetFOV = normalFOV;
        float fatigue = 0f;

        if (staminaSystem != null)
        {
            // Calculate fatigue: 0 is fully rested, 1 is completely exhausted
            fatigue = 1f - (staminaSystem.currentStamina / staminaSystem.maxStamina);

            // Calculate the continuous breathing wave
            float breathingWave = Mathf.Sin(Time.time * breathingSpeed);

            // 1. Dynamic Forward Movement (Z-Axis)
            float zOffset = breathingWave * forwardBreathingIntensity * fatigue;
            targetCameraPosition.z += zOffset;

            // 2. Dynamic FOV
            float fovOffset = breathingWave * fovBreathingIntensity * fatigue;
            targetFOV = normalFOV + fovOffset;

            // 3. Dynamic Vignette
            if (vignetteImage != null)
            {
                // Absolute value so the vignette pulses smoothly in and out
                float vignettePulse = Mathf.Abs(breathingWave) * maxVignetteAlpha * fatigue;
                
                Color vColor = vignetteImage.color;
                vColor.a = Mathf.Lerp(vColor.a, vignettePulse, cameraSmoothSpeed * Time.deltaTime);
                vignetteImage.color = vColor;
            }
        }

        // Apply smoothed positional and FOV changes
        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition, 
            targetCameraPosition, 
            cameraSmoothSpeed * Time.deltaTime
        );

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView, 
            targetFOV, 
            cameraSmoothSpeed * Time.deltaTime
        );
    }

    private void HandleMouseLook()
    {
        if (!canMove) return;

        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        
        transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * lookSpeed, 0f);
    }
}