using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawnable Prefabs")]
    [SerializeField] private SpawnableItem[] spawnableItems;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 3f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Input")]
    [SerializeField] private InputActionReference spawnAction;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;

    private AudioSource audioSource;
    private int selectedIndex;
    private Camera mainCam;

    public SpawnableItem[] SpawnableItems => spawnableItems;
    public int SelectedIndex => selectedIndex;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        spawnAction.action.Enable();
        spawnAction.action.performed += OnSpawn;
    }

    private void OnDisable()
    {
        spawnAction.action.performed -= OnSpawn;
        spawnAction.action.Disable();
    }

    public void SetSelectedIndex(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, spawnableItems.Length - 1);
    }

    private void OnSpawn(InputAction.CallbackContext ctx)
    {
        // Don't spawn if menu is open
        if (GameManager.Instance != null && GameManager.Instance.IsMenuOpen) return;
        if (spawnableItems.Length == 0) return;

        Vector3 spawnPos = GetSpawnPosition();
        GameObject obj = Instantiate(
            spawnableItems[selectedIndex].prefab,
            spawnPos,
            Quaternion.identity
        );
        obj.tag = "SpawnedObject";

        if (spawnSound != null)
            audioSource.PlayOneShot(spawnSound);
    }

    private Vector3 GetSpawnPosition()
    {
        // Raycast from camera to find ground position in front of player
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, spawnDistance * 2f, groundLayer))
        {
            return hit.point;
        }

        // Fallback: spawn in front of player at ground level
        Vector3 forward = mainCam.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 pos = transform.position + forward * spawnDistance;
        pos.y = 0f;
        return pos;
    }
}

[System.Serializable]
public class SpawnableItem
{
    public string name;
    public GameObject prefab;
    public Sprite icon;
}
