using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneResetManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference resetAction;

    [Header("Settings")]
    [SerializeField] private bool requireDoublePress = true;
    [SerializeField] private float doublePressWindow = 0.5f;

    private float lastPressTime;

    private void OnEnable()
    {
        resetAction.action.Enable();
        resetAction.action.performed += OnReset;
    }

    private void OnDisable()
    {
        resetAction.action.performed -= OnReset;
        resetAction.action.Disable();
    }

    private void OnReset(InputAction.CallbackContext ctx)
    {
        if (requireDoublePress)
        {
            if (Time.unscaledTime - lastPressTime < doublePressWindow)
            {
                ResetScene();
            }
            lastPressTime = Time.unscaledTime;
        }
        else
        {
            ResetScene();
        }
    }

    private void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Alternative: only destroy spawned objects without reloading the scene.
    /// </summary>
    public void ClearSpawnedObjects()
    {
        GameObject[] spawned = GameObject.FindGameObjectsWithTag("SpawnedObject");
        foreach (GameObject obj in spawned)
        {
            Destroy(obj);
        }
    }
}
