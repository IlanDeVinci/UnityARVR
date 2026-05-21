using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Menu créatif VR à 4 onglets : GRAFFITI, OBJETS, TABLEAUX, MUSIQUE.
/// Pilote SprayPainter, ObjectSpawner, PicturePlacer et MusicSpawner.
/// </summary>
public class PaintMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private SprayPainter sprayPainter;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private PicturePlacer picturePlacer;
    [SerializeField] private MusicSpawner musicSpawner;

    [Header("Input")]
    [SerializeField] private InputActionReference menuToggleAction;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(960f, 900f);

    [Header("Graffiti — Palette")]
    [SerializeField] private Color[] palette = new[]
    {
        new Color(0.95f, 0.15f, 0.15f), new Color(1f,    0.45f, 0.10f),
        new Color(1f,    0.85f, 0.15f), new Color(0.45f, 0.85f, 0.20f),
        new Color(0.10f, 0.65f, 0.30f), new Color(0.15f, 0.85f, 0.95f),
        new Color(0.15f, 0.30f, 0.95f), new Color(0.55f, 0.20f, 0.95f),
        new Color(0.95f, 0.30f, 0.85f), new Color(1f,    1f,    1f),
        new Color(0.55f, 0.40f, 0.30f), new Color(0.05f, 0.05f, 0.05f),
    };

    [Header("Graffiti — Sizes")]
    [SerializeField] private float minSize = 0.05f;
    [SerializeField] private float maxSize = 1.5f;

    [Header("Graffiti — Favorites")]
    [SerializeField] private int favoritesCount = 6;
    [SerializeField] private string playerPrefsKey = "PaintMenu_Favorites";

    [Header("Tableau — Tailles")]
    [SerializeField] private float minPictureWidth = 0.30f;
    [SerializeField] private float maxPictureWidth = 2.50f;

    // ─── style ──────────────────────────────────────────────────────
    private static readonly Color BgColor      = new Color(0.06f, 0.04f, 0.10f, 0.97f);
    private static readonly Color CardColor    = new Color(0.10f, 0.08f, 0.16f, 1f);
    private static readonly Color CardActive   = new Color(0.95f, 0.65f, 0.15f, 1f);
    private static readonly Color TabIdle      = new Color(0.12f, 0.10f, 0.18f, 1f);
    private static readonly Color TabActive    = new Color(0.95f, 0.65f, 0.15f, 1f);
    private static readonly Color BorderClr    = new Color(0.95f, 0.75f, 0.25f, 0.6f);
    private static readonly Color TitleClr     = new Color(1f, 0.85f, 0.30f);
    private static readonly Color LabelClr     = new Color(0.78f, 0.78f, 0.85f);
    private static readonly Color SubLabelClr  = new Color(0.55f, 0.55f, 0.65f);
    private static readonly Color ButtonGreen  = new Color(0.20f, 0.65f, 0.35f);
    private static readonly Color ButtonGreenH = new Color(0.30f, 0.78f, 0.45f);

    // ─── tabs ──────────────────────────────────────────────────────
    private static readonly string[] TabNames = { "GRAFFITI", "OBJETS", "TABLEAUX", "MUSIQUE" };
    private GameObject[] tabPanels;
    private Image[] tabButtonBgs;
    private TMP_Text[] tabButtonLabels;
    private int activeTab = 0;

    // ─── graffiti state ─────────────────────────────────────────────
    private Image colorPreviewImg;
    private TMP_Text colorPreviewHex;
    private TMP_Text brushNameLabel;
    private TMP_Text sizeLabel;
    private Slider sizeSlider;
    private Image[] brushBackgrounds;
    private Image[] favoriteSwatches;
    private Color[] favoriteColors;
    private Color currentColor = Color.red;

    // ─── tableau state ──────────────────────────────────────────────
    private TMP_Text mediaNameLabel;
    private Image[] frameStyleBgs;
    private FrameStyle currentFrameStyle = FrameStyle.Black;
    private Slider picWidthSlider;
    private TMP_Text picWidthLabel;
    private Image[] mediaButtonBgs;
    private int selectedMediaIndex = -1;
    private MediaItem[] tableauxCache;

    // ─── drag-and-drop tableaux ─────────────────────────────────────
    [Header("Tableau — Drag & Drop")]
    [Tooltip("Distance entre la main et le tableau pendant le drag (m).")]
    [SerializeField] private float dragHoldDistance = 0.20f;
    [Tooltip("Facteur de taille appliqué pendant le drag (1 = taille réelle).")]
    [SerializeField, Range(0.05f, 1f)] private float dragHoldScale = 0.30f;
    [Tooltip("Distance max du raycast de snap au release (m).")]
    [SerializeField] private float dragMaxDistance = 2.5f;
    [SerializeField, Range(0f, 1f)] private float dragWallNormalThreshold = 0.4f;
    [SerializeField] private LayerMask dragRaycastLayers = ~0;

    private bool isDraggingTableau;
    private int draggingIndex = -1;
    private XRRayInteractor draggingInteractor;
    private GameObject draggedTableau;

    // ─── musique state ──────────────────────────────────────────────
    private Image[] musicTrackBgs;
    private TMP_Text musicNameLabel;
    private Button musicPlaceButton;
    private int selectedMusicIndex = -1;

    // ─── objets state ───────────────────────────────────────────────
    private Image[] objectButtonBgs;
    private TMP_Text objectNameLabel;
    private Button objectPlaceButton;
    private int selectedObjectIndex = -1;

    // ────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        favoriteColors = new Color[favoritesCount];
        LoadFavorites();
    }

    private void OnEnable()
    {
        if (menuToggleAction != null && menuToggleAction.action != null)
        {
            menuToggleAction.action.Enable();
            menuToggleAction.action.performed += OnToggleMenu;
        }
    }

    private void OnDisable()
    {
        if (menuToggleAction != null && menuToggleAction.action != null)
        {
            menuToggleAction.action.performed -= OnToggleMenu;
            menuToggleAction.action.Disable();
        }

        // Filet de sécurité : si on désactive le menu en plein drag, on annule.
        if (isDraggingTableau) CancelDrag();
    }

    private void Start()
    {
        if (sprayPainter   == null) sprayPainter   = FindAnyObjectByType<SprayPainter>();
        if (objectSpawner  == null) objectSpawner  = FindAnyObjectByType<ObjectSpawner>();
        if (picturePlacer  == null) picturePlacer  = FindAnyObjectByType<PicturePlacer>();
        if (musicSpawner   == null) musicSpawner   = FindAnyObjectByType<MusicSpawner>();

        if (menuPanel == null) BuildPanelFromScratch();
        else PrepareExistingPanel();

        BuildHeader();
        BuildTabBar();
        BuildTabPanels();

        SetActiveTab(0);

        // Sync graffiti depuis SprayPainter
        if (sprayPainter != null)
        {
            currentColor = sprayPainter.CurrentColor;
            if (sizeSlider != null) sizeSlider.value = sprayPainter.StampSize;
            HighlightBrush(sprayPainter.BrushIndex);
        }
        UpdateColorPreview();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Build root
    // ────────────────────────────────────────────────────────────────────

    private void PrepareExistingPanel()
    {
        for (int i = menuPanel.transform.childCount - 1; i >= 0; i--)
            Destroy(menuPanel.transform.GetChild(i).gameObject);

        var panelRT = menuPanel.GetComponent<RectTransform>();
        if (panelRT != null)
        {
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = panelSize;
        }

        var canvas = menuPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var canvasRT = canvas.GetComponent<RectTransform>();
            if (canvasRT != null)
            {
                canvasRT.sizeDelta = panelSize;
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    Vector3 s = canvasRT.localScale;
                    if (s.x < 0.0008f || s.x > 0.005f)
                        canvasRT.localScale = Vector3.one * 0.0022f;
                }
            }
        }

        var img = menuPanel.GetComponent<Image>();
        if (img == null) img = menuPanel.AddComponent<Image>();
        img.color = BgColor;

        var outline = menuPanel.GetComponent<Outline>();
        if (outline == null) outline = menuPanel.AddComponent<Outline>();
        outline.effectColor = BorderClr;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        menuPanel.SetActive(false);
    }

    private void BuildPanelFromScratch()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;

        if (GetComponent<CanvasScaler>() == null) gameObject.AddComponent<CanvasScaler>();
        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var gr = GetComponent<GraphicRaycaster>();
        if (gr != null && gr.GetType() == typeof(GraphicRaycaster)) Destroy(gr);

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = panelSize;

        menuPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        menuPanel.transform.SetParent(transform, false);
        var panelRT = menuPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        menuPanel.GetComponent<Image>().color = BgColor;
        var outline = menuPanel.AddComponent<Outline>();
        outline.effectColor = BorderClr;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        menuPanel.SetActive(false);
    }

    private void BuildHeader()
    {
        CreateText("Title", menuPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -34f), new Vector2(0f, 44f),
            "S T U D I O", 34, TitleClr,
            TextAlignmentOptions.Center, FontStyles.Bold);

        CreateText("Subtitle", menuPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -68f), new Vector2(0f, 22f),
            "graffiti · objets · tableaux · musique", 14, SubLabelClr,
            TextAlignmentOptions.Center, FontStyles.Italic);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Tab bar
    // ────────────────────────────────────────────────────────────────────

    private void BuildTabBar()
    {
        var bar = new GameObject("TabBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(menuPanel.transform, false);
        var bRT = bar.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot     = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -90f);
        bRT.sizeDelta = new Vector2(0f, 56f);
        bar.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.07f, 0.95f);

        tabButtonBgs = new Image[TabNames.Length];
        tabButtonLabels = new TMP_Text[TabNames.Length];

        float pad = 16f;
        float gap = 8f;

        for (int i = 0; i < TabNames.Length; i++)
        {
            int idx = i;
            var btn = new GameObject($"Tab_{TabNames[i]}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(bar.transform, false);
            var bRT2 = btn.GetComponent<RectTransform>();
            float frac = 1f / TabNames.Length;
            bRT2.anchorMin = new Vector2(frac * i, 0f);
            bRT2.anchorMax = new Vector2(frac * (i + 1), 1f);
            bRT2.offsetMin = new Vector2(i == 0 ? pad : gap / 2f, 6f);
            bRT2.offsetMax = new Vector2(i == TabNames.Length - 1 ? -pad : -gap / 2f, -6f);

            var bg = btn.GetComponent<Image>();
            bg.color = TabIdle;
            tabButtonBgs[i] = bg;

            var b = btn.GetComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => SetActiveTab(idx));

            var labelObj = CreateText("Label", btn.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TabNames[i], 18, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var lRT = labelObj.GetComponent<RectTransform>();
            lRT.offsetMin = lRT.offsetMax = Vector2.zero;
            tabButtonLabels[i] = labelObj.GetComponent<TMP_Text>();
        }
    }

    private void SetActiveTab(int index)
    {
        activeTab = Mathf.Clamp(index, 0, TabNames.Length - 1);
        if (tabPanels != null)
        {
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null) tabPanels[i].SetActive(i == activeTab);
        }
        if (tabButtonBgs != null)
        {
            for (int i = 0; i < tabButtonBgs.Length; i++)
            {
                if (tabButtonBgs[i] != null) tabButtonBgs[i].color = (i == activeTab) ? TabActive : TabIdle;
                if (tabButtonLabels[i] != null)
                    tabButtonLabels[i].color = (i == activeTab) ? Color.black : Color.white;
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Tab content
    // ────────────────────────────────────────────────────────────────────

    private void BuildTabPanels()
    {
        tabPanels = new GameObject[TabNames.Length];

        for (int i = 0; i < TabNames.Length; i++)
        {
            var p = new GameObject($"TabPanel_{TabNames[i]}", typeof(RectTransform));
            p.transform.SetParent(menuPanel.transform, false);
            var rt = p.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(16f, 16f);
            rt.offsetMax = new Vector2(-16f, -150f); // header + tab bar
            tabPanels[i] = p;
        }

        BuildGraffitiTab(tabPanels[0].transform);
        BuildObjectsTab(tabPanels[1].transform);
        BuildTableauxTab(tabPanels[2].transform);
        BuildMusiqueTab(tabPanels[3].transform);
    }

    // ─── Graffiti ──────────────────────────────────────────────────

    private void BuildGraffitiTab(Transform root)
    {
        // 1. Aperçu couleur courante
        var preview = CreateCard("ColorCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 100f));

        var swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatch.transform.SetParent(preview.transform, false);
        var sRT = swatch.GetComponent<RectTransform>();
        sRT.anchorMin = sRT.anchorMax = new Vector2(0f, 0.5f);
        sRT.pivot = new Vector2(0f, 0.5f);
        sRT.anchoredPosition = new Vector2(16f, 0f);
        sRT.sizeDelta = new Vector2(80f, 80f);
        colorPreviewImg = swatch.GetComponent<Image>();
        colorPreviewImg.color = currentColor;
        swatch.AddComponent<Outline>().effectColor = new Color(0.95f, 0.75f, 0.25f, 0.8f);

        var t1 = CreateText("Lbl", preview.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(112f, -16f), new Vector2(280f, 24f),
            "COULEUR ACTIVE", 13, LabelClr,
            TextAlignmentOptions.Left, FontStyles.Bold);
        t1.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

        var hex = CreateText("Hex", preview.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(112f, -6f), new Vector2(260f, 30f),
            "#FF2626", 22, Color.white,
            TextAlignmentOptions.Left, FontStyles.Bold);
        hex.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        colorPreviewHex = hex.GetComponent<TMP_Text>();

        var bn = CreateText("BrushName", preview.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f), new Vector2(260f, 28f),
            "Pinceau : —", 16, LabelClr,
            TextAlignmentOptions.Right, FontStyles.Normal);
        bn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        brushNameLabel = bn.GetComponent<TMP_Text>();

        // 2. Pinceaux
        var brushCard = CreateCard("BrushCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -112f), new Vector2(0f, 130f));
        CreateSectionTitle(brushCard.transform, "PINCEAUX");

        Texture2D[] brushes = sprayPainter != null ? sprayPainter.Brushes : null;
        int nb = brushes != null ? brushes.Length : 0;
        if (nb > 0)
        {
            brushBackgrounds = new Image[nb];
            float padX = 16f, padY = 38f, gap = 10f;
            float availW = panelSize.x - 32f - padX * 2f;
            float cellW = Mathf.Min(96f, (availW - gap * (nb - 1)) / nb);
            for (int i = 0; i < nb; i++)
            {
                int idx = i;
                var b = new GameObject($"Brush_{i}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                b.transform.SetParent(brushCard.transform, false);
                var bRT = b.GetComponent<RectTransform>();
                bRT.anchorMin = bRT.anchorMax = new Vector2(0f, 1f);
                bRT.pivot = new Vector2(0f, 1f);
                bRT.anchoredPosition = new Vector2(padX + i * (cellW + gap), -padY);
                bRT.sizeDelta = new Vector2(cellW, 80f);
                var bg = b.GetComponent<Image>();
                bg.color = CardColor;
                brushBackgrounds[i] = bg;

                var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                icon.transform.SetParent(b.transform, false);
                var iRT = icon.GetComponent<RectTransform>();
                iRT.anchorMin = iRT.anchorMax = new Vector2(0.5f, 0.5f);
                iRT.anchoredPosition = Vector2.zero;
                iRT.sizeDelta = new Vector2(cellW * 0.68f, 80f * 0.68f);
                var raw = icon.GetComponent<RawImage>();
                raw.texture = brushes[i];
                raw.color = LabelClr;
                raw.raycastTarget = false;

                b.GetComponent<Button>().onClick.AddListener(() => OnSelectBrush(idx));
                b.GetComponent<Button>().targetGraphic = bg;
            }
        }

        // 3. Palette
        var palCard = CreateCard("PaletteCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -256f), new Vector2(0f, 150f));
        CreateSectionTitle(palCard.transform, "PALETTE");
        BuildPaletteSwatches(palCard.transform);

        // 4. Favoris
        var favCard = CreateCard("FavCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -418f), new Vector2(0f, 100f));
        CreateSectionTitle(favCard.transform, "FAVORIS");
        BuildFavorites(favCard.transform);

        // 5. Taille
        var sizeCard = CreateCard("SizeCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -530f), new Vector2(0f, 70f));
        CreateSectionTitle(sizeCard.transform, "TAILLE");
        BuildSizeSlider(sizeCard.transform);
    }

    private void BuildPaletteSwatches(Transform card)
    {
        if (palette == null || palette.Length == 0) return;
        int cols = 6;
        float padX = 16f, padY = 38f, gap = 8f;
        float availW = panelSize.x - 32f - padX * 2f;
        float cellW = (availW - gap * (cols - 1)) / cols;
        float cellH = 44f;

        for (int i = 0; i < palette.Length; i++)
        {
            int idx = i;
            int r = i / cols;
            int c = i % cols;
            var s = new GameObject($"Pal_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            s.transform.SetParent(card, false);
            var rt = s.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(padX + c * (cellW + gap), -padY - r * (cellH + gap));
            rt.sizeDelta = new Vector2(cellW, cellH);
            var im = s.GetComponent<Image>();
            im.color = palette[i];
            var b = s.GetComponent<Button>();
            b.targetGraphic = im;
            b.onClick.AddListener(() => OnSelectColor(palette[idx]));
        }
    }

    private void BuildFavorites(Transform card)
    {
        float padX = 16f, padY = 38f, gap = 12f;
        var add = new GameObject("AddFav", typeof(RectTransform), typeof(Image), typeof(Button));
        add.transform.SetParent(card, false);
        var aRT = add.GetComponent<RectTransform>();
        aRT.anchorMin = aRT.anchorMax = new Vector2(0f, 1f);
        aRT.pivot = new Vector2(0f, 1f);
        aRT.anchoredPosition = new Vector2(padX, -padY);
        aRT.sizeDelta = new Vector2(48f, 48f);
        add.GetComponent<Image>().color = ButtonGreen;
        add.AddComponent<Outline>().effectColor = new Color(0.95f, 0.75f, 0.25f, 0.6f);

        var plus = CreateText("Plus", add.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            "+", 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        plus.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        plus.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        add.GetComponent<Button>().onClick.AddListener(AddCurrentToFavorites);

        favoriteSwatches = new Image[favoritesCount];
        for (int i = 0; i < favoritesCount; i++)
        {
            int idx = i;
            var slot = new GameObject($"Fav_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(card, false);
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(padX + 48f + gap + i * (48f + gap), -padY);
            rt.sizeDelta = new Vector2(48f, 48f);
            var im = slot.GetComponent<Image>();
            im.color = favoriteColors[i].a > 0f ? favoriteColors[i] : new Color(0.15f, 0.12f, 0.20f, 1f);
            favoriteSwatches[i] = im;
            slot.GetComponent<Button>().onClick.AddListener(() => OnSelectFavorite(idx));
        }
    }

    private void BuildSizeSlider(Transform card)
    {
        var s = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        s.transform.SetParent(card, false);
        var rt = s.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(130f, -10f);
        rt.offsetMax = new Vector2(-110f, -4f);
        s.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.20f, 1f);

        sizeSlider = s.GetComponent<Slider>();
        sizeSlider.minValue = minSize;
        sizeSlider.maxValue = maxSize;
        sizeSlider.value = sprayPainter != null ? sprayPainter.StampSize : 0.35f;
        sizeSlider.targetGraphic = s.GetComponent<Image>();

        BuildSliderVisuals(sizeSlider, s.transform);
        sizeSlider.onValueChanged.AddListener(OnSizeChanged);

        sizeLabel = CreateText("Val", card,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-18f, 0f), new Vector2(90f, 28f),
            "0.35 m", 17, Color.white,
            TextAlignmentOptions.Right, FontStyles.Bold).GetComponent<TMP_Text>();
        sizeLabel.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
    }

    private static void BuildSliderVisuals(Slider slider, Transform sliderTr)
    {
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderTr, false);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = new Vector2(8f, 0f);
        faRT.offsetMax = new Vector2(-15f, 0f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.GetComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.95f, 0.65f, 0.15f);
        slider.fillRect = fRT;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderTr, false);
        var haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0f, 0f); haRT.anchorMax = new Vector2(1f, 1f);
        haRT.offsetMin = new Vector2(8f, 0f); haRT.offsetMax = new Vector2(-8f, 0f);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hRT = handle.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(22f, 34f);
        handle.GetComponent<Image>().color = Color.white;
        slider.handleRect = hRT;
    }

    // ─── Objects ────────────────────────────────────────────────────

    private void BuildObjectsTab(Transform root)
    {
        var card = CreateCard("ObjGridCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 540f));

        CreateSectionTitle(card.transform, "CHOISIS UN OBJET");

        // Label sélection
        objectNameLabel = CreateText("SelLbl", card.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -38f), new Vector2(0f, 24f),
            "Sélectionne un objet dans la liste", 14, SubLabelClr,
            TextAlignmentOptions.Center, FontStyles.Italic).GetComponent<TMP_Text>();

        SpawnableItem[] items = objectSpawner != null ? objectSpawner.SpawnableItems : null;
        if (items == null || items.Length == 0)
        {
            CreateText("Empty", card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(500f, 30f),
                "(Aucun objet dans Resources/Furniture/)", 14, SubLabelClr,
                TextAlignmentOptions.Center, FontStyles.Italic);
            return;
        }

        // Grille (scrollable si trop d'objets, sinon static)
        BuildScrollableGrid(card.transform, items.Length, 4, (i, parent) =>
        {
            BuildObjectCell(parent, i, items[i]);
        }, gridStartYOffset: -72f, gridEndYOffset: -70f);

        // Bouton PLACER
        var btnObj = new GameObject("PlaceObj", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(root, false);
        var bRT = btnObj.GetComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -560f);
        bRT.sizeDelta = new Vector2(280f, 60f);
        btnObj.GetComponent<Image>().color = new Color(0.30f, 0.30f, 0.30f);
        objectPlaceButton = btnObj.GetComponent<Button>();
        objectPlaceButton.interactable = false;
        objectPlaceButton.onClick.AddListener(OnPlaceObject);

        CreateText("T", btnObj.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, "PLACER", 26, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold)
            .GetComponent<RectTransform>().offsetMin = Vector2.zero;
    }

    private void BuildObjectCell(Transform parent, int index, SpawnableItem item)
    {
        var cell = new GameObject($"Obj_{item.name}",
            typeof(RectTransform), typeof(Image), typeof(Button));
        cell.transform.SetParent(parent, false);
        var im = cell.GetComponent<Image>();
        im.color = CardColor;
        if (objectButtonBgs == null || objectButtonBgs.Length <= index)
        {
            // Init on first call
            int n = objectSpawner != null ? objectSpawner.SpawnableItems.Length : 0;
            if (objectButtonBgs == null || objectButtonBgs.Length != n)
                objectButtonBgs = new Image[n];
        }
        objectButtonBgs[index] = im;

        // Preview 3D
        Texture2D tex = ModelPreviewGenerator.Generate(item.prefab, 128);
        if (tex != null)
        {
            var img = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(cell.transform, false);
            var iRT = img.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.05f, 0.25f);
            iRT.anchorMax = new Vector2(0.95f, 0.98f);
            iRT.offsetMin = iRT.offsetMax = Vector2.zero;
            var raw = img.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }

        // Name
        var name = CreateText("Name", cell.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0.22f),
            Vector2.zero, Vector2.zero,
            item.name, 11, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var nRT = name.GetComponent<RectTransform>();
        nRT.offsetMin = new Vector2(4f, 2f); nRT.offsetMax = new Vector2(-4f, -2f);
        name.GetComponent<TMP_Text>().enableWordWrapping = true;

        cell.GetComponent<Button>().targetGraphic = im;
        cell.GetComponent<Button>().onClick.AddListener(() => OnSelectObject(index));
    }

    // ─── Tableaux ───────────────────────────────────────────────────

    private void BuildTableauxTab(Transform root)
    {
        // Grille de médias
        var grid = CreateCard("MediaGrid", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 360f));
        CreateSectionTitle(grid.transform, "IMAGES & VIDÉOS");

        mediaNameLabel = CreateText("SelLbl", grid.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -36f), new Vector2(0f, 22f),
            "Sélectionne une image ou vidéo (Resources/tableaux)",
            13, SubLabelClr, TextAlignmentOptions.Center, FontStyles.Italic).GetComponent<TMP_Text>();

        tableauxCache = LoadTableauxMedia();
        Debug.Log($"[PaintMenuUI] Tableaux tab : {tableauxCache.Length} média(s) trouvé(s) (picturePlacer={(picturePlacer != null ? "OK" : "null")})");

        if (tableauxCache.Length == 0)
        {
            CreateText("Empty", grid.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600f, 30f),
                "(Aucun média dans Assets/Resources/tableaux/)", 14, SubLabelClr,
                TextAlignmentOptions.Center, FontStyles.Italic);
        }
        else
        {
            BuildScrollableGrid(grid.transform, tableauxCache.Length, 5, (i, parent) =>
                BuildMediaCell(parent, i, tableauxCache[i]),
                gridStartYOffset: -70f, gridEndYOffset: -10f);
        }

        // Style de cadre (5 choix)
        var frameCard = CreateCard("FrameCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -376f), new Vector2(0f, 110f));
        CreateSectionTitle(frameCard.transform, "CADRE");

        string[] frameLabels = { "NOIR", "BLANC", "DORÉ", "BOIS", "AUCUN" };
        Color[] framePreviewColors = {
            new Color(0.05f, 0.05f, 0.05f),
            new Color(0.92f, 0.92f, 0.92f),
            new Color(0.85f, 0.68f, 0.22f),
            new Color(0.45f, 0.30f, 0.18f),
            new Color(0.20f, 0.18f, 0.28f)
        };
        frameStyleBgs = new Image[5];
        float padX = 16f, gap = 12f;
        float availW = panelSize.x - 32f - padX * 2f;
        float cellW = (availW - gap * 4f) / 5f;

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var b = new GameObject($"Frame_{frameLabels[i]}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(frameCard.transform, false);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(padX + i * (cellW + gap), -38f);
            rt.sizeDelta = new Vector2(cellW, 60f);
            b.GetComponent<Image>().color = CardColor;
            frameStyleBgs[i] = b.GetComponent<Image>();

            var inner = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(b.transform, false);
            var pRT = inner.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0f, 0.35f);
            pRT.anchorMax = new Vector2(1f, 1f);
            pRT.offsetMin = new Vector2(8f, 4f);
            pRT.offsetMax = new Vector2(-8f, -2f);
            inner.GetComponent<Image>().color = framePreviewColors[i];
            inner.GetComponent<Image>().raycastTarget = false;

            CreateText("Lbl", b.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0.35f),
                Vector2.zero, Vector2.zero,
                frameLabels[i], 12, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold)
                .GetComponent<RectTransform>().offsetMin = new Vector2(0f, 2f);

            b.GetComponent<Button>().targetGraphic = b.GetComponent<Image>();
            b.GetComponent<Button>().onClick.AddListener(() => OnSelectFrame((FrameStyle)idx));
        }

        // Slider taille tableau
        var sizeCard = CreateCard("PicSizeCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -502f), new Vector2(0f, 70f));
        CreateSectionTitle(sizeCard.transform, "TAILLE (largeur en mètres)");

        var s = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        s.transform.SetParent(sizeCard.transform, false);
        var sRT = s.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.5f);
        sRT.anchorMax = new Vector2(1f, 0.5f);
        sRT.pivot = new Vector2(0.5f, 0.5f);
        sRT.offsetMin = new Vector2(220f, -10f);
        sRT.offsetMax = new Vector2(-110f, -4f);
        s.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.20f, 1f);

        picWidthSlider = s.GetComponent<Slider>();
        picWidthSlider.minValue = minPictureWidth;
        picWidthSlider.maxValue = maxPictureWidth;
        picWidthSlider.value = picturePlacer != null ? picturePlacer.PictureWidth : 0.8f;
        picWidthSlider.targetGraphic = s.GetComponent<Image>();
        BuildSliderVisuals(picWidthSlider, s.transform);
        picWidthSlider.onValueChanged.AddListener(OnPictureWidthChanged);

        picWidthLabel = CreateText("Val", sizeCard.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-18f, 0f), new Vector2(90f, 28f),
            "0.80 m", 17, Color.white,
            TextAlignmentOptions.Right, FontStyles.Bold).GetComponent<TMP_Text>();
        picWidthLabel.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

        // Hint
        CreateText("Hint", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -590f),
            new Vector2(0f, 22f),
            "Pointe une surface avec la main GAUCHE et tire sur la gâchette",
            14, SubLabelClr, TextAlignmentOptions.Center, FontStyles.Italic);

        // Highlight initial
        HighlightFrame(currentFrameStyle);
    }

    private void BuildMediaCell(Transform parent, int index, MediaItem item)
    {
        var cell = new GameObject($"Media_{item.displayName}",
            typeof(RectTransform), typeof(Image), typeof(Button));
        cell.transform.SetParent(parent, false);
        var im = cell.GetComponent<Image>();
        im.color = CardColor;

        if (mediaButtonBgs == null || mediaButtonBgs.Length <= index)
        {
            int n = tableauxCache != null ? tableauxCache.Length : 0;
            if (mediaButtonBgs == null || mediaButtonBgs.Length != n)
                mediaButtonBgs = new Image[n];
        }
        mediaButtonBgs[index] = im;

        // Preview
        if (item.kind == MediaItem.Kind.Image && item.image != null)
        {
            var img = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(cell.transform, false);
            var iRT = img.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.05f, 0.20f);
            iRT.anchorMax = new Vector2(0.95f, 0.95f);
            iRT.offsetMin = iRT.offsetMax = Vector2.zero;
            var raw = img.GetComponent<RawImage>();
            raw.texture = item.image;
            raw.raycastTarget = false;
        }
        else
        {
            // Vidéo : pas de preview frame, placeholder + badge ▶
            var bg = new GameObject("VPreview", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(cell.transform, false);
            var iRT = bg.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.05f, 0.20f);
            iRT.anchorMax = new Vector2(0.95f, 0.95f);
            iRT.offsetMin = iRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.20f, 1f);
            bg.GetComponent<Image>().raycastTarget = false;

            CreateText("Play", bg.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                "▶", 36, new Color(0.95f, 0.75f, 0.25f),
                TextAlignmentOptions.Center, FontStyles.Bold)
                .GetComponent<RectTransform>().offsetMin = Vector2.zero;
        }

        var label = CreateText("Lbl", cell.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0.18f),
            Vector2.zero, Vector2.zero,
            (item.kind == MediaItem.Kind.Video ? "▶ " : "") + item.displayName,
            10, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var lRT = label.GetComponent<RectTransform>();
        lRT.offsetMin = new Vector2(2f, 1f); lRT.offsetMax = new Vector2(-2f, -1f);
        label.GetComponent<TMP_Text>().enableWordWrapping = true;

        cell.GetComponent<Button>().targetGraphic = im;
        cell.GetComponent<Button>().onClick.AddListener(() => OnSelectMedia(index));

        var drag = cell.AddComponent<TableauCellDragHandler>();
        drag.Init(this, index);
    }

    // ─── Musique ────────────────────────────────────────────────────

    private void BuildMusiqueTab(Transform root)
    {
        var card = CreateCard("MusicCard", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 540f));
        CreateSectionTitle(card.transform, "PISTES MUSICALES");

        musicNameLabel = CreateText("SelLbl", card.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -38f), new Vector2(0f, 24f),
            "Choisis une piste (Resources/Music/)", 14, SubLabelClr,
            TextAlignmentOptions.Center, FontStyles.Italic).GetComponent<TMP_Text>();

        AudioClip[] tracks = musicSpawner != null ? musicSpawner.Tracks : null;
        if (tracks == null || tracks.Length == 0)
        {
            CreateText("Empty", card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 30f),
                "(Pas de pistes — dépose des .mp3/.wav/.ogg dans Resources/Music/)",
                14, SubLabelClr, TextAlignmentOptions.Center, FontStyles.Italic);
        }
        else
        {
            BuildScrollableList(card.transform, tracks.Length, (i, parent) =>
                BuildMusicCell(parent, i, tracks[i]),
                gridStartYOffset: -72f, gridEndYOffset: -16f);
        }

        // Bouton PLACER (spawn boombox)
        var btnObj = new GameObject("PlaceMusic", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(root, false);
        var bRT = btnObj.GetComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -560f);
        bRT.sizeDelta = new Vector2(320f, 60f);
        btnObj.GetComponent<Image>().color = new Color(0.30f, 0.30f, 0.30f);
        musicPlaceButton = btnObj.GetComponent<Button>();
        musicPlaceButton.interactable = false;
        musicPlaceButton.onClick.AddListener(OnPlaceMusic);

        CreateText("T", btnObj.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, "PLACER LA BOOMBOX", 22, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold)
            .GetComponent<RectTransform>().offsetMin = Vector2.zero;
    }

    private void BuildMusicCell(Transform parent, int index, AudioClip clip)
    {
        var row = new GameObject($"Track_{clip.name}",
            typeof(RectTransform), typeof(Image), typeof(Button));
        row.transform.SetParent(parent, false);
        var im = row.GetComponent<Image>();
        im.color = CardColor;

        if (musicTrackBgs == null || musicTrackBgs.Length <= index)
        {
            int n = musicSpawner != null ? musicSpawner.Tracks.Length : 0;
            if (musicTrackBgs == null || musicTrackBgs.Length != n)
                musicTrackBgs = new Image[n];
        }
        musicTrackBgs[index] = im;

        CreateText("Icon", row.transform,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(18f, 0f), new Vector2(40f, 0f),
            "♪", 28, new Color(0.95f, 0.75f, 0.25f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        CreateText("Name", row.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero,
            clip.name, 18, Color.white,
            TextAlignmentOptions.Left, FontStyles.Bold)
            .GetComponent<RectTransform>().offsetMin = new Vector2(64f, 0f);

        CreateText("Dur", row.transform,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-16f, 0f), new Vector2(80f, 0f),
            FormatDuration(clip.length), 14, SubLabelClr,
            TextAlignmentOptions.Right, FontStyles.Normal)
            .GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

        row.GetComponent<Button>().targetGraphic = im;
        row.GetComponent<Button>().onClick.AddListener(() => OnSelectMusic(index));
    }

    private static string FormatDuration(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m}:{s:00}";
    }

    // ────────────────────────────────────────────────────────────────────
    //  Scrollable layouts
    // ────────────────────────────────────────────────────────────────────

    private void BuildScrollableGrid(Transform parent, int itemCount, int cols,
        System.Action<int, Transform> builder,
        float gridStartYOffset, float gridEndYOffset)
    {
        var scroll = new GameObject("Scroll",
            typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scroll.transform.SetParent(parent, false);
        var sRT = scroll.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = new Vector2(12f, 12f);
        sRT.offsetMax = new Vector2(-12f, gridStartYOffset);
        scroll.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.8f);

        var sr = scroll.GetComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scroll.transform, false);
        var vRT = viewport.GetComponent<RectTransform>();
        vRT.anchorMin = Vector2.zero; vRT.anchorMax = Vector2.one;
        vRT.offsetMin = new Vector2(4, 4); vRT.offsetMax = new Vector2(-28, -4);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vRT;

        var content = new GameObject("Content",
            typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot = new Vector2(0.5f, 1f); cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130f, 130f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sr.content = cRT;

        BuildScrollbar(scroll.transform, sr);

        for (int i = 0; i < itemCount; i++)
            builder(i, content.transform);
    }

    private void BuildScrollableList(Transform parent, int itemCount,
        System.Action<int, Transform> builder,
        float gridStartYOffset, float gridEndYOffset)
    {
        var scroll = new GameObject("ScrollList",
            typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scroll.transform.SetParent(parent, false);
        var sRT = scroll.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = new Vector2(12f, 12f);
        sRT.offsetMax = new Vector2(-12f, gridStartYOffset);
        scroll.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.8f);

        var sr = scroll.GetComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scroll.transform, false);
        var vRT = viewport.GetComponent<RectTransform>();
        vRT.anchorMin = Vector2.zero; vRT.anchorMax = Vector2.one;
        vRT.offsetMin = new Vector2(4, 4); vRT.offsetMax = new Vector2(-28, -4);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vRT;

        var content = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot = new Vector2(0.5f, 1f); cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        var v = content.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(8, 8, 8, 8);
        v.spacing = 8f;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = false;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sr.content = cRT;

        BuildScrollbar(scroll.transform, sr);

        for (int i = 0; i < itemCount; i++)
        {
            int idx = i;
            var item = new GameObject($"Item_{i}", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 58f;
            builder(idx, item.transform);
        }
    }

    private static void BuildScrollbar(Transform scrollParent, ScrollRect sr)
    {
        var sb = new GameObject("Scrollbar",
            typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sb.transform.SetParent(scrollParent, false);
        var rt = sb.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(20f, 0f);
        sb.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        var sbc = sb.GetComponent<Scrollbar>();
        sbc.direction = Scrollbar.Direction.BottomToTop;

        var sliding = new GameObject("SlidingArea", typeof(RectTransform));
        sliding.transform.SetParent(sb.transform, false);
        var slRT = sliding.GetComponent<RectTransform>();
        slRT.anchorMin = Vector2.zero; slRT.anchorMax = Vector2.one;
        slRT.offsetMin = new Vector2(3, 3); slRT.offsetMax = new Vector2(-3, -3);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliding.transform, false);
        var hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin = Vector2.zero; hRT.anchorMax = Vector2.one;
        hRT.offsetMin = hRT.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().color = new Color(0.9f, 0.7f, 0.2f);

        sbc.handleRect = hRT;
        sbc.targetGraphic = handle.GetComponent<Image>();
        sr.verticalScrollbar = sbc;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Callbacks — Graffiti
    // ────────────────────────────────────────────────────────────────────

    private void OnSelectBrush(int idx)
    {
        if (sprayPainter != null) sprayPainter.SetBrushIndex(idx);
        HighlightBrush(idx);
    }

    private void HighlightBrush(int idx)
    {
        if (brushBackgrounds == null) return;
        for (int i = 0; i < brushBackgrounds.Length; i++)
            if (brushBackgrounds[i] != null)
                brushBackgrounds[i].color = (i == idx) ? CardActive : CardColor;
        if (brushNameLabel != null && sprayPainter != null
            && sprayPainter.Brushes != null && idx < sprayPainter.Brushes.Length
            && sprayPainter.Brushes[idx] != null)
            brushNameLabel.text = $"Pinceau : {sprayPainter.Brushes[idx].name}";
    }

    private void OnSelectColor(Color c)
    {
        currentColor = c;
        if (sprayPainter != null) sprayPainter.SetColor(c);
        UpdateColorPreview();
    }

    private void OnSelectFavorite(int idx)
    {
        if (favoriteColors[idx].a > 0f) OnSelectColor(favoriteColors[idx]);
    }

    private void AddCurrentToFavorites()
    {
        for (int i = favoritesCount - 1; i > 0; i--)
            favoriteColors[i] = favoriteColors[i - 1];
        favoriteColors[0] = currentColor;
        SaveFavorites();
        RefreshFavoriteSwatches();
    }

    private void RefreshFavoriteSwatches()
    {
        if (favoriteSwatches == null) return;
        for (int i = 0; i < favoriteSwatches.Length; i++)
            if (favoriteSwatches[i] != null)
                favoriteSwatches[i].color = favoriteColors[i].a > 0f
                    ? favoriteColors[i]
                    : new Color(0.15f, 0.12f, 0.20f, 1f);
    }

    private void OnSizeChanged(float v)
    {
        if (sprayPainter != null) sprayPainter.SetStampSize(v);
        if (sizeLabel != null) sizeLabel.text = $"{v:0.00} m";
    }

    private void UpdateColorPreview()
    {
        if (colorPreviewImg != null) colorPreviewImg.color = currentColor;
        if (colorPreviewHex != null) colorPreviewHex.text = "#" + ColorUtility.ToHtmlStringRGB(currentColor);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Callbacks — Objets
    // ────────────────────────────────────────────────────────────────────

    private void OnSelectObject(int idx)
    {
        selectedObjectIndex = idx;
        if (objectSpawner != null) objectSpawner.SetSelectedIndex(idx);
        if (objectButtonBgs != null)
            for (int i = 0; i < objectButtonBgs.Length; i++)
                if (objectButtonBgs[i] != null)
                    objectButtonBgs[i].color = (i == idx) ? CardActive : CardColor;
        if (objectNameLabel != null && objectSpawner != null
            && idx < objectSpawner.SpawnableItems.Length)
        {
            objectNameLabel.text = $"Sélectionné : {objectSpawner.SpawnableItems[idx].name}";
            objectNameLabel.color = TitleClr;
        }
        if (objectPlaceButton != null)
        {
            objectPlaceButton.interactable = true;
            var im = objectPlaceButton.GetComponent<Image>();
            if (im != null) im.color = ButtonGreen;
        }
    }

    private void OnPlaceObject()
    {
        if (selectedObjectIndex < 0 || objectSpawner == null) return;
        objectSpawner.SpawnItem(selectedObjectIndex);
        if (objectNameLabel != null)
        {
            objectNameLabel.text = $"{objectSpawner.SpawnableItems[selectedObjectIndex].name} placé !";
            objectNameLabel.color = new Color(0.30f, 1f, 0.40f);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Callbacks — Tableaux
    // ────────────────────────────────────────────────────────────────────

    private void OnSelectMedia(int idx)
    {
        selectedMediaIndex = idx;
        if (picturePlacer != null) picturePlacer.SetMediaIndex(idx);
        if (mediaButtonBgs != null)
            for (int i = 0; i < mediaButtonBgs.Length; i++)
                if (mediaButtonBgs[i] != null)
                    mediaButtonBgs[i].color = (i == idx) ? CardActive : CardColor;
        if (mediaNameLabel != null && tableauxCache != null && idx < tableauxCache.Length)
        {
            var m = tableauxCache[idx];
            string kind = m.kind == MediaItem.Kind.Video ? "Vidéo" : "Image";
            mediaNameLabel.text = $"{kind} : {m.displayName}";
            mediaNameLabel.color = TitleClr;
        }
    }

    private MediaItem[] LoadTableauxMedia()
    {
        if (picturePlacer != null && picturePlacer.Media != null && picturePlacer.Media.Length > 0)
            return picturePlacer.Media;

        var imgs = Resources.LoadAll<Texture2D>("tableaux");
        var vids = Resources.LoadAll<UnityEngine.Video.VideoClip>("tableaux");
        System.Array.Sort(imgs, (a, b) => string.Compare(a.name, b.name));
        System.Array.Sort(vids, (a, b) => string.Compare(a.name, b.name));

        var list = new List<MediaItem>(imgs.Length + vids.Length);
        foreach (var t in imgs)
            list.Add(new MediaItem { kind = MediaItem.Kind.Image, image = t, displayName = t.name });
        foreach (var v in vids)
            list.Add(new MediaItem { kind = MediaItem.Kind.Video, video = v, displayName = v.name });
        return list.ToArray();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Drag & drop tableaux
    // ────────────────────────────────────────────────────────────────────

    public void BeginTableauDrag(int cellIndex, PointerEventData eventData)
    {
        if (tableauxCache == null || cellIndex < 0 || cellIndex >= tableauxCache.Length)
        {
            Debug.LogWarning($"[PaintMenuUI] BeginTableauDrag refusé : cache vide ou index {cellIndex} hors bornes.");
            return;
        }
        if (picturePlacer == null)
        {
            Debug.LogWarning("[PaintMenuUI] BeginTableauDrag : PicturePlacer manquant.");
            return;
        }
        if (isDraggingTableau) CancelDrag();

        draggingIndex = cellIndex;
        OnSelectMedia(cellIndex);
        draggingInteractor = ResolveInteractor(eventData);

        Transform anchor = GetAnchorTransform();
        if (anchor == null)
        {
            Debug.LogWarning("[PaintMenuUI] BeginTableauDrag : aucune ancre (ni XRRayInteractor ni Camera.main).");
            return;
        }

        Vector3 spawnPos = anchor.position + anchor.forward * dragHoldDistance;
        Vector3 spawnNormal = -anchor.forward;

        draggedTableau = picturePlacer.SpawnFloating(tableauxCache[cellIndex], spawnPos, spawnNormal);
        if (draggedTableau == null) return;

        // Désactive le grab natif XR pendant qu'on contrôle manuellement le tableau.
        var grab = draggedTableau.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
        var col = draggedTableau.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        draggedTableau.transform.SetParent(anchor, true);
        // Réduit la taille pendant qu'on le tient en main.
        draggedTableau.transform.localScale = Vector3.one * dragHoldScale;
        isDraggingTableau = true;

        Debug.Log($"[PaintMenuUI] BeginTableauDrag idx={cellIndex} item={tableauxCache[cellIndex].displayName} anchor={anchor.name}");
    }

    public void EndTableauDrag(int cellIndex, PointerEventData eventData)
    {
        if (!isDraggingTableau || draggedTableau == null)
        {
            isDraggingTableau = false;
            draggedTableau = null;
            draggingInteractor = null;
            return;
        }

        // Reparenter au container "Tableaux" (pas null) pour que la persistance Play→Edit le capture.
        Transform tableauxParent = picturePlacer != null ? picturePlacer.TableauxParent : null;
        draggedTableau.transform.SetParent(tableauxParent, true);

        Transform anchor = GetAnchorTransform();
        bool snapped = false;
        if (anchor != null && Physics.Raycast(anchor.position, anchor.forward,
                out RaycastHit hit, dragMaxDistance, dragRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.normal.y) < dragWallNormalThreshold)
            {
                Vector3 up = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > 0.99f
                    ? Vector3.forward : Vector3.up;
                draggedTableau.transform.position = hit.point + hit.normal * 0.002f;
                draggedTableau.transform.rotation = Quaternion.LookRotation(hit.normal, up);
                snapped = true;
            }
        }

        if (snapped)
        {
            // Restaure la taille réelle après le snap au mur.
            draggedTableau.transform.localScale = Vector3.one;

            var col = draggedTableau.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            var grab = draggedTableau.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = true;

            var snapper = draggedTableau.GetComponent<TableauGrabSnapper>();
            if (snapper != null) snapper.InitAnchor(draggedTableau.transform.position, draggedTableau.transform.rotation);
        }
        else
        {
            Destroy(draggedTableau);
        }

        draggedTableau = null;
        isDraggingTableau = false;
        draggingInteractor = null;
    }

    private void CancelDrag()
    {
        if (draggedTableau != null) Destroy(draggedTableau);
        draggedTableau = null;
        isDraggingTableau = false;
        draggingIndex = -1;
        draggingInteractor = null;
    }

    private Transform GetAnchorTransform()
    {
        if (draggingInteractor != null)
        {
            return draggingInteractor.rayOriginTransform != null
                ? draggingInteractor.rayOriginTransform
                : draggingInteractor.transform;
        }
        return Camera.main != null ? Camera.main.transform : null;
    }

    private void Update()
    {
        // Le tableau suit naturellement le contrôleur via le parenting. Rien à faire ici
        // côté positionnement, mais on garde la possibilité d'ajouter des animations
        // ou de la mise à jour visuelle plus tard.
    }

    private XRRayInteractor ResolveInteractor(PointerEventData eventData)
    {
        var interactors = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
        if (interactors == null || interactors.Length == 0)
        {
            Debug.LogWarning("[PaintMenuUI] Aucun XRRayInteractor dans la scène. Le fantôme suivra la caméra.");
            return null;
        }

        GameObject target = eventData != null ? eventData.pointerCurrentRaycast.gameObject : null;

        // 1) Celui dont le UI raycast pointe sur la cellule pressée.
        foreach (var inter in interactors)
        {
            if (inter.TryGetCurrentUIRaycastResult(out var result) && result.gameObject != null)
            {
                if (target != null && result.gameObject == target) return inter;
            }
        }

        // 2) N'importe lequel qui pointe actuellement vers de l'UI.
        foreach (var inter in interactors)
        {
            if (inter.TryGetCurrentUIRaycastResult(out _)) return inter;
        }

        // 3) Fallback : premier interactor trouvé.
        return interactors[0];
    }


    private void OnSelectFrame(FrameStyle f)
    {
        currentFrameStyle = f;
        if (picturePlacer != null) picturePlacer.SetFrame(f);
        HighlightFrame(f);
    }

    private void HighlightFrame(FrameStyle f)
    {
        if (frameStyleBgs == null) return;
        for (int i = 0; i < frameStyleBgs.Length; i++)
            if (frameStyleBgs[i] != null)
                frameStyleBgs[i].color = (i == (int)f) ? CardActive : CardColor;
    }

    private void OnPictureWidthChanged(float v)
    {
        if (picturePlacer != null) picturePlacer.SetPictureWidth(v);
        if (picWidthLabel != null) picWidthLabel.text = $"{v:0.00} m";
    }

    // ────────────────────────────────────────────────────────────────────
    //  Callbacks — Musique
    // ────────────────────────────────────────────────────────────────────

    private void OnSelectMusic(int idx)
    {
        selectedMusicIndex = idx;
        if (musicSpawner != null) musicSpawner.SetTrackIndex(idx);
        if (musicTrackBgs != null)
            for (int i = 0; i < musicTrackBgs.Length; i++)
                if (musicTrackBgs[i] != null)
                    musicTrackBgs[i].color = (i == idx) ? CardActive : CardColor;
        if (musicNameLabel != null && musicSpawner != null
            && idx < musicSpawner.Tracks.Length)
        {
            musicNameLabel.text = $"Sélectionnée : {musicSpawner.Tracks[idx].name}";
            musicNameLabel.color = TitleClr;
        }
        if (musicPlaceButton != null)
        {
            musicPlaceButton.interactable = true;
            var im = musicPlaceButton.GetComponent<Image>();
            if (im != null) im.color = ButtonGreen;
        }
    }

    private void OnPlaceMusic()
    {
        if (selectedMusicIndex < 0 || musicSpawner == null) return;
        musicSpawner.SpawnSelected();
        if (musicNameLabel != null)
        {
            musicNameLabel.text = $"Boombox '{musicSpawner.Tracks[selectedMusicIndex].name}' placée !";
            musicNameLabel.color = new Color(0.30f, 1f, 0.40f);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Toggle menu
    // ────────────────────────────────────────────────────────────────────

    private void OnToggleMenu(InputAction.CallbackContext ctx)
    {
        if (menuPanel == null) return;
        bool opening = !menuPanel.activeSelf;

        // Si on ferme le menu en plein drag de tableau, on annule le drag pour
        // ne pas se retrouver avec un tableau collé dans la main indéfiniment.
        if (!opening && isDraggingTableau) CancelDrag();

        menuPanel.SetActive(opening);
        if (GameManager.Instance != null)
        {
            if (opening) GameManager.Instance.OpenMenu();
            else GameManager.Instance.CloseMenu();
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Favorites persistence
    // ────────────────────────────────────────────────────────────────────

    private void LoadFavorites()
    {
        string s = PlayerPrefs.GetString(playerPrefsKey, "");
        if (string.IsNullOrEmpty(s)) return;
        string[] parts = s.Split(';');
        for (int i = 0; i < favoritesCount && i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            if (ColorUtility.TryParseHtmlString("#" + parts[i], out Color c))
                favoriteColors[i] = c;
        }
    }

    private void SaveFavorites()
    {
        var list = new List<string>();
        for (int i = 0; i < favoritesCount; i++)
            list.Add(favoriteColors[i].a > 0f
                ? ColorUtility.ToHtmlStringRGB(favoriteColors[i])
                : "");
        PlayerPrefs.SetString(playerPrefsKey, string.Join(";", list));
        PlayerPrefs.Save();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────

    private static GameObject CreateCard(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var card = new GameObject(name, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        card.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.08f, 0.85f);
        return card;
    }

    private static GameObject CreateSectionTitle(Transform parent, string text)
    {
        return CreateText("Title", parent,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -14f), new Vector2(320f, 22f),
            text, 13, LabelClr,
            TextAlignmentOptions.Left, FontStyles.Bold);
    }

    private static GameObject CreateText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center,
        FontStyles style = FontStyles.Normal)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        return obj;
    }
}
