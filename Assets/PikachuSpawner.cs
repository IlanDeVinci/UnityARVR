using UnityEngine;

public class PikachuSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject pikachuPrefab;   // Glisse ton prefab Pikachu ici

    [Header("Spawn")]
    public int count = 20;             // Nombre de Pikachu
    public float spawnRadius = 8f;     // Zone de spawn

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3(randomPos.x, 0, randomPos.y);

            GameObject pikachu = Instantiate(pikachuPrefab, spawnPos, Quaternion.identity);

            // Donner un centre de déplacement propre à chacun
            PikachuWander wander = pikachu.GetComponent<PikachuWander>();
            if (wander != null)
            {
                wander.centerPoint = spawnPos;
                // Varier un peu la vitesse pour que ce soit plus vivant
                wander.wanderSpeed = Random.Range(1.5f, 3f);
            }
        }
    }
}