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

    [Header("Movement Inertia & Smoothing")]
    [Tooltip("Rate of acceleration when starting to move.")]
    public float accelerationRate = 24.0f;
    [Tooltip("Rate of deceleration when releasing movement keys (smooth deceleration, not abrupt freeze).")]
    public float decelerationRate = 15.0f;
    private Vector3 _currentPlanarVelocity = Vector3.zero;

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
    public CharacterController Controller => characterController;

    private Vector3 standingCameraPosition;
    private Vector3 crouchingCameraPosition;

    private float defaultCenterY;
    private float crouchCenterY;
    private float _lastGroundedTime = 0f;

    private void Awake()
    {
        canMove = true;
        isDialogueCameraOverride = false;
        isJumpscareCameraOverride = false;
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
        }
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        canMove = true;
        isDialogueCameraOverride = false;
        isJumpscareCameraOverride = false;

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

        if (footstepAudioSource != null)
        {
            footstepAudioSource.volume = footstepVolume;
            // Remove any reverb/echo filters to guarantee crisp, clean, single-body footsteps with zero doubling
            var reverb = footstepAudioSource.gameObject.GetComponent<AudioReverbFilter>();
            if (reverb != null) Destroy(reverb);
            var echo = footstepAudioSource.gameObject.GetComponent<AudioEchoFilter>();
            if (echo != null) Destroy(echo);
        }

        EnsureLobbyCollidersAndObstacles();
    }

    private void EnsureLobbyCollidersAndObstacles()
    {
        var roundTable = GameObject.Find("Lobby_RoundTable");
        if (roundTable != null)
        {
            // 1. Ensure solid physical collision
            var col = roundTable.GetComponent<Collider>();
            if (col == null)
            {
                var box = roundTable.AddComponent<BoxCollider>();
                box.isTrigger = false;
                box.center = new Vector3(0f, 0.45f, 0f);
                box.size = new Vector3(2.6f, 0.90f, 2.6f);
            }
            else
            {
                col.isTrigger = false;
            }

            // 2. Remove any accidental NavMeshObstacle on roundTable (table has scale 103x, which would carve a 280m void!)
            var obs = roundTable.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obs != null)
            {
                Destroy(obs);
            }
        }

        foreach (var name in new string[] { "CoffeeTable_GlassTop", "CoffeeTable_Frame", "CoffeeTable_Leg1", "CoffeeTable_Leg2", "CoffeeTable_Leg3", "CoffeeTable_Leg4" })
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) c.isTrigger = false;
            }
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled)
        {
            _currentPlanarVelocity = Vector3.zero;
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Pause();
            }
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

    private bool _crouchToggled = false;

    private void UpdateMovementStates()
    {
        GameSettingsManager.GetMoveAxes(out float moveH, out float moveV);
        bool hasInput = (Mathf.Abs(moveH) > 0.01f || Mathf.Abs(moveV) > 0.01f);
        isMoving = canMove && (hasInput || _currentPlanarVelocity.sqrMagnitude > 0.05f);
        isRunning = GameSettingsManager.GetKey(GameAction.Sprint) && hasInput && canMove && staminaSystem != null && staminaSystem.CanSprint;

        bool crouchToggleMode = GameSettingsManager.Instance != null && GameSettingsManager.Instance.CrouchToggleMode;
        bool wantsCrouch;
        if (crouchToggleMode)
        {
            if (canMove && GameSettingsManager.GetKeyDown(GameAction.Crouch))
            {
                _crouchToggled = !_crouchToggled;
            }
            wantsCrouch = _crouchToggled && canMove;
        }
        else
        {
            _crouchToggled = false;
            wantsCrouch = GameSettingsManager.GetKey(GameAction.Crouch) && canMove;
        }

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

        if (DevDemoPanel.PlayerSpeedMultiplier > 0.05f)
        {
            currentSpeed *= DevDemoPanel.PlayerSpeedMultiplier;
        }

        GameSettingsManager.GetMoveAxes(out float inputH, out float inputV);
        Vector3 targetPlanarVelocity = Vector3.zero;
        if (canMove)
        {
            targetPlanarVelocity = (forward * (inputV * currentSpeed)) + (right * (inputH * currentSpeed));
        }

        float smoothRate = targetPlanarVelocity.sqrMagnitude > 0.01f ? accelerationRate : decelerationRate;
        _currentPlanarVelocity = Vector3.MoveTowards(_currentPlanarVelocity, targetPlanarVelocity, smoothRate * Time.deltaTime);

        if (characterController.isGrounded)
        {
            // Jumping disabled — player stays firmly grounded in survival horror
            verticalVelocity = -2.0f;
        }
        else
        {
            // Accumulate downward velocity properly while airborne
            verticalVelocity -= gravity * Time.deltaTime;
            if (verticalVelocity < -35f) verticalVelocity = -35f;
        }

        moveDirection = _currentPlanarVelocity;
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

        // Footstep Audio Logic (smooth grounded buffer so stair steps never stutter or double-trigger)
        if (footstepAudioSource != null)
        {
            if (characterController.isGrounded) _lastGroundedTime = Time.time;
            bool effectivelyGrounded = (Time.time - _lastGroundedTime) < 0.14f;

            if (isMoving && effectivelyGrounded)
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

        if (DevDemoPanel.IsInfiniteStaminaActive)
        {
            staminaSystem.currentStamina = staminaSystem.maxStamina;
            return;
        }

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
        if (playerCamera == null || PauseMenu.GameIsPaused || DevDemoPanel.IsOpen || isJumpscareCameraOverride || LockerHideManager.IsPlayerHidden || !canMove) return;

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
        // Re-lock cursor on left mouse click during normal gameplay if unlocked in editor
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && canMove && !PauseMenu.GameIsPaused && !PauseMenu.IsAnyModalOrPuzzleOpen && !DevDemoPanel.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (!canMove || PauseMenu.GameIsPaused || isDialogueCameraOverride || isJumpscareCameraOverride || DevDemoPanel.IsOpen) return;

        float sens = GameSettingsManager.Instance != null ? GameSettingsManager.Instance.MouseSensitivity : lookSpeed;
        if (sens <= 0.05f) sens = 1.8f; // Safety clamp against corrupted zero sensitivity
        float invertSign = (GameSettingsManager.Instance != null && GameSettingsManager.Instance.InvertMouseY) ? 1f : -1f;

        rotationX += invertSign * Input.GetAxis("Mouse Y") * sens;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        }
        
        transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * sens, 0f);
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

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit != null && hit.collider != null && NPCJumpscareManager.Instance != null)
        {
            NPCJumpscareManager.Instance.OnPlayerControllerHit(hit.collider.gameObject);
        }
    }
}