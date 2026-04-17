using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

[RequireComponent(typeof(Collider))]
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

        // Automatically detect dimensions from the Mesh and Scale
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            // Calculate real world size (Mesh size * Object scale)
            objectDimensions = Vector3.Scale(meshFilter.sharedMesh.bounds.size, transform.localScale);
        }
        else
        {
            // Fallback to your provided dimensions if no mesh is found
            objectDimensions = new Vector3(3f, 4f, 0.5f);
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
}