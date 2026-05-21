using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum FrameStyle { Black = 0, White = 1, Gold = 2, Wood = 3, None = 4 }

/// <summary>
/// Élément média placeable : image (Texture2D) OU vidéo (VideoClip).
/// </summary>
[System.Serializable]
public class MediaItem
{
    public enum Kind { Image, Video }
    public Kind kind;
    public string displayName;
    public Texture2D image;
    public VideoClip video;
}

/// <summary>
/// Placeur de tableaux/écrans VR. Raycast depuis ce GameObject (main gauche) ;
/// au déclenchement, instancie un cadre + média (image ou vidéo avec son spatial)
/// sur la surface visée. Le menu pilote la sélection (média, cadre, taille).
/// </summary>
public class PicturePlacer : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference placeAction;
    [SerializeField] private InputActionReference cyclePictureAction;

    [Header("Dimensions (modifiables au runtime)")]
    [SerializeField] private float pictureWidth = 0.8f;
    [SerializeField] private float frameThickness = 0.04f;
    [SerializeField] private float frameDepth = 0.025f;

    [Header("Placement")]
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float placeCooldown = 0.25f;

    [Header("Cadres (un matériau par FrameStyle)")]
    [Tooltip("Index 0=Black, 1=White, 2=Gold, 3=Wood, 4=None")]
    [SerializeField] private Material[] frameMaterials;

    [Header("Style")]
    [SerializeField] private Material pictureBaseMaterial;

    [Header("Filtre")]
    [SerializeField] private LayerMask placeableLayers = ~0;
    [SerializeField] private string ignoreTag = "Tableau";

    [Header("Murs")]
    [Tooltip("Si vrai, refuse les surfaces dont la normale n'est pas mostly horizontale (sol/plafond).")]
    [SerializeField] private bool wallsOnly = true;
    [Tooltip("Seuil |normal.y| pour considérer une surface comme un mur (0 = parfaitement vertical).")]
    [SerializeField, Range(0f, 1f)] private float wallNormalThreshold = 0.4f;

    private MediaItem[] mediaItems;
    private int mediaIndex;
    private FrameStyle currentFrame = FrameStyle.Black;
    private float lastPlaceTime;
    private Transform tableauxParent;
    public Transform TableauxParent => tableauxParent;

    public MediaItem[] Media => mediaItems;
    public int MediaIndex => mediaIndex;
    public FrameStyle CurrentFrame => currentFrame;
    public float PictureWidth => pictureWidth;

    private void Awake()
    {
        LoadMedia();
        var existing = GameObject.Find("Tableaux");
        tableauxParent = existing != null ? existing.transform : new GameObject("Tableaux").transform;
    }

    private void LoadMedia()
    {
        // Source unique : Resources/tableaux/ (images + vidéos détectées par type).
        var imgs = Resources.LoadAll<Texture2D>("tableaux");
        var vids = Resources.LoadAll<VideoClip>("tableaux");

        System.Array.Sort(imgs, (a, b) => string.Compare(a.name, b.name));
        System.Array.Sort(vids, (a, b) => string.Compare(a.name, b.name));

        int n = imgs.Length + vids.Length;
        mediaItems = new MediaItem[n];
        int k = 0;
        foreach (var t in imgs)
            mediaItems[k++] = new MediaItem { kind = MediaItem.Kind.Image, image = t, displayName = t.name };
        foreach (var v in vids)
            mediaItems[k++] = new MediaItem { kind = MediaItem.Kind.Video, video = v, displayName = v.name };

        Debug.Log($"[PicturePlacer] Resources/tableaux/ : {imgs.Length} image(s) + {vids.Length} vidéo(s) chargées");
    }

    private void OnEnable()
    {
        if (placeAction != null && placeAction.action != null)
        {
            placeAction.action.Enable();
            placeAction.action.performed += OnPlace;
        }
        if (cyclePictureAction != null && cyclePictureAction.action != null)
        {
            cyclePictureAction.action.Enable();
            cyclePictureAction.action.performed += OnCyclePicture;
        }
    }

    private void OnDisable()
    {
        if (placeAction != null && placeAction.action != null)
        {
            placeAction.action.performed -= OnPlace;
            placeAction.action.Disable();
        }
        if (cyclePictureAction != null && cyclePictureAction.action != null)
        {
            cyclePictureAction.action.performed -= OnCyclePicture;
            cyclePictureAction.action.Disable();
        }
    }

    private void OnPlace(InputAction.CallbackContext ctx)
    {
        if (mediaItems == null || mediaItems.Length == 0) return;
        if (Time.time - lastPlaceTime < placeCooldown) return;

        // Quand le menu studio est ouvert, le placement passe par le drag-and-drop
        // de l'onglet TABLEAUX (qui appelle SpawnAt directement). On évite le
        // double spawn déclenché par la même gâchette.
        if (GameManager.Instance != null && GameManager.Instance.IsMenuOpen) return;

        if (!Physics.Raycast(transform.position, transform.forward,
                out RaycastHit hit, maxDistance, placeableLayers, QueryTriggerInteraction.Ignore))
            return;

        if (!string.IsNullOrEmpty(ignoreTag) && hit.collider.CompareTag(ignoreTag))
            return;

        if (wallsOnly && !IsWall(hit.normal)) return;

        lastPlaceTime = Time.time;
        SpawnTableau(hit, mediaItems[mediaIndex]);
    }

    public bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < wallNormalThreshold;

    /// <summary>
    /// Spawne un tableau au point/normale donnés. Renvoie le GameObject racine,
    /// ou null si le placement est refusé (mur uniquement et surface non valide).
    /// Utilisé par le drag-and-drop du menu.
    /// </summary>
    public GameObject SpawnAt(MediaItem item, Vector3 position, Vector3 normal)
    {
        if (item == null) return null;
        if (wallsOnly && !IsWall(normal)) return null;
        var go = SpawnTableauInternal(item, position, normal);
        if (go != null)
        {
            var snapper = go.GetComponent<TableauGrabSnapper>();
            if (snapper != null) snapper.InitAnchor(go.transform.position, go.transform.rotation);
        }
        return go;
    }

    /// <summary>
    /// Spawne un tableau sans vérification de mur — utilisé par le drag-and-drop
    /// pour faire apparaître le tableau dans la main du joueur. Le snap au mur
    /// est laissé au menu / au TableauGrabSnapper lors du release.
    /// </summary>
    public GameObject SpawnFloating(MediaItem item, Vector3 position, Vector3 normal)
    {
        if (item == null) return null;
        return SpawnTableauInternal(item, position, normal);
    }

    private void SpawnTableau(RaycastHit hit, MediaItem item)
    {
        SpawnTableauInternal(item, hit.point, hit.normal);
    }

    private GameObject SpawnTableauInternal(MediaItem item, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Ratio basé sur image OU vidéo
        float aspect = 1f;
        if (item.kind == MediaItem.Kind.Image && item.image != null && item.image.height > 0)
            aspect = (float)item.image.width / item.image.height;
        else if (item.kind == MediaItem.Kind.Video && item.video != null && item.video.height > 0)
            aspect = (float)item.video.width / item.video.height;

        float w = pictureWidth;
        float h = pictureWidth / Mathf.Max(0.01f, aspect);

        Vector3 up = Mathf.Abs(Vector3.Dot(hitNormal, Vector3.up)) > 0.99f
            ? Vector3.forward : Vector3.up;

        GameObject tableau = new GameObject($"Tableau_{item.displayName}");
        try { tableau.tag = ignoreTag; } catch { }
        tableau.transform.SetParent(tableauxParent, true);
        tableau.transform.position = hitPoint + hitNormal * 0.002f;
        tableau.transform.rotation = Quaternion.LookRotation(hitNormal, up);

        // Cadre (sauf style None)
        if (currentFrame != FrameStyle.None && frameThickness > 0.001f)
        {
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            var fc = frame.GetComponent<Collider>();
            if (fc != null) Destroy(fc);
            frame.transform.SetParent(tableau.transform, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(
                w + frameThickness * 2f,
                h + frameThickness * 2f,
                frameDepth);

            var fmr = frame.GetComponent<MeshRenderer>();
            fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fmr.sharedMaterial = GetFrameMaterial(currentFrame);
        }

        // Surface média
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = item.kind == MediaItem.Kind.Video ? "Screen" : "Picture";
        var qc = quad.GetComponent<Collider>();
        if (qc != null) Destroy(qc);
        quad.transform.SetParent(tableau.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, frameDepth * 0.5f + 0.008f);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(w, h, 1f);

        var qmr = quad.GetComponent<MeshRenderer>();
        qmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        qmr.receiveShadows = false;

        // Matériau frais : on évite de cloner pictureBaseMaterial qui peut perdre
        // ses keywords URP au clonage (rendu noir).
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Standard");
        Material mat = new Material(unlit);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     Color.white);
        // Le Quad d'Unity a sa normale en -Z : sans désactiver le culling,
        // la face visible se retrouve souvent du mauvais côté → tableau noir.
        if (mat.HasProperty("_Cull"))      mat.SetFloat("_Cull", 0f);
        mat.color = Color.white;

        if (item.kind == MediaItem.Kind.Image)
        {
            ApplyTexture(mat, item.image);
            qmr.sharedMaterial = mat;
        }
        else if (item.kind == MediaItem.Kind.Video && item.video != null)
        {
            // RenderTexture pour la vidéo
            int rtW = Mathf.Max(64, (int)item.video.width);
            int rtH = Mathf.Max(64, (int)item.video.height);
            var rt = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
            rt.name = $"RT_{item.displayName}";
            rt.Create();

            ApplyTexture(mat, rt);
            qmr.sharedMaterial = mat;

            // VideoPlayer + AudioSource spatial
            var vp = tableau.AddComponent<VideoPlayer>();
            vp.playOnAwake = true;
            vp.isLooping = true;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.clip = item.video;

            var aud = tableau.AddComponent<AudioSource>();
            aud.playOnAwake = false;
            aud.loop = true;
            aud.spatialBlend = 1f;
            aud.rolloffMode = AudioRolloffMode.Linear;
            aud.minDistance = 1f;
            aud.maxDistance = 15f;

            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.SetTargetAudioSource(0, aud);
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.Play();
        }

        AttachGrabComponents(tableau, w, h);
        return tableau;
    }

    private void AttachGrabComponents(GameObject tableau, float w, float h)
    {
        // BoxCollider qui englobe le cadre — nécessaire pour le grab et le snap.
        var box = tableau.AddComponent<BoxCollider>();
        box.size = new Vector3(w + frameThickness * 2f, h + frameThickness * 2f, Mathf.Max(0.02f, frameDepth));
        box.center = Vector3.zero;

        var rb = tableau.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = tableau.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.smoothPosition = true;
        grab.smoothPositionAmount = 12f;
        grab.tightenPosition = 0.5f;
        grab.smoothRotation = true;
        grab.smoothRotationAmount = 12f;
        grab.tightenRotation = 0.5f;

        tableau.AddComponent<TableauGrabSnapper>();
        // L'ancre est définie par l'appelant (SpawnAt) ou plus tard lors du snap.
    }

    private void ApplyTexture(Material mat, Texture tex)
    {
        if (tex == null) return;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        mat.color = Color.white;
    }

    private Material GetFrameMaterial(FrameStyle style)
    {
        int idx = (int)style;
        if (frameMaterials != null && idx >= 0 && idx < frameMaterials.Length && frameMaterials[idx] != null)
            return frameMaterials[idx];
        // Fallback : URP Unlit (Lit nécessite une lumière, sinon rendu noir).
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color")
                  ?? Shader.Find("Standard");
        Color frameColor = style switch
        {
            FrameStyle.White => new Color(0.92f, 0.92f, 0.92f),
            FrameStyle.Gold  => new Color(0.85f, 0.68f, 0.22f),
            FrameStyle.Wood  => new Color(0.45f, 0.30f, 0.18f),
            FrameStyle.None  => new Color(0.20f, 0.18f, 0.28f),
            _                => new Color(0.10f, 0.10f, 0.10f),
        };
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", frameColor);
        if (m.HasProperty("_Color"))     m.SetColor("_Color",     frameColor);
        m.color = frameColor;
        return m;
    }

    private void OnCyclePicture(InputAction.CallbackContext ctx)
    {
        if (mediaItems == null || mediaItems.Length == 0) return;
        SetMediaIndex(mediaIndex + 1);
    }

    // ────────────────────────────────────────────────────────────────────
    //  API publique (menu)
    // ────────────────────────────────────────────────────────────────────

    public void SetMediaIndex(int index)
    {
        if (mediaItems == null || mediaItems.Length == 0) return;
        mediaIndex = ((index % mediaItems.Length) + mediaItems.Length) % mediaItems.Length;
        Debug.Log($"[PicturePlacer] Média : {mediaItems[mediaIndex].displayName}");
    }

    public void SetFrame(FrameStyle f) => currentFrame = f;
    public void SetPictureWidth(float w) => pictureWidth = Mathf.Clamp(w, 0.2f, 4f);
    public void SetFrameThickness(float t) => frameThickness = Mathf.Clamp(t, 0f, 0.2f);

    public void Reload() => LoadMedia();
}
