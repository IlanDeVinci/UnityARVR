using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

/// <summary>
/// Planche à découper avec 2 étapes :
///   1. Poser un Pikachu qui court → il devient Pikachu couché
///   2. Toucher le Pikachu couché avec le couteau → il devient Pikachu découpé
///
/// Les modèles sont chargés depuis Resources/.
/// Le couteau est détecté par le tag "Knife" ou par le composant KnifeSetup.
/// </summary>
public class CuttingBoard : MonoBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float detectionHeight = 0.5f;
    [SerializeField] private float modelScale = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip cutSound;

    private AudioSource audioSource;
    private GameObject couchePrefab;
    private GameObject decoupePrefab;

    // Le Pikachu couché actuellement sur la planche
    private GameObject pikachuOnBoard;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // Charger les modèles
        couchePrefab = Resources.Load<GameObject>("pikachu_couche");
        if (couchePrefab == null)
            Debug.LogError("[CuttingBoard] pikachu_couche introuvable dans Resources/");

        decoupePrefab = Resources.Load<GameObject>("pikachu_decoupe");
        if (decoupePrefab == null)
            Debug.LogError("[CuttingBoard] pikachu_decoupe introuvable dans Resources/");

        // Zone de détection au-dessus de la planche
        var triggerZone = gameObject.AddComponent<BoxCollider>();
        triggerZone.isTrigger = true;

        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = transform.InverseTransformPoint(combined.center);
            Vector3 localSize = combined.size;

            triggerZone.center = localCenter + Vector3.up * detectionHeight * 0.5f;
            triggerZone.size = new Vector3(localSize.x, detectionHeight, localSize.z);
        }
        else
        {
            triggerZone.center = Vector3.up * detectionHeight * 0.5f;
            triggerZone.size = new Vector3(1f, detectionHeight, 1f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- Cas 1 : un Pikachu qui court tombe sur la planche ---
        var wander = other.GetComponent<PikachuWander>();
        if (wander == null)
            wander = other.GetComponentInParent<PikachuWander>();

        if (wander != null)
        {
            // Ne pas transformer s'il est tenu en main
            var grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null)
                grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected) return;

            PlacePikachuOnBoard(wander.gameObject);
            return;
        }

        // --- Cas 2 : le couteau touche la planche (avec un Pikachu couché dessus) ---
        if (pikachuOnBoard != null && IsKnife(other))
        {
            CutPikachu();
        }
    }

    private bool IsKnife(Collider col)
    {
        // Détecter par le composant KnifeSetup
        if (col.GetComponent<KnifeSetup>() != null) return true;
        if (col.GetComponentInParent<KnifeSetup>() != null) return true;

        // Ou par le nom de l'objet
        string name = col.gameObject.name.ToLower();
        if (name.Contains("couteau") || name.Contains("knife") || name.Contains("scalp") || name.Contains("lame"))
            return true;

        Transform parent = col.transform.parent;
        if (parent != null)
        {
            string parentName = parent.name.ToLower();
            if (parentName.Contains("couteau") || parentName.Contains("knife") || parentName.Contains("scalp") || parentName.Contains("lame"))
                return true;
        }

        return false;
    }

    private void PlacePikachuOnBoard(GameObject pikachuRunning)
    {
        if (couchePrefab == null) return;

        // Supprimer l'ancien Pikachu sur la planche s'il y en a un
        if (pikachuOnBoard != null)
            Destroy(pikachuOnBoard);

        Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
        Quaternion spawnRot = transform.rotation;

        if (placeSound != null)
            audioSource.PlayOneShot(placeSound);

        Destroy(pikachuRunning);

        pikachuOnBoard = Instantiate(couchePrefab, spawnPos, spawnRot);
        pikachuOnBoard.name = "Pikachu_Couche";
        pikachuOnBoard.transform.localScale = Vector3.one * modelScale;
    }

    private void CutPikachu()
    {
        if (decoupePrefab == null || pikachuOnBoard == null) return;

        Vector3 pos = pikachuOnBoard.transform.position;
        Quaternion rot = pikachuOnBoard.transform.rotation;

        if (cutSound != null)
            audioSource.PlayOneShot(cutSound);

        Destroy(pikachuOnBoard);
        pikachuOnBoard = null;

        GameObject sliced = Instantiate(decoupePrefab, pos, rot);
        sliced.name = "Pikachu_Decoupe";
        sliced.transform.localScale = Vector3.one * modelScale;

        // Rendre le pikachu découpé grabbable pour le mettre dans la poêle
        MakeGrabbable(sliced);
    }

    /// <summary>
    /// Ajoute Rigidbody + BoxCollider + XRGrabInteractable à un objet
    /// pour qu'il soit grabbable en VR.
    /// </summary>
    private void MakeGrabbable(GameObject obj)
    {
        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.isKinematic = true; // Reste en place tant qu'on ne le grab pas

        // Collider englobant
        var renderers = obj.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            var box = obj.AddComponent<BoxCollider>();
            box.center = obj.transform.InverseTransformPoint(combined.center);
            box.size = new Vector3(
                combined.size.x / obj.transform.lossyScale.x,
                combined.size.y / obj.transform.lossyScale.y,
                combined.size.z / obj.transform.lossyScale.z);
        }
        else
        {
            obj.AddComponent<BoxCollider>();
        }

        // XRGrabInteractable avec far grab
        var grab = obj.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.farAttachMode = InteractableFarAttachMode.Near;

        // Quand on le grab, le rendre non-kinematic pour la physique
        grab.selectEntered.AddListener(args => { rb.isKinematic = false; });
    }
}
