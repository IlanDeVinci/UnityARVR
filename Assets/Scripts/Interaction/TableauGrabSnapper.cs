using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Snap automatique d'un tableau sur un mur après grab/relâcher.
/// Si le joueur relâche le tableau hors d'un mur, il retourne à sa dernière
/// position valide (le mur sur lequel il était).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TableauGrabSnapper : MonoBehaviour
{
    [SerializeField] private float wallNormalThreshold = 0.4f;
    [SerializeField] private float searchDistance = 1.2f;
    [SerializeField] private LayerMask wallLayers = ~0;

    private XRGrabInteractable grab;
    private Rigidbody rb;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;
    private bool hasValid;

    public void InitAnchor(Vector3 position, Quaternion rotation)
    {
        lastValidPosition = position;
        lastValidRotation = rotation;
        hasValid = true;
        transform.SetPositionAndRotation(position, rotation);
    }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = false;

        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (TrySnapToWall())
        {
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
            hasValid = true;
        }
        else if (hasValid)
        {
            transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private bool TrySnapToWall()
    {
        var ownCollider = GetComponent<Collider>();
        bool prevEnabled = ownCollider != null && ownCollider.enabled;
        if (ownCollider != null) ownCollider.enabled = false;

        bool snapped = false;
        Vector3 origin = transform.position;
        Vector3 back = -transform.forward;

        if (Physics.Raycast(origin, back, out RaycastHit hit, searchDistance, wallLayers, QueryTriggerInteraction.Ignore)
            && IsWall(hit.normal))
        {
            transform.position = hit.point + hit.normal * 0.002f;
            transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
            snapped = true;
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Physics.Raycast(origin, dir, out RaycastHit h, searchDistance, wallLayers, QueryTriggerInteraction.Ignore)
                    && IsWall(h.normal))
                {
                    transform.position = h.point + h.normal * 0.002f;
                    transform.rotation = Quaternion.LookRotation(h.normal, Vector3.up);
                    snapped = true;
                    break;
                }
            }
        }

        if (ownCollider != null) ownCollider.enabled = prevEnabled;
        return snapped;
    }

    private bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < wallNormalThreshold;
}
