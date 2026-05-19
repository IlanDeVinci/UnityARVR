using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spray paint VR : raycast depuis ce GameObject (la main du joueur) vers
/// l'avant. Quand le bouton "spray" est maintenu, une marque (quad) est
/// posée sur la surface touchée à la cadence configurée.
/// Une autre action permet de cycler la couleur.
/// </summary>
public class SprayPainter : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action 'press and hold' qui déclenche le spray (gâchette).")]
    [SerializeField] private InputActionReference sprayAction;

    [Tooltip("Action qui change la couleur (bouton primaire de la manette).")]
    [SerializeField] private InputActionReference cycleColorAction;

    [Header("Spray")]
    [Tooltip("Distance max du jet en mètres.")]
    [SerializeField] private float maxDistance = 8f;

    [Tooltip("Taille du splash en mètres (diamètre).")]
    [SerializeField] private float stampSize = 0.35f;

    [Tooltip("Variation aléatoire de taille (0 = aucune, 0.5 = +/-50%).")]
    [SerializeField, Range(0f, 0.8f)] private float sizeJitter = 0.25f;

    [Tooltip("Cadence en marques par seconde.")]
    [SerializeField] private float spraysPerSecond = 25f;

    [Header("Style")]
    [Tooltip("Matériau de base utilisé pour chaque marque (clôné à chaque spray pour avoir une couleur indépendante).")]
    [SerializeField] private Material paintBaseMaterial;

    [Tooltip("Couleurs cyclables avec cycleColorAction.")]
    [SerializeField]
    private Color[] colors = new[]
    {
        new Color(0.95f, 0.15f, 0.15f), // rouge
        new Color(0.15f, 0.6f,  0.95f), // bleu
        new Color(0.20f, 0.85f, 0.30f), // vert
        new Color(1f,    0.85f, 0.15f), // jaune
        new Color(0.95f, 0.30f, 0.85f), // rose
        new Color(0.05f, 0.05f, 0.05f), // noir
        new Color(0.95f, 0.95f, 0.95f), // blanc
    };

    [Header("Filtre")]
    [Tooltip("Surfaces sur lesquelles on peut peindre. Default = tout.")]
    [SerializeField] private LayerMask paintableLayers = ~0;

    [Tooltip("Tag exclu (ne pas peindre sur les marques précédentes).")]
    [SerializeField] private string ignoreTag = "PaintStamp";

    private int colorIndex;
    private float lastSprayTime;
    private Transform stampsParent;

    private void Awake()
    {
        // Conteneur pour ne pas polluer la racine de la scène
        var existing = GameObject.Find("PaintStamps");
        stampsParent = existing != null ? existing.transform : new GameObject("PaintStamps").transform;
    }

    private void OnEnable()
    {
        if (sprayAction != null && sprayAction.action != null)
            sprayAction.action.Enable();

        if (cycleColorAction != null && cycleColorAction.action != null)
        {
            cycleColorAction.action.Enable();
            cycleColorAction.action.performed += OnCycleColor;
        }
    }

    private void OnDisable()
    {
        if (sprayAction != null && sprayAction.action != null)
            sprayAction.action.Disable();

        if (cycleColorAction != null && cycleColorAction.action != null)
        {
            cycleColorAction.action.performed -= OnCycleColor;
            cycleColorAction.action.Disable();
        }
    }

    private void Update()
    {
        if (sprayAction == null || sprayAction.action == null) return;
        if (!sprayAction.action.IsPressed()) return;

        float interval = 1f / Mathf.Max(1f, spraysPerSecond);
        if (Time.time - lastSprayTime < interval) return;

        if (!Physics.Raycast(transform.position, transform.forward,
                out RaycastHit hit, maxDistance, paintableLayers, QueryTriggerInteraction.Ignore))
            return;

        if (!string.IsNullOrEmpty(ignoreTag) && hit.collider.CompareTag(ignoreTag))
            return;

        lastSprayTime = Time.time;
        SpawnStamp(hit);
    }

    private void SpawnStamp(RaycastHit hit)
    {
        GameObject stamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stamp.name = "PaintStamp";

        var col = stamp.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Tag (si défini dans le projet)
        try { stamp.tag = ignoreTag; } catch { /* tag pas déclaré, ignore */ }

        stamp.transform.SetParent(stampsParent, true);
        // Léger offset pour éviter le z-fighting
        stamp.transform.position = hit.point + hit.normal * 0.002f;
        stamp.transform.rotation = Quaternion.LookRotation(-hit.normal);
        stamp.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);

        float size = stampSize * (1f + Random.Range(-sizeJitter, sizeJitter));
        stamp.transform.localScale = new Vector3(size, size, 1f);

        var mr = stamp.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Material mat = paintBaseMaterial != null
            ? new Material(paintBaseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = colors[colorIndex];
        mr.sharedMaterial = mat;
    }

    private void OnCycleColor(InputAction.CallbackContext ctx)
    {
        if (colors == null || colors.Length == 0) return;
        colorIndex = (colorIndex + 1) % colors.Length;
        Debug.Log($"[SprayPainter] Couleur : {ColorUtility.ToHtmlStringRGB(colors[colorIndex])}");
    }

    /// <summary>API publique pour changer la couleur depuis l'UI.</summary>
    public void SetColorIndex(int index)
    {
        if (colors == null || colors.Length == 0) return;
        colorIndex = ((index % colors.Length) + colors.Length) % colors.Length;
    }
}
