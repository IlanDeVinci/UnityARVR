using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Bridges XR interaction events to game objects (doors, switches).
/// Add XRSimpleInteractable to any interactable object, then add this
/// as a listener to XRSimpleInteractable.selectEntered via Inspector
/// OR attach this to the XR Origin and it auto-wires at runtime.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    /// <summary>
    /// Called when an XR Interactor selects an object with XRSimpleInteractable.
    /// Wire this to XRSimpleInteractable.selectEntered in the Inspector,
    /// or call it manually from code.
    /// </summary>
    public void OnObjectInteracted(SelectEnterEventArgs args)
    {
        GameObject target = args.interactableObject.transform.gameObject;
        TryInteract(target);
    }

    /// <summary>
    /// Direct interaction method - can be called from any context.
    /// </summary>
    public void TryInteract(GameObject target)
    {
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
