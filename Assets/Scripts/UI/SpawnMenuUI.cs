using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Menu de spawn VR amélioré : grid scrollable avec tous les objets disponibles.
/// Le menu se construit automatiquement au Start() à partir des objets
/// chargés dans ObjectSpawner.
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
    [SerializeField] private float cellWidth = 140f;
    [SerializeField] private float cellHeight = 100f;
    [SerializeField] private float spacing = 12f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(0.9f, 0.7f, 0.2f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.5f, 0.7f, 0.95f);

    private Button[] buttons;
    private TMP_Text selectedLabel;

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
            BuildMenuFromScratch();
        else
            menuPanel.SetActive(false);

        BuildGrid();
    }

    /// <summary>
    /// Construit un menu complet depuis zéro si aucun n'est assigné.
    /// </summary>
    private void BuildMenuFromScratch()
    {
        // Configuration du Canvas
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;

        if (GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();

        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var gr = GetComponent<GraphicRaycaster>();
        if (gr != null && gr.GetType() == typeof(GraphicRaycaster))
            Destroy(gr);

        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900f, 700f);

        // Panel
        menuPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        menuPanel.transform.SetParent(transform, false);
        RectTransform panelRT = menuPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        menuPanel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.12f, 0.95f);

        // Titre
        CreateText("Title", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 45f),
            "MENU - Restaurant Pokemon", 32, new Color(1f, 0.85f, 0.3f));

        // Label de l'objet sélectionné
        var labelObj = CreateText("SelectedLabel", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(700f, 30f),
            "Aucun objet sélectionné", 20, new Color(0.8f, 0.8f, 0.8f));
        selectedLabel = labelObj.GetComponent<TMP_Text>();

        // Instructions bas de page
        CreateText("Hint", menuPanel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(800f, 30f),
            "Cliquez sur un objet pour le spawner devant vous", 16, new Color(0.55f, 0.55f, 0.55f));

        menuPanel.SetActive(false);
    }

    private void BuildGrid()
    {
        if (objectSpawner == null)
            objectSpawner = FindAnyObjectByType<ObjectSpawner>();
        if (objectSpawner == null || menuPanel == null) return;

        SpawnableItem[] items = objectSpawner.SpawnableItems;
        if (items == null || items.Length == 0) return;

        // Container scrollable
        GameObject scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scroll.transform.SetParent(menuPanel.transform, false);
        RectTransform scrollRT = scroll.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.05f, 0.1f);
        scrollRT.anchorMax = new Vector2(0.95f, 0.82f);
        scrollRT.offsetMin = scrollRT.offsetMax = Vector2.zero;
        scroll.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.08f, 0.7f);

        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport (masque)
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = vpRT;

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;

        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset(15, 15, 15, 15);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = contentRT;

        // Créer les boutons
        buttons = new Button[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            int index = i; // capture
            GameObject btnObj = new GameObject($"Btn_{items[i].name}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(content.transform, false);

            var img = btnObj.GetComponent<Image>();
            img.color = normalColor;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = hoverColor;
            colors.pressedColor = selectedColor;
            colors.selectedColor = selectedColor;
            btn.colors = colors;
            btn.onClick.AddListener(() => OnItemSelected(index));
            buttons[i] = btn;

            // Texte du bouton
            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(4, 4);
            txtRT.offsetMax = new Vector2(-4, -4);
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = items[i].name;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = true;
        }
    }

    private void OnItemSelected(int index)
    {
        if (objectSpawner == null) return;
        objectSpawner.SetSelectedIndex(index);

        // Spawn immédiat
        objectSpawner.SpawnItem(index);

        // Feedback visuel
        if (selectedLabel != null && objectSpawner.SpawnableItems != null
            && index < objectSpawner.SpawnableItems.Length)
        {
            selectedLabel.text = $"Spawné : {objectSpawner.SpawnableItems[index].name}";
        }

        UpdateButtonHighlight(index);
    }

    private void UpdateButtonHighlight(int selectedIndex)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            var img = buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == selectedIndex) ? selectedColor : normalColor;
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
