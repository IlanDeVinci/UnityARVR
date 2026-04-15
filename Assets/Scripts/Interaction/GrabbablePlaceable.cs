using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Properly configures an XRGrabInteractable with good VR defaults and optionally
/// returns the object to its origin if dropped too far away.
///
/// Setup:
///   1. Add to any GameObject you want to be grabbable.
///      Rigidbody and XRGrabInteractable are auto-required.
///   2. Make sure the object is on an Interaction Layer that your
///      XR Ray / Direct interactors can select.
///   3. Enable returnIfDropped if the object should float back to its
///      start position when released far from a snap zone.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabbablePlaceable : MonoBehaviour
{
    [Header("Grab")]
    [SerializeField] private XRBaseInteractable.MovementType movementType = XRBaseInteractable.MovementType.VelocityTracking;
    [SerializeField] private bool throwOnRelease = true;

    [Header("Return to Origin")]
    [SerializeField] private bool returnIfDropped = false;
    [SerializeField] private float maxDropDistance = 5f;
    [SerializeField] private float returnSpeed = 4f;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private Vector3 _originPos;
    private Quaternion _originRot;
    private bool _isGrabbed;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb   = GetComponent<Rigidbody>();

        _grab.movementType = movementType;
        _grab.throwOnDetach = throwOnRelease;

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void Start()
    {
        _originPos = transform.position;
        _originRot = transform.rotation;
    }

    private void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args) => _isGrabbed = true;

    private void OnReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        if (returnIfDropped)
            StartCoroutine(CheckAndReturn());
    }

    private IEnumerator CheckAndReturn()
    {
        yield return new WaitForSeconds(1.5f);
        if (!_isGrabbed && Vector3.Distance(transform.position, _originPos) > maxDropDistance)
            StartCoroutine(ReturnToOrigin());
    }

    private IEnumerator ReturnToOrigin()
    {
        _rb.isKinematic = true;
        while (!_isGrabbed && Vector3.Distance(transform.position, _originPos) > 0.02f)
        {
            transform.position = Vector3.Lerp(transform.position, _originPos, Time.deltaTime * returnSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _originRot, Time.deltaTime * returnSpeed);
            yield return null;
        }
        if (!_isGrabbed)
        {
            transform.position = _originPos;
            transform.rotation = _originRot;
        }
        _rb.isKinematic = false;
    }
}
