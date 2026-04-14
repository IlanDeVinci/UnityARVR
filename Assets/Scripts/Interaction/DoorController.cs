using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Animated door that opens/closes when selected with an XR controller.
/// Automatically adds XRSimpleInteractable if missing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 3f;
    [SerializeField] private Transform pivot;

    [Header("Audio")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private AudioSource audioSource;
    private bool isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private float t;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound in VR

        Transform target = pivot != null ? pivot : transform;
        closedRotation = target.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        // Auto-setup XR interactable
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDestroy()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void Update()
    {
        Transform target = pivot != null ? pivot : transform;
        float targetT = isOpen ? 1f : 0f;
        t = Mathf.MoveTowards(t, targetT, openSpeed * Time.deltaTime);
        target.localRotation = Quaternion.Slerp(closedRotation, openRotation, t);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        AudioClip clip = isOpen ? openSound : closeSound;
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}
