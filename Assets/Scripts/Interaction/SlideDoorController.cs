using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

public class SlidingDoorController : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 3f;
    public bool isOpen = false;

    private Vector3 initialPosition;
    private Vector3 objectDimensions;
    private Coroutine currentRoutine;

    void Awake()
    {
        initialPosition = transform.localPosition;

        // Detect dimensions — works whether meshes are on this object or on children
        objectDimensions = ComputeBounds();

        // Ensure a collider exists for XR interaction
        if (GetComponent<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            // Size the collider to cover all child meshes
            Bounds local = ComputeLocalBounds();
            box.center = local.center;
            box.size = local.size;
        }

        // Auto-setup XR interactable for VR controller support
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.selectEntered.AddListener(OnSelected);
    }

    void OnDestroy()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        // Handle interruption: stop current move before starting a new one
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        isOpen = !isOpen;
        currentRoutine = StartCoroutine(AnimateDoor());
    }

    IEnumerator AnimateDoor()
    {
        // Calculate distances with 10% margin (1.1f)
        float slideDistance = objectDimensions.x * 1.1f;
        float clearDistance = objectDimensions.z * 1.1f;

        // Define target positions based on local axes
        Vector3 clearedPosition = initialPosition + (Vector3.forward * clearDistance);
        Vector3 fullyOpenPosition = clearedPosition + (Vector3.right * slideDistance);

        if (isOpen)
        {
            // OPEN SEQUENCE: Move Forward (Z) then Slide Right (X)
            yield return StartCoroutine(MoveTo(clearedPosition));
            yield return StartCoroutine(MoveTo(fullyOpenPosition));
        }
        else
        {
            // CLOSE SEQUENCE: Slide Left (X) then Move Back (Z)
            yield return StartCoroutine(MoveTo(clearedPosition));
            yield return StartCoroutine(MoveTo(initialPosition));
        }

        currentRoutine = null;
    }

    IEnumerator MoveTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localPosition, target) > 0.001f)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                target,
                speed * Time.deltaTime
            );
            yield return null;
        }
        transform.localPosition = target;
    }

    /// <summary>
    /// Returns world-space dimensions of the door (this object + all children).
    /// </summary>
    private Vector3 ComputeBounds()
    {
        // Try this object first
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return Vector3.Scale(mf.sharedMesh.bounds.size, transform.localScale);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
            return mr.bounds.size;

        // Combine all child renderers
        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);
            return combined.size;
        }

        return new Vector3(3f, 4f, 0.5f);
    }

    /// <summary>
    /// Returns local-space bounds encompassing all child meshes (for collider setup).
    /// </summary>
    private Bounds ComputeLocalBounds()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        // Convert world bounds to local space
        Bounds combined = new Bounds(
            transform.InverseTransformPoint(renderers[0].bounds.center),
            Vector3.zero);

        foreach (var r in renderers)
        {
            // Encapsulate the 8 corners of each renderer's world bounds in local space
            Bounds wb = r.bounds;
            Vector3 min = wb.min;
            Vector3 max = wb.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                combined.Encapsulate(transform.InverseTransformPoint(corner));
            }
        }

        return combined;
    }
}