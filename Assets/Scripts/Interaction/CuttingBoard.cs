using UnityEngine;

/// <summary>
/// Planche à découper : quand un Pikachu (qui court) est lâché dessus,
/// il se transforme en Pikachu couché (pikachu_poele_premium).
///
/// Le modèle est chargé automatiquement depuis Resources/pikachu_poele_premium.
/// </summary>
public class CuttingBoard : MonoBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float detectionHeight = 0.5f;
    [SerializeField] private float cookedScale = 0.2f;
    [SerializeField] private AudioClip cookSound;

    private AudioSource audioSource;
    private GameObject cookedPrefab;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // Charger le modèle depuis Resources (pas de cast problématique)
        cookedPrefab = Resources.Load<GameObject>("pikachu_poele_premium");
        if (cookedPrefab == null)
            Debug.LogError("[CuttingBoard] pikachu_poele_premium introuvable dans Resources/");

        // Créer une zone de détection au-dessus de la planche
        var triggerZone = gameObject.AddComponent<BoxCollider>();
        triggerZone.isTrigger = true;

        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = transform.InverseTransformPoint(combined.center);
            Vector3 localSize = combined.size;

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
        var wander = other.GetComponent<PikachuWander>();
        if (wander == null)
            wander = other.GetComponentInParent<PikachuWander>();
        if (wander == null) return;

        // Ne transformer que si le Pikachu n'est pas tenu en main
        var grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
            grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        TransformPikachu(wander.gameObject);
    }

    private void TransformPikachu(GameObject pikachuRunning)
    {
        if (cookedPrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
        Quaternion spawnRot = transform.rotation;

        if (cookSound != null)
            audioSource.PlayOneShot(cookSound);

        Destroy(pikachuRunning);

        GameObject cooked = Instantiate(cookedPrefab, spawnPos, spawnRot);
        cooked.name = "Pikachu_Cuisine";
        cooked.transform.localScale = Vector3.one * cookedScale;
    }
}
