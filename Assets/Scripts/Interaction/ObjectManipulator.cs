using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles deleting spawned objects in VR.
/// Grab/move/rotate is handled natively by XRGrabInteractable on spawned objects.
/// This script only adds the delete functionality.
///
/// Attach to the XR Origin or a manager object.
/// </summary>
public class ObjectManipulator : MonoBehaviour
{
    [Header("Delete Input - Bind to a controller button (e.g. Y/B)")]
    [SerializeField] private InputActionReference deleteAction;

    [Header("Audio")]
    [SerializeField] private AudioClip deleteSound;

    [Header("References")]
    [SerializeField] private CameraController cameraController;

    private AudioSource audioSource;

    public GameObject HoveredObject => cameraController != null && cameraController.HasTarget
        ? (cameraController.CurrentTarget.CompareTag("SpawnedObject") ? cameraController.CurrentTarget : null)
        : null;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (deleteAction != null && deleteAction.action != null)
        {
            deleteAction.action.Enable();
            deleteAction.action.performed += OnDelete;
        }
    }

    private void OnDisable()
    {
        if (deleteAction != null && deleteAction.action != null)
        {
            deleteAction.action.performed -= OnDelete;
            deleteAction.action.Disable();
        }
    }

    private void OnDelete(InputAction.CallbackContext ctx)
    {
        // Delete the object the player is looking at (gaze) or the nearest grabbed object
        GameObject target = HoveredObject;
        if (target != null)
        {
            if (deleteSound != null)
                audioSource.PlayOneShot(deleteSound);
            Destroy(target);
        }
    }
}
