using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Spawns furniture at the point where the right controller's ray hits the ground.
/// Uses XR controller ray for positioning.
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawnable Prefabs")]
    [SerializeField] private SpawnableItem[] spawnableItems;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 3f;
    [SerializeField] private LayerMask groundLayer;

    [Header("XR References")]
    [SerializeField] private XRRayInteractor rightHandRay;

    [Header("Input - Bind to right controller button (e.g. Primary Button / A)")]
    [SerializeField] private InputActionReference spawnAction;
    [SerializeField] private InputActionReference nextItemAction;
    [SerializeField] private InputActionReference prevItemAction;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;

    private AudioSource audioSource;
    private int selectedIndex;

    public SpawnableItem[] SpawnableItems => spawnableItems;
    public int SelectedIndex => selectedIndex;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (spawnAction != null && spawnAction.action != null)
        {
            spawnAction.action.Enable();
            spawnAction.action.performed += OnSpawn;
        }
        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.Enable();
            nextItemAction.action.performed += OnNextItem;
        }
        if (prevItemAction != null && prevItemAction.action != null)
        {
            prevItemAction.action.Enable();
            prevItemAction.action.performed += OnPrevItem;
        }
    }

    private void OnDisable()
    {
        if (spawnAction != null && spawnAction.action != null)
        {
            spawnAction.action.performed -= OnSpawn;
            spawnAction.action.Disable();
        }
        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.performed -= OnNextItem;
            nextItemAction.action.Disable();
        }
        if (prevItemAction != null && prevItemAction.action != null)
        {
            prevItemAction.action.performed -= OnPrevItem;
            prevItemAction.action.Disable();
        }
    }

    public void SetSelectedIndex(int index)
    {
        if (spawnableItems.Length == 0) return;
        selectedIndex = ((index % spawnableItems.Length) + spawnableItems.Length) % spawnableItems.Length;
    }

    private void OnNextItem(InputAction.CallbackContext ctx) => SetSelectedIndex(selectedIndex + 1);
    private void OnPrevItem(InputAction.CallbackContext ctx) => SetSelectedIndex(selectedIndex - 1);

    private void OnSpawn(InputAction.CallbackContext ctx)
    {
        if (spawnableItems.Length == 0) return;

        Vector3 spawnPos = GetSpawnPosition();
        GameObject obj = Instantiate(
            spawnableItems[selectedIndex].prefab,
            spawnPos,
            Quaternion.identity
        );
        obj.tag = "SpawnedObject";

        // Make spawned objects grabbable in VR
        if (obj.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1f;
        }

        var grabInteractable = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable == null)
            obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (spawnSound != null)
            audioSource.PlayOneShot(spawnSound);
    }

    private Vector3 GetSpawnPosition()
    {
        // Use the XR ray interactor's hit point if available
        if (rightHandRay != null && rightHandRay.TryGetCurrent3DRaycastHit(out RaycastHit xrHit))
        {
            return xrHit.point;
        }

        // Fallback: use headset forward direction
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, spawnDistance * 2f, groundLayer))
                return hit.point;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            return cam.transform.position + forward * spawnDistance;
        }

        return transform.position + Vector3.forward * spawnDistance;
    }
}

[System.Serializable]
public class SpawnableItem
{
    public string name;
    public GameObject prefab;
    public Sprite icon;
}
