using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Snap/placement zone for grabbed objects. Wraps XRSocketInteractor
/// and adds colour feedback on the zone's renderer.
///
/// Setup:
///   1. Create an empty GameObject at the placement position.
///   2. Add a trigger Collider (sphere or box) sized to the slot opening.
///   3. Add this component — XRSocketInteractor is added automatically.
///   4. Add a visual ring/disc Mesh Renderer and assign it to zoneRenderer.
///   5. Set the socket's Interaction Layer Mask to match grabbable objects.
/// </summary>
[RequireComponent(typeof(XRSocketInteractor))]
public class XRSnapZone : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Renderer zoneRenderer;
    [SerializeField] private Color emptyColor    = new Color(1f, 1f, 1f,  0.3f);
    [SerializeField] private Color hoverColor    = new Color(0.2f, 1f, 0.4f, 0.6f);
    [SerializeField] private Color occupiedColor = new Color(1f, 0.8f, 0.2f, 0.5f);

    private XRSocketInteractor _socket;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _socket = GetComponent<XRSocketInteractor>();
        _mpb    = new MaterialPropertyBlock();

        _socket.hoverEntered.AddListener(OnHoverEnter);
        _socket.hoverExited.AddListener(OnHoverExit);
        _socket.selectEntered.AddListener(OnSelectEnter);
        _socket.selectExited.AddListener(OnSelectExit);
    }

    private void Start() => SetColor(emptyColor);

    private void OnDestroy()
    {
        if (_socket == null) return;
        _socket.hoverEntered.RemoveListener(OnHoverEnter);
        _socket.hoverExited.RemoveListener(OnHoverExit);
        _socket.selectEntered.RemoveListener(OnSelectEnter);
        _socket.selectExited.RemoveListener(OnSelectExit);
    }

    private void SetColor(Color c)
    {
        if (zoneRenderer == null) return;
        _mpb.SetColor("_BaseColor", c);
        zoneRenderer.SetPropertyBlock(_mpb);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)  => SetColor(hoverColor);
    private void OnHoverExit(HoverExitEventArgs args)    => SetColor(_socket.hasSelection ? occupiedColor : emptyColor);
    private void OnSelectEnter(SelectEnterEventArgs args) => SetColor(occupiedColor);
    private void OnSelectExit(SelectExitEventArgs args)   => SetColor(emptyColor);
}
