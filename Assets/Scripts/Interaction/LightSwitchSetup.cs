using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Interrupteur VR interactif avec animation et contrôle des lumières.
/// Fonctionne avec les modèles GLB qui ont des animations "TurnOn" / "TurnOff".
///
/// Setup :
///   1. Ajouter ce script sur le root du modèle d'interrupteur.
///   2. Si le champ lights est vide, le script trouvera automatiquement
///      toutes les lumières de la scène (sauf la Directional Light).
///      Si aucune n'est trouvée, il contrôle la Directional Light.
/// </summary>
public class LightSwitchSetup : MonoBehaviour
{
    [Header("Lumières à contrôler")]
    [Tooltip("Si vide, toutes les lumières de la scène seront détectées automatiquement.")]
    [SerializeField] private Light[] lights;

    [Header("État initial")]
    [SerializeField] private bool startsOn = true;

    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;

    private Animator animator;
    private AudioSource audioSource;
    private bool isOn;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        // Collider englobant pour l'interaction XR (même logique que la porte)
        if (GetComponent<Collider>() == null)
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length > 0)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                Bounds combined = new Bounds(
                    transform.InverseTransformPoint(renderers[0].bounds.center),
                    Vector3.zero);
                foreach (var r in renderers)
                {
                    Bounds wb = r.bounds;
                    combined.Encapsulate(transform.InverseTransformPoint(wb.min));
                    combined.Encapsulate(transform.InverseTransformPoint(wb.max));
                }
                box.center = combined.center;
                box.size = combined.size;
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        // XR interaction
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.selectEntered.AddListener(OnSelected);
    }

    private void Start()
    {
        // Auto-détecter les lumières si aucune n'est assignée
        if (lights == null || lights.Length == 0)
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        isOn = startsOn;
        ApplyState(false);
    }

    private void OnDestroy()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        Toggle();
    }

    public void Toggle()
    {
        isOn = !isOn;
        ApplyState(true);
    }

    private void ApplyState(bool playEffects)
    {
        // Animation
        if (animator != null)
        {
            string clip = isOn ? "TurnOn" : "TurnOff";
            animator.Play(clip, 0, 0f);
        }

        // Lumières
        foreach (var l in lights)
        {
            if (l != null)
                l.enabled = isOn;
        }

        // Son
        if (playEffects && switchSound != null)
            audioSource.PlayOneShot(switchSound);
    }
}
