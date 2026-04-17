using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using TMPro;
using System.Collections;

/// <summary>
/// Plaque de cuisson : quand une poêle (FryingPan ou pikachu_poele) est posée
/// au-dessus du nœud cible, affiche un FX de fumée + un chronomètre de 30s.
/// À zéro, affiche "C'EST CUIT !" et remplace la poêle par poele_steak.
/// </summary>
public class CookingPlate : MonoBehaviour
{
    [Header("Plaque")]
    [Tooltip("Index du child mesh qui correspond à la plaque.")]
    [SerializeField] private int plateChildIndex = 17;
    [SerializeField] private float detectionHeight = 0.5f;
    [SerializeField] private float smokeHeightOffset = 0.3f;

    [Header("Cuisson")]
    [SerializeField] private float cookDuration = 30f;
    [SerializeField] private float timerHeightOffset = 0.6f;

    private Transform plateNode;
    private GameObject smokeFX;
    private GameObject timerUI;
    private TextMeshPro timerText;
    private GameObject panOnPlate;
    private Coroutine cookingRoutine;

    private void Start()
    {
        plateNode = FindPlateNode();
        if (plateNode == null)
        {
            Debug.LogWarning($"[CookingPlate] Nœud {plateChildIndex} introuvable sur {name}");
            return;
        }

        CreateTriggerZone();
        CreateSmokeFX();
        CreateTimerUI();
    }

