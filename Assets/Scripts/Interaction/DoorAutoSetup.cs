using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to the restaurant root (or any parent containing doors).
/// At Start, scans all child MeshRenderers and turns door-shaped meshes
/// into XR-interactable sliding doors.
///
/// A mesh is considered "door-shaped" when:
///   - its world-space height is within [minDoorHeight, maxDoorHeight]
///   - its thinnest axis is ≤ maxDoorThickness
///
/// You can also manually tag child GameObjects with the "Door" tag
/// to force them to become interactive doors regardless of shape.
/// </summary>
public class DoorAutoSetup : MonoBehaviour
{
    [Header("Door Detection Heuristic")]
    [Tooltip("Minimum height (Y) in world units for a mesh to be considered a door.")]
    [SerializeField] private float minDoorHeight = 1.5f;

    [Tooltip("Maximum height (Y) in world units for a mesh to be considered a door.")]
    [SerializeField] private float maxDoorHeight = 4f;

    [Tooltip("Maximum thickness (thinnest axis) for a mesh to be considered a door.")]
    [SerializeField] private float maxDoorThickness = 0.3f;

    [Header("Sliding Behaviour")]
    [SerializeField] private float slideSpeed = 3f;

    [Header("Manual Override")]
    [Tooltip("Drag specific child transforms here to force them as doors (bypasses heuristic).")]
    [SerializeField] private Transform[] manualDoors;

    private void Start()
    {
        // 1. Manual overrides
        if (manualDoors != null)
        {
            foreach (var t in manualDoors)
            {
                if (t != null) SetupDoor(t.gameObject);
            }
        }

        // 2. Tagged doors
        foreach (var tagged in GameObject.FindGameObjectsWithTag("Door"))
        {
            if (tagged.transform.IsChildOf(transform))
                SetupDoor(tagged);
        }

        // 3. Auto-detect by shape
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in renderers)
        {
            if (mr.GetComponent<SlidingDoorController>() != null) continue; // already set up

            if (IsDoorShaped(mr))
                SetupDoor(mr.gameObject);
        }
    }

    private bool IsDoorShaped(MeshRenderer mr)
    {
        Bounds bounds = mr.bounds; // world-space bounds
        Vector3 size = bounds.size;

        float height = size.y;
        float thickness = Mathf.Min(size.x, size.z);

        return height >= minDoorHeight
            && height <= maxDoorHeight
            && thickness <= maxDoorThickness;
    }

    private void SetupDoor(GameObject obj)
    {
        // Skip if already configured
        if (obj.GetComponent<SlidingDoorController>() != null) return;

        // Ensure a collider exists for XR interaction
        if (obj.GetComponent<Collider>() == null)
        {
            var meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var box = obj.AddComponent<BoxCollider>();
                box.center = meshFilter.sharedMesh.bounds.center;
                box.size = meshFilter.sharedMesh.bounds.size;
            }
            else
            {
                obj.AddComponent<BoxCollider>();
            }
        }

        // Add the sliding door controller (which adds XRSimpleInteractable in Awake)
        var door = obj.AddComponent<SlidingDoorController>();
        door.speed = slideSpeed;
    }
}
