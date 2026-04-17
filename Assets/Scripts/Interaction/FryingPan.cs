using UnityEngine;

/// <summary>
/// Poêle : quand un Pikachu découpé est posé/lâché dedans,
/// il se transforme en pikachu_poele_premium (le plat final).
///
/// Le modèle est chargé depuis Resources/pikachu_poele.
/// </summary>
public class FryingPan : MonoBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float detectionHeight = 0.4f;
    [SerializeField] private float cookedScale = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip sizzleSound;

    private AudioSource audioSource;
    private GameObject poelePrefab;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        poelePrefab = Resources.Load<GameObject>("pikachu_poele");
        if (poelePrefab == null)
            Debug.LogError("[FryingPan] pikachu_poele introuvable dans Resources/");

        // Zone de détection au-dessus de la poêle
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
            triggerZone.size = new Vector3(0.5f, detectionHeight, 0.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Détecter un Pikachu découpé par son nom
        GameObject obj = other.gameObject;
        string name = obj.name.ToLower();
        if (!name.Contains("pikachu_decoupe") && !name.Contains("pikachu decoupe"))
        {
            // Chercher le parent
            if (obj.transform.parent != null)
            {
                string parentName = obj.transform.parent.name.ToLower();
                if (parentName.Contains("pikachu_decoupe") || parentName.Contains("pikachu decoupe"))
                    obj = obj.transform.parent.gameObject;
                else
                    return;
            }
            else
            {
                return;
            }
        }

        // Ne pas transformer s'il est tenu en main
        var grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        CookPikachu(obj);
    }

    private void CookPikachu(GameObject pikachuDecoupe)
    {
        if (poelePrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
        Quaternion spawnRot = transform.rotation;

        if (sizzleSound != null)
            audioSource.PlayOneShot(sizzleSound);

        Destroy(pikachuDecoupe);

        GameObject cooked = Instantiate(poelePrefab, spawnPos, spawnRot);
        cooked.name = "Pikachu_Poele_Final";
        cooked.transform.localScale = Vector3.one * cookedScale;
    }
}
