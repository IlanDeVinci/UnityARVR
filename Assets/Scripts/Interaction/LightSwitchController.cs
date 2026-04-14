using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Toggles lights on/off when selected with an XR controller.
/// Automatically adds XRSimpleInteractable if missing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LightSwitchController : MonoBehaviour
{
    [Header("Lights to Control")]
    [SerializeField] private Light[] lights;

    [Header("Visual Feedback")]
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Renderer switchRenderer;

    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;

    private AudioSource audioSource;
    private bool isOn;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound in VR

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

    private void Start()
    {
        SetLights(false);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        ToggleSwitch();
    }

    public void ToggleSwitch()
    {
        isOn = !isOn;
        SetLights(isOn);

        if (switchSound != null)
            audioSource.PlayOneShot(switchSound);
    }

    private void SetLights(bool on)
    {
        foreach (Light l in lights)
        {
            if (l != null) l.enabled = on;
        }

        if (switchRenderer != null)
            switchRenderer.material = on ? onMaterial : offMaterial;
    }
}
