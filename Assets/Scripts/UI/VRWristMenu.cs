using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Makes a Canvas VR-interactive (WorldSpace + TrackedDeviceGraphicRaycaster)
/// and optionally follows a hand anchor (wrist-mounted menu).
///
/// Setup:
///   1. Add this to any Canvas GameObject.
///   2. Assign handAnchor to the Left Controller / Left Hand Visual transform
///      under the XR Origin's LeftHand controller.
///   3. Scale the Canvas transform to ~0.001 in XYZ for a physical wrist size.
///   4. Make sure your EventSystem has an XRUIInputModule component.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRWristMenu : MonoBehaviour
{
    [Header("Hand Attachment")]
    [SerializeField] private Transform handAnchor;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.04f);
    [SerializeField] private Vector3 localEulerOffset = new Vector3(-90f, 0f, 0f);

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        if (_canvas.worldCamera == null)
            _canvas.worldCamera = Camera.main;

        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Standard GraphicRaycaster does not work in VR
        var gr = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (gr != null)
            Destroy(gr);
    }

    private void LateUpdate()
    {
        if (handAnchor == null) return;
        transform.position = handAnchor.TransformPoint(localOffset);
        transform.rotation = handAnchor.rotation * Quaternion.Euler(localEulerOffset);
    }
}
