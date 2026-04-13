using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    private void OnEnable()
    {
        interactAction.action.Enable();
        interactAction.action.performed += OnInteract;
    }

    private void OnDisable()
    {
        interactAction.action.performed -= OnInteract;
        interactAction.action.Disable();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsMenuOpen) return;
        if (!cameraController.HasTarget) return;

        GameObject target = cameraController.CurrentTarget;

        // Try door
        DoorController door = target.GetComponentInParent<DoorController>();
        if (door != null)
        {
            door.ToggleDoor();
            return;
        }

        // Try light switch
        LightSwitchController lightSwitch = target.GetComponentInParent<LightSwitchController>();
        if (lightSwitch != null)
        {
            lightSwitch.ToggleSwitch();
            return;
        }
    }
}
