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
    
    [Header("Audio")]
    public AudioSource footstepAudioSource;
    public bool enableDiegeticFootsteps = true;
    [Range(0f, 1f)] public float footstepVolume = 0.4f;
    [Range(0f, 1f)] public float stereoPanAmount = 0.8f; 
    public AudioReverbPreset footstepReverbPreset = AudioReverbPreset.Hallway;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 6.5f;
    public float exhaustedSpeed = 2f;
    public float jumpPower = 3.5f;
    public float gravity = 20f;

    [Header("Mouse Look")]
    public float lookSpeed = 1.8f;
    public float lookXLimit = 85f;

    [Header("Crouching")]
    public float defaultHeight = 1.50f;
    public float crouchHeight = 1.10f;
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
    public float maxVignetteAlpha = 0.25f;

    [Header("Dialogue Camera Override")]
    public bool isDialogueCameraOverride = false;
    public float dialogueTargetFOV = 38f;

    [Header("Jumpscare Camera Lock")]
    [Tooltip("True while a jumpscare sequence locks camera zoom and orientation.")]
    public bool isJumpscareCameraOverride = false;

    private Vector3 moveDirection = Vector3.zero;
    private float verticalVelocity = 0f;
    private float rotationX = 0f;
    private float headBobTimer = 0f;

    private CharacterController characterController;
    private bool canMove = true;
    private bool isMoving = false;
    private bool isRunning = false;
    private bool isCrouching = false;

    public bool IsMoving => isMoving;
    public bool IsRunning => isRunning;
    public bool IsCrouching => isCrouching;

    private Vector3 standingCameraPosition;
    private Vector3 crouchingCameraPosition;

    private float defaultCenterY;
    private float crouchCenterY;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Ensure player height doesn't exceed 1.55m so player can walk under 1.85m door lintels
        if (defaultHeight > 1.55f) defaultHeight = 1.50f;
        if (crouchHeight > 1.15f) crouchHeight = 1.10f;

        defaultCenterY = defaultHeight / 2f;
        crouchCenterY = crouchHeight / 2f;

        characterController.height = defaultHeight;
        characterController.center = new Vector3(0f, defaultCenterY, 0f);
        characterController.radius = 0.25f;
        characterController.skinWidth = 0.03f;
        characterController.stepOffset = 0.30f;

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

        if (footstepAudioSource != null && enableDiegeticFootsteps)
        {
            footstepAudioSource.volume = footstepVolume;
            AudioReverbFilter reverb = footstepAudioSource.gameObject.GetComponent<AudioReverbFilter>();
            if (reverb == null)
            {
                reverb = footstepAudioSource.gameObject.AddComponent<AudioReverbFilter>();
            }
            reverb.reverbPreset = footstepReverbPreset;
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled && footstepAudioSource != null && footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Pause();
        }
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
        isMoving = canMove && (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f);
        isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && canMove && staminaSystem != null && staminaSystem.CanSprint;
        
        bool wantsCrouch = (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) && canMove;
        if (!wantsCrouch && isCrouching)
        {
            // Only remain crouched if there is a low ceiling/obstacle blocking standing up
            isCrouching = !CanUncrouch();
        }
        else
        {
            isCrouching = wantsCrouch;
        }
    }

    private bool CanUncrouch()
    {
        if (characterController == null) return true;

        // Check overhead clearance from crouchHeight to defaultHeight
        float radius = characterController.radius * 0.8f;
        Vector3 point1 = transform.position + Vector3.up * crouchHeight;
        Vector3 point2 = transform.position + Vector3.up * Mathf.Max(crouchHeight + 0.05f, defaultHeight - radius);

        Collider[] hits = Physics.OverlapCapsule(point1, point2, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            // Ignore player's own colliders, controller, and any child attachments
            if (col == characterController || col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            return false; // Overhead obstruction present
        }

        return true;
    }

    private void HandleMovement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        float currentSpeed;

        if (staminaSystem != null && !staminaSystem.CanSprint)
        {
            currentSpeed = exhaustedSpeed;
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

        if (characterController.isGrounded)
        {
            if (Input.GetButton("Jump") && canMove)
            {
                verticalVelocity = jumpPower;
            }
            else
            {
                // Firm downward snap to eliminate slope and stair bouncing/jitter
                verticalVelocity = -2.0f;
            }
        }
        else
        {
            // Accumulate downward velocity properly while airborne
            verticalVelocity -= gravity * Time.deltaTime;
            if (verticalVelocity < -35f) verticalVelocity = -35f;
        }

        moveDirection = (forward * curSpeedX) + (right * curSpeedY);
        moveDirection.y = verticalVelocity;

        characterController.Move(moveDirection * Time.deltaTime);

        // Fall Safety Recovery: If player clips below map (Y < -2.0f), safety teleport back to floor
        if (transform.position.y < -2.0f)
        {
            characterController.enabled = false;
            transform.position = new Vector3(-84.5f, 2.5f, 13.5f); // Teleport to stair entrance floor
            characterController.enabled = true;
            Debug.LogWarning("[PlayerMovement] Player fell through ground! Reset to safety floor.");
        }

        // Footstep Audio Logic
        if (footstepAudioSource != null)
        {
            if (isMoving && characterController.isGrounded)
            {
                if (!footstepAudioSource.isPlaying) footstepAudioSource.Play();
                footstepAudioSource.pitch = isRunning ? 1.3f : (isCrouching ? 0.7f : 1f);

                if (enableDiegeticFootsteps)
                {
                    // Use the headbob timer to alternate stereo pan left/right
                    // This syncs the sound position with the visual head sway
                    footstepAudioSource.panStereo = Mathf.Sin(headBobTimer) * stereoPanAmount;
                }
            }
            else
            {
                if (footstepAudioSource.isPlaying) footstepAudioSource.Pause();
            }
        }
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
        if (playerCamera == null || PauseMenu.GameIsPaused || isJumpscareCameraOverride) return;

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
        if (isDialogueCameraOverride)
        {
            targetFOV = dialogueTargetFOV;
        }
        else if (staminaSystem != null)
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
                Color vColor = vignetteImage.color;
                vColor.r = 0f; vColor.g = 0f; vColor.b = 0f;
                vColor.a = Mathf.Lerp(vColor.a, targetAlpha, cameraSmoothSpeed * Time.deltaTime);
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
        if (!canMove || PauseMenu.GameIsPaused || isDialogueCameraOverride || isJumpscareCameraOverride) return;

        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        
        transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * lookSpeed, 0f);
    }

    /// <summary>
    /// Synchronizes rotationX mouse pitch when dialogue or cinematic camera override ends.
    /// </summary>
    public void SyncPitch(float pitch)
    {
        rotationX = Mathf.Clamp(pitch, -lookXLimit, lookXLimit);
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        }
    }
}