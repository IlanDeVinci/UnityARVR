using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// VR Spawn Menu - displayed as a World Space Canvas attached to the left hand
/// or floating in front of the player.
/// Toggle with a controller button (e.g. Menu / Y button).
/// </summary>
public class SpawnMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;

    [Header("Input")]
    [SerializeField] private InputActionReference menuToggleAction;

    [Header("Visual")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color normalColor = Color.white;

    private Button[] buttons;

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
        if (menuPanel != null)
            menuPanel.SetActive(false);
        BuildMenu();
    }

    private void BuildMenu()
    {
        if (objectSpawner == null) return;

        SpawnableItem[] items = objectSpawner.SpawnableItems;
        if (items == null || items.Length == 0) return;

        buttons = new Button[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            GameObject btnObj;

            if (buttonPrefab != null)
            {
                btnObj = Instantiate(buttonPrefab, buttonContainer);
            }
            else
            {
                btnObj = new GameObject($"Btn_{items[i].name}", typeof(RectTransform), typeof(Button), typeof(Image));
                btnObj.transform.SetParent(buttonContainer, false);

                // Add text child
                GameObject textObj = new GameObject("Text", typeof(RectTransform));
                textObj.transform.SetParent(btnObj.transform, false);
                TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = items[i].name;
                tmp.fontSize = 14;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.black;
                RectTransform textRT = textObj.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = textRT.offsetMax = Vector2.zero;
            }

            TMP_Text text = btnObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = items[i].name;

            Image icon = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && items[i].icon != null)
                icon.sprite = items[i].icon;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectItem(index));
            buttons[i] = btn;
        }

        UpdateSelection();
    }

    private void SelectItem(int index)
    {
        objectSpawner.SetSelectedIndex(index);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            Image img = buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == objectSpawner.SelectedIndex) ? selectedColor : normalColor;
        }
    }

    private void OnToggleMenu(InputAction.CallbackContext ctx)
    {
        if (menuPanel == null) return;
        bool opening = !menuPanel.activeSelf;
        menuPanel.SetActive(opening);

        if (GameManager.Instance != null)
        {
            if (opening)
                GameManager.Instance.OpenMenu();
            else
                GameManager.Instance.CloseMenu();
        }
    }
}
