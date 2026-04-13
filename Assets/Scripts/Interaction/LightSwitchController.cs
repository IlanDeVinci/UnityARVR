using UnityEngine;

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
    }

    private void Start()
    {
        // Start with lights off
        SetLights(false);
    }

    /// <summary>
    /// Called by the player interaction system when clicking the switch.
    /// </summary>
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
        {
            switchRenderer.material = on ? onMaterial : offMaterial;
        }
    }
}
