using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applique un préréglage d'ambiance nocturne (lampadaire ambre faible) à toutes les
/// lumières de la scène, y compris celles chargées au runtime par glTFast (.glb)
/// — re-scan continu activé par défaut.
///
/// Pose ce composant sur un GameObject vide. Ajuste les types ciblés et les couleurs
/// puis lance la scène, ou utilise le menu contextuel "Appliquer maintenant".
/// </summary>
public enum LightDimmerMode
{
    SetAbsolute,
    Multiply,
}

public class LightDimmer : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("SetAbsolute = remplace l'intensité par 'targetIntensity'. Multiply = multiplie par 'intensityFactor' (à utiliser une seule fois).")]
    [SerializeField] private LightDimmerMode mode = LightDimmerMode.SetAbsolute;

    [Header("Valeurs spots / point / area")]
    [Tooltip("Intensité absolue appliquée aux spots/point/area. 0.5–1.5 pour 'faible lampadaire'.")]
    [SerializeField] private float targetIntensity = 0.8f;

    [Tooltip("Multiplicateur d'intensité (mode Multiply). 1 = inchangé, 0.5 = moitié.")]
    [SerializeField, Range(0f, 2f)] private float intensityFactor = 0.5f;

    [Tooltip("Range max (m) appliqué aux spots/point. <=0 = ne pas modifier.")]
    [SerializeField] private float maxRange = 0f;

    [Tooltip("Angle max du cône des spots (°). <=0 = ne pas modifier.")]
    [SerializeField] private float maxSpotAngle = 0f;

    [Header("Couleur (ambiance nocturne)")]
    [Tooltip("Si vrai, force la couleur de chaque lumière (ambre 'lampadaire' par défaut).")]
    [SerializeField] private bool applyColor = true;

    [Tooltip("Couleur appliquée à chaque lumière. Défaut : ambre sodium ~2000K.")]
    [SerializeField] private Color lampColor = new Color(1f, 0.65f, 0.25f);

    [Tooltip("Force useColorTemperature=false sur chaque lumière (sinon la couleur custom est ignorée par URP).")]
    [SerializeField] private bool disableColorTemperature = true;

    [Tooltip("Si vrai, force l'éclairage ambiant (RenderSettings) à un bleu nuit faible.")]
    [SerializeField] private bool overrideAmbient = true;

    [SerializeField] private Color ambientNightColor = new Color(0.04f, 0.05f, 0.09f);

    [Header("Types ciblés")]
    [Tooltip("Les lampadaires sont des Spot. Coche les autres si besoin.")]
    [SerializeField] private bool includeDirectional = false;
    [SerializeField] private bool includePoint = false;
    [SerializeField] private bool includeSpot = true;
    [SerializeField] private bool includeArea = false;

    [Header("Override Directional (clair de lune)")]
    [SerializeField] private float directionalIntensity = 0.08f;
    [SerializeField] private Color directionalColor = new Color(0.55f, 0.65f, 0.95f);

    [Header("Model ciblé (cityv5)")]
    [Tooltip("Racine du model dans la scène (drag le GameObject cityv5 ici). TOUTES les Light et TOUS les matériaux émissifs sous cette racine seront remplacés par l'ambiance nocturne, sans filtre. Position des spots conservée.")]
    [SerializeField] private Transform modelRoot;

    [Tooltip("Si vrai, force toutes les Light du modelRoot à devenir des Spot (type uniforme). Position/rotation préservées.")]
    [SerializeField] private bool forceModelLightsToSpot = true;

    [Tooltip("Range (m) appliqué aux Light du modelRoot.")]
    [SerializeField] private float modelLightRange = 12f;

    [Tooltip("Angle du cône (°) appliqué aux Spot du modelRoot.")]
    [SerializeField] private float modelLightSpotAngle = 90f;

    [Tooltip("Angle interne (°) appliqué aux Spot du modelRoot.")]
    [SerializeField] private float modelLightInnerSpotAngle = 40f;

    [Header("Matériaux émissifs (ampoules)")]
    [Tooltip("Si vrai, ramène aussi les matériaux émissifs du modelRoot à une teinte ambre faible.")]
    [SerializeField] private bool tameEmissiveMaterials = true;

    [Tooltip("Couleur émissive cible.")]
    [SerializeField] private Color emissiveColor = new Color(1f, 0.65f, 0.25f);

    [Tooltip("Intensité émissive (HDR). 0 = éteint, 1 = ampoule chaude, 3 = vif.")]
    [SerializeField, Range(0f, 8f)] private float emissiveIntensity = 0.8f;

    [Header("Options")]
    [Tooltip("Inclut aussi les lumières sur GameObjects désactivés.")]
    [SerializeField] private bool includeInactive = true;

    [Tooltip("Re-scan chaque frame pour attraper les lumières chargées au runtime (glTFast importe les .glb async). FORTEMENT RECOMMANDÉ avec ce projet.")]
    [SerializeField] private bool reapplyEveryFrame = true;

    [Header("Log")]
    [SerializeField] private bool verbose = true;
    [SerializeField] private bool logEachLightOnFirstApply = true;

    // Anti-spam log : on n'imprime "First apply" qu'une fois par instance de Light.
    private readonly HashSet<int> alreadyLoggedLights = new HashSet<int>();
    private readonly HashSet<int> alreadyTamedMaterials = new HashSet<int>();
    private int lastFrameLightCount = -1;

    private void Start()
    {
        ApplyDimming();
    }

    private void Update()
    {
        if (reapplyEveryFrame) ApplyDimming();
    }

    [ContextMenu("Appliquer maintenant")]
    public void ApplyDimming()
    {
        Light[] lights = FindObjectsByType<Light>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int dimmed = 0;
        int newlySeen = 0;
        foreach (var l in lights)
        {
            if (l == null) continue;

            // Si un modelRoot est assigné, on FORCE le traitement des Lights qui en font partie,
            // peu importe leur type (Spot/Point/Area). Hors modelRoot, on suit les types ciblés.
            bool inModelRoot = modelRoot != null && l.transform.IsChildOf(modelRoot);
            if (!inModelRoot && !ShouldInclude(l.type)) continue;

            int id = l.GetInstanceID();
            bool firstTime = !alreadyLoggedLights.Contains(id);
            if (firstTime)
            {
                alreadyLoggedLights.Add(id);
                newlySeen++;
                if (logEachLightOnFirstApply && verbose)
                {
                    Debug.Log($"[LightDimmer] Détecté : {l.name} (type={l.type}, intensité avant={l.intensity:F2}, color={l.color}, useTemp={l.useColorTemperature}, inModel={inModelRoot})", l);
                }
            }

            if (disableColorTemperature) l.useColorTemperature = false;

            if (inModelRoot)
            {
                // Remplacement complet : on garde la position/rotation et on force tous les paramètres ambiance lampadaire.
                if (forceModelLightsToSpot) l.type = LightType.Spot;
                l.intensity = targetIntensity;
                l.color = lampColor;
                l.range = modelLightRange;
                if (l.type == LightType.Spot)
                {
                    l.spotAngle = modelLightSpotAngle;
                    l.innerSpotAngle = modelLightInnerSpotAngle;
                }
            }
            else
            {
                bool isDirectional = l.type == LightType.Directional;
                if (isDirectional)
                {
                    l.intensity = directionalIntensity;
                    if (applyColor) l.color = directionalColor;
                }
                else
                {
                    if (mode == LightDimmerMode.SetAbsolute) l.intensity = targetIntensity;
                    else if (firstTime) l.intensity *= intensityFactor;

                    if (applyColor) l.color = lampColor;

                    if (maxRange > 0f && l.range > maxRange) l.range = maxRange;
                    if (maxSpotAngle > 0f && l.type == LightType.Spot && l.spotAngle > maxSpotAngle)
                        l.spotAngle = maxSpotAngle;
                }
            }
            dimmed++;
        }

        if (overrideAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientNightColor;
        }

        if (tameEmissiveMaterials) TameEmissiveMaterials();

        if (verbose && (lights.Length != lastFrameLightCount || newlySeen > 0))
        {
            string desc = mode == LightDimmerMode.SetAbsolute
                ? $"intensité={targetIntensity}"
                : $"x{intensityFactor}";
            Debug.Log($"[LightDimmer] {dimmed}/{lights.Length} lumière(s) ajustée(s) ({desc}, ambre={applyColor}, +{newlySeen} nouvelle(s)).", this);
            lastFrameLightCount = lights.Length;
        }
    }

    private void TameEmissiveMaterials()
    {
        if (modelRoot == null)
        {
            // Sans cible, ne touche pas tous les matériaux de la scène (paint stamps, UI…).
            return;
        }

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        Color emissionFinal = emissiveColor * emissiveIntensity;
        int touched = 0;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!m.HasProperty("_EmissionColor")) continue;

                int id = m.GetInstanceID();
                bool firstTime = !alreadyTamedMaterials.Contains(id);
                if (!firstTime) continue;

                Color before = m.GetColor("_EmissionColor");
                // Ne touche que les matériaux réellement émissifs (≠ noir).
                if (before.maxColorComponent < 0.001f) { alreadyTamedMaterials.Add(id); continue; }

                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emissionFinal);
                alreadyTamedMaterials.Add(id);
                touched++;

                if (verbose) Debug.Log($"[LightDimmer] Émissif calmé : {r.name} / {m.name} (avant={before}, après={emissionFinal})", r);
            }
        }
        if (verbose && touched > 0)
            Debug.Log($"[LightDimmer] {touched} matériau(x) émissif(s) ajusté(s) sous {modelRoot.name}.", this);
    }

    private bool ShouldInclude(LightType type)
    {
        switch (type)
        {
            case LightType.Directional: return includeDirectional;
            case LightType.Point: return includePoint;
            case LightType.Spot: return includeSpot;
            case LightType.Rectangle:
            case LightType.Disc:
                return includeArea;
            default: return true;
        }
    }
}
