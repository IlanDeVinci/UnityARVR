using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectManipulator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float dragHeight = 0.5f;
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private float dragSmoothSpeed = 15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask interactLayer;

    [Header("Input")]
    [SerializeField] private InputActionReference clickAction;   // Left mouse button
    [SerializeField] private InputActionReference rotateAction;  // R key
    [SerializeField] private InputActionReference deleteAction;  // X or Delete key
    [SerializeField] private InputActionReference lookAction;    // Mouse delta (for rotation)

    [Header("Audio")]
    [SerializeField] private AudioClip deleteSound;

    private AudioSource audioSource;
    private Camera mainCam;
    private GameObject dragTarget;
    private bool isDragging;
    private bool isRotating;

    // Expose hovered object for HUD
    public GameObject HoveredObject { get; private set; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        clickAction.action.Enable();
        rotateAction.action.Enable();
        deleteAction.action.Enable();
        lookAction.action.Enable();

        deleteAction.action.performed += OnDelete;
    }

    private void OnDisable()
    {
        deleteAction.action.performed -= OnDelete;

        clickAction.action.Disable();
        rotateAction.action.Disable();
        deleteAction.action.Disable();
        lookAction.action.Disable();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsMenuOpen) return;

        UpdateHover();
        HandleDrag();
        HandleRotation();
    }

    private void UpdateHover()
    {
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f, interactLayer))
        {
            GameObject obj = hit.collider.gameObject;
            if (obj.CompareTag("SpawnedObject"))
            {
                HoveredObject = obj;
                return;
            }
        }
        if (!isDragging)
            HoveredObject = null;
    }

    private void HandleDrag()
    {
        bool clicking = clickAction.action.IsPressed();

        if (clicking && !isDragging && HoveredObject != null)
        {
            // Start drag
            dragTarget = HoveredObject;
            isDragging = true;
            Rigidbody rb = dragTarget.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
        else if (!clicking && isDragging)
        {
            // Stop drag
            Rigidbody rb = dragTarget.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
            isDragging = false;
            dragTarget = null;
        }

        if (isDragging && dragTarget != null)
        {
            // Move object along ground plane following camera look
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            Plane groundPlane = new Plane(Vector3.up, Vector3.up * dragHeight);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPos = ray.GetPoint(distance);
                dragTarget.transform.position = Vector3.Lerp(
                    dragTarget.transform.position,
                    targetPos,
                    dragSmoothSpeed * Time.deltaTime
                );
            }
        }
    }

    private void HandleRotation()
    {
        isRotating = rotateAction.action.IsPressed();
        if (isRotating && HoveredObject != null)
        {
            Vector2 look = lookAction.action.ReadValue<Vector2>();
            HoveredObject.transform.Rotate(Vector3.up, look.x * rotateSpeed * Time.deltaTime);
        }
    }

    private void OnDelete(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsMenuOpen) return;

        if (HoveredObject != null && HoveredObject.CompareTag("SpawnedObject"))
        {
            if (deleteSound != null)
                audioSource.PlayOneShot(deleteSound);

            Destroy(HoveredObject);
            HoveredObject = null;
        }
    }
}
