using UnityEngine;

/// <summary>
/// Plaque de cuisson : quand une poêle (FryingPan ou pikachu_poele) est posée
/// au-dessus du nœud cible (par défaut index 6), affiche un FX de fumée.
/// </summary>
public class CookingPlate : MonoBehaviour
{
    [Header("Plaque")]
    [Tooltip("Index du child mesh qui correspond à la plaque (nœud 6 par défaut).")]
    [SerializeField] private int plateChildIndex = 6;

    [Tooltip("Hauteur de la zone de détection au-dessus de la plaque.")]
    [SerializeField] private float detectionHeight = 0.5f;

    [Tooltip("Hauteur où la fumée apparaît au-dessus de la plaque.")]
    [SerializeField] private float smokeHeightOffset = 0.3f;

    private Transform plateNode;
    private GameObject smokeFX;
    private int objectsOnPlate = 0;

    private void Start()
    {
        plateNode = FindPlateNode();
        if (plateNode == null)
        {
            Debug.LogWarning($"[CookingPlate] Nœud {plateChildIndex} introuvable sur {name}");
            return;
        }

        // Ajouter un trigger au-dessus de la plaque
        CreateTriggerZone();

        // Créer le système de fumée (désactivé au départ)
        CreateSmokeFX();
    }

    private Transform FindPlateNode()
    {
        // Chercher le Nth mesh renderer en descente hiérarchique
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

        // Un relais qui transmet les events à ce script
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
        main.gravityModifier = -0.05f; // Monte légèrement

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

        // Renderer avec un material de particules par défaut
        var renderer = smokeFX.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        if (renderer.material.shader == null || renderer.material.shader.name == "Hidden/InternalErrorShader")
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        ps.Stop();
    }

    private void OnObjectEnter(Collider other)
    {
        if (!IsPan(other)) return;
        objectsOnPlate++;
        if (objectsOnPlate == 1 && smokeFX != null)
        {
            var ps = smokeFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }
    }

    private void OnObjectExit(Collider other)
    {
        if (!IsPan(other)) return;
        objectsOnPlate = Mathf.Max(0, objectsOnPlate - 1);
        if (objectsOnPlate == 0 && smokeFX != null)
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
