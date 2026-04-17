using UnityEngine;

public class PikachuSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject pikachuPrefab;

    [Header("Spawn")]
    public int count = 20;
    public float spawnRadius = 8f;

    [Header("Sons Pikachu")]
    public AudioClip fleeSound;
    public AudioClip grabSound;
    public AudioClip throwSound;

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3(randomPos.x, 0, randomPos.y);

            GameObject pikachu = Instantiate(pikachuPrefab, spawnPos, Quaternion.identity);

            // Supprimer les lumières/caméras embed du modèle
            StripExtras(pikachu);

            PikachuWander wander = pikachu.GetComponent<PikachuWander>();
            if (wander != null)
            {
                wander.centerPoint = spawnPos;
                wander.wanderSpeed = Random.Range(1.5f, 3f);

                if (fleeSound != null) wander.fleeSound = fleeSound;
                if (grabSound != null) wander.grabSound = grabSound;
                if (throwSound != null) wander.throwSound = throwSound;
            }
        }
    }

    private static void StripExtras(GameObject obj)
    {
        foreach (var cam in obj.GetComponentsInChildren<Camera>(true))
        {
            if (cam == null) continue;
            if (cam.gameObject == obj) cam.enabled = false;
            else Destroy(cam.gameObject);
        }
        foreach (var l in obj.GetComponentsInChildren<Light>(true))
        {
            if (l == null) continue;
            if (l.gameObject == obj) l.enabled = false;
            else Destroy(l.gameObject);
        }
        foreach (var al in obj.GetComponentsInChildren<AudioListener>(true))
        {
            if (al == null) continue;
            al.enabled = false;
            if (al.gameObject != obj) Destroy(al.gameObject);
        }
    }
}