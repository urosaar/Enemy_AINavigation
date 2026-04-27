using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float sprintSpeed = 8f;

    [Header("Jump & Gravity")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;
    public Transform cameraPivot;

    [Header("Ground Check")]
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    private CharacterController controller;
    private PlayerInputActions inputActions;

    private Vector2 moveInput;
    private Vector3 velocity;

    private bool isGrounded;
    private bool isSprinting;
    private bool jumpHeld;

    private float xRotation = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();

        // Movement
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Jump
        inputActions.Player.Jump.performed += ctx =>
        {
            jumpHeld = true;
            Jump();
        };
        inputActions.Player.Jump.canceled += ctx => jumpHeld = false;

        // Sprint
        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Update()
    {
        if (cameraPivot == null)
            cameraPivot = transform;

        // ✅ Ground check at FEET (fix)
        Vector3 checkPos = transform.position + Vector3.down * (controller.height / 2f);
        isGrounded = Physics.CheckSphere(checkPos, groundDistance, groundMask);

        // Stick to ground
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Movement
        float currentSpeed = isSprinting ? sprintSpeed : speed;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Gravity
        velocity.y += gravity * Time.deltaTime;

        // Better jump feel
        if (velocity.y < 0)
        {
            velocity.y += gravity * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (velocity.y > 0 && !jumpHeld)
        {
            velocity.y += gravity * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    void LateUpdate()
    {
        Vector2 lookInput = inputActions.Player.Look.ReadValue<Vector2>();

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // 🔍 Debug sphere (optional but VERY useful)
    void OnDrawGizmos()
    {
        if (controller != null)
        {
            Gizmos.color = Color.red;
            Vector3 checkPos = transform.position + Vector3.down * (controller.height / 2f);
            Gizmos.DrawWireSphere(checkPos, groundDistance);
        }
    }
}