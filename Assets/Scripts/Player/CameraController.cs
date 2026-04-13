using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Raycast")]
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private LayerMask interactLayer;

    [Header("Input")]
    [SerializeField] private InputActionReference lookAction;

    private float xRotation;
    private Transform playerBody;

    // Expose what we're looking at to other scripts
    public GameObject CurrentTarget { get; private set; }
    public RaycastHit CurrentHit { get; private set; }
    public bool HasTarget { get; private set; }

    private void Awake()
    {
        playerBody = transform.parent;
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
    }

    private void OnDisable()
    {
        lookAction.action.Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleRaycast();
    }

    private void HandleLook()
    {
        // Don't rotate camera when cursor is unlocked (menu open)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

        xRotation -= lookInput.y * mouseSensitivity;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
    }

    private void HandleRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            CurrentTarget = hit.collider.gameObject;
            CurrentHit = hit;
            HasTarget = true;
        }
        else
        {
            CurrentTarget = null;
            HasTarget = false;
        }
    }
}
