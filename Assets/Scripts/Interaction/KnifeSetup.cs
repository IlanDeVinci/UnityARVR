using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

/// <summary>
/// Configure un couteau comme objet grabbable avec far-grab blaster.
/// Quand on pointe le couteau de loin et qu'on grip, il vole vers la main.
///
/// Ajoute automatiquement : Rigidbody, BoxCollider, XRGrabInteractable.
/// </summary>
public class KnifeSetup : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float mass = 0.3f;

    private void Awake()
    {
        // Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Collider englobant tous les meshes enfants
        if (GetComponent<Collider>() == null)
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                var box = gameObject.AddComponent<BoxCollider>();
                box.center = transform.InverseTransformPoint(combined.center);
                // Convertir la taille world-space en local-space
                box.size = new Vector3(
                    combined.size.x / transform.lossyScale.x,
                    combined.size.y / transform.lossyScale.y,
                    combined.size.z / transform.lossyScale.z);
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        // XRGrabInteractable avec far-grab blaster
        var grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = gameObject.AddComponent<XRGrabInteractable>();

        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.farAttachMode = InteractableFarAttachMode.Near;

        // Smooth pour un rendu propre
        grab.smoothPosition = true;
        grab.smoothPositionAmount = 12f;
        grab.tightenPosition = 0.5f;
        grab.smoothRotation = true;
        grab.smoothRotationAmount = 12f;
        grab.tightenRotation = 0.5f;

        grab.attachEaseInTime = 0.1f;
    }
}
