using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spray paint VR : raycast depuis ce GameObject (main du joueur) vers
/// l'avant. Quand le bouton "spray" est maintenu, une marque (quad) est
/// posée sur la surface touchée à la cadence configurée.
///
/// L'état (pinceau, couleur, taille) est piloté par le PaintMenuUI via
/// l'API publique.
/// </summary>
public class SprayPainter : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action 'press and hold' qui déclenche le spray (gâchette).")]
    [SerializeField] private InputActionReference sprayAction;

    [Tooltip("Action qui change le pinceau (bouton secondaire).")]
    [SerializeField] private InputActionReference cycleBrushAction;

    [Header("Spray")]
    [Tooltip("Distance max du jet en mètres (au-delà, on ne peint pas).")]
    [SerializeField] private float maxDistance = 0.80f;

    [Tooltip("Taille du splash en mètres (diamètre) à distance idéale.")]
    [SerializeField] private float stampSize = 0.35f;

    [Tooltip("Variation aléatoire de taille (0 = aucune, 0.5 = +/-50%).")]
    [SerializeField, Range(0f, 0.8f)] private float sizeJitter = 0.25f;

    [Tooltip("Cadence en marques par seconde.")]
    [SerializeField] private float spraysPerSecond = 25f;

    [Header("Distance dynamics")]
    [Tooltip("Distance idéale (m) où le spray est le plus harmonieux.")]
    [SerializeField] private float idealDistance = 0.20f;

    [Tooltip("Distance (m) en dessous de laquelle la peinture devient pâteuse / très opaque.")]
    [SerializeField] private float closeDistance = 0.05f;

    [Tooltip("Distance (m) au-delà de laquelle le spray est totalement diffus / léger.")]
    [SerializeField] private float farDistance = 0.80f;

    [Tooltip("Facteur de taille quand la bombe est très proche (pâté concentré).")]
    [SerializeField, Range(0.1f, 1f)] private float closeSizeFactor = 0.45f;

    [Tooltip("Facteur de taille quand le spray est éloigné (très étalé).")]
    [SerializeField, Range(1f, 4f)] private float farSizeFactor = 2.4f;

    [Tooltip("Opacité quand le spray est éloigné (diffus).")]
    [SerializeField, Range(0f, 1f)] private float farOpacity = 0.25f;

    [Header("Brushes")]
    [Tooltip("Textures de pinceaux disponibles (premier = défaut).")]
    [SerializeField] private Texture2D[] brushes;

    [Header("Couleur")]
    [Tooltip("Couleur courante du spray.")]
    [SerializeField] private Color currentColor = new Color(0.95f, 0.15f, 0.15f);

    [Header("Style")]
    [Tooltip("Matériau de base utilisé pour chaque marque (cloné à chaque spray).")]
    [SerializeField] private Material paintBaseMaterial;

    [Header("Filtre")]
    [Tooltip("Surfaces sur lesquelles on peut peindre. Default = tout.")]
    [SerializeField] private LayerMask paintableLayers = ~0;

    [Tooltip("Tag exclu (ne pas peindre sur les marques précédentes).")]
    [SerializeField] private string ignoreTag = "PaintStamp";

    [Header("Visuel manette")]
    [Tooltip("Préfab du modèle 3D de spray (ex. Assets/Models/Spray.glb) instancié comme enfant pour remplacer la manette.")]
    [SerializeField] private GameObject sprayModelPrefab;

    [Tooltip("Racine du visuel de la manette à masquer pendant que le spray est tenu (optionnel — SetActive(false) sur cet objet).")]
    [SerializeField] private GameObject controllerVisualRoot;

    [Tooltip("Si vrai, désactive automatiquement tous les Renderers enfants de la manette (utile quand le modèle est chargé runtime par l'XR rig via XRControllerModel).")]
    [SerializeField] private bool autoHideControllerRenderers = true;

    [Tooltip("Racine sous laquelle chercher les Renderers à masquer automatiquement. Si vide, le parent de ce GameObject est utilisé.")]
    [SerializeField] private Transform controllerVisualSearchRoot;

    [Tooltip("Point d'ancrage où placer le modèle. Si vide, ce GameObject est utilisé.")]
    [SerializeField] private Transform sprayAttachPoint;

    [Tooltip("Position locale du modèle Spray par rapport à l'ancre.")]
    [SerializeField] private Vector3 sprayLocalPosition = Vector3.zero;

    [Tooltip("Rotation locale (Euler, degrés) du modèle Spray.")]
    [SerializeField] private Vector3 sprayLocalEulerAngles = Vector3.zero;

    [Tooltip("Échelle uniforme appliquée au modèle Spray.")]
    [SerializeField] private float sprayScale = 1f;

    [Tooltip("Si vrai : le spray ne remplace la manette que tant que la gâchette est pressée. Sinon : remplacement permanent tant que SprayPainter est actif.")]
    [SerializeField] private bool swapOnlyWhileSpraying = false;

    [Header("Réticule de visée")]
    [Tooltip("Affiche un petit point jaune brillant à l'endroit visé par la manette.")]
    [SerializeField] private bool showReticle = true;

    [Tooltip("Diamètre du point de visée (m).")]
    [SerializeField, Range(0.002f, 0.05f)] private float reticleSize = 0.012f;

    [Tooltip("Couleur du point de visée.")]
    [SerializeField] private Color reticleColor = new Color(1f, 0.92f, 0.15f);

    [Tooltip("Intensité émissive du point de visée (HDR).")]
    [SerializeField, Range(0f, 8f)] private float reticleEmissionIntensity = 3f;

    private int brushIndex;
    private float lastSprayTime;
    private Transform stampsParent;
    private int stampCounter;
    private GameObject sprayModelInstance;
    private bool sprayVisualActive;
    private GameObject reticleInstance;
    private MeshRenderer reticleRenderer;
    private bool ignoreTagValid;
    private readonly Dictionary<Renderer, bool> autoHiddenRenderers = new Dictionary<Renderer, bool>();
    private readonly List<Renderer> sprayModelRenderers = new List<Renderer>();
    private bool sprayModelLoaded;

    public Texture2D[] Brushes => brushes;
    public int BrushIndex => brushIndex;
    public Color CurrentColor => currentColor;
    public float StampSize => stampSize;

    private void Awake()
    {
        var existing = GameObject.Find("PaintStamps");
        stampsParent = existing != null ? existing.transform : new GameObject("PaintStamps").transform;

        ignoreTagValid = ValidateTag(ignoreTag);

        SpawnSprayModel();
        SpawnReticle();
    }

    private static bool ValidateTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        try
        {
            // Lève UnityException si le tag n'est pas défini dans Tags & Layers.
            GameObject.FindGameObjectsWithTag(tag);
            return true;
        }
        catch
        {
            Debug.LogWarning($"[SprayPainter] Tag '{tag}' non déclaré dans Project Settings → Tags & Layers. La protection 'ne pas peindre par-dessus une marque' est désactivée. Ajoute ce tag pour l'activer.");
            return false;
        }
    }

    private void OnEnable()
    {
        if (sprayAction != null && sprayAction.action != null)
            sprayAction.action.Enable();

        if (cycleBrushAction != null && cycleBrushAction.action != null)
        {
            cycleBrushAction.action.Enable();
            cycleBrushAction.action.performed += OnCycleBrush;
        }

        // En mode "toujours visible", on swappe dès que ce composant devient actif.
        if (!swapOnlyWhileSpraying) SetSprayVisualActive(true);
    }

    private void OnDisable()
    {
        if (sprayAction != null && sprayAction.action != null)
            sprayAction.action.Disable();

        if (cycleBrushAction != null && cycleBrushAction.action != null)
        {
            cycleBrushAction.action.performed -= OnCycleBrush;
            cycleBrushAction.action.Disable();
        }

        // On rend toujours la manette à la désactivation, peu importe le mode.
        SetSprayVisualActive(false);
    }

    private void SpawnSprayModel()
    {
        if (sprayModelPrefab == null)
        {
            Debug.LogWarning("[SprayPainter] Champ 'Spray Model Prefab' vide dans l'inspector. Glisse Assets/Models/Spray.glb dans ce champ pour afficher le modèle à la place de la manette.", this);
            return;
        }
        if (sprayModelInstance != null) return;

        Transform anchor = sprayAttachPoint != null ? sprayAttachPoint : transform;
        sprayModelInstance = Instantiate(sprayModelPrefab, anchor);
        sprayModelInstance.name = "SprayModel";
        sprayModelInstance.transform.localPosition = sprayLocalPosition;
        sprayModelInstance.transform.localEulerAngles = sprayLocalEulerAngles;
        sprayModelInstance.transform.localScale = Vector3.one * sprayScale;

        // IMPORTANT : on laisse le GameObject ACTIF pour permettre à glTFast (importeur des .glb)
        // de charger la géométrie de façon asynchrone dans Start(). Faire SetActive(false) ici
        // bloquerait le chargement et le mesh n'apparaîtrait jamais.
        // La visibilité est ensuite pilotée via Renderer.enabled une fois le modèle chargé.
        sprayModelInstance.SetActive(true);

        // Cherche déjà les Renderers (cas modèle non-async : .fbx, .obj…)
        CollectSprayRenderersNow();

        if (!sprayModelLoaded)
        {
            // Aucun renderer immédiat → glTFast (ou équivalent async) en cours de chargement.
            StartCoroutine(WaitForSprayModelLoadCo());
        }
        else
        {
            // Renderers présents tout de suite, applique la visibilité voulue.
            ApplySprayRendererVisibility(sprayVisualActive);
        }
    }

    private void CollectSprayRenderersNow()
    {
        sprayModelRenderers.Clear();
        if (sprayModelInstance == null) return;
        var rs = sprayModelInstance.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        sprayModelRenderers.AddRange(rs);
        sprayModelLoaded = true;

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Debug.Log($"[SprayPainter] SprayModel chargé : {rs.Length} renderer(s), bounds world size={b.size} (sprayScale={sprayScale}).", this);
    }

    private IEnumerator WaitForSprayModelLoadCo()
    {
        const float timeout = 6f;
        float t = 0f;
        while (t < timeout)
        {
            if (sprayModelInstance == null) yield break;
            var rs = sprayModelInstance.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                sprayModelRenderers.Clear();
                sprayModelRenderers.AddRange(rs);
                sprayModelLoaded = true;

                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                Debug.Log($"[SprayPainter] SprayModel chargé (async, après {t:F2}s) : {rs.Length} renderer(s), bounds world size={b.size}.", this);

                // Applique la visibilité courante (la manette est peut-être déjà masquée).
                ApplySprayRendererVisibility(sprayVisualActive);
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning("[SprayPainter] SprayModel : aucun Renderer après 6s. Vérifie que Assets/Models/Spray.glb contient bien un mesh (glTFast l'importe en async).", this);
    }

    private void ApplySprayRendererVisibility(bool visible)
    {
        if (!sprayModelLoaded) return;
        foreach (var r in sprayModelRenderers)
            if (r != null) r.enabled = visible;
    }

    private void SetSprayVisualActive(bool active)
    {
        if (sprayVisualActive == active) return;

        // Sécurité : pas de modèle instancié = pas de swap. La coroutine de chargement
        // appliquera la visibilité une fois prête.
        bool hasSprayModel = sprayModelInstance != null;
        if (active && !hasSprayModel)
        {
            Debug.LogWarning("[SprayPainter] Demande d'afficher le spray mais aucun modèle instancié. Manette laissée visible.", this);
            return;
        }

        sprayVisualActive = active;

        // On NE FAIT PLUS sprayModelInstance.SetActive(false) : ça empêcherait glTFast
        // de finir son chargement async. La visibilité passe par Renderer.enabled.
        ApplySprayRendererVisibility(active);
        if (controllerVisualRoot != null) controllerVisualRoot.SetActive(!active);

        if (autoHideControllerRenderers && hasSprayModel) ApplyAutoHide(active);
    }

    private void ApplyAutoHide(bool spraying)
    {
        if (spraying)
        {
            // Par défaut on cherche sous le transform de SprayPainter (généralement le GameObject "Left Controller").
            // L'élargir au parent risquerait de masquer l'ensemble du XR rig.
            Transform root = controllerVisualSearchRoot != null ? controllerVisualSearchRoot : transform;

            // Restaure d'abord toute désactivation précédente au cas où.
            RestoreAutoHidden();

            var all = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in all)
            {
                if (r == null) continue;
                // Ne pas masquer le modèle Spray, le réticule, ni leur sous-arbre.
                if (sprayModelInstance != null && r.transform.IsChildOf(sprayModelInstance.transform)) continue;
                if (reticleInstance != null && r.transform.IsChildOf(reticleInstance.transform)) continue;

                autoHiddenRenderers[r] = r.enabled;
                r.enabled = false;
            }
        }
        else
        {
            RestoreAutoHidden();
        }
    }

    private void RestoreAutoHidden()
    {
        foreach (var kv in autoHiddenRenderers)
        {
            if (kv.Key != null) kv.Key.enabled = kv.Value;
        }
        autoHiddenRenderers.Clear();
    }

    private void SpawnReticle()
    {
        if (reticleInstance != null) return;

        reticleInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        reticleInstance.name = "SprayReticle";

        var col = reticleInstance.GetComponent<Collider>();
        if (col != null) Destroy(col);

        reticleRenderer = reticleInstance.GetComponent<MeshRenderer>();
        reticleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        reticleRenderer.receiveShadows = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", reticleColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", reticleColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", reticleColor * reticleEmissionIntensity);
        }
        reticleRenderer.sharedMaterial = mat;

        reticleInstance.transform.localScale = Vector3.one * reticleSize;
        reticleInstance.SetActive(false);
    }

    private void UpdateReticle(bool hasHit, Vector3 point, Vector3 normal, bool menuOpen)
    {
        if (reticleInstance == null) return;

        bool visible = showReticle && hasHit && !menuOpen;
        if (reticleInstance.activeSelf != visible) reticleInstance.SetActive(visible);
        if (!visible) return;

        // Léger décalage vers la caméra pour éviter le z-fighting avec la surface.
        reticleInstance.transform.position = point + normal * (reticleSize * 0.6f);
        reticleInstance.transform.localScale = Vector3.one * reticleSize;
    }

    private void Update()
    {
        bool pressed = sprayAction != null && sprayAction.action != null && sprayAction.action.IsPressed();
        bool menuOpen = GameManager.Instance != null && GameManager.Instance.IsMenuOpen;

        // Visuel : mode gâchette = on suit l'état d'appui (suspendu quand menu ouvert).
        // Mode permanent = on masque le spray pendant le menu pour pouvoir cliquer normalement.
        if (swapOnlyWhileSpraying) SetSprayVisualActive(pressed && !menuOpen);
        else SetSprayVisualActive(!menuOpen);

        // Raycast en continu pour piloter le réticule de visée.
        bool hasHit = Physics.Raycast(transform.position, transform.forward,
            out RaycastHit hit, maxDistance, paintableLayers, QueryTriggerInteraction.Ignore);

        bool validHit = hasHit && (!ignoreTagValid || !hit.collider.CompareTag(ignoreTag));
        UpdateReticle(validHit, validHit ? hit.point : Vector3.zero, validHit ? hit.normal : Vector3.up, menuOpen);

        if (sprayAction == null || sprayAction.action == null) return;
        if (!pressed) return;

        if (menuOpen) return;

        float interval = 1f / Mathf.Max(1f, spraysPerSecond);
        if (Time.time - lastSprayTime < interval) return;

        if (!validHit) return;

        lastSprayTime = Time.time;
        SpawnStamp(hit);
    }

    private void SpawnStamp(RaycastHit hit)
    {
        float distance = hit.distance;
        float sizeFactor = ComputeSizeFactor(distance);
        float opacity = ComputeOpacity(distance);

        GameObject stamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stamp.name = "PaintStamp";

        var col = stamp.GetComponent<Collider>();
        if (col != null) Destroy(col);

        if (ignoreTagValid) stamp.tag = ignoreTag;

        stampCounter++;
        float depthOffset = 0.0008f + (stampCounter % 200) * 0.00004f;

        stamp.transform.SetParent(stampsParent, true);
        stamp.transform.position = hit.point + hit.normal * depthOffset;
        stamp.transform.rotation = Quaternion.LookRotation(-hit.normal);
        stamp.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);

        float size = stampSize * sizeFactor * (1f + Random.Range(-sizeJitter, sizeJitter));
        stamp.transform.localScale = new Vector3(size, size, 1f);

        var mr = stamp.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Material mat = paintBaseMaterial != null
            ? new Material(paintBaseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        if (brushes != null && brushes.Length > 0)
        {
            int idx = Mathf.Clamp(brushIndex, 0, brushes.Length - 1);
            Texture2D tex = brushes[idx];
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
        }

        Color paintCol = currentColor;
        paintCol.a *= opacity;
        mat.color = paintCol;
        mr.sharedMaterial = mat;
    }

    private float ComputeSizeFactor(float distance)
    {
        if (distance <= idealDistance)
        {
            float t = Mathf.InverseLerp(closeDistance, idealDistance, distance);
            return Mathf.Lerp(closeSizeFactor, 1f, t);
        }
        float u = Mathf.InverseLerp(idealDistance, farDistance, distance);
        return Mathf.Lerp(1f, farSizeFactor, u);
    }

    private float ComputeOpacity(float distance)
    {
        if (distance <= idealDistance)
        {
            return 1f;
        }
        float u = Mathf.InverseLerp(idealDistance, farDistance, distance);
        return Mathf.Lerp(1f, farOpacity, u);
    }

    private void OnCycleBrush(InputAction.CallbackContext ctx) => SetBrushIndex(brushIndex + 1);

    // ────────────────────────────────────────────────────────────────────
    //  API publique (utilisée par PaintMenuUI)
    // ────────────────────────────────────────────────────────────────────

    public void SetBrushIndex(int index)
    {
        if (brushes == null || brushes.Length == 0) { brushIndex = 0; return; }
        brushIndex = ((index % brushes.Length) + brushes.Length) % brushes.Length;
    }

    public void SetColor(Color c) => currentColor = c;
    public void SetStampSize(float size) => stampSize = Mathf.Clamp(size, 0.05f, 2f);
}
