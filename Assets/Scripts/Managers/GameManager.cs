using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private SpawnMenuUI spawnMenuUI;

    public PlayerController Player => player;
    public CameraController CameraCtrl => cameraController;
    public ObjectSpawner Spawner => objectSpawner;

    public bool IsMenuOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        LockCursor();
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsMenuOpen = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsMenuOpen = true;
    }

    public void ToggleMenu()
    {
        if (IsMenuOpen)
            LockCursor();
        else
            UnlockCursor();
    }
}
