using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

/// <summary>
/// Interrupteur VR interactif avec animation du rocker et contrôle des lumières.
/// Anime directement le nœud "Rocker" du modèle GLB (pas besoin d'Animator Controller).
///
/// Setup :
///   1. Ajouter ce script sur le root du modèle d'interrupteur.
///   2. Si le champ lights est vide, le script trouvera automatiquement
///      toutes les lumières de la scène.
/// </summary>
public class LightSwitchSetup : MonoBehaviour
{
    [Header("Lumières à contrôler")]
    [Tooltip("Si vide, toutes les lumières de la scène seront détectées automatiquement.")]
    [SerializeField] private Light[] lights;

    [Header("État initial")]
    [SerializeField] private bool startsOn = true;

    [Header("Animation")]
    [Tooltip("Nom du nœud enfant qui bascule (le bouton de l'interrupteur).")]
    [SerializeField] private string rockerNodeName = "Rocker";
    [SerializeField] private float animationDuration = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;

    private AudioSource audioSource;
    private Transform rocker;
    private bool isOn;

    // Rotations extraites du GLB : TurnOn = -0.0749 en X, TurnOff = +0.0749 en X
    private static readonly Quaternion onRotation = new Quaternion(-0.0749f, 0f, 0f, 0.9972f);
    private static readonly Quaternion offRotation = new Quaternion(0.0749f, 0f, 0f, 0.9972f);

    private Coroutine animRoutine;

    private void Awake()
    {
        // Trouver le rocker dans les enfants
        rocker = FindChildRecursive(transform, rockerNodeName);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        // Collider englobant pour l'interaction XR
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
                    combined.Encapsulate(transform.InverseTransformPoint(r.bounds.min));
                    combined.Encapsulate(transform.InverseTransformPoint(r.bounds.max));
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
        // Animation du rocker
        Quaternion target = isOn ? onRotation : offRotation;

        if (rocker != null)
        {
            if (playEffects && animationDuration > 0f)
            {
                if (animRoutine != null) StopCoroutine(animRoutine);
                animRoutine = StartCoroutine(AnimateRocker(target));
            }
            else
            {
                rocker.localRotation = target;
            }
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

    private IEnumerator AnimateRocker(Quaternion target)
    {
        Quaternion start = rocker.localRotation;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out pour un mouvement naturel de switch
            t = 1f - (1f - t) * (1f - t);
            rocker.localRotation = Quaternion.Slerp(start, target, t);
            yield return null;
        }

        rocker.localRotation = target;
        animRoutine = null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
