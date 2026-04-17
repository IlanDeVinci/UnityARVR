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

            PikachuWander wander = pikachu.GetComponent<PikachuWander>();
            if (wander != null)
            {
                wander.centerPoint = spawnPos;
                wander.wanderSpeed = Random.Range(1.5f, 3f);

                // Brancher les sons
                if (fleeSound != null) wander.fleeSound = fleeSound;
                if (grabSound != null) wander.grabSound = grabSound;
                if (throwSound != null) wander.throwSound = throwSound;
            }
        }
    }
}