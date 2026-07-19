using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Head Bobbing")]
    public bool enableHeadBobbing = true;
    public float headBobSpeed = 12f;
    public float headBobAmount = 0.05f;

    [Header("Breathing Camera Effect")]
    public float normalFOV = 60f;
    public float fovBreathingIntensity = 2f;
    public float forwardBreathingIntensity = 0.15f;
    public float breathingSpeed = 3f;
    public float cameraSmoothSpeed = 8f;

    [Header("Vignette Effect (Universal)")]
    [Tooltip("Assign a UI Image with a white or black vignette texture. It will be colored black via code.")]
    public Image vignetteImage;
    public float maxVignetteAlpha = 0.85f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0f;
    private float headBobTimer = 0f;

    private CharacterController characterController;
    private bool canMove = true;
    private bool isMoving = false;
    private bool isRunning = false;
    private bool isCrouching = false;

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
            // Force the initial color to be pure black and transparent
            vignetteImage.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        canMove = enabled;
    }

    void Update()
    {
        UpdateMovementStates();
        HandleMovement();
        HandleStamina();
        HandleCameraAndBreathing();
        HandleMouseLook();
    }

    private void UpdateMovementStates()
    {
        isMoving = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f;
        isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && canMove && staminaSystem != null && staminaSystem.CanSprint;
        isCrouching = Input.GetKey(KeyCode.C) && canMove;
    }

    private void HandleMovement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        float currentSpeed;

        if (staminaSystem != null && !staminaSystem.CanSprint)
        {
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

        if (isCrouching)
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

        Vector3 targetCameraPosition = isCrouching ? crouchingCameraPosition : standingCameraPosition;
        float targetFOV = normalFOV;
        float fatigue = 0f;

        // 1. Head Bobbing (Y-Axis)
        if (enableHeadBobbing && isMoving && characterController.isGrounded)
        {
            headBobTimer += Time.deltaTime * (isRunning ? headBobSpeed * 1.5f : headBobSpeed);
            float currentBobAmount = isRunning ? headBobAmount * 1.5f : headBobAmount;
            targetCameraPosition.y += Mathf.Sin(headBobTimer) * currentBobAmount;
        }
        else
        {
            headBobTimer = 0f; 
        }

        // 2. Breathing Effects & Vignette
        if (staminaSystem != null)
        {
            fatigue = 1f - (staminaSystem.currentStamina / staminaSystem.maxStamina);
            float breathingWave = Mathf.Sin(Time.time * breathingSpeed);

            // Forward/Backward Breathing (Z-Axis)
            float zOffset = breathingWave * forwardBreathingIntensity * fatigue;
            targetCameraPosition.z += zOffset;

            // FOV Breathing
            float fovOffset = breathingWave * fovBreathingIntensity * fatigue;
            targetFOV = normalFOV + fovOffset;

            // Interval Black Vignette
            if (vignetteImage != null)
            {
                // Using a powered sine wave creates a sharp, interval-based pulse
                float intervalPulse = Mathf.Pow(Mathf.Sin(Time.time * (breathingSpeed * 0.5f)), 4f);
                float targetAlpha = intervalPulse * maxVignetteAlpha * fatigue;
                
                // Explicitly set the RGB to 0, 0, 0 (black) and only lerp the alpha channel
                float newAlpha = Mathf.Lerp(vignetteImage.color.a, targetAlpha, cameraSmoothSpeed * Time.deltaTime);
                vignetteImage.color = new Color(0f, 0f, 0f, newAlpha);
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