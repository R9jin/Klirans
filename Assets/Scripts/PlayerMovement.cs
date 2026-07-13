using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 6.5f;
    public float jumpPower = 3.5f;
    public float gravity = 20f;

    [Header("Mouse Look")]
    public float lookSpeed = 1.8f;
    public float lookXLimit = 85f;

    [Header("Crouching")]
    public float defaultHeight = 2f;
    public float crouchHeight = 1.2f;
    public float crouchSpeed = 2f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;

    private CharacterController characterController;
    private bool canMove = true;

    private float defaultWalkSpeed;
    private float defaultRunSpeed;

    private Vector3 standingCameraPosition;
    private Vector3 crouchingCameraPosition;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        defaultWalkSpeed = walkSpeed;
        defaultRunSpeed = runSpeed;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        characterController.height = defaultHeight;
        characterController.center = new Vector3(0, defaultHeight / 2f, 0);

        standingCameraPosition = playerCamera.transform.localPosition;
        crouchingCameraPosition = new Vector3(
            standingCameraPosition.x,
            standingCameraPosition.y - (defaultHeight - crouchHeight),
            standingCameraPosition.z
        );
    }

    void Update()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed =
            isRunning ? runSpeed : walkSpeed;

        float curSpeedX = canMove
            ? currentSpeed * Input.GetAxis("Vertical")
            : 0;

        float curSpeedY = canMove
            ? currentSpeed * Input.GetAxis("Horizontal")
            : 0;

        float movementDirectionY = moveDirection.y;

        moveDirection = (forward * curSpeedX) +
                        (right * curSpeedY);

        if (Input.GetButton("Jump") &&
            canMove &&
            characterController.isGrounded)
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

        // Crouch
        if (Input.GetKey(KeyCode.C) && canMove)
        {
            characterController.height = crouchHeight;
            characterController.center =
                new Vector3(0, crouchHeight / 2f, 0);

            walkSpeed = crouchSpeed;
            runSpeed = crouchSpeed;

            playerCamera.transform.localPosition =
                crouchingCameraPosition;
        }
        else
        {
            characterController.height = defaultHeight;
            characterController.center =
                new Vector3(0, defaultHeight / 2f, 0);

            walkSpeed = defaultWalkSpeed;
            runSpeed = defaultRunSpeed;

            playerCamera.transform.localPosition =
                standingCameraPosition;
        }

        characterController.Move(
            moveDirection * Time.deltaTime
        );

        if (canMove)
        {
            rotationX +=
                -Input.GetAxis("Mouse Y") * lookSpeed;

            rotationX = Mathf.Clamp(
                rotationX,
                -lookXLimit,
                lookXLimit
            );

            playerCamera.transform.localRotation =
                Quaternion.Euler(rotationX, 0, 0);

            transform.rotation *=
                Quaternion.Euler(
                    0,
                    Input.GetAxis("Mouse X") * lookSpeed,
                    0
                );
        }
    }
}