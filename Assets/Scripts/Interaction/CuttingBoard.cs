using UnityEngine;

/// <summary>
/// Planche à découper : quand un Pikachu (qui court) est lâché dessus,
/// il se transforme en Pikachu couché (pikachu_poele_premium).
///
/// Setup :
///   1. Ajouter ce script sur la planche à découper.
///   2. Assigner le prefab/modèle pikachu_poele_premium dans l'inspector.
///   3. Un trigger collider est auto-ajouté au-dessus de la planche.
/// </summary>
public class CuttingBoard : MonoBehaviour
{
    [Header("Modèle de remplacement")]
    [Tooltip("Le modèle de Pikachu couché (pikachu_poele_premium).")]
    [SerializeField] private GameObject pikachuCookedPrefab;

    [Header("Réglages")]
    [SerializeField] private float detectionHeight = 0.5f;
    [SerializeField] private AudioClip cookSound;

    private AudioSource audioSource;
    private BoxCollider triggerZone;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // Créer une zone de détection au-dessus de la planche
        triggerZone = gameObject.AddComponent<BoxCollider>();
        triggerZone.isTrigger = true;

        // Calculer la taille à partir des meshes enfants
        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = transform.InverseTransformPoint(combined.center);
            Vector3 localSize = combined.size;

            // Zone au-dessus de la planche
            triggerZone.center = localCenter + Vector3.up * detectionHeight * 0.5f;
            triggerZone.size = new Vector3(localSize.x, detectionHeight, localSize.z);
        }
        else
        {
            triggerZone.center = Vector3.up * detectionHeight * 0.5f;
            triggerZone.size = new Vector3(1f, detectionHeight, 1f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Vérifier que c'est un Pikachu qui court (a le composant PikachuWander)
        var wander = other.GetComponent<PikachuWander>();
        if (wander == null)
            wander = other.GetComponentInParent<PikachuWander>();
        if (wander == null) return;

        // Vérifier qu'il vient d'être lancé/lâché (pas en train de marcher)
        // On check s'il n'est plus grabbed et qu'il n'est pas grounded (en vol / vient de tomber)
        // Ou simplement s'il a été lancé récemment
        var grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
            grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Ne transformer que si le Pikachu n'est pas actuellement tenu en main
        if (grab != null && grab.isSelected) return;

        TransformPikachu(wander.gameObject);
    }

    private void TransformPikachu(GameObject pikachuRunning)
    {
        if (pikachuCookedPrefab == null)
        {
            Debug.LogWarning("[CuttingBoard] Pas de prefab pikachu_poele_premium assigné !");
            return;
        }

        // Position et rotation sur la planche
        Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
        Quaternion spawnRot = transform.rotation;

        // Son
        if (cookSound != null)
            audioSource.PlayOneShot(cookSound);

        // Détruire le Pikachu qui court
        Destroy(pikachuRunning);

        // Instancier le Pikachu couché
        GameObject cooked = Instantiate(pikachuCookedPrefab, spawnPos, spawnRot);
        cooked.name = "Pikachu_Cuisine";

        // Ajuster l'échelle pour correspondre à la planche
        cooked.transform.localScale = Vector3.one * 0.2f;
    }
}
