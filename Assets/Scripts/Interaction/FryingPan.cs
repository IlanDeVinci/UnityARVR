using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

/// <summary>
/// Poêle grabbable : quand un Pikachu découpé y est posé, elle se transforme
/// en pikachu_poele_premium (poêle + Pikachu cuisiné). Les deux sont grabbables.
/// </summary>
public class FryingPan : MonoBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float detectionHeight = 0.4f;
    [SerializeField] private float cookedScale = 0.03f;

    [Header("Audio")]
    [SerializeField] private AudioClip sizzleSound;

    private AudioSource audioSource;
    private GameObject poelePrefab;
    private Rigidbody rb;
    private BoxCollider physicsCollider;
    private BoxCollider triggerZone;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        poelePrefab = Resources.Load<GameObject>("pikachu_poele");
        if (poelePrefab == null)
            Debug.LogError("[FryingPan] pikachu_poele introuvable dans Resources/");

        SetupGrabbable();
        SetupTriggerZone();
    }

    /// <summary>
    /// Rend la poêle grabbable avec Rigidbody + Collider + XRGrabInteractable.
    /// </summary>
    private void SetupGrabbable()
    {
        // Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.isKinematic = true; // Reste en place jusqu'au premier grab
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Collider physique englobant (non-trigger) — pour XR select + physique
        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            physicsCollider = gameObject.AddComponent<BoxCollider>();
            physicsCollider.center = transform.InverseTransformPoint(combined.center);
            physicsCollider.size = new Vector3(
                combined.size.x / transform.lossyScale.x,
                combined.size.y / transform.lossyScale.y,
                combined.size.z / transform.lossyScale.z);
        }
        else
        {
            physicsCollider = gameObject.AddComponent<BoxCollider>();
        }

        // XRGrabInteractable avec far-grab blaster
        var grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = gameObject.AddComponent<XRGrabInteractable>();

        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.farAttachMode = InteractableFarAttachMode.Near;
        grab.smoothPosition = true;
        grab.smoothPositionAmount = 12f;
        grab.tightenPosition = 0.5f;
        grab.smoothRotation = true;
        grab.smoothRotationAmount = 12f;
        grab.tightenRotation = 0.5f;
        grab.attachEaseInTime = 0.1f;

        // Au premier grab, désactiver le kinematic pour la physique
        grab.selectEntered.AddListener(_ => { rb.isKinematic = false; });
    }

    /// <summary>
    /// Trigger séparé au-dessus de la poêle pour détecter le Pikachu découpé.
    /// </summary>
    private void SetupTriggerZone()
    {
        triggerZone = gameObject.AddComponent<BoxCollider>();
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
            triggerZone.size = new Vector3(
                localSize.x / transform.lossyScale.x,
                detectionHeight / transform.lossyScale.y,
                localSize.z / transform.lossyScale.z);
        }
        else
        {
            triggerZone.center = Vector3.up * detectionHeight * 0.5f;
            triggerZone.size = new Vector3(0.5f, detectionHeight, 0.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject obj = other.gameObject;
        string name = obj.name.ToLower();
        if (!name.Contains("pikachu_decoupe") && !name.Contains("pikachu decoupe"))
        {
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

        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        CookPikachu(obj);
    }

    private void CookPikachu(GameObject pikachuDecoupe)
    {
        if (poelePrefab == null) return;

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        if (sizzleSound != null)
            audioSource.PlayOneShot(sizzleSound);

        Destroy(pikachuDecoupe);

        GameObject cooked = Instantiate(poelePrefab, spawnPos, spawnRot);
        cooked.name = "Pikachu_Poele_Final";
        cooked.transform.localScale = Vector3.one * cookedScale;

        // Rendre le résultat final grabbable aussi
        MakeGrabbable(cooked);

        // Détruire la poêle vide originale (plutôt que juste masquer)
        Destroy(gameObject);
    }

    /// <summary>
    /// Ajoute Rigidbody + Collider + XRGrabInteractable à un objet pour qu'il soit grabbable.
    /// </summary>
    private static void MakeGrabbable(GameObject obj)
    {
        var r = obj.AddComponent<Rigidbody>();
        r.mass = 1.5f;
        r.isKinematic = true;
        r.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var renderers = obj.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            var box = obj.AddComponent<BoxCollider>();
            box.center = obj.transform.InverseTransformPoint(combined.center);
            box.size = new Vector3(
                combined.size.x / obj.transform.lossyScale.x,
                combined.size.y / obj.transform.lossyScale.y,
                combined.size.z / obj.transform.lossyScale.z);
        }
        else
        {
            obj.AddComponent<BoxCollider>();
        }

        var g = obj.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        g.throwOnDetach = true;
        g.useDynamicAttach = true;
        g.farAttachMode = InteractableFarAttachMode.Near;
        g.smoothPosition = true;
        g.smoothPositionAmount = 12f;
        g.tightenPosition = 0.5f;
        g.smoothRotation = true;
        g.smoothRotationAmount = 12f;
        g.tightenRotation = 0.5f;
        g.attachEaseInTime = 0.1f;

        g.selectEntered.AddListener(_ => { r.isKinematic = false; });
    }
}
