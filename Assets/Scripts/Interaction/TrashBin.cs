using UnityEngine;

/// <summary>
/// Poubelle : tout objet grabbable ou spawné qui entre dedans est détruit.
/// Ajoute automatiquement une zone trigger au-dessus de la poubelle.
/// </summary>
public class TrashBin : MonoBehaviour
{
    [Header("Zone de détection")]
    [SerializeField] private float detectionHeight = 1.2f;
    [SerializeField] private float sizeMultiplier = 1.0f;
    [SerializeField] private Vector3 offsetFromTop = new Vector3(0f, -0.2f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip trashSound;

    [Header("Sécurité")]
    [Tooltip("Ne supprime pas les objets essentiels (XR, camera, etc.)")]
    [SerializeField] private bool onlySpawnedObjects = false;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.7f;

        CreateTriggerZone();
    }

    private void CreateTriggerZone()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        Bounds b;
        if (renderers.Length > 0)
        {
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
        }
        else
        {
            b = new Bounds(transform.position, Vector3.one * 0.5f);
        }

        // Zone au-dessus de la poubelle (pas parentée pour éviter le scale)
        GameObject zone = new GameObject("TrashTrigger");
        zone.transform.position = new Vector3(b.center.x, b.max.y + detectionHeight * 0.5f + offsetFromTop.y, b.center.z);
        zone.transform.rotation = Quaternion.identity;
        zone.transform.localScale = Vector3.one;

        var col = zone.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(
            b.size.x * sizeMultiplier,
            detectionHeight,
            b.size.z * sizeMultiplier);

        var relay = zone.AddComponent<TrashTriggerRelay>();
        relay.bin = this;

        Debug.Log($"[TrashBin] Zone créée à {zone.transform.position}, taille {col.size}");
    }

    public void HandleObjectEntered(Collider other)
    {
        GameObject target = ResolveTarget(other);
        if (target == null) return;
        if (IsProtected(target)) return;

        if (onlySpawnedObjects && !target.CompareTag("SpawnedObject"))
            return;

        if (trashSound != null)
            audioSource.PlayOneShot(trashSound);

        Debug.Log($"[TrashBin] Objet détruit : {target.name}");
        Destroy(target);
    }

    /// <summary>
    /// Trouve le GameObject "principal" à détruire (remonte jusqu'au Rigidbody parent).
    /// </summary>
    private static GameObject ResolveTarget(Collider col)
    {
        if (col == null) return null;

        // Remonter au Rigidbody (c'est l'objet grabbable)
        var rb = col.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.gameObject;

        // Sinon prendre le collider lui-même
        return col.gameObject;
    }

    /// <summary>
    /// Liste des objets qu'on ne doit JAMAIS détruire.
    /// </summary>
    private static bool IsProtected(GameObject obj)
    {
        if (obj == null) return true;

        string name = obj.name.ToLower();

        // Ne jamais détruire le joueur, le XR Origin, la caméra, les controllers
        if (name.Contains("xr origin") || name.Contains("xr rig")
            || name.Contains("camera") || name.Contains("controller")
            || name.Contains("hand") || name.Contains("eventsystem")
            || name.Contains("gamemanager"))
            return true;

        if (obj.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) return true;
        if (obj.GetComponent<Camera>() != null) return true;

        return false;
    }
}

/// <summary>
/// Relais d'event trigger vers TrashBin (le trigger est sur un autre GameObject).
/// </summary>
public class TrashTriggerRelay : MonoBehaviour
{
    public TrashBin bin;

    private void OnTriggerEnter(Collider other)
    {
        if (bin != null) bin.HandleObjectEntered(other);
    }
}
