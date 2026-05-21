using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneResetManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Settings")]
    [SerializeField] private float fallThresholdY = -10f;

    private void Update()
    {
        if (playerTransform != null && playerTransform.position.y < fallThresholdY)
        {
            ResetScene();
        }
    }

    private void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ClearSpawnedObjects()
    {
        GameObject[] spawned = GameObject.FindGameObjectsWithTag("SpawnedObject");
        foreach (GameObject obj in spawned)
        {
            Destroy(obj);
        }
    }
}
