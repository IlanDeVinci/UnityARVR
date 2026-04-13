using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isSprinting;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        sprintAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        sprintAction.action.Disable();
    }

    private void Update()
    {
        // Gravity
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        // Movement
        isSprinting = sprintAction.action.IsPressed();
        float speed = isSprinting ? sprintSpeed : walkSpeed;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move((move * speed + velocity) * Time.deltaTime);
    }
}
