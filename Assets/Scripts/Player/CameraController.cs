using UnityEngine;

/// <summary>
/// Exposes headset gaze raycast data to other scripts (HUD, etc.).
/// Attach to the Main Camera inside XR Origin > Camera Offset > Main Camera.
/// Head tracking is handled by XR's TrackedPoseDriver, not this script.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Gaze Raycast")]
    [SerializeField] private float gazeDistance = 10f;
    [SerializeField] private LayerMask interactLayer = ~0;

    public GameObject CurrentTarget { get; private set; }
    public RaycastHit CurrentHit { get; private set; }
    public bool HasTarget { get; private set; }

    private void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, gazeDistance, interactLayer))
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