    private Transform FindPlateNode()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        if (plateChildIndex >= 0 && plateChildIndex < renderers.Length)
            return renderers[plateChildIndex].transform;
        return null;
    }

    private void CreateTriggerZone()
    {
        var mr = plateNode.GetComponent<MeshRenderer>();
        Bounds b = mr != null ? mr.bounds : new Bounds(plateNode.position, Vector3.one * 0.5f);

        GameObject zone = new GameObject("PlateTrigger");
        zone.transform.SetParent(transform, false);
        zone.transform.position = b.center + Vector3.up * detectionHeight * 0.5f;
        zone.transform.rotation = transform.rotation;

        var col = zone.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(b.size.x, detectionHeight, b.size.z);

        var relay = zone.AddComponent<TriggerRelay>();
        relay.onEnter = OnObjectEnter;
        relay.onExit = OnObjectExit;
    }

    private void CreateSmokeFX()
    {
        smokeFX = new GameObject("SmokeFX");
        smokeFX.transform.SetParent(transform, false);

        var mr = plateNode.GetComponent<MeshRenderer>();
        Vector3 center = mr != null ? mr.bounds.center : plateNode.position;
        smokeFX.transform.position = center + Vector3.up * smokeHeightOffset;

        var ps = smokeFX.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 2f;
        main.startSpeed = 0.3f;
        main.startSize = 0.25f;
        main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.4f);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;

        var emission = ps.emission;
        emission.rateOverTime = 15f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(1f, 2f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f),
                new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = smokeFX.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        ps.Stop();
    }

    private void CreateTimerUI()
    {
        timerUI = new GameObject("CookingTimer");
        timerUI.transform.SetParent(transform, false);

        var mr = plateNode.GetComponent<MeshRenderer>();
        Vector3 center = mr != null ? mr.bounds.center : plateNode.position;
        timerUI.transform.position = center + Vector3.up * timerHeightOffset;

        timerText = timerUI.AddComponent<TextMeshPro>();
        timerText.text = "30";
        timerText.fontSize = 2;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = new Color(1f, 0.9f, 0.3f);
        timerText.fontStyle = FontStyles.Bold;

        // Orient vers la caméra
        timerUI.AddComponent<FaceCamera>();

        timerUI.SetActive(false);
    }

    private void OnObjectEnter(Collider other)
    {
        if (!IsPan(other)) return;

        GameObject pan = other.GetComponent<FryingPan>() != null
            ? other.gameObject
            : other.GetComponentInParent<FryingPan>()?.gameObject;

        if (pan == null)
        {
            // Fallback : chercher via le nom
            pan = other.gameObject;
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("poele"))
                pan = other.transform.parent.gameObject;
        }

        if (panOnPlate != null) return; // Déjà en cuisson

        panOnPlate = pan;

        // Démarrer la fumée
        if (smokeFX != null)
        {
            var ps = smokeFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        // Démarrer le chronomètre
        if (cookingRoutine != null) StopCoroutine(cookingRoutine);
        cookingRoutine = StartCoroutine(CookCountdown());
    }

    private void OnObjectExit(Collider other)
    {
        if (!IsPan(other)) return;

        // Vérifier que c'est bien la poêle qu'on suit
        GameObject pan = other.gameObject;
        if (other.transform.parent != null && other.transform.parent == panOnPlate?.transform)
            pan = other.transform.parent.gameObject;
        if (other.GetComponentInParent<FryingPan>()?.gameObject == panOnPlate)
            pan = panOnPlate;

        if (panOnPlate == null || pan != panOnPlate) return;

        StopCooking();
    }

    private IEnumerator CookCountdown()
    {
        float remaining = cookDuration;
        timerUI.SetActive(true);

        while (remaining > 0f)
        {
            if (panOnPlate == null)
            {
                StopCooking();
                yield break;
            }

            timerText.text = Mathf.CeilToInt(remaining).ToString();

            // Couleur qui passe au rouge en fin de cuisson
            float t = 1f - (remaining / cookDuration);
            timerText.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.3f, 0.1f), t);

            remaining -= Time.deltaTime;
            yield return null;
        }

        // Cuisson terminée
        yield return ShowCuitThenReplace();
    }

    private IEnumerator ShowCuitThenReplace()
    {
        // Afficher "C'EST CUIT !"
        timerText.text = "C'EST CUIT !";
        timerText.color = new Color(0.3f, 1f, 0.3f);
        timerText.fontSize = 1.2f;

        yield return new WaitForSeconds(2.5f);

        // Remplacer la poêle par poele_steak
        if (panOnPlate != null)
        {
            ReplacePan(panOnPlate);
            panOnPlate = null;
        }

        // Nettoyer
        timerUI.SetActive(false);
        timerText.fontSize = 2;

        if (smokeFX != null)
        {
            var ps = smokeFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop();
        }
    }

    private void ReplacePan(GameObject oldPan)
    {
        var steakPrefab = Resources.Load<GameObject>("poele_steak");
        if (steakPrefab == null)
        {
            Debug.LogError("[CookingPlate] poele_steak introuvable dans Resources/");
            return;
        }

        Vector3 pos = oldPan.transform.position;
        Quaternion rot = oldPan.transform.rotation;
        Vector3 scale = oldPan.transform.localScale;

        Destroy(oldPan);

        GameObject steak = Instantiate(steakPrefab, pos, rot);
        steak.name = "Poele_Steak";
        steak.transform.localScale = scale;

        // Rendre grabbable
        MakeGrabbable(steak);
    }

    private static void MakeGrabbable(GameObject obj)
    {
        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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

        var g = obj.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        g.throwOnDetach = true;
        g.useDynamicAttach = true;
        g.farAttachMode = InteractableFarAttachMode.Near;
        g.smoothPosition = true;
        g.smoothPositionAmount = 12f;
        g.tightenPosition = 0.5f;
        g.smoothRotation = true;
        g.smoothRotationAmount = 12f;
        g.tightenRotation = 0.5f;
        g.attachEaseInTime = 0.1f;
    }

    private void StopCooking()
    {
        if (cookingRoutine != null)
        {
            StopCoroutine(cookingRoutine);
            cookingRoutine = null;
        }
        panOnPlate = null;

        if (timerUI != null) timerUI.SetActive(false);

        if (smokeFX != null)
        {
            var ps = smokeFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop();
        }
    }

    private bool IsPan(Collider col)
    {
        if (col.GetComponent<FryingPan>() != null) return true;
        if (col.GetComponentInParent<FryingPan>() != null) return true;

        string n = col.gameObject.name.ToLower();
        if (n.Contains("poele") || n.Contains("pan")) return true;

        if (col.transform.parent != null)
        {
            string pn = col.transform.parent.name.ToLower();
            if (pn.Contains("poele") || pn.Contains("pan")) return true;
        }
        return false;
    }
}

/// <summary>
/// Relais d'événements trigger vers des callbacks externes.
/// </summary>
public class TriggerRelay : MonoBehaviour
{
    public System.Action<Collider> onEnter;
    public System.Action<Collider> onExit;

    private void OnTriggerEnter(Collider other) => onEnter?.Invoke(other);
    private void OnTriggerExit(Collider other) => onExit?.Invoke(other);
}

/// <summary>
/// Fait toujours face à la caméra principale (billboard).
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position,
            Vector3.up);
    }
}
