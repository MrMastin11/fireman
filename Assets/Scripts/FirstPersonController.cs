using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private RectTransform handsOverlay;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.08f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("View Motion")]
    [SerializeField] private float cameraBobHeight = 0.075f;
    [SerializeField] private float cameraBobSide = 0.028f;
    [SerializeField] private float handBobAmount = 28f;
    [SerializeField] private float handRollAmount = 2.5f;
    [SerializeField] private float motionSmoothness = 12f;

    private CharacterController characterController;
    private float verticalVelocity;
    private float cameraPitch;
    private float movementBlend;
    private float walkCycle;
    private Vector2 handsLookOffset;
    private float handsLookRoll;
    private Vector3 cameraBasePosition;
    private Vector3 cameraBobVelocity;
    private Vector2 handsBasePosition;
    private Quaternion handsBaseRotation;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            Debug.LogError("FirstPersonController needs a Camera assigned or placed as a child.", this);

        if (playerCamera != null)
            cameraBasePosition = playerCamera.transform.localPosition;

        if (handsOverlay != null)
        {
            handsBasePosition = handsOverlay.anchoredPosition;
            handsBaseRotation = handsOverlay.localRotation;
        }

        LockCursor(true);
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(Cursor.lockState != CursorLockMode.Locked);

        if (Cursor.lockState == CursorLockMode.Locked)
            Look();

        Move();
        AnimateView();
    }

    private void Move()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);
        float speed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        characterController.Move(direction * speed * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (characterController.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        bool movingOnGround = input.sqrMagnitude > 0.01f && characterController.isGrounded;
        movementBlend = Mathf.MoveTowards(movementBlend, movingOnGround ? 1f : 0f, Time.deltaTime * 7f);

        if (movingOnGround)
        {
            float cycleSpeed = Keyboard.current.leftShiftKey.isPressed ? 11f : 8f;
            walkCycle += Time.deltaTime * cycleSpeed;
        }
    }

    private void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * lookSensitivity;
        transform.Rotate(Vector3.up * mouseDelta.x);

        cameraPitch = Mathf.Clamp(cameraPitch - mouseDelta.y, -maxLookAngle, maxLookAngle);
        handsLookOffset += new Vector2(-mouseDelta.x * 1.2f, -mouseDelta.y * 0.65f);
        handsLookOffset = Vector2.ClampMagnitude(handsLookOffset, 24f);
        handsLookRoll = Mathf.Clamp(handsLookRoll - mouseDelta.x * 0.12f, -4f, 4f);
    }

    private void AnimateView()
    {
        float downParabola = -Mathf.Pow(Mathf.Sin(walkCycle), 2f);
        float sideWave = Mathf.Sin(walkCycle * 0.5f);
        float smooth = 1f - Mathf.Exp(-motionSmoothness * Time.deltaTime);

        if (playerCamera != null)
        {
            Vector3 cameraTarget = cameraBasePosition + new Vector3(
                sideWave * cameraBobSide * movementBlend,
                downParabola * cameraBobHeight * movementBlend,
                0f);

            playerCamera.transform.localPosition = Vector3.SmoothDamp(
                playerCamera.transform.localPosition,
                cameraTarget,
                ref cameraBobVelocity,
                1f / motionSmoothness);

            playerCamera.transform.localRotation = Quaternion.Euler(
                cameraPitch + downParabola * 1.2f * movementBlend,
                sideWave * 0.5f * movementBlend,
                -Mathf.Sin(walkCycle) * 0.75f * movementBlend);
        }

        handsLookOffset = Vector2.Lerp(handsLookOffset, Vector2.zero, smooth);
        handsLookRoll = Mathf.Lerp(handsLookRoll, 0f, smooth);

        if (handsOverlay != null)
        {
            Vector2 walkBob = new Vector2(
                Mathf.Sin(walkCycle) * handBobAmount * 0.65f,
                downParabola * handBobAmount);

            handsOverlay.anchoredPosition = Vector2.Lerp(
                handsOverlay.anchoredPosition,
                handsBasePosition + handsLookOffset + walkBob * movementBlend,
                smooth);

            Quaternion targetRotation = handsBaseRotation * Quaternion.Euler(
                0f,
                0f,
                handsLookRoll - Mathf.Sin(walkCycle) * handRollAmount * movementBlend);
            handsOverlay.localRotation = Quaternion.Slerp(
                handsOverlay.localRotation,
                targetRotation,
                smooth);
        }
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
