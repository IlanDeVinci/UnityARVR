using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Permet de tourner n'importe quel objet tenu en main avec les sticks des contrôleurs.
/// Attacher au XR Origin (ou à tout GameObject actif).
///
/// Utilisation :
///   - Grab un objet (main gauche ou droite)
///   - Pousser le thumbstick de la MAIN OPPOSÉE pour faire tourner l'objet
///     (ou de la même main si oneHandedMode = true)
///
/// Stick X (gauche/droite) → yaw (rotation Y)
/// Stick Y (haut/bas)      → pitch (rotation X)
/// </summary>
public class GrabRotator : MonoBehaviour
{
    [Header("Interactors")]
    [SerializeField] private NearFarInteractor leftInteractor;
    [SerializeField] private NearFarInteractor rightInteractor;

    [Header("Input")]
    [Tooltip("Action thumbstick main gauche (Vector2). Ex: XRI LeftHand Interaction/Turn ou XRI LeftHand/ThumbstickValue.")]
    [SerializeField] private InputActionReference leftStickAction;
    [Tooltip("Action thumbstick main droite (Vector2).")]
    [SerializeField] private InputActionReference rightStickAction;

    [Header("Réglages")]
    [Tooltip("Vitesse de rotation en degrés/seconde.")]
    [SerializeField] private float rotationSpeed = 90f;
    [Tooltip("Zone morte du stick (ignorer les petites valeurs).")]
    [SerializeField] private float deadZone = 0.2f;
    [Tooltip("Si true : le stick de la main qui tient l'objet tourne cet objet.\nSi false : c'est le stick de l'AUTRE main qui tourne (permet de garder locomotion).")]
    [SerializeField] private bool oneHandedMode = true;

    private void OnEnable()
    {
        if (leftStickAction != null && leftStickAction.action != null)
            leftStickAction.action.Enable();
        if (rightStickAction != null && rightStickAction.action != null)
            rightStickAction.action.Enable();

        AutoFindInteractors();
    }

    private void AutoFindInteractors()
    {
        if (leftInteractor != null && rightInteractor != null) return;

        var all = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        foreach (var i in all)
        {
            string n = i.gameObject.name.ToLower();
            if (leftInteractor == null && (n.Contains("left") || i.transform.root.name.ToLower().Contains("left")))
                leftInteractor = i;
            else if (rightInteractor == null && (n.Contains("right") || i.transform.root.name.ToLower().Contains("right")))
                rightInteractor = i;
        }
    }

    private void Update()
    {
        Vector2 leftStick = leftStickAction != null && leftStickAction.action != null
            ? leftStickAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector2 rightStick = rightStickAction != null && rightStickAction.action != null
            ? rightStickAction.action.ReadValue<Vector2>() : Vector2.zero;

        // Main gauche tient un objet → tourner avec stick
        if (leftInteractor != null && leftInteractor.hasSelection)
        {
            Vector2 input = oneHandedMode ? leftStick : rightStick;
            RotateGrabbed(leftInteractor, input);
        }

        // Main droite tient un objet → tourner avec stick
        if (rightInteractor != null && rightInteractor.hasSelection)
        {
            Vector2 input = oneHandedMode ? rightStick : leftStick;
            RotateGrabbed(rightInteractor, input);
        }
    }

    private void RotateGrabbed(NearFarInteractor interactor, Vector2 input)
    {
        if (input.sqrMagnitude < deadZone * deadZone) return;

        // Appliquer une deadzone radiale
        Vector2 adjusted = input;
        if (adjusted.magnitude > deadZone)
            adjusted = adjusted.normalized * ((adjusted.magnitude - deadZone) / (1f - deadZone));
        else
            return;

        float yaw = adjusted.x * rotationSpeed * Time.deltaTime;
        float pitch = -adjusted.y * rotationSpeed * Time.deltaTime;

        // Récupérer l'objet tenu (premier sélectionné)
        foreach (var interactable in interactor.interactablesSelected)
        {
            Transform t = interactable.transform;
            if (t == null) continue;

            // Tourner autour du monde (axes caméra pour un feeling naturel)
            Camera cam = Camera.main;
            if (cam != null)
            {
                t.Rotate(cam.transform.up, yaw, Space.World);
                t.Rotate(cam.transform.right, pitch, Space.World);
            }
            else
            {
                t.Rotate(Vector3.up, yaw, Space.World);
                t.Rotate(Vector3.right, pitch, Space.World);
            }
        }
    }
}
