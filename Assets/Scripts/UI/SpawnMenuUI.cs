using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Menu de spawn VR avec previews 3D de chaque modèle.
/// Flow : clic sur un item → sélection (highlight) → clic sur PLACER → spawn.
/// </summary>
public class SpawnMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private ObjectSpawner objectSpawner;

    [Header("Input")]
    [SerializeField] private InputActionReference menuToggleAction;

    [Header("Grid")]
    [SerializeField] private int columns = 4;
    [SerializeField] private float cellSize = 130f;
    [SerializeField] private float spacing = 10f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.22f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.5f, 0.7f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.65f, 0.15f, 1f);

    private Button[] buttons;
    private Image[] buttonBackgrounds;
    private TMP_Text selectedLabel;
    private Button placeButton;
    private int selectedIndex = -1;

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
    }

    private void Start()
    {
        if (menuPanel == null)
        {
            BuildMenuFromScratch();
        }
        else
        {
            // Nettoyer l'existant et le redimensionner pour le nouveau layout
            PrepareExistingPanel();
        }

        BuildGrid();
    }

    /// <summary>
    /// Si le menuPanel existe déjà dans la scène, on le nettoie et le redimensionne.
    /// </summary>
    private void PrepareExistingPanel()
    {
        // Supprimer tous les enfants de l'ancien menu
        for (int i = menuPanel.transform.childCount - 1; i >= 0; i--)
            Destroy(menuPanel.transform.GetChild(i).gameObject);

        // Redimensionner et recentrer le panel
        RectTransform panelRT = menuPanel.GetComponent<RectTransform>();
        if (panelRT != null)
        {
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(800f, 780f);
        }

        // Aussi redimensionner le Canvas parent s'il est plus petit
        var canvas = menuPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRT = canvas.GetComponent<RectTransform>();
            if (canvasRT != null)
            {
                canvasRT.sizeDelta = new Vector2(800f, 780f);
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    Vector3 s = canvasRT.localScale;
                    if (s.x < 0.001f || s.x > 0.005f)
                        canvasRT.localScale = Vector3.one * 0.002f;
                }
            }
        }

        // S'assurer que l'image de fond couvre tout le panel
        var panelImg = menuPanel.GetComponent<Image>();
        if (panelImg != null)
            panelImg.color = new Color(0.06f, 0.04f, 0.1f, 0.97f);

        // Ajouter titre, label, bouton placer
        AddMenuHeader();
        AddPlaceButton();

        menuPanel.SetActive(false);
    }

    private void AddMenuHeader()
    {
        CreateText("Title", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 45f),
            "CHOISIR UN OBJET", 30, new Color(1f, 0.85f, 0.3f));

        var labelObj = CreateText("SelectedLabel", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(700f, 28f),
            "Sélectionne un objet dans la liste", 18, new Color(0.75f, 0.75f, 0.75f));
        selectedLabel = labelObj.GetComponent<TMP_Text>();

        AddSensorPanel();
    }

    /// <summary>
    /// Ajoute un panneau avec les données capteur (Chain API MIT) en haut à droite.
    /// </summary>
    private void AddSensorPanel()
    {
        GameObject panel = new GameObject("SensorPanel",
            typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(menuPanel.transform, false);

        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-15f, -15f);
        panelRT.sizeDelta = new Vector2(270f, 180f);

        var img = panel.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.08f, 0.1f, 0.18f, 0.92f);

        // Outline directement sur l'image du panel
        var outline = panel.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.25f, 0.8f);
        outline.effectDistance = new Vector2(2, -2);

        // Texte dans le panel
        GameObject txtObj = new GameObject("SensorText", typeof(RectTransform));
        txtObj.transform.SetParent(panel.transform, false);
        RectTransform txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8, 6);
        txtRT.offsetMax = new Vector2(-8, -6);

        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Chargement...";
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        // Attacher le fetcher et lier le texte
        var fetcher = panel.AddComponent<SensorDataFetcher>();
        fetcher.displayText = tmp;
    }

    private void AddPlaceButton()
    {
        GameObject placeObj = new GameObject("PlaceButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        placeObj.transform.SetParent(menuPanel.transform, false);
        RectTransform placeRT = placeObj.GetComponent<RectTransform>();
        placeRT.anchorMin = new Vector2(0.5f, 0f);
        placeRT.anchorMax = new Vector2(0.5f, 0f);
        placeRT.anchoredPosition = new Vector2(0f, 45f);
        placeRT.sizeDelta = new Vector2(260f, 60f);
        placeObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

        placeButton = placeObj.GetComponent<Button>();
        placeButton.interactable = false;
        placeButton.onClick.AddListener(OnPlaceClicked);

        GameObject placeTxtObj = new GameObject("Text", typeof(RectTransform));
        placeTxtObj.transform.SetParent(placeObj.transform, false);
        RectTransform placeTxtRT = placeTxtObj.GetComponent<RectTransform>();
        placeTxtRT.anchorMin = Vector2.zero;
        placeTxtRT.anchorMax = Vector2.one;
        placeTxtRT.offsetMin = placeTxtRT.offsetMax = Vector2.zero;
        var placeTxt = placeTxtObj.AddComponent<TextMeshProUGUI>();
        placeTxt.text = "PLACER";
        placeTxt.fontSize = 28;
        placeTxt.alignment = TextAlignmentOptions.Center;
        placeTxt.color = Color.white;
        placeTxt.fontStyle = FontStyles.Bold;
    }

    private void BuildMenuFromScratch()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;

        if (GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();

        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var gr = GetComponent<GraphicRaycaster>();
        if (gr != null && gr.GetType() == typeof(GraphicRaycaster))
            Destroy(gr);

        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1100f, 820f);

        // Panel de fond
        menuPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        menuPanel.transform.SetParent(transform, false);
        RectTransform panelRT = menuPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        menuPanel.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.1f, 0.97f);

        AddMenuHeader();
        AddPlaceButton();

        menuPanel.SetActive(false);
    }

    private void BuildGrid()
    {
        if (objectSpawner == null)
            objectSpawner = FindAnyObjectByType<ObjectSpawner>();
        if (objectSpawner == null || menuPanel == null) return;

        SpawnableItem[] items = objectSpawner.SpawnableItems;
        if (items == null || items.Length == 0) return;

        // Scroll container
        GameObject scroll = new GameObject("Scroll",
            typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scroll.transform.SetParent(menuPanel.transform, false);
        RectTransform scrollRT = scroll.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.03f, 0.16f);
        scrollRT.anchorMax = new Vector2(0.97f, 0.84f);
        scrollRT.offsetMin = scrollRT.offsetMax = Vector2.zero;
        scroll.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.8f);

        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport (masque) — laisse 30 px à droite pour la scrollbar
        GameObject viewport = new GameObject("Viewport",
            typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(5, 5);
        vpRT.offsetMax = new Vector2(-35, -5); // -35 pour laisser place à la scrollbar
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = vpRT;

        // Content (grid)
        GameObject content = new GameObject("Content",
            typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;

        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = contentRT;

        // Scrollbar verticale visible à droite
        GameObject scrollbar = new GameObject("Scrollbar",
            typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbar.transform.SetParent(scroll.transform, false);
        RectTransform sbRT = scrollbar.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(1f, 0.5f);
        sbRT.anchoredPosition = Vector2.zero;
        sbRT.sizeDelta = new Vector2(25, 0);
        scrollbar.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        var sb = scrollbar.GetComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;

        GameObject sliding = new GameObject("SlidingArea", typeof(RectTransform));
        sliding.transform.SetParent(scrollbar.transform, false);
        RectTransform sldRT = sliding.GetComponent<RectTransform>();
        sldRT.anchorMin = Vector2.zero;
        sldRT.anchorMax = Vector2.one;
        sldRT.offsetMin = new Vector2(3, 3);
        sldRT.offsetMax = new Vector2(-3, -3);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliding.transform, false);
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin = Vector2.zero;
        hRT.anchorMax = Vector2.one;
        hRT.offsetMin = hRT.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().color = new Color(0.9f, 0.7f, 0.2f, 1f);

        sb.handleRect = hRT;
        sb.targetGraphic = handle.GetComponent<Image>();
        scrollRect.verticalScrollbar = sb;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Boutons avec preview 3D
        buttons = new Button[items.Length];
        buttonBackgrounds = new Image[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            GameObject btnObj = new GameObject($"Btn_{items[i].name}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(content.transform, false);

            var img = btnObj.GetComponent<Image>();
            img.color = normalColor;
            buttonBackgrounds[i] = img;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnItemSelected(index));
            buttons[i] = btn;

            // Generate preview texture
            Texture2D preview = ModelPreviewGenerator.Generate(items[i].prefab, 128);

            // Image de preview (prend ~75% de la hauteur)
            GameObject imgObj = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            imgObj.transform.SetParent(btnObj.transform, false);
            RectTransform imgRT = imgObj.GetComponent<RectTransform>();
            imgRT.anchorMin = new Vector2(0.05f, 0.25f);
            imgRT.anchorMax = new Vector2(0.95f, 0.98f);
            imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
            var raw = imgObj.GetComponent<RawImage>();
            raw.texture = preview;
            raw.raycastTarget = false; // clicks passent au bouton parent

            // Label nom en bas
            GameObject txtObj = new GameObject("Name", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0f, 0f);
            txtRT.anchorMax = new Vector2(1f, 0.22f);
            txtRT.offsetMin = new Vector2(4, 2);
            txtRT.offsetMax = new Vector2(-4, -2);
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = items[i].name;
            tmp.fontSize = 11;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
        }
    }

    private void OnItemSelected(int index)
    {
        if (objectSpawner == null) return;

        selectedIndex = index;
        objectSpawner.SetSelectedIndex(index);

        if (selectedLabel != null && objectSpawner.SpawnableItems != null
            && index < objectSpawner.SpawnableItems.Length)
        {
            selectedLabel.text = $"Sélectionné : {objectSpawner.SpawnableItems[index].name}";
            selectedLabel.color = new Color(1f, 0.85f, 0.3f);
        }

        // Activer le bouton PLACER
        if (placeButton != null)
        {
            placeButton.interactable = true;
            var placeImg = placeButton.GetComponent<Image>();
            if (placeImg != null) placeImg.color = new Color(0.2f, 0.7f, 0.3f);
        }

        UpdateButtonHighlight(index);
    }

    private void OnPlaceClicked()
    {
        if (selectedIndex < 0 || objectSpawner == null) return;
        objectSpawner.SpawnItem(selectedIndex);

        if (selectedLabel != null)
        {
            string name = objectSpawner.SpawnableItems[selectedIndex].name;
            selectedLabel.text = $"{name} placé !";
            selectedLabel.color = new Color(0.3f, 1f, 0.4f);
        }
    }

    private void UpdateButtonHighlight(int selectedIdx)
    {
        if (buttonBackgrounds == null) return;
        for (int i = 0; i < buttonBackgrounds.Length; i++)
        {
            if (buttonBackgrounds[i] != null)
                buttonBackgrounds[i].color = (i == selectedIdx) ? selectedColor : normalColor;
        }
    }

    private void OnToggleMenu(InputAction.CallbackContext ctx)
    {
        if (menuPanel == null) return;
        bool opening = !menuPanel.activeSelf;
        menuPanel.SetActive(opening);

        if (GameManager.Instance != null)
        {
            if (opening) GameManager.Instance.OpenMenu();
            else GameManager.Instance.CloseMenu();
        }
    }

    private static GameObject CreateText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        return obj;
    }
}
